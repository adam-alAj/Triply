# Triply Mobile — Progress

*Owner: Dana Yaseen · Track: Flutter/Mobile · Last updated: 2026-09-22*

This document tracks what exists in the Flutter app today: architecture, every screen, the shared component library, and — most importantly — which features are wired to the real backend versus still running on mock data.

---

## 1. Architecture

- **Pattern**: Provider (state) → Repository (data access) → UI (screens/widgets). Screens never talk to the network directly; they read a `Provider` and the `Provider` calls a `Repository`.
- **Networking**: `lib/core/network/api_client.dart` is the only class allowed to touch Dio directly. The backend base URL is no longer hardcoded — `ApiClient.create()` probes `10.0.2.2:8080` (Android emulator) and `localhost:8080` (a real device over USB with `adb reverse tcp:8080 tcp:8080` set up, or iOS simulator/desktop) with a short timeout and uses whichever answers, so the same build runs unmodified on either. It translates transport errors into a single `ApiException`.
- **Shared reference-data cache**: `lib/core/network/destination_assets_cache.dart` caches `GET /api/destinations` and `GET /api/destinations/assets` for the app session — both are near-static and were previously refetched on every single screen visit (Home, My Trips, Trip Creation), which was enough repeated navigation to trip the backend's rate limiter (429).
- **Auth propagation**: `lib/core/network/auth_interceptor.dart` reads the JWT from `lib/core/storage/token_storage.dart` (secure storage) and attaches it to every outgoing request automatically. No screen or repository has to think about the token.
- **Mock ↔ Real swap**: every feature has an abstract `XyzRepository` interface, a `MockXyzRepository`, and (where connected) an `ApiXyzRepository`. Swapping one for the other never touches the UI layer — proven by the architecture smoke test in `test/widget_test.dart`.

---

## 2. Screens

| # | Screen | File | Status |
|---|---|---|---|
| 1 | Splash | `lib/presentation/screens/splash_screen.dart` | Built + **connected to real backend** (session restore via `GET /api/users/me`) |
| 2 | Onboarding | `lib/presentation/screens/onboarding/` | Built |
| 3 | Login | `lib/presentation/widgets/auth/login_screen.dart` | Built + **connected to real backend** |
| 4 | Register | `lib/presentation/widgets/auth/register_screen.dart` | Built + **connected to real backend**; now navigates straight to Home on success instead of leaving the user stranded on a confirmation snackbar with no way forward |
| 5 | Home | `lib/presentation/screens/home/home_screen.dart` | Built + **connected to real backend** (`ApiHomeRepository`); real logged-in user name in greeting; Featured Regions and the active-trip card use real destination cover photos |
| 6 | Planning Mode | `lib/presentation/screens/trip_creation/planning_mode_screen.dart` | Built — local state only, no backend needed |
| 7 | Budget/Destination | `lib/presentation/screens/trip_creation/budget_destination_screen.dart` | Built + **connected to real backend** — both Budget-first and Destination-first use `GET /api/destinations` / suggestions; destination cards show real cover photos |
| 8 | Destination Suggestions | `lib/presentation/screens/trip_creation/destination_suggestions_screen.dart` | Built + **connected to real backend** (budget-first path); suggestion cards show real destination photos instead of one generic placeholder |
| 9 | Trip Details | `lib/presentation/screens/trip_creation/trip_details_screen.dart` | Built — local state, sent to backend on submit |
| 10 | Interests | `lib/presentation/screens/trip_creation/interests_screen.dart` | Built — values mapped to real backend IDs |
| 11 | Review | `lib/presentation/screens/trip_creation/review_screen.dart` | Built + **connected to real backend** (triggers trip creation) |
| 12 | Generating | `lib/presentation/screens/trip_creation/generating_screen.dart` | Built + **connected to real backend** (create trip + start generation); generate call now allows up to 120s (was the same 15s default as every other call, which a multi-attempt AI generation can easily exceed even on success) |
| 13 | My Trips | `lib/presentation/screens/my_trips/my_trips_screen.dart` | Built + **connected to real backend**; surfaces `Trip.Title` / `Trip.CoverImageUrl`, falling back to the destination's real cover photo when a trip has none of its own |
| 14 | Profile | `lib/presentation/screens/profile/profile_screen.dart` | Built + **connected to real backend** — `GET`/`PATCH /api/users/me`, `GET`/`PUT /api/users/me/preferences`, `GET /api/users/me/stats` |
| 15 | Trip Overview — Itinerary tab | `lib/presentation/screens/trip_overview/trip_overview_screen.dart` | Built + **connected to real backend** (`ApiTripOverviewRepository`); item edit via `PATCH .../itinerary/items/{itemId}`, partial regen via `POST .../generate?scope=DAY\|ITEM` (now sends `expectedVersion`), Save and Archive actions wired with the backend's real error messages surfaced instead of a generic one |
| 16 | Trip Overview — Costs & Split tab | same file | Built + **connected to real backend**; added a Stay Highlight card (real Figma design element) that detects the accommodation item via its `Notes: "Accommodation: ..."` prefix and shows it with the destination's real cover photo |
| 17 | Place Detail Sheet | `lib/presentation/widgets/trip_overview/place_detail_sheet.dart` | Built + **connected to real backend** (`GET /api/places/{id}`) — rich redesign (hero image, info pills, price, description); hero image/crowd-cadence/verification badges have no backend source yet and are clearly labeled "Preview data" |

