# Triply AI — Progress

> **Current status update (2026-09-24):** The Jerusalem/Palestine conflict recorded below was resolved by PR #66 and migration `20260921001200_RemoveBackendStaticSeed`. The retired `Backend/Triply.Api/Data/seed-dataset.sql` is not an executable seed path; development provisioning reads the curated CSV dataset (`Backend/Triply.Api/Data/SeedData.cs`), and generation validation filters destinations by `IsSupported`. The earlier rows are historical coordination notes, not open blockers. `PlaceInterest` seeding is implemented by the backend curated-data loader. Sections 5–13 were added on 2026-09-24 to cover all AI-track work done since 2026-09-19.

*Owner: Aya Maali & Adam Alafandi · Track: AI/ML · Last updated: 2026-09-24*

This document tracks work done on the AI track — what was added, why, and its current status — so the rest of the team can follow along without digging through commits.

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

## 2. Interest-Aware Destination Suggestions — ✅ Complete

**Gap found:** `DestinationSuggestionService.cs` required `InterestCategoryIds` in the request but never used it — suggestions were budget-only. Root cause: no DB relationship between `InterestCategory` and `Destination`/`Place`.

| Step                                                  | Detail                                                                                                                                                                                                                                                                                                                                                               | Status                                  |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------- |
| Investigated backend code                             | Confirmed the gap in `DestinationSuggestionService.cs`, `DestinationSuggestionDtos.cs`, `DestinationSuggestionRequestValidator.cs`, and the entity model (`ReferenceEntities.cs`, `TripSchema.cs`)                                                                                                                                                                   | ✅ Done                                  |
| Cross-checked data readiness                          | `Extra_AI_Context.csv` had `interest_tag` per place; all non-blank values map 1:1 to `InterestCategory.Code`. 9 of 57 places had a blank tag                                                                                                                                                                                                                         | ✅ Done                                  |
| Spec written                                          | `AI/05-Destination-Suggestion/Interest_Aware_Destination_Suggestions_Spec.md` — proposes a `PlaceInterest` table, matching/ranking logic, and open questions for Backend                                                                                                                                                                                             | ✅ Done                                  |
| Sent to Backend lead                                  | Sent to Lynn for review                                                                                                                                                                                                                                                                                                                                              | ✅ Done                                  |
| Pushed to GitHub                                      | `AI/05-Destination-Suggestion/` folder created and pushed                                                                                                                                                                                                                                                                                                            | ✅ Done                                  |
| Backend decision (schema + open questions in spec §5) | Backend implemented the place-level `PlaceInterest` table (migration `AddPlaceInterest`) and interest-aware ranking (match count DESC, then converted cost ASC). Destinations with **zero** interest overlap are **excluded** (covered by `Suggestions_WithNoInterestOverlap_ExcludesDestination`)                                                                   | ✅ Done                                  |
| Fill the 9 missing `interest_tag` values              | Investigated — all 9 are `PlaceCategory = TRANSPORT`. First decision was to leave them blank; **final decision:** link the 9 TRANSPORT places to `OTHER` in `PlaceInterest_seed_draft.csv` so every place has exactly one link (**57 rows**, not 48). `Extra_AI_Context.csv.interest_tag` is now reserved/blank; `PlaceInterest_seed_draft.csv` is the single source | ✅ Done (resolved by linking to `OTHER`) |
| Seeder                                                | `seed/Seed_place_interests.py` — additive, idempotent, natural-key matching, `--dry-run`. Backend `SeedData.cs` performs the same import automatically in Development                                                                                                                                                                                                | ✅ Done                                  |
| Live verification                                     | `POST /api/destinations/suggestions` returned 200 with a USD-converted ranking (Amman 596 JOD → 840.36 USD against a 1,500 USD budget), 2026-09-21                                                                                                                                                                                                                   | ✅ Done                                  |

---

## 3. Dataset Curation Schema/Workflow Mapping (TASK17) — ✅ Complete (doc sign-off pending)

**Task objective:** Define the working spreadsheet/notebook schema for curating places so it maps cleanly onto the DB's Place/Destination tables.

