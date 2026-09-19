#!/usr/bin/env python3
"""TRIPLY — Place↔Interest seeder (feeds the `PlaceInterests` junction table).

Loads `curated-data/PlaceInterest_seed_draft.csv` and links each row to the
already-seeded `Places` and `InterestCategories` rows via NATURAL KEYS
(Destination.Name + Place.Name, InterestCategory.Code) — never a CSV numeric
id, for the same reason `seed_places.py` avoids it: the CSV's ids have no
relationship to the database's actual identity values.

This script is ADDITIVE and IDEMPOTENT: it only INSERTs a (PlaceId,
InterestCategoryId) pair if that exact pair does not already exist — same
pattern as the manual `INSERT ... WHERE NOT EXISTS` blocks already in
`Backend/Triply.Api/Data/seed-dataset.sql`. It never deletes or updates a
row, so re-running it is always safe.

Deliberately separate from `seed_places.py` and unrelated to
`Extra_AI_Context.csv` / `budget_tier`:
  - Does not touch the `Places`, `Destinations`, or any other table that
    `seed_places.py` or `DatasetContextService` (BUDGET_FIRST) depend on.
  - Only INSERTs into `PlaceInterests`, so `DestinationSuggestionService`
    (`_db.PlaceInterests`) is the only runtime code path affected. Running
    this script cannot change BUDGET_FIRST generation behaviour.

Usage (from AI/01-Dataset/seed/):
    pip install -r requirements.txt
    python seed_place_interests.py --connection-string "Server=...;Database=...;..."
    python seed_place_interests.py --dry-run          # plan only, no writes

Exit codes: 0 = success, 1 = validation or runtime failure.
"""

from __future__ import annotations

import argparse
import csv
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Tuple

try:
    import pyodbc  # noqa: F401  (imported by seed_places; re-checked here for a clear error)
except ImportError:  # pragma: no cover
    print("pyodbc is required: pip install -r requirements.txt", file=sys.stderr)
    sys.exit(1)

# Reuse the already-tested connection/driver-detection logic instead of
# duplicating it — keeps both seeders behaving identically against the DB.
from seed_places import SCALAR, open_connection, table_exists  # noqa: E402

SCRIPT_DIR = Path(__file__).resolve().parent
DATA_DIR = SCRIPT_DIR.parent / "curated-data"

EXPECTED_INTEREST_CATEGORIES = {
    "NATURE": "Nature",
    "HISTORY": "History",
    "FOOD": "Food",
    "SHOPPING": "Shopping",
    "ADVENTURE": "Adventure",
    "CULTURE": "Culture",
    "RELAXATION": "Relaxation",
    "OTHER": "Other",
}


class ValidationError(Exception):
    """Raised when the curated dataset itself fails pre-flight checks."""


@dataclass
class PlaceInterestRow:
    destination_name: str
    place_name: str
    interest_code: str


def read_csv(path: Path) -> List[Dict[str, str]]:
    if not path.exists():
        raise ValidationError(f"Required dataset file missing: {path}")
    with path.open(encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))


def load_dataset(data_dir: Path) -> List[PlaceInterestRow]:
    rows: List[PlaceInterestRow] = []
    for row in read_csv(data_dir / "PlaceInterest_seed_draft.csv"):
        rows.append(
            PlaceInterestRow(
                destination_name=(row.get("destination_name") or "").strip(),
                place_name=(row.get("place_name") or "").strip(),
                interest_code=(row.get("interest_category_code") or "").strip().upper(),
            )
        )
    return rows


def validate_dataset(rows: List[PlaceInterestRow]) -> List[str]:
    errors: List[str] = []
    if not rows:
        errors.append("PlaceInterest_seed_draft.csv contains no rows")

    seen: set[Tuple[str, str, str]] = set()
    for r in rows:
        if not r.destination_name or not r.place_name:
            errors.append(f"Row with missing destination_name/place_name: {r!r}")
            continue
        if r.interest_code not in EXPECTED_INTEREST_CATEGORIES:
            errors.append(
                f"{r.destination_name} / {r.place_name!r}: unknown "
                f"interest_category_code {r.interest_code!r}"
            )
        key = (r.destination_name, r.place_name, r.interest_code)
        if key in seen:
            errors.append(f"Duplicate row: {key!r}")
        seen.add(key)

    return errors


