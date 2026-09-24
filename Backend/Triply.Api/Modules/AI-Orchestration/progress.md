# AI-Orchestration Progress

**Owner:** Leen Sharbati · **Track:** Backend · **Status:** Done (live-verified) · **Updated:** 24 Sep 2026

## Tasks

### TASK45 — Gemini HTTP call
- [x] Gemini client and configuration
- [x] Prompt builder and response parsing
- [x] `responseJsonSchema` support, schema v2.0.0 kept in `AI-Schemas/`
- [x] Tested with the real Gemini API (live on Render, 24 Sep 2026)

### TASK46 — AI validation
- [x] Place exists and is active
- [x] Destination grounding (`Destination.IsSupported` required) and category checks
- [x] Name-based grounding rules (v2.0.0)
- [x] Budget-first destination scoping
- [x] Validated with a real Gemini response (live run passed validation on the first attempt)

### TASK47 — Generation endpoint
- [x] `POST /api/trips/{id}/generate` with bounded retries
- [x] Name → place ID resolution and itinerary saving
- [x] `DESTINATION_FIRST` and `BUDGET_FIRST` prompts
- [x] Real end-to-end generation (`200`, `attemptsUsed: 1`, real dataset places)

### TASK48 — Costs
- [x] Costs from saved itinerary values; accommodation = price × nights
- [x] `CostEstimate` rows saved; trip total updated
- [x] Covered by integration tests
- [ ] Confirm the saved cost rows after a live run (response was only partly visible)

### TASK49 — Grounding and schema hardening
- [x] `IsSupported` required for grounding
- [x] `maxItems` per mode: 1 (destination-first), 3 (budget-first)
- [x] Duplicate active place name fails closed
- [x] Unknown JSON fields rejected (`UnmappedMemberHandling.Disallow`)
- [x] HTTP tests: invented place, unsupported destination, valid destination, nothing saved on failure
- [x] Python ↔ C# shared fixtures (12 cases) and a schema-drift test
- [x] Budget-tier data indexed by both `source_place_id` and `place_name`

### TASK51 — Partial regeneration
- [x] `DAY` and `ITEM` scopes, `expectedVersion` required, `409` on stale version

## Budget policy

| Mode | Rule |
|---|---|
| `BUDGET_FIRST` | The AI may return up to 3 options. Each is costed by backend code. Options within budget are kept, the first one is saved onto the trip. If none fits → `422 ALL_OPTIONS_OVER_BUDGET`. No destination needs to be chosen first. |
| `DESTINATION_FIRST` | The plan is always saved. If it is over budget the response has `isOverBudget: true`. |

## Current status

| Item | Status |
|---|---|
| Build | Green (last checked 23 Sep 2026) |
| Tests | 119/119 on 23 Sep; now 121 Fact + 2 Theory, needs a fresh run |
| Real Gemini call | **Passed on Render, 24 Sep 2026** |
| Schema | v2.0.0 |
| Python ↔ C# parity | Enforced by shared fixtures |

## Next
1. Run one live **budget-first** generation and check the destination saved on the trip.
2. Check the `CostEstimate` rows and trip total after a live run.
3. Keep `Extra_AI_Context.csv` place names identical to `Place.name`.
4. Re-check the dataset-provisioning test budget as the dataset grows.

## Run the tests locally

The suite needs SQL Server. Use the container from `docker-compose.yml`:

```bash
export TRIPLY_TEST_DB_CONNECTION="Server=localhost,1433;Database=TriplyDb;User Id=sa;Password=<DB_SA_PASSWORD>;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet test Triply.Api.Tests/Triply.Api.Tests.csproj
```

Each test factory creates its own throw-away `TriplyDb_*` database. Without this variable the tests fall back to LocalDB and every test fails on connection.
`AI:ExtraAiContextPath` lets tests use a temporary CSV for budget-first cases.

## Lesson from a bad merge

`main` once failed to compile because a merge dropped `});` and the rate limiter's `opt.Window`. After the build was fixed, `Window` defaulted to zero and every rate-limited route (even `/api/auth/register`) returned `500`. That single error made ~107 tests look broken. Fixed, and `Window` is now always set.
