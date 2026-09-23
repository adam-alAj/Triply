# Triply AI — Progress

> **Current status update (2026-09-23):** The Jerusalem/Palestine conflict recorded below was resolved by PR #66 and migration `20260921001200_RemoveBackendStaticSeed`. The retired `Backend/Triply.Api/Data/seed-dataset.sql` is not an executable seed path; development provisioning reads the curated CSV dataset, and generation validation filters destinations by `IsSupported`. The earlier rows are historical coordination notes, not open blockers. `PlaceInterest` seeding is implemented by the backend curated-data loader.

*Owner: Aya Malli · Track: AI/ML · Last updated: 2026-09-19*

This document tracks new work done on the AI track from this point forward — what was added, why, and its current status — so the rest of the team can follow along without digging through commits.

---

## 1. Dataset Environment Setup (Python/Pandas/Jupyter) — ✅ Complete

**Task objective:** Stand up the local environment used for dataset curation and the validation harness.

| Step                                | Detail                                                                                                                                                                                                                                                         | Status |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| Reviewed existing repo state        | Confirmed dataset v1.0.0 already curated (`REVIEW_RECORD.md`), but no formal Python/Pandas/Jupyter tooling existed — `seed_places.py` and `generate_manifest.py` use only the stdlib `csv` module, and `notebooks/` had an `.xlsx` file but no actual notebook | ✅ Done |
| Python version confirmed            | `3.13.14` (above the 3.10 minimum in `seed/README.md`)                                                                                                                                                                                                         | ✅ Done |
| Virtual environment created         | `AI/01-Dataset/.venv` (kept separate from the rest of the project)                                                                                                                                                                                             | ✅ Done |
| Dedicated requirements file created | `AI/01-Dataset/notebooks/requirements.txt` — `pandas`, `jupyter`, `openpyxl`, `jsonschema`. **Kept separate from `seed/requirements.txt`** (that one stays production-only, `pyodbc` only)                                                                     | ✅ Done |
| Install dependencies                | `pip install -r notebooks/requirements.txt` — installed cleanly, no errors                                                                                                                                                                                     | ✅ Done |
| First verification notebook         | `notebooks/dataset_environment_check.ipynb` — loaded every `curated-data/` CSV with pandas, checked shape/nulls, cross-checked row counts against `seed/DATASET_MANIFEST.json`. **Result: ALL MATCH** (8/8 files, correct row counts, 0 unexpected nulls)      | ✅ Done |
| Commit `requirements.txt`           | Committed and pushed to `AI` branch                                                                                                                                                                                                                            | ✅ Done |
| Synced branch with `main`           | Merged `origin/main` into `AI` locally (no conflicts), pushed via VS Code Sync (135 commits)                                                                                                                                                                   | ✅ Done |
| Progress tracking file added        | `AI/progress.md` (this file) committed to the repo                                                                                                                                                                                                             | ✅ Done |

**Note on acceptance criteria wording:** the task's acceptance criteria calls
for the notebook to run "against a placeholder CSV." No separate placeholder
file was created — verification ran directly against the real
`curated-data/*.csv` files (the actual v1.0.0 dataset) instead of a dummy
stand-in. This is a stronger check than the literal wording (it proves the
environment works against the real data, not just a synthetic sample), so
the criterion is considered satisfied; flagging the difference here for
traceability.

---

## 2. Interest-Aware Destination Suggestions — Backend Coordination

**Gap found:** `DestinationSuggestionService.cs` requires `InterestCategoryIds` in the request but never uses it — suggestions are budget-only today. Root cause: no DB relationship between `InterestCategory` and `Destination`/`Place`.

| Step                                                  | Detail                                                                                                                                                                                                                                                                                                                                                                    | Status                                            |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| Investigated backend code                             | Confirmed the gap in `DestinationSuggestionService.cs`, `DestinationSuggestionDtos.cs`, `DestinationSuggestionRequestValidator.cs`, and the entity model (`ReferenceEntities.cs`, `TripSchema.cs`)                                                                                                                                                                        | ✅ Done                                            |
| Cross-checked data readiness                          | `Extra_AI_Context.csv` already has `interest_tag` per place; all non-blank values map 1:1 to `InterestCategory.Code`. 9 of 57 places have a blank tag                                                                                                                                                                                                                     | ✅ Done                                            |
| Spec written                                          | `AI/05-Destination-Suggestion/Interest_Aware_Destination_Suggestions_Spec.md` — proposes a `PlaceInterest` table, matching/ranking logic, and open questions for Backend                                                                                                                                                                                                  | ✅ Done                                            |
| Sent to Backend lead                                  | Sent to Lynn for review                                                                                                                                                                                                                                                                                                                                                   | ✅ Done                                            |
| Pushed to GitHub                                      | `AI/05-Destination-Suggestion/` folder created and pushed                                                                                                                                                                                                                                                                                                                 | ✅ Done                                            |
| Backend decision (schema + open questions in spec §5) | —                                                                                                                                                                                                                                                                                                                                                                         | ⬜ Waiting on Leen                                 |
| Fill the 9 missing `interest_tag` values              | Investigated further — all 9 are `PlaceCategory = TRANSPORT` (airport transfers, transit passes). **Decision: intentionally left blank**, not a data gap — transport/logistics places aren't traveler "interests". Documented via a `notes` annotation on each row instead. Spec updated to scope `PlaceInterest` seeding to non-TRANSPORT categories only (48/57 places) | ✅ Done (resolved as "not applicable", not filled) |