def require_tables(cursor: "pyodbc.Cursor") -> None:
    missing = [
        t
        for t in ("Destinations", "Places", "InterestCategories", "PlaceInterests")
        if not table_exists(cursor, t)
    ]
    if missing:
        raise SystemExit(
            f"Missing tables {missing} — apply the Backend EF Core migrations first "
            "(dotnet ef database update), then re-run this seeder."
        )


def fetch_interest_category_ids(cursor: "pyodbc.Cursor") -> Dict[str, int]:
    cursor.execute("SELECT Code, Id FROM InterestCategories")
    return {row[0]: int(row[1]) for row in cursor.fetchall()}


def fetch_place_id(cursor: "pyodbc.Cursor", destination_name: str, place_name: str) -> int | None:
    cursor.execute(
        """
        SELECT p.Id
        FROM Places p
        JOIN Destinations d ON d.Id = p.DestinationId
        WHERE d.Name = ? AND p.Name = ?
        """,
        destination_name,
        place_name,
    )
    row = cursor.fetchone()
    return int(row[0]) if row else None


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Seed PlaceInterests (Place<->InterestCategory links) into SQL Server."
    )
    parser.add_argument(
        "--connection-string",
        required=True,
        help="ODBC connection string, e.g. "
        '"Server=localhost,1433;Database=TriplyDb;User Id=sa;Password=...;TrustServerCertificate=yes"',
    )
    parser.add_argument(
        "--driver",
        default="ODBC Driver 18 for SQL Server",
        help="ODBC driver name (default: ODBC Driver 18 for SQL Server)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Validate the dataset and print the planned changes without writing.",
    )
    parser.add_argument("--data-dir", default=str(DATA_DIR), help=argparse.SUPPRESS)
    args = parser.parse_args()

    data_dir = Path(args.data_dir)

    # ---------------- load + validate ---------------- #
    try:
        rows = load_dataset(data_dir)
        errors = validate_dataset(rows)
        if errors:
            print("Dataset validation FAILED — no database work attempted:", file=sys.stderr)
            for err in errors:
                print(f"  - {err}", file=sys.stderr)
            return 1
    except ValidationError as exc:
        print(f"Dataset validation FAILED: {exc}", file=sys.stderr)
        return 1

    print(f"Dataset OK: {len(rows)} place<->interest rows")

    # ---------------- connect ---------------- #
    try:
        connection = open_connection(args.connection_string, args.driver)
    except SystemExit as exc:
        print(f"Connection failed: {exc}", file=sys.stderr)
        return 1

    inserted = 0
    already_existed = 0
    skipped_unresolved = 0

    with connection:
        cursor = connection.cursor()
        require_tables(cursor)

        interest_category_ids = fetch_interest_category_ids(cursor)
        missing_categories = set(EXPECTED_INTEREST_CATEGORIES) - set(interest_category_ids)
        if missing_categories:
            raise SystemExit(
                f"InterestCategories table is missing expected codes {sorted(missing_categories)}. "
                "Seed InterestCategory.csv first."
            )

        for row in rows:
            place_id = fetch_place_id(cursor, row.destination_name, row.place_name)
            if place_id is None:
                print(
                    f"Skipping {row.destination_name} / {row.place_name!r}: "
                    "no matching Place found (seed Place.csv first)",
                    file=sys.stderr,
                )
                skipped_unresolved += 1
                continue

            interest_category_id = interest_category_ids[row.interest_code]

            cursor.execute(
                "SELECT 1 FROM PlaceInterests WHERE PlaceId = ? AND InterestCategoryId = ?",
                place_id,
                interest_category_id,
            )
            exists = cursor.fetchone() is not None

            if exists:
                already_existed += 1
                continue

            if args.dry_run:
                print(
                    f"  [dry-run] would link {row.place_name!r} ({row.destination_name}) "
                    f"-> {row.interest_code}"
                )
                inserted += 1
                continue

            cursor.execute(
                "INSERT INTO PlaceInterests (PlaceId, InterestCategoryId) VALUES (?, ?)",
                place_id,
                interest_category_id,
            )
            inserted += 1

    # ---------------- summary ---------------- #
    prefix = "[dry-run] " if args.dry_run else ""
    print(f"{prefix}PlaceInterests: {inserted} to insert, {already_existed} already present, "
          f"{skipped_unresolved} skipped (unresolved place)")
    if args.dry_run:
        print("Dry-run complete — no changes were written.")
    else:
        print("Seed complete.")
    return 0


if __name__ == "__main__":
    sys.exit(main())