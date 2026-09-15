#!/usr/bin/env python3
"""TRIPLY — Curated dataset seeder (FR-DATA-001).

Loads the versioned curated CSVs (curated-data/) into SQL Server:

    Country, Currency, PlaceCategory, CostCategory  -> landed only if missing
    Destination                                      -> matched by name (+country), updated if drifted
    Place                                            -> matched by (destination, name), upserted

The seeder is ADDITIVE and matched by NATURAL KEYS (ISO codes / names),
never by the CSV's numeric id column — so it is safe to run against a
database whose reference-table identities already differ (e.g. the
Backend team's SeedCountriesCurrenciesDestinations migration).

Usage (from AI/01-Dataset/seed/):
    pip install -r requirements.txt
    python seed_places.py --connection-string "Server=...;Database=...;..."
    python seed_places.py --dry-run          # plan only, no writes

Exit codes: 0 = success, 1 = validation or runtime failure.
"""

from __future__ import annotations

import argparse
import csv
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

try:
    import pyodbc
except ImportError:  # pragma: no cover
    print(
        "pyodbc is required: pip install -r requirements.txt",
        file=sys.stderr,
    )
    sys.exit(1)

# --------------------------------------------------------------------------- #
# Paths — the seeder always reads the versioned curated CSVs.                  #
# --------------------------------------------------------------------------- #
SCRIPT_DIR = Path(__file__).resolve().parent
DATA_DIR = SCRIPT_DIR.parent / "curated-data"

SEEDER_VERSION = "1.0.0"
DATASET_VERSION = "1.0.0"

# --------------------------------------------------------------------------- #
# Natural-key anchors: the seeder only asserts these codes, not their ids.     #
# --------------------------------------------------------------------------- #
EXPECTED_CURRENCIES = {"EUR": "€", "JOD": "د.أ", "USD": "$"}
EXPECTED_COUNTRIES = {"FR": "France", "JO": "Jordan", "US": "United States"}
EXPECTED_PLACE_CATEGORIES = {
    "ATTRACTION": "Attraction",
    "RESTAURANT": "Restaurant",
    "ACTIVITY": "Activity",
    "ACCOMMODATION": "Accommodation",
    "TRANSPORT": "Transport",
}
EXPECTED_COST_CATEGORIES = {
    "ACCOMMODATION": "Accommodation",
    "TRANSPORTATION": "Transportation",
    "FOOD": "Food",
    "ACTIVITIES": "Activities",
    "OTHER": "Other",
}

# CSV place_category ids (curated-data convention) -> code, per the seeded
# PlaceCategory rows.  Single source of truth for the id->code translation.
PLACE_CATEGORY_BY_ID = {
    "1": "ATTRACTION",
    "2": "RESTAURANT",
    "3": "ACTIVITY",
    "4": "ACCOMMODATION",
    "5": "TRANSPORT",
}
COST_CATEGORY_BY_ID = {
    "1": "ACCOMMODATION",
    "2": "TRANSPORTATION",
    "3": "FOOD",
    "4": "ACTIVITIES",
    "5": "OTHER",
}

VALID_TIME_SLOTS = frozenset()  # (unused; places have no time dimension)

# --------------------------------------------------------------------------- #
# Dataset rows                                                                 #
# --------------------------------------------------------------------------- #


@dataclass
class DestinationRow:
    csv_id: str
    country_iso: str
    name: str
    description: str
    latitude: Optional[Decimal]
    longitude: Optional[Decimal]
    is_supported: bool


@dataclass
class PlaceRow:
    csv_id: str
    destination_csv_id: str
    category_code: str
    name: str
    description: str
    reference_price: Decimal
    currency_iso: str
    cost_category_code: str
    price_updated_at: str
    is_active: bool


class ValidationError(Exception):
    """Raised when the curated dataset itself fails pre-flight checks."""


