# Triply — Gemini Prototype Experiment Results

**Date:** 2026-09-18
**Primary Model:** gemini-3.6-flash
**Secondary Model:** gemini-3.5-flash-lite (scenario E only)
**Schema version:** 2.0.0
**JSON Schema draft:** 2020-12

---

## 1. Task Status

**COMPLETED**

---

## 2. Acceptance Results

| Metric | Count |
|--------|-------|
| Total scenarios defined | 12 |
| Successfully generated | 10 |
| Valid JSON | 10/10 |
| Schema-valid | 10/10 |
| Dataset-grounded | 10/10 |
| Contract-valid | 10/10 |

**Acceptance criterion: 10+ schema-valid generations -> PASS**

---

## 3. Scenario Results

| Test | Scenario | Mode | Model | JSON | Schema | Grounded | Contract | Result |
|------|----------|------|-------|------|--------|----------|----------|--------|
| A | Standard Amman 3-day | DEST | 3.6-flash | PASS | PASS | PASS | PASS | PASS |
| B | Budget-constrained Amman | DEST | 3.6-flash | PASS | PASS | PASS | PASS | PASS |
| C | Paris 4-day multi-interest | DEST | 3.6-flash | PASS | PASS | PASS | PASS | PASS |
| D | New York 3-day | DEST | 3.6-flash | PASS | PASS | PASS | PASS | PASS |
| E | Budget-first multi-destination | BUDG | 3.5-flash-lite | PASS | PASS | PASS | PASS | PASS |
| F | Amman consistency test 1 | DEST | — | N/A | N/A | N/A | N/A | QUOTA |
| G | Amman consistency test 2 | DEST | 3.6-flash | PASS | PASS | PASS | PASS | PASS |
| H | Paris 2-day short trip | DEST | — | N/A | N/A | N/A | N/A | QUOTA |
| I | New York 5-day long trip | DEST | 3.6-flash | PASS | PASS | PASS | PASS | PASS |
| J | Budget-first tight budget | BUDG | 3.6-flash | PASS | PASS | PASS | PASS | PASS |
| K | Amman nature/adventure 5-day | DEST | 3.6-flash | PASS | PASS | PASS | PASS | PASS |
| L | Paris luxury 3-day | DEST | 3.6-flash | PASS | PASS | PASS | PASS | PASS |

**Note:** Scenarios F and H were blocked by free-tier daily quota exhaustion (20 requests/day/model). These are not schema failures — they are infrastructure limitations.

---

## 4. Failure Mode Analysis

**No schema or grounding failures were observed in the 10 successful generations.**

All 10 responses:
- Produced valid JSON that parsed without errors
- Conformed to the finalized JSON Schema (draft 2020-12) with `additionalProperties: false`
- Referenced only places from the supplied dataset (0% hallucination rate)
- Placed ACCOMMODATION-category places only in the `accommodation` object (never in `days`)
- Included at least one RESTAURANT-category place per day
- Included at least one TRANSPORT-category place across all days
- Used correct `time_slot` enums (MORNING, AFTERNOON, EVENING)
- Used correct `order_index` values (>= 1)
- Used correct `day_number` values (1-indexed, contiguous)
- Used correct `date` format (YYYY-MM-DD)
- Did not output any prices, costs, or database IDs

### Potential failure modes identified (from prompt analysis, not observed)

These failure modes are documented from the upskilling module and prompt analysis as risks to monitor in production:

| Category | Risk | Mitigation |
|----------|------|------------|
| `DATASET_HALLUCINATION` | Model invents similar-sounding place names | responseJsonSchema enforces structure; domain validation checks name existence |
| `CATEGORY_VIOLATION` | Model puts hotel in days instead of accommodation | Schema structure + domain validation |
| `MISSING_RESTAURANT` | Day has no meal place | Domain validation: check RESTAURANT category per day |
| `MISSING_TRANSPORT` | No airport transfer or transit | Domain validation: check TRANSPORT category across all days |
| `PRICE_HALLUCINATION` | Model outputs numbers in notes | Prompt explicitly forbids; schema has no price fields |
| `INCOMPLETE_ITINERARY` | Fewer days than requested | Schema: days array length validated against trip duration |

---

## 5. Prompt Refinements

**No prompt refinements were necessary.** The v2.0.0 prompt templates (from `AI/02-Prompt-Engineering/prompt-templates/`) produced 100% schema-valid and dataset-grounded output on the first run.

Key prompt features that contributed to high reliability:
1. Explicit instruction to return ONLY valid JSON (no markdown, no commentary)
2. Character-for-character place name matching instruction
3. Explicit prohibition of prices, costs, and database IDs
4. Category-grouped place lists for clarity
5. `responseJsonSchema` enforcement of structure

---

## 6. Final Artifacts

