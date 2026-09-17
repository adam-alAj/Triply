# Interest-Aware Destination Suggestions — Backend Coordination Item

**Track:** AI/ML
**Prepared by:** Aya Maali
**Date:** 2026-09-17
**Status:** Proposal — pending Backend review & sign-off
**Requirement reference:** Project Master Report §8.4 (AI track: "تصميم منطق اقتراح الوجهات في حالة ميزانية بدون وجهة بناءً على مطابقة الميزانية والاهتمامات")
**Related files:**
- `Backend/Triply.Api/Modules/Destination/DestinationSuggestionService.cs`
- `Backend/Triply.Api/Modules/Destination/Dtos/DestinationSuggestionDtos.cs`
- `Backend/Triply.Api/Modules/Destination/Validators/DestinationSuggestionValidator.cs`
- `AI/01-Dataset/curated-data/Extra_AI_Context.csv`

Follows the same coordination pattern as `AI/01-Dataset/REVIEW_RECORD.md` §6.

---

## 1. Problem Statement

The current budget-first suggestion endpoint (`POST /api/destinations/suggestions`) accepts `InterestCategoryIds` and **requires** at least one (`DestinationSuggestionRequestValidator`), but the service implementation **never uses it**:

```csharp
// DestinationSuggestionService.SuggestAsync — current logic
var candidates = await _db.Places
    .Where(p => p.IsActive && p.CurrencyId == request.BudgetCurrencyId)
    .GroupBy(...)
    .Select(...)
    .Where(x => x.EstimatedCost <= request.BudgetAmount)
    .OrderBy(x => x.EstimatedCost)
    .ToListAsync();
```

Root cause: **the DB schema has no relationship between `InterestCategory` and `Destination`/`Place`.** The only existing link is `TripInterest` (Trip ↔ InterestCategory), which captures what the *user* selected for a saved trip — not what a *place* offers. This matches the note already on record in `REVIEW_RECORD.md` §3: *"Interest tags are advisory prompt context only — the approved schema has no Place-to-Interest relationship."*

Result: suggestions today are budget-only, contradicting the SRS/Master-Report requirement of budget **+ interest** matching.

---

## 2. Proposed DB Change

Add a many-to-many table: **`PlaceInterest`**

| Column               | Type                                | Notes |
| -------------------- | ----------------------------------- | ----- |
| `PlaceId`            | `bigint` FK → `Places.Id`           |       |
| `InterestCategoryId` | `bigint` FK → `InterestCategory.Id` |       |
| Composite PK         | (`PlaceId`, `InterestCategoryId`)   |       |

**Why `Place`-level and not `Destination`-level:**
- The curated dataset already tags interests at the place level (`Extra_AI_Context.csv → interest_tag`), so no new data collection is needed — only migration + seeding.
- Place-level is more precise and lets us compute a destination's interest coverage by aggregation, instead of hand-picking tags per destination.
- It stays consistent with how `Place` already anchors cost data (`CostCategory`, `Currency`) per §6.5 of the Database Design doc.

---

## 3. Data Readiness (already done on the AI side)

`Extra_AI_Context.csv` (57 rows) already has an `interest_tag` column. Cross-checked against `InterestCategory.csv`:

| interest_tag value      | Count | Maps to InterestCategory.Code |
| ----------------------- | ----- | ----------------------------- |
| History                 | 12    | HISTORY                       |
| Food                    | 9     | FOOD                          |
| Culture                 | 8     | CULTURE                       |
| Shopping                | 6     | SHOPPING                      |
| Nature                  | 5     | NATURE                        |
| Relaxation              | 3     | RELAXATION                    |
| Adventure               | 3     | ADVENTURE                     |
| Other                   | 2     | OTHER                         |
| *(blank — intentional)* | 9     | **N/A — see below**           |

All 8 non-blank values map 1:1 to existing `InterestCategory` codes — no new reference values needed.

**Design decision on the 9 blank rows:** all 9 are `PlaceCategory = TRANSPORT` (airport transfers, single-ride/weekly transit passes, day-trip passes) across the three destinations. A transport/logistics line item has no traveler "interest" in the same sense an attraction, restaurant, activity, or hotel does — nobody picks a destination because of its metro ticket. These are **intentionally left untagged**, not a data gap. Each row's `notes` field now documents this explicitly (`"interest_tag intentionally blank — TRANSPORT/logistics place, not interest-relevant by design (excluded from PlaceInterest scope)"`).

**Consequence for §2:** the `PlaceInterest` seeding step should only cover `PlaceCategory IN (ATTRACTION, RESTAURANT, ACTIVITY, ACCOMMODATION)` — 48 of 57 places. `TRANSPORT` places stay out of `PlaceInterest` by design but remain fully priced and available to the (unrelated, already-working) budget/cost-aggregation logic.

---

## 4. Proposed Matching Logic

Given `BudgetAmount`, `BudgetCurrencyId`, `InterestCategoryIds[]`:

1. **Budget filter (unchanged):** keep current aggregate-cost-per-destination logic — no change needed here.
2. **Interest score per destination:**
   `matchCount = COUNT(DISTINCT PlaceInterest.InterestCategoryId)` among that destination's places, intersected with the requested `InterestCategoryIds`.
3. **Ranking:** order by `matchCount DESC`, then `EstimatedCost ASC` (current tie-breaker).
4. **Inclusion rule (needs team decision — see §5):** should a destination with **zero** interest overlap still appear (budget-only, ranked last), or be excluded entirely?

This keeps the existing budget computation intact and only adds a scoring/ordering layer — low risk to what's already shipped and tested.

---

## 5. Open Questions for Backend / Team Decision

1. Confirm `PlaceInterest` (place-level) vs. a simpler `DestinationInterest` (destination-level, less precise, no migration of existing 57-row tagging needed) — AI recommends place-level per §2.
2. Should a destination with 0 matched interests be **excluded** or **included but ranked last**? (Affects whether "no suggestions" is possible when interests are too narrow.)
3. Minimum overlap threshold, if any (e.g., require ≥1 match vs. require all requested interests present)?
4. Which sprint/track owns the EF Core migration — Backend, or AI track submits the migration for Backend review (AI has already verified compatibility with `seed/seed_places.py`'s natural-key approach)?

---

## 6. What AI Track Delivers Once Approved

- Completed `interest_tag` for the 9 currently-blank places.
- A `PlaceInterest.csv` (or equivalent) seed file, versioned the same way as the rest of `curated-data/` (manifest entry, SHA-256).
- An update to `seed_places.py` (or a new small seeder) to load `PlaceInterest` rows, following the same additive/idempotent pattern already verified for `Place`/`Destination`.

---

## 7. Sign-off

| Reviewer   | Role         | Status |
| ---------- | ------------ | ------ |
| Aya Mali   | AI track     | ☐      |
| Leen Azzam | Backend lead | ☐      |