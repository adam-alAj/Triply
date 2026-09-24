# Triply Backend

Triply is an AI-assisted trip-planning platform (mobile + web) that turns a user's destination or budget input into a personalized, day-by-day itinerary with a category-level cost breakdown.

This document tells the backend story **in the order it was actually built** — foundation → security → domain logic → lifecycle → concurrency → AI integration → hardening → deployment. It is written to be walked through top-to-bottom in a mentor review.

For exact request/response payloads, see [API Contract](docs/API%20Contract.md).
For API design rules (naming, DTOs, error format, versioning), see [API Conventions](docs/API_CONVENTIONS_.md).

> **Status: the backend is live.** Every stage below is implemented, not planned — including the Gemini integration, which was the last open item in the previous version of this document. The service is deployed to Render and monitored by UptimeRobot (§15–16).

---

## 1. Project Goal

```text
User preferences
      ↓
Create trip
      ↓
Destination-first / Budget-first planning
      ↓
AI itinerary generation
      ↓
Validation against internal dataset
      ↓
Cost estimation
      ↓
User modification
      ↓
Save trip
      ↓
Retrieve / archive / restore
```

The backend is designed so that AI-generated content is **never accepted blindly**: every place referenced by a generated itinerary must exist in the internal, curated dataset before it is persisted. That guarantee is enforced at the database level (mandatory foreign key), not just in application code.

---

## 2. Design Principle Behind Every Decision

Every technical choice below — the modular monolith, the single Flutter codebase, who calls Gemini, which database, which testing tools — was made to match **demonstrated team capability**, not popularity. The same rule applies inside the backend itself: no feature was built beyond what the approved SRS/Architecture/Database Design documents require. If something wasn't in the approved schema, it was **not invented**.

---

## 3. Technology Stack & Module Structure

- ASP.NET Core 9 / C# / .NET 9 (SDK pinned via `global.json`, `9.0.316`)
- Entity Framework Core + SQL Server
- ASP.NET Core Identity + JWT Bearer Authentication + refresh tokens
- FluentValidation
- Gemini API (`generativelanguage.googleapis.com`) — the only external service the backend calls
- Swagger / OpenAPI
- Docker / Docker Compose (local dev) · Docker on Render (staging)
- xUnit / Moq / `WebApplicationFactory` integration tests
- GitHub Actions CI

> Redis was evaluated in earlier planning docs but was never wired in — the 30-day AI-output retention job runs as a plain `BackgroundService` (see §11) and nothing else in the backend currently needs a cache. If a future performance pass reintroduces Redis, this line should be updated together with it.

```text
Triply.Api/
├── Common/
│   ├── Authorization/        # ownership ("TripOwner") policy
│   └── Middleware/           # centralized exception handling
├── Data/                     # DbContext, dev/test dataset seeding
├── Entities/                 # EF Core entities
├── Migrations/                # 12 migrations, InitialCreate → AddAiGenerationSchemaVersion
├── AI-Schemas/                # versioned Gemini responseJsonSchema (v2.0.0)
└── Modules/
    ├── Auth/                 # register, login, refresh, logout, email confirm, password reset
    ├── User/                 # profile, preferences, stats (Flutter "Profile" screen)
    ├── Trip/                 # CRUD, lifecycle, ownership, optimistic concurrency
    ├── Destination/          # supported-destination list + budget-first suggestions
    ├── Place/                # single place lookup
    ├── Currency/             # currency list + conversion
    ├── InterestCategory/     # interest reference list
    ├── Cost/                 # deterministic cost aggregation
    ├── Itinerary/            # day/item persistence, direct item edits
    └── AI-Orchestration/     # Gemini call, prompt building, schema + dataset validation
```

| Module | Responsibility |
|---|---|
| Auth | Registration, login, JWT + refresh tokens, logout/revocation, email confirmation, password reset |
| User | Own profile (get/update), trip-planning preferences (currency, distance unit, pacing), lifetime stats |
| Trip | Creation, retrieval, listing, updates, destination selection, lifecycle, ownership, concurrency |
| Destination | Supported-destination reference list + budget-first destination suggestions |
| Place | Single curated place lookup (used by clients rendering itinerary items) |
| Currency | Reference currency list + conversion helper used by Cost/AI modules |
| InterestCategory | Reference interest list (nature, history, food, …) used by Trip creation |
| Cost | Deterministic cost aggregation per trip, grouped by category |
| Itinerary | Day/item persistence, atomic full-write, direct per-item edits |
| AI-Orchestration | Owns the live Gemini call, prompt construction, JSON-schema + dataset validation, full and partial regeneration |