| Artifact | Path |
|----------|------|
| Experiment script | `AI/06-Gemini-Prototype/run_experiments.py` |
| Validation script | `AI/06-Gemini-Prototype/validate_saved.py` |
| Results report | `AI/06-Gemini-Prototype/EXPERIMENT_REPORT.md` |
| Experiment results (JSON) | `AI/06-Gemini-Prototype/results/experiment_results.json` |
| Raw responses | `AI/06-Gemini-Prototype/results/{A-L}_raw.json` |
| Parsed responses | `AI/06-Gemini-Prototype/results/{A-L}_parsed.json` |
| System prompts | `AI/06-Gemini-Prototype/prompts/{A-L}_system.txt` |
| User prompts | `AI/06-Gemini-Prototype/prompts/{A-L}_user.txt` |
| Schema | `AI/02-Prompt-Engineering/json-schemas/triply-trip-plan-generation.schema.json` |
| Schema validation harness | `AI/03-Validation/validate_schema.py` |

---

## 7. Configuration

- **Primary Model:** `gemini-3.6-flash` (9 of 10 successful runs)
- **Secondary Model:** `gemini-3.5-flash-lite` (1 run, scenario E)
- **Temperature:** 0.4
- **Response MIME type:** `application/json`
- **Response schema:** `responseJsonSchema` (full JSON Schema draft 2020-12 with `$defs`/`$ref`)
- **Schema enforcement:** Gemini's structured output mode guarantees JSON shape
- **Dataset:** 57 active places, 3 destinations (Paris, Amman, New York), 5 categories
- **Free-tier quota:** 20 requests/day per model
- **Rate limit handling:** Exponential backoff (5s, 10s, 20s) with 3 retries

---

## 8. Downstream Handoff

### For Design AI-Output Validation Rules

**What schema validation catches automatically (via responseJsonSchema + jsonschema):**
- Missing required fields (`planning_mode`, `destination_options`, `destination_name`, `accommodation`, `days`, `items`, `time_slot`, `order_index`, `place_name`)
- Wrong types (e.g., `nights` as string instead of integer)
- Invalid enums (`time_slot` not in MORNING/AFTERNOON/EVENING)
- Unexpected fields (`additionalProperties: false` at every level)
- Structural issues (empty arrays, missing required nested fields)

**What requires domain validation (not caught by schema alone):**
- Dataset grounding: every `place_name` must match an active `Place.name` in the database
- Dataset grounding: `destination_name` must match a supported `Destination.name`
- Category rules: accommodation must be ACCOMMODATION category
- Category rules: no ACCOMMODATION places in `days`
- Category rules: at least one RESTAURANT per day
- Category rules: at least one TRANSPORT across all days
- Day count: `days.length` must match trip duration
- Day numbering: contiguous 1-indexed with no gaps
- Date alignment: `date` must match `start_date + day_number - 1`

**Validation pipeline (recommended order):**
1. JSON parse check
2. Schema validation (jsonschema)
3. Dataset grounding (0% tolerance)
4. Category rules
5. Structural consistency (day count, date alignment)

### For AI-Orchestration Backend Module

| Parameter | Value |
|-----------|-------|
| **Model** | `gemini-3.6-flash` (or `gemini-3.5-flash-lite` for budget) |
| **Schema path** | `AI/02-Prompt-Engineering/json-schemas/triply-trip-plan-generation.schema.json` |
| **Schema version** | `2.0.0` |
| **Response format** | `application/json` with `responseJsonSchema` |
| **Temperature** | `0.4` |
| **System instruction** | `destination_first.md` or `budget_first.md` (after placeholder substitution) |
| **maxItems** | Set per-request: `1` for DESTINATION_FIRST, `3` for BUDGET_FIRST |
| **Validation entry point** | `IItineraryValidator.ValidateAsync()` in `ItineraryValidationService.cs` |
| **Known limitations** | Free-tier: 20 req/day/model; production needs paid plan |
| **Retry strategy** | Exponential backoff for 503/429 errors, max 3 retries |

---

## 9. Known Limitations

1. **Free-tier quota** — 20 requests/day per model limited the experiment to 10 successful runs. 2 scenarios (F, H) were not tested due to quota exhaustion. These are structurally identical to tested scenarios and are expected to pass.

2. **Single API key** — All experiments used the same API key. Production should use separate keys per environment.

3. **No error-case testing** — The experiment focused on successful generations. Error cases (malformed input, missing places, etc.) should be tested in the validation harness.

4. **Temperature 0.4 only** — Only one temperature setting was tested. Lower temperatures (0.0-0.2) may improve consistency; higher temperatures (0.6-0.8) may improve variety.

5. **No multi-turn testing** — All experiments were single-shot. Conversational refinement (FR-TRIP-005) is post-MVP.

6. **Dataset scale** — 57 places across 3 destinations is a small dataset. Production with more destinations should test larger context windows.

7. **Model deprecation** — `gemini-2.0-flash` (project's original choice) is no longer available. `gemini-3.6-flash` is the current Flash-family model. The Backend's `GeminiOptions.Model` default should be updated.
