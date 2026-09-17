# Dataset Review Record — Curated Destinations/Places/Pricing (Initial Pass)

**Task:** Curate Internal Destinations/Places/Pricing Dataset (Initial Pass)
**Track:** AI / ML — **Sprint 2** — **P0 (Critical)**
**Owners:** Aya Maali, Anas Musleh, Adam Alafandi
**Dataset version:** 1.0.0 (see `seed/DATASET_MANIFEST.json`)
**Requirement:** FR-DATA-001 (SRS §9) — internal dataset maintenance
**References:** Database Design §6.3–§6.7 (entity fields), §26 (seed data strategy), §28 (price freshness risk), SRS D5 / DB-D4 (destination scope)

---

## 1. Scope

Per the D5-agreed MVP scope, this initial pass covers **3 destinations**:

| Destination | Country | Currency | Places |
|---|---|---|---|
| Paris | France (FR) | EUR | 19 |
| Amman | Jordan (JO) | JOD | 19 |
| New York | United States (US) | USD | 19 |

**Total: 57 places**, spanning all 5 place categories (Accommodation 9, Activity 21,
Attraction 9, Restaurant 9, Transport 9) and 4 cost categories. Free entries
(parks, streets, malls, cathedral entry) carry `reference_price = 0.00`.

## 2. Acceptance criteria status

| Criterion | Status | Evidence |
|---|---|---|
| Dataset covers D5-agreed destinations | ✅ Met | §1 above; `Destination.csv` |
| Every place has PlaceCategory | ✅ Met | 57/57 mapped; validated by seeder pre-flight |
| Every place has CostCategory | ✅ Met | 57/57 mapped |
| Every place has Currency | ✅ Met | 57/57 mapped (EUR/JOD/USD) |
| Every place has reference_price | ✅ Met | 57/57, `decimal(10,2)` range checked |
| Dataset is versioned | ✅ Met | `seed/DATASET_MANIFEST.json` — SHA-256 per CSV + row counts + coverage stats, version `1.0.0` |
| Reviewed before use in generation | ✅ Met (this record) | §4 below; sign-off table §6 |
| Seeded into SQL Server | ✅ Met | `seed/seed_places.py` verified end-to-end against SQL Server 2022 (see §5) |

## 3. Data conventions & quality rules applied

- **`Place.name` is the AI-grounding key** (FR-AI-002 exact-match rule, per the
  AI JSON schema contract v2 §4.5). Names are therefore written exactly as they
  should be matched: no abbreviations, disambiguation added in parentheses
  (e.g. `Eiffel Tower (2nd floor, lift)`, `Broadway Show (standard ticket)`).
  Uniqueness within a destination is validated by the seeder (case-insensitive).
- **Descriptions are AI-grounding context** (Database Design §6.5): one or two
  sentences stating what the place is plus any price-relevant qualifier
  (per-person, per-night, show-only, day trip distance). No marketing fluff.
- **`reference_price` units** follow the description's qualifier:
  hotels = per-night double-room rate; restaurants = per-person; transport =
  per-ride or pass; attractions = standard adult ticket; shows = standard seat.
- **`price_updated_at`** records the curation date (2026-09-13). Per Database
  Design §28 (stale-price risk), prices sourced from official pricing pages are
  marked in `Extra_AI_Context.notes`; all rows need re-verification before
  Sprint 3 generation runs that depend on them.
- **`Extra_AI_Context.csv`** carries the per-place source URL, budget tier, and
  interest tags used by prompt-context assembly. It is versioned in the same
  manifest. (Interest tags are advisory prompt context only — the approved
  schema has no Place-to-Interest relationship.)
- Prices are **reference estimates**, not quotes: official tariff pages were
  used where available (Eiffel Tower, Catacombs, JSTA sites, MTA/OMNY, NYC TLC,
  Jordan Pass); everything else is an average from maps/listing data and is
  flagged `verify` in `Extra_AI_Context.notes`.

## 4. Review performed

