# TRIPLY — RECOMMENDED FREE TECHNOLOGY STACK

*Prepared from Documents 1–5 (Master Plan, SRS, Architecture, Team & Responsibilities, Database Design). The SRS and Architecture are treated as fixed; this document selects technologies only — no architecture redesign.*

---

## SECTION 1 — EXECUTIVE SUMMARY

Triply's stack is dictated by one hard rule: **every choice must be free and must match a capability a team member already demonstrated.** The result is deliberately boring and 100% buildable by this team: one ASP.NET Core backend with EF Core + SQL Server (Lynn's capstone-tested stack), one Flutter codebase for mobile + web (Dana's capstone-tested stack), and the Gemini API on its genuine free tier as the only AI component — no custom ML model, no vector database, no RAG pipeline, nothing the AI track hasn't been trained on yet. Every layer is open-source software; the only cloud free tiers involved are Gemini's API quota, GitHub Actions minutes, and whichever hosting option is chosen for the demo environment (a pending D3 decision). Nothing in this stack requires a credit card to start, and every "may become paid" item is flagged explicitly.

---

## SECTION 2 — FINAL RECOMMENDED STACK

| Category | Recommended Technology | Cost | Why We Choose It |
|---|---|---|---|
| Web Frontend | Flutter Web (same codebase as mobile) | OPEN-SOURCE | Architecture ADR-02 already decided this; no web-frontend skill exists on the team |
| Mobile App | Flutter (Dart) | OPEN-SOURCE | Dana's demonstrated, capstone-tested stack |
| Backend Framework | ASP.NET Core Web API (.NET 8 LTS) | OPEN-SOURCE | Lynn's demonstrated stack; modular monolith matches ADR-00 |
| ORM | EF Core (code-first migrations) | OPEN-SOURCE | Demonstrated in Backend capstone; matches Document 5 schema |
| Database | SQL Server (Developer Edition for dev / testing) | FREE FOR DEVELOPMENT | Matches Document 5's deliberate decision; production hosting pending D3 |
| AuthN / AuthZ | ASP.NET Core Identity + JWT bearer | OPEN-SOURCE | Sprint-2 demonstrated pattern (role + ownership auth, NFR-SEC-001/002, NFR-PRIV-001) |
| AI / LLM | Gemini API via Google AI Studio (Flash-family models) | FREE TIER | Only AI approach in the SRS; Flash models are free with no card required — see free-tier privacy caveat in Section 5 |
| AI Interaction | HTTPS JSON from Backend AI-Orchestration module | OPEN-SOURCE | ADR-01: Backend owns the call; AI track owns prompt/schema/validation content |
| Dataset Curation | Python + Pandas + Jupyter/Colab | OPEN-SOURCE / FREE | AI track's demonstrated strength (EDA, data engineering) |
| Caching | Redis (optional, per Architecture §6) | OPEN-SOURCE (local) | Demonstrated Sprint 3; only add if Phase performance work triggers it |
| Input Validation | FluentValidation | OPEN-SOURCE | Demonstrated backend tool; SRS §12 |
| API Communication | REST/JSON over HTTPS + Dio (Flutter side) | OPEN-SOURCE | Both tracks demonstrated this exact pattern |
| API Documentation | Swashbuckle (Swagger/OpenAPI) + Markdown in repo | OPEN-SOURCE | Demonstrated by Backend; Flutter consumes the contract from it |
| File / Image Storage | Not required for MVP (no upload requirement exists) | — | Place images, if added, → OPTIONAL: Cloudinary free tier (Dana already used it) |
| Version Control | Git + GitHub | FREE (private repos) | Standard; PR-review workflow matches methodology §6 |
| CI/CD | GitHub Actions | FREE TIER (~2,000 min/month on private repos; free on public repos) | Simplest possible CI; zero new tools for the team |
| Deployment / Hosting | **REQUIRES DECISION (D3)** — demo path: Render free tier (Docker); alternative: Azure free/student credit | FREE TIER | No track has hosting experience; Render's free web service + sleep policy is acceptable for a graduation demo |
| Monitoring / Logging | ASP.NET Core `ILogger` + EF Core query logs (NFR-OBS-001) + UptimeRobot free tier for uptime checks | FREE | Matches SRS — no centralized stack is required or evidenced |
| Security | Built-in middleware: rate limiter, CORS, security headers + OWASP ZAP (free) for a one-time scan | OPEN-SOURCE / FREE | Exactly NFR-SEC-001's scope — nothing beyond app-layer baseline |
| Testing | xUnit + Moq + WebApplicationFactory (Backend); flutter_test + widget tests (Flutter); Python validation harness (AI) | OPEN-SOURCE | NFR-TEST-001: matches demonstrated tooling on each track |

**Explicitly NOT selected (no justification in the documents):** vector database, RAG pipeline, embeddings, self-hosted LLM (Ollama/Llama fine-tuning), message queues, microservices, Kubernetes, MongoDB, Elasticsearch, maps SDK, live pricing APIs. The SRS's anti-hallucination rule is enforced by the `ItineraryItem.place_id` FK + dataset validation (FR-AI-002), not by vector search — Document 5 already made this a database-level guarantee, so adding retrieval infrastructure would be over-engineering.

---

## SECTION 3 — TECHNOLOGY BY TEAM

### AI / ML — Aya, Anas, Adam
| Technology | Purpose | Cost |
|---|---|---|
| Python + Pandas + Jupyter / Google Colab | Curate and version the destinations/places/pricing dataset (FR-DATA-001); run the validation harness (0% invented places, cost tolerance) | OPEN-SOURCE / FREE |
| Google AI Studio | Prompt prototyping and JSON-schema testing before Phase 6 handoff to Backend | FREE |
| Gemini API (Flash models) | Itinerary + budget-first destination suggestion generation (FR-AI-001) | FREE TIER (rate-limited; data may be used for training) |

*No classical ML model, no deep learning, no training, no embeddings — none of it is required by the SRS.*

### Backend — Lynn
| Technology | Purpose | Cost |
|---|---|---|
| .NET 8 / ASP.NET Core Web API | Modular monolith: Auth, Trip, AI-Orchestration, Cost modules | OPEN-SOURCE |
| EF Core + SQL Server | Schema exactly per Document 5 (17 tables) | OPEN-SOURCE / FREE FOR DEVELOPMENT |
| ASP.NET Core Identity + JWT | FR-AUTH-001/002, NFR-SEC-002, ownership authorization (NFR-PRIV-001) | OPEN-SOURCE |
| FluentValidation + built-in RateLimiter/CORS/headers | NFR-SEC-001, SRS §10/§12 | OPEN-SOURCE |
| Swashbuckle (Swagger) | API contract documentation for Dana's integration checkpoints | OPEN-SOURCE |
| xUnit + Moq + WebApplicationFactory | Unit + integration tests (NFR-TEST-001) | OPEN-SOURCE |
| Redis (optional) | Cache-aside only if performance work is triggered | OPEN-SOURCE |

### Flutter / Mobile + Web — Dana
| Technology | Purpose | Cost |
|---|---|---|
| Flutter / Dart (mobile + web targets) | Entire client, one codebase (ADR-02, FR-MOBILE-001, FR-WEB-001) | OPEN-SOURCE |
| Provider | State management (demonstrated) | OPEN-SOURCE |
| Dio + Repository/DI pattern | REST integration against Backend contract | OPEN-SOURCE |
| flutter_test / widget tests | NFR-TEST-001 Flutter side | OPEN-SOURCE |
| Firebase App Distribution (optional) | Free distribution of Android test builds to the team | FREE TIER |

### UI / UX / Web — Rania
| Technology | Purpose | Cost |
|---|---|---|
| **TBD — Pending Capability Analysis** (Team Doc §2) | No design-tool commitment until Rania's capability is confirmed. If confirmed: Figma free tier for design specs → Flutter design tokens | FREE TIER (if confirmed) |

### Cybersecurity — Arb
| Technology | Purpose | Cost |
|---|---|---|
| **No one assigned — TBD** (Team Doc §2) | MVP security = Backend's app-layer baseline (NFR-SEC-001). Optional one-time OWASP ZAP scan before Phase 9 exit | FREE |

### DevOps / Deployment — shared learning need (flagged in Team Doc §8)
| Technology | Purpose | Cost |
|---|---|---|
| GitHub Actions | Build + test + deploy on push | FREE TIER |
| Render free tier (Docker web service) | Demo/staging hosting for the API | FREE TIER (sleeps on idle; limited resources) |
| Azure SQL Database free tier **or** SQL Server in the same Docker host | Production-shaped DB at zero/low cost | FREE TIER (100k vCore-seconds/month) — **needs D3 decision** |
| UptimeRobot | Free uptime check for the staging URL | FREE TIER |

---

## SECTION 4 — COMPLETE SYSTEM STACK

```
Flutter Mobile App ─┐
                    ├─ HTTPS / REST+JSON ─▶ ASP.NET Core API (modular monolith)
Flutter Web ────────┘                          │
                                               ├─▶ SQL Server (EF Core)
                                               │      users, trips, itinerary,
                                               │      places, cost estimates
                                               ├─▶ Redis (OPTIONAL — cache-aside)
                                               ├─▶ Internal Dataset (SQL tables:
                                               │      Destination, Place, Currency)
                                               └─▶ Gemini API (external, HTTPS)
                                                      Flash model, free tier
                                                      ├─ AI track provides:
                                                      │   prompt templates,
                                                      │   JSON schema, validation rules
                                                      └─ Backend validates output
                                                         against dataset (FR-AI-002)
                                                        ↓
                                             Validated itinerary + cost breakdown
                                                       ↓
                                             Flutter clients (labeled "Estimated")
```

No other services. No message queue, no vector store, no second database, no separate AI service — consistent with ADR-00 and the SRS's "simplest architecture the team can fully own" principle.

---

## SECTION 5 — FREE VS PAID

| Technology / Service | Default Status | When Could We Pay? |
|---|---|---|
| .NET / EF Core / ASP.NET Core Identity / FluentValidation / Flutter / Dart / Dio / xUnit / Moq / Pandas | **FREE / OPEN-SOURCE — permanently** | Never (licensing) |
| SQL Server Developer Edition | **FREE FOR DEVELOPMENT** — not licensed for production | If Triply goes to real production → paid SQL Server license or Azure SQL |
| Gemini API (Flash models) | **FREE TIER** — no card needed; ~10 RPM / ~1,500 requests/day on Gemini 3 Flash; ⚠️ free-tier prompts may be used by Google to improve its products | Pay-per-token if daily quota or privacy becomes an issue; Pro models are already paid-only |
| GitHub (private repo) + GitHub Actions | **FREE TIER** (~2,000 CI minutes/month) | Larger teams/private-repo scale-up |
| Render hosting | **FREE TIER** — web service sleeps when idle; limited RAM/CPU | Always-on staging/production → ~$7+/month |
| Azure (student credit + free services, incl. Azure SQL free tier) | **FREE TIER** for 12 months (student) | After graduation / credit exhaustion |
| Cloudinary (place images — OPTIONAL, not in MVP) | **FREE TIER** (25k credits/month) | Heavy image traffic |
| UptimeRobot | **FREE TIER** (50 monitors) | More monitors/SMS alerts |
| OWASP ZAP | **FREE / OPEN-SOURCE** | Never |
| Figma (if UI/UX confirmed) | **FREE TIER** | Team plan |

**Important distinction:** everything in the left column above marked OPEN-SOURCE is free software with no quota. Items marked FREE TIER are commercial cloud services with limits — they can change their terms at any time. For a graduation project all free tiers listed are comfortably sufficient; the only one with a realistic risk of being hit is the Gemini daily request cap, which is mitigated by caching generations per trip and by the bounded test-user population.

---

## SECTION 6 — ALTERNATIVES

**Backend:** ASP.NET Core → *Alternative:* NestJS (Node.js). *Reason:* technically fine, but zero team evidence — Lynn would learn a new framework mid-project. No.

**Database:** SQL Server → *Alternative:* PostgreSQL. *Reason:* technically strong (JSON columns, geo types), but Document 5 already ruled it out on team-capability grounds. Only revisit if hosting costs force it.

**ORM:** EF Core → *Alternative:* Dapper. *Reason:* faster raw SQL, but adds manual mapping work for 17 tables; EF Core is what Lynn demonstrated.

**LLM:** Gemini API free tier → *Alternative:* Groq or Mistral free tier (Llama-class models). *Reason:* both offer free tiers, but Gemini is already written into the SRS, has the largest free context window, and requires no new decision. Keep as fallback if Gemini's free quota proves too tight.

**Hosting:** Render free tier → *Alternative:* Fly.io free allowances (3 small VMs). *Reason:* no cold starts, but deployment via Docker/CLI is a steeper learning curve for a team with zero DevOps evidence. Railway is *not* a true free option (credit-based).

**CI/CD:** GitHub Actions → *Alternative:* Azure DevOps free tier. *Reason:* equivalent, but GitHub keeps repo + CI + PR reviews in one place — fewer tools to learn.

**Flutter Web hosting:** Firebase Hosting free tier → *Alternative:* GitHub Pages / Render static. *Reason:* Firebase is what Dana already used in training; free SSL + CDN + simple deploy.

**Maps/geolocation:** None (not required) → *Alternative:* Google Maps Platform free tier ($200 monthly credit). *Reason:* the schema only stores lat/long; no map-rendering requirement exists in the SRS. Only add if a future requirement demands it.

**File/image storage:** None for MVP → *Alternative:* Cloudinary free tier. *Reason:* no upload requirement exists in any Triply document.

---

## SECTION 7 — FINAL RECOMMENDATION

- **Frontend (Web):** Flutter Web — same codebase as mobile
- **Mobile:** Flutter (Dart), Provider, Dio
- **Backend:** ASP.NET Core 8 Web API, modular monolith
- **Database:** SQL Server (Developer Edition dev / Azure SQL free tier for hosted env), EF Core
- **AI/ML:** Python + Pandas (dataset curation + validation harness only)
- **LLM:** Gemini API, Flash-family models, via Google AI Studio key — free tier
- **Authentication:** ASP.NET Core Identity + JWT (role + ownership)
- **Storage:** none required for MVP (optional Cloudinary free tier later)
- **Testing:** xUnit + Moq + WebApplicationFactory; flutter_test; Python validation scripts
- **Deployment:** REQUIRES DECISION (D3) — default demo path: Render free tier + Azure SQL free tier
- **CI/CD:** GitHub Actions
- **Monitoring:** ILogger + EF query logs + UptimeRobot free tier
- **Documentation:** Swashbuckle/Swagger + Markdown in the repo (GitHub Pages if a public docs site is wanted — free)

---

## SECTION 8 — DECISIONS THAT MUST BE CONFIRMED

1. **Hosting platform (D3 — SRS §17).** No track has deployment experience. Confirm: Render free tier (simplest) vs. Azure student/free services vs. university-provided server. This is the single most consequential open technical decision.
2. **Gemini free-tier privacy trade-off.** Free-tier data may be used by Google to improve its products. Mitigation: `input_snapshot` must exclude PII (enforce in AI-Orchestration). If supervisors reject even that, Gemini paid tier becomes a small real cost — must be approved.
3. **Gemini model choice within free tier.** Gemini 3 Flash (best quality, 1,500 RPD) vs. Flash-Lite (higher RPM). Confirm after Phase 3 latency testing (NFR-PERF-001 is TBD for exactly this reason).
4. **Production licensing of SQL Server.** Developer Edition cannot host the "production" demo. Confirm: Azure SQL free tier vs. running SQL Server in a Docker container on the host (free, but ops burden falls on Lynn).
5. **Cost tolerance band (D1, ±15% proposed)** — affects the AI validation harness, not the stack, but blocks Phase 7 test thresholds.
6. **Guest browsing (D2)** — if in scope, it changes only authorization config, but confirm before Phase 2.
7. **Flutter Web hosting target** — Firebase Hosting free tier is the default; confirm no university hosting is mandated.
8. **UI/UX and Cybersecurity tracks (D4)** — tool choices (Figma, ZAP) remain frozen until capability analysis is done.

---

**Validation check (performed):** every technology maps to a requirement in the SRS or a decision in Documents 3–5; all fit the modular-monolith architecture without modification; every selection matches demonstrated team capability except hosting/CI-CD, which are explicitly flagged as learning objectives with the simplest possible tools; cost classifications distinguish open-source software from cloud free tiers; no vector DB, RAG, self-hosted model, or other unjustified AI technology was introduced; no paid service is part of the default stack. No conflict with the SRS or Architecture was found.
