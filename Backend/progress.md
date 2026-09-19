# Backend Progress

**Owner:** Leen Sharbati
**Track:** Backend
**Status:** In Progress — Mobile Profile/Place/Itinerary features implemented and fully tested (67/67 passing); real Gemini test still pending API key

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
- [ ] Finalize AI JSON schema
- [ ] Test with real Gemini API
- [ ] Complete end-to-end AI generation test

---

## Currently Working On

- [ ] Complete real Gemini API integration testing
- [ ] Verify full and partial generation against the live Gemini service
- [ ] Verify Flutter integration with the new profile/place/itinerary endpoints
- [ ] Update API contract documentation with the newly added routes and confirm with Flutter

---

## Waiting For

- [ ] Real Gemini API key for live Docker generation test
- [ ] Real `budget_tier` values: `Extra_AI_Context.csv` is currently empty
- [ ] Confirmation from Flutter team that the new endpoint shapes match integration needs
- [ ] Dataset/storage decision for place images and opening/business hours; current endpoint returns empty collections because those fields are not present in the current Place dataset model

---

## Testing

- [x] Existing Backend integration tests passed before the latest feature changes
- [x] Added integration coverage for profile, preferences, stats, place details, trip metadata, and individual itinerary-item editing
- [x] Run full `dotnet build` after latest feature changes — succeeds (9 warnings, no errors)
- [x] Run full `dotnet test` after latest feature changes — 67/67 passing
- [ ] Test Flutter ↔ Backend integration
- [ ] Test real Gemini generation end-to-end

---

## Next Steps

1. ~~Run full Backend build.~~ Done.
2. ~~Run full Backend tests.~~ Done — 67/67 passing.
3. Verify the new Flutter-facing endpoints in Swagger.
4. Complete the live Gemini Docker test for Destination First and Budget First.
5. Verify DAY and ITEM partial regeneration with a generated itinerary.
6. Update API contract documentation and communicate the new routes to Flutter.
7. Open a PR from `feature/mobile-profile-endpoints` for review.
8. (Non-blocking) Address EF Core warning: `Trip`'s global soft-delete query filter isn't matched by the required-end relationships `AIGeneration`, `CostEstimate`, `Itinerary`, `TripInterest`.
9. Update this `progress.md` whenever Backend work changes.

---

## Notes

This file should be updated whenever Backend work is completed,
started, blocked, or changed.

Before committing changes, make sure this file reflects the current
state of the Backend implementation.

`feature/mobile-profile-endpoints` pushed to GitHub; PR not yet opened.