# --------------------------------------------------------------------------- #
# CSV loading                                                                  #
# --------------------------------------------------------------------------- #


def read_csv(path: Path) -> List[Dict[str, str]]:
    if not path.exists():
        raise ValidationError(f"Required dataset file missing: {path}")
    # utf-8-sig strips the BOM the curation pipeline writes.
    with path.open(encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))


def parse_decimal(value: str, context: str) -> Optional[Decimal]:
    value = (value or "").strip()
    if not value:
        return None
    try:
        return Decimal(value)
    except InvalidOperation as exc:
        raise ValidationError(f"{context}: invalid decimal {value!r}") from exc


def parse_bool(value: str, context: str) -> bool:
    value = (value or "").strip().lower()
    if value in ("true", "1", "yes"):
        return True
    if value in ("false", "0", "no"):
        return False
    raise ValidationError(f"{context}: expected boolean, got {value!r}")


def parse_timestamp(value: str, context: str) -> datetime:
    value = (value or "").strip()
    for fmt in ("%Y-%m-%d", "%Y-%m-%d %H:%M:%S", "%Y-%m-%dT%H:%M:%S"):
        try:
            return datetime.strptime(value, fmt).replace(tzinfo=timezone.utc)
        except ValueError:
            continue
    raise ValidationError(f"{context}: unparseable timestamp {value!r}")


def load_dataset(data_dir: Path) -> Tuple[List[DestinationRow], List[PlaceRow]]:
    destinations: List[DestinationRow] = []
    for row in read_csv(data_dir / "Destination.csv"):
        lat = parse_decimal(row.get("latitude", ""), f"Destination {row.get('id')} latitude")
        lon = parse_decimal(row.get("longitude", ""), f"Destination {row.get('id')} longitude")
        destinations.append(
            DestinationRow(
                csv_id=(row.get("id") or "").strip(),
                country_iso=(row.get("country_id") or "").strip(),
                name=(row.get("name") or "").strip(),
                description=(row.get("description") or "").strip(),
                latitude=lat,
                longitude=lon,
                is_supported=parse_bool(
                    row.get("is_supported", ""), f"Destination {row.get('id')} is_supported"
                ),
            )
        )

    # Destination.country_id holds the *Country.csv* id — translate through
    # Country.csv so the seeder never assumes the DB's identity values.
    countries = read_csv(data_dir / "Country.csv")
    country_id_to_iso = {
        (c.get("id") or "").strip(): (c.get("iso_code") or "").strip() for c in countries
    }
    for dest in destinations:
        iso = country_id_to_iso.get(dest.country_iso)
        if not iso:
            raise ValidationError(
                f"Destination.csv country_id {dest.country_iso!r} has no Country.csv row"
            )
        dest.country_iso = iso

    places: List[PlaceRow] = []
    for row in read_csv(data_dir / "Place.csv"):
        csv_id = (row.get("id") or "").strip()
        ctx = f"Place {csv_id} ({(row.get('name') or '').strip()!r})"
        category_id = (row.get("place_category_id") or "").strip()
        cost_id = (row.get("cost_category_id") or "").strip()
        if category_id not in PLACE_CATEGORY_BY_ID:
            raise ValidationError(f"{ctx}: unknown place_category_id {category_id!r}")
        if cost_id not in COST_CATEGORY_BY_ID:
            raise ValidationError(f"{ctx}: unknown cost_category_id {cost_id!r}")
        price = parse_decimal(row.get("reference_price", ""), f"{ctx} reference_price")
        if price is None:
            raise ValidationError(f"{ctx}: reference_price is required (NOT NULL)")
        if price < 0:
            raise ValidationError(f"{ctx}: reference_price must be >= 0")
        if price > Decimal("99999999.99"):
            raise ValidationError(f"{ctx}: reference_price exceeds decimal(10,2)")
        price_updated = (row.get("price_updated_at") or "").strip()
        if not price_updated:
            raise ValidationError(f"{ctx}: price_updated_at is required")
        parse_timestamp(price_updated, f"{ctx} price_updated_at")

        places.append(
            PlaceRow(
                csv_id=csv_id,
                destination_csv_id=(row.get("destination_id") or "").strip(),
                category_code=PLACE_CATEGORY_BY_ID[category_id],
                name=(row.get("name") or "").strip(),
                description=(row.get("description") or "").strip(),
                reference_price=price,
                currency_iso=(row.get("currency_id") or "").strip(),
                cost_category_code=COST_CATEGORY_BY_ID[cost_id],
                price_updated_at=price_updated,
                is_active=parse_bool(row.get("is_active", ""), f"{ctx} is_active"),
            )
        )

    # currency_id in Place.csv is the Currency.csv id — translate to ISO.
    currencies = read_csv(data_dir / "Currency.csv")
    currency_id_to_iso = {
        (c.get("id") or "").strip(): (c.get("iso_code") or "").strip() for c in currencies
    }
    for place in places:
        iso = currency_id_to_iso.get(place.currency_iso)
        if not iso:
            raise ValidationError(
                f"Place.csv currency_id {place.currency_iso!r} has no Currency.csv row"
            )
        place.currency_iso = iso

    return destinations, places


