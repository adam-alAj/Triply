# Backend Progress

**Owner:** Leen Sharbati · **Track:** Backend
**Last updated:** 24 Sep 2026

## Summary

The backend is **feature-complete for the MVP, deployed on Render, and verified with a real Gemini call.**

```text
Foundation (project, Docker, DB, migrations, Swagger)   DONE
Auth + security hardening                                DONE
Trips (create, edit, lifecycle, ownership, versioning)   DONE
Reference data (destinations, currencies, interests,
  places) + budget-first suggestions                     DONE
Cost calculation                                         DONE
Itinerary (read, write, edit one item)                   DONE
AI generation (Gemini) FULL / DAY / ITEM                 DONE  (live-verified on Render)
User profile (profile, preferences, stats)               DONE
Tests (28 files, 121 Fact + 2 Theory)                    DONE  (re-run once to confirm)
CI (GitHub Actions)                                      DONE
Deployment (Render)                                      DONE
Monitoring (UptimeRobot, 2 monitors, 100% up)            DONE
Documentation (README, API contract, conventions)        DONE
```

---

## What was built, in order

### 1. Foundation
- [x] ASP.NET Core project, SQL Server + EF Core, migrations
- [x] Docker Compose (API + SQL Server), `.env` for secrets
- [x] Swagger, `GET /health`, centralized error handling (`ProblemDetails`)
- [x] API conventions agreed with Flutter (`docs/API_CONVENTIONS.md`)

### 2. Authentication and security
- [x] Register, login, JWT (15 min), refresh tokens (30 days, rotated), logout
- [x] Email confirmation and password reset endpoints
- [x] Rate limits: login 5/min, general 10/min, AI generation 10/hour (per user or IP)
- [x] 1 MB request limit, bounded interest lists, security headers, CORS
- [x] Ownership policy: a user can only reach their own trips (`404` otherwise)

### 3. Trips
- [x] Create (destination-first / budget-first), list, get, update, title/cover edit, change destination
- [x] Lifecycle: save, archive, restore (no free "set status" endpoint)
- [x] Optimistic concurrency with `version` → `409` on stale edits

### 4. Reference data and suggestions
- [x] `GET /destinations`, `/destinations/assets`, `/currencies`, `/interest-categories`, `/places/{id}`
- [x] `POST /destinations/suggestions` (budget + interests → up to 3 destinations)

### 5. Itinerary and costs
- [x] Read, atomic write, and edit of a single item (`isAiGenerated` becomes `false`)
- [x] Cost per category by backend code; accommodation = price × nights; always `isEstimated: true`

### 6. AI generation (Gemini)
- [x] Gemini client, prompt builder, JSON schema v2.0.0
- [x] Validation: schema, place exists, active, supported destination, no duplicate names
- [x] Bounded retries; clear errors (`422`, `502`); nothing saved on failure
- [x] Partial regeneration: `DAY` and `ITEM`
- [x] Budget policy: budget-first up to 3 options / destination-first `isOverBudget`
- [x] Protection against two simultaneous "Generate" taps on the same trip
- [x] Raw Gemini output cleared after 30 days
- [x] **Live test on Render passed (24 Sep 2026):** `POST /generate` → `200`, `attemptsUsed: 1`, `isOverBudget: false`, real places from the dataset (Amman hotel, airport transfer, Citadel, restaurant)

### 7. User profile
- [x] `GET/PATCH /users/me`, `GET/PUT /users/me/preferences`, `GET /users/me/stats`

### 8. Testing and CI
- [x] xUnit + `WebApplicationFactory`; fake Gemini client in tests
- [x] GitHub Actions: build + run tests on every push

### 9. Deployment and monitoring
- [x] Render (Docker, free plan): https://triply-api-za13.onrender.com
- [x] UptimeRobot: 2 monitors on `/health`, every 5 min, both Up
- [x] Staging JSON logs, request logs, SQL command logs

---

## Still open (small)

- [ ] Run one **budget-first** generation on the live service and confirm the saved destination and cost rows.
- [ ] Re-run the full test suite once and record the new count (last full run: 119/119 on 23 Sep).
- [ ] Connect a real email provider (today `LoggingEmailSender` only logs emails).
- [ ] Regenerate `docs/openapi.yaml` from Swagger (the current file is an old Sprint 0 draft).
- [ ] Consider a higher general rate limit (10 requests/min per user may be tight for the mobile app).
- [ ] Free Render plan sleeps when idle (~50 s first request). Upgrade the plan before a live demo if needed.

---

## Migrations (12)

`InitialCreate` → `SeedReferenceCategoriesViaHasData` → `AddUserEmailUniqueAndDisplayNameMaxLength` → `AddTripSchemaCheckConstraints` → `SeedCountriesCurrenciesDestinations` → `AlignDatasetSchema` → `AddPlaceInterest` → `AddUserPreferencesAndTripMetadata` → `AddExchangeRates` → `RemoveBackendStaticSeed` → `AddRefreshTokens` → `AddAiGenerationSchemaVersion`

## Problems found and fixed along the way

| Problem | Fix |
|---|---|
| A bad merge on `main` dropped a closing `});` and the rate limiter's `Window` value. Once it compiled, every rate-limited route returned `500`, which made about 107 tests fail. | Restored the code; `Window` is now always set. |
| Unsafe `POST /api/trips/test-create` endpoint with no ownership check | Removed |
| Two "Generate" taps at once could race | Concurrency guard on the trip |
| Unbounded `InterestCategoryIds` list | Limited to 50 items |
| Two active places with the same name in one destination | Generation fails closed with a clear error |
| Static seed data inside the backend | Removed; the AI track's CSVs are the single source |
| Redis mentioned in old plans but never used | Removed from docs and Compose |
| Gemini call failing on Render (IPv6 / model config) | Force IPv4 connection and fix the model setting |
