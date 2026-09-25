# ✈️ Triply Mobile — Flutter Progress

*Owner: Dana Yaseen · Track: Flutter / Mobile + Web · Last updated: 2026-09-25*

> **One Flutter codebase. Android, iOS and Web. The full Triply loop — PLAN → GENERATE → VIEW → CUSTOMIZE → SAVE — running end-to-end against the real backend.**

---

## 🌟 At a Glance

| | |
|---|---|
| 📱 **Screens & surfaces** | 18 screens plus bottom sheets, modals and dialogs |
| 🔌 **Backend integration** | 20+ real endpoints wired through the repository layer |
| 🧩 **Design system** | 25+ reusable widgets, one implementation each |
| ✅ **Tests** | **87 / 87 passing** (`flutter test`) |
| 🧹 **Static analysis** | **0 issues** (`flutter analyze`) |
| 🌍 **Platforms** | Android (emulator + real device), iOS, Flutter Web — same code |

---

## 🧭 1. The Core Journey

Every step of the product loop is built, connected, and reachable from the app:

```
 PLAN                  GENERATE             VIEW                  CUSTOMIZE              SAVE
 ──────────────────    ─────────────────    ──────────────────    ───────────────────    ─────────────
 Planning Mode         Review & Confirm     Trip Overview         Edit item              Save trip
 Budget / Destination  Generating screen    · Itinerary tab       Regenerate day / item  My Trips
 Suggestions           (long-wait aware)    · Costs tab           Adjust budget          Archive
 Trip Details                               Place Detail Sheet    Share / Export
 Interests
```

Both planning strategies from the SRS are fully supported:
- **Destination First** — pick a curated destination → details → interests → review → generate.
- **Budget First** — set a budget → interests → Triply suggests destinations that fit → generate.

---

## 🏗️ 2. Architecture

Clean, layered and swappable — the same pattern across every feature:

```
 UI (screens / widgets)  →  Provider (state)  →  Repository (data)  →  ApiClient (Dio)  →  Backend
```

- **Provider → Repository → UI.** Screens never touch the network; they read a `Provider`, which calls a `Repository`.
- **Mock ↔ Real, zero UI changes.** Every feature has an abstract `XyzRepository`, a `MockXyzRepository` and an `ApiXyzRepository`. Swapping them never touches a widget — proven by the architecture smoke test in `test/widget_test.dart`.
- **Single network gateway.** `lib/core/network/api_client.dart` owns base URL, timeouts, logging and error translation into one friendly `ApiException`.
- **Automatic device detection.** `ApiClient.create()` probes `10.0.2.2:8080` (Android emulator) and `localhost:8080` (real device via `adb reverse`, iOS simulator, desktop, web) and uses whichever answers — the same build runs everywhere without edits.
- **Seamless auth.** `auth_interceptor.dart` attaches the JWT from secure storage (`token_storage.dart`) to every request; session restore on launch keeps users signed in, and only a real 401/403 signs them out.
- **Smart caching.** `destination_assets_cache.dart` shares destinations and cover photos across Home, My Trips, Trip Creation and Trip Overview for the whole session — fast screens and no rate-limit pressure on the API.

**Stack:** Flutter · Dart · Provider · Dio · flutter_secure_storage · device_info_plus · share_plus

---

## 📱 3. Screens

| # | Screen | File | Highlights |
|---|---|---|---|
| 1 | Splash | `screens/splash_screen.dart` | Session restore via `GET /api/users/me` |
| 2 | Onboarding | `screens/onboarding/` | Welcome flow with skip |
| 3 | Login | `widgets/auth/login_screen.dart` | Real JWT login, friendly error handling |
| 4 | Register | `widgets/auth/register_screen.dart` | Client validation mirrors the server's password policy; lands straight on Home |
| 5 | Home | `screens/home/home_screen.dart` | Personal greeting, active-trip card, featured regions with real destination photos |
| 6 | Planning Mode | `screens/trip_creation/planning_mode_screen.dart` | Destination First vs Budget First |
| 7 | Budget / Destination | `screens/trip_creation/budget_destination_screen.dart` | Budget presets + custom budget; destination picker (redesigned — see §7) |
| 8 | Destination Suggestions | `screens/trip_creation/destination_suggestions_screen.dart` | Budget-fit suggestions with real photos, empty state with next steps |
| 9 | Trip Details | `screens/trip_creation/trip_details_screen.dart` | Dates, travelers, optional budget |
| 10 | Interests | `screens/trip_creation/interests_screen.dart` | Chips mapped 1:1 to the backend's interest categories |
| 11 | Review & Confirm | `screens/trip_creation/review_screen.dart` | Full preference snapshot with edit-jumps to any step |
| 12 | Generating | `screens/trip_creation/generating_screen.dart` | Honest long-wait UX (`GenerationWaitNotice`), cancel, retry, over-budget notice |
| 13 | My Trips | `screens/my_trips/my_trips_screen.dart` | Active / Archived filter, real titles and cover photos |
| 14 | Profile | `screens/profile/profile_screen.dart` | Edit name, stats, currency & pacing pickers, distance units, privacy toggles, logout |
| 15 | Trip Overview — Itinerary | `screens/trip_overview/trip_overview_screen.dart` | Day selector, timeline cards, edit, regenerate day/item, save, archive, share, invite |
| 16 | Trip Overview — Costs | same file | Transparent breakdown, budget health, category chart, stay highlight, adjust budget, export |
| 17 | Place Detail Sheet | `widgets/trip_overview/place_detail_sheet.dart` | Rich sheet with live place details from `GET /api/places/{id}` |
| 18 | About Triply | `screens/profile/about_screens.dart` | Terms of Service, Privacy & GDPR, live Supported Destinations directory |