- **Cross-check vs schema contract:** every `Place.name` is unique within its
  destination (the contract's §9 open item on name collisions) — 57/57 unique.
  Accommodation rows exist for all 3 destinations (required by contract §4.3);
  every destination has ≥1 RESTAURANT and ≥1 TRANSPORT place (contract §5
  step 2 checks need both).
- **Category sanity:** each place's CostCategory matches its PlaceCategory's
  natural bucket (hotels→ACCOMMODATION, restaurants→FOOD, transit→TRANSPORTATION,
  activities/attractions→ACTIVITIES). The 2 known intentional exceptions: the
  Jerash/Petra/Wadi Rum day trips sit under ACTIVITIES cost bucket though
  attraction-flavored, and Jordan Pass under TRANSPORTATION (it is a transport +
  entry bundle) — accepted as correct for budgeting.
- **Seed verification:** see §5.
- **Known limitations for generation:** day-trip places (Jerash, Petra, Wadi Rum,
  Dead Sea, Aqua Mundo) are anchored to their base destination; prompt context
  should treat their `description` ("day trip from Amman, ~X hrs") as the
  travel-time signal. Transport rows model one-way/single-ride costs; multi-day
  trip cost logic belongs to Backend's cost aggregation, not the dataset.

## 5. Seed script verification record

`seed/seed_places.py` (additive, natural-key matching, idempotent upsert) was
verified 2026-09-15 against **SQL Server 2022 (Docker, `mcr.microsoft.com/mssql/server:2022-latest`)**
using a test schema (`seed/test-schema.sql`) that mirrors the Backend EF Core
migrations:

| Scenario | Result |
|---|---|
| Fresh database (no reference data) | ✅ Reference rows landed; 3 destinations + 57 places inserted; correct category/currency joins |
| Database seeded with conflicting Backend data (USD=1/JOD=2, Jerusalem/Palestine, Amman id=2) | ✅ No ID collisions; existing Amman matched by name and re-used (id=2); Paris/NY + 57 places inserted; USD/JOD re-used by ISO code; EUR added |
| Re-run (idempotency) | ✅ 0 inserts, 0 updates; row counts unchanged (57 places / 4 destinations / 3 currencies) |
| `--dry-run` | ✅ Prints full plan (inserts vs. existing), writes nothing |
| Dataset validation gate | ✅ Rejects duplicate place names per destination, unknown category/currency ids, missing/over-range prices, empty descriptions, before touching the DB |

Verification artifacts: `seed/test-schema.sql` (the DDL used). Rerunnable with:
`python seed_places.py --connection-string "..."` per `seed/README.md`.

## 6. Backend coordination items (for the Backend lead)

1. **Reference-data conflict — needs Backend alignment.** The Backend seed
   migration `SeedCountriesCurrenciesDestinations` seeds Countries/Currencies/
   Destinations that contradict this dataset (PS/Jerusalem vs. FR/US/Paris/New York;
   USD=1/JOD=2 vs. the dataset's EUR=1/JOD=2/USD=3 ordering; JD vs. د.أ symbol).
   The seeder is deliberately tolerant of both states (natural-key matching),
   but the double source of truth should be resolved — recommended: Backend
   drops its Destination/Country/Currency `HasData` rows in favor of this
   seeder's dataset (tracked as DB-D4 sign-off).
2. **`Destinations.Latitude/Longitude` is `decimal(10,2)`** in the applied
   migration, vs. `DECIMAL(9,6)` in Database Design §6.3 — coordinates are
   truncated to 2dp in the DB. Harmless for MVP (the seeder compares with
   tolerance), but worth a migration fix at some point.
3. **`Places.Name` is `nvarchar(max)`** in the migration vs. `VARCHAR(200)` in
   §6.5; the seeder validates the 200-char rule in code instead.
4. There is **no unique index on `Places (DestinationId, Name)`** — the FR-AI-002
   exact-match lookup relies on name uniqueness that only the seeder enforces.
   Recommend Backend adds the unique index (contract §9 names this pattern too).

## 7. Sign-off

| Reviewer | Role | Status | Date |
|---|---|---|---|
| Aya Maali | AI track — dataset curation | ☐ | — |
| Anas Musleh | AI track — dataset curation | ☐ | — |
| Adam Alafandi | AI track — dataset curation | ☐ | — |
| Lynn Sharbati | Backend lead — §6 items | ☐ | — |

The dataset is cleared for use in prompt-context assembly and generation
validation once the AI-track sign-offs are recorded; Backend item §6.1 must be
resolved before the shared test/production databases are seeded.
