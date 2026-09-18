# Triply AI — Progress

*Owner: Aya Malli · Track: AI/ML · Last updated: 2026-09-17*

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

## 4. Gemini JSON Output Schema Finalization (TASK46) — ✅ Complete

**Task objective:** Validate and lock the Gemini JSON output schema against the agreed contract, dataset, and prompt templates. Align Backend with v2.0.0.

| Step | Detail | Status |
| --- | --- | --- |
| Phase 1-2: Repository & dependency audit | Inspected all artifacts: contract v2.0.0, schema.json, prompt templates, dataset CSVs, Backend entities/DTOs | ✅ Done |
| Phase 3-6: Consistency matrix | Identified 7 critical misalignments between Backend (v1-era) and contract v2.0.0 | ✅ Done |
| Phase 7-8: Schema validation | Confirmed `triply-trip-plan-generation.schema.json` is valid JSON Schema draft 2020-12, all $refs resolve | ✅ Done |
| Phase 10: Backend DTO alignment | Rewrote `AiOrchestrationDtos.cs` — root shape `{planning_mode, destination_options[]}`, `place_name` replaces `PlaceId`, `accommodation` object added | ✅ Done |
| Phase 10b: Validation service | Rewrote `ItineraryValidationService.cs` — name-based grounding, category rules, 0% tolerance | ✅ Done |
| Phase 10c: Prompt builder | Rewrote `ItineraryPromptBuilder.cs` — `place_name` grounding, category-grouped lists, BUDGET_FIRST support | ✅ Done |
| Phase 10d: Orchestration service | Updated `AiOrchestrationService.cs` — handles new DTO structure, name-to-ID resolution for persistence | ✅ Done |
| Phase 10e: Gemini client | Updated `GeminiClient.cs` — added `GenerateJsonWithSchemaAsync` with `responseJsonSchema` | ✅ Done |
| Phase 11: Validation harness | Created `AI/03-Validation/validate_schema.py` — 15 test cases covering valid/invalid/edge scenarios | ✅ Done |
| Phase 12: Test results | All 15/15 test cases pass: valid itineraries, missing fields, wrong types, invalid enums, unexpected fields, realistic dataset values | ✅ Done |
| Phase 9: Schema changelog | Created `AI/docs/SCHEMA_CHANGELOG.md` — documents all changes, reasons, and integration notes | ✅ Done |

**Key findings:**
- The schema JSON file was already correct at v2.0.0 — no schema changes needed
- The Backend was the source of drift: DTOs used `PlaceId` (numeric), flat structure, no `planning_mode`/`accommodation`
- All 5 Backend files in AI-Orchestration were updated to align with the contract
- `Extra_AI_Context.csv` budget_tier not yet joined in the place query (marked as TODO)

---

## 5. Gemini Prototype Experiments (TASK45) — ✅ Complete

**Task objective:** Run controlled prototype experiments against Gemini Flash-family models to verify prompt + schema + dataset context can reliably produce structured itinerary output.

| Step | Detail | Status |
| --- | --- | --- |
| Schema verification | Confirmed finalized schema v2.0.0 at `json-schemas/triply-trip-plan-generation.schema.json` | ✅ Done |
| Prompt verification | Confirmed both templates (`destination_first.md`, `budget_first.md`) reference v2.0.0 | ✅ Done |
| Dataset context | Built real context from 57 active places across 3 destinations | ✅ Done |
| Experiment design | 12 scenarios: DESTINATION_FIRST (9) + BUDGET_FIRST (3), varying budget/duration/interests | ✅ Done |
| Model selection | Primary: `gemini-3.6-flash`, Secondary: `gemini-3.5-flash-lite` | ✅ Done |
| Generations executed | 10 of 12 scenarios (2 blocked by free-tier daily quota exhaustion) | ✅ Done |
| Schema validation | 10/10 responses are schema-valid (JSON parse + JSON Schema draft 2020-12) | ✅ Done |
| Dataset grounding | 10/10 responses reference only valid dataset places (0% hallucination) | ✅ Done |
| Contract compliance | 10/10 responses are fully contract-valid | ✅ Done |
| Failure analysis | No schema or grounding failures observed; 6 potential failure modes documented as risks | ✅ Done |
| Prompt refinements | None needed — v2.0.0 prompts produced 100% success on first run | ✅ Done |
| Results artifact | `AI/06-Gemini-Prototype/EXPERIMENT_REPORT.md` + `results/experiment_results.json` | ✅ Done |
| Downstream handoff | Validation rules and AI-Orchestration integration notes prepared | ✅ Done |

**Key findings:**
- `responseJsonSchema` (full JSON Schema with `$defs`/`$ref`) works perfectly with Gemini Flash
- Zero hallucination rate across all 10 generations — model strictly uses supplied place names
- `gemini-2.0-flash` (project original) is deprecated; `gemini-3.6-flash` is the current Flash model
- Free-tier quota: 20 requests/day per model — production needs paid plan
- No prompt refinements were needed — the v2.0.0 templates are production-ready

---

## 6. Not Yet Started

- `PlaceInterest` seed file + seeder update (blocked on Backend decision, §2 above)

---

*Update this file after each new step — add a row, or a new numbered section for a new work area, so it stays a running log the whole team can read.*