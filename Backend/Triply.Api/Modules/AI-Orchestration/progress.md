# AI-Orchestration Progress

**Owner:** Leen Sharbati  
**Track:** Backend  
**Status:** In Progress

## TASK45 — Gemini HTTP Call
- [x] Gemini client and configuration
- [x] Prompt builder and response parsing
- [x] `responseJsonSchema` support
- [x] v2.0.0 schema added as a source-controlled backend artifact
- [ ] Test with real Gemini API

## TASK46 — AI Validation
- [x] Place existence and active-status validation
- [x] Destination grounding and category validation
- [x] v2.0.0 name-based grounding rules
- [x] Budget-first destination scoping validation
- [ ] Test validation with a real Gemini response

## TASK47 — AI Generation
- [x] AI orchestration and generation endpoint
- [x] Bounded retry handling
- [x] Name-to-ID resolution and itinerary persistence
- [x] `DESTINATION_FIRST` and `BUDGET_FIRST` prompt selection
- [x] Gemini generation now uses `GenerateJsonWithSchemaAsync`
- [ ] Test real Gemini generation end-to-end

## TASK48 — Cost Aggregation
- [x] Calculate costs from persisted itinerary values
- [x] Accommodation cost uses `reference_price × nights`
- [x] Persist `CostEstimate` rows
- [x] Update Trip total estimated cost
- [ ] Verify end-to-end AI generation → cost calculation

## Budget-first dataset dependency
- [x] Backend loader added for `Extra_AI_Context.csv`
- [x] Supports lookup by `place_id`/`id` or `place_name`/`name`
- [x] Populate `budget_tier` data in `Extra_AI_Context.csv` (file now has 56 rows with a `budget_tier` column)
- [ ] Run BUDGET_FIRST generation after dataset is populated

> Note: `Extra_AI_Context.csv` labels its id column `source_place_id`. That column
> is now recognised, and every row is indexed under both its id and its
> `place_name`, so `AiOrchestrationService` (which tries `Place.id` then
> `Place.name`) resolves either way. If a place ends up with no tier,
> BUDGET_FIRST still fails loudly rather than guessing. Verified against the
> current dataset: all 57 rows match both `Place.id` and `Place.name`.

## TASK49 — Grounding & Schema Hardening (2026-09-23)
- [x] Destination grounding now requires `Destination.IsSupported`, not just existence
- [x] Per-mode `destination_options.maxItems` (`ItineraryGenerationSchema.LoadForMode`): 1 for `DESTINATION_FIRST`, 3 for `BUDGET_FIRST`
- [x] Duplicate active `Place.name` inside a destination fails closed with a clear error instead of throwing
- [x] DTO binding uses `UnmappedMemberHandling.Disallow` as an in-process stand-in for `additionalProperties: false`
- [x] HTTP integration tests for the grounding guarantee (invented place, unsupported destination, valid destination, non-persistence)
- [x] Python ↔ C# parity fixtures (`AI/03-Validation/fixtures/`) — 12 cases, including 3 Step 1 structural cases
- [x] Budget-tier keying indexes every row by BOTH `source_place_id` and `place_name`, so id or name lookup resolves
- [x] Schema-file drift guard (Python `validate_shared_fixtures.py` + `SchemaFileParityTests.cs`)
- [x] Python harness Step 1 structural checks + duplicate-name fail-closed (parity with this module)
- [x] `BUDGET_FIRST` minimum-surviving-options policy decided (rules-as-written) and implemented
- [x] `DESTINATION_FIRST` over-budget now flagged via `isOverBudget` instead of failing
- [x] `BUDGET_FIRST` may generate with no destination selected, so 1–3 candidate destinations are reachable
- [x] `BUDGET_FIRST` HTTP coverage (all-over-budget 422; within-budget success; multi-option selection)

## Budget policy — decided 2026-09-23 (rules-as-written)

Open issue #1 was decided in favour of the validation rules
(`AI/03-Validation/AI_OUTPUT_VALIDATION_RULES.md` §5.3), and the Backend now
implements them literally (`SelectFirstOptionWithinBudgetAsync` +
`isOverBudget`):

- `BUDGET_FIRST` — every returned option is costed deterministically; the ones
  within budget are kept and the attempt fails only when **none** survive
  (`ALL_OPTIONS_OVER_BUDGET`). The first surviving option is persisted, and
  `Trip.DestinationId` is released on failure so the user can pick another
  suggestion.
