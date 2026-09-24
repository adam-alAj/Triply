# Triply Backend

Triply is an AI-assisted trip planner (mobile + web). The user picks a destination (or a budget), and the backend builds a **day-by-day itinerary** with a **cost breakdown**.

This is the backend: one ASP.NET Core API + SQL Server, calling Gemini to generate plans.

> **Status: live.** Deployed on Render and monitored by UptimeRobot. A real Gemini generation was verified end-to-end on the live service (24 Sep 2026).

| | |
|---|---|
| Live API | https://triply-api-za13.onrender.com |
| Health check | https://triply-api-za13.onrender.com/health |
| Local Swagger | http://localhost:8080/swagger (Development only) |
| Owner | Leen Sharbati (Backend track) |

> The free Render instance sleeps when idle, so the **first request can take ~50 seconds**. That is normal, not an outage.

---

## 1. The idea in one picture

```text
User preferences
   → create trip (destination-first OR budget-first)
   → AI generates itinerary (Gemini)
   → backend checks every place against our own dataset
   → backend calculates costs
   → user edits / regenerates a day or an item
   → save → later: open, archive, restore
```

**The most important rule:** AI output is never trusted. Every place in a generated plan must exist in our curated dataset, be active, and belong to a supported destination. If not, the plan is rejected and retried, and finally the user gets a clear error. **A made-up place never reaches the user.**

---

## 2. Tech stack

- .NET 9 / ASP.NET Core (SDK pinned in `global.json`)
- Entity Framework Core + SQL Server
- ASP.NET Core Identity + JWT + refresh tokens
- FluentValidation
- Gemini API (the only external service)
- Docker + Docker Compose (local), Docker on Render (deployment)
- xUnit + `WebApplicationFactory` (tests), GitHub Actions (CI)

---

## 3. Project structure

```text
Backend/
├── Triply.Api/
│   ├── Common/          # exception middleware, trip-ownership policy
│   ├── Data/            # DbContext + dataset seeding (Development only)
│   ├── Entities/        # database entities
│   ├── Migrations/      # 12 EF Core migrations
│   ├── AI-Schemas/      # Gemini JSON schema (v2.0.0)
│   └── Modules/
│       ├── Auth/            register, login, refresh, logout, email, password reset
│       ├── User/            profile, preferences, stats
│       ├── Trip/            create, edit, save, archive, restore
│       ├── Destination/     supported destinations + budget-first suggestions
│       ├── Place/           one place by id
│       ├── Currency/        currencies + conversion
│       ├── InterestCategory/ interest list
│       ├── Cost/            cost calculation
│       ├── Itinerary/       days/items, edit one item
│       └── AI-Orchestration/ Gemini call, prompt, validation, retries
├── Triply.Api.Tests/    # integration + unit tests
├── docs/                # API contract, conventions, monitoring
├── docker-compose.yml
└── progress.md          # what is done / what is left
```

---

## 4. What the API can do

Every route starts with `/api` and (except where marked) needs `Authorization: Bearer <token>`.
Exact request/response examples: **[docs/API Contract.md](docs/API%20Contract.md)**.

| Area | Endpoints |
|---|---|
| **Auth** (public) | `POST /auth/register` · `/auth/login` · `/auth/refresh` · `/auth/forgot-password` · `/auth/reset-password` · `/auth/resend-confirmation` · `GET /auth/confirm-email` |
| **Auth** (token) | `POST /auth/logout` |
| **Profile** | `GET/PATCH /users/me` · `GET/PUT /users/me/preferences` · `GET /users/me/stats` |
| **Reference data** | `GET /destinations` · `GET /destinations/assets` (public) · `GET /currencies` · `GET /interest-categories` · `GET /places/{id}` |
| **Budget-first** | `POST /destinations/suggestions` |
| **Trips** | `POST /trips` · `GET /trips` · `GET /trips/{id}` · `PUT /trips/{id}` · `PATCH /trips/{id}` (title/cover) · `PATCH /trips/{id}/destination` |
| **Trip lifecycle** | `POST /trips/{id}/save` · `/archive` · `/restore` |
| **AI generation** | `POST /trips/{id}/generate` (`FULL`, `DAY`, or `ITEM`) |
| **Itinerary** | `GET/POST /trips/{id}/itinerary` · `PATCH /trips/{id}/itinerary/items/{itemId}` |
| **Costs** | `GET /trips/{id}/cost-estimate` |
| **Health** | `GET /health` (public) |

---

## 5. How the main parts work

### 5.1 Two ways to plan
- **Destination-first** – the user already knows where to go.
- **Budget-first** – the user gives a budget and interests; the backend suggests up to 3 supported destinations that fit (`POST /destinations/suggestions`), based on the real place prices in the dataset.

### 5.2 Trip status (lifecycle)
```text
DRAFT → GENERATING → GENERATED → MODIFIED → SAVED → ARCHIVED
                ↘ (failed) → DRAFT          (ARCHIVED can be restored to SAVED)
```
There is **no "set status" endpoint**. The status changes only as a result of a real action (generate, edit, save, archive, restore), so a trip can never be forced into a wrong state.

### 5.3 AI generation (Gemini)
1. Load candidate places and prices for the destination from our dataset.
2. Build a prompt for the planning mode.
3. Call Gemini with a fixed JSON schema (v2.0.0).
4. Validate: correct schema → every place exists, is active, and is in a supported destination.
5. If invalid → retry (bounded). Still invalid → clear error, nothing saved.
6. If valid → save the itinerary, calculate costs, bump the trip version.

