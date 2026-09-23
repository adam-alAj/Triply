# TRIPLY — TEAM & RESPONSIBILITIES

## 1. Team Structure

| Name | Track | Role on Triply |
|---|---|---|
| Lynn Sharbati | Backend | Backend API owner |
| Dana Yaseen | Flutter/Mobile | Client (mobile + web) owner |
| Aya Maali | AI/ML | Dataset & AI-output validation |
| Anas Musleh | AI/ML | Dataset & prompt/schema design |
| Adam Alafandi | AI/ML | Dataset & AI-output validation |
| Rania Muhamid | UI/UX / Graphic Design | UI/UX design system & specs (track-level; individual capability still unverified — see §2) |
| Arab Hammad | Cybersecurity | Cybersecurity track owner |

## 2. Individual Capability Profiles

### Lynn Sharbati — Backend
**Capability summary:** Completed Weeks 1–8 of an 8-week-evidenced, 10-week ASP.NET Core track; two full capstone sprints (Task/Project Management API, Cardiac Patient Monitoring API). Confirmed high-confidence skills: routing/middleware/DI, EF Core + SQL Server modeling and migrations, ASP.NET Core Identity + JWT + role/ownership authorization, xUnit/Moq/WebApplicationFactory testing, Redis cache-aside, composite indexing, Swagger/Postman documentation.
**Primary responsibilities:** Auth module, Trip module, Cost module, AI-Orchestration integration (the Gemini HTTP call), database schema, API documentation.
**Secondary responsibilities:** Performance tuning of hot endpoints; baseline security hardening (FluentValidation, rate limiting, CORS, headers).
**Owned components:** Backend API (all modules), SQL Server schema.
**Supporting components:** AI-Orchestration validation logic (co-owned with AI track).
**Dependencies:** AI track's finalized prompt/JSON schema (Phase 3/6); Flutter's agreed API contract expectations.
**Known gaps:** No CI/CD, no cloud hosting, no microservices/distributed-systems experience.
**Learning requirements:** Deployment/CI-CD basics before Phase 11.
**Confidence:** High (core backend) / Low (DevOps, distributed architecture).

