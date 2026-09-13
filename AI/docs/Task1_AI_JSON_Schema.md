# TRIPLY — AI JSON SCHEMA CONTRACT (Gemini Itinerary Output)

**Document status:** DRAFT — pending sign-off
**Schema version:** `1.0.0`
**Applies to requirement:** FR-AI-001 (itinerary generation), FR-AI-002 (output validation)
**References:** Architecture ADR-01 (§4), Architecture §9 (AI Generation Lifecycle), Database Design §6.15 (`AIGeneration`), §6.13 (`ItineraryItem`), §11 (Itinerary Data Model)
**Owners:** AI track (Aya Maali, Anas Musleh, Adam Alafandi) — schema content · Backend (Lynn Sharbati) — integration/enforcement
**Sign-off required from:** AI lead + Backend lead (see §7)
---

This document defines the **exact structured JSON shape** the Gemini API must return for a day-by-day itinerary generation request, and the validation rules Backend applies to that output before any data is written to the database or shown to a user.

This is a **contract**, not an implementation detail. Per ADR-01, the AI track owns this schema and the validation-rule *definitions*; Backend owns the live Gemini call and *enforces* the rules in code. Schema drift between what AI prompts for and what Backend expects is the named risk this document exists to prevent — any change to this schema requires a version bump and re-agreement from both leads (§7, §8).

This schema governs the **itinerary generation** response (FR-AI-001). It does **not** cover the budget-first destination-suggestion response (FR-TRIP-002), which is a separate, smaller contract — see §9 (Open Items).

---

## 2. Design Principles

1. **The AI never outputs a database ID.** Gemini has no knowledge of internal `Place.id` values. The model outputs a **place name** (and destination context); Backend resolves that name to a `Place.id` via exact/fuzzy lookup scoped to the trip's `destination_id`. This is the FR-AI-002 enforcement point — see §5.
2. **No invented fields.** The AI must not return confidence scores, model metadata, or explanatory prose outside the defined fields (matches Design doc Principle 04 — "AI should feel helpful, not magical").
3. **Cost is a number, not a formatted string.** No currency symbols, no ranges ("$50-70") — a single `estimated_cost` decimal per item, plus a `cost_summary` block for category totals. Currency is stated once, not repeated per item, to avoid drift.
4. **Everything maps 1:1 to an existing table.** `days[]` → `ItineraryDay`, `days[].items[]` → `ItineraryItem`, `cost_summary` → `CostEstimate` rows. Nothing in this schema requires a new table.
5. **Fail closed.** If the response doesn't validate against this schema, or any `place_name` doesn't resolve to an active `Place` row in the trip's destination, the entire generation attempt is rejected (`AIGeneration.status = FAILED_VALIDATION`) — never partially written, never fabricated (FR-AI-002, SRS journey 6).

---

## 3. Top-Level Response Shape

```json
{
  "schema_version": "1.0.0",
  "trip_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "destination_id": 42,
  "days": [
    {
      "day_number": 1,
      "date": "2026-11-03",
      "items": [
        {
          "time_slot": "MORNING",
          "order_index": 1,
          "place_name": "Petra Visitor Center",
          "place_category": "ATTRACTION",
          "estimated_cost": 50.00,
          "notes": "Start early to avoid midday heat; allow 3-4 hours."
        },
        {
          "time_slot": "AFTERNOON",
          "order_index": 1,
          "place_name": "Petra Kitchen",
          "place_category": "RESTAURANT",
          "estimated_cost": 25.00,
          "notes": null
        }
      ]
    }
  ],
  "cost_summary": {
    "currency": "USD",
    "accommodation": 300.00,
    "transportation": 120.00,
    "food": 180.00,
    "activities": 150.00,
    "other": 20.00,
    "total": 770.00
  }
}
```

---

## 4. Field-Level Definitions

### 4.1 Root object

| Field | Type | Required | Notes |
|---|---|---|---|
| `schema_version` | string (semver) | Yes | Must match the version Backend is currently configured to accept. Mismatches are rejected before any other validation runs. |
| `trip_id` | string (GUID) | Yes | Echoed back from the request; Backend cross-checks it matches the request context (defense against a swapped/cached response). |
| `destination_id` | integer | Yes | Must match `Trip.destination_id` for the request. If the trip was budget-first, this is the destination the user already confirmed *before* itinerary generation ran — itinerary generation always happens after a destination is fixed. |
| `days` | array of `ItineraryDay` | Yes, min 1 | Length must equal the trip's day count (`end_date - start_date + 1`). A mismatch is a validation failure, not a silent truncation/padding. |
| `cost_summary` | `CostSummary` | Yes | Category totals; see §4.4. |

