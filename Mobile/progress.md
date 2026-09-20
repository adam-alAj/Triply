# Triply Mobile — Progress

*Owner: Dana Yaseen · Track: Flutter/Mobile · Last updated: 2026-09-19*

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
| 1 | Splash | `lib/presentation/screens/splash_screen.dart` | Built + **connected to real backend** (session restore via `GET /api/users/me`) |
| 2 | Onboarding | `lib/presentation/screens/onboarding/` | Built |
| 3 | Login | `lib/presentation/widgets/auth/login_screen.dart` | Built + **connected to real backend** |
| 4 | Register | `lib/presentation/widgets/auth/register_screen.dart` | Built + **connected to real backend** |
| 5 | Home | `lib/presentation/screens/home/home_screen.dart` | Built + **connected to real backend** (`ApiHomeRepository`); real logged-in user name in greeting (no longer hardcoded "Alex") |
| 6 | Planning Mode | `lib/presentation/screens/trip_creation/planning_mode_screen.dart` | Built — local state only, no backend needed |
| 7 | Budget/Destination | `lib/presentation/screens/trip_creation/budget_destination_screen.dart` | Built + **connected to real backend** — both Budget-first and Destination-first now use `GET /api/destinations` / suggestions (destination-first mock list removed) |
| 8 | Destination Suggestions | `lib/presentation/screens/trip_creation/destination_suggestions_screen.dart` | Built + **connected to real backend** (budget-first path) |
| 9 | Trip Details | `lib/presentation/screens/trip_creation/trip_details_screen.dart` | Built — local state, sent to backend on submit |
| 10 | Interests | `lib/presentation/screens/trip_creation/interests_screen.dart` | Built — values mapped to real backend IDs |
| 11 | Review | `lib/presentation/screens/trip_creation/review_screen.dart` | Built + **connected to real backend** (triggers trip creation) |
| 12 | Generating | `lib/presentation/screens/trip_creation/generating_screen.dart` | Built + **connected to real backend** (create trip + start generation) |
| 13 | My Trips | `lib/presentation/screens/my_trips/my_trips_screen.dart` | Built + **connected to real backend**; surfaces `Trip.Title` / `Trip.CoverImageUrl` |
| 14 | Profile | `lib/presentation/screens/profile/profile_screen.dart` | Built + **connected to real backend** — `GET`/`PATCH /api/users/me`, `GET`/`PUT /api/users/me/preferences`, `GET /api/users/me/stats` (replaces old local-only `ProfileLocalStorage`) |
| 15 | Trip Overview — Itinerary tab | `lib/presentation/screens/trip_overview/trip_overview_screen.dart` | Built + **connected to real backend** (`ApiTripOverviewRepository`); item edit via `PATCH .../itinerary/items/{itemId}`, partial regen via `POST .../generate?scope=DAY|ITEM`; Save action added (was Archive-only, 409s masked the real backend reason) |
| 16 | Trip Overview — Costs & Split tab | same file | Built + **connected to real backend** |
| 17 | Place Detail Sheet | `lib/presentation/widgets/trip_overview/place_detail_sheet.dart` | Built + **connected to real backend** (`GET /api/places/{id}`) |

**Not yet built**: AI Assistant chat (Post-MVP per SRS), Welcome/guest screen, Password Recovery.

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
| Session restore / display-name edit | `GET`/`PATCH /api/users/me` | `ApiAuthRepository` |
| User preferences | `GET`/`PUT /api/users/me/preferences` | `ApiUserSettingsRepository` |
| User stats | `GET /api/users/me/stats` | `ApiUserSettingsRepository` |
| Place details | `GET /api/places/{id}` | (used by Place Detail Sheet) |
| Destinations list (destination-first) | `GET /api/destinations` | `ApiTripCreationRepository` |
| Destination suggestions (budget-first) | `POST /api/destinations/suggestions` | `ApiTripCreationRepository` |
| Create trip | `POST /api/trips` | `ApiTripCreationRepository` |
| Start AI generation | `POST /api/trips/{id}/generate` | `ApiTripCreationRepository` |
| Home screen (recent trip, active trip) | `GET /api/trips` | `ApiHomeRepository` |
| My Trips list | `GET /api/trips` | `ApiHomeRepository` |
| Trip Overview (Itinerary + Costs tabs) | `GET /api/trips/{id}` | `ApiTripOverviewRepository` |
| Itinerary item edit | `PATCH /api/trips/{tripId}/itinerary/items/{itemId}` | `ApiTripOverviewRepository` |
| Partial regeneration (day/item scope) | `POST /api/trips/{id}/generate?scope=DAY\|ITEM` | `ApiTripOverviewRepository` |
| Trip Save action | (Trip lifecycle endpoint) | `ApiTripOverviewRepository` |

