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
- `AI/01-Dataset/curated-data/PlaceInterest_seed_draft.csv`
- `AI/01-Dataset/seed/Seed_place_interests.py`

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

Root cause: **the DB schema has no relationship between `InterestCategory` and `Destination`/`Place`.** The only existing link is `TripInterest` (Trip ↔ InterestCategory), which captures what the *user* selected for a saved trip — not what a *place* offers. This matched the note on record in `REVIEW_RECORD.md` §3 when this item was written (since replaced by a pointer to `PlaceInterest`): *"Interest tags are advisory prompt context only — the approved schema has no Place-to-Interest relationship."*

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
- The curated dataset already tags interests at the place level (`PlaceInterest_seed_draft.csv`), so no new data collection is needed — only migration + seeding.
- Place-level is more precise and lets us compute a destination's interest coverage by aggregation, instead of hand-picking tags per destination.
- It stays consistent with how `Place` already anchors cost data (`CostCategory`, `Currency`) per §6.5 of the Database Design doc.

---

## 3. Data Readiness (already done on the AI side)

`PlaceInterest_seed_draft.csv` (57 rows — one interest per place, keyed by `destination_name` + `place_name`) already provides the place-level tags. Cross-checked against `InterestCategory.csv`:

| interest_category_code | Count | Notes                           |
| ---------------------- | ----- | ------------------------------- |
| HISTORY                | 12    |                                 |
| FOOD                   | 9     |                                 |
| CULTURE                | 8     |                                 |
| SHOPPING               | 6     |                                 |
| NATURE                 | 5     |                                 |
| RELAXATION             | 3     |                                 |
| ADVENTURE              | 3     |                                 |
| OTHER                  | 11    | 9 `TRANSPORT` places + 2 hotels |

All 8 codes map 1:1 to existing `InterestCategory` codes — no new reference values needed. Each of the 57 rows resolves to exactly one `Place` by natural key (destination name + place name), and there are no duplicate rows.

**Design decision on the 9 `TRANSPORT` places:** airport transfers, single-ride/weekly transit passes and day-trip passes are linked to `OTHER`, so every place has exactly one `PlaceInterest` link and seeding covers all 57 places. They remain fully priced and available to the budget/cost-aggregation logic. Side effect: every destination has 3 `TRANSPORT` places, so a request for `OTHER` matches all three destinations; matching on the other 7 interests is unaffected.

**Consequence for §2:** the `PlaceInterest` seeding step covers all 57 places (`seed/Seed_place_interests.py`: additive, idempotent, natural-key matching).

> Note: `Extra_AI_Context.csv` no longer carries interest data (its `interest_tag` column is reserved and blank). `PlaceInterest_seed_draft.csv` is the only source for Place↔Interest links.

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

- [x] Place-level interest tags for all 57 places, including the 9 `TRANSPORT` places (linked to `OTHER`): `AI/01-Dataset/curated-data/PlaceInterest_seed_draft.csv`.
- [ ] Version the seed file like the rest of `curated-data/` (manifest entry, SHA-256): add `PlaceInterest_seed_draft.csv` to `DATASET_FILES` in `seed/generate_manifest.py` and regenerate the manifest.
- [x] A small dedicated seeder, `seed/Seed_place_interests.py`, loads the `PlaceInterests` rows (additive/idempotent, natural-key matching, `--dry-run`), following the same pattern already verified for `Place`/`Destination`.

---

## 7. Sign-off

| Reviewer   | Role         | Status |
| ---------- | ------------ | ------ |
| Aya Mali   | AI track     | ☐      |
| Leen Azzam | Backend lead | ☐      |