### 4.2 `ItineraryDay`

| Field | Type | Required | Notes |
|---|---|---|---|
| `day_number` | integer | Yes | 1-indexed, CHECK > 0 (matches `ItineraryDay.day_number`). Must be unique within `days[]` and contiguous (1, 2, 3…) — no gaps. |
| `date` | string (`YYYY-MM-DD`) | Yes | Must fall within `[Trip.start_date, Trip.end_date]` and be consistent with `day_number` (`date = start_date + day_number - 1`). |
| `items` | array of `ItineraryItem` | Yes, min 1 | At least one item per day; empty days are a validation failure, not a valid "rest day" (if rest days become a real requirement, this is a schema v2 change, not a workaround). |

### 4.3 `ItineraryItem`

| Field | Type | Required | Notes |
|---|---|---|---|
| `time_slot` | enum: `MORNING`, `AFTERNOON`, `EVENING` | Yes | Matches `ItineraryItem.time_slot` CHECK constraint exactly (case-sensitive, uppercase). |
| `order_index` | integer | Yes | Ordering within the time slot, starting at 1. Must be unique per `(day_number, time_slot)` pair. |
| `place_name` | string | Yes | **The FR-AI-002 enforcement field.** Must exactly match a `Place.name` value for an `is_active = true` place whose `destination_id` equals the response's `destination_id`. No partial/fuzzy match is accepted at the schema level — matching strategy is a Backend implementation detail, but the *contract* is exact-string-against-dataset. |
| `place_category` | enum: `ATTRACTION`, `RESTAURANT`, `ACTIVITY`, `ACCOMMODATION`, `TRANSPORT` | Yes | Must match `PlaceCategory.code` for the resolved `Place` row. Included so Backend can cheaply reject an obviously wrong category *before* doing the name lookup, and so a name collision across categories can't silently resolve to the wrong place. |
| `estimated_cost` | number (decimal, 2dp) | Yes | Must be `>= 0`. Checked against `Place.reference_price` for the resolved place within the agreed cost-tolerance band (SRS D1, proposed ±15% — see Technical Notes below). Out-of-band values fail validation; they are not silently clamped. |
| `notes` | string or `null` | No | Free-text AI rationale/tip. Never used for anything structural — purely user-facing context. Must not contain invented pricing, confidence language, or claims not derivable from the place/category (Design doc Principle 04). |

### 4.4 `CostSummary`

| Field | Type | Required | Notes |
|---|---|---|---|
| `currency` | string (ISO 4217, 3 chars) | Yes | Must match `Trip.budget_currency_id`'s ISO code if the trip has a budget currency set; otherwise the destination's dominant/default currency. |
| `accommodation`, `transportation`, `food`, `activities`, `other` | number (decimal, 2dp) | Yes | Must each be `>= 0`. These five map 1:1 to `CostCategory.code` (§6.6 of Database Design) — no sixth category may be invented, and none may be omitted (all five keys must be present even if `0.00`). |
| `total` | number (decimal, 2dp) | Yes | Must equal the sum of the five category fields (exact match after rounding to 2dp). A mismatch is a validation failure — Backend does not silently recompute and proceed; it triggers `FAILED_VALIDATION`. |

