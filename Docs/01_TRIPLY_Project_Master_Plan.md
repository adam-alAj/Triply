# TRIPLY — PROJECT MASTER PLAN

*Version 1.0 · Prepared from verified team capability summaries (AI/ML, Backend, Flutter) · Status: Foundation stage — precedes Work Breakdown Structure*

---

## 1. Executive Summary

Triply is an AI-assisted trip-planning platform (mobile + web) that turns a user's destination or budget input into a personalized, day-by-day itinerary with a category-level cost breakdown. This plan sets the realistic implementation path for the **current six-person team**, based strictly on what each track's training record demonstrates — not on assumed skill.

The single most important planning fact, confirmed across all three capability summaries: **the AI track has not yet been trained on Gemini API integration or prompt engineering** (its strength is classical ML/data science), **the Backend track has never integrated with AI, mobile, or web** (its strength is a single ASP.NET Core service), and **the Flutter track has never shipped a production backend or CI/CD pipeline** (its strength is client-side feature delivery against a contract). UI/UX responsibility is documented at **track level** from the shipped design system (`09_DESIGN.md`) and its Flutter implementation (Doc 4 §2), with the individual member's capability still unverified. Cybersecurity ownership is confirmed as **Arab Hammad**, who owns cybersecurity review and follow-up while Backend continues to implement the application-layer controls it has demonstrated.

Given this, the plan deliberately favors **the simplest architecture and the fewest moving parts the team can actually own end to end**: a modular monolith backend, a single Flutter codebase targeting both mobile and web, and an AI responsibility split where the AI track owns *data and prompt/validation logic* while the Backend track owns the *actual Gemini API call* (a REST integration, which is backend's demonstrated strength — not the AI track's).

## 2. Project Vision

Triply helps individuals and families plan domestic or international trips by turning stated preferences (destination-first or budget-first) into a personalized itinerary and a transparent, categorized cost estimate — reducing manual travel research without requiring travel-agency expertise.

## 3. Objectives

- Fast, simple planning flow: preferences → destination validation/recommendation → itinerary → cost breakdown → refinement.
- Genuinely personalized plans (interests: nature, history, food, shopping, adventure, culture, relaxation) rather than generic text.
- Cost estimates broken into accommodation, transportation, food, activities, other — clearly labeled **estimated**, not verified real-time pricing (no such data source exists yet).
- Consistent core functionality across mobile and web.
- Secure-by-default engineering practices appropriate to team's demonstrated security exposure (application-layer only — see Risk R-06).
- A system the current team can realistically build — not the largest system a "typical" travel app could have.

## 4. Scope

### MUST HAVE — MVP
| # | Feature |
|---|---|
| 1 | Registration / login / session (JWT, ASP.NET Core Identity) |
| 2 | Destination-first trip request |
| 3 | Budget-first destination suggestion |
| 4 | AI-generated day-by-day itinerary (Gemini + prompt engineering, grounded in internal dataset) |
| 5 | Category-level estimated cost breakdown |
| 6 | View / edit / regenerate (partial) itinerary |
| 7 | Save trip / return to saved trip |
| 8 | Internal destinations/places/pricing dataset (curated, versioned) |
| 9 | AI-output validation: 0% invented places; deterministic backend cost and budget-feasibility checks |
| 10 | Mobile app (Flutter) and Web (Flutter Web — see ADR-02 in Architecture doc) |

### SHOULD HAVE — Post-MVP
- Conversational refinement (multi-turn chat-style trip editing)
- Multi-traveler / group cost splitting
- Saved-trip sharing (read-only link)
- Basic admin/content-moderation tooling for the internal dataset

### COULD HAVE — Future
- Offline itinerary access
- Push notifications for trip reminders
- Currency conversion for international trips
- Localization / multi-language UI

### OUT OF SCOPE (unless separately approved)
Flight booking, hotel booking, in-app payments, social/reviews features, loyalty programs, a travel marketplace, real-time flight tracking, live/verified pricing feeds. None of these are justified by a stated requirement, and none are supported by current team capability or timeline.

## 5. Team & Capability Summary

| Person | Track | Confirmed strength (High confidence) | Notable gap |
|---|---|---|---|
| Lynn Sharbati | Backend | ASP.NET Core Web API, EF Core/SQL Server, Identity+JWT auth, RBAC/ownership auth, Redis caching, xUnit/Moq testing, Swagger | No CI/CD, no cloud hosting, no microservices, single-service pattern only |
| Dana Yaseen | Flutter/Mobile | Flutter UI, Dart, state (Provider), REST integration (Dio), Firebase Auth/Firestore, media upload, design-system reuse | No production backend/CI/CD, limited automated testing, no large-scale offline sync |
| Aya Maali, Anas Musleh, Adam Alafandi | AI/ML | Python/Pandas/EDA (dataset building), evaluation & error-analysis discipline (baselines, validation) | **No Gemini/LLM API or prompt-engineering training yet** — needs a focused upskilling module before owning Section-8.4-style tasks |
| Rania Muhamid | UI/UX | Design system on record (`09_DESIGN.md`, authoritative) and implemented in the Flutter theme | **Individual capability unverified** (no training summary provided); responsibility documented at track level — see Doc 4 §2 |
| Arab Hammad | Cybersecurity | Security review, secrets-hygiene sign-off, coordination of security work beyond the backend app-layer baseline | Works with Backend for implementation of demonstrated ASP.NET Core controls |

