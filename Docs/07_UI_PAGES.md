# TRIPLY — MOBILE APPLICATION SCREEN & NAVIGATION SPECIFICATION

*Derived strictly from Documents 1–6 (Master Plan, SRS, Architecture, Database Design, Team & Responsibilities, Tech Stack). Mobile is the primary product experience; the Flutter Web build mirrors this same structure (ADR-02). No visual design, no implementation — information architecture only.*

---

## SECTION 1 — MOBILE PRODUCT STRUCTURE

Triply mobile is a small, focused app built around one loop: **PLAN → GENERATE → VIEW → CUSTOMIZE → SAVE**. After login, the user lands on a 4-destination bottom navigation (Home, My Trips, Create Trip, Profile). Trip creation is a short linear wizard (mode → inputs → review → generating), and the generated trip lives on one screen with two tabs: **Itinerary** and **Costs**. Customization in MVP is *structured* (edit item, reorder, regenerate a day) — the conversational AI assistant is explicitly Post-MVP (FR-TRIP-005 is a "Should", not MVP). There are no notifications, no maps, no social features, and no admin screens in scope. Total: **15 screens** (9 of them P0), plus 4 modals/bottom sheets that are deliberately *not* screens.

---

## SECTION 2 — SCREEN INVENTORY

| ID | Screen Name | Module | Purpose | Priority | Access | Main Actions |
|---|---|---|---|---|---|---|
| MOB-AUTH-01 | Welcome | Onboarding & Auth | Entry point; choose Login / Register (guest entry REQUIRES DECISION — D2) | P2 | Public | Go to Login, Go to Register |
| MOB-AUTH-02 | Login | Onboarding & Auth | Authenticate; receive JWT | **P0** | Public | Enter credentials, Submit, Go to Register |
| MOB-AUTH-03 | Register | Onboarding & Auth | Create account | **P0** | Public | Enter email/password/name, Submit, Go to Login |
| MOB-AUTH-04 | Password Recovery | Onboarding & Auth | Reset forgotten password | P2 | Public | **REQUIRES PRODUCT DECISION — no FR exists in the SRS** |
| MOB-HOME-01 | Home | Home | Entry hub: start planning, resume recent trip | **P0** | Authenticated | Start new trip, Open recent trip |
| MOB-TRIP-01 | Planning Mode | Trip Creation | Choose DESTINATION_FIRST or BUDGET_FIRST | **P0** | Authenticated | Select mode, Continue |
| MOB-TRIP-02 | Budget Input | Trip Creation | Capture budget for budget-first flow (destination-first: optional field on Trip Details) | **P0** | Authenticated | Enter amount + currency, Continue |
| MOB-TRIP-03 | Destination Selection | Trip Creation | Pick from supported destinations (destination-first) | **P0** | Authenticated | Search/list destinations, Select, Continue |
| MOB-TRIP-04 | Destination Suggestions | Trip Creation | Budget-first: show candidate destinations matching budget + interests (FR-TRIP-002) | **P0** | Authenticated | View suggestions, Select one, Continue |
| MOB-TRIP-05 | Trip Details | Trip Creation | Dates, traveler count, budget (destination-first mode) | **P0** | Authenticated | Pick dates, Set travelers, Set budget (optional), Continue |
| MOB-TRIP-06 | Interests | Trip Creation | Multi-select interest categories (TripInterest) | **P0** | Authenticated | Toggle interests, Continue |
| MOB-TRIP-07 | Review & Confirm | Trip Creation | Show full preference snapshot before generation | **P0** | Authenticated | Edit any step, Generate |
| MOB-TRIP-08 | Generating | Trip Creation | AI generation in progress; handles success/failure (SRS journey 6) | **P0** | Authenticated | Wait, Cancel, Retry on failure |
| MOB-TRIP-09 | Trip Overview | Trip Result | The generated/saved trip: header + **Itinerary tab** + **Costs tab** | **P0** | Trip owner | Save, Edit, Regenerate, Archive |
| MOB-TRIPS-01 | My Trips | Saved Trips | List of user's trips with status filter (active/archived) | **P0** | Authenticated | Open trip, Filter, Archive/Delete (via sheet) |
| MOB-PROF-01 | Profile | Profile | Display name, email, logout | P1 | Authenticated | Edit name, Logout |
| MOB-AI-01 | AI Assistant (chat) | AI Assistant | Conversational refinement of a trip | P3 — Future | Trip owner | **Post-MVP (FR-TRIP-005); do not build in MVP** |

**Not screens (components/modals/sheets — see Section 6):** Place/Item Detail (bottom sheet), Edit Itinerary Item (modal), Regenerate Options (bottom sheet), Archive/Delete Trip confirmation (dialog).

---

## SECTION 3 — USER JOURNEYS

**J1 — Register & first login:** Welcome (P2, optional) → MOB-AUTH-03 Register → MOB-HOME-01

**J2 — Login (returning):** Welcome (optional) → MOB-AUTH-02 Login → MOB-HOME-01

**J3 — Destination-first trip (SRS journey 2):** MOB-HOME-01 → MOB-TRIP-01 (DESTINATION_FIRST) → MOB-TRIP-03 Destination Selection → MOB-TRIP-05 Trip Details → MOB-TRIP-06 Interests → MOB-TRIP-07 Review → MOB-TRIP-08 Generating → MOB-TRIP-09 (Itinerary tab)