# --------------------------------------------------------------------------- #
# Pre-flight validation (dataset-internal consistency, no DB needed)           #
# --------------------------------------------------------------------------- #


def validate_dataset(
    destinations: Sequence[DestinationRow],
    places: Sequence[PlaceRow],
) -> List[str]:
    errors: List[str] = []
    dest_ids = {d.csv_id for d in destinations}

    if not destinations:
        errors.append("Destination.csv contains no rows")
    if not places:
        errors.append("Place.csv contains no rows")

    seen_dest_names: Dict[str, str] = {}
    for d in destinations:
        if not d.name:
            errors.append("Destination row with empty name")
            continue
        if d.csv_id not in dest_ids or not d.csv_id:
            errors.append(f"Destination {d.name!r}: missing id")
        if d.name in seen_dest_names:
            errors.append(
                f"Duplicate destination name {d.name!r} "
                f"(rows {seen_dest_names[d.name]} and {d.csv_id})"
            )
        seen_dest_names[d.name] = d.csv_id
        if not d.description:
            errors.append(f"Destination {d.name!r}: description is empty (AI-grounding context)")

    seen_place_names: Dict[Tuple[str, str], str] = {}
    for p in places:
        key = (p.destination_csv_id, p.name.casefold())
        if key in seen_place_names:
            errors.append(
                f"Duplicate place name {p.name!r} within destination {p.destination_csv_id} "
                f"(rows {seen_place_names[key]} and {p.csv_id})"
            )
        seen_place_names[key] = p.csv_id
        if p.destination_csv_id not in dest_ids:
            errors.append(f"Place {p.csv_id!r} references unknown destination_id {p.destination_csv_id!r}")
        if not p.name:
            errors.append(f"Place {p.csv_id}: empty name")
        elif len(p.name) > 200:
            errors.append(f"Place {p.csv_id}: name exceeds VARCHAR(200)")
        if not p.description:
            errors.append(f"Place {p.csv_id} ({p.name!r}): description is empty (AI-grounding context)")

    return errors


# --------------------------------------------------------------------------- #
# Database access                                                              #
# --------------------------------------------------------------------------- #

SCALAR = lambda cur: cur.fetchone()[0]


def _available_sql_drivers() -> List[str]:
    return [d for d in pyodbc.drivers() if "sql server" in d.lower()]


