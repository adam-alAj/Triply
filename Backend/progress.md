# Backend Progress

**Owner:** Leen Sharbati
**Track:** Backend
**Status:** In Progress — Mobile Profile/Place/Itinerary features implemented; Backend build and tests passing (67/67). AI-Orchestration and partial regeneration implemented. Live Gemini end-to-end verification is still pending.

---

## Completed

### Authentication

* [x] User registration
* [x] User login
* [x] JWT authentication
* [x] Return `DisplayName` in Login/Register response

### Trip Management

* [x] Create Trip
* [x] Get Trip
* [x] Save Trip
* [x] Archive Trip
* [x] Trip lifecycle/status handling

### Itinerary

* [x] Create itinerary
* [x] Update itinerary
* [x] Validate itinerary items
* [x] Validate that places belong to the selected destination
* [x] Persist itinerary data

### Cost

* [x] Cost aggregation service
* [x] Calculate category costs from the persisted itinerary using internal dataset prices
* [x] Persist CostEstimate rows by cost category
* [x] Update Trip total estimated cost
* [x] Integration coverage for itinerary → CostEstimate → Trip total

### Mobile Profile / User

* [x] `GET /api/users/me`
* [x] `PATCH /api/users/me`
* [x] Server-side user preferences model/storage
* [x] `GET /api/users/me/preferences`
* [x] `PUT /api/users/me/preferences` with backend validation
* [x] `GET /api/users/me/stats`

### Place Details

* [x] `GET /api/places/{id}`
* [x] Place description/category/destination/country/reference-price response
* [x] Flutter-ready `images` and `openingHours` response fields
* [x] Not-found handling for invalid/inactive places

### Itinerary Editing / Regeneration

* [x] `PATCH /api/trips/{tripId}/itinerary/items/{itemId}`
* [x] Individual item ownership, trip-scope, place and field validation
* [x] Preserve unrelated itinerary items during single-item edits
* [x] `POST /api/trips/{tripId}/generate` with FULL/DAY/ITEM scope
* [x] Day-level partial regeneration
* [x] Activity-level partial regeneration
* [x] Partial regeneration failure leaves the existing itinerary unchanged
* [x] Optimistic concurrency through `Trip.Version`

### Trip Metadata

* [x] Trip `title` persistence
* [x] Trip `coverImageUrl` persistence
* [x] `PATCH /api/trips/{tripId}` for custom trip metadata
* [x] EF migration for user preferences and trip metadata
* [x] Fixed missing `[DbContext(typeof(ApplicationDbContext))]` migration Designer attribute

### AI-Orchestration

* [x] Gemini client
* [x] Gemini configuration
* [x] Prompt builder aligned with the current AI JSON contract
* [x] Gemini response parsing
* [x] AI response validation
* [x] AI generation endpoint
* [x] Retry handling
* [x] Generated itinerary persistence
* [x] FULL / DAY / ITEM generation scopes implemented
* [x] Cost recalculation integrated with generated itineraries

---

## Currently Working On

* [ ] Complete live Gemini end-to-end testing
* [ ] Verify FULL generation against the live Gemini service
* [ ] Verify DAY partial regeneration against a generated itinerary
* [ ] Verify ITEM partial regeneration against a generated itinerary
* [ ] Verify BUDGET_FIRST generation once the required AI dataset context is available
* [ ] Verify Flutter integration with the new profile/place/itinerary endpoints
* [ ] Finalize and confirm API contract documentation with Flutter

---

## Waiting For

* [ ] `Extra_AI_Context.csv` with the required `budget_tier` values for BUDGET_FIRST testing
* [ ] Live Gemini API key / usable live Gemini test environment
* [ ] Confirmation from Flutter team that the endpoint shapes match integration needs
* [ ] Dataset/storage decision for place images and opening/business hours

---

## Testing

* [x] Full Backend build succeeds
* [x] Full Backend test suite passes — 67/67
* [x] Integration coverage for profile, preferences, stats, place details, trip metadata, and itinerary-item editing
* [x] Integration coverage for itinerary → CostEstimate → Trip total
* [ ] Live Gemini generation test
* [ ] DAY partial regeneration end-to-end test
* [ ] ITEM partial regeneration end-to-end test
* [ ] Flutter ↔ Backend integration verification
* [ ] CI verification on pull request

---

## Next Steps

1. Verify the Flutter-facing endpoints in Swagger.
2. Complete live Gemini testing for Destination First.
3. Obtain/verify `Extra_AI_Context.csv` and test BUDGET_FIRST.
4. Verify DAY and ITEM partial regeneration with a generated itinerary.
5. Update API contract documentation and communicate the new routes to Flutter.
6. Open the PR from `feature/mobile-profile-endpoints`.
7. Verify CI runs the full backend test suite on the PR.
8. Address the EF Core global query-filter warning if required.
9. Keep this `progress.md` updated as Backend work changes.

---

## Notes

This file should be updated whenever Backend work is completed, started, blocked, or changed.

`feature/mobile-profile-endpoints` is pushed to GitHub; PR not yet opened.
