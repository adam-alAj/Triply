# AI-Output Validation Rules Specification

**Document status:** v2.0.0 — aligned with AI JSON Schema Contract v2.0.0
**Owner:** AI track (Adam Alafandi) — rule definitions · Backend (Lynn Sharbati) — enforcement
**Date:** 2026-09-18
**Applies to:** FR-AI-002 (output validation), FR-AI-001 (itinerary generation)

---

## 1. Purpose

This document defines the deterministic, codeable validation rules that Backend applies to every Gemini-generated itinerary response before any data is written to the database or shown to a user.

The two critical rules are:

1. **Rule V-001 — 0%-Invented-Place Check**: Reject any itinerary containing a place name that cannot be matched to an authorized place in the internal dataset.
2. **Rule V-002 — Budget Feasibility Check**: Verify that the Backend-computed total cost of each destination option does not exceed the trip's budget (for `BUDGET_FIRST` mode). Note: this is NOT a tolerance check against AI-reported costs — the v2.0.0 contract removes all cost fields from the model output.

These rules enforce **FR-AI-002**:

> "Every generated plan is checked against the internal dataset before being shown to the user. 0% invented-place rate; failed validation triggers regeneration or a user-facing error, never silent fabrication."

---

## 2. Scope

These rules validate:

- **Gemini-generated itinerary output** matching `triply-trip-plan-generation.schema.json` v2.0.0
- **Both planning modes**: `DESTINATION_FIRST` and `BUDGET_FIRST`
- **All place references**: accommodation `place_name`, daily item `place_name`, and `destination_name`

These rules do **not** validate:

- Schema shape (handled by `responseJsonSchema` enforcement + jsonschema validation)
- Authentication, authorization, or API-level concerns
- Dataset freshness or staleness (separate concern per Database Design §28)

---

## 3. Source of Truth

| Document | Section | What it defines |
|----------|---------|-----------------|
| SRS | FR-AI-002 | Core requirement: 0% invented places, validation before display |
| SRS | D1 | **Closed: not applicable / superseded by AI Contract v2.0.0.** No AI cost field exists to compare; backend-computed costs and budget feasibility are validated separately. |
| Database Design | §6.5 (`Place`) | Place entity structure, `reference_price` semantics |
| Database Design | §6.13 (`ItineraryItem`) | `place_id` NOT NULL FK — DB-level enforcement |
| Database Design | §15 | `estimated_cost` copied from `Place.reference_price`, never from model |
| AI Schema Contract v2.0.0 | §2 Design Principles | "AI never outputs a database ID"; "Cost is never proposed by the model" |
| AI Schema Contract v2.0.0 | §4.5 | `place_name` is the FR-AI-002 enforcement field |
| AI Schema Contract v2.0.0 | §5 Steps 1-6 | Validation pipeline definition |
| AI Schema Contract v2.0.0 | §9 Open Items | Cost tolerance is "no longer applicable to this schema" |
| Gemini Prototype | Experiment Report | 10/10 responses were 100% grounded; 0 hallucination rate observed |
| Dataset | `Place.csv` | 57 active places, 3 destinations, `Place.name` as natural key |

---

## 4. Rule V-001 — 0%-Invented-Place Check

### 4.1 Definition

Triply must reject an AI-generated itinerary if **any** place name in the output cannot be resolved to an authorized, active place in the internal dataset. The tolerance is **exactly 0%** — a single invented/unresolvable place causes the entire destination option to fail validation.

### 4.2 Input

- AI-generated itinerary JSON (after JSON Schema validation passes)
- Internal authoritative `Place` table (filtered to `is_active = true`)
- Internal authoritative `Destination` table (filtered to `is_supported = true`)

### 4.3 Authoritative Identity

**Place identity is `Place.name` (string, case-sensitive, exact match).**