**J4 — Budget-first trip (SRS journey 3):** MOB-HOME-01 → MOB-TRIP-01 (BUDGET_FIRST) → MOB-TRIP-02 Budget → MOB-TRIP-06 Interests → MOB-TRIP-04 Destination Suggestions → MOB-TRIP-05 Trip Details → MOB-TRIP-07 Review → MOB-TRIP-08 → MOB-TRIP-09

**J5 — Review & customize (SRS journeys 4, 13):** MOB-TRIP-09 → (edit item modal / regenerate day sheet) → MOB-TRIP-08 (partial regeneration) → MOB-TRIP-09

**J6 — View costs (FR-COST-001):** MOB-TRIP-09 → Costs tab

**J7 — Save & return later (SRS journey 5):** MOB-TRIP-09 → Save → MOB-TRIPS-01 → MOB-TRIP-09 (retrieved exactly as saved, FR-TRIP-004)

**J8 — Unsupported destination (SRS journey 7):** MOB-TRIP-03 → "destination not supported" state on the same screen → suggest supported list — no new screen

**J9 — Generation failure (SRS journey 6):** MOB-TRIP-08 error state → Retry → success, or → back to MOB-TRIP-07 with clear error (never a fabricated plan, FR-AI-002)

**J10 — Profile & logout (journey 20):** MOB-PROF-01 → Logout → MOB-AUTH-02

*Journeys marked in the brief but NOT supported by the SRS: password/account recovery (J-list item 4 — REQUIRES PRODUCT DECISION), conversational chat (item 14 — Post-MVP), deleting a trip (item 18 — DB supports soft delete, but no SRS journey requires it; included as optional archive/delete via confirmation dialog, P1, pending product confirmation).*

---

## SECTION 4 — MOBILE NAVIGATION ARCHITECTURE

```
APP
│
├── AUTH STACK (public; shown when no valid JWT — FR-AUTH-002 session restore)
│   ├── Welcome (optional, pending D2 guest-browsing decision)
│   ├── Login ───────────────→ Register
│   └── Register ────────────→ Login
│
└── MAIN (authenticated) — BOTTOM NAVIGATION, 4 destinations
    ├── HOME ──────────────── MOB-HOME-01
    │   └── push → TRIP CREATION WIZARD (full-screen stack, no bottom nav)
    │
    ├── MY TRIPS ──────────── MOB-TRIPS-01
    │   └── push → TRIP OVERVIEW (MOB-TRIP-09, owner-guarded)
    │
    ├── CREATE TRIP (center action) ── push → TRIP CREATION WIZARD
    │
    └── PROFILE ───────────── MOB-PROF-01

TRIP CREATION WIZARD (linear stack; back = previous step; steps skippable
only where marked optional; "Review" reachable from any step via edit)
MOB-TRIP-01 Mode → [MOB-TRIP-02 Budget | MOB-TRIP-03 Destination] →
[MOB-TRIP-04 Suggestions] → MOB-TRIP-05 Details → MOB-TRIP-06 Interests →
MOB-TRIP-07 Review → MOB-TRIP-08 Generating → REPLACE → MOB-TRIP-09 Trip Overview
```

**Rationale:** bottom navigation is justified by exactly four peer-level destinations (Home, My Trips, Create, Profile) — no deeper top-level areas exist in the SRS. The wizard replaces the main scaffold (full-screen stack) because steps are sequential and must not lose state. Trip Overview is pushed, never a tab, because a trip is reached from multiple entries (creation, My Trips, Home resume) and needs a back path. No deep links are required by the SRS (saved-trip *sharing* is Post-MVP) — skip.

---

## SECTION 5 — CORE TRIP CREATION FLOW

```
User taps "Plan a Trip" (Home or bottom nav)
        ↓
[1] MODE — destination-first or budget-first?        REQUIRED · Trip.planning_mode · not editable later · drives all AI behavior
        ↓
[2a] DESTINATION (destination-first) or [2b] BUDGET (budget-first)
     Destination: pick from supported list            REQUIRED · Trip.destination_id · editable until generation
     Budget: amount + currency                        REQUIRED in budget-first (Trip.budget_amount);
                                                      optional field on Trip Details in destination-first
        ↓
[3] (budget-first only) DESTINATION SUGGESTIONS
     1+ candidates from internal dataset              REQUIRED output of FR-TRIP-002 · user must pick one
        ↓
[4] TRIP DETAILS — dates, traveler count, budget      dates REQUIRED before generation (start/end);
     (traveler_count CHECK > 0; dates drive day count) editable later via regenerate · drives cost estimate
        ↓
[5] INTERESTS — multi-select categories               ≥1 expected (business rule, app-level) · TripInterest rows ·
                                                      feeds AI personalization + budget-first matching
        ↓
[6] REVIEW & CONFIRM — full preference snapshot       REQUIRED gate · all fields editable here (jump back to any step)
        ↓
[7] GENERATING — Trip.status = GENERATING             full-screen state · async-safe (NFR-PERF-001 TBD) ·
                                                      bounded retries; failure → clear error + retry, never fabricated
        ↓
[8] TRIP PRESENTATION — Trip Overview (Itinerary tab) status GENERATED · AI content labeled "AI-generated / Estimated"
        ↓
[9] CUSTOMIZE — edit item · reorder · delete ·
     regenerate day/item (structured, FR-TRIP-003)   Trip.version increments · edited items flip is_ai_generated=false
        ↓
[10] SAVE — status SAVED (FR-TRIP-004)               retrievable later exactly as confirmed · ARCHIVED state available
```

