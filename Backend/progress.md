# Backend Progress

**Owner:** Leen Sharbati
**Track:** Backend
**Status:** Backend is feature-complete for MVP scope and deployed to Render (staging). AI integration (Gemini) is implemented, grounded, and covered by the deterministic test suite; a fresh live Gemini run has not been re-verified since the latest model-configuration fix (see "Remaining verification" at the bottom).

This file is the top-level status log. For day-to-day detail on the AI/Gemini work specifically, see `Triply.Api/Modules/AI-Orchestration/progress.md` — it is updated more frequently than this file and is the source of truth for anything AI-specific.

---

## Snapshot

```text
Auth + security hardening     DONE
Trip CRUD + lifecycle          DONE
Destination / Currency /
  InterestCategory / Place     DONE  (reference-data read endpoints)
Cost aggregation                DONE
Itinerary read/write/edit       DONE
Optimistic concurrency          DONE
AI integration (Gemini)         DONE  (deterministic tests green; live-key run pending re-verification)
User profile module              DONE
Dev-time dataset provisioning    DONE
Testing (28 files, 121 Fact
  + 2 Theory tests)              DONE, pending a fresh full run
CI (GitHub Actions)              DONE
Deployment (Render)              DONE — live at triply-api-za13.onrender.com
Monitoring (UptimeRobot)         DONE — 2 monitors, both Up, 100% uptime
```

---

## TASK 51 — Partial Regeneration End-to-End

- [x] `POST /api/trips/{tripId}/generate` supports `FULL`, `DAY`, and `ITEM`.
- [x] DAY regeneration replaces only the targeted day's non-accommodation items.
- [x] ITEM regeneration replaces only the targeted activity.
- [x] Partial regeneration validates the generated response against the AI contract and internal dataset.
- [x] `Trip.Version` is used for optimistic concurrency.
- [x] `expectedVersion` is required for DAY/ITEM regeneration.
- [x] Stale versions return `409 Conflict` without replacing itinerary content.
- [x] Successful partial regeneration increments `Trip.Version` once.
- [x] Direct item edits set only the edited item to `isAiGenerated=false`.
- [x] Added integration tests for DAY, ITEM, stale-version conflict, missing-version validation, and direct-edit behavior.
- [x] Updated API contract documentation.

## TASK 53 — Backend Unit/Integration Test Suite

- [x] Existing Auth, Trip, Cost, and backend integration coverage retained.
- [x] Added AI-Orchestration unit tests with xUnit/Moq.
- [x] Added AI partial-regeneration integration tests using a deterministic test Gemini client.
- [x] Added Gemini envelope/error handling unit coverage.
- [x] Added CI workflow that restores, builds, runs the full xUnit suite, and collects coverage against SQL Server.
- [x] Removed the empty placeholder unit test.

---

## Work completed after TASK 51/53 (this section consolidates everything since, in build order)

### AI grounding & schema hardening (TASK45–TASK49 — see AI-Orchestration/progress.md for full detail)
- [x] Gemini client, configuration, prompt builder, and `responseJsonSchema` support (TASK45).
- [x] Place existence/active-status/destination-grounding/category validation (TASK46).
- [x] Full AI generation endpoint with bounded retry and name→ID resolution (TASK47).
- [x] Cost aggregation from persisted AI-generated itinerary values, incl. accommodation × nights (TASK48).
- [x] Schema aligned to v2.0.0; per-mode `maxItems`; `Destination.IsSupported` required for grounding; duplicate active place names fail closed; budget policy decided and implemented (`isOverBudget` for DESTINATION_FIRST, `ALL_OPTIONS_OVER_BUDGET` for BUDGET_FIRST); Python↔C# validation-rule parity fixtures (TASK49).
- [x] `BUDGET_FIRST` no longer requires a pre-selected destination — up to 3 candidate destinations are returned, costed, and the winning one is persisted onto the trip.
- [x] Backend test suite green (119/119 as of 2026-09-23 per AI-Orchestration/progress.md); grown to 121 `[Fact]` + 2 `[Theory]` in the current checkout.

