# TRIPLY — SOFTWARE REQUIREMENTS SPECIFICATION

*Single source of truth. No implementation task should be created based on a requirement not represented here or explicitly approved as a change.*

## 1. Introduction

**Purpose:** Define the complete functional and non-functional requirements for Triply MVP.

**Scope:** An AI-assisted trip planner delivered as a Flutter mobile app and a Flutter-Web build, backed by a single ASP.NET Core API, using Gemini (LLM) for itinerary generation grounded in an internally curated dataset.

**Product vision:** See Master Plan §2.

**Definitions / Acronyms:** FR = Functional Requirement · NFR = Non-Functional Requirement · LLM = Large Language Model · MVP = Minimum Viable Product · JWT = JSON Web Token.

**Intended audience:** Development team, project supervision, future contributors.

## 2. Overall Description

**Product perspective:** New standalone system; no legacy system replaced.

**User classes:** Guest (unauthenticated, browsing only — **DECISION REQUIRED**: is guest browsing in MVP?), Registered User, (future) Administrator — **TBD**, no admin requirement confirmed for MVP.

**Operating environment:** Flutter mobile (iOS/Android) + Flutter Web, ASP.NET Core Web API backend, SQL Server, Redis (optional, performance phase only), Gemini API (external).

**Constraints:** Team capability constraints as documented in Master Plan §5. No custom ML model will be built (ASSUMPTION, confirmed by AI track's own scoping). No payment processing (OUT OF SCOPE).

**Assumptions:** Users have internet connectivity when planning (no offline requirement in MVP — ASSUMPTION). Internal dataset covers a bounded, "supported destinations" list rather than global coverage (ASSUMPTION — dataset size/scope is **DECISION REQUIRED**).

**Dependencies:** Gemini API availability and rate limits (external dependency, risk R-03).

## 3. System Actors
- **User** — creates/edits/views trips.
- **Triply Backend** — orchestrates requests, owns business logic and data.
- **AI Subsystem (Gemini + prompt/validation logic)** — generates and validates itinerary content; does not own persisted state.
- **Internal Dataset** — source of truth for valid places/pricing.

## 4. Primary User Journeys (condensed — see Master Plan for full list)
1. New user registers and logs in.
2. Destination-first: user picks a destination + preferences → itinerary + cost.
3. Budget-first: user gives budget + preferences (no destination) → system suggests destinations → user picks one → itinerary + cost.
4. User reviews itinerary, requests a partial regeneration (e.g., "change Day 2").
5. User saves a trip and returns to it later.
6. AI generation fails or times out → user sees a clear error and retry option, not a broken state.
7. Requested destination isn't in the supported/internal dataset → system informs user rather than fabricating a plan.

## 5. Functional Requirements

| ID | Name | Priority | Description | Acceptance Criteria |
|---|---|---|---|---|
| FR-AUTH-001 | Registration | Must | User creates an account with email/password | Account created; duplicate email rejected; password meets policy |
| FR-AUTH-002 | Login/Session | Must | JWT-based login, session restore on mobile/web | Valid credentials issue a JWT; invalid credentials rejected with generic error |
| FR-TRIP-001 | Destination-first request | Must | User submits a destination + preferences (interests, dates, budget) | Request validated; missing required fields rejected with field-level errors |
| FR-TRIP-002 | Budget-first destination suggestion | Must | User submits budget + preferences, no destination | System returns 1+ candidate destinations from the internal dataset matching budget/preferences |
| FR-AI-001 | Itinerary generation | Must | Backend sends a structured prompt to Gemini and receives a day-by-day itinerary in a defined JSON schema | Output validates against schema; 100% of places referenced exist in internal dataset |
| FR-AI-002 | Output validation | Must | Every generated plan is checked against the internal dataset before being shown to the user | 0% invented-place rate; failed validation triggers regeneration or a user-facing error, never silent fabrication |
| FR-COST-001 | Category cost breakdown | Must | System returns accommodation/transportation/food/activities/other estimates per trip | All categories present; total = sum of categories; each figure labeled "Estimated" |
| FR-TRIP-003 | Edit/regenerate itinerary | Must | User can request changes to part of the plan | A day or item can be regenerated without discarding the rest of the plan |
| FR-TRIP-004 | Save / retrieve trip | Must | User can save and later reopen a generated trip | Saved trip is retrievable exactly as last confirmed by the user |
| FR-MOBILE-001 | Mobile parity | Must | All MVP flows above work on the Flutter mobile app | Manual QA checklist passed on at least one Android and one iOS-equivalent target |
| FR-WEB-001 | Web parity | Must | All MVP flows above work on the Flutter Web build | Manual QA checklist passed in at least one modern desktop browser |
| FR-TRIP-005 | Conversational refinement | Should | Multi-turn chat-style trip editing | Post-MVP — not required for release |
| FR-DATA-001 | Internal dataset maintenance | Must | AI track curates/updates destinations, places, categories, reference pricing | Dataset versioned; changes reviewed before use in generation |

## 6. Non-Functional Requirements

| ID | Category | Requirement | Note |
|---|---|---|---|
| NFR-PERF-001 | Performance | Itinerary generation response should complete within an acceptable wait (target: **TBD**, pending Gemini latency testing) | Cannot commit a number until Phase 3 upskilling produces real latency data |
| NFR-AVAIL-001 | Availability | Single-service deployment; no formal uptime SLA in MVP | Consistent with single-service architecture, no HA infra experience on team |
| NFR-SEC-001 | Security | JWT auth, role/ownership authorization, input validation (FluentValidation), rate limiting, CORS, security headers | Matches Backend track's demonstrated skillset exactly — no more, no less |
| NFR-SEC-002 | Security | No sensitive data (passwords) stored in plaintext; standard ASP.NET Core Identity hashing used | — |
| NFR-PRIV-001 | Privacy | User trip data is only accessible to its owner (ownership-based authorization) | Directly matches Backend Sprint-2 evidence |
| NFR-MAINT-001 | Maintainability | Single-service (modular monolith) codebase with clear module boundaries | Matches team's single-service experience; avoids unproven microservices |
| NFR-USE-001 | Usability | Consistent core UX between mobile and web | Enabled by shared Flutter codebase (see Architecture ADR-02) |
| NFR-OBS-001 | Observability | Built-in ASP.NET Core `ILogger` + EF Core query logging | No centralized logging stack evidenced — **TBD** if added later |
| NFR-TEST-001 | Testability | Backend: xUnit/Moq/WebApplicationFactory. Flutter: widget tests | Matches demonstrated tooling on each track |

## 7. Data Requirements
Users, Trips, Itinerary Days/Items, Destinations, Places, Pricing Reference, Interest Categories. Relational schema in SQL Server via EF Core (backend-demonstrated). The Places/Pricing dataset is the **single authority** an AI-generated plan is checked against.

## 8. AI Requirements
- AI is responsible for: generating a candidate itinerary and (given budget-first input) candidate destinations — both grounded via prompt in the internal dataset.
- AI is **not** responsible for: authentication, persistence, payment, or any deterministic business rule that can be computed directly (e.g., summing cost categories is plain backend logic, not an LLM task).
- Output must be structured JSON matching an agreed schema; invalid/non-conforming output is rejected and regenerated, not silently passed through.
- AI-generated content is always labeled **estimated / AI-generated** to the user and is distinct from **verified/deterministic** system data (e.g., the user's own saved trips, account data).
- Hallucination mitigation: mandatory post-generation validation against the internal dataset (FR-AI-002) — this is the project's non-negotiable success criterion.

## 9. API / Integration Requirements
REST/JSON over HTTPS between Flutter clients and backend. Backend owns the single external integration point to Gemini (ASSUMPTION — see Architecture ADR-01: AI track does not have REST/API-consumption training yet, so the live integration call is backend-owned; AI track owns prompt content, schema, and validation rules).

## 10. Security Requirements
JWT bearer auth, ASP.NET Core Identity, role- and ownership-based authorization, FluentValidation input validation, rate limiting, CORS policy, standard security headers. **Explicitly out of current capability:** penetration testing, secrets-management infrastructure, formal compliance review — flagged as a project risk (R-06), not silently assumed away.

## 11. Error Handling Requirements
Centralized error handling (ASP.NET Core `ProblemDetails`, per Backend track's demonstrated pattern). AI generation failures return a clear, distinguishable error rather than a partial or fabricated plan.

## 12. Validation Requirements
FluentValidation on all backend inputs. AI output schema validation before any content reaches the user or database.

## 13. Platform Requirements
Flutter (mobile + Flutter Web, single codebase), ASP.NET Core Web API, SQL Server, optional Redis (only if performance work is triggered).

## 14. MVP Scope
See Master Plan §4 MUST HAVE.

## 15. Post-MVP Scope
See Master Plan §4 SHOULD/COULD HAVE.

## 16. Assumptions
- No custom ML model is built; Gemini + prompt engineering is the chosen AI approach (confirmed by AI track's own project scoping).
- Backend owns the live Gemini REST call; AI owns prompt/schema/validation content (**DECISION REQUIRED** — confirm with AI + Backend leads before Phase 6).
- Bounded/supported destination list, not global coverage.
- No offline mode in MVP.

## 17. Open Questions / Decisions Required
| # | Question | Why it matters |
|---|---|---|
| D1 | AI cost-tolerance band (proposed ±15%) | **Not applicable / superseded by AI JSON Contract v2.0.0:** model output contains no cost fields; the Backend computes costs from curated reference prices. D1 is closed and does not define FR-AI-002 acceptance. |
| D2 | Is guest (unauthenticated) browsing in scope? | Affects FR-AUTH and navigation architecture |
| D3 | Hosting/infrastructure choice | No team member has cloud deployment experience — needs an explicit decision before Phase 11 |
| D4 | Who owns UI/UX and Cybersecurity responsibilities long-term | Cybersecurity ownership is confirmed as Arab Hammad; UI/UX is owned at track level for the existing design system |
| D5 | Supported destination list size/scope for MVP | Drives dataset-curation workload (Phase 3) |

## 18. Traceability
Each FR/NFR above maps to an architecture component and an owning track in Documents 3 and 4 respectively — see the Traceability table in Document 4 §Ownership Model.