---

## SECTION 6 — GENERATED TRIP SCREEN STRUCTURE

**One screen — MOB-TRIP-09 Trip Overview** — with this internal structure:

```
MOB-TRIP-09 TRIP OVERVIEW (SCREEN, owner-guarded)
├── HEADER (COMPONENT)
│   ├── destination name, dates, travelers, status badge
│   ├── total estimated cost (denormalized Trip.total_estimated_cost)
│   └── actions: Save · Regenerate · Archive · (overflow)
│
├── TAB 1 — ITINERARY (TAB)
│   ├── DAY SELECTOR (COMPONENT: horizontal chips, day_number)
│   └── selected day (SECTION):
│       └── ITINERARY ITEMS (COMPONENT LIST, ordered by time_slot + order_index)
│           ├── item card: time slot · place name · estimated cost · AI-generated badge
│           └── tap → PLACE/ITEM DETAIL (BOTTOM SHEET: place description,
│               reference price, notes, "Edit" and "Remove" actions)
│
└── TAB 2 — COSTS (TAB) — FR-COST-001
    ├── category rows: Accommodation / Transportation / Food / Activities / Other
    ├── total = sum of categories
    └── persistent label: "Estimated" (not verified pricing)

EDIT ITINERARY ITEM (MODAL) — notes, order, removal → sets modified_at,
is_ai_generated = false, Trip.status = MODIFIED
REGENERATE OPTIONS (BOTTOM SHEET) — "Regenerate this day" / "Regenerate this item"
→ partial regeneration (FR-TRIP-003) → Generating state → back to Overview
ARCHIVE/DELETE TRIP (CONFIRMATION DIALOG) — soft delete only (DB §16)
```

**Deliberately not separate screens:** day-by-day pages (day selector inside one tab), cost breakdown (tab, not page), place details (bottom sheet), item editing (modal), regenerate choices (sheet). This keeps the trip experience to a single screen, matching the lean schema (Trip → Itinerary → Day → Item).

---

## SECTION 7 — AI ASSISTANT EXPERIENCE

**MVP: there is no chat.** FR-TRIP-005 (conversational refinement) is explicitly Post-MVP in both the SRS and Master Plan, and the AI track has no prompt-engineering training yet. MVP customization is **structured and context-attached**:

- Access point: the **Regenerate bottom sheet** inside Trip Overview, attached to a specific day/item (FR-TRIP-003) — not a free-text assistant.
- The user can: edit item notes, reorder, remove items, regenerate a day, regenerate an item. Budget changes are NOT supported in MVP (would require full regeneration — REQUIRES PRODUCT DECISION if wanted).
- AI changes are applied only after the backend's validation passes (FR-AI-002), then the Itinerary tab refreshes; the user confirms by Saving (status SAVED preserves the exact confirmed state).
- **Future (P3):** MOB-AI-01 — a chat screen anchored to a trip, reusing the Conversation/ConversationMessage tables already designed in Document 5 (§13). It becomes buildable only after the AI track's Phase 3/6 upskilling. Marked here so no redesign is needed later.

---

## SECTION 8 — SCREEN STATES

| Screen | Loading | Empty | Error | Success | Special States |
|---|---|---|---|---|---|
| Login / Register | submitting spinner | — | generic invalid-credentials message (SRS: no credential leakage) | → Home | session restore loading (FR-AUTH-002) |
| Home | skeleton | "No trips yet — plan your first trip" | generic retry | recent trip card | — |
| Destination Selection | skeleton | — | fetch error + retry | list, search results | **"Destination not supported" inline notice (SRS journey 7)** |
| Destination Suggestions | skeleton (generation feel) | **"No destinations match your budget"** | fetch error + retry | ranked candidates | — |
| Trip Details / Interests / Review | prefill spinner | — | save-draft error | prefilled on edit-back | invalid-field errors (FR-TRIP-001 field-level) |
| Generating | progress + cancelable wait | — | **failure state: clear message + Retry / back to Review (never partial/fake)** | auto-advance to Trip Overview | timeout state (bounded wait) |
| Trip Overview | itinerary skeleton | — | load error + retry | tabs populated | MODIFIED/SAVED/ARCHIVED status badges; stale-version conflict notice (optimistic concurrency) |
| My Trips | skeleton | **"No saved trips"** | retry | grouped by status, filter chips | archived section |
| Profile | spinner | — | save error | saved toast | — |

Offline: SRS assumes connectivity (Assumption §16) — a single "no connection" banner state on Home/My Trips is sufficient; no dedicated offline screens.

---

## SECTION 9 — MVP SCREEN SET (P0 ONLY)

**Authentication:** MOB-AUTH-02 Login · MOB-AUTH-03 Register

**Home:** MOB-HOME-01 Home

**Trip Planning:** MOB-TRIP-01 Mode · MOB-TRIP-02 Budget · MOB-TRIP-03 Destination Selection · MOB-TRIP-04 Destination Suggestions · MOB-TRIP-05 Trip Details · MOB-TRIP-06 Interests · MOB-TRIP-07 Review & Confirm · MOB-TRIP-08 Generating