Plus contextual surfaces kept deliberately *off* the page stack (per UI Pages §6): Edit Item modal, Regenerate sheet, Archive dialog, Adjust Budget sheet, Notifications sheet, option pickers.

---

## 🎨 4. Design System

One implementation each, reused everywhere (`lib/presentation/widgets/`):

`AppScaffold` · `PrimaryButton` · `SecondaryButton` · `AppTextField` · `SelectionCard` · `InterestChip` · `DestinationCard` · `TripCard` · `DaySelector` · `ItineraryItemCard` · `CostCategoryRow` · `StatusBadge` · `AIGeneratedBadge` · `EstimatedBadge` · `VerifiedBadge` · `BudgetStatusChip` · `OverBudgetNotice` · `GenerationWaitNotice` · `EmptyState` · `ErrorState` · `LoadingSkeleton` · `AppBottomSheet` · `ConfirmationDialog` · `AppBottomNavigation` · `showNotificationsSheet`

Trip Overview kit (`widgets/trip_overview/`): Place Detail Sheet · Edit Item Modal · Regenerate Sheet · Archive / Delete Dialog · Adjust Budget Sheet · Trip Share (summary, invite, cost breakdown)

**Design principles built in**
- **Trust labeling** — three distinct provenance markers: `AIGeneratedBadge` (AI content), `EstimatedBadge` (costs), `VerifiedBadge` (dataset-checked). AI content is never confused with verified data (08 §Principle 05, SRS §8).
- **No dead ends** — every loading, empty and error state explains what happened and offers a next action (08 §Principle 06).
- **Resilient layouts** — buttons scale long labels to fit instead of overflowing.
- **Accessibility** — `liveRegion` announcements on the generation-wait and over-budget notices, tooltips on icon-only actions, color never the only signal.
- **Tokens, not magic numbers** — `AppColors` and `AppTextStyles` drive the whole UI.

---

## 🔌 5. Backend Integration

All wired through repositories and verified against the live API:

| Feature | Endpoint(s) |
|---|---|
| Register · Login | `POST /api/auth/register` · `POST /api/auth/login` |
| Session restore · Edit name | `GET` / `PATCH /api/users/me` |
| Preferences (currency, pacing, units) | `GET` / `PUT /api/users/me/preferences` |
| Profile stats | `GET /api/users/me/stats` |
| Currencies | `GET /api/currencies` |
| Destinations · Cover photos | `GET /api/destinations` · `GET /api/destinations/assets` |
| Budget-first suggestions | `POST /api/destinations/suggestions` |
| Create trip · Adjust budget | `POST /api/trips` · `PUT /api/trips/{id}` |
| AI generation (full + day/item regeneration) | `POST /api/trips/{id}/generate` |
| Home · My Trips | `GET /api/trips` |
| Trip Overview (itinerary + costs) | `GET /api/trips/{id}` |
| Edit itinerary item | `PATCH /api/trips/{tripId}/itinerary/items/{itemId}` |
| Save · Archive | `POST /api/trips/{id}/save` · `POST /api/trips/{id}/archive` |
| Place details | `GET /api/places/{id}` |

Smart details that make it feel solid:
- Generation calls get a **120s** window (AI generation can take several attempts) while every other call stays fast at 15s.
- Partial regeneration sends **`expectedVersion`** so a stale screen can never overwrite a newer trip.
- Preference changes are **optimistic** — the UI updates instantly and rolls back cleanly if the save fails.
- The backend's **`isOverBudget`** signal is surfaced through `OverBudgetNotice` and `BudgetStatusChip`.

---

## 🛠️ 6. Problems Solved Along the Way

| Challenge | Solution |
|---|---|
| No network calls worked on Android | Added the missing `INTERNET` permission to `AndroidManifest.xml` |
| Real phones couldn't reach the backend (emulator-only address) | Automatic base-URL detection for emulator, real device, simulator and web |
| Budget-first asked for suggestions before interests existed | Reordered the wizard so Interests comes first on that path |
| "Nightlife" interest didn't exist server-side | Interests aligned exactly with the backend's reference list |
| Register accepted passwords the server would reject | Client validation now mirrors the server policy |
| Home buttons did nothing | "Plan Your First Trip" and "Resume" wired; Resume opens Trip Overview |
| A network hiccup signed users out | Logout only on a real 401/403 |
| Trip Overview always showed mock "Tokyo" data | Switched to the real `ApiTripOverviewRepository` |
| Trips stuck forever at `GENERATING` | Traced the exception mismatch in the generation flow and restored the trip to `DRAFT` on failure |
| Repeated navigation hit the API rate limiter | Session-wide destination/photo cache |
| Long AI generations timed out on the client | Dedicated 120s window for generation |
| Over-budget trips looked identical to on-target ones | `BudgetStatusChip` shows both states clearly |
| Budget-first users couldn't let the AI choose a destination | Continue enabled without a pick in Budget First |
| Inconsistent emoji glyphs across devices | Replaced with Material Icons |
| Overflow stripe on the Costs tab buttons | Buttons scale labels to fit |

