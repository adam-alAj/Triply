# Dataset Curation Schema & Workflow — Column-to-Table Mapping

**Task:** [P0][AI][TASK17] Design and Version the Dataset Curation Schema/Workflow
**Track:** AI / ML — Sprint 1 — P1 (High)
**Owner:** Aya Maali
**Requirement:** FR-DATA-001 (SRS §9)
**References:** Database Design & Data Model Specification — §6.2–§6.8 (Entity Specifications), §6.3 (Destination), §6.5 (Place), §26 (Seed Data Strategy)
**Related artifacts:** `AI/01-Dataset/curated-data/*.csv`, `AI/01-Dataset/seed/DATASET_MANIFEST.json`, `AI/01-Dataset/seed/seed_places.py`, `AI/01-Dataset/REVIEW_RECORD.md`

**Status:** Draft — pending Backend review (see §6, Sign-off)

---

## 1. Purpose

This document defines the working spreadsheet/notebook schema used to curate
destinations and places, and maps every column in every curated CSV file to
the exact database table/column it lands in (or explains why it does not land
in the DB at all). It exists so Backend can confirm import compatibility
before the dataset is treated as a reliable seed source, per §26 of the
Database Design spec ("Destination / Place — curated by the AI track, not
auto-seeded").

## 2. Curation workflow (end to end)

```
raw-data/  (source material — maps/listings/official pricing pages, not versioned as data)
   │
   ▼
curated-data/*.csv   (working spreadsheet schema — one CSV per DB table, see §3)
   │  reviewed against Database Design §6.2–§6.8, quality rules in REVIEW_RECORD.md
   ▼
seed/generate_manifest.py   → seed/DATASET_MANIFEST.json  (SHA-256 + row counts per file, versioned x.y.z)
   │
   ▼
seed/seed_places.py   (natural-key match + idempotent upsert into SQL Server)
   │
   ▼
Backend database (Country, Currency, PlaceCategory, CostCategory, Destination, Place tables)
```

- **Working format:** one flat CSV per reference/entity table, columns named
  to match the DB column names directly (snake_case), so the mapping in §3 is
  close to 1:1 by design.
- **Versioning:** any change to `curated-data/` requires re-running
  `generate_manifest.py --write --dataset-version x.y.z` before commit. Patch
  = price/description fixes; minor = new places/destinations/categories;
  major = scope or schema change. Enforced by convention today, not by CI.
- **Import:** `seed_places.py` never trusts the CSV's `id` column as the DB
  primary key — it matches existing rows by **natural key** (ISO code for
  Country/Currency, `code` for category tables, `name` for
  Destination/Place) and upserts. This is why the CSV `id` columns are listed
  as "curation-local only" in the mapping below, not as the DB `id`.

## 3. Column-to-table mapping

### 3.1 `Country.csv` → `Country` table (Database Design §6.2)

| CSV column | DB column        | Type (DB)    | Notes                                                                                                        |
| ---------- | ---------------- | ------------ | ------------------------------------------------------------------------------------------------------------ |
| `id`       | *(not imported)* | —            | Curation-local row reference only; DB `id` is `BIGINT IDENTITY`, assigned by the DB or matched by `iso_code` |
| `name`     | `name`           | VARCHAR(100) | UNIQUE in DB                                                                                                 |
| `iso_code` | `iso_code`       | CHAR(2)      | UNIQUE in DB; **natural key used for matching**                                                              |

### 3.2 `Currency.csv` → `Currency` table (§6.7)

| CSV column | DB column        | Type (DB)  | Notes                                     |
| ---------- | ---------------- | ---------- | ----------------------------------------- |
| `id`       | *(not imported)* | —          | Curation-local only                       |
| `iso_code` | `iso_code`       | CHAR(3)    | UNIQUE; **natural key used for matching** |
| `symbol`   | `symbol`         | VARCHAR(5) |                                           |

### 3.3 `PlaceCategory.csv` → `PlaceCategory` table (§6.4)

| CSV column | DB column        | Type (DB)   | Notes                                                                                                                       |
| ---------- | ---------------- | ----------- | --------------------------------------------------------------------------------------------------------------------------- |
| `id`       | *(not imported)* | —           | Curation-local only                                                                                                         |
| `code`     | `code`           | VARCHAR(30) | UNIQUE; **natural key used for matching**; fixed set (`ATTRACTION`, `RESTAURANT`, `ACTIVITY`, `ACCOMMODATION`, `TRANSPORT`) |
| `label`    | `label`          | VARCHAR(60) | Display label                                                                                                               |

### 3.4 `CostCategory.csv` → `CostCategory` table (§6.6)

| CSV column | DB column        | Type (DB)   | Notes                                                                                                 |
| ---------- | ---------------- | ----------- | ----------------------------------------------------------------------------------------------------- |
| `id`       | *(not imported)* | —           | Curation-local only                                                                                   |
| `code`     | `code`           | VARCHAR(30) | UNIQUE; **natural key**; fixed set (`ACCOMMODATION`, `TRANSPORTATION`, `FOOD`, `ACTIVITIES`, `OTHER`) |
| `label`    | `label`          | VARCHAR(60) |                                                                                                       |

### 3.5 `InterestCategory.csv` → `InterestCategory` table (§6.8)

| CSV column | DB column        | Type (DB)   | Notes                                                                                                                               |
| ---------- | ---------------- | ----------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `id`       | *(not imported)* | —           | Curation-local only                                                                                                                 |
| `code`     | `code`           | VARCHAR(30) | UNIQUE; **natural key**; fixed 8-value set (`NATURE`, `HISTORY`, `FOOD`, `SHOPPING`, `ADVENTURE`, `CULTURE`, `RELAXATION`, `OTHER`) |
| `label`    | `label`          | VARCHAR(60) |                                                                                                                                     |

### 3.6 `Destination.csv` → `Destination` table (§6.3)

| CSV column     | DB column         | Type (DB)            | Notes                                                                                                                                                |
| -------------- | ----------------- | -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `id`           | *(not imported)*  | —                    | Curation-local only; used to join `Place.destination_id` within the CSV set, resolved to the DB's actual `Destination.id` by name match at seed time |
| `country_id`   | `country_id` (FK) | BIGINT               | Resolved via `Country.iso_code`, not the CSV numeric id                                                                                              |
| `name`         | `name`            | VARCHAR(150)         | **Natural key used for matching**                                                                                                                    |
| `description`  | `description`     | TEXT                 | AI-grounding context per §6.3                                                                                                                        |
| `latitude`     | `latitude`        | DECIMAL(9,6) in spec | ⚠️ Applied Backend migration uses `decimal(10,2)` — coordinates truncate to 2dp in the DB today (see §5, item 2)                                      |
| `longitude`    | `longitude`       | DECIMAL(9,6) in spec | Same caveat as latitude                                                                                                                              |
| `is_supported` | `is_supported`    | BOOLEAN              | Controls whether destination is offered (SRS D5)                                                                                                     |

### 3.7 `Place.csv` → `Place` table (§6.5)

| CSV column          | DB column                | Type (DB)                                     | Notes                                                                                                                                                                                                                                  |
| ------------------- | ------------------------ | --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `id`                | *(not imported)*         | —                                             | Curation-local only                                                                                                                                                                                                                    |
| `destination_id`    | `destination_id` (FK)    | BIGINT                                        | Resolved via `Destination.name`, not the CSV numeric id                                                                                                                                                                                |
| `place_category_id` | `place_category_id` (FK) | BIGINT                                        | Resolved via `PlaceCategory.code`                                                                                                                                                                                                      |
| `name`              | `name`                   | VARCHAR(200) spec / `nvarchar(max)` migration | **Natural key used for matching AND the AI-grounding key** (FR-AI-002 exact-match rule per AI JSON Schema Contract v2 §4.5). Must be unique within a destination — enforced by the seeder, not yet by a DB constraint (see §5, item 4) |
| `description`       | `description`            | TEXT                                          | AI-grounding context                                                                                                                                                                                                                   |
| `reference_price`   | `reference_price`        | DECIMAL(10,2)                                 | Canonical estimated price per unit; unit convention documented in `REVIEW_RECORD.md` §3                                                                                                                                                |
| `currency_id`       | `currency_id` (FK)       | BIGINT                                        | Resolved via `Currency.iso_code`                                                                                                                                                                                                       |
| `cost_category_id`  | `cost_category_id` (FK)  | BIGINT                                        | Resolved via `CostCategory.code`                                                                                                                                                                                                       |
| `price_updated_at`  | `price_updated_at`       | TIMESTAMP                                     | Curation date, not import date                                                                                                                                                                                                         |
| `is_active`         | `is_active`              | BOOLEAN                                       | Soft-disable flag                                                                                                                                                                                                                      |

### 3.8 `Extra_AI_Context.csv` — **not a DB table**

| CSV column              | Destination | Notes                                                                                                                                                                                                                                                                                                                                    |
| ----------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `source_place_id`       | *(none)*    | Curation-local id of the matching row in `Place.csv`; not persisted. Deliberately **not** named `place_id`: the backend's `ExtraAiContextReader` uses a `place_id`/`id` column as its lookup key when present, and DB ids differ from curation ids, so with this name it matches on `place_name` instead.                                |
| `place_name`            | *(none)*    | **Lookup key used by the backend** (exact, case-sensitive match against `Place.Name`). Must equal `name` in `Place.csv`.                                                                                                                                                                                                                 |
| `destination_name`      | *(none)*    | For human readability / cross-check against `Destination.csv`                                                                                                                                                                                                                                                                            |
| `place_category`        | *(none)*    | For human readability; `PlaceCategory.code` of the place                                                                                                                                                                                                                                                                                 |
| `reference_price`       | *(none)*    | Copy of `Place.csv` `reference_price` for readability; `Place.csv` remains authoritative                                                                                                                                                                                                                                                 |
| `currency`              | *(none)*    | Copy of the place's currency ISO code for readability; `Currency.csv` / `Place.csv` remain authoritative                                                                                                                                                                                                                                 |
| `budget_tier`           | *(none)*    | **Consumed by the backend** as BUDGET_FIRST prompt context. One of `BUDGET`, `MID_RANGE`, `LUXURY`. Relative tier: assigned by price ordering within each (destination, place category), so it is not comparable across destinations. Every active `Place` must have a value, otherwise the backend rejects the BUDGET_FIRST generation. |
| `suitable_travel_style` | *(none)*    | Reserved — currently blank                                                                                                                                                                                                                                                                                                               |
| `interest_tag`          | *(none)*    | Reserved — currently blank. Place↔Interest links are curated in `PlaceInterest_seed_draft.csv` and loaded into the `PlaceInterests` table by `seed/Seed_place_interests.py` (see `AI/05-Destination-Suggestion/Interest_Aware_Destination_Suggestions_Spec.md`).                                                                         |
| `source_url`            | *(none)*    | Reserved — currently blank (intended for provenance/audit)                                                                                                                                                                                                                                                                               |
| `notes`                 | *(none)*    | Reserved — currently blank (intended for provenance/audit, e.g. "verify" flags)                                                                                                                                                                                                                                                          |

This file is versioned in the same manifest as the importable CSVs (same
integrity guarantee) but is explicitly out of scope for `seed_places.py`. It is
not imported into the database: the backend reads it at runtime from the path
configured in `AI:ExtraAiContextPath` (`ExtraAiContextReader`).

## 4. Fields that exist in curated CSVs with no DB equivalent

None beyond `Extra_AI_Context.csv` (§3.8) — every column in
`Country.csv`, `Currency.csv`, `PlaceCategory.csv`, `CostCategory.csv`,
`InterestCategory.csv`, `Destination.csv`, and `Place.csv` maps to a DB
column.

## 5. Known mismatches Backend should confirm (import-compatibility risk)

These are carried over from `REVIEW_RECORD.md` §6 and repeated here because
they directly affect whether this mapping is safe to import against the
*actual* applied schema, not just the spec document:

1. **Reference-data conflict.** The Backend seed migration
   `SeedCountriesCurrenciesDestinations` seeds Country/Currency/Destination
   rows that contradict this dataset (e.g. different Currency ordering,
   different Destination set). The natural-key matching in `seed_places.py`
   tolerates this, but the double source of truth should be resolved —
   tracked as DB-D4 sign-off.
2. **Coordinate precision.** Spec says `DECIMAL(9,6)` for
   `Destination.latitude/longitude`; the applied migration uses
   `decimal(10,2)`. Harmless for MVP but worth a migration fix.
3. **Name column width.** Spec says `Place.name VARCHAR(200)`; the applied
   migration uses `nvarchar(max)`. The seeder enforces the 200-char rule in
   code as a stopgap.
4. **No unique index on `Places(DestinationId, Name)`.** The FR-AI-002
   exact-match lookup depends on name uniqueness per destination that only
   the seeder currently enforces at import time, not the DB schema.

## 6. Sign-off

| Reviewer      | Role                        | Focus                                                         | Status | Date |
| ------------- | --------------------------- | ------------------------------------------------------------- | ------ | ---- |
| Aya Maali     | AI track — dataset curation | Mapping accuracy vs curated CSVs                              | ☐      | —    |
| Leen Sharbati | Backend lead                | Import compatibility vs applied EF Core migrations (§5 items) | ☐      | —    |

This document is considered accepted for TASK17's acceptance criteria once
Backend has reviewed §3 and §5 and confirmed (or corrected) the column
mapping and the applied-schema mismatches above.