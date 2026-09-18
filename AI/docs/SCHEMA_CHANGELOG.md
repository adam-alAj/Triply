# Triply — Gemini Output Schema Changelog

**Schema:** `triply-trip-plan-generation.schema.json`
**Contract:** `TRIPLY_AI_JSON_SCHEMA_CONTRACT_v2.md`
**Last updated:** 2026-09-18

---

## v2.0.0 — Backend Alignment (2026-09-18)

**Status:** Backend DTOs, validation, and prompt builder updated to match the v2.0.0 contract.

### What Changed

| Component | Before (v1-era) | After (v2.0.0 aligned) | Reason |
|-----------|-----------------|------------------------|--------|
| **Backend DTOs** (`AiOrchestrationDtos.cs`) | Flat `{ Days[] }` with `PlaceId` (numeric) | `{ planning_mode, destination_options[] }` with `place_name` (string) | Contract v2.0.0 §1a: root shape restructured, IDs removed |
| **Accommodation** | Not modeled (hotel was a daily item) | Separate `accommodation` object per option with `place_name` + `nights` | Contract v2.0.0 §1a #5: hotel is trip-scoped, not day-scoped |
| **Planning mode** | Not in DTOs | `planning_mode` enum in root + `DestinationOptionDto` | Contract v2.0.0 §1a #6: unified schema for both modes |
| **Validation service** | Validated `PlaceId` existence | Validates `place_name` exact match + category rules | Contract v2.0.0 §5: dataset grounding by name, not ID |
| **Prompt builder** | Flat prompt with `placeId` | Structured prompt with `place_name` + category groups | Contract v2.0.0 §4: model never sees or returns IDs |
| **Gemini client** | `responseMimeType` only | `responseMimeType` + `responseJsonSchema` support | Gemini structured output with full JSON Schema enforcement |

### Why Each Change Was Necessary

1. **Root shape change** — The v2.0.0 contract merges `DESTINATION_FIRST` and `BUDGET_FIRST` into a single schema with `planning_mode` + `destination_options[]`. The old flat `{ Days[] }` shape only supported single-destination plans.

2. **`place_name` replaces `PlaceId`** — Design Principle 1 (§2): "The AI never outputs a database ID — of any kind." Gemini has no knowledge of internal IDs. Using `place_name` for grounding is the FR-AI-002 enforcement point.

3. **Accommodation separation** — A hotel is booked for the whole stay, not a single day/time_slot. Modeling it as a per-day `ItineraryItem` was a mismatch (§1a #5).

4. **No cost fields** — Design Principle 3 (§2): "Cost is never proposed by the model." Every price comes from `Place.reference_price` server-side. The old DTO had no cost fields either, but the prompt was ambiguous about it.

5. **`responseJsonSchema`** — Per Gemini API docs, `responseJsonSchema` accepts full JSON Schema (draft 2020-12) including `$defs`/`$ref`, which our schema uses. The old `responseSchema` (OpenAPI subset) doesn't support `$defs`.

### Dataset Compatibility

No dataset changes were required. The schema validates against actual `Place.name` values from `Place.csv`:
- 57 active places across 3 destinations (Paris, Amman, New York)
- 5 categories: ATTRACTION, RESTAURANT, ACTIVITY, ACCOMMODATION, TRANSPORT
- All `place_name` values in the schema's examples come from the real dataset

### Prompt Compatibility

Both prompt templates (`destination_first.md`, `budget_first.md`) already reference the v2.0.0 schema. The Backend prompt builder now generates the same structure:
- Uses `place_name` (not `PlaceId`) for grounding lists
- Groups places by category for clarity
- Explicitly forbids IDs, prices, and invented names

### Backend Integration Notes

1. **`maxItems` must be set per-request** — Before calling Gemini, Backend must set `destination_options.maxItems` to `1` (DESTINATION_FIRST) or `3` (BUDGET_FIRST) on the schema object. This is per contract §5, step 0.

2. **Accommodation persistence** — The accommodation `ItineraryItem` is written once per option on `day_number = 1` with `time_slot = MORNING` and `order_index = 0`. This is a Backend convention, not part of the contract.

3. **Budget check is separate** — The budget check (§5 step 4) is deterministic Backend computation from `Place.reference_price`. It's not part of schema validation.

4. **`PlaceContextDto.BudgetTier`** — The prompt builder includes a `BudgetTier` field for budget-first mode. This comes from `Extra_AI_Context.csv` (not yet joined in the query — marked as TODO).

### Validation Results

| Check | Result |
|-------|--------|
| Valid JSON Schema (draft 2020-12) | PASS |
| All `$ref` references resolve | PASS |
| 15/15 representative test cases | PASS |
| Contract compatibility | PASS |
| Dataset compatibility | PASS |
| Prompt compatibility | PASS |

### Files Changed

| File | Change |
|------|--------|
| `Backend/Triply.Api/Modules/AI-Orchestration/Dtos/AiOrchestrationDtos.cs` | Rewritten: v2.0.0 DTOs with `planning_mode`, `destination_options`, `place_name`, `accommodation` |
| `Backend/Triply.Api/Modules/AI-Orchestration/ItineraryValidationService.cs` | Rewritten: validates v2.0.0 structure, name-based grounding, category rules |
| `Backend/Triply.Api/Modules/AI-Orchestration/ItineraryPromptBuilder.cs` | Rewritten: generates v2.0.0 prompts with `place_name` grounding |
| `Backend/Triply.Api/Modules/AI-Orchestration/AiOrchestrationService.cs` | Updated: handles new DTO structure, name-to-ID resolution for persistence |
| `Backend/Triply.Api/Modules/AI-Orchestration/GeminiClient.cs` | Added `GenerateJsonWithSchemaAsync` with `responseJsonSchema` support |
| `AI/03-Validation/validate_schema.py` | New: validation harness with 15 test cases |

---

## v2.0.0 — Initial Schema (previous commit)

See `TRIPLY_AI_JSON_SCHEMA_CONTRACT_v2.md` for the full contract.
The `triply-trip-plan-generation.schema.json` file was created in a prior task.