### Security hardening (post-MVP pass, "Security Task 1–3")
- [x] **Security Task 1** — refresh tokens (`RefreshToken` entity, `/api/auth/refresh`, `/api/auth/logout` revocation), email confirmation (`/api/auth/confirm-email`, `/api/auth/resend-confirmation`), password reset (`/api/auth/forgot-password`, `/api/auth/reset-password`) via `IEmailSender` (`LoggingEmailSender` placeholder implementation).
- [x] **Security Task 2** — 1 MB Kestrel max request body; bounded `InterestCategoryIds` list on trip create/update (was previously unbounded — a real DoS-shaped gap).
- [x] **Security Task 3** — rate limiting repartitioned per authenticated user / remote IP instead of one shared counter; dedicated `ai-generation` policy (10/hour/user) separate from the general `fixed` policy (10/min/user in Production, 200/min in Testing to cover the full integration suite).
- [x] Security response headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`) added globally.
- [x] `SecurityFixesTests.cs` / `SecurityHardeningTests.cs` cover all of the above.

### Gap fixes (identified during hardening/review, each with a regression test)
- [x] **Gap 1** — removed the backend's static seed (PR #66); a fresh database now starts empty and the AI track's curated CSVs are provisioned automatically in `Development` only (`SeedData.EnsureCuratedDatasetAsync`) — additive, idempotent, fails fast on a missing/incomplete dataset.
- [x] **Gap 2** — removed an unsafe `POST /api/trips/test-create` endpoint that had no ownership/business-rule protection.
- [x] **Gap 3** — full AI generation now acquires the target `DRAFT` trip through a concurrency guard so two simultaneous "Generate" requests on the same trip can't race.
- [x] **Gap 4** — 30-day retention on `AIGeneration.RawOutput`, enforced by a plain `BackgroundService` (`AiRawOutputRetentionBackgroundService`), configurable via `DataRetention:RawOutputDays`.
- [x] **Gap 6** — a duplicate active `Place.name` within one destination now fails the generation attempt closed with a clear error instead of silently picking one or throwing an unhandled exception.

### New modules added since TASK 51/53
- [x] **User module** (`/api/users/me`, `.../preferences`, `.../stats`) — profile get/update, trip-planning preferences (currency, distance unit, pacing) with sensible first-read defaults, and lifetime stats (trip count, distinct saved places, distinct countries) computed on demand from existing data.
- [x] **Currency module** (`/api/currencies`) — reference list + conversion service, consumed by Cost and AI-Orchestration.
- [x] **Place module** (`/api/places/{id}`) — single curated place lookup for clients rendering itinerary items.
- [x] **InterestCategory module** (`/api/interest-categories`) — reference list used by trip creation.
- [x] **Destination assets endpoint** (`/api/destinations/assets`) added alongside the existing list/suggestions endpoints.

### Data model growth (migrations since the TASK 51/53 baseline)
`SeedReferenceCategoriesViaHasData` → `AddUserEmailUniqueAndDisplayNameMaxLength` → `AddTripSchemaCheckConstraints` → `SeedCountriesCurrenciesDestinations` → `AlignDatasetSchema` → `AddPlaceInterest` → `AddUserPreferencesAndTripMetadata` → `AddExchangeRates` → `RemoveBackendStaticSeed` (Gap 1) → `AddRefreshTokens` (Security Task 1) → `AddAiGenerationSchemaVersion`. New entities: `RefreshToken`, `UserPreferences`, `ExchangeRate`, `PlaceInterest`, plus the reference tables (`ReferenceEntities.cs`) originally scoped in the Database Design doc.

### Monitoring & deployment (TASK 64 + Render)
- [x] **TASK 64 — Monitoring**: staging JSON console logging (`appsettings.Staging.json`), HTTP request/response telemetry enabled only in `Staging`, EF Core command-level SQL logging at `Information` (no sensitive parameter values), unauthenticated `GET /health` for external monitors. Full checklist and rationale in `docs/Monitoring.md`.
- [x] Backend deployed to **Render** as a Docker web service (`triply-api`), currently live at `https://triply-api-za13.onrender.com`, deployed from `fix/gemini-content-length`.
- [x] **UptimeRobot** configured with two HTTP(s) monitors against `/health` (5-minute interval); both currently report **Up**, 100% uptime over the last 24 hours — the acceptance checklist in `docs/Monitoring.md` is satisfied against the live deployment.
- [x] `docker-compose.yml` simplified to API + SQL Server only (no Redis — it was never actually wired into the app despite an earlier planning mention; the retention job is a plain `BackgroundService` instead).

---
## Historical implementation notes (kept for context)

- Partial DAY/ITEM regeneration uses atomic `Trip.Version` compare-and-swap; the tracked Trip is not written a second time by `SaveChanges`.
- Stale-version integration coverage returns `409` without replacing itinerary content.
- A bad merge on `main` (fixed during the security/gap pass) had dropped the closing brace of the rate limiter's fixed-window lambda and left a duplicate, undefined-variable `AddPolicy("fixed", …)` registration; the same merge had also dropped the required `opt.Window` assignment, which meant every rate-limited route — including `/api/auth/register` — threw at runtime once the build was fixed. This cascaded into ~107 unrelated-looking test failures (registration returned no token → every authenticated test saw `401`). Root-caused and fixed; see `Triply.Api/Modules/AI-Orchestration/progress.md` for the full writeup.