---

## 🚀 7. Latest Updates (2026-09-23 → 2026-09-25)

### Every action now does something — PR #91 ✅ merged

| Where | Action | Result |
|---|---|---|
| Trip Overview | **Share** | OS share sheet with a clean day-by-day trip summary |
| Trip Overview | **Invite** | Ready-to-send invitation (destination, dates, estimated total) |
| Costs tab | **Adjust Budget** | Budget sheet → saved via `PUT /api/trips/{id}` → trip refreshes |
| Costs tab | **Export** | Full cost breakdown (categories, %, total, budget, per traveler/day) to any app |
| Profile | **Preferred Currency** | Live currency list, saved to the account |
| Profile | **Default Pacing** | Relaxed / Balanced / Fast, saved to the account |
| Profile | **Privacy toggles** | Remembered on the device |
| Profile | **About Triply** | Terms, Privacy & GDPR, live Supported Destinations |
| Home · My Trips · Profile · Planning Mode | **Notifications** | Friendly "You're all caught up" sheet |

### Destination selection redesign — branch `Mobile/feat/destination-selection-redesign`

A richer, image-led "Where do you want to explore?" experience, built entirely from real data:
- **Large photo cards** — destination photo with gradient, country + name overlay, "Curated by Triply" pill, clear selected state.
- **Live search** across name, country and description.
- **Country filter chips** generated automatically from the dataset — new destinations appear with no code change.
- **Helpful "not supported yet" notice** with *Show all destinations* — covers UI Pages journey J8 inline, exactly as specified.
- **Bottom selection bar** — "Selected: Amman" + **Continue to Trip Details**.
- Verified at 320 / 360 / 412 px phone widths.

---

## ✅ 8. Quality

- **87 / 87 tests passing** across 17 test files — architecture smoke test, shared UI components, badges, notices, budget chip, generation outcome and more (`Mobile/test/`).
- **`flutter analyze`: 0 issues.**
- Every change shipped through small, single-purpose branches and reviewed pull requests.

---

## ▶️ 9. Running the App

```bash
cd Mobile
flutter pub get
flutter run                                  # emulator or connected device
flutter run -d chrome --web-port 8765        # Flutter Web (port allowed by the API's CORS policy)
```

Real Android device over USB: run `adb reverse tcp:8080 tcp:8080` once, and the app finds the backend on its own. After adding a native plugin (e.g. `share_plus`), do a full restart rather than hot reload.

---

## 📦 10. Delivery History

| PR | Branch | Delivered |
|---|---|---|
| [#49](https://github.com/adam-alAj/Triply/pull/49) | `Mobile/feat/update-navigation` | Navigation structure |
| [#51](https://github.com/adam-alAj/Triply/pull/51) | `web` | Flutter Web build |
| [#71](https://github.com/adam-alAj/Triply/pull/71) | `Mobile/fix/session-restore-logout` | Reliable session restore |
| [#72](https://github.com/adam-alAj/Triply/pull/72) | `Mobile/fix/trip-generation-and-overview` | Generation reliability, real cover photos, caching, generation timeout |
| — | `Mobile/feat/network-autodetect-and-auth-fixes` | Base-URL auto-detect, auto-login after register, icon cleanup |
| [#77](https://github.com/adam-alAj/Triply/pull/77) | `Mobile/fix/budget-suggestions-images` | Real photos on budget suggestions |
| [#78](https://github.com/adam-alAj/Triply/pull/78) | `Mobile/feat/trip-overview-rich-redesign` | Place Detail Sheet redesign, stay highlight, `expectedVersion` |
| [#79](https://github.com/adam-alAj/Triply/pull/79) · [#81](https://github.com/adam-alAj/Triply/pull/81) | `Mobile/feat/device-run-auto-detect` | Run on any device without configuration |
| [#91](https://github.com/adam-alAj/Triply/pull/91) | `Mobile/feat/activate-placeholder-actions` | Every action activated (§7) |
| — | `Mobile/feat/destination-selection-redesign` | Destination selection redesign (§7) |

---

## 🔭 11. What's Next

Natural next steps that build on what's already in place:
- **Currency display** — apply the saved preferred currency to every price once exchange rates are exposed by the API.
- **Persisted item removal & reordering** — the UI is ready; plug in the endpoints as they land.
- **Per-place imagery** — swap the Place Detail Sheet's preview image for real place photos when the dataset includes them.
- **Deeper test coverage** — extend widget tests to every screen flow.

---

*Triply Mobile — built so every traveler can say: "I told it what I want, and it made planning the trip easy."*