def _legacy_driver_conn_str(connection_string: str) -> str:
    """Translate modern keywords for the legacy 'SQL Server' driver:
    User Id -> Uid, Password -> Pwd, drop unsupported TLS attributes."""
    parts = []
    for part in connection_string.split(";"):
        key, _, value = part.partition("=")
        key = key.strip()
        if key.lower() in (
            "trustservercertificate",
            "encrypt",
            "trust server certificate",
        ):
            continue  # unsupported by the legacy driver
        if key.lower() == "user id":
            key = "Uid"
        elif key.lower() == "password":
            key = "Pwd"
        parts.append(f"{key}={value}" if _ else part)
    return ";".join(parts)


def open_connection(connection_string: str, driver_hint: str) -> "pyodbc.Connection":
    candidates: List[str] = []
    if "Driver=" not in connection_string and "driver=" not in connection_string:
        for driver in (driver_hint, "ODBC Driver 17 for SQL Server", "SQL Server"):
            if driver in pyodbc.drivers() and driver not in candidates:
                candidates.append(driver)
        if not candidates:
            raise SystemExit(
                "No SQL Server ODBC driver found. Install 'ODBC Driver 18 for "
                "SQL Server' or pass an explicit Driver= in --connection-string. "
                f"Installed drivers: {pyodbc.drivers()}"
            )
    else:
        candidates = [""]  # caller-supplied Driver= stays as-is

    last_error: Optional[Exception] = None
    for driver in candidates:
        if driver == "SQL Server":
            conn_str = f"Driver={{SQL Server}};{_legacy_driver_conn_str(connection_string)}"
        else:
            conn_str = (
                f"Driver={{{driver}}};{connection_string}" if driver else connection_string
            )
        try:
            return pyodbc.connect(conn_str, timeout=10)
        except pyodbc.InterfaceError as exc:
            last_error = exc
        except pyodbc.OperationalError as exc:
            if driver == "SQL Server":
                last_error = exc
                continue  # keyword translation may have been imperfect; try next
            raise  # driver loaded; server/login issue — surface it directly
    raise SystemExit(
        f"Could not connect with any available driver ({candidates}): {last_error}"
    )


def table_exists(cursor: pyodbc.Cursor, table: str) -> bool:
    cursor.execute(
        "SELECT 1 FROM sys.tables WHERE name = ?", table
    )
    return cursor.fetchone() is not None


def require_tables(cursor: pyodbc.Cursor) -> None:
    missing = [t for t in ("Countries", "Currencies", "PlaceCategories",
                           "CostCategories", "Destinations", "Places")
               if not table_exists(cursor, t)]
    if missing:
        raise SystemExit(
            f"Missing tables {missing} — apply the Backend EF Core migrations first "
            "(dotnet ef database update), then re-run this seeder."
        )


def fetch_id_map(
    cursor: pyodbc.Cursor, table: str, key_col: str
) -> Dict[str, int]:
    cursor.execute(f"SELECT {key_col}, Id FROM {table}")
    return {row[0]: int(row[1]) for row in cursor.fetchall()}