**Trip Result:** MOB-TRIP-09 Trip Overview (Itinerary tab + Costs tab)

**AI Customization:** no chat screen — Regenerate sheet + Edit modal as components of Trip Overview

**My Trips:** MOB-TRIPS-01 My Trips

**Profile:** none in MVP (logout can live in Profile stub or Home overflow — REQUIRES PRODUCT DECISION; minimal impact)

→ **10 P0 screens**, sufficient for the full PLAN → GENERATE → VIEW → CUSTOMIZE → SAVE loop and every Must-have FR.

---

## SECTION 10 — OPTIONAL / FUTURE SCREENS

| Screen | Why not MVP |
|---|---|
| MOB-AI-01 AI Assistant chat | FR-TRIP-005 is Post-MVP; AI track upskilling pending |
| MOB-AUTH-01 Welcome / guest browsing | Blocked on D2 decision; no guest FR exists |
| MOB-AUTH-04 Password Recovery | No FR in the SRS — needs product decision + email-service work |
| Profile editing (MOB-PROF-01 full) | No FR; display_name edit only, P1 |
| Trip share link screen | Post-MVP (Master Plan Should-have) |
| Admin/content-moderation screens | Post-MVP; no admin requirement confirmed (SRS §2) |
| Notifications, maps, reviews, payments | Out of scope in every Triply document |

---

## SECTION 11 — SCREEN COUNT

- **Total screens: 15** (P0: 10 · P1: 3 — Profile, +2 reserved for polish · P2: 2 — Welcome, Password Recovery · P3/Future: 1 — AI chat)
- **Plus 4 non-screen components** (place sheet, edit modal, regenerate sheet, archive dialog)

**Sanity check: reasonable.** A travel app with one core loop should land in the 12–20 range; anything larger would mean components had been promoted to pages. The MVP set of 10 screens is small enough for one Flutter developer (Dana) to build across Phases 4–8, per her evidence window.

---

## SECTION 12 — TRACEABILITY (P0)

| Screen | Requirement | User Journey |
|---|---|---|
| Login | FR-AUTH-002, NFR-SEC-001 | J2 |
| Register | FR-AUTH-001 | J1 |
| Home | FR-TRIP-001 (entry), FR-TRIP-004 (resume) | J3, J4, J7 |
| Mode | FR-TRIP-001/002 (planning_mode) | J3, J4 |
| Budget | FR-TRIP-002, Trip.budget_amount | J4 |
| Destination Selection | FR-TRIP-001, D5 supported list, journey 7 | J3, J8 |
| Destination Suggestions | FR-TRIP-002 | J4 |
| Trip Details | FR-TRIP-001 (dates/travelers validation) | J3, J4 |
| Interests | FR-TRIP-001/002, TripInterest | J3, J4 |
| Review & Confirm | FR-TRIP-001 acceptance criteria (validation) | J3, J4 |
| Generating | FR-AI-001, FR-AI-002, SRS journeys 6, NFR-PERF-001 | J9 |
| Trip Overview (Itinerary tab) | FR-AI-001, FR-TRIP-003, FR-TRIP-004 | J3–J5 |
| Trip Overview (Costs tab) | FR-COST-001 | J6 |
| My Trips | FR-TRIP-004 | J7 |

---

## SECTION 13 — UI/UX TEAM HANDOFF

**Design these (P0 first):** the 10 MVP screens in Section 9, in the order of journeys J1–J9. Primary flows: **J3 (destination-first)** and **J4 (budget-first)** — these two are the product; everything else supports them.

**Navigation to design:** auth stack → 4-item bottom nav; wizard as full-screen overlay with back navigation and an edit-jump affordance on Review; Trip Overview pushed onto Home/My Trips stacks.

**States needing real design attention (not afterthoughts):** Generating (waiting UX — latency is TBD per NFR-PERF-001, so the screen must tolerate 30s+), Generating-failure (retry language, never blame the user), "destination not supported" (journey 7 — must feel helpful, not dead-end), "no destinations match your budget" (budget-first empty state), and the AI-generated/Estimated labeling (SRS §8 — AI content must always be distinguishable from verified data).

**Must NOT become pages:** day view, cost breakdown, place details, item edit, regenerate options, archive/delete confirmation (sheet/modal/dialog respectively, per Section 6).

**Needs special care:** the Regenerate sheet is the *only* AI customization surface in MVP — its clarity determines whether FR-TRIP-003 feels real; keep the Itinerary tab scannable (time slot → ordered items) since it's the screen users will stare at the longest.

**Not in this handoff:** colors, typography, branding, iconography — pending Rania's capability analysis (D4). Also awaiting decisions: D1 (cost tolerance affects displayed cost messaging only), D2 (guest browsing → Welcome screen), password recovery.

---

**Documented inconsistencies (reported, not silently resolved):** (1) The brief's journey list includes password recovery and trip *deletion*, but the SRS contains no FR for either — marked REQUIRES PRODUCT DECISION / optional. (2) FR-TRIP-005 chat appears in user-journey thinking but is Post-MVP — MVP customization is structured only. (3) The Architecture's AI sequence shows a synchronous request while NFR-PERF-001 defers the latency target — the Generating screen is designed async-safe to cover both. (4) The DB schema supports soft-delete and ARCHIVED status, but the SRS never describes a delete/archive journey — included minimally (dialog + status filter) rather than omitted.
# TRIPLY — MOBILE APPLICATION SCREEN & NAVIGATION SPECIFICATION