**Not yet built**: AI Assistant chat (Post-MVP per SRS), Welcome/guest screen, Password Recovery, a currency picker (budget currency is hardcoded to USD — no UI exists to change it).

---

## 3. Shared Component Library (`lib/presentation/widgets/`)

One implementation each, reused everywhere: `AppScaffold`, `PrimaryButton`, `SecondaryButton`, `AppTextField`, `SelectionCard`, `InterestChip`, `DestinationCard`, `TripCard`, `DaySelector`, `ItineraryItemCard`, `CostCategoryRow`, `StatusBadge`, `AIGeneratedBadge`, `EstimatedBadge`, `VerifiedBadge`, `EmptyState`, `ErrorState`, `LoadingSkeleton`, `GenerationWaitNotice`, `AppBottomSheet`, `ConfirmationDialog`, `AppBottomNavigation`.

The three provenance markers are deliberately distinct: `AIGeneratedBadge` (AI-authored content, cool blue-grey), `EstimatedBadge` (cost estimates, warm coral) and `VerifiedBadge` (dataset-checked, semantic green) — AI content can never be mistaken for a verified or estimated value (08 §Principle 05, SRS §8).

**Gap**: not every component has a dedicated widget test yet (the architecture smoke test plus `test/` widget tests for the shared library exist; coverage is still partial).

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
| Destinations list (destination-first) | `GET /api/destinations` | `ApiTripCreationRepository` (cached — see §1) |
| Destination cover photos | `GET /api/destinations/assets` | shared `DestinationAssetsCache` (Home, My Trips, Trip Creation, Trip Overview) |
| Destination suggestions (budget-first) | `POST /api/destinations/suggestions` | `ApiTripCreationRepository` |
| Create trip | `POST /api/trips` | `ApiTripCreationRepository` |
| Start AI generation | `POST /api/trips/{id}/generate` | `ApiTripCreationRepository` |
| Home screen (recent trip, active trip) | `GET /api/trips` | `ApiHomeRepository` |
| My Trips list | `GET /api/trips` | `ApiHomeRepository` |
| Trip Overview (Itinerary + Costs tabs) | `GET /api/trips/{id}` | `ApiTripOverviewRepository` |
| Itinerary item edit | `PATCH /api/trips/{tripId}/itinerary/items/{itemId}` | `ApiTripOverviewRepository` |
| Partial regeneration (day/item scope) | `POST /api/trips/{id}/generate?scope=DAY\|ITEM` | `ApiTripOverviewRepository` |
| Trip Save / Archive actions | `POST /api/trips/{id}/save` / `/archive` | `ApiTripOverviewRepository` |

### 🟡 Still mock / not wired