| Step                                     | Detail                                                                                                                                                                                                                                                                              | Status                     |
| ---------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- |
| Compared curated CSVs against DB spec    | Checked every column in `curated-data/*.csv` (Country, Currency, PlaceCategory, CostCategory, InterestCategory, Destination, Place, Extra_AI_Context) against Database Design §6.2–§6.8                                                                                             | ✅ Done                     |
| Documented column-to-table mapping       | `AI/01-Dataset/DATASET_CURATION_SCHEMA_MAPPING.md` — full column mapping, curation workflow diagram, and the known applied-schema mismatches carried over from `REVIEW_RECORD.md` §6                                                                                                | ✅ Done                     |
| Flagged `Extra_AI_Context.csv` as non-DB | Documented explicitly: no column in this file has a DB equivalent (prompt-context/provenance only); read at runtime by Backend (`AI:ExtraAiContextPath`)                                                                                                                            | ✅ Done                     |
| Committed on dedicated branch            | `AI-docs/add-schema-mapping-doc-task17`                                                                                                                                                                                                                                             | ✅ Done                     |
| Sent to Backend lead for review          | Sent to Lynn                                                                                                                                                                                                                                                                        | ✅ Done                     |
| Applied-schema mismatches (doc §5)       | All resolved on the Backend side: `Place.Name` is now `varchar(200)`, `Destination` coordinates are `decimal(9,6)`, unique index on `Places(DestinationId, Name)` exists (migration `AlignDatasetSchema`), and the reference-data conflict was removed by `RemoveBackendStaticSeed` | ✅ Resolved                 |
| Sign-off checkbox in the doc (§6)        | AI + Backend checkboxes are still unchecked in the document itself                                                                                                                                                                                                                  | ⬜ Pending — see Open Items |

---

## 4. Extra_AI_Context.csv — BUDGET_FIRST Dataset Population — ✅ Complete

**Task objective:** Fully populate `Extra_AI_Context.csv` (previously empty) so Backend can consume it for `BUDGET_FIRST` prompt context.