*Derived strictly from Documents 1–6 (Master Plan, SRS, Architecture, Database Design, Team & Responsibilities, Tech Stack). Mobile is the primary product experience; the Flutter Web build mirrors this same structure (ADR-02). No visual design, no implementation — information architecture only.*

---

## SECTION 1 — MOBILE PRODUCT STRUCTURE

Triply mobile is a small, focused app built around one loop: **PLAN → GENERATE → VIEW → CUSTOMIZE → SAVE**. After login, the user lands on a 4-destination bottom navigation (Home, My Trips, Create Trip, Profile). Trip creation is a short linear wizard (mode → inputs → review → generating), and the generated trip lives on one screen with two tabs: **Itinerary** and **Costs**. Customization in MVP is *structured* (edit item, reorder, regenerate a day) — the conversational AI assistant is explicitly Post-MVP (FR-TRIP-005 is a "Should", not MVP). There are no notifications, no maps, no social features, and no admin screens in scope. Total: **15 screens** (9 of them P0), plus 4 modals/bottom sheets that are deliberately *not* screens.

---

## SECTION 2 — SCREEN INVENTORY

| ID | Screen Name | Module | Purpose | Priority | Access | Main Actions |
|---|---|---|---|---|---|---|
| MOB-AUTH-01 | Welcome | Onboarding & Auth | Entry point; choose Login / Register (guest entry REQUIRES DECISION — D2) | P2 | Public | Go to Login, Go to Register |
| MOB-AUTH-02 | Login | Onboarding & Auth | Authenticate; receive JWT | **P0** | Public | Enter credentials, Submit, Go to Register |
| MOB-AUTH-03 | Register | Onboarding & Auth | Create account | **P0** | Public | Enter email/password/name, Submit, Go to Login |
| MOB-AUTH-04 | Password Recovery | Onboarding & Auth | Reset forgotten password | P2 | Public | **REQUIRES PRODUCT DECISION — no FR exists in the SRS** |
| MOB-HOME-01 | Home | Home | Entry hub: start planning, resume recent trip | **P0** | Authenticated | Start new trip, Open recent trip |
| MOB-TRIP-01 | Planning Mode | Trip Creation | Choose DESTINATION_FIRST or BUDGET_FIRST | **P0** | Authenticated | Select mode, Continue |
| MOB-TRIP-02 | Budget Input | Trip Creation | Capture budget for budget-first flow (destination-first: optional field on Trip Details) | **P0** | Authenticated | Enter amount + currency, Continue |
| MOB-TRIP-03 | Destination Selection | Trip Creation | Pick from supported destinations (destination-first) | **P0** | Authenticated | Search/list destinations, Select, Continue |
| MOB-TRIP-04 | Destination Suggestions | Trip Creation | Budget-first: show candidate destinations matching budget + interests (FR-TRIP-002) | **P0** | Authenticated | View suggestions, Select one, Continue |
| MOB-TRIP-05 | Trip Details | Trip Creation | Dates, traveler count, budget (destination-first mode) | **P0** | Authenticated | Pick dates, Set travelers, Set budget (optional), Continue |
| MOB-TRIP-06 | Interests | Trip Creation | Multi-select interest categories (TripInterest) | **P0** | Authenticated | Toggle interests, Continue |
| MOB-TRIP-07 | Review & Confirm | Trip Creation | Show full preference snapshot before generation | **P0** | Authenticated | Edit any step, Generate |
| MOB-TRIP-08 | Generating | Trip Creation | AI generation in progress; handles success/failure (SRS journey 6) | **P0** | Authenticated | Wait, Cancel, Retry on failure |
| MOB-TRIP-09 | Trip Overview | Trip Result | The generated/saved trip: header + **Itinerary tab** + **Costs tab** | **P0** | Trip owner | Save, Edit, Regenerate, Archive |
| MOB-TRIPS-01 | My Trips | Saved Trips | List of user's trips with status filter (active/archived) | **P0** | Authenticated | Open trip, Filter, Archive/Delete (via sheet) |
| MOB-PROF-01 | Profile | Profile | Display name, email, logout | P1 | Authenticated | Edit name, Logout |
| MOB-AI-01 | AI Assistant (chat) | AI Assistant | Conversational refinement of a trip | P3 — Future | Trip owner | **Post-MVP (FR-TRIP-005); do not build in MVP** |

**Not screens (components/modals/sheets — see Section 6):** Place/Item Detail (bottom sheet), Edit Itinerary Item (modal), Regenerate Options (bottom sheet), Archive/Delete Trip confirmation (dialog).

---

## SECTION 3 — USER JOURNEYS

**J1 — Register & first login:** Welcome (P2, optional) → MOB-AUTH-03 Register → MOB-HOME-01

**J2 — Login (returning):** Welcome (optional) → MOB-AUTH-02 Login → MOB-HOME-01

**J3 — Destination-first trip (SRS journey 2):** MOB-HOME-01 → MOB-TRIP-01 (DESTINATION_FIRST) → MOB-TRIP-03 Destination Selection → MOB-TRIP-05 Trip Details → MOB-TRIP-06 Interests → MOB-TRIP-07 Review → MOB-TRIP-08 Generating → MOB-TRIP-09 (Itinerary tab)

