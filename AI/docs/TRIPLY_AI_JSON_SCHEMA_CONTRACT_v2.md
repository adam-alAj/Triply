# TRIPLY — AI JSON SCHEMA CONTRACT (Gemini Trip Plan Output)

**Document status:** DRAFT — pending sign-off (re-opened for v2.0.0)
**Schema version:** `2.0.0` (supersedes `1.0.0`)
**Applies to requirement:** FR-AI-001 (itinerary generation), FR-AI-002 (output validation), FR-TRIP-002 (budget-first destination suggestion — now merged into this contract, see §1a)
**References:** Architecture ADR-01 (§4), Architecture §9 (AI Generation Lifecycle), Database Design §6.15 (`AIGeneration`), §6.13 (`ItineraryItem`), §6.3 (`Destination`), §6.5 (`Place`), §11 (Itinerary Data Model)
**Owners:** AI track (Aya Maali, Anas Musleh, Adam Alafandi) — schema content · Backend (Lynn Sharbati) — integration/enforcement
**Sign-off required from:** AI lead + Backend lead (see §7) — **v1.0.0 sign-off does not carry over; this is a major version bump per §8 and needs fresh approval**

---

This document defines the **exact structured JSON shape** the Gemini API must return for a trip-plan generation request (covering both planning modes — see §1a), and the validation rules Backend applies to that output before any data is written to the database or shown to a user.

This is a **contract**, not an implementation detail. Per ADR-01, the AI track owns this schema and the validation-rule *definitions*; Backend owns the live Gemini call and *enforces* the rules in code. Schema drift between what AI prompts for and what Backend expects is the named risk this document exists to prevent — any change to this schema requires a version bump and re-agreement from both leads (§7, §8).

## 1a. Summary of Changes from v1.0.0

| #   | Change                                                                                                                                     | Why                                                                                                                                                                                                                                                                                                                                                                                                                          |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **`cost_summary` block removed entirely.** The model no longer returns any cost figures.                                                   | Per Database Design §12/§14, `CostEstimate` is deterministic backend-computed data, not AI-generated data. v1.0.0 kept a model-proposed `cost_summary` as a "coherence check," but in practice it added a ±15%-tolerance validation step and a whole field category with no authoritative use — it was never the source of truth and added surface area for `FAILED_VALIDATION` without added safety. Removed for v2.0.0.    |
| 2   | **`estimated_cost` removed from `ItineraryItem`.**                                                                                         | Same reasoning as #1, at the item level. `ItineraryItem.estimated_cost` is copied from `Place.reference_price` server-side (Database Design §15); the model's number was never written to the DB even in v1.0.0 — v2.0.0 just stops asking for it.                                                                                                                                                                           |
| 3   | **`place_category` removed from `ItineraryItem`.**                                                                                         | v1.0.0 kept this as a cheap pre-lookup category check. v2.0.0 relies on the exact `place_name` match alone (Backend still resolves the full `Place` row, category included, from the DB — it just no longer cross-checks it against a model-supplied value). See §9 for the case this reopens if name collisions become a real problem.                                                                                      |
| 4   | **`trip_id` and `destination_id` removed from the root object.** Replaced by `planning_mode` and `destination_options[].destination_name`. | Extends Design Principle 1 (§2): the model never outputs *any* database ID — not `Place.id`, and now not `Destination.id` either. `destination_name` is resolved server-side by exact lookup, the same way `place_name` always was. `trip_id` echo-back is dropped; Backend already knows which request it's matching a response to via the API call itself, so the echo added a validation step without closing a real gap. |
| 5   | **New: `accommodation` object, one per `destination_option`, separate from `days[]`.**                                                     | A hotel is booked for the whole stay, not a single day/time_slot — modeling it as a per-day `ItineraryItem` was a mismatch. `accommodation.nights × Place.reference_price` is computed by Backend. See §4.3.                                                                                                                                                                                                                 |
| 6   | **New: root object is now `{ planning_mode, destination_options[] }` instead of a single flat itinerary.**                                 | Merges what v1.0.0 called out as a *separate, smaller contract* for FR-TRIP-002 (§9, v1.0.0) into this one. `destination_options` holds exactly 1 entry for `DESTINATION_FIRST` and 1–3 for `BUDGET_FIRST` — one schema, one validation pipeline, one `AIGeneration` row per attempt regardless of mode. See §3.                                                                                                             |