Rationale:
- The v2.0.0 contract explicitly states: "The AI never outputs a database ID — of any kind" (Design Principle 1, §2)
- The model outputs `place_name` (string); Backend resolves by exact lookup
- `Place.name` is the natural key used for matching (Database Design Major Query Pattern #5)
- `Place.name` must be unique within a destination (enforced by seeder, pending DB constraint — see Dataset Curation §5, item 4)

The model does **not** output `Place.id`. The `ItineraryItem.place_id` FK is resolved server-side after name lookup and is never part of the AI contract.

### 4.4 Authoritative Dataset Source

```
SOURCE = Place table
FILTER = is_active = true
KEY    = Place.name (exact string match)
SCOPE  = Place.destination_id matches the option's resolved destination
```

The authoritative dataset is the **database at validation time**, not a static CSV file. The CSV files in `AI/01-Dataset/curated-data/` are the curation source; the database is the runtime source of truth.

### 4.5 Extraction

Extract every place reference from the AI-generated itinerary:

```
PLACE_REFERENCES = {
    itinerary.destination_options[*].destination_name,
    itinerary.destination_options[*].accommodation.place_name,
    itinerary.destination_options[*].days[*].items[*].place_name
}
```

Each reference is a string value from the JSON output. Duplicates are counted once for validation purposes (duplicate valid names do not cause failure).

### 4.6 Validation Algorithm

```
For each destination_option in itinerary.destination_options:

    1. RESOLVE destination_name:
       SELECT Destination.id FROM Destination
       WHERE Destination.name = option.destination_name
         AND Destination.is_supported = true
       
       IF no match: FAIL with DESTINATION_NOT_FOUND

    2. COLLECT all place_name values from this option:
       - option.accommodation.place_name
       - option.days[*].items[*].place_name

    3. For each place_name:
       SELECT Place.id, Place.place_category_id FROM Place
       WHERE Place.name = place_name
         AND Place.is_active = true
         AND Place.destination_id = resolved_destination_id
       
      IF no match: FAIL with PLACE_NOT_FOUND
      IF multiple active matches inside the resolved destination:
         FAIL with DUPLICATE_PLACE_NAME (fail closed; do not pick an arbitrary row)
      IF the same active name exists in other destinations:
         resolve the single row scoped to the option's destination

    4. CATEGORY CHECKS (post-resolution):
       a. accommodation.place_name must resolve to a Place where
          PlaceCategory.code = 'ACCOMMODATION'
          IF not: FAIL with WRONG_CATEGORY
       
       b. No daily item's place_name may resolve to a Place where
          PlaceCategory.code = 'ACCOMMODATION'
          IF so: FAIL with ACCOMMODATION_IN_DAYS
       
       c. At least one daily item must resolve to a Place where
          PlaceCategory.code = 'RESTAURANT'
          IF not: FAIL with MISSING_RESTAURANT
       
       d. At least one daily item across ALL days must resolve to a Place where
          PlaceCategory.code = 'TRANSPORT'
          IF not: FAIL with MISSING_TRANSPORT

PASS only if ALL of the above pass for ALL destination_options.
```

### 4.7 Pass Condition

```
invalid_place_count == 0
```

**Every** generated place name must resolve. No partial passes.

### 4.8 Failure Condition

```
invalid_place_count > 0
```

A single unresolvable name causes the destination option to fail:
- `DESTINATION_FIRST`: the entire generation attempt fails → `AIGeneration.status = FAILED_VALIDATION`
- `BUDGET_FIRST`: the failed option is dropped; if zero options survive, the attempt fails

### 4.9 Matching Method

**Exact string match only. No fuzzy matching. No partial matching. No normalization.**

- `Place.name = "Hashem Restaurant"` matches only `"Hashem Restaurant"`
- `"hashem restaurant"` (lowercase) does **not** match
- `"Hashem Rest."` (abbreviation) does **not** match
- `"Hashem Restaurant Amman"` (extra text) does **not** match

Rationale: The prompt templates explicitly instruct Gemini to "copy exactly, character-for-character, from the place list." The prototype experiments (10/10 pass rate) confirm this works. Fuzzy matching would weaken the anti-hallucination guarantee without justification.

### 4.10 Edge Cases

| Case | Behavior | Code |
|------|----------|------|
| `place_name` is empty string | `FAIL` | `PLACE_NAME_EMPTY` |
| `place_name` is null | `FAIL` | `PLACE_NAME_MISSING` |
| `place_name` matches no active Place | `FAIL` | `PLACE_NOT_FOUND` |
| `place_name` matches an inactive Place | `FAIL` | `PLACE_NOT_FOUND` (inactive places are excluded from the authorized set) |
| `place_name` matches a Place in a different destination | `FAIL` | `PLACE_WRONG_DESTINATION` |
| `place_name` matches an ACCOMMODATION place in `days` | `FAIL` | `ACCOMMODATION_IN_DAYS` |
| `accommodation.place_name` matches a non-ACCOMMODATION place | `FAIL` | `WRONG_CATEGORY` |
| Same valid place appears multiple times | `PASS` | — (duplicates are not invented places) |
| `destination_name` matches no supported Destination | `FAIL` | `DESTINATION_NOT_FOUND` |
| `notes` contains invented text about a valid place | `PASS` for V-001 | (notes are user-facing, not validated against dataset) |

### 4.11 Failure Codes

| Code | Meaning | Trigger | Action |
|------|---------|---------|--------|
| `PLACE_NOT_FOUND` | Generated place name does not exist in the active dataset for this destination | `place_name` lookup returns zero rows | Reject destination option |
| `PLACE_NAME_EMPTY` | Generated place name is empty or whitespace | `place_name` is `""` or null | Reject destination option |
| `PLACE_WRONG_DESTINATION` | Place exists but belongs to a different destination | `Place.destination_id != resolved destination id` | Reject destination option |
| `DESTINATION_NOT_FOUND` | Generated destination name does not exist or is not supported | `Destination` lookup returns zero rows | Reject entire attempt |
| `WRONG_CATEGORY` | Accommodation resolves to a non-ACCOMMODATION category | `PlaceCategory.code != 'ACCOMMODATION'` | Reject destination option |
| `ACCOMMODATION_IN_DAYS` | An ACCOMMODATION-category place appears in daily items | Resolved category is ACCOMMODATION | Reject destination option |
| `MISSING_RESTAURANT` | No RESTAURANT-category place in a day's items | Zero RESTAURANT matches for that day | Reject destination option |
| `MISSING_TRANSPORT` | No TRANSPORT-category place across all days | Zero TRANSPORT matches across all days | Reject destination option |

---

## 5. Rule V-002 — Cost/Budget Validation

### 5.1 Critical Design Decision: No AI-Reported Costs

The v2.0.0 contract **removes all cost fields from the model output**:

- `cost_summary` removed from root (§1a #1)
- `estimated_cost` removed from `ItineraryItem` (§1a #2)
- Design Principle 3 (§2): "Cost is never proposed by the model — not a number, not a string, not a summary."

**Therefore, there is no "AI-reported cost" to compare against an "expected cost."** The v1.0.0 ±15% tolerance check (SRS D1) is no longer applicable to the schema validation pipeline.

Per the contract §9 Open Items:

> "Cost tolerance band exact value (±15% proposed, SRS D1 / Database Design DB-D5) — **No longer applicable to this schema** — there is no model-provided cost to check a tolerance against."

### 5.2 What Replaces the Tolerance Check

Instead of comparing AI-reported vs. expected cost, the Backend:

1. **Resolves every place name** to a `Place` row (Rule V-001)
2. **Copies `Place.reference_price`** directly as `ItineraryItem.estimated_cost` (Database Design §15)
3. **Computes `CostEstimate` rows** deterministically from the resolved items (Database Design §15)
4. **Compares the computed total** against `Trip.budget_amount` for budget feasibility

This is **not** a tolerance check — it is a deterministic budget comparison using authoritative internal data only.

### 5.3 Budget Feasibility Check (V-002)

For each `destination_option` that passes V-001:

```
EXPECTED_TOTAL =
    (accommodation.nights × accommodation_place.reference_price)
    + SUM(item_place.reference_price for each daily item)

WHERE:
    accommodation_place = resolved Place for accommodation.place_name
    item_place = resolved Place for each day's item.place_name
```

#### For DESTINATION_FIRST mode:

```
IF EXPECTED_TOTAL > Trip.budget_amount:
    FLAG as over_budget (log it, but do NOT automatically fail the attempt)
    The plan is shown with its real computed total.
    (Final UX behavior TBD per contract §9 — "budget overrun does not
     automatically fail the attempt")
```

#### For BUDGET_FIRST mode:

```
IF EXPECTED_TOTAL > Trip.budget_amount:
    DROP this destination option from the response

IF number of surviving options == 0:
    FAIL entire attempt with ALL_OPTIONS_OVER_BUDGET
    AIGeneration.status = FAILED_VALIDATION
```

### 5.4 Pricing Semantics

From the `Place.csv` dataset and Database Design §6.5:

| Field | Type | Meaning |
|-------|------|---------|
| `reference_price` | `DECIMAL(10,2)` | Canonical reference price per unit |
| `currency_id` | FK → Currency | Currency of the reference price |
| `cost_category_id` | FK → CostCategory | Cost bucket (ACCOMMODATION, TRANSPORTATION, FOOD, ACTIVITIES, OTHER) |

**Price unit conventions:**

| Category | Unit | Example |
|----------|------|---------|
| ACCOMMODATION | Per night | "Jordan Tower Hotel" = 25 JOD/night |
| RESTAURANT | Per visit/person | "Hashem Restaurant" = 4 JOD/visit |
| ATTRACTION | Per visit/person | "Eiffel Tower" = 23.50 EUR/visit |
| ACTIVITY | Per visit/person | "Wadi Rum Jeep Safari" = 35 JOD/activity |
| TRANSPORT | Per trip/pass | "Airport Transfer" = 22 JOD/trip |

**Free places** (`reference_price = 0`): Notre-Dame Cathedral, Central Park, etc. These contribute 0 to the total cost. Zero is a valid price, not a missing price.

### 5.5 Currency Handling

- Each `Place` has its own `currency_id` (EUR for Paris, JOD for Amman, USD for New York)
- Budget comparison is per-destination within the same currency context
- **No cross-currency conversion** is performed at the validation layer
- The `Trip.budget_currency` should match the destination's currency for a meaningful comparison
- If currencies don't match, the budget check result should be flagged but not auto-fail (this is an architectural decision for Backend)

### 5.6 Edge Cases

| Case | Behavior |
|------|----------|
| `reference_price` is 0 (free place) | Contributes 0 to total. PASS. |
| `reference_price` is NULL | Should not occur (DB NOT NULL constraint on applied schema). If it does, reject with `PRICE_DATA_MISSING`. |
| `accommodation.nights` = 0 | Schema rejects this (minimum: 1). |
| `EXPECTED_TOTAL` = 0 (all free places) | PASS. Zero is a valid total. |
| `EXPECTED_TOTAL` is negative | Should not occur. If it does, reject with `INVALID_COST`. |
| `Trip.budget_amount` is NULL | Skip budget check (no budget constraint). |
| `Trip.budget_amount` = 0 | All non-free options are over budget. |

### 5.7 Failure Codes

| Code | Meaning | Trigger | Action |
|------|---------|---------|--------|
| `ALL_OPTIONS_OVER_BUDGET` | No destination option fits within budget (BUDGET_FIRST only) | All options dropped after budget check | Reject entire attempt |
| `PRICE_DATA_MISSING` | Resolved Place has NULL reference_price | Data integrity issue | Reject destination option |
| `INVALID_COST` | Computed total is negative or otherwise invalid | Arithmetic error | Reject destination option |

---

## 6. Validation Order

The validation pipeline executes in this exact order. Each step depends on the previous step succeeding:

```
Step 0: JSON Schema Validation
    ↓ (PASS)
Step 1: Structural Consistency
    ↓ (PASS)
Step 2: Dataset Grounding — V-001 (0%-Invented-Place Check)
    ↓ (PASS)
Step 3: Category Rules (part of V-001)
    ↓ (PASS)
Step 4: Budget Feasibility — V-002
    ↓ (PASS)
Step 5: Persist (only for user-selected option)
```

### Step 0 — JSON Schema Validation

Handled at the Gemini API level by `responseJsonSchema` enforcement, plus
independent in-process checks on the Backend. The Backend does **not** run a
JSON-Schema engine: the `System.Text.Json` response DTOs are bound with
`UnmappedMemberHandling.Disallow` (so an unexpected field is rejected rather than
silently dropped, matching the schema's `additionalProperties: false`), and
`ItineraryValidationService` then applies the semantic checks (required fields,
enums, `days.length`, numeric ranges). The Python harness (`harness.py`) is the
only implementation that validates against the raw JSON Schema with `jsonschema`.

Catches: missing fields, wrong types, invalid enums, unexpected fields, structural issues.

**Does NOT catch:** invented place names, wrong categories, budget violations.

> **Per-mode `destination_options.maxItems` (contract §5 step 0):** applied by
> `ItineraryGenerationSchema.LoadForMode` immediately before the Gemini call —
> `1` for `DESTINATION_FIRST`, `3` for `BUDGET_FIRST`. The committed artifact
> stays mode-agnostic; the override happens on the in-memory copy, so the
> schema file on disk is never mutated. There is no longer any universal value.

### Step 1 — Structural Consistency

- `days.length` == trip duration (`end_date - start_date + 1`)
- `day_number` is 1-indexed and contiguous (no gaps)
- `date` = `start_date + day_number - 1`
- No duplicate `(day_number, time_slot, order_index)` tuples
- `accommodation.nights` >= 1
- `order_index` >= 1
- `time_slot` in {MORNING, AFTERNOON, EVENING}

### Step 2 — Dataset Grounding (V-001, primary)

- `destination_name` resolves to an active, supported `Destination`
- Every `place_name` resolves to an active `Place` scoped to the resolved destination
- **0% tolerance**: any single failure rejects the option

### Step 3 — Category Rules (V-001, secondary)

- Accommodation `place_name` resolves to ACCOMMODATION category
- No daily item resolves to ACCOMMODATION category
- At least one daily item per day resolves to RESTAURANT category
- At least one daily item across all days resolves to TRANSPORT category

### Step 4 — Budget Feasibility (V-002)

- Compute total from `Place.reference_price` (deterministic, no model input)
- `DESTINATION_FIRST`: flag over-budget, don't auto-fail
- `BUDGET_FIRST`: drop over-budget options; fail if zero survive

### Step 5 — Persist

- Only for the user-selected option
- `estimated_cost` = `Place.reference_price` (copied, never from model)
- `CostEstimate` rows computed deterministically

**Critical dependency:** Cost validation (Step 4) cannot be completed if a referenced place cannot be resolved (Step 2). If V-001 fails, V-002 is not executed for that option.

---

## 7. Validation Result Contract

The validation layer returns a deterministic, machine-readable result:

```json
{
  "is_valid": true,
  "schema_validation": {
    "passed": true,
    "errors": []
  },
  "structural_validation": {
    "passed": true,
    "errors": []
  },
  "place_grounding": {
    "passed": true,
    "generated_place_count": 12,
    "resolved_place_count": 12,
    "invalid_place_count": 0,
    "invalid_places": [],
    "failure_codes": []
  },
  "category_rules": {
    "passed": true,
    "errors": []
  },
  "budget_feasibility": {
    "checked": true,
    "passed": true,
    "expected_total": 156.00,
    "budget_amount": 400.00,
    "currency": "JOD",
    "over_budget": false
  }
}
```

### Distinguishing Failure Types

| Failure Type | `is_valid` | When |
|--------------|-----------|------|
| Schema failure | `false` | Step 0 fails (JSON parse, missing fields, wrong types) |
| Structural failure | `false` | Step 1 fails (day count, date alignment, duplicates) |
| Invented-place failure | `false` | Step 2 fails (any place_name unresolvable) |
| Category failure | `false` | Step 3 fails (wrong category, missing restaurant/transport) |
| Budget failure (BUDGET_FIRST) | depends | Step 4: if zero options survive → `false`; if some survive → `true` with dropped options |
| Budget flag (DESTINATION_FIRST) | `true` | Step 4: over-budget flagged but not failed |

---

## 8. Backend Implementation Notes

### 8.1 Database Query for Place Resolution

```sql
SELECT p.id, p.name, p.place_category_id, p.reference_price,
       pc.code AS category_code
FROM Place p
JOIN PlaceCategory pc ON p.place_category_id = pc.id
WHERE p.name IN ({place_names})
  AND p.is_active = 1
  AND p.destination_id = {resolved_destination_id}
```

### 8.2 Database Query for Destination Resolution

```sql
SELECT id, name FROM Destination
WHERE name = {destination_name}
  AND is_supported = 1
```

### 8.3 Cost Computation

```csharp
decimal ComputeOptionCost(GeminiDestinationOptionDto option, Dictionary<string, Place> placeLookup)
{
    var accPlace = placeLookup[option.Accommodation.PlaceName];
    decimal total = option.Accommodation.Nights * accPlace.ReferencePrice;

    foreach (var day in option.Days)
        foreach (var item in day.Items)
            total += placeLookup[item.PlaceName].ReferencePrice;

    return total;
}
```

### 8.4 Validation Entry Point

```
IItineraryValidator.ValidateAsync(trip, output, cancellationToken)
→ ItineraryValidationService.cs
→ Returns ItineraryValidationResult { IsValid, Errors }
```

### 8.5 What the Existing Backend Already Implements

The Backend `ItineraryValidationService.cs` + `AiOrchestrationService.cs` implement:
- ✅ Schema-shape validation (DTO binding + `UnmappedMemberHandling.Disallow`; no JSON-Schema engine — see Step 0)
- ✅ Structural consistency (day count, date alignment, contiguous day numbers, duplicate slot+order)
- ✅ Dataset grounding (name-based, destination-scoped lookup, 0% tolerance)
- ✅ Destination support — a destination must resolve to a real `Destination` **and** satisfy `IsSupported`
- ✅ Category rules (ACCOMMODATION in accommodation only, RESTAURANT per day, TRANSPORT per option)
- ✅ Duplicate `Place.name` inside a destination — fails closed with a clear error (open issue #4)
- ✅ Per-mode `destination_options.maxItems` applied before the Gemini call
- ✅ Budget feasibility comparison (V-002 Step 4) — deterministic guards, in budget currency, both before and after persistence
- ✅ Cost copy from resolved `Place.reference_price` into `CostEstimate` rows (Cost module)

### 8.6 Budget Policy — Resolved (rules-as-written)

Open issue #1 was decided on 2026-09-23 in favour of **these rules**, and the
Backend now implements §5.3 literally:

- `BUDGET_FIRST` — the model's up-to-three options are each costed
  deterministically; the ones within budget are kept and the attempt fails only
  when **none** survive (`ALL_OPTIONS_OVER_BUDGET`). The first surviving option is
  persisted, and the trip's destination is released on failure so the user can
  pick another suggestion.
- `DESTINATION_FIRST` — a single option is generated and persisted even when it
  exceeds the budget. The over-budget condition is returned as an additive
  `isOverBudget` flag on the generate response rather than as a failure, because
  the user explicitly chose the destination.

Two constraints surfaced while implementing this and are **not** resolved here:

1. ~~**`BUDGET_FIRST` could not return more than one usable option.**~~
   **Resolved (2026-09-23) by a product decision.** `BUDGET_FIRST` may now generate
   with **no destination selected yet**, which is what makes the 1–3 option rule
   real: the prompt offers every supported destination, the model may return up to
   three distinct candidate options, the ones within budget are kept, and the
   winning option's destination is persisted onto the trip so the user can keep or
   change it through the `SelectDestination` endpoint. When a destination *is*
   already set, the prompt stays scoped to it and the previous behaviour is
   unchanged.
2. ~~**`BUDGET_FIRST` cannot be exercised with synthetic test places.**~~
   **Resolved:** generation still requires a `budget_tier` for every active place,
   but `ExtraAiContextReader` honours the `AI:ExtraAiContextPath` setting, so a test
   host can point it at a temporary CSV covering its synthetic places instead of the
   curated file. `AiGroundingTestFactory` does this, and the HTTP tests now cover
   both `ALL_OPTIONS_OVER_BUDGET` (422) and a within-budget `BUDGET_FIRST` success.

---

## 9. Traceability Matrix

| FR-AI-002 Requirement | Validation Rule | Implementation Mechanism | Status |
|-----------------------|-----------------|--------------------------|--------|
| 0% invented-place rate | V-001 | Authoritative `Place.name` exact-match lookup against active, destination-scoped dataset | ✅ Implemented in `ItineraryValidationService.cs` |
| No silent fabrication | V-001 + V-002 | Fail-closed: any validation failure → `AIGeneration.status = FAILED_VALIDATION`, bounded retry, never partial write | ✅ Implemented in `AiOrchestrationService.cs` |
| Failed validation triggers regeneration | V-001 + V-002 | `MaxRetries` bounded retry loop; if all retries fail → user-facing error (SRS journey 6) | ✅ Implemented in `AiOrchestrationService.cs` |
| Cost correctness | V-002 (§5) | Deterministic computation from `Place.reference_price`, never from model output | ✅ Implemented — price copy + budget comparison in budget currency; policy threshold open (#1) |
| Cost tolerance (D1, ±15%) | **N/A in v2.0.0** | No AI-reported cost exists to check tolerance against. Contract §9: "No longer applicable to this schema." | ✅ Design decision: removed from schema |
| Database-level guarantee | `ItineraryItem.place_id` NOT NULL FK | EF Core + SQL Server constraint: item cannot be saved without a real Place | ✅ Implemented in migration + DbContext |

---

## 10. Examples

### Example 1 — Valid Itinerary (PASS)

**Input:** Gemini returns Amman 3-day itinerary

```json
{
  "planning_mode": "DESTINATION_FIRST",
  "destination_options": [{
    "destination_name": "Amman",
    "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
    "days": [
      {"day_number": 1, "date": "2026-11-10", "items": [
        {"time_slot": "MORNING", "order_index": 1, "place_name": "Airport Transfer – QAIA to Amman"},
        {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Roman Theatre & Museum of Popular Traditions"},
        {"time_slot": "EVENING", "order_index": 1, "place_name": "Hashem Restaurant"}
      ]},
      {"day_number": 2, "date": "2026-11-11", "items": [...]},
      {"day_number": 3, "date": "2026-11-12", "items": [...]}
    ]
  }]
}
```

**Validation:**
- `Amman` → resolves to `Destination.id = 2` ✅
- `Jordan Tower Hotel` → resolves to `Place.id = 20`, category ACCOMMODATION ✅
- `Airport Transfer – QAIA to Amman` → resolves to `Place.id = 36`, category TRANSPORT ✅
- `Roman Theatre & Museum of Popular Traditions` → resolves to `Place.id = 27`, category ATTRACTION ✅
- `Hashem Restaurant` → resolves to `Place.id = 23`, category RESTAURANT ✅
- All names are exact character-for-character matches ✅
- Day count = 3, matches trip duration ✅

**Result: PASS**

### Example 2 — Invented Place Name (FAIL)

**Input:** Gemini returns `"place_name": "Amman Citadel Museum"` (slightly modified name)

**Dataset contains:** `"Amman Citadel & Jordan Archaeological Museum"` (note the `&` and longer name)

**Validation:**
- `Amman Citadel Museum` → lookup returns 0 rows → `PLACE_NOT_FOUND`

**Result: FAIL**
- Failure code: `PLACE_NOT_FOUND`
- Invented-place count: 1 / 12 total places = 8.3%
- Required: 0%
- Action: Reject destination option → `FAILED_VALIDATION`

### Example 3 — Accommodation in Days (FAIL)

**Input:** Gemini puts the hotel in daily items instead of the accommodation object

```json
{
  "days": [{
    "day_number": 1,
    "items": [
      {"time_slot": "MORNING", "order_index": 1, "place_name": "Jordan Tower Hotel"},
      ...
    ]
  }]
}
```

**Validation:**
- `Jordan Tower Hotel` resolves to category ACCOMMODATION
- ACCOMMODATION found in daily items → `ACCOMMODATION_IN_DAYS`

**Result: FAIL**

### Example 4 — Budget Over (DESTINATION_FIRST, FLAG only)

**Input:** 3-day Amman trip, total computed cost = 450 JOD, budget = 400 JOD

**Validation:**
- V-001: All places resolved ✅
- V-002: `EXPECTED_TOTAL (450) > Trip.budget_amount (400)` → over budget

**Result: PASS with over-budget flag** (DESTINATION_FIRST mode does not auto-fail on budget overrun per contract §5 step 4)

### Example 5 — All Options Over Budget (BUDGET_FIRST, FAIL)

**Input:** 3 destination options returned, all computed totals exceed budget

**Validation:**
- V-001: All places resolved ✅
- V-002: All 3 options dropped (over budget)
- Surviving options: 0

**Result: FAIL** with code `ALL_OPTIONS_OVER_BUDGET`

---

## 11. Gemini Prototype Findings (Incorporated)

From the prototype experiments (10/10 pass rate):

1. **Zero hallucination rate observed** — all 10 generations used only supplied dataset place names
2. **`responseJsonSchema` enforcement is highly effective** — structural compliance was 100%
3. **Prompt instructions for exact name matching work** — "copy exactly, character-for-character" produced correct results
4. **No price hallucination observed** — model respected the "never output prices" instruction
5. **Category placement was correct** — accommodation always in the accommodation object, never in days

**Implication for rules:** V-001 is a safety net, not an expected failure path. The combination of `responseJsonSchema` + prompt instructions makes hallucination unlikely, but V-001 must remain as the non-negotiable guarantee per FR-AI-002.

---

## 12. Open Issues

| # | Issue | Status | Impact |
|---|-------|--------|--------|
| 1 | Budget-first minimum surviving options (how many of 3 must fit?) | **Resolved (2026-09-23): ≥ 1.** `BUDGET_FIRST` keeps every option that fits and fails only when none survive; `DESTINATION_FIRST` flags an over-budget plan instead of failing. `BUDGET_FIRST` may now generate without a pre-selected destination, so multiple candidate options are reachable (§8.6) | No longer blocks V-002 |
| 2 | Cross-currency budget comparison (Trip budget in USD, destination in JOD) | Open | Affects V-002 when currencies don't match |
| 3 | Cost tolerance D1 (±15%) | **Resolved: N/A in v2.0.0** — no AI-reported cost exists | No impact on validation |
| 4 | `Place.name` uniqueness within destination (pending DB constraint) | **Mitigated** — both the C# validator and the Python harness now group by name and fail closed instead of throwing/ambiguously resolving (see §13) | Could cause ambiguous resolution if violated |
| 5 | Accommodation slot placement in `days[]` for persistence | Open (contract §9, minor) | Backend implementation detail |

---

## 13. Python ↔ C# Parity

`harness.py` (Python, validation-time) and `ItineraryValidationService.cs` (C#,
runtime) are two independent implementations of V-001/V-002. They are now checked
against **identical inputs** via shared fixtures:

- `AI/03-Validation/fixtures/v001-v002-parity-fixtures.json` — 12 cases (9 rules + 3 Step 1 structural)
- `AI/03-Validation/validate_shared_fixtures.py` — Python runner (also enforces the schema-file guard below)
- `Backend/Triply.Api.Tests/ValidationParityFixtureTests.cs` — C# runner (reads the same file)

`expected.v001Passed` means *"the option is valid"*: it covers Step 1 structural
consistency **and** the V-001 grounding rules, matching C#'s
`ItineraryValidationResult.IsValid` and the Python `OptionValidationResult.passed`.
`expected.v002Passed` covers the budget outcome, and is `true` whenever V-001
already failed (V-002 is not executed — spec §6).

The Python runner passes 12/12 fixtures. The C# runner drives the **real**
`ItineraryValidationService` over the same file, using the fixture file's
`tripStartDate`/`tripEndDate` so both sides apply Step 1 to the same trip window.
It must be executed on a machine with the .NET SDK (`dotnet test`); it has **not**
been run as part of this revision, because no .NET SDK was available in that
environment.

Documented rule differences:

| Area | Python harness | C# validator |
|------|----------------|--------------|
| Destination support | Rejects names missing from the `is_supported == "True"` set (`DESTINATION_NOT_FOUND`) | Rejects names that don't resolve to an `IsSupported` `Destination` |
| Place lookup | Groups by name; a duplicate active name **inside one destination** fails closed (`DUPLICATE_PLACE_NAME`). The same name in several destinations is legitimate and resolved by the option's destination. | Same rules, with the duplicate reported as a validation error |
| Structural rules | ✅ Day count, contiguous 1-indexed `day_number`, date alignment, non-empty days/items, valid `time_slot`, `order_index` ≥ 1, duplicate `(time_slot, order_index)` | Same rules |
| Structural inputs | Day count and date alignment use `trip_start_date`/`trip_end_date` from the fixture file; when omitted only itinerary-internal checks run | Derives them from the trip's `StartDate`/`EndDate` |
| Duplicate destination options | `destination_options` must have distinct `destination_name` values | Same |
| Budget scope | Every option; `BUDGET_FIRST` fails only if **zero** options survive; `DESTINATION_FIRST` flags only | ✅ Same policy (rules-as-written, §8.6): `BUDGET_FIRST` costs every option and fails only when none survive; `DESTINATION_FIRST` returns `isOverBudget` instead of failing |
| Failure reason | Fixtures may declare expected `codes`; the runner asserts containment | Fixtures may declare expected `fragments`; the runner asserts containment against the validator's error text |
| Currency | Uses each resolved place's own currency; no conversion | Converts totals into the trip's budget currency before comparing |
| Schema validation | `jsonschema` against the raw schema file | DTO binding + `UnmappedMemberHandling.Disallow` + semantic validator |

Known weakness of this guard: fixtures carry only a boolean expectation, so a
case that expects failure cannot distinguish *failing for the right reason* from
failing for any reason. Asserting the specific failure code (Python) and error
fragment (C#) per case is the recommended next strengthening.

Rules that are identical in both: exact-string (ordinal) name matching, no
case-folding, destination-scoped active-place resolution, empty/whitespace name
rejection, accommodation-only accommodation, restaurant-per-day and
transport-placement rules, and `reference_price × nights` accommodation costing.

**Documentation drift found during this reconciliation:**
- §8.5/§8.6 previously claimed budget feasibility was "not yet implemented"; it is
  implemented. Only the *policy* is open.
- Step 0 previously implied the Backend ran a JSON-Schema engine. It does not.
- The traceability matrix marked cost correctness as partial; it is implemented.
- `AI/docs/SCHEMA_CHANGELOG.md` listed `maxItems` and `PlaceContextDto.BudgetTier`
  as Backend TODOs; both are wired up.
- `harness.validate_generation`'s docstring advertised "Step 1: Structural
  consistency (partial — day count, date alignment)" that did not exist in the
  body. Step 1 is now implemented (see the structural rows above).
- The Python harness silently let the last CSV row win for a duplicated active
  place name; it now fails closed, matching the C# validator.

**Schema file drift (guarded):** the generation schema is committed twice —
`AI/02-Prompt-Engineering/json-schemas/triply-trip-plan-generation.schema.json` and
`Backend/Triply.Api/AI-Schemas/triply-trip-plan-generation.schema.json` — and
nothing links them. They are byte-identical as of this revision, and both sides
now fail loudly if that stops being true:

- Python: `validate_shared_fixtures.py` → "Schema file parity" check
- C#: `Backend/Triply.Api.Tests/SchemaFileParityTests.cs`

A change to only one copy would otherwise silently alter what Gemini is asked to
produce while validation kept checking a contract the model never received.