**J4 — Budget-first trip (SRS journey 3):** MOB-HOME-01 → MOB-TRIP-01 (BUDGET_FIRST) → MOB-TRIP-02 Budget → MOB-TRIP-06 Interests → MOB-TRIP-04 Destination Suggestions → MOB-TRIP-05 Trip Details → MOB-TRIP-07 Review → MOB-TRIP-08 → MOB-TRIP-09

**J5 — Review & customize (SRS journeys 4, 13):** MOB-TRIP-09 → (edit item modal / regenerate day sheet) → MOB-TRIP-08 (partial regeneration) → MOB-TRIP-09

**J6 — View costs (FR-COST-001):** MOB-TRIP-09 → Costs tab

**J7 — Save & return later (SRS journey 5):** MOB-TRIP-09 → Save → MOB-TRIPS-01 → MOB-TRIP-09 (retrieved exactly as saved, FR-TRIP-004)

**J8 — Unsupported destination (SRS journey 7):** MOB-TRIP-03 → "destination not supported" state on the same screen → suggest supported list — no new screen

**J9 — Generation failure (SRS journey 6):** MOB-TRIP-08 error state → Retry → success, or → back to MOB-TRIP-07 with clear error (never a fabricated plan, FR-AI-002)

**J10 — Profile & logout (journey 20):** MOB-PROF-01 → Logout → MOB-AUTH-02

*Journeys marked in the brief but NOT supported by the SRS: password/account recovery (J-list item 4 — REQUIRES PRODUCT DECISION), conversational chat (item 14 — Post-MVP), deleting a trip (item 18 — DB supports soft delete, but no SRS journey requires it; included as optional archive/delete via confirmation dialog, P1, pending product confirmation).*

---

## SECTION 4 — MOBILE NAVIGATION ARCHITECTURE

```
APP
│
├── AUTH STACK (public; shown when no valid JWT — FR-AUTH-002 session restore)
│   ├── Welcome (optional, pending D2 guest-browsing decision)
│   ├── Login ───────────────→ Register
│   └── Register ────────────→ Login
│
└── MAIN (authenticated) — BOTTOM NAVIGATION, 4 destinations
    ├── HOME ──────────────── MOB-HOME-01
    │   └── push → TRIP CREATION WIZARD (full-screen stack, no bottom nav)
    │
    ├── MY TRIPS ──────────── MOB-TRIPS-01
    │   └── push → TRIP OVERVIEW (MOB-TRIP-09, owner-guarded)
    │
    ├── CREATE TRIP (center action) ── push → TRIP CREATION WIZARD
    │
    └── PROFILE ───────────── MOB-PROF-01

TRIP CREATION WIZARD (linear stack; back = previous step; steps skippable
only where marked optional; "Review" reachable from any step via edit)
MOB-TRIP-01 Mode → [MOB-TRIP-02 Budget | MOB-TRIP-03 Destination] →
[MOB-TRIP-04 Suggestions] → MOB-TRIP-05 Details → MOB-TRIP-06 Interests →
MOB-TRIP-07 Review → MOB-TRIP-08 Generating → REPLACE → MOB-TRIP-09 Trip Overview
```

**Rationale:** bottom navigation is justified by exactly four peer-level destinations (Home, My Trips, Create, Profile) — no deeper top-level areas exist in the SRS. The wizard replaces the main scaffold (full-screen stack) because steps are sequential and must not lose state. Trip Overview is pushed, never a tab, because a trip is reached from multiple entries (creation, My Trips, Home resume) and needs a back path. No deep links are required by the SRS (saved-trip *sharing* is Post-MVP) — skip.

---

## SECTION 5 — CORE TRIP CREATION FLOW

```
User taps "Plan a Trip" (Home or bottom nav)
        ↓
[1] MODE — destination-first or budget-first?        REQUIRED · Trip.planning_mode · not editable later · drives all AI behavior
        ↓
[2a] DESTINATION (destination-first) or [2b] BUDGET (budget-first)
     Destination: pick from supported list            REQUIRED · Trip.destination_id · editable until generation
     Budget: amount + currency                        REQUIRED in budget-first (Trip.budget_amount);
                                                      optional field on Trip Details in destination-first
        ↓
[3] (budget-first only) DESTINATION SUGGESTIONS
     1+ candidates from internal dataset              REQUIRED output of FR-TRIP-002 · user must pick one
        ↓
[4] TRIP DETAILS — dates, traveler count, budget      dates REQUIRED before generation (start/end);
     (traveler_count CHECK > 0; dates drive day count) editable later via regenerate · drives cost estimate
        ↓
[5] INTERESTS — multi-select categories               ≥1 expected (business rule, app-level) · TripInterest rows ·
                                                      feeds AI personalization + budget-first matching
        ↓
[6] REVIEW & CONFIRM — full preference snapshot       REQUIRED gate · all fields editable here (jump back to any step)
        ↓
[7] GENERATING — Trip.status = GENERATING             full-screen state · async-safe (NFR-PERF-001 TBD) ·
                                                      bounded retries; failure → clear error + retry, never fabricated
        ↓
[8] TRIP PRESENTATION — Trip Overview (Itinerary tab) status GENERATED · AI content labeled "AI-generated / Estimated"
        ↓
[9] CUSTOMIZE — edit item · reorder · delete ·
     regenerate day/item (structured, FR-TRIP-003)   Trip.version increments · edited items flip is_ai_generated=false
        ↓
[10] SAVE — status SAVED (FR-TRIP-004)               retrievable later exactly as confirmed · ARCHIVED state available
```