Full profiles are in Document 4 — Team & Responsibilities.

## 6. Development Methodology

Agile, 2-week sprints — this matches the sprint cadence both the Backend and AI tracks already trained under (planning, stand-ups, PR review, retrospective). Each sprint closes with a mentor/lead-reviewed pull request, matching the working pattern evidenced in all three summaries.

## 7. Project Phases

| Phase | Objective | Primary track | Exit criteria |
|---|---|---|---|
| 0 — Discovery & Foundation | Confirm requirements, finalize SRS, resolve open DECISION REQUIRED items | Whole team | SRS signed off |
| 1 — Architecture & Technical Foundations | Confirm modular monolith, repo structure, environments, contracts (API schema, JSON schema for AI output) | Backend + AI | Contracts documented and agreed |
| 2 — Backend Foundation | Auth (Identity+JWT), core entities, DB schema, base CRUD APIs | Backend (Lynn) | Auth + trip/user CRUD endpoints pass integration tests |
| 3 — AI Foundation | Gemini API upskilling; build/curate internal places/pricing dataset; draft prompt templates + JSON output schema | AI (Aya, Anas, Adam) | Prompt produces schema-valid itinerary against sample inputs |
| 4 — Mobile Foundation | Project structure, design system, auth screens, navigation | Flutter (Dana) | Auth flow navigable end-to-end on mobile |
| 5 — Core Trip Planning | Destination-first & budget-first flows, backend endpoints, Flutter screens | Backend + Flutter | End-to-end trip request works with mocked AI response |
| 6 — AI Integration | Backend calls Gemini (real REST integration, backend-owned); AI track's prompt + validation logic wired in | Backend (integration) + AI (logic) | Real Gemini-generated itinerary passes validation checks |
| 7 — Cost Estimation | Backend-computed category breakdown and deterministic budget-feasibility checks | Backend | Cost breakdown returned from curated reference prices |
| 8 — Cross-Platform Integration | Web build via Flutter Web, full mobile+web parity check | Flutter (Dana) | Core flows verified on both mobile and web |
| 9 — Security & Hardening | Rate limiting, input validation, CORS, security headers (all backend-demonstrated skills); external security review of anything beyond that | Backend | Baseline hardening checklist passed |
| 10 — Testing & QA | xUnit/Moq backend tests, Flutter widget tests, AI validation test suite | All tracks | Agreed test coverage thresholds met |
| 11 — Deployment | Environment setup, first deployment — **flagged risk**, no track has evidenced this (see R-02) | Backend (with external support likely needed) | App reachable in a staging environment |
| 12 — Final Validation & Release | End-to-end acceptance test against SRS acceptance criteria | Whole team | MVP acceptance criteria met |

## 8. Major Deliverables
SRS · System Architecture · API contract / OpenAPI spec · Internal places/pricing dataset · Prompt + JSON schema library · Mobile+Web Flutter app · Backend API · Test suites · Deployment environment.

## 9. Dependencies
Flutter work on AI-driven screens depends on the AI JSON contract (Phase 3/6). Cost estimation depends on the dataset (Phase 3) and cost logic (Phase 7). Deployment (Phase 11) depends on hardening (Phase 9) and cannot start before an infrastructure decision is made (**DECISION REQUIRED** — no one has cloud-hosting experience; see Risk R-02).

## 10. Testing Strategy
Backend: xUnit + Moq unit tests, WebApplicationFactory integration tests (demonstrated skill). Flutter: widget tests (basic level demonstrated) plus manual QA per feature checkpoint. AI: systematic validation harness for schema compliance and 0% unsupported/invented places. Cost calculation and budget feasibility are checked deterministically by Backend; D1's AI cost-tolerance check is superseded by AI Contract v2.0.0.

## 11. Security Strategy
Apply only what is demonstrated in code: ASP.NET Core Identity + JWT, role/ownership-based authorization, FluentValidation, rate limiting, CORS, security headers. Anything beyond application-layer controls (penetration testing, secrets-management infrastructure, compliance review) must be reviewed and coordinated by Arab Hammad before being treated as covered.

## 12. Risk Management
See Document 3 (Architecture) §Risks and Document 4 for owners. Top risks: AI hallucination/invented places (mitigated by validation layer), no deployment/CI-CD experience on any track, and no dedicated web capability.

## 13. Definition of Done
A feature is done when: it matches its SRS requirement and acceptance criteria, it is covered by the track's demonstrated testing method, it passes the agreed API/JSON contract, and it has been reviewed via pull request.

## 14. Success Criteria (MVP)
1. A user can go from preferences to a saved, cost-broken-down itinerary on both mobile and web.
2. 0% of places/activities in a generated itinerary are outside the internal dataset (non-negotiable).
3. Cost estimates are computed by Backend from curated `Place.reference_price` values; budget feasibility uses those backend-computed totals. The former AI cost-tolerance target (D1, proposed ±15%) is **not applicable / superseded** by AI Contract v2.0.0, which removes model-generated cost fields.
4. The system runs without requiring capabilities no track member has demonstrated (e.g., no microservices, no custom ML model, no multi-cloud deployment).

---
*This document works together with: 02 — SRS, 03 — System Architecture, 04 — Team & Responsibilities. Any change to scope or phases here must be reflected in all three.*
