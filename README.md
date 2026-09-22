# Triply

AI Travel Planner — Flutter mobile/web client, ASP.NET Core backend, and AI-track
dataset & prompt tooling.

## Repository layout

| Path | What it is |
|---|---|
| `Backend/` | ASP.NET Core Web API (modular monolith, EF Core + SQL Server) — see [Backend/README.md](Backend/README.md) |
| `Mobile/` | Flutter app (mobile + web client) |
| `AI/` | Curated dataset (`01-Dataset/`), prompt engineering, validation rules |
| `Docs/` | SRS, system architecture, database design, UI/API specifications |

## Standard local setup (fresh clone → working environment)

This is the **single documented initialization path**. Following it always
produces the same valid reference-data state — there are no manual seeding steps.

1. Clone the repository.

2. Create the environment file:

   ```bash
   cd Backend
   cp .env.example .env
   ```

   Fill in `DB_SA_PASSWORD`, `JWT_KEY` and `GEMINI_API_KEY` (placeholders are
   marked `REPLACE_...`). `.env` is gitignored and must never be committed.

3. Start the standard development environment:

   ```bash
   docker compose up -d --build
   ```

   Compose starts SQL Server (`db`) and the API (`api`, running with
   `ASPNETCORE_ENVIRONMENT=Development`) and mounts the curated dataset
   read-only at `/AI`.

4. On first boot the API **automatically**:

   - runs all EF Core migrations (`Database.Migrate()`), then
   - provisions curated reference data from `AI/01-Dataset/curated-data/`
     (countries, currencies, categories, destinations, places, place↔interest
     links, placeholder exchange rates) — additive, idempotent, Development-only
     (`Backend/Triply.Api/Data/SeedData.cs`).

   If the dataset cannot be found or a required reference table would remain
   empty, startup **fails with a clear error** instead of running with a
   partially provisioned database.

5. Verify:

   - health: `http://localhost:8080/health`
   - Swagger: `http://localhost:8080/swagger`
   - reference data via the API (destination suggestions / trip generation)

Start from a clean slate any time with `docker compose down -v` (drops the
database volume only) — the next start re-migrates and re-provisions the exact
same reference state.

### What is automatic vs manual

| Step | Automatic? |
|---|---|
| Database creation + EF migrations | ✅ on API startup |
| Curated reference-data provisioning | ✅ Development startup (idempotent, fail-fast) |
| Exchange-rate rows | ✅ placeholder insert-if-missing (maintained manually afterwards) |
| Python dataset seeders (`AI/01-Dataset/seed/*.py`) | ❌ manual/optional — kept for offline dataset ops, **not** part of the standard setup |
| Tests | `cd Backend && dotnet test` (CI runs the same suite against SQL Server) |

## Further reading

- [Backend/README.md](Backend/README.md) — backend architecture, stages and testing
- [Docs/](Docs/) — SRS, architecture, database design spec, configuration guide
- [AI/01-Dataset/seed/README.md](AI/01-Dataset/seed/README.md) — curated dataset tooling