---

## SECTION 6 — GENERATED TRIP SCREEN STRUCTURE

**One screen — MOB-TRIP-09 Trip Overview** — with this internal structure:

```
MOB-TRIP-09 TRIP OVERVIEW (SCREEN, owner-guarded)
├── HEADER (COMPONENT)
│   ├── destination name, dates, travelers, status badge
│   ├── total estimated cost (denormalized Trip.total_estimated_cost)
│   └── actions: Save · Regenerate · Archive · (overflow)
│
├── TAB 1 — ITINERARY (TAB)
│   ├── DAY SELECTOR (COMPONENT: horizontal chips, day_number)
│   └── selected day (SECTION):
│       └── ITINERARY ITEMS (COMPONENT LIST, ordered by time_slot + order_index)
│           ├── item card: time slot · place name · estimated cost · AI-generated badge
│           └── tap → PLACE/ITEM DETAIL (BOTTOM SHEET: place description,
│               reference price, notes, "Edit" and "Remove" actions)
│
└── TAB 2 — COSTS (TAB) — FR-COST-001
    ├── category rows: Accommodation / Transportation / Food / Activities / Other
    ├── total = sum of categories
    └── persistent label: "Estimated" (not verified pricing)

EDIT ITINERARY ITEM (MODAL) — notes, order, removal → sets modified_at,
is_ai_generated = false, Trip.status = MODIFIED
REGENERATE OPTIONS (BOTTOM SHEET) — "Regenerate this day" / "Regenerate this item"
→ partial regeneration (FR-TRIP-003) → Generating state → back to Overview
ARCHIVE/DELETE TRIP (CONFIRMATION DIALOG) — soft delete only (DB §16)
```

**Deliberately not separate screens:** day-by-day pages (day selector inside one tab), cost breakdown (tab, not page), place details (bottom sheet), item editing (modal), regenerate choices (sheet). This keeps the trip experience to a single screen, matching the lean schema (Trip → Itinerary → Day → Item).

---

## SECTION 7 — AI ASSISTANT EXPERIENCE

**MVP: there is no chat.** FR-TRIP-005 (conversational refinement) is explicitly Post-MVP in both the SRS and Master Plan, and the AI track has no prompt-engineering training yet. MVP customization is **structured and context-attached**:

- Access point: the **Regenerate bottom sheet** inside Trip Overview, attached to a specific day/item (FR-TRIP-003) — not a free-text assistant.
- The user can: edit item notes, reorder, remove items, regenerate a day, regenerate an item. Budget changes are NOT supported in MVP (would require full regeneration — REQUIRES PRODUCT DECISION if wanted).
- AI changes are applied only after the backend's validation passes (FR-AI-002), then the Itinerary tab refreshes; the user confirms by Saving (status SAVED preserves the exact confirmed state).
- **Future (P3):** MOB-AI-01 — a chat screen anchored to a trip, reusing the Conversation/ConversationMessage tables already designed in Document 5 (§13). It becomes buildable only after the AI track's Phase 3/6 upskilling. Marked here so no redesign is needed later.

---

## SECTION 8 — SCREEN STATES

| Screen | Loading | Empty | Error | Success | Special States |
|---|---|---|---|---|---|
| Login / Register | submitting spinner | — | generic invalid-credentials message (SRS: no credential leakage) | → Home | session restore loading (FR-AUTH-002) |
| Home | skeleton | "No trips yet — plan your first trip" | generic retry | recent trip card | — |
| Destination Selection | skeleton | — | fetch error + retry | list, search results | **"Destination not supported" inline notice (SRS journey 7)** |
| Destination Suggestions | skeleton (generation feel) | **"No destinations match your budget"** | fetch error + retry | ranked candidates | — |
| Trip Details / Interests / Review | prefill spinner | — | save-draft error | prefilled on edit-back | invalid-field errors (FR-TRIP-001 field-level) |
| Generating | progress + cancelable wait | — | **failure state: clear message + Retry / back to Review (never partial/fake)** | auto-advance to Trip Overview | timeout state (bounded wait) |
| Trip Overview | itinerary skeleton | — | load error + retry | tabs populated | MODIFIED/SAVED/ARCHIVED status badges; stale-version conflict notice (optimistic concurrency) |
| My Trips | skeleton | **"No saved trips"** | retry | grouped by status, filter chips | archived section |
| Profile | spinner | — | save error | saved toast | — |

Offline: SRS assumes connectivity (Assumption §16) — a single "no connection" banner state on Home/My Trips is sufficient; no dedicated offline screens.

---

## SECTION 9 — MVP SCREEN SET (P0 ONLY)

**Authentication:** MOB-AUTH-02 Login · MOB-AUTH-03 Register

**Home:** MOB-HOME-01 Home

**Trip Planning:** MOB-TRIP-01 Mode · MOB-TRIP-02 Budget · MOB-TRIP-03 Destination Selection · MOB-TRIP-04 Destination Suggestions · MOB-TRIP-05 Trip Details · MOB-TRIP-06 Interests · MOB-TRIP-07 Review & Confirm · MOB-TRIP-08 Generating