### Dana Yaseen — Flutter / Mobile
**Capability summary:** Evidence window 29 Jul–7 Sep 2026. Progressed from Flutter/Dart fundamentals through a real Firebase-backed commerce app (Cat Cafe) with cart/checkout/orders/admin, plus an API-integrated project (Dio, Repository/DI pattern). Confirmed high-confidence skills: Flutter UI/widgets/responsive layout, Dart/OOP, Provider-based state management, REST integration (Dio/ApiService/JSON-to-model), Firebase Auth/Firestore, media upload (image_picker/Cloudinary), design-system reuse.
**Primary responsibilities:** All mobile screens/features, Flutter-Web build for the web platform requirement (ADR-02), client-side validation and state, design-system implementation.
**Secondary responsibilities:** Basic widget testing; mobile build/debug support.
**Owned components:** Flutter Client (mobile + web) — single owner, both platforms.
**Supporting components:** None outside the client.
**Dependencies:** Backend API contract (entities, endpoints, error format) at each integration checkpoint; AI JSON contract (Phase 8 per Dana's own execution plan); UI/UX designs from Rania once her capability/responsibilities are confirmed.
**Known gaps:** No production backend or CI/CD experience; testing beyond basic widget tests not demonstrated; large-scale offline sync/caching not demonstrated.
**Learning requirements:** None blocking MVP; testing depth is a post-MVP improvement area.
**Confidence:** High (Flutter/mobile delivery) / Medium (testing depth, offline scenarios).

### Aya Maali, Anas Musleh, Adam Alafandi — AI / ML
**Capability summary:** Weeks 1–8 of a 10-week AI/ML track: Python/Pandas/EDA foundations, classical supervised/unsupervised ML, model evaluation discipline, deep-learning architectures (CNN/RNN/Transformer) via a 3-sprint capstone. **Explicitly not covered:** Gemini/LLM API integration, prompt engineering, REST API consumption — confirmed directly by the track's own summary as the single most important planning gap for this project.
**Primary responsibilities:** Curate and maintain the internal destinations/places/pricing dataset; define AI-output schema and place-grounding validation (0% unsupported/invented places). Cost calculations and budget-feasibility checks use backend-computed values; the former AI cost-tolerance check was superseded by Contract v2.0.0.
**Secondary responsibilities:** Once upskilled, design prompt templates and the JSON output schema for Gemini (handed to Backend for live integration per ADR-01).
**Owned components:** Internal dataset; AI-output validation rules.
**Supporting components:** Prompt/schema design (co-owned with Backend for integration).
**Dependencies:** Must complete a focused Gemini/prompt-engineering upskilling module before Phase 6 (AI Integration) can start with real confidence.
**Known gaps:** No LLM/Gemini API training yet; no REST API consumption experience; no frontend/mobile/backend/cybersecurity exposure (not assumed).
**Learning requirements:** Gemini API + prompt engineering + structured/JSON-mode output design — **required before Section on AI Integration begins**, not assumed as already present.
**Confidence:** High (data engineering, evaluation/QA) / Low (LLM integration and prompt engineering, until upskilled).

### Rania Muhamid — UI/UX / Graphic Design
**Capability summary:** No training/capability document was provided for this track, so her individual capability remains unverified.
**Responsibilities (documented at track level from repository evidence, 2026-09-23):** the **UI/UX track owns the design system and screen specs**. Concretely, `09_DESIGN.md` ("Sunset Wanderer") is on record as the **authoritative visual system** (colors, typography, spacing, radii), the Flutter theme (`app_colors.dart`, `app_text_styles.dart`) implements its tokens, and `07_UI_PAGES.md` §13 maps the screens and states it covers. Accessibility requirements are defined in `08_SYSTEM_DESIGN.md` §37 and implemented by the Flutter/Mobile track. Because no capability document exists, this is deliberately scoped to **what already exists** — no *additional* UI/UX deliverables are assumed. The previous "TBD" is therefore resolved at track level using evidence (the shipped design system and its implementation), not assumption, per the rule in §7.

### Arab Hammad — Cybersecurity
**Capability summary:** Confirmed Cybersecurity track owner.
**Responsibilities:** Owns cybersecurity review, secrets-hygiene sign-off, and coordination of security work beyond the backend application-layer baseline. Backend remains responsible for implementing the ASP.NET Core controls it owns (auth, authorization, validation, rate limiting, CORS, and headers), with Arab Hammad accountable for cybersecurity review and follow-up.

## 3. Component Ownership Table

| Component | Primary Owner | Supporting | Track | Dependencies |
|---|---|---|---|---|
| Auth module | Lynn Sharbati | — | Backend | None |
| Trip module | Lynn Sharbati | — | Backend | AI JSON contract |
| Cost module | Lynn Sharbati | AI track (reference data) | Backend | Dataset |
| AI-Orchestration (Gemini call) | Lynn Sharbati | AI track (prompt/schema/validation logic) | Backend + AI | AI upskilling complete |
| Internal dataset | AI track | — | AI | None |
| AI-output validation rules | AI track | Backend (integration) | AI | Dataset |
| Flutter Client (mobile) | Dana Yaseen | — | Flutter | Backend + AI contracts |
| Flutter Client (web) | Dana Yaseen | — | Flutter | Same as mobile |
| Design system / visual design | UI/UX track (specs/tokens) | Flutter/Mobile (implementation) | UI/UX | `09_DESIGN.md` (authoritative) |
| Client accessibility (`08 §37`) | Flutter/Mobile | UI/UX track (design intent) | Flutter | `08_SYSTEM_DESIGN.md` §37 |
| Security hardening beyond app-layer | Arab Hammad | Lynn Sharbati (app-layer baseline) | Cybersecurity | Security review |
| Deployment / CI-CD | **TBD (shared learning need)** | Lynn Sharbati | Backend | Infra decision (D3 in SRS) |

Every critical component above has exactly one primary owner; where owner is TBD, no work should be assigned as if the capability already existed.

## 4. Track Responsibilities

**Backend (Lynn):** Owns the entire API service, DB schema, auth, cost logic, and the live Gemini integration call.
**AI/ML (Aya, Anas, Adam):** Owns dataset curation and AI-output validation now; owns prompt/schema design once upskilled.
**Flutter/Mobile (Dana):** Owns the entire client experience across mobile and web from one codebase.
**UI/UX:** Owns the design system and screen specs — `09_DESIGN.md` is the authoritative visual system. Individual track-member capability remains unverified (no capability document), so responsibility is documented only for the system that already exists; the Flutter/Mobile track owns its implementation and client accessibility.
**Cybersecurity (Arab Hammad):** confirmed Cybersecurity track owner; baseline app-layer security remains implemented by Backend with Cybersecurity review/sign-off.
**Integration / Cross-Team:** Shared — governed by the contracts in §5 below.

## 5. Cross-Team Contracts

| Boundary | Backend side | Other side | Data exchanged | Key risk |
|---|---|---|---|---|
| AI ↔ Backend | Executes Gemini call, applies validation rules provided by AI track | AI track supplies prompt templates + JSON schema + validation rule definitions | Structured JSON itinerary | Schema drift if not versioned |
| Backend ↔ Flutter | Exposes REST/JSON endpoints, stable entity IDs, error format | Flutter consumes via Dio/ApiService pattern already practiced | Trip/user/AI-result DTOs | Contract must be agreed before each integration checkpoint (Dana's own execution plan already requires this) |
| UI/UX ↔ Flutter | — | Design specs → Flutter implementation | Screens, design tokens (`09_DESIGN.md`) | Specs are on record; new UI/UX work beyond them is unstaffed until capability is confirmed |
| Cybersecurity ↔ Backend | Backend implements baseline controls it has evidenced | Arab Hammad reviews security gaps beyond the app-layer baseline | Security requirements | Cybersecurity owner confirmed |
| Cybersecurity ↔ Flutter | Arab Hammad reviews client-facing security expectations | Client reflects auth/permission state only; authoritative security stays server-side | Permission/session state | Cybersecurity owner confirmed |
| AI ↔ Security | Backend enforces access to the Gemini call itself | AI track has no security training; not assumed | API-key handling, rate limiting | Key handling responsibility defaults to Backend |

## 6. Dependency & Responsibility Matrix (condensed)
See §3 and §5 above; every dependency listed there is the authoritative reference. No dependency in this project is undocumented.

## 7. Ownership Rules
1. Every critical component has exactly one primary owner (§3).
2. "Everyone is responsible" is not used anywhere in this plan.
3. A responsibility is never assigned based on track label alone — only on evidenced capability (see profiles in §2).
4. Cybersecurity ownership is confirmed as Arab Hammad. For UI/UX, responsibility is documented at **track level** from the shipped design system and its Flutter implementation (§2), while the individual member's capability stays unverified.

## 8. Knowledge Gaps & Learning Requirements Summary
| Track | Gap | Required before |
|---|---|---|
| AI/ML | Gemini API + prompt engineering | Phase 6 — AI Integration |
| Backend | CI/CD, cloud hosting | Phase 11 — Deployment |
| Flutter | Deeper automated testing | Post-MVP hardening |
| Team-wide | UI/UX individual capability unknown | Any *new* UI/UX deliverable beyond the existing `09_DESIGN.md` system |
| Cybersecurity | Additional control scope must be reviewed by Arab Hammad | Any control beyond app-layer baseline |

## 9. Responsibility Assignment Rationale
Every assignment above traces to confirmed ownership or specific repository evidence rather than track labels or assumed norms. Where the evidence is still silent (new UI/UX work beyond the existing system), the plan says so explicitly rather than filling the gap with a plausible-sounding capability.