### 🟡 Still mock / not wired

*(none currently known — every screen listed in §2 is on a real repository as of 2026-09-19; re-check this list as new screens are added)*

### ⛔ Known backend gaps (need a teammate to build, listed by priority)

1. **Real Gemini itinerary generation** — `POST /api/trips/{id}/generate` still needs full end-to-end verification against the live Gemini service (per `Backend/progress.md`); worth re-confirming Trip Overview shows genuinely AI-generated content, not placeholder data.
2. *(Nice to have)* `DestinationSuggestionResponse` has no image/description field, so suggestion cards show a generic blurb and a placeholder icon instead of a real photo.
3. **Backend CI is currently red** — see §7 below. Doesn't block the endpoints mobile already consumes, but any new backend change should be checked against this before relying on it.

~~`GET /api/destinations`~~ — resolved, see ✅ table above.
~~`AuthResponse`/`GET /api/users/me` doesn't return display name~~ — resolved, see ✅ table above.

---

## 5. Notable Fixes Made Along the Way

- **Android `INTERNET` permission was missing entirely** from `AndroidManifest.xml` — silently broke every network call and the debug VM service. Fixed.
- **Wizard step order bug**: budget-first flow requested destination suggestions *before* the user picked interests, and the backend rejects an empty `interestCategoryIds` list. Reordered so Interests comes before Suggestions for that path only.
- **Interests mismatch**: mobile had "Nightlife" as an option; backend's fixed reference list has no such category (only `OTHER`). Fixed to match exactly.
- **Register password validation** was weaker than the backend's actual policy (needs uppercase + digit + symbol) — users could fill the whole form and still get rejected. Client-side check now mirrors the server rule.
- **Home's "Plan Your First Trip" and "Resume Trip Itinerary" buttons had no `onPressed` wired at all** — fixed; "Resume" now correctly opens Trip Overview instead of restarting the creation wizard.

---

## 6. Git Status

Currently on branch `web`. Recent mobile work has landed:

- PR [#48](https://github.com/adam-alAj/Triply/pull/48) `feature/mobile-profile-endpoints` — backend profile/preferences/stats/place-details/itinerary-item-PATCH/partial-regen/trip-metadata endpoints — **merged into `main`**.
- Commit `6e24ee3` "wire newly-added backend endpoints across profile, trip overview" — Profile, Place Detail Sheet, itinerary item edit, partial regen, My Trips/Trip Overview title+cover image.
- Commit `3fa0105` "wire Save action and real user name on Home" — Home greeting now uses the real logged-in user; Trip Overview Save action added.
- PR [#49](https://github.com/adam-alAj/Triply/pull/49) `Mobile/feat/update-navigation` (bundles the two commits above) — **merged into `main`**.
- PR [#50](https://github.com/adam-alAj/Triply/pull/50) `feature/mobile-profile-endpoints` — **merged into `main`**.
- PR [#51](https://github.com/adam-alAj/Triply/pull/51) `web` — **merged into `main`**.

**Previously flagged `main` compile issue (missing `api_auth_repository.dart`) — resolved**; the file exists and is in active use (see §4).

---

## 7. CI Status (checked 2026-09-19)

**Backend CI (`Backend CI / build-and-test`) is currently failing** on the latest merges into `main` (confirmed on PR #49's merge commit and reproduced again as "All checks have failed — 1 failing check" on a later commit touching `AI/03-Validation/`).

- Result: `Process completed with exit code 1` (1 error, 11 warnings, 1 notice).
- Warnings are nullable-reference-type mismatches in:
  - `Backend/Triply.Api.Tests/ItineraryIntegrationTests.cs:110,117`
  - `Backend/Triply.Api.Tests/UserIdentityTests.cs:31` (repeated ×7)
  - `Backend/Triply.Api/Modules/AI-Orchestration/ItineraryValidationService.cs:191`
- The exact failing assertion/test behind the exit-code-1 error wasn't visible without a signed-in GitHub session — needs a teammate with repo access to open the run logs and confirm root cause.
- **Not a merge conflict** — all listed PRs merged cleanly; this is a build/test failure only.
- Doesn't block the mobile-consumed endpoints listed in §4 (they were working pre-merge), but should be fixed before trusting any *new* backend change on `main`.
