#!/usr/bin/env python3
"""TRIPLY — ExchangeRate seeder (feeds the `ExchangeRates` table).

Upserts one row per currency into `ExchangeRates`, matched by
Currency.IsoCode (a natural key) — never a numeric CurrencyId — for the same
reason `seed_places.py` and `seed_place_interests.py` avoid numeric ids:
EUR's actual database CurrencyId depends on insertion order/history on each
database and cannot be assumed or hard-coded.

Deliberately NOT a scheduled job: Triply has no background-job infrastructure
today (see the note in Entities/ExchangeRate.cs). Rates below are manually
maintained placeholders — update RATES_TO_USD and re-run this script whenever
they need refreshing. Re-running is always safe: existing rows are UPDATEd in
place (RateToUsd + UpdatedAt), nothing is duplicated or deleted.

Usage (from AI/01-Dataset/seed/, same venv as the other seeders):
    python seed_exchange_rates.py --connection-string "..."
    python seed_exchange_rates.py --connection-string "..." --dry-run

Requires the `ExchangeRates` table to already exist — run the Backend's EF
Core migration first (dotnet ef database update), same prerequisite as
seed_places.py needs `Places`.
"""

from __future__ import annotations

import argparse
import sys
from datetime import datetime, timezone
from pathlib import Path

try:
    import pyodbc  # noqa: F401
except ImportError:
    print("pyodbc is required: pip install -r requirements.txt", file=sys.stderr)
    sys.exit(1)

from seed_places import open_connection, table_exists  # noqa: E402

# How many USD one unit of the currency is worth. MANUALLY MAINTAINED — update
# and re-run when these drift; there is no live-rate fetch by design (see
# module docstring above).
RATES_TO_USD: dict[str, float] = {
    "USD": 1.00,
    "JOD": 1.41,
    "EUR": 1.08,
}


def require_tables(cursor: "pyodbc.Cursor") -> None:
    missing = [t for t in ("Currencies", "ExchangeRates") if not table_exists(cursor, t)]
    if missing:
        raise SystemExit(
            f"Missing tables {missing} — apply the Backend EF Core migration first "
            "(dotnet ef database update), then re-run this seeder."
        )


def fetch_currency_ids(cursor: "pyodbc.Cursor") -> dict[str, int]:
    cursor.execute("SELECT IsoCode, Id FROM Currencies")
    return {row[0]: int(row[1]) for row in cursor.fetchall()}


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Seed/refresh ExchangeRates (rate-to-USD per Currency) into SQL Server."
    )
    parser.add_argument("--connection-string", required=True)
    parser.add_argument("--driver", default="ODBC Driver 18 for SQL Server")
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Validate and print the planned changes without writing.",
    )
    args = parser.parse_args()

    unknown = set(RATES_TO_USD) - {"USD", "JOD", "EUR"}  # sanity check on this file itself
    if unknown:
        print(f"Unexpected currency codes in RATES_TO_USD: {unknown}", file=sys.stderr)
        return 1

    try:
        connection = open_connection(args.connection_string, args.driver)
    except SystemExit as exc:
        print(f"Connection failed: {exc}", file=sys.stderr)
        return 1

    inserted = 0
    updated = 0
    skipped_unresolved = 0
    now = datetime.now(timezone.utc)

    with connection:
        cursor = connection.cursor()
        require_tables(cursor)

        currency_ids = fetch_currency_ids(cursor)

        for iso_code, rate in RATES_TO_USD.items():
            currency_id = currency_ids.get(iso_code)
            if currency_id is None:
                print(
                    f"Skipping {iso_code}: no matching row in Currencies "
                    "(seed Currency.csv via seed_places.py first)",
                    file=sys.stderr,
                )
                skipped_unresolved += 1
                continue

            cursor.execute("SELECT 1 FROM ExchangeRates WHERE CurrencyId = ?", currency_id)
            exists = cursor.fetchone() is not None

            if args.dry_run:
                action = "update" if exists else "insert"
                print(f"  [dry-run] would {action} {iso_code} -> RateToUsd={rate}")
                if exists:
                    updated += 1
                else:
                    inserted += 1
                continue

            if exists:
                cursor.execute(
                    "UPDATE ExchangeRates SET RateToUsd = ?, UpdatedAt = ? WHERE CurrencyId = ?",
                    rate,
                    now,
                    currency_id,
                )
                updated += 1
            else:
                cursor.execute(
                    "INSERT INTO ExchangeRates (CurrencyId, RateToUsd, UpdatedAt) VALUES (?, ?, ?)",
                    currency_id,
                    rate,
                    now,
                )
                inserted += 1

    prefix = "[dry-run] " if args.dry_run else ""
    print(f"{prefix}ExchangeRates: {inserted} inserted, {updated} updated, "
          f"{skipped_unresolved} skipped (unresolved currency)")
    print("Dry-run complete — no changes were written." if args.dry_run else "Seed complete.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