- `DESTINATION_FIRST` — the plan is generated and persisted even when over
  budget; the condition is returned as the additive `isOverBudget` field on the
  generate response instead of a failure. The final persisted-cost guard is now
  `BUDGET_FIRST`-only.

Two constraints surfaced while implementing this, and both were then resolved
(see rules §8.6):

1. ~~**`BUDGET_FIRST` can currently return only one usable option.**~~
   **Resolved (2026-09-23).** `BUDGET_FIRST` no longer requires a pre-selected
   destination: the prompt offers every supported destination, the model may return
   up to three distinct candidate options, the ones within budget are kept, and the
   winning option's destination is persisted onto the trip (changeable later via
   `SelectDestination`). With a destination already set the prompt stays scoped to
   it, so the previous flow is unchanged.
2. ~~**`BUDGET_FIRST` needs the real curated dataset to test.**~~ **Resolved:**
   `ExtraAiContextReader` honours `AI:ExtraAiContextPath`, so `AiGroundingTestFactory`
   points it at a temporary CSV covering its synthetic places. `BUDGET_FIRST` now has
   HTTP coverage: `ALL_OPTIONS_OVER_BUDGET` (422) and a within-budget success.

## Current Status
- Build: **GREEN — verified 2026-09-23** (`dotnet build`: 0 errors, 9 pre-existing warnings)
- Backend test suite: **119/119 passing**, stable across three consecutive runs (42–44s each)
- Real Gemini test: **PENDING** (requires a valid Gemini API key)
- Schema alignment: **DONE — v2.0.0**
- Backend JSON-schema generation path: **DONE**
- Per-mode `maxItems`: **DONE**
- Destination `IsSupported` grounding: **DONE**
- Budget policy (§5.3, approved: rules as written): **DONE — HTTP-verified**
- Budget-tier integration: **DONE** — synthetic-place BUDGET_FIRST now exercised over HTTP via a temporary `Extra_AI_Context.csv`
- Python ↔ C# parity: **FIXTURE-ENFORCED** (12 shared cases with per-case failure reasons, passing in both)

## Running the Backend suite locally

The whole suite is integration-level and needs SQL Server. The quickest path is the project's own
compose container (`Backend/docker-compose.yml`), already published on `localhost:1433`:

```bash
# Backend/.env holds DB_SA_PASSWORD
export TRIPLY_TEST_DB_CONNECTION="Server=localhost,1433;Database=TriplyDb;User Id=sa;\
Password=<DB_SA_PASSWORD>;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet test Triply.Api.Tests/Triply.Api.Tests.csproj
```

Factories read `TRIPLY_TEST_DB_CONNECTION` and replace only the database name, so each factory
gets its own throwaway `TriplyDb_*` database. Without that variable the suite falls back to
`(localdb)\mssqllocaldb` and every test fails on connection, which is what made the suite look
broken earlier. `AI:ExtraAiContextPath` is the second seam: `AiGroundingIntegrationTests` points
the tier reader at a temporary CSV so BUDGET_FIRST can run with synthetic places.

### Two merge artifacts found while fixing the build

`main` did not compile, so the suite had never actually run. Fixing the build exposed a second,
runtime-only defect that the build could not catch:

1. `Program.cs` — the PR #75 merge dropped the `});` closing the `AddFixedWindowLimiter("fixed")`
   lambda (leaving the rest of the rate-limiter block nested inside it) plus that lambda's
   `opt.Window` assignment. A leftover `AddPolicy("fixed", ...)` also referenced `generalPermitLimit`,
   which is declared nowhere in the repo. Restored the `Window` line and dropped the duplicate
   registration.
2. **`Window` is not optional.** `FixedWindowRateLimiterOptions.Window` defaults to
   `TimeSpan.Zero`, so once it compiled, *every* rate-limited route — `/api/auth/register` included —
   threw `ArgumentException: Window must be set to a value greater than TimeSpan.Zero` and returned
   500. That single 500 cascaded: registration returned no token, so every authenticated test then
   saw 401, which is why ~107 tests failed for what looked like unrelated reasons.

## Next
1. Run a real Gemini generation using the v2.0.0 JSON schema.
2. Verify the persisted itinerary, CostEstimate rows, and Trip total.
3. Confirm `Extra_AI_Context.csv` place names match `Place.name` exactly for every BUDGET_FIRST destination.
4. Confirm the `DatasetProvisioningTests` suggestion budget still holds as the curated dataset grows
   (it is now derived from the data rather than hardcoded to 500 USD).
