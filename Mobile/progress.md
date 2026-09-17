# Triply Mobile — Progress

*Owner: Dana Yaseen · Track: Flutter/Mobile · Last updated: 2026-09-17*

This document tracks what exists in the Flutter app today: architecture, every screen, the shared component library, and — most importantly — which features are wired to the real backend versus still running on mock data.

---

## 1. Architecture

- **Pattern**: Provider (state) → Repository (data access) → UI (screens/widgets). Screens never talk to the network directly; they read a `Provider` and the `Provider` calls a `Repository`.
- **Networking**: `lib/core/network/api_client.dart` is the only class allowed to touch Dio directly. It resolves the backend base URL per platform (`10.0.2.2:8080` for the Android emulator, `localhost:8080` for iOS simulator/desktop) and translates transport errors into a single `ApiException`.
- **Auth propagation**: `lib/core/network/auth_interceptor.dart` reads the JWT from `lib/core/storage/token_storage.dart` (secure storage) and attaches it to every outgoing request automatically. No screen or repository has to think about the token.
- **Mock ↔ Real swap**: every feature has an abstract `XyzRepository` interface, a `MockXyzRepository`, and (where connected) an `ApiXyzRepository`. Swapping one for the other never touches the UI layer — proven by the architecture smoke test in `test/widget_test.dart`.

---

## 2. Screens

| # | Screen | File | Status |
|---|---|---|---|
| 1 | Splash | `lib/presentation/screens/splash_screen.dart` | Built |
| 2 | Onboarding | `lib/presentation/screens/onboarding/` | Built |
| 3 | Login | `lib/presentation/widgets/auth/login_screen.dart` | Built + **connected to real backend** |
| 4 | Register | `lib/presentation/widgets/auth/register_screen.dart` | Built + **connected to real backend** |
| 5 | Home | `lib/presentation/screens/home/home_screen.dart` | Built — **mock data** (`MockHomeRepository`) |
| 6 | Planning Mode | `lib/presentation/screens/trip_creation/planning_mode_screen.dart` | Built — local state only, no backend needed |
| 7 | Budget/Destination | `lib/presentation/screens/trip_creation/budget_destination_screen.dart` | Built — Budget-first **real**; Destination-first still **mock** (see §4) |
| 8 | Destination Suggestions | `lib/presentation/screens/trip_creation/destination_suggestions_screen.dart` | Built + **connected to real backend** (budget-first path) |
| 9 | Trip Details | `lib/presentation/screens/trip_creation/trip_details_screen.dart` | Built — local state, sent to backend on submit |
| 10 | Interests | `lib/presentation/screens/trip_creation/interests_screen.dart` | Built — values mapped to real backend IDs |
| 11 | Review | `lib/presentation/screens/trip_creation/review_screen.dart` | Built + **connected to real backend** (triggers trip creation) |
| 12 | Generating | `lib/presentation/screens/trip_creation/generating_screen.dart` | Built + **connected to real backend** (create trip + start generation) |
| 13 | Trip Overview — Itinerary tab | `lib/presentation/screens/trip_overview/trip_overview_screen.dart` | Built — **mock data** (`MockTripOverviewRepository`) |
| 14 | Trip Overview — Costs & Split tab | same file | Built — **mock data**, matched against the supplied Figma reference |

**Not yet built**: My Trips list, Profile, AI Assistant chat (Post-MVP per SRS), Welcome/guest screen, Password Recovery.

---

## 3. Shared Component Library (`lib/presentation/widgets/`)

One implementation each, reused everywhere: `AppScaffold`, `PrimaryButton`, `SecondaryButton`, `AppTextField`, `SelectionCard`, `InterestChip`, `DestinationCard`, `TripCard`, `DaySelector`, `ItineraryItemCard`, `CostCategoryRow`, `StatusBadge`, `AIGeneratedBadge`, `EmptyState`, `ErrorState`, `LoadingSkeleton`, `AppBottomSheet`, `ConfirmationDialog`, `AppBottomNavigation`.

**Gap**: none of these have a dedicated widget test yet (only the architecture smoke test exists).

---

## 4. Backend Integration Status

### ✅ Fully connected (real HTTP calls, tested against the live API)

| Feature | Endpoint(s) | Repository |
|---|---|---|
| Register | `POST /api/auth/register` | `ApiAuthRepository` |
| Login | `POST /api/auth/login` | `ApiAuthRepository` |
| Destination suggestions (budget-first) | `POST /api/destinations/suggestions` | `ApiTripCreationRepository` |
| Create trip | `POST /api/trips` | `ApiTripCreationRepository` |
| Start AI generation | `POST /api/trips/{id}/generate` | `ApiTripCreationRepository` |

### 🟡 Still mock (deliberately, pending backend work)

| Feature | Why | Repository |
|---|---|---|
| Home screen (recent trip, active trip) | No task done yet to wire `GET /api/trips` into Home | `MockHomeRepository` |
| Trip Overview (Itinerary + Costs tabs) | Needs `GET /api/trips/{id}` (exists) *and* real AI-generated itinerary content (doesn't exist yet — see below) | `MockTripOverviewRepository` |
| Destination Selection (destination-first) | Backend has no "list all supported destinations" endpoint yet | hardcoded list in `budget_destination_screen.dart` |

### ⛔ Known backend gaps (need a teammate to build, listed by priority)

1. **`GET /api/destinations`** — plain list of supported destinations. Blocks the destination-first path entirely; only Jerusalem and Amman exist as real seeded destinations today.
2. **Real Gemini itinerary generation** — `POST /api/trips/{id}/generate` currently only flips the trip's status to `GENERATING`; it does not produce actual day/item content. This blocks Trip Overview from ever showing a *real* generated itinerary.
3. *(Nice to have)* `AuthResponse` doesn't return the user's display name — mobile currently derives a placeholder from the email's local part.
4. *(Nice to have)* `DestinationSuggestionResponse` has no image/description field, so suggestion cards show a generic blurb and a placeholder icon instead of a real photo.

---

## 5. Notable Fixes Made Along the Way

- **Android `INTERNET` permission was missing entirely** from `AndroidManifest.xml` — silently broke every network call and the debug VM service. Fixed.
- **Wizard step order bug**: budget-first flow requested destination suggestions *before* the user picked interests, and the backend rejects an empty `interestCategoryIds` list. Reordered so Interests comes before Suggestions for that path only.
- **Interests mismatch**: mobile had "Nightlife" as an option; backend's fixed reference list has no such category (only `OTHER`). Fixed to match exactly.
- **Register password validation** was weaker than the backend's actual policy (needs uppercase + digit + symbol) — users could fill the whole form and still get rejected. Client-side check now mirrors the server rule.
- **Home's "Plan Your First Trip" and "Resume Trip Itinerary" buttons had no `onPressed` wired at all** — fixed; "Resume" now correctly opens Trip Overview instead of restarting the creation wizard.

---

## 6. Git Status

Work is split across two feature branches (pushed, **not merged into `main`**):

- `Mobile/feat/backend-integration` — auth wiring, trip creation backend integration, Generating screen.
- `Mobile/feat/trip-overview` — Trip Overview screen, shared bottom nav, Home button fixes.

**Known pre-existing issue on `main`**: `main.dart` references `data/repositories/api_auth_repository.dart`, which does not actually exist on `main` — pulling `main` fresh today does not compile. Not caused by the above branches; flagged to the team separately.