**Trip Result:** MOB-TRIP-09 Trip Overview (Itinerary tab + Costs tab)

**AI Customization:** no chat screen — Regenerate sheet + Edit modal as components of Trip Overview

**My Trips:** MOB-TRIPS-01 My Trips

**Profile:** none in MVP (logout can live in Profile stub or Home overflow — REQUIRES PRODUCT DECISION; minimal impact)

→ **10 P0 screens**, sufficient for the full PLAN → GENERATE → VIEW → CUSTOMIZE → SAVE loop and every Must-have FR.

---

## SECTION 10 — OPTIONAL / FUTURE SCREENS

| Screen | Why not MVP |
|---|---|
| MOB-AI-01 AI Assistant chat | FR-TRIP-005 is Post-MVP; AI track upskilling pending |
| MOB-AUTH-01 Welcome / guest browsing | Blocked on D2 decision; no guest FR exists |
| MOB-AUTH-04 Password Recovery | No FR in the SRS — needs product decision + email-service work |
| Profile editing (MOB-PROF-01 full) | No FR; display_name edit only, P1 |
| Trip share link screen | Post-MVP (Master Plan Should-have) |
| Admin/content-moderation screens | Post-MVP; no admin requirement confirmed (SRS §2) |
| Notifications, maps, reviews, payments | Out of scope in every Triply document |

---

## SECTION 11 — SCREEN COUNT

- **Total screens: 15** (P0: 10 · P1: 3 — Profile, +2 reserved for polish · P2: 2 — Welcome, Password Recovery · P3/Future: 1 — AI chat)
- **Plus 4 non-screen components** (place sheet, edit modal, regenerate sheet, archive dialog)

**Sanity check: reasonable.** A travel app with one core loop should land in the 12–20 range; anything larger would mean components had been promoted to pages. The MVP set of 10 screens is small enough for one Flutter developer (Dana) to build across Phases 4–8, per her evidence window.

---

## SECTION 12 — TRACEABILITY (P0)

| Screen | Requirement | User Journey |
|---|---|---|
| Login | FR-AUTH-002, NFR-SEC-001 | J2 |
| Register | FR-AUTH-001 | J1 |
| Home | FR-TRIP-001 (entry), FR-TRIP-004 (resume) | J3, J4, J7 |
| Mode | FR-TRIP-001/002 (planning_mode) | J3, J4 |
| Budget | FR-TRIP-002, Trip.budget_amount | J4 |
| Destination Selection | FR-TRIP-001, D5 supported list, journey 7 | J3, J8 |
| Destination Suggestions | FR-TRIP-002 | J4 |
| Trip Details | FR-TRIP-001 (dates/travelers validation) | J3, J4 |
| Interests | FR-TRIP-001/002, TripInterest | J3, J4 |
| Review & Confirm | FR-TRIP-001 acceptance criteria (validation) | J3, J4 |
| Generating | FR-AI-001, FR-AI-002, SRS journeys 6, NFR-PERF-001 | J9 |
| Trip Overview (Itinerary tab) | FR-AI-001, FR-TRIP-003, FR-TRIP-004 | J3–J5 |
| Trip Overview (Costs tab) | FR-COST-001 | J6 |
| My Trips | FR-TRIP-004 | J7 |

---

## SECTION 13 — UI/UX TEAM HANDOFF

**Design these (P0 first):** the 10 MVP screens in Section 9, in the order of journeys J1–J9. Primary flows: **J3 (destination-first)** and **J4 (budget-first)** — these two are the product; everything else supports them.

**Navigation to design:** auth stack → 4-item bottom nav; wizard as full-screen overlay with back navigation and an edit-jump affordance on Review; Trip Overview pushed onto Home/My Trips stacks.

**States needing real design attention (not afterthoughts):** Generating (waiting UX — latency is TBD per NFR-PERF-001, so the screen must tolerate 30s+), Generating-failure (retry language, never blame the user), "destination not supported" (journey 7 — must feel helpful, not dead-end), "no destinations match your budget" (budget-first empty state), and the AI-generated/Estimated labeling (SRS §8 — AI content must always be distinguishable from verified data).

**Must NOT become pages:** day view, cost breakdown, place details, item edit, regenerate options, archive/delete confirmation (sheet/modal/dialog respectively, per Section 6).

**Needs special care:** the Regenerate sheet is the *only* AI customization surface in MVP — its clarity determines whether FR-TRIP-003 feels real; keep the Itinerary tab scannable (time slot → ordered items) since it's the screen users will stare at the longest.

**Not in this handoff:** colors, typography, branding, iconography — pending Rania's capability analysis (D4). Also awaiting decisions: D1 (cost tolerance affects displayed cost messaging only), D2 (guest browsing → Welcome screen), password recovery.

---

**Documented inconsistencies (reported, not silently resolved):** (1) The brief's journey list includes password recovery and trip *deletion*, but the SRS contains no FR for either — marked REQUIRES PRODUCT DECISION / optional. (2) FR-TRIP-005 chat appears in user-journey thinking but is Post-MVP — MVP customization is structured only. (3) The Architecture's AI sequence shows a synchronous request while NFR-PERF-001 defers the latency target — the Generating screen is designed async-safe to cover both. (4) The DB schema supports soft-delete and ARCHIVED status, but the SRS never describes a delete/archive journey — included minimally (dialog + status filter) rather than omitted.
