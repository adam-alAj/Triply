# Backend Progress

**Owner:** Leen Sharbati
**Track:** Backend
**Status:** In Progress

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

### Dataset / Destination Suggestions
- [x] Add `PlaceInterest` place-to-interest relationship
- [x] Add composite key and foreign keys for `PlaceInterest`
- [x] Make budget-first destination suggestions interest-aware
- [x] Rank suggestions by matched-interest count, then estimated cost
- [x] Exclude destinations with zero interest overlap
- [x] Add separate migrations for dataset schema alignment and `PlaceInterest`
- [ ] Load the AI track's authoritative `PlaceInterest` seed data once delivered
- [ ] Resolve Backend vs AI reference-data seed conflict

### Flutter Integration
- [x] `GET /api/destinations`
- [x] `GET /api/interest-categories`
- [x] `GET /api/currencies`
- [x] Add `DisplayName` to Login/Register response
- [x] Update destination suggestions
- [x] Add Flutter integration tests
- [x] Update API documentation

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

- [ ] Verify Flutter-facing endpoints with the Flutter client
- [ ] Apply and verify the new EF Core migrations against the development database
- [ ] Run complete Backend build and test suite after latest changes
- [ ] Complete real Gemini API integration testing
- [ ] Verify Trip → AI → Itinerary → Cost end-to-end flow

---

## Waiting For

- [ ] Final AI JSON output schema from AI track
- [ ] Confirmation from Flutter team that the new endpoints match their integration
- [ ] Real Gemini API test using the required API key/configuration

---

## Testing

- [x] Existing Backend tests passed before latest Flutter integration changes
- [x] Flutter integration tests added
- [ ] Run full `dotnet build` after latest changes
- [ ] Run full `dotnet test` after latest changes
- [ ] Test Flutter ↔ Backend integration
- [ ] Test real Gemini generation end-to-end

---

## API Endpoints Added / Updated

### Authentication
- `POST /api/auth/register`
- `POST /api/auth/login`

Auth responses include:
- `token`
- `expiresAtUtc`
- `userId`
- `email`
- `displayName`

### Reference Data
- `GET /api/destinations`
- `GET /api/interest-categories`
- `GET /api/currencies`

### Destination Suggestions
- `POST /api/destinations/suggestions`

### Trip / Itinerary
- Trip CRUD/lifecycle endpoints
- `POST /api/trips/{tripId}/itinerary`
- AI itinerary generation

---

## Dependencies

### Flutter
Backend provides:
- Authentication response
- Destinations
- Interest categories
- Currencies
- Destination suggestions
- Trip and itinerary APIs

### AI Track
Backend depends on:
- Final Gemini JSON schema
- Prompt/schema decisions
- Dataset expectations
- AI validation requirements

---

## Next Steps

1. Run full Backend build.
2. Run full Backend tests.
3. Verify Flutter integration with real Backend endpoints.
4. Complete Gemini real API test.
5. Verify end-to-end itinerary generation.
6. Update this `progress.md` whenever Backend work changes.

---

## Notes

This file should be updated whenever Backend work is completed,
started, blocked, or changed.

Before committing changes, make sure this file reflects the current
state of the Backend implementation.