---

## 4. Stage 1 — Backend Foundation

Before writing any feature, the project foundation was set up so it could actually run and be tested end to end:

- ASP.NET Core project setup, SQL Server + EF Core, migrations
- Docker Compose (API + SQL Server)
- Environment configuration via `.env` / `.env.example` — **no secrets in source control**
- Swagger, health endpoint (`GET /health`), centralized exception-handling middleware
- API conventions agreed up front: `/api` base path, DTOs (never raw EF entities), `camelCase` JSON, `ProblemDetails` error format, UTC timestamps, `yyyy-MM-dd` dates

This foundation is the single source of truth for how Flutter and the backend communicate — see [`API_CONVENTIONS.md`](./API_CONVENTIONS%20.md).

---

## 5. Stage 2 — Authentication & Security

```text
POST /api/auth/register           → returns a JWT + refresh token immediately
POST /api/auth/login
POST /api/auth/refresh            → exchange a refresh token for a new JWT
POST /api/auth/logout             → revokes the refresh token
GET  /api/auth/confirm-email
POST /api/auth/resend-confirmation
POST /api/auth/forgot-password
POST /api/auth/reset-password
```

**Tested cases:**

| Case | Result |
|---|---|
| Successful registration | `200` |
| Successful login | `200` |
| Wrong password | `401` (generic message — doesn't reveal which field was wrong) |
| Invalid / non-existing email | `401` |
| Duplicate email on register | `400` |
| Invalid registration data | `400` |
| Excessive login attempts | `429` (5 failed attempts / minute) |
| Forgot-password / resend-confirmation for a non-existent account | `200` with a generic "if that account exists…" message (never reveals whether the account exists) |

JWT config: **60-minute access-token lifetime**, issuer `Triply`, audience `TriplyClients`. Refresh tokens are persisted (`RefreshToken` entity) so a session can be renewed without re-entering credentials, and can be individually revoked on logout.

**Evidence:**
| Register success | Login success | Wrong password (401) |
|---|---|---|
| ![Register](docs/image.png) | ![Login](docs/image-1.png) | ![Wrong password](docs/image-2.png) |

| Rate limiting (429) | Duplicate email (400) | Invalid email (401) |
|---|---|---|
| ![Rate limit](docs/image-3.png) | ![Duplicate](docs/image-4.png) | ![Invalid email](docs/image-5.png) |

### Security hardening (post-MVP pass)

Three follow-up security tasks were applied on top of the original auth/trip work:

- **Security Task 1** — refresh tokens + revocation, email confirmation, and password reset, all built on `ASP.NET Core Identity`'s token providers. Email delivery goes through an `IEmailSender` abstraction; `LoggingEmailSender` (logs instead of sending) is the current implementation, so a real provider can be swapped in without touching controller code.
- **Security Task 2** — request-size limit (`Kestrel` max body `1 MB`, comfortably above every real payload) and a bound on client-supplied `InterestCategoryIds` lists, closing an unbounded-array-input gap on trip create/update.
- **Security Task 3** — rate limiting is partitioned **per authenticated user (falling back to remote IP)**, not one shared global counter, so one abusive client can't throttle everyone else. AI generation gets its own, much tighter budget (10 generations/hour/user) because it's the one action that costs real money and time.

Security headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`) are applied to every response.

---

## 6. Stage 3 — Trip Management

Once a user is authenticated, the Trip domain was built:

- Authenticated trip creation, retrieval, and listing of a user's own trips
- Request validation: planning mode, traveler count, dates, budget, destination, interests (bounded list — Security Task 2)
- Destination selection/change on an existing trip (`PATCH /api/trips/{id}/destination`)
- Transaction handling on writes
- **Ownership protection** — a user can never see or modify another user's trip; unauthorized access returns `404`, not `403`, so trip existence isn't leaked

**Evidence:**
| Create trip | Get trip (authenticated) | Other user's trip blocked | Trip update |
|---|---|---|---|
| ![Trip POST](docs/image-9.png) | ![Trip GET](docs/image-7.png) | ![Blocked](docs/image-8.png) | ![Update](docs/image-10.png) |

---

## 7. Stage 4 — Reference Data & Budget-First Destination Suggestions

```text
GET  /api/destinations/assets
GET  /api/destinations
POST /api/destinations/suggestions
GET  /api/currencies
GET  /api/interest-categories
GET  /api/places/{id}
```

Given a budget, currency, and interest categories, `POST /api/destinations/suggestions` aggregates **active `Place` reference prices per destination** (from the real internal dataset) and returns destinations whose estimated aggregate cost fits the budget, sorted ascending by cost.

The other endpoints above are read-only reference lookups (supported destinations, currencies, interest categories, a single place by id) that Flutter uses to populate trip-creation screens and render itinerary items without duplicating dataset values on the client.

Full request/response shape: see [`API Contract.md`](./API%20Contract.md).

---

## 8. Stage 5 — Deterministic Cost Aggregation

```text
GET /api/trips/{tripId}/cost-estimate
```

```text
CostEstimates
      ↓
group by CostCategory (all configured categories included, even at 0.00)
      ↓
category totals
      ↓
grand total
      ↓
persisted to Trip.TotalEstimatedCost
```

A single currency is required before aggregation. Every figure returned is explicitly marked `"isEstimated": true` — never presented as a verified price.

---

## 9. Stage 6 — Itinerary Read/Write & Direct Edits

```text
GET   /api/trips/{tripId}/itinerary
POST  /api/trips/{tripId}/itinerary
PATCH /api/trips/{tripId}/itinerary/items/{itemId}
```

```text
Itinerary
 └── Days (dayNumber, date)
      └── Items (place, timeSlot, orderIndex, estimatedCost, notes, isAiGenerated)
```

Validation: unique/positive day numbers, valid time slots, non-negative cost and order index, place must exist, be active, and (when the trip has a destination) belong to that destination, and no duplicate active place name inside one destination (fails closed rather than silently picking one — see §11). The full write is an **atomic replacement** of the current itinerary; the `PATCH items/{itemId}` endpoint lets a user directly edit one item, which flips only that item's `isAiGenerated` to `false`.

---

## 10. Stage 7 — Trip Save, Retrieve & Lifecycle

```text
DRAFT → GENERATING → GENERATED → MODIFIED ⇄ (further edits) → SAVED → ARCHIVED
                 ↘ (validation failure) → DRAFT (retry)
```

```text
POST /api/trips/{id}/generate       → FULL | DAY | ITEM (see §11)
POST /api/trips/{id}/save
POST /api/trips/{id}/archive
POST /api/trips/{id}/restore
GET  /api/trips/{id}                → trip + itinerary + cost estimate, in one response
```

> **Important point:** there is no generic "set status" endpoint that lets a client jump to any status directly. Status only changes as the *result* of a specific business action (generate, save, archive, restore) — this prevents clients from putting a trip into an invalid state.

---

## 11. Stage 8 — AI Integration (Gemini)

This was the open item in the previous version of this document; it is now fully implemented and tested.

```text
Flutter
   ↓
POST /api/trips/{tripId}/generate  { scope: FULL | DAY | ITEM }
   ↓
Backend loads candidate places/pricing for the trip's destination(s) from the internal dataset
   ↓
Prompt built per mode (DESTINATION_FIRST vs BUDGET_FIRST — different candidate scope)
   ↓
Gemini call, constrained by a versioned responseJsonSchema (currently v2.0.0)
   ↓
Schema validation (unknown/extra fields rejected, not silently dropped)
   ↓
Dataset validation: every place must exist, be active, belong to a *supported* destination
   ↓
On failure → bounded retry, then a clear error — never a fabricated plan
   ↓
On success → itinerary persisted, cost aggregated, Trip.Version incremented, lifecycle updated
```

**Full vs. partial regeneration** (`POST /api/trips/{tripId}/generate`):

- `scope: "FULL"` — generates the whole itinerary for a `DRAFT` trip.
- `scope: "DAY"` — regenerates only the targeted day's non-accommodation items.
- `scope: "ITEM"` — regenerates only the targeted activity.
- `DAY`/`ITEM` require `expectedVersion`; a stale version returns `409 Conflict` without touching the current itinerary (same optimistic-concurrency guarantee as direct trip edits — see §12).

**Budget policy** (decided against `AI_OUTPUT_VALIDATION_RULES.md` §5.3, implemented as written):

- `BUDGET_FIRST` — the model may return up to three candidate destination options; each is costed deterministically, the ones within budget are kept, and the attempt fails only when **none** survive (`ALL_OPTIONS_OVER_BUDGET`). `BUDGET_FIRST` no longer requires a destination to be pre-selected — the winning option's destination is written onto the trip.
- `DESTINATION_FIRST` — the plan is generated and persisted even when it comes out over budget; this is surfaced as an additive `isOverBudget` flag on the response instead of a failure, since the user already committed to that destination.

**Grounding & hardening applied on top of the base integration:**

- Destination grounding requires `Destination.IsSupported`, not just existence in the table.
- Per-mode candidate limits (`ItineraryGenerationSchema.LoadForMode`): 1 destination option for `DESTINATION_FIRST`, up to 3 for `BUDGET_FIRST`.
- A duplicate active place name inside one destination fails the attempt closed with a clear error instead of guessing which one was meant.
- Full generation acquires the trip through a concurrency guard so two simultaneous "Generate" taps on the same `DRAFT` trip can't race each other.
- AI-generated content is always structurally distinguishable from user data (`ItineraryItem.IsAiGenerated`), and raw Gemini responses are retention-limited — see §12.
- Python ↔ C# parity: the AI track's validation-rules fixtures (`AI/03-Validation/fixtures/`) and this module's own tests are checked against the same 12 shared cases, so both sides agree on what "valid" means.

Live Gemini verification (an actual API call, not the deterministic test double used in CI) is the one item still tracked as pending — see `Triply.Api/Modules/AI-Orchestration/progress.md` for the up-to-date status of that.

---

## 12. Stage 9 — Optimistic Concurrency & Data Retention

`Trip.Version` is an EF Core concurrency token. Every update — direct trip edits and `DAY`/`ITEM` regeneration alike — carries an `expectedVersion`:

```json
{ "expectedVersion": 1 }
```

```text
expectedVersion  vs  current Trip.Version
        ↓ mismatch
     409 Conflict   (instead of silently overwriting a newer save)
```

**Integration test scenario:**

```text
Request A: version 1 → success → version becomes 2
Request B: still sends expectedVersion = 1 → 409 Conflict
```

**AI raw-output retention:** `AIGeneration.RawOutput` (the full Gemini response, kept for debugging) is nulled out for attempts older than 30 days by a background service (`AiRawOutputRetentionBackgroundService`) — a plain `BackgroundService`, not a new scheduling dependency. The retention window is configurable (`DataRetention:RawOutputDays`).

---

## 13. Stage 10 — User Profile Module

Added to support the Flutter "Profile" screen:

```text
GET   /api/users/me
PATCH /api/users/me
GET   /api/users/me/preferences   → preferred currency, distance unit (KM/MILES), pacing (RELAXED/BALANCED/FAST)
PUT   /api/users/me/preferences
GET   /api/users/me/stats         → total trips, distinct saved places, distinct countries visited
```

Preferences are created with sensible defaults on first read rather than requiring a separate "initialize" call. Stats are computed on demand from existing Trip/Itinerary data — no new aggregate tables were introduced for numbers that can be derived cheaply.

---

## 14. Testing

Integration tests via `WebApplicationFactory` cover: authentication (including refresh/logout/email/password flows), authorization/ownership, validation, destination suggestions, cost aggregation, itinerary persistence, trip lifecycle, optimistic concurrency, rate limiting, full and partial AI generation (against a deterministic fake Gemini client), dataset provisioning, duplicate-place validation, and AI raw-output retention.

```text
28 test files — 121 [Fact] tests + 2 [Theory] cases in the current checkout
```

The AI-Orchestration module's own progress log (`Triply.Api/Modules/AI-Orchestration/progress.md`) recorded the last verified full run as **119/119 passing** on 2026-09-23 against the project's SQL Server test environment; the suite has grown slightly since (see count above) and should be re-run to confirm before the next milestone.

---

## 15. Docker & Local Development

```text
triply-api        → 8080
triply-sqlserver   → 1433
```

```powershell
docker compose down
docker compose up -d --build
docker compose ps
```

Health: `http://localhost:8080/health` · Swagger: `http://localhost:8080/swagger`

### Reference data on a fresh database (development provisioning)

PR #66 removed the backend's static seed on purpose: a fresh database starts with
**zero** reference rows, and the AI track's versioned CSVs in
`AI/01-Dataset/curated-data/` are the single source of truth. In the
`Development` environment the API now provisions them automatically right after
migrating (`Data/SeedData.cs`):

- additive and idempotent — every row is matched by natural key and only inserted
  when missing; re-running never duplicates rows and never updates or deletes
  existing user/trip/reference data,
- imports the real curated dataset (countries, currencies, categories,
  destinations, places, place↔interest links, placeholder exchange rates),
- needs no Python/pyodbc/ODBC inside the container — the manual
  `AI/01-Dataset/seed/*.py` scripts stay available for offline/ops dataset work
  but are **not** part of the standard setup (the automatic startup path above is),
- dataset path configurable via `AI:CuratedDataPath` (default
  `../../AI/01-Dataset/curated-data` relative to the content root — in Docker the
  compose `../AI:/AI` mount resolves this to `/AI/01-Dataset/curated-data`),
- **fails fast**: a missing dataset directory/file or a required reference
  table that would remain empty aborts startup with a clear error — the
  environment never continues silently unprovisioned,
- **Development only**: `Testing` keeps its test fixtures, and
  Staging/Production are never seeded by the application.

---

## 16. Deployment & Monitoring

The backend is deployed to **Render** as a Docker web service and is currently **live**:

```text
Service:    triply-api (Render, Docker, Free tier)
Branch:     fix/gemini-content-length
URL:        https://triply-api-za13.onrender.com
Health:     https://triply-api-za13.onrender.com/health
```

The Free-tier instance sleeps on inactivity, which can delay the first request after idle by ~50 seconds — expected behavior on the current plan, not a bug, and worth knowing before assuming a slow first response is an outage.

**UptimeRobot** watches two HTTP(s) monitors against `/health` on a 5-minute interval (per the plan in `docs/Monitoring.md`), and both currently report **Up**, 100% uptime:

- `Triply Staging API`
- `triply-api-za13.onrender.com`

The acceptance checklist in `docs/Monitoring.md` (UptimeRobot Up, staging smoke test, structured JSON request logs, EF Core command telemetry) is verified against this live deployment — see that file for the full checklist and staging-logging configuration.

---

## 17. CI/CD & Git Workflow

GitHub Actions builds the backend and runs the integration test suite (against the same SQL Server test environment) on every push — changes are verified before merge.

Representative feature commits, in the order the corresponding stages above were built:

```text
feat: implement budget-first destination suggestions
feat: implement deterministic cost aggregation
feat: implement itinerary read write scaffolding
feat: implement trip save retrieve and status lifecycle
feat: implement optimistic trip concurrency
feat: Gemini client, prompt builder, JSON-schema response parsing
feat: AI dataset/schema grounding + validation hardening
feat: partial (DAY/ITEM) regeneration end-to-end
feat: refresh tokens, email confirmation, password reset (Security Task 1)
feat: request size limit + bounded interest lists (Security Task 2)
feat: per-user/IP partitioned rate limiting (Security Task 3)
feat: user profile, preferences, and stats module
feat: staging logging + health check + UptimeRobot monitoring
fix: Gemini model configuration (current `main`/Render deploy)
```

Migration history (`Triply.Api/Migrations/`), for reference: `InitialCreate` → seed-by-`HasData` reference categories → user email/display-name constraints → trip schema check constraints → seeded countries/currencies/destinations → dataset schema alignment → `PlaceInterest` → user preferences & trip metadata → exchange rates → removal of the backend's static seed (PR #66, replaced by dev-time provisioning, §15) → refresh tokens → AI generation schema version.

---

## 18. Current Status Summary

```text
Backend foundation → Core trip APIs → AI integration (Gemini) → Security hardening
       → Reference-data & profile modules → Testing/CI → Deployment (Render) → Monitoring (UptimeRobot)
```

**Completed:** foundation, Docker, SQL Server/EF Core, migrations, API conventions, Swagger, exception handling, auth (register/login/refresh/logout/email-confirm/password-reset), rate limiting (general + login + AI-specific), ownership authorization, security headers, request-size limiting, trip management + destination selection, budget-first suggestions, currency/interest-category/place reference endpoints, deterministic cost aggregation, itinerary persistence + direct item edits, trip save/retrieve/lifecycle, optimistic concurrency, **live Gemini integration (full + partial regeneration, schema + dataset grounding, budget policy)**, AI raw-output retention, user profile/preferences/stats, dev-time dataset provisioning, integration testing, CI, **Render deployment**, **UptimeRobot monitoring**.

**Not yet done:** a real (non-test-double) end-to-end Gemini generation run has not been independently re-verified since the latest schema/model-configuration change; see `Triply.Api/Modules/AI-Orchestration/progress.md` for the exact pending items.

---

## 19. Related Documents
- [API Contract](docs/API%20Contract.md) — exact request/response JSON for every endpoint *(currently covers Auth core flows, reference data, destination suggestions, cost estimate, itinerary, and trip lifecycle in detail; the newer Auth security endpoints, User profile module, and Places lookup still need their sections added — see the note at the top of that file)*
- [API Conventions](docs/API_CONVENTIONS_.md) — naming, DTO, error-format, and versioning rules
- [Monitoring](docs/Monitoring.md) — staging logging configuration and the UptimeRobot acceptance checklist
- `Triply.Api/Modules/AI-Orchestration/progress.md` — detailed, actively-maintained log of the AI integration work (grounding rules, budget policy, test-environment setup)
