# Triply Curated Dataset Seeder — FR-DATA-001

> **Not part of the standard local setup.** The backend provisions the same CSVs
> automatically at Development startup (`Backend/Triply.Api/Data/SeedData.cs`,
> see *Backend/README → “Reference data on a fresh database”*), so a fresh clone
> never needs a manual seeder run. The scripts below remain for offline/ops use:
> bulk re-imports, repairing a non-development database, and dataset maintenance.

Loads the versioned curated CSVs (`../curated-data/`) into SQL Server:
`Country`, `Currency`, `PlaceCategory`, `CostCategory` (landed only if missing),
`Destination`, and `Place` (matched by natural key and upserted).

## Dataset summary (v1.0.0)

| Metric | Value |
|---|---|
| Destinations | 3 (Paris, Amman, New York — the D5-agreed MVP scope) |
| Places | 57 (19 per destination) |
| Every place has | PlaceCategory + CostCategory + Currency + reference_price + price_updated_at |
| Currencies | EUR (Paris), JOD (Amman), USD (New York) |
| Integrity | `DATASET_MANIFEST.json` (SHA-256 per CSV, row counts, coverage) |

## Prerequisites

- Python 3.10+
- SQL Server reachable (Backend LocalDB or the shared test DB)
- An ODBC driver: "ODBC Driver 18/17 for SQL Server" or the legacy "SQL Server" driver
  (the seeder auto-detects whichever is installed)

```bash
pip install -r requirements.txt
```

The database schema itself comes from the Backend EF Core migrations
(`Backend/Triply.Api`) — run `dotnet ef database update` there first if the
target database is empty. The seeder checks for the required tables and stops
with a clear message if they are missing.

## Usage

```bash
# Preview what would change (no writes):
python seed_places.py --dry-run \
  --connection-string "Server=(localdb)\mssqllocaldb;Database=TriplyDb;Trusted_Connection=yes;"

# Seed:
python seed_places.py \
  --connection-string "Server=(localdb)\mssqllocaldb;Database=TriplyDb;Trusted_Connection=yes;"
```

Notes:

- The seeder is **additive and idempotent** — safe to run repeatedly and
  against a database whose reference-table identity values differ from the
  CSVs (matching is by ISO code / name, never by CSV `id`).
- Existing reference rows (e.g. the Backend-seeded currencies) are **never
  overwritten**; destinations/places are inserted if missing and updated
  only where the curated CSVs differ.
- Exit code 0 = success; 1 = validation or connection failure.

## Versioning workflow

Any change to `curated-data/` must ship with an updated manifest:

```bash
python generate_manifest.py                     # verify CSVs against manifest
python generate_manifest.py --write --dataset-version 1.1.0
```

Version bump policy: `patch` = price/description corrections; `minor` = new
places/destinations/categories; `major` = scope or schema change.
See `../REVIEW_RECORD.md` for the review record.