---

## 3. Dataset Curation Schema/Workflow Mapping (TASK17) — ⬜ Pending Backend review

**Task objective:** Define the working spreadsheet/notebook schema for curating places so it maps cleanly onto the DB's Place/Destination tables.

| Step                                     | Detail                                                                                                                                                                                                                                                | Status                                                                                 |
| ---------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| Compared curated CSVs against DB spec    | Checked every column in `curated-data/*.csv` (Country, Currency, PlaceCategory, CostCategory, InterestCategory, Destination, Place, Extra_AI_Context) against Database Design §6.2–§6.8                                                               | ✅ Done                                                                                 |
| Documented column-to-table mapping       | `AI/01-Dataset/DATASET_CURATION_SCHEMA_MAPPING.md` — full column mapping, curation workflow diagram, and the known applied-schema mismatches carried over from `REVIEW_RECORD.md` §6 (coordinate precision, `Place.name` width, missing unique index) | ✅ Done                                                                                 |
| Flagged `Extra_AI_Context.csv` as non-DB | Documented explicitly: no column in this file has a DB equivalent (prompt-context/provenance only); `interest_tag` has no destination table until `PlaceInterest` is approved                                                                         | ✅ Done                                                                                 |
| Committed on dedicated branch            | `AI-docs/add-schema-mapping-doc-task17`                                                                                                                                                                                                               | ✅ Done                                                                                 |
| Sent to Backend lead for review          | —                                                                                                                                                                                                                                                     | ⬜ Pending — waiting on Lynn to review §3 (mapping) and §5 (mismatches) of the document |

---

## 4. Extra_AI_Context.csv — BUDGET_FIRST Dataset Population — ✅ Complete

**Task objective:** Fully populate `Extra_AI_Context.csv` (previously empty) so Backend can consume it for `BUDGET_FIRST` prompt context.

| Step                                                         | Detail                                                                                                                                                                                                                                                                                                                                                                                              | Status                                      |
| ------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------- |
| Populated all 57 rows                                        | `Extra_AI_Context.csv` was empty before this pass — now has `source_place_id`, `place_name`, `destination_name`, `place_category`, `reference_price`, `currency`, `budget_tier` for all 57 places across Paris / Amman / New York                                                                                                                                                                   | ✅ Done                                      |
| Assigned `budget_tier`                                       | Every active place tagged `BUDGET` / `MID_RANGE` / `LUXURY`, assigned by price ordering **within each (destination, place_category)** — explicitly *not* comparable across destinations                                                                                                                                                                                                             | ✅ Done                                      |
| Renamed join key                                             | Used `source_place_id` (not `place_id`) for the curation-local id, so Backend's `ExtraAiContextReader` falls back to matching on `place_name` (exact, case-sensitive vs. `Place.Name`) instead of accidentally joining on a curation id that isn't the DB id                                                                                                                                        | ✅ Done                                      |
| Decoupled interest data                                      | `interest_tag` left blank/reserved in this file — Place↔Interest links now live only in `PlaceInterest_seed_draft.csv` (all 57 places, including the 9 `TRANSPORT` places tagged `OTHER`) so there's a single source of truth                                                                                                                                                                       | ✅ Done                                      |
| Updated schema mapping doc                                   | `DATASET_CURATION_SCHEMA_MAPPING.md` §3.8 rewritten — documents the `place_name` lookup contract, the `AI:ExtraAiContextPath` runtime read (not imported into the DB), and that every active `Place` needs a `budget_tier` or Backend rejects `BUDGET_FIRST` generation                                                                                                                             | ✅ Done                                      |
| Updated `REVIEW_RECORD.md`                                   | §3 rewritten to match — removed stale claims about `source_url`/`notes` being populated (they're reserved/blank), added the `budget_tier` runtime-read note                                                                                                                                                                                                                                         | ✅ Done                                      |
| Updated Interest-Aware spec                                  | `Interest_Aware_Destination_Suggestions_Spec.md` §3 updated to point at `PlaceInterest_seed_draft.csv` instead of the now-blank `Extra_AI_Context.csv.interest_tag`                                                                                                                                                                                                                                 | ✅ Done                                      |
| Flagged pre-existing Jerusalem/Palestine conflict to Backend | Historical: reported before PR #66; the obsolete seed is now removed/retired, curated CSVs are the reference-data source, and the validator filters `IsSupported` | ✅ Resolved by PR #66; see current-status note above |
| Sent to Backend                                              | Message sent confirming the file is ready, explaining the `place_name` matching + `budget_tier` semantics, and flagging the Jerusalem/Palestine conflict above                                                                                                                                                                                                                                      | ✅ Done                                      |

**Key findings:**
- `budget_tier` is fully populated and documented as consumed at runtime by `ExtraAiContextReader`, not imported into the DB
- `Extra_AI_Context.csv` is intentionally not the source for interests anymore; `PlaceInterest_seed_draft.csv` is
- The former Jerusalem/Palestine conflict is resolved for current seed and generation paths. Populated legacy databases are preserved by migration; their rows are not used as supported generation destinations unless marked `IsSupported`.

---

## 5. Not Yet Started

- `PlaceInterest` seed file + runtime provisioning are implemented; the backend loads the curated CSV during Development startup.

---

*Update this file after each new step — add a row, or a new numbered section for a new work area, so it stays a running log the whole team can read.*