`POST /trips/{id}/generate` scopes:

| Scope | What it does |
|---|---|
| `FULL` | Builds the whole itinerary for a `DRAFT` trip |
| `DAY` | Regenerates one day (needs `dayNumber` + `expectedVersion`) |
| `ITEM` | Regenerates one activity (needs `itemId` + `expectedVersion`) |

**Budget rules**
- Budget-first: the AI may return up to 3 options; the first one within budget is saved. If none fits → `422`.
- Destination-first: the plan is saved even if over budget, and the response has `isOverBudget: true`.

**Errors from generate:** `422` plan failed validation · `502` Gemini failed · `409` version conflict · `429` too many generations.

### 5.4 Costs
Costs are calculated by plain backend code (not by the AI), grouped by category (accommodation, transportation, food, activities, other). Accommodation = price × nights. Every number is marked `isEstimated: true`.

### 5.5 Two users editing at once (`version`)
Every trip has a `version`. Edits and DAY/ITEM regeneration send `expectedVersion`. If it is old, the API answers **`409 Conflict`** instead of overwriting newer work.

### 5.6 Ownership
Users can only see their own trips. Someone else's trip returns **`404`** (not 403), so its existence is not revealed.

---

## 6. Security

- Passwords hashed by ASP.NET Core Identity; password policy: 8+ chars, upper + lower + digit + symbol.
- **Access token: 15 minutes.** Refresh token: 30 days, stored only as a SHA-256 hash, rotated on every refresh, revoked on logout and on password reset.
- Login / forgot-password never reveal whether an email exists.
- **Rate limits (per user, or per IP if anonymous):** login 5/min · general endpoints 10/min · AI generation 10/hour.
- Max request body 1 MB; lists from the client are bounded.
- Security headers on every response (`nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`).
- CORS only for the listed origins (`Cors:AllowedOrigins`).
- Errors use `ProblemDetails`; stack traces are never returned.
- Secrets (DB password, JWT key, Gemini key) come from environment variables, never from the code.

> **Known limitation:** emails are not really sent yet. `LoggingEmailSender` only writes them to the log, so email confirmation and password-reset emails need a real email provider before production. Email confirmation is off by default (`Auth:RequireConfirmedEmail = false`).

---

## 7. Data and migrations

- 12 migrations in `Triply.Api/Migrations/`, from `InitialCreate` to `AddAiGenerationSchemaVersion`.
- The backend does **not** contain reference data. The curated dataset (countries, currencies, destinations, places, interests) comes from the AI track's CSV files (`AI/01-Dataset/curated-data`).
- In **Development** only, the API loads those CSVs automatically at startup. It is safe to re-run (inserts only what is missing, never deletes) and stops with a clear error if a file is missing.
- Staging/Production are never seeded by the app.
- `AIGeneration.RawOutput` (the raw Gemini reply, kept for debugging) is cleared after 30 days by a background service (`DataRetention:RawOutputDays`).

---

## 8. Run it locally

1. Create `.env` next to `docker-compose.yml` (copy `.env.example` and fill in real values):
   ```text
   DB_SA_PASSWORD=...
   JWT_KEY=...            # long random string
   GEMINI_API_KEY=...
   ```
2. Start everything:
   ```powershell
   docker compose up -d --build
   docker compose ps
   ```
3. Open:
   - API: http://localhost:8080
   - Swagger: http://localhost:8080/swagger
   - Health: http://localhost:8080/health

Stop: `docker compose down` · wipe the database too: `docker compose down -v`.

> Never commit `.env`. Make sure it is in `.gitignore`.

---

## 9. Tests

```powershell
$env:TRIPLY_TEST_DB_CONNECTION = "Server=localhost,1433;Database=TriplyDb;User Id=sa;Password=<DB_SA_PASSWORD>;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet test Triply.Api.Tests/Triply.Api.Tests.csproj
```

- 28 test files: 121 `[Fact]` + 2 `[Theory]`.
- They cover auth, ownership, validation, suggestions, costs, itinerary, lifecycle, concurrency, rate limits, full and partial AI generation, dataset loading, and data retention.
- CI (GitHub Actions) builds and runs the suite on every push.
- Gemini is replaced by a fake client in tests, so tests are fast and free.
- Last full run: 119/119 passed on 23 Sep 2026. Please re-run once to confirm the current 121 + 2.

---

## 10. Deployment and monitoring

- **Render** (Docker web service `triply-api`, free plan). Latest deploy: *"fix Gemini model configuration"*, live.
- **UptimeRobot**: 2 HTTP monitors on `/health`, every 5 minutes, both **Up** with 100% uptime.
- Staging writes JSON logs, request logs, and SQL command logs (no sensitive values). Details: [docs/Monitoring.md](docs/Monitoring.md).

---

## 11. Other documents

| File | What it is |
|---|---|
| [docs/API Contract.md](docs/API%20Contract.md) | Every endpoint with request/response examples (for Flutter) |
| [docs/API_CONVENTIONS.md](docs/API_CONVENTIONS.md) | Naming, JSON, error format, status codes |
| [docs/Monitoring.md](docs/Monitoring.md) | Logging, health check, UptimeRobot |
| [progress.md](progress.md) | What is done and what is left |
| [Triply.Api/Modules/AI-Orchestration/progress.md](Triply.Api/Modules/AI-Orchestration/progress.md) | Detailed AI integration log |