| Step                                                         | Detail                                                                                                                                                                                                                                                                  | Status                                              |
| ------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------- |
| Populated all 57 rows                                        | `Extra_AI_Context.csv` was empty before this pass — now has `source_place_id`, `place_name`, `destination_name`, `place_category`, `reference_price`, `currency`, `budget_tier` for all 57 places across Paris / Amman / New York                                       | ✅ Done                                              |
| Assigned `budget_tier`                                       | Every active place tagged `BUDGET` / `MID_RANGE` / `LUXURY`, assigned by price ordering **within each (destination, place_category)** — explicitly *not* comparable across destinations                                                                                 | ✅ Done                                              |
| Renamed join key                                             | Used `source_place_id` (not `place_id`) for the curation-local id, so Backend's `ExtraAiContextReader` falls back to matching on `place_name` (exact, case-sensitive vs. `Place.Name`) instead of accidentally joining on a curation id that isn't the DB id            | ✅ Done                                              |
| Decoupled interest data                                      | `interest_tag` left blank/reserved in this file — Place↔Interest links now live only in `PlaceInterest_seed_draft.csv` (all 57 places, including the 9 `TRANSPORT` places tagged `OTHER`) so there's a single source of truth                                           | ✅ Done                                              |
| Updated schema mapping doc                                   | `DATASET_CURATION_SCHEMA_MAPPING.md` §3.8 rewritten — documents the `place_name` lookup contract, the `AI:ExtraAiContextPath` runtime read (not imported into the DB), and that every active `Place` needs a `budget_tier` or Backend rejects `BUDGET_FIRST` generation | ✅ Done                                              |
| Updated `REVIEW_RECORD.md`                                   | §3 rewritten to match — removed stale claims about `source_url`/`notes` being populated (they're reserved/blank), added the `budget_tier` runtime-read note                                                                                                             | ✅ Done                                              |
| Updated Interest-Aware spec                                  | `Interest_Aware_Destination_Suggestions_Spec.md` §3 updated to point at `PlaceInterest_seed_draft.csv` instead of the now-blank `Extra_AI_Context.csv.interest_tag`                                                                                                     | ✅ Done                                              |
| Flagged pre-existing Jerusalem/Palestine conflict to Backend | Historical: reported before PR #66; the obsolete seed is now removed/retired, curated CSVs are the reference-data source, and the validator filters `IsSupported`                                                                                                       | ✅ Resolved by PR #66; see current-status note above |
| Sent to Backend                                              | Message sent confirming the file is ready, explaining the `place_name` matching + `budget_tier` semantics, and flagging the Jerusalem/Palestine conflict above                                                                                                          | ✅ Done                                              |

**Key findings:**
- `budget_tier` is fully populated and documented as consumed at runtime by `ExtraAiContextReader`, not imported into the DB
- `Extra_AI_Context.csv` is intentionally not the source for interests anymore; `PlaceInterest_seed_draft.csv` is
- The former Jerusalem/Palestine conflict is resolved for current seed and generation paths. Populated legacy databases are preserved by migration; their rows are not used as supported generation destinations unless marked `IsSupported`.
- Backend Docker image now bakes `Extra_AI_Context.csv` (and compose mounts `../AI:/AI:ro` for local override), so BUDGET_FIRST works inside containers.

---

## 5. AI JSON Schema Contract v2.0.0 — ✅ Complete (sign-off pending)

**Owners:** Aya Maali, Anas Musleh, Adam Alafandi · **Backend counterpart:** Lynn Sharbati

| Step                                            | Detail                                                                                                                                                                                                                                                      | Status     |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| Contract v2.0.0 written                         | `AI/docs/TRIPLY_AI_JSON_SCHEMA_CONTRACT_v2.md` — root shape `{ planning_mode, destination_options[] }`, names-only (no IDs), `accommodation` object separate from `days[]`, **no cost fields** (`cost_summary`, `estimated_cost`, `place_category` removed) | ✅ Done     |
| Formal schema                                   | `02-Prompt-Engineering/json-schemas/triply-trip-plan-generation.schema.json` (draft 2020-12, `additionalProperties: false` at every level)                                                                                                                  | ✅ Done     |
| Schema changelog                                | `AI/docs/SCHEMA_CHANGELOG.md` — v1 → v2.0.0 changes and rationale, backend alignment notes                                                                                                                                                                  | ✅ Done     |
| Cost tolerance (SRS D1, ±15%)                   | Closed as **N/A** — there is no model-provided cost to tolerate. Backend's dead `CostTolerancePercent` config was removed                                                                                                                                   | ✅ Resolved |
| Budget policy for BUDGET_FIRST                  | Resolved (2026-09-23): **at least one fitting option** — Backend persists the first grounded option within budget and fails only if none fit (`ALL_OPTIONS_OVER_BUDGET`). DESTINATION_FIRST flags over-budget (`isOverBudget`) instead of failing           | ✅ Resolved |
| BUDGET_FIRST without a pre-selected destination | Product decision (2026-09-23): BUDGET_FIRST may generate with **no destination selected**, so the 1–3 option rule is real; the first fitting option's destination is persisted onto the trip                                                                | ✅ Resolved |
| Per-mode `maxItems` (contract §5 step 0)        | Implemented in Backend (`ItineraryGenerationSchema.LoadForMode`): 1 for DESTINATION_FIRST, 3 for BUDGET_FIRST. Committed schema stays mode-agnostic                                                                                                         | ✅ Done     |
| Schema-version provenance                       | `AIGeneration.SchemaVersion` stored on every full/partial attempt (migration `AddAiGenerationSchemaVersion`); older rows = `unknown`                                                                                                                        | ✅ Done     |
| Two schema copies kept identical                | AI copy + `Backend/Triply.Api/AI-Schemas/` copy; guarded by the Python parity check and `SchemaFileParityTests`                                                                                                                                             | ✅ Done     |
| Lead sign-off (contract §7)                     | AI lead + Backend lead checkboxes still unchecked                                                                                                                                                                                                           | ⬜ Pending  |

---

## 6. Prompt Templates & Backend Integration Requirements — ✅ Complete

| Step                     | Detail                                                                                                                                                                                                                                                           | Status                                                                                                                                                                                             |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Prompt templates         | `02-Prompt-Engineering/prompt-templates/destination_first.md` and `budget_first.md` — system instruction (exact-name copy, no IDs/prices, ≥1 RESTAURANT per day, ≥1 TRANSPORT per option, accommodation only in `accommodation`) + user prompt with placeholders | ✅ Done                                                                                                                                                                                             |
| Backend requirements doc | `BACKEND_INTEGRATION_REQUIREMENTS.md` — placeholder filling, active-places-only, template choice by planning mode, per-mode `maxItems`, validation order, never take IDs/prices from the model                                                                   | ✅ Done — all 7 items are now implemented in Backend (`ItineraryPromptBuilder`, `ItineraryValidationService`, `AiOrchestrationService`); the doc's "not yet built" status line still needs updating |
| BUDGET_FIRST context     | `budget_tier` (BUDGET / MID_RANGE / LUXURY) supplied to the model instead of prices, from `Extra_AI_Context.csv`                                                                                                                                                 | ✅ Done                                                                                                                                                                                             |

---

## 7. Gemini API + Prompt-Engineering Upskilling Module — ✅ Complete

- `AI/04-Learning-Notes/Gemini_Prompt_Engineering_Upskilling_Module.md` (Arabic, based on real Triply/Amman data).
- Covers: `generateContent` vs Interactions API (decision: `generateContent` for MVP), `x-goog-api-key` auth, request/response envelope and `finishReason`, prompt design principles, `responseSchema` vs `responseJsonSchema` (decision: **`responseJsonSchema`**, because our schema uses `$defs`/`$ref`), why structured output guarantees shape but not content (so dataset grounding is mandatory), common failure table.

---

## 8. Gemini Prototype Experiments — ✅ Complete

**Owner:** Adam Alafandi · Folder: `AI/06-Gemini-Prototype/`

| Item                               | Result                                                                                                                                                                 |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Scenarios defined                  | 12 (A–L): Amman / Paris / New York, 2–5 days, DESTINATION_FIRST and BUDGET_FIRST                                                                                       |
| Successful generations             | **10/10** — valid JSON, schema-valid, dataset-grounded, contract-valid                                                                                                 |
| Not run                            | F and H — free-tier quota (20 req/day/model), not schema failures                                                                                                      |
| Hallucinated places / prices / IDs | **0** observed                                                                                                                                                         |
| Prompt refinements needed          | None — v2.0.0 templates passed on the first run                                                                                                                        |
| Config                             | Temperature 0.4, `application/json`, `responseJsonSchema`; models `gemini-3.6-flash` (9 runs) and `gemini-3.5-flash-lite` (1 run)                                      |
| Artifacts                          | `run_experiments.py`, `validate_saved.py`, `EXPERIMENT_REPORT.md`, `results/{A–L}_raw/parsed.json`, `results/experiment_results.json`, `prompts/{A–L}_system/user.txt` |
| Acceptance (≥10 schema-valid)      | ✅ PASS                                                                                                                                                                 |

---

## 9. Latency Testing — Flash vs Flash-Lite — ✅ Complete

**Owner:** Adam Alafandi · Folder: `AI/07-Latency-Testing/`

| Model                   | Samples | p50 (s) | p95 (s) | min  | max   | Failures |
| ----------------------- | ------- | ------- | ------- | ---- | ----- | -------- |
| `gemini-3.6-flash`      | 20      | 12.35   | 16.04   | 9.67 | 18.71 | 0        |
| `gemini-3.5-flash-lite` | 20      | 2.83    | 6.47    | 1.73 | 17.49 | 0        |

- 20 prompts (A–T), mix ~15 DESTINATION_FIRST : 5 BUDGET_FIRST.
- Flash-Lite is ~4–5× faster. The 17.49 s first call looks like a cold-start (p95 ≈ 4.4 s without it) — worth a confirming re-run.
- Recommendation: Flash-Lite for interactive generation. Backend default is `gemini-flash-lite-latest`.
- Artifacts: `latency_test.py`, `results/latency_raw.csv`, `LATENCY_REPORT.md`, `Readme.md`.

---

## 10. AI-Output Validation Rules & Harness — ✅ Complete

**Owner (rules):** Adam Alafandi · **Enforcement:** Backend (Lynn Sharbati)

| Item                         | Detail                                                                                                                                                                                                                                                                                               | Status |
| ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| Rules spec                   | `03-Validation/AI_OUTPUT_VALIDATION_RULES.md` v2.0.0                                                                                                                                                                                                                                                 | ✅ Done |
| **V-001** 0%-invented-place  | Exact, case-sensitive `Place.name` match; active places only; scoped to the option's destination; destination must be `IsSupported`; category rules (accommodation only in `accommodation`, ≥1 RESTAURANT per day, ≥1 TRANSPORT per option); duplicate active name inside a destination fails closed | ✅ Done |
| **V-002** budget feasibility | Deterministic, from `Place.reference_price` (accommodation = price × nights), converted to the trip's budget currency; DESTINATION_FIRST flags, BUDGET_FIRST selects first fit / fails `ALL_OPTIONS_OVER_BUDGET`                                                                                     | ✅ Done |
| Validation order             | Schema → structure → grounding → category → budget → persist                                                                                                                                                                                                                                         | ✅ Done |
| Python harness               | `harness.py` (V-001, V-002, Step 1 structural, 10 fixture tests), `validate_schema.py` (15 schema test cases), `requirements.txt`                                                                                                                                                                    | ✅ Done |
| Real-data run                | `reports/VALIDATION_REPORT.md` + `validation_results.json`: 138 place references, **0 invalid → 0.00% invented-place rate**                                                                                                                                                                          | ✅ PASS |
| Backend enforcement          | `ItineraryValidationService` + `AiOrchestrationService`; DB backstop: `ItineraryItem.PlaceId` NOT NULL FK                                                                                                                                                                                            | ✅ Done |
| HTTP-layer proof             | `AiGroundingIntegrationTests`: invented place → 422 and nothing persisted, unknown JSON field rejected, non-JSON output, retry then success, Gemini 5xx / timeout, over-budget flag, BUDGET_FIRST no-fit → 422, BUDGET_FIRST without destination keeps the affordable option                         | ✅ Done |
| Traceability                 | Spec §9 maps every FR-AI-002 requirement to an implementation                                                                                                                                                                                                                                        | ✅ Done |

---

## 11. Python ↔ C# Validation Parity — ✅ Complete

- Shared fixture file: `03-Validation/fixtures/v001-v002-parity-fixtures.json` — 12 cases (9 rule cases + 3 structural).
- Python runner: `validate_shared_fixtures.py` — 12/12 pass; also runs the **schema-file parity check** between the AI copy and the Backend copy.
- C# runners: `ValidationParityFixtureTests.cs` (drives the real `ItineraryValidationService`) and `SchemaFileParityTests.cs`.
- Failure-reason checks added (`expectedFailureChecks`): Python asserts failure **codes**, C# asserts error **fragments**, so a case must fail for the right reason.
- Harness fixes found during reconciliation: Step 1 structural checks implemented; duplicate active place names now fail closed (before, the last CSV row silently won).
- Documented rule differences table in spec §13 (currency conversion, schema engine, etc.).
- Note: the Python side runs on any machine; the C# runner needs the .NET SDK (`dotnet test`).

---

## 12. Dataset Tooling, Versioning & Provisioning — ✅ Complete

**Owners:** Aya Maali, Anas Musleh, Adam Alafandi

| Item                         | Detail                                                                                                                                                            | Status                          |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------- |
| Curated dataset v1.0.0       | 3 destinations (Paris, Amman, New York), 57 places (19 each), 8 curated CSVs                                                                                      | ✅ Done                          |
| Review record                | `REVIEW_RECORD.md` — conventions (exact-name grounding key, price units), review checks, seed verification                                                        | ✅ Done (sign-off table pending) |
| Manifest                     | `seed/DATASET_MANIFEST.json` (SHA-256 + row counts) + `generate_manifest.py`                                                                                      | ✅ Done                          |
| Seeders (ops/offline use)    | `seed_places.py`, `Seed_place_interests.py`, `seed_exchange_rates.py` — additive, idempotent, natural-key matching, `--dry-run`                                   | ✅ Done                          |
| Place↔Interest data          | `PlaceInterest_seed_draft.csv` — **57 rows** (9 TRANSPORT places linked to `OTHER`)                                                                               | ✅ Done                          |
| `budget_tier`                | `Extra_AI_Context.csv`, 57/57, relative per (destination, category), read at runtime by Backend                                                                   | ✅ Done                          |
| Automatic provisioning       | Backend `SeedData.cs` loads the same CSVs at Development startup (fail-fast, additive, idempotent). Python seeders are no longer part of the standard local setup | ✅ Done (Backend)                |
| Notebook                     | `notebooks/dataset_environment_check.ipynb` — shape/nulls + manifest row-count check (ALL MATCH)                                                                  | ✅ Done                          |
| Jerusalem/Palestine conflict | Resolved: static seed removed (`RemoveBackendStaticSeed`), CSVs are the only reference source, validator filters `IsSupported`                                    | ✅ Resolved                      |

---

## 13. AI Audit & Live Verification — ✅ Complete

- `AI_BACKEND_ARCHITECTURE_AUDIT.md` (2026-09-21) audited the backend + AI artifacts against the docs. Its 2026-09-23 addendum and the follow-up work show the main gaps closed: `IsSupported` filter in the validator, per-mode `maxItems`, V-002 budget check, dead `CostTolerancePercent` removed, API key moved from the URL to the `x-goog-api-key` header, graceful duplicate-name handling, schema-version provenance, 30-day raw-output retention, dataset auto-provisioning, and HTTP-layer negative tests.
- Live end-to-end generation with real Gemini: grounded Paris itinerary (2026-09-21, 1 attempt) and on Render (2026-09-24, `attemptsUsed: 1`, real dataset places, `isOverBudget: false`).
- Interest-aware suggestions live-verified (see section 2).
- Still to confirm live: one BUDGET_FIRST generation and the saved `CostEstimate` rows (tracked by Backend).

---

## 14. Open Items

| #   | Item                                                                                                                                                    | Owner              |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------ |
| 1   | Get sign-offs: contract v2.0.0 §7, `REVIEW_RECORD.md` §7, `DATASET_CURATION_SCHEMA_MAPPING.md` §6                                                       | AI + Backend leads |
| 2   | Add `PlaceInterest_seed_draft.csv` to `DATASET_FILES` in `generate_manifest.py` and regenerate the manifest (spec §6 checkbox still open)               | AI                 |
| 3   | Run `generate_manifest.py` in verify mode: the manifest dates from 2026-09-15, before `Extra_AI_Context.csv` was populated, so its hash is likely stale | AI                 |
| 4   | Re-verify all reference prices (`price_updated_at = 2026-09-13`, Database Design §28)                                                                   | AI                 |
| 5   | Sync `destination_first.md` with the backend prompt builder (the template has 9 rules; the prototype and backend prompts add "Return ONLY valid JSON")  | AI + Backend       |
| 6   | Add a small test asserting the C# prompt contains all contract rules (prompt ↔ template parity is still manual)                                         | AI + Backend       |
| 7   | Re-run scenarios F and H, and test other temperatures (only 0.4 was tested)                                                                             | AI                 |
| 8   | Re-run Flash-Lite latency to confirm the 17.49 s outlier is a cold-start                                                                                | AI                 |
| 9   | Update the `BACKEND_INTEGRATION_REQUIREMENTS.md` status line to "implemented" and link the Backend module                                               | AI                 |
| 10  | Refresh stale wording in `SCHEMA_CHANGELOG.md` and `Backend/docs/API Contract.md` examples                                                              | AI + Backend       |

---

*Update this file after each new step — add a row, or a new numbered section for a new work area, so it stays a running log the whole team can read.*