- Per-item description in the Itinerary tab's list card — `ItineraryItemResponse` only sends `PlaceName`, not a description (that lives on the separate `GET /api/places/{id}` the Place Detail Sheet already uses). Not fetched per list row to avoid one extra call per item.
- Itemized cost lines inside each Costs-tab category card (e.g. "4 nights Kyoto Machiya Ryokan — $680" under Accommodation) — the real Figma design shows these, but `CostEstimateResponse` only returns category totals, not a per-place breakdown. Would need a small backend DTO addition (e.g. a `Category` field on `ItineraryItemResponse`) to build client-side.
- Itinerary item reorder / delete — no backend endpoint exists for either yet.

### ⛔ Known backend/infra gaps (need a teammate, listed by priority)

1. **Local reference-data seeding is not part of automatic startup.** Three Python scripts under `AI/01-Dataset/seed/` must be run manually, in this order, against any fresh database: `seed_places.py` → `Seed_place_interests.py` → `seed_exchange_rates.py`. Skipping any one of them reproduces a real bug we hit today (see §5). This needs to become part of the documented/automated setup, not tribal knowledge.
2. **Budget-First destination suggestions compute an unrealistic "estimated cost."** `DestinationSuggestionService` sums the reference price of *every* active place for a destination (all hotel tiers, every restaurant, every activity) as if a trip would include all of them — e.g. Paris comes out to ~$2,585 and New York ~$2,055 just from that sum, so a normal budget (e.g. $1,000) only ever returns Amman. Needs a product/algorithm decision (e.g. one hotel + N days of meals/activities) — not a one-line fix.
3. **`AI/` dataset folder was never in the API container's Docker build context** — `BUDGET_FIRST` generation (`Extra_AI_Context.csv`) failed with a file-not-found until we added a read-only volume mount in `Backend/docker-compose.yml`. Confirm this mount is preserved in any future compose/deployment changes.
4. **No currency conversion rates ship by default** — `ExchangeRates` table is empty until `seed_exchange_rates.py` runs (see #1); until then, `POST /api/destinations/suggestions` 500s outright for any request needing cross-currency comparison.
5. **Gemini model name goes stale without warning.** Google retires model names with no backward-compatible alias — `gemini-2.0-flash` (the shipped default in `appsettings.Example.json` and `GeminiOptions.cs`) now 404s. Fixed to `gemini-3.6-flash`, confirmed against the live API, but expect this to need updating again; there's no runtime check that surfaces "your configured model no longer exists" other than every generation failing.
6. **This specific Gemini API key's free-tier quota is tight enough that real generation calls (large grounding-data + JSON-schema prompt) routinely hit 429/503**, even after raising retries to 5 attempts with exponential backoff (3s/6s/12s/20s). A trivial prompt succeeds far more reliably than the real generation prompt — the quota is effectively token-based, not just request-count-based. Needs either a higher-tier key or a smaller prompt (less grounding data sent per call).
7. Per-place images and opening hours don't exist in the data model at all (`PlacesController` always returns `Images: []`) — not a bug, just unbuilt.

~~`GET /api/destinations`~~ — resolved, see ✅ table above.
~~`AuthResponse`/`GET /api/users/me` doesn't return display name~~ — resolved, see ✅ table above.
~~Trip generation gets permanently stuck at `GENERATING`~~ — resolved, see §5.

---

## 5. Notable Fixes Made Along the Way

- **Android `INTERNET` permission was missing entirely** from `AndroidManifest.xml` — silently broke every network call and the debug VM service. Fixed.
- **Wizard step order bug**: budget-first flow requested destination suggestions *before* the user picked interests, and the backend rejects an empty `interestCategoryIds` list. Reordered so Interests comes before Suggestions for that path only.
- **Interests mismatch**: mobile had "Nightlife" as an option; backend's fixed reference list has no such category (only `OTHER`). Fixed to match exactly.
- **Register password validation** was weaker than the backend's actual policy (needs uppercase + digit + symbol) — users could fill the whole form and still get rejected. Client-side check now mirrors the server rule.
- **Home's "Plan Your First Trip" and "Resume Trip Itinerary" buttons had no `onPressed` wired at all** — fixed; "Resume" now correctly opens Trip Overview instead of restarting the creation wizard.
- **`restoreSession()` was wiping the saved token on *any* failure of `GET /api/users/me`** — not just a real 401/403, but also a network timeout or the backend being unreachable, permanently signing the user out on a transient hiccup. Fixed to only call `logout()` on an actual 401/403.
- **Trip Overview screen was still wired to `MockTripOverviewRepository`** — every trip, regardless of what was actually generated, showed the same hardcoded "Tokyo" mock data. Real bug, not a data/backend issue; switched to `ApiTripOverviewRepository`.
- **Generation left the trip permanently stuck at `GENERATING` whenever `GeminiClient` threw before the retry loop's `catch (GeminiApiException)` block could see it** (e.g. a missing/misconfigured API key throws `InvalidOperationException` instead) — the status flip to `GENERATING` had already committed, and nothing on that path ever reverted it. Every trip that failed this way showed $0 everywhere in Trip Overview with no way to retry, save, or archive it. Fixed in `AiOrchestrationService.GenerateItineraryAsync` to catch and properly revert to `DRAFT`.
- **Local reference database was missing all of: curated places (only 2 old placeholder Amman rows existed), place↔interest tags (0 rows for the real dataset — see gap #1 above), and exchange rates** — reproduced and fixed by running the three seed scripts. Also found and deactivated 2 leftover duplicate Amman places from an earlier ad-hoc manual seed attempt that were skewing `DestinationSuggestionService`'s interest-match filter.
- **Backend base URL was hardcoded to the Android emulator's `10.0.2.2` alias**, which doesn't exist on real hardware — a physical device over USB could never reach the backend no matter what. `ApiClient.create()` now probes both `10.0.2.2` and `localhost` and uses whichever answers (see §1).
- **Six raw emoji/unicode-glyph characters embedded directly in `Text` widgets** (Login, Register, Profile, Home) replaced with proper Material Icons for consistent rendering across devices.

- **UI/UX gap states implemented** (from the UI/UX Gap Report): the Generating screen now escalates at 30s+ via a reusable `GenerationWaitNotice` (honest elapsed time, reassurance, Cancel still available — no fake progress, no fake percent); the budget-first Destination Suggestions screen now has a real "No destinations match your budget" empty state with *Adjust budget* / *Change interests* next steps plus a loading skeleton; and the AI-generated vs Estimated vs Verified labeling is now a consistent three-way system (`AIGeneratedBadge` / `EstimatedBadge` / `VerifiedBadge`). The duplicated local `_AccuracyBadge` in Trip Overview no longer reuses the AI badge's color for "Verified".

**Obstacle**: the `GENERATING`-stuck bug was hard to pin down from the report alone ("$0 everywhere, stuck on GENERATING") — needed to trace the exact exception type `GeminiClient` throws against exactly which `catch` blocks the retry loop actually has, since the type mismatch (`InvalidOperationException` vs the expected `GeminiApiException`) is easy to miss on a quick read.

---

## 6. Git Status

Recent mobile/backend work has landed across several small, single-purpose branches (per-feature branches, not one large one):

- PR [#48](https://github.com/adam-alAj/Triply/pull/48) `feature/mobile-profile-endpoints` — backend profile/preferences/stats/place-details/itinerary-item-PATCH/partial-regen/trip-metadata endpoints — **merged into `main`**.
- PR [#49](https://github.com/adam-alAj/Triply/pull/49) `Mobile/feat/update-navigation` — **merged into `main`**.
- PR [#50](https://github.com/adam-alAj/Triply/pull/50) `feature/mobile-profile-endpoints` — **merged into `main`**.
- PR [#51](https://github.com/adam-alAj/Triply/pull/51) `web` — **merged into `main`**.
- Branch `Mobile/fix/session-restore-logout` — the `restoreSession()` token-wipe fix (§5). Pushed, not yet merged.
- Branch `Mobile/fix/trip-generation-and-overview` — stuck-`GENERATING` fix, Gemini model-name fix, `AI/` dataset volume mount, destination cover images (Home/My Trips/Trip Creation/Trip Overview), rate-limit cache, generate-call timeout. Pushed, not yet merged.
- Branch `Mobile/feat/network-autodetect-and-auth-fixes` — base-URL auto-detect, auto-login after register, emoji cleanup, generation retry/backoff improvements. Pushed, not yet merged.
- Branch `Mobile/fix/budget-suggestions-images` — real destination photos on Budget-First suggestion cards. Pushed, not yet merged.
- Branch `Mobile/feat/trip-overview-rich-redesign` — Place Detail Sheet redesign, `expectedVersion` on partial regen, region/country label, Stay Highlight card. Pushed, not yet merged.

**Previously flagged `main` compile issue (missing `api_auth_repository.dart`) — resolved**; the file exists and is in active use (see §4).

### Over-budget signal surfaced (2026-09-23)

The Backend's `isOverBudget` field on `POST /api/trips/{id}/generate` is now surfaced to users, closing the last gap from the AI/ML validation work:

- `GenerationOutcome` (`lib/data/models/generation_outcome.dart`) carries the flag.
- `TripCreationRepository.startGeneration` now returns it instead of `Future<void>`; the API implementation parses `isOverBudget`, the mock returns the unflagged default.
- `TripCreationProvider.isOverBudget` exposes it, reset with the rest of the flow state.
- `OverBudgetNotice` (`lib/presentation/widgets/over_budget_notice.dart`) renders it on the generation success view — an advisory notice, not an error, because the itinerary was generated and persisted successfully. It uses `AppColors.warning`, not the error treatment, and is a `liveRegion` for screen readers.

Only `DESTINATION_FIRST` can be flagged: `BUDGET_FIRST` rejects a plan that does not fit instead of returning it.

### Budget status + multi-option BUDGET_FIRST (2026-09-23, same day)

- **Trip Overview showed nothing when over budget.** It already computed `isOnTarget` (`budgetAmount ?? totalEstimatedCost`) but the cost summary only rendered an "On Target" chip and omitted the chip entirely otherwise, so an over-budget trip looked like one with no budget data. Now extracted into `BudgetStatusChip` (`lib/presentation/widgets/budget_status_chip.dart`), which renders **both** states — warning treatment and `Icons.trending_up_rounded` for over budget, matching `OverBudgetNotice`. Recomputed from budget vs. cost, so it also shows on later visits where `isOverBudget` (a generate-response field only) is unavailable.
- **BUDGET_FIRST users could not reach the no-destination flow.** The suggestions step's `_ContinueButton` required a destination in both modes, so generation was always scoped to a user-picked destination and the Backend could never propose multiple affordable destinations. Continue is now enabled for `BUDGET_FIRST` without a selection, with a hint explaining that the AI will suggest destinations the budget can afford. `DESTINATION_FIRST` still requires a choice. The review step already renders `data.destination ?? 'Not selected'`, and the only submit guard is `DESTINATION_FIRST`-specific, so no other step needed changing.

---

## 7. CI Status (resolved 2026-09-23)

**Backend CI (`Backend CI / build-and-test`) was failing** on `main` (`Process completed with exit code 1`, 1 error / 11 warnings). It is now **fixed and verified locally**.

- The error was in `Backend/Triply.Api/Program.cs` and came from the PR #75 merge, which lost three lines of the rate-limiter block:
  - the closing `});` of the `AddFixedWindowLimiter("fixed", ...)` lambda, which left the following `AddPolicy(...)` calls nested inside it, and
  - that lambda's `opt.Window = TimeSpan.FromMinutes(1);` line, whose absence left `FixedWindowRateLimiterOptions.Window` at its default `TimeSpan.Zero`.
- A leftover `options.AddPolicy("fixed", ...)` block also referenced an **undefined** `generalPermitLimit` (declared nowhere in the repository). It was a duplicate registration of the already-registered `"fixed"` policy name, so it was removed rather than given an invented value.
- **The second half of that merge damage was a runtime 500, not a compile error.** With the braces fixed the build went green, but every rate-limited route — including `/api/auth/register` — threw `ArgumentException: Window must be set to a value greater than TimeSpan.Zero`, because `AddFixedWindowLimiter` does not default `Window` to a positive value. This is why the build fix alone was not enough to get the suite working.
- Remaining warnings are pre-existing nullable-reference-type mismatches in `Backend/Triply.Api.Tests/ItineraryIntegrationTests.cs` and `UserIdentityTests.cs` only (9 warnings, no errors). `ItineraryValidationService.cs` no longer warns.
- Backend suite: **119/119 passing** against SQL Server, stable across three consecutive runs. See `Backend/Triply.Api/Modules/AI-Orchestration/progress.md` for how the suite is run locally.