**Important:** `cost_summary` totals are what the *model* proposed. Per Database Design §12/§14, the **authoritative** `CostEstimate` rows and `Trip.total_estimated_cost` are written by deterministic backend logic that sums the resolved `Place.reference_price` values of the actually-persisted `ItineraryItem` rows — not copied verbatim from `cost_summary`. `cost_summary` is used only as a coherence check (does the model's own math and category sense agree with what it generated) and is not the source of truth. This distinction is deliberate: it keeps "system-generated/deterministic data" (§12 of the Database Design) strictly separate from AI-generated data, per SRS §8.

---

## 5. Validation Pipeline (maps to Architecture §9 sequence diagram)

1. **Schema-shape validation** — JSON parses; required fields present; types/enums correct; `schema_version` matches. Failure → `FAILED_VALIDATION`, no dataset lookups performed.
2. **Structural consistency** — day count matches trip duration; `day_number`/`date` alignment; no gaps or duplicate `(day_number, time_slot, order_index)` tuples; `cost_summary.total` equals the sum of its five categories.
3. **Dataset grounding (FR-AI-002, the non-negotiable rule)** — every `place_name` resolves to exactly one active `Place` row scoped to `destination_id`; the `Place.place_category_id` matches `place_category`. **0% tolerance** — a single unresolved place name fails the *entire* attempt, not just that item.
4. **Cost tolerance check** — each item's `estimated_cost` is compared to the resolved `Place.reference_price` within the agreed band (D1, proposed ±15%, pending sign-off — Database Design §29 DB-D5 / SRS §17 D1).
5. **Persist** — only on full success: `Itinerary`, `ItineraryDay`, `ItineraryItem` rows are written in one transaction, `estimated_cost` is copied from `Place.reference_price` (not from the AI's number) per Database Design §15, and `CostEstimate` rows are computed deterministically from those persisted items.
6. **On any failure** — `AIGeneration.status = FAILED_VALIDATION`, `AIGeneration.validation_errors` records what failed, bounded retry per Architecture §9, and if retries are exhausted the user sees a clear error (SRS journey 6) — never a partial or fabricated itinerary.

---

## 6. Formal JSON Schema

See companion file **`triply-itinerary-generation.schema.json`** (JSON Schema draft 2020-12) for the machine-enforceable version of §3–§4. That file is the artifact Backend actually wires into the AI-Orchestration module's validation step; this document is its human-readable explanation and the record of agreed intent.

---

## 7. Sign-Off

| Role | Name | Status | Date |
|---|---|---|---|
| AI track lead | Aya Maali / Anas Musleh / Adam Alafandi (nominate one signer) | ☐ Pending | — |
| Backend lead | Lynn Sharbati | ☐ Pending | — |

This schema is a **draft** until both boxes above are checked. Per the task's acceptance criteria, sign-off — not just existence of the draft — is what closes this task.

---

## 8. Versioning Policy

- Schema changes that add an optional field, a new enum value, or loosen a constraint → **minor** version bump (`1.0.0` → `1.1.0`).
- Schema changes that remove/rename a field, tighten a constraint, or change a type → **major** version bump (`1.0.0` → `2.0.0`) and require re-sign-off from both leads.
- `AIGeneration.model_provider`/prompt templates must always declare which `schema_version` they target; Backend rejects a response whose `schema_version` doesn't match what it's configured to validate against, rather than attempting best-effort parsing of an unknown version.
- All versions are committed to source control alongside the prompt templates that target them (matches the PR-review-gated workflow in Master Plan §6).

---

## 9. Open Items / Not Yet Covered

| Item | Status |
|---|---|
| Cost tolerance band exact value (±15% proposed) | Blocked on SRS D1 / Database Design DB-D5 sign-off — this schema is written to be compatible with whatever value is confirmed |
| Budget-first destination-suggestion response schema (FR-TRIP-002) | Separate, smaller contract — not covered here; needed before Phase 3 exit alongside this one |
| Partial regeneration request/response shape (single day or single item, FR-TRIP-003) | Out of scope for this draft — this schema covers a full-trip generation; a "regenerate one day" variant should reuse `ItineraryDay`/`ItineraryItem` definitions from §4.2/§4.3 but is a distinct top-level contract, to be drafted alongside Phase 6 work |
| Conversational refinement schema (FR-TRIP-005) | Post-MVP — not drafted |

---

## 10. Traceability

| Requirement/Doc | Where addressed |
|---|---|
| FR-AI-001 | §3, §4 — full response shape |
| FR-AI-002 | §4.3 (`place_name`), §5 step 3 — 0% invented-place enforcement |
| FR-COST-001 | §4.4 `cost_summary`, §5 step 5 (deterministic recomputation) |
| Architecture ADR-01 | §1 (ownership split) |
| Architecture §9 (AI Generation Lifecycle) | §5 (validation pipeline mirrors the sequence diagram) |
| Database Design §6.13 (`ItineraryItem`) | §4.3 |
| Database Design §6.15 (`AIGeneration`) | §5 step 6 |
| Database Design §11 (Itinerary Data Model) | §2.4, §4.4 (why `cost_summary` isn't the source of truth) |
| Database Design §15 (Normalization/denormalization) | §5 step 5 (`estimated_cost` snapshot behavior) |
