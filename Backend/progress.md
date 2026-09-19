# Backend Progress

**Owner:** Leen Sharbati
**Track:** Backend
**Status:** In Progress — Mobile Profile/Place/Itinerary features implemented and fully tested (67/67 passing); real Gemini generation verified end-to-end for DESTINATION_FIRST, BUDGET_FIRST still pending

---

## Completed

### Authentication
- [x] User registration
- [x] User login
- [x] JWT authentication
- [x] Return `DisplayName` in Login/Register response

### Trip Management
- [x] Create Trip
- [x] Get Trip
- [x] Save Trip
- [x] Archive Trip
- [x] Trip lifecycle/status handling

### Itinerary
- [x] Create itinerary
- [x] Update itinerary
- [x] Validate itinerary items
- [x] Validate that places belong to the selected destination
- [x] Persist itinerary data

### Cost
- [x] Cost aggregation service
- [x] Calculate category costs from the persisted itinerary using internal dataset prices
- [x] Persist CostEstimate rows by cost category
- [x] Update Trip total estimated cost
- [x] Add integration coverage for itinerary → CostEstimate → Trip total

### Flutter Integration
- [x] `GET /api/destinations`
- [x] `GET /api/interest-categories`
- [x] `GET /api/currencies`
- [x] Add `DisplayName` to Login/Register response
- [x] Update destination suggestions
- [x] Add Flutter integration tests
- [x] Update API documentation

### Mobile Profile / User
- [x] `GET /api/users/me`
- [x] `PATCH /api/users/me` for authenticated display name updates
- [x] Server-side user preferences model/storage
- [x] `GET /api/users/me/preferences`
- [x] `PUT /api/users/me/preferences` with backend validation
- [x] `GET /api/users/me/stats` with user-scoped trip/country/saved-place aggregation

### Place Details
- [x] `GET /api/places/{id}`
- [x] Place description/category/destination/country/reference-price response
- [x] Flutter-ready `images` and `openingHours` response fields
- [x] Not-found handling for invalid/inactive places

### Itinerary Editing / Regeneration
- [x] `PATCH /api/trips/{tripId}/itinerary/items/{itemId}`
- [x] Individual item ownership, trip-scope, place and field validation
- [x] Preserve unrelated itinerary items during single-item edits
- [x] Extend `POST /api/trips/{tripId}/generate` with FULL/DAY/ITEM scope
- [x] Day-level partial regeneration preserves unaffected days
- [x] Activity-level partial regeneration preserves unaffected activities
- [x] Partial regeneration failure leaves the existing itinerary unchanged

### Trip Metadata
- [x] Trip `title` persistence
- [x] Trip `coverImageUrl` persistence
- [x] `PATCH /api/trips/{tripId}` for custom trip metadata
- [x] Existing full trip update remains available
- [x] EF migration for user preferences and trip metadata
- [x] Fixed missing `[DbContext(typeof(ApplicationDbContext))]` attribute on the migration's Designer.cs (migration was silently skipped by `Database.Migrate()` without it)

### AI-Orchestration
- [x] Gemini client
- [x] Gemini configuration
- [x] Prompt builder
- [x] Gemini response parsing
- [x] AI response validation
- [x] AI generation endpoint
- [x] Retry handling
- [x] Generated itinerary persistence
- [x] Real Gemini API key wired via `.env` (not committed)
- [x] Real end-to-end generation verified for **DESTINATION_FIRST** (Jerusalem): correct dates, grounded places, computed cost — succeeded on first attempt
- [x] Fixed: prompt never told Gemini the trip's actual start date, so every generated day used a hallucinated/unrelated date (e.g. 2024) instead of the real trip dates — `ItineraryPromptBuilder` now spells out the exact `day_number → date` mapping for both DESTINATION_FIRST and BUDGET_FIRST prompts
- [x] Fixed: a cost-aggregation failure after a successful generation (e.g. mismatched place currencies) used to leave the trip stuck in `GENERATED` status with no cost data and no way to retry; `AiOrchestrationService` now only transitions the trip to `GENERATED` after cost aggregation succeeds, and rolls the trip back to `DRAFT` if it fails
- [x] Fixed test-data bug: `Jerusalem Hotel` / `Jerusalem Public Transport` (accommodation/transport places for destination 1) were seeded with the wrong `CurrencyId` (JOD instead of USD), causing the currency-mismatch failure above
- [ ] Real end-to-end generation test for **BUDGET_FIRST** (multiple destination candidates) — not yet run
- [ ] Finalize AI JSON schema
- [ ] Complete end-to-end AI generation test (BUDGET_FIRST leg + confirm no remaining mocked generation path)
- [ ] Known trade-off, not yet resolved: itinerary persistence and cost-aggregation are two separate committed transactions rather than one atomic all-or-nothing transaction; the current fix is a self-healing rollback of trip status to `DRAFT` on cost-aggregation failure, not a single DB transaction — acceptable for now but worth revisiting against TASK47's literal "all-or-nothing transaction" acceptance criterion

---

## Currently Working On

- [ ] Real end-to-end Gemini test for BUDGET_FIRST mode
- [ ] Verify Flutter integration with the new profile/place/itinerary endpoints
- [ ] Update API contract documentation with the newly added routes and confirm with Flutter

---

## Waiting For

- [ ] Real `budget_tier` values: `Extra_AI_Context.csv` is currently empty
- [ ] Confirmation from Flutter team that the new endpoint shapes match integration needs
- [ ] Dataset/storage decision for place images and opening/business hours; current endpoint returns empty collections because those fields are not present in the current Place dataset model

---

## Testing

- [x] Existing Backend integration tests passed before the latest feature changes
- [x] Added integration coverage for profile, preferences, stats, place details, trip metadata, and individual itinerary-item editing
- [x] Run full `dotnet build` after latest feature changes — succeeds (10 warnings, no errors)
- [x] Run full `dotnet test` after latest feature changes — 67/67 passing
- [x] Real Gemini generation end-to-end — DESTINATION_FIRST verified (dates, grounded places, cost all correct)
- [ ] Real Gemini generation end-to-end — BUDGET_FIRST
- [ ] Test Flutter ↔ Backend integration

---

## Next Steps

1. ~~Run full Backend build.~~ Done.
2. ~~Run full Backend tests.~~ Done — 67/67 passing.
3. ~~Verify the new Flutter-facing endpoints in Swagger.~~ Done.
4. ~~Complete the live Gemini Docker test for Destination First.~~ Done — succeeded on first attempt after fixes.
5. Complete the live Gemini Docker test for Budget First.
6. Verify DAY and ITEM partial regeneration with a generated itinerary.
7. Update API contract documentation and communicate the new routes to Flutter.
8. Open a PR from `feature/mobile-profile-endpoints` for review.
9. (Non-blocking) Address EF Core warning: `Trip`'s global soft-delete query filter isn't matched by the required-end relationships `AIGeneration`, `CostEstimate`, `Itinerary`, `TripInterest`.
10. (Non-blocking, design decision) Decide whether the itinerary-persist + cost-aggregation flow needs to become a single atomic DB transaction, or whether the current self-healing rollback-to-DRAFT behavior is an acceptable interpretation of TASK47's acceptance criteria.
11. Update this `progress.md` whenever Backend work changes.

---

## Notes

This file should be updated whenever Backend work is completed,
started, blocked, or changed.

Before committing changes, make sure this file reflects the current
state of the Backend implementation.

`feature/mobile-profile-endpoints` pushed to GitHub; PR not yet opened.

Gemini API key is stored only in the local `.env` (git-ignored) — never commit it or push it to GitHub.