def ensure_reference_row(
    cursor: pyodbc.Cursor,
    table: str,
    key_col: str,
    key: str,
    value_col: str,
    value: str,
) -> None:
    """Land a missing reference row (never overwrite an existing one)."""
    cursor.execute(
        f"""
        IF NOT EXISTS (SELECT 1 FROM {table} WHERE {key_col} = ?)
        INSERT INTO {table} ({key_col}, {value_col}) VALUES (?, ?)
        """,
        key,
        key,
        value,
    )


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Seed the curated Trip dataset into SQL Server."
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
    parser.add_argument(
        "--data-dir",
        default=str(DATA_DIR),
        help=argparse.SUPPRESS,
    )
    args = parser.parse_args()

    data_dir = Path(args.data_dir)

    # ---------------- load + validate ---------------- #
    try:
        destinations, places = load_dataset(data_dir)
        errors = validate_dataset(destinations, places)
        if errors:
            print("Dataset validation FAILED — no database work attempted:", file=sys.stderr)
            for err in errors:
                print(f"  - {err}", file=sys.stderr)
            return 1
    except ValidationError as exc:
        print(f"Dataset validation FAILED: {exc}", file=sys.stderr)
        return 1

    print(
        f"Dataset OK: {len(destinations)} destinations, {len(places)} places "
        f"(dataset v{DATASET_VERSION}, seeder v{SEEDER_VERSION})"
    )

    # ---------------- connect ---------------- #
    try:
        connection = open_connection(args.connection_string, args.driver)
    except SystemExit as exc:
        print(f"Connection failed: {exc}", file=sys.stderr)
        return 1

    inserted_destinations = 0
    updated_destinations = 0
    inserted_places = 0
    updated_places = 0

    with connection:
        cursor = connection.cursor()
        require_tables(cursor)

        # ---------------- reference rows (land only) ---------------- #
        ensure_reference_row(cursor, "Currencies", "IsoCode", "EUR", "Symbol", "€")
        ensure_reference_row(cursor, "Currencies", "IsoCode", "JOD", "Symbol", "د.أ")
        ensure_reference_row(cursor, "Currencies", "IsoCode", "USD", "Symbol", "$")
        ensure_reference_row(cursor, "Countries", "IsoCode", "FR", "Name", "France")
        ensure_reference_row(cursor, "Countries", "IsoCode", "JO", "Name", "Jordan")
        ensure_reference_row(cursor, "Countries", "IsoCode", "US", "Name", "United States")
        for code, label in EXPECTED_PLACE_CATEGORIES.items():
            ensure_reference_row(cursor, "PlaceCategories", "Code", code, "Label", label)
        for code, label in EXPECTED_COST_CATEGORIES.items():
            ensure_reference_row(cursor, "CostCategories", "Code", code, "Label", label)

        currency_ids = fetch_id_map(cursor, "Currencies", "IsoCode")
        country_ids = fetch_id_map(cursor, "Countries", "IsoCode")
        place_category_ids = fetch_id_map(cursor, "PlaceCategories", "Code")
        cost_category_ids = fetch_id_map(cursor, "CostCategories", "Code")

        # ---------------- destinations ---------------- #
        dest_id_by_name: Dict[str, int] = {}
        for dest in destinations:
            country_id = country_ids[dest.country_iso]
            cursor.execute(
                "SELECT Id, Description, Latitude, Longitude, IsSupported "
                "FROM Destinations WHERE Name = ? AND CountryId = ?",
                dest.name,
                country_id,
            )
            existing = cursor.fetchone()

            if existing is None:
                if args.dry_run:
                    print(f"  [dry-run] would insert destination {dest.name!r}")
                else:
                    cursor.execute(
                        """
                        INSERT INTO Destinations (CountryId, Name, Description, Latitude, Longitude, IsSupported)
                        OUTPUT INSERTED.Id
                        VALUES (?, ?, ?, ?, ?, ?)
                        """,
                        country_id,
                        dest.name,
                        dest.description,
                        dest.latitude,
                        dest.longitude,
                        dest.is_supported,
                    )
                    dest_id_by_name[dest.name] = int(SCALAR(cursor))
                inserted_destinations += 1
                dest_id_by_name.setdefault(dest.name, -1)
                continue

            db_id, db_desc, db_lat, db_lon, db_supported = existing
            dest_id_by_name[dest.name] = int(db_id)

            if args.dry_run:
                print(f"  [dry-run] destination {dest.name!r} exists (id={db_id})")
                continue

            needs_update = (
                (db_desc or "") != dest.description
                or db_supported != dest.is_supported
                or not _coords_match(db_lat, dest.latitude)
                or not _coords_match(db_lon, dest.longitude)
            )
            if needs_update:
                cursor.execute(
                    """
                    UPDATE Destinations
                    SET Description = ?, Latitude = ?, Longitude = ?, IsSupported = ?
                    WHERE Id = ?
                    """,
                    dest.description,
                    dest.latitude,
                    dest.longitude,
                    dest.is_supported,
                    db_id,
                )
                updated_destinations += 1

        # ---------------- places ---------------- #
        for place in places:
            dest_id = dest_id_by_name.get(
                next(d.name for d in destinations if d.csv_id == place.destination_csv_id)
            )
            if not dest_id or dest_id < 0:
                # In dry-run mode destinations may not have been inserted.
                if args.dry_run:
                    dest_name = next(
                        d.name for d in destinations if d.csv_id == place.destination_csv_id
                    )
                    print(f"  [dry-run] would insert place {place.name!r} (destination {dest_name!r})")
                    inserted_places += 1
                    continue
                print(f"Skipping place {place.name!r}: destination unresolved", file=sys.stderr)
                continue

            cursor.execute(
                "SELECT Id, PlaceCategoryId, Description, ReferencePrice, CurrencyId, "
                "CostCategoryId, IsActive FROM Places WHERE DestinationId = ? AND Name = ?",
                dest_id,
                place.name,
            )
            existing = cursor.fetchone()

            price_updated = parse_timestamp(place.price_updated_at, f"Place {place.csv_id}")
            if existing is None:
                if args.dry_run:
                    print(f"  [dry-run] would insert place {place.name!r}")
                    inserted_places += 1
                    continue
                cursor.execute(
                    """
                    INSERT INTO Places (DestinationId, PlaceCategoryId, Name, Description,
                                        ReferencePrice, CurrencyId, CostCategoryId,
                                        PriceUpdatedAt, IsActive)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """,
                    dest_id,
                    place_category_ids[place.category_code],
                    place.name,
                    place.description,
                    place.reference_price,
                    currency_ids[place.currency_iso],
                    cost_category_ids[place.cost_category_code],
                    price_updated,
                    place.is_active,
                )
                inserted_places += 1
                continue

            (
                db_place_id,
                db_cat,
                db_desc,
                db_price,
                db_currency,
                db_cost,
                db_active,
            ) = existing
            if args.dry_run:
                print(f"  [dry-run] place {place.name!r} exists (id={db_place_id})")
                continue

            needs_update = (
                db_cat != place_category_ids[place.category_code]
                or (db_desc or "") != place.description
                or Decimal(str(db_price)) != place.reference_price
                or db_currency != currency_ids[place.currency_iso]
                or db_cost != cost_category_ids[place.cost_category_code]
                or db_active != place.is_active
            )
            if needs_update:
                cursor.execute(
                    """
                    UPDATE Places
                    SET PlaceCategoryId = ?, Description = ?, ReferencePrice = ?,
                        CurrencyId = ?, CostCategoryId = ?, IsActive = ?,
                        PriceUpdatedAt = ?
                    WHERE Id = ?
                    """,
                    place_category_ids[place.category_code],
                    place.description,
                    place.reference_price,
                    currency_ids[place.currency_iso],
                    cost_category_ids[place.cost_category_code],
                    place.is_active,
                    price_updated,
                    db_place_id,
                )
                updated_places += 1

    # ---------------- summary ---------------- #
    prefix = "[dry-run] " if args.dry_run else ""
    print(f"{prefix}Destinations: {inserted_destinations} to insert, {updated_destinations} updated")
    print(f"{prefix}Places:       {inserted_places} to insert, {updated_places} updated")
    if args.dry_run:
        print("Dry-run complete — no changes were written.")
    else:
        print("Seed complete.")
    return 0


def _coords_match(db_value: Any, csv_value: Optional[Decimal], tolerance: Decimal = Decimal("0.05")) -> bool:
    """DB coords are decimal(10,2) (truncated), CSVs carry full precision."""
    if csv_value is None:
        return db_value is None
    if db_value is None:
        return False
    return abs(Decimal(str(db_value)) - csv_value) <= tolerance


if __name__ == "__main__":
    sys.exit(main())