---

## 2. Design Principles

1. **The AI never outputs a database ID — of any kind.** Gemini has no knowledge of internal `Place.id` or `Destination.id` values. The model outputs a **place name** and a **destination name**; Backend resolves both by exact lookup. This is the FR-AI-002 enforcement point — see §5.
2. **No invented fields.** The AI must not return confidence scores, model metadata, cost figures, or explanatory prose outside the defined fields (matches Design doc Principle 04 — "AI should feel helpful, not magical").
3. **Cost is never proposed by the model — not a number, not a string, not a summary.** Every price shown to the user or written to the DB comes from `Place.reference_price`, looked up server-side after grounding. This removes the entire `cost_summary`/tolerance-band validation step that existed in v1.0.0 (see §1a, #1–2).
4. **Accommodation is trip-scoped, not day-scoped.** Modeled as a single `accommodation` object per `destination_option`, not as a recurring `ItineraryItem`.
5. **One schema, two planning modes.** `planning_mode` and the `destination_options[]` cardinality (1 vs. 1–3) let Backend build one request/response contract for both `DESTINATION_FIRST` and `BUDGET_FIRST` flows (Trip.planning_mode, Database Design §6.9), instead of maintaining two contracts.
6. **Everything maps 1:1 to an existing table.** `destination_options[].days[]` → `ItineraryDay`, `days[].items[]` → `ItineraryItem`, `destination_options[].accommodation` → one `ItineraryItem` row (category `ACCOMMODATION`) written once per option. Nothing in this schema requires a new table.
7. **Fail closed.** If the response doesn't validate against this schema, or any `place_name`/`destination_name` doesn't resolve to an active row in the dataset, the attempt is rejected (`AIGeneration.status = FAILED_VALIDATION`) — never partially written, never fabricated (FR-AI-002, SRS journey 6). Per-option budget failures in `BUDGET_FIRST` mode are handled differently — see §5, step 4.

---

## 3. Top-Level Response Shape

```json
{
  "planning_mode": "BUDGET_FIRST",
  "destination_options": [
    {
      "destination_name": "Amman",
      "accommodation": {
        "place_name": "Jordan Tower Hotel",
        "nights": 3
      },
      "days": [
        {
          "day_number": 1,
          "date": "2026-11-03",
          "items": [
            {
              "time_slot": "MORNING",
              "order_index": 1,
              "place_name": "Roman Theatre",
              "notes": "Start early to avoid midday heat."
            },
            {
              "time_slot": "AFTERNOON",
              "order_index": 1,
              "place_name": "Hashem Restaurant",
              "notes": null
            },
            {
              "time_slot": "EVENING",
              "order_index": 1,
              "place_name": "Airport Transfer – QAIA to city centre",
              "notes": "Booked for arrival day"
            }
          ]
        }
      ]
    }
  ]
}
```

For `DESTINATION_FIRST` requests, `destination_options` contains exactly one entry, built the same way.

---

## 4. Field-Level Definitions

### 4.1 Root object

| Field                 | Type                                      | Required          | Notes                                                                                                                                                                                                                                                                    |
| --------------------- | ----------------------------------------- | ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `planning_mode`       | enum: `DESTINATION_FIRST`, `BUDGET_FIRST` | Yes               | Echo of `Trip.planning_mode` as sent in the prompt context. Backend cross-checks it matches the request.                                                                                                                                                                 |
| `destination_options` | array of `DestinationOption`              | Yes, min 1, max 3 | **`DESTINATION_FIRST`: exactly 1 entry.** **`BUDGET_FIRST`: 1–3 entries**, each a complete, self-contained plan for a different supported destination. Backend sets the schema's `maxItems` per-request based on `planning_mode` before calling Gemini (see §5, step 0). |

### 4.2 `DestinationOption`

| Field              | Type                    | Required   | Notes                                                                                                                                                                                          |
| ------------------ | ----------------------- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `destination_name` | string                  | Yes        | Must exactly match a `Destination.name` from the supported-destination list given in the prompt context. Resolved server-side to `Destination.id` — never accepted as a raw ID from the model. |
| `accommodation`    | `Accommodation`         | Yes        | Exactly one hotel for the whole stay in this destination. See §4.3.                                                                                                                            |
| `days`             | array of `ItineraryDay` | Yes, min 1 | Length must equal the trip's requested day count (`end_date - start_date + 1`). A mismatch is a validation failure, not a silent truncation/padding.                                           |

### 4.3 `Accommodation`

| Field        | Type    | Required    | Notes                                                                                                                                                                                                                                                                                                                                                                        |
| ------------ | ------- | ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `place_name` | string  | Yes         | Must exactly match a `Place.name` whose resolved `place_category_id` is `ACCOMMODATION`, scoped to this option's `destination_name`.                                                                                                                                                                                                                                         |
| `nights`     | integer | Yes, `>= 1` | Backend computes `nights × Place.reference_price` (a per-night rate) as the accommodation's total cost. The model is never asked to do this multiplication. Backend persists this as a single `ItineraryItem` row (category `ACCOMMODATION`), conventionally on `day_number = 1`; the specific slot placement is a Backend implementation detail, not part of this contract. |

### 4.4 `ItineraryDay`

| Field        | Type                     | Required   | Notes                                                                                                                                                                                                                                                     |
| ------------ | ------------------------ | ---------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `day_number` | integer                  | Yes        | 1-indexed, CHECK > 0 (matches `ItineraryDay.day_number`). Must be unique within `days[]` and contiguous (1, 2, 3…) — no gaps.                                                                                                                             |
| `date`       | string (`YYYY-MM-DD`)    | Yes        | Must fall within `[Trip.start_date, Trip.end_date]` and be consistent with `day_number` (`date = start_date + day_number - 1`).                                                                                                                           |
| `items`      | array of `ItineraryItem` | Yes, min 1 | Non-accommodation entries only (restaurants, attractions, activities, transport) — the accommodation lives in `DestinationOption.accommodation`, never repeated here. Must include at least one `RESTAURANT`-category place for the day (see §5, step 2). |

### 4.5 `ItineraryItem`

| Field         | Type                                    | Required | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| ------------- | --------------------------------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `time_slot`   | enum: `MORNING`, `AFTERNOON`, `EVENING` | Yes      | Matches `ItineraryItem.time_slot` CHECK constraint exactly (case-sensitive, uppercase).                                                                                                                                                                                                                                                                                                                                                                                    |
| `order_index` | integer                                 | Yes      | Ordering within the time slot, starting at 1. Must be unique per `(day_number, time_slot)` pair.                                                                                                                                                                                                                                                                                                                                                                           |
| `place_name`  | string                                  | Yes      | **The FR-AI-002 enforcement field.** Must exactly match a `Place.name` value for an `is_active = true` place whose `destination_id` matches the option's resolved `destination_name` — and must **not** resolve to an `ACCOMMODATION`-category place (that belongs only in `Accommodation.place_name`). No partial/fuzzy match is accepted at the schema level — matching strategy is a Backend implementation detail, but the *contract* is exact-string-against-dataset. |
| `notes`       | string or `null`                        | No       | Free-text AI rationale/tip. Never used for anything structural — purely user-facing context. Must not contain invented pricing, confidence language, or claims not derivable from the place (Design doc Principle 04).                                                                                                                                                                                                                                                     |

**Removed in v2.0.0** (see §1a): `place_category`, `estimated_cost`. **Removed section:** the v1.0.0 `CostSummary` object (root-level `cost_summary`) no longer exists in this schema — cost is never proposed by the model (Design Principle 3, §2).

---

## 5. Validation Pipeline (maps to Architecture §9 sequence diagram)

0. **Request construction (pre-call)** — Backend reads `Trip.planning_mode` and sets the JSON Schema's `destination_options.maxItems` to `1` (`DESTINATION_FIRST`) or `3` (`BUDGET_FIRST`) before calling Gemini. This is request-building, not response validation, but is listed here because a mismatched `maxItems` is what step 1 below would otherwise catch late.
1. **Schema-shape validation** — JSON parses; required fields present; types/enums correct. Failure → `FAILED_VALIDATION`, no dataset lookups performed.
2. **Structural consistency** — for each `destination_option`: day count matches trip duration; `day_number`/`date` alignment; no gaps or duplicate `(day_number, time_slot, order_index)` tuples; at least one `RESTAURANT`-category item per day (checked after resolution in step 3); at least one `TRANSPORT`-category item somewhere across the option's `days[]` (checked after resolution in step 3).
3. **Dataset grounding (FR-AI-002, the non-negotiable rule)** — for each `destination_option`: `destination_name` resolves to exactly one supported `Destination`; every `place_name` (accommodation and daily items) resolves to exactly one active `Place` row scoped to that `destination_id`; the accommodation's resolved place has `place_category_id = ACCOMMODATION` and no daily item resolves to that category. **0% tolerance** — a single unresolved name fails that `destination_option` (see step 4 for how a failed option is handled differently in `BUDGET_FIRST` mode vs. `DESTINATION_FIRST` mode).
4. **Budget check (deterministic, Backend-computed — no model input)** — for each surviving `destination_option`: sum `(accommodation.nights × Place.reference_price)` + every daily item's `Place.reference_price`. Compare against `Trip.budget_amount`.
   - **`DESTINATION_FIRST`:** the single option is expected to fit; Backend still computes and flags the actual total either way, but budget overrun does not automatically fail the attempt — the plan is shown with its real computed total (final UX behavior TBD, see §9).
   - **`BUDGET_FIRST`:** options that don't fit the budget are **dropped from the response shown to the user**, not treated as a whole-attempt failure. If **zero** of the 1–3 returned options fit, that *is* an attempt failure (`FAILED_VALIDATION`) — the model was asked for budget-fitting options and produced none. See §9 for the exact minimum-surviving-options policy still to be confirmed.
5. **Persist** — only for `destination_option`(s) the user actually selects: `Trip.destination_id` is set (if not already), and `Itinerary`, `ItineraryDay`, `ItineraryItem` rows are written in one transaction. Every `estimated_cost` is copied from the resolved `Place.reference_price` (accommodation cost = `nights × reference_price`) — never from the model, because the model no longer sends one. `CostEstimate` rows are then computed deterministically from those persisted items (Database Design §15).
6. **On any failure** — `AIGeneration.status = FAILED_VALIDATION`, `AIGeneration.validation_errors` records what failed, bounded retry per Architecture §9, and if retries are exhausted the user sees a clear error (SRS journey 6) — never a partial or fabricated itinerary.

---

## 6. Formal JSON Schema

See companion file **`triply-trip-plan-generation.schema.json`** (JSON Schema draft 2020-12) for the machine-enforceable version of §3–§4. That file is the artifact Backend actually wires into the AI-Orchestration module's validation step; this document is its human-readable explanation and the record of agreed intent.

---

## 7. Sign-Off

| Role          | Name                                                          | Status                           | Date |
| ------------- | ------------------------------------------------------------- | -------------------------------- | ---- |
| AI track lead | Aya Maali / Anas Musleh / Adam Alafandi (nominate one signer) | ☐ Pending (re-opened for v2.0.0) | —    |
| Backend lead  | Lynn Sharbati                                                 | ☐ Pending (re-opened for v2.0.0) | —    |

This schema is a **draft** until both boxes above are checked. The v1.0.0 sign-off (if it existed) does not apply to v2.0.0 — §8's versioning policy requires fresh sign-off for any major version.

---

## 8. Versioning Policy

- Schema changes that add an optional field, a new enum value, or loosen a constraint → **minor** version bump (e.g. `2.0.0` → `2.1.0`).
- Schema changes that remove/rename a field, tighten a constraint, or change a type → **major** version bump and require re-sign-off from both leads. **This is how `1.0.0` became `2.0.0`** — five fields removed, root shape restructured (§1a).
- **Required provenance (implementation pending):** each generation record must persist the schema version used, alongside the model identifier, and the prompt templates must declare their target version. The current `AIGeneration` entity stores the model identifier in `model_provider` but has no schema-version field; therefore schema-version persistence is **not implemented** and this contract requirement is not yet satisfied. The Backend change must add and populate a persisted schema-version value before claiming provenance is tracked. Runtime validation currently uses the configured schema; do not describe it as rejecting based on a version recorded on a generation row.
- All versions are committed to source control alongside the prompt templates that target them (matches the PR-review-gated workflow in Master Plan §6).

---

## 9. Open Items / Not Yet Covered

| Item                                                                                        | Status                                                                                                                                                                                                                                                                                                                                                          |
| ------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Cost tolerance band exact value (±15% proposed, SRS D1 / Database Design DB-D5)             | **No longer applicable to this schema** — there is no model-provided cost to check a tolerance against (§1a, #1–2). DB-D5 may still matter elsewhere (e.g. how stale `Place.reference_price` is allowed to get before a manual review, per Database Design §28), but that's a dataset-freshness question, not a response-validation question for this contract. |
| Budget-first destination-suggestion response schema (FR-TRIP-002)                           | **Resolved — merged into this contract.** No longer a separate document; see §1a, #6 and §3.                                                                                                                                                                                                                                                                    |
| Minimum surviving `destination_options` for a `BUDGET_FIRST` attempt to count as successful | **Open.** §5 step 4 currently says "zero surviving options = failure," but whether 1-of-3 fitting is an acceptable success, or whether Backend should trigger a retry with an adjusted prompt to get closer to 3 valid options, is undecided.                                                                                                                   |
| Accommodation slot placement within `days[]` for persistence                                | **Open (minor).** §4.3 says "conventionally `day_number = 1`" — needs Backend to confirm which `time_slot` it writes the single accommodation `ItineraryItem` into, purely for UI display consistency.                                                                                                                                                          |
| Re-introducing `place_category` on `ItineraryItem` for name-collision safety                | **Open.** Removed in v2.0.0 (§1a, #3) on the assumption `Place.name` is effectively unique within a destination (Database Design Major Query Pattern #5 assumes this for lookup). If two active places in the same destination ever share a name across categories, this field should come back as a minor-version addition.                                    |
| Partial regeneration request/response shape (single day or single item, FR-TRIP-003)        | Out of scope for this draft — this schema covers a full trip-plan generation; a "regenerate one day" variant should reuse `ItineraryDay`/`ItineraryItem` definitions from §4.4/§4.5 but is a distinct top-level contract.                                                                                                                                       |
| Conversational refinement schema (FR-TRIP-005)                                              | Post-MVP — not drafted.                                                                                                                                                                                                                                                                                                                                         |
| Persist schema-version provenance on each `AIGeneration`                                    | **Implementation pending.** `AIGeneration` currently persists the model identifier only; add and populate a schema-version field before treating §8 provenance as implemented. |

---

## 10. Traceability

| Requirement/Doc                                     | Where addressed                                                                                                   |
| --------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| FR-AI-001                                           | §3, §4 — full response shape                                                                                      |
| FR-AI-002                                           | §4.3, §4.5 (`place_name`, `destination_name`), §5 step 3 — 0% invented-place/destination enforcement              |
| FR-TRIP-002 (budget-first)                          | §3, §4.1, §4.2, §5 step 4 — now covered by this single contract instead of a separate one                         |
| FR-COST-001                                         | §5 step 5 (fully deterministic backend computation — no model input at all, unlike v1.0.0's coherence-check step) |
| Architecture ADR-01                                 | §1 (ownership split)                                                                                              |
| Architecture §9 (AI Generation Lifecycle)           | §5 (validation pipeline mirrors the sequence diagram)                                                             |
| Database Design §6.3 (`Destination`)                | §4.2                                                                                                              |
| Database Design §6.5 (`Place`)                      | §4.3, §4.5                                                                                                        |
| Database Design §6.13 (`ItineraryItem`)             | §4.5                                                                                                              |
| Database Design §6.15 (`AIGeneration`)              | §5 step 6                                                                                                         |
| Database Design §11 (Itinerary Data Model)          | §2.6                                                                                                              |
| Database Design §15 (Normalization/denormalization) | §5 step 5 (`estimated_cost` snapshot behavior)                                                                    |
