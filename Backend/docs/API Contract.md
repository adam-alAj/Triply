# Triply — Authentication API Contract

**Backend base URL (local dev):** `https://localhost:8080`


All endpoints below are under `/api/auth`.

---

## 1. Register

**Endpoint:** `POST /api/auth/register`

### Request body

| Field       | Type   | Required | Notes                                                                                                                              |
| ----------- | ------ | -------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| email       | string | Yes      | Must be a valid and unused email                                                                                                   |
| password    | string | Yes      | Minimum 8 characters, including at least one uppercase letter, one lowercase letter, one digit, and one non-alphanumeric character |
| displayName | string | No       | Optional, maximum 100 characters                                                                                                   |

### Example request

```json
{
  "email": "leen.test2026@example.com",
  "password": "Test1234!",
  "displayName": "Leen Test"
}
```

### Success response — `200 OK`

```json
{
  "token": "<JWT_TOKEN>",
  "expiresAtUtc": "2026-09-14T16:16:57.1299752Z",
  "refreshToken": "<REFRESH_TOKEN>",
  "refreshTokenExpiresAtUtc": "2026-10-14T16:01:57.1299752Z",
  "userId": "<USER_ID>",
  "email": "leen.test2026@example.com",
  "displayName": "Leen Test"
}
```

Register already returns a valid JWT, so no separate login call is required immediately after registration.

### Tested in Swagger

![Register](image.png)

### Error responses

**`400 Bad Request` — validation error or duplicate email**

Example:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": [
      "An account with this email already exists."
    ]
  },
  "traceId": "<TRACE_ID>"
}
```

Other validation errors, such as invalid email or weak password, use the same `400` response structure with an `errors` object.

Flutter should read the relevant field from `errors` when displaying validation messages.

### Duplicate email test

![Register already registered account](image-4.png)

---

## 2. Login

**Endpoint:** `POST /api/auth/login`

### Request body

| Field    | Type   | Required |
| -------- | ------ | -------- |
| email    | string | Yes      |
| password | string | Yes      |

### Example request

```json
{
  "email": "leen.test2026@example.com",
  "password": "Test1234!"
}
```

### Success response — `200 OK`

```json
{
  "token": "<JWT_TOKEN>",
  "expiresAtUtc": "2026-09-14T16:17:57.7489158Z",
  "refreshToken": "<REFRESH_TOKEN>",
  "refreshTokenExpiresAtUtc": "2026-10-14T16:02:57.7489158Z",
  "userId": "<USER_ID>",
  "email": "leen.test2026@example.com",
  "displayName": "Leen Test"
}
```

The response has the same structure as Register, including `displayName`.

### Tested in Swagger

![Login](image-1.png)

### Error responses

**`401 Unauthorized` — invalid email or password**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Invalid email or password.",
  "status": 401,
  "traceId": "<TRACE_ID>"
}
```

The same generic message is returned for both an incorrect password and an email that does not exist.

Flutter should show one generic authentication error instead of trying to distinguish between the two cases.

### Wrong password test

![Wrong password](image-2.png)

### Invalid or non-existing email test

![Invalid email](image-5.png)

---

### `429 Too Many Requests` — rate limit exceeded

The login endpoint allows up to **5 failed attempts per minute**. After the limit is exceeded, the API returns `429 Too Many Requests`.

Flutter should handle `429` separately and show a message such as:

> Too many attempts. Please try again in a moment.

### Rate limit test

![Multiple login rate limit](image-3.png)

---

## 3. JWT Usage

Send the access token (`token`) with every authenticated request:

```http
Authorization: Bearer <JWT_TOKEN>
```

* **Access token lifetime:** 15 minutes by default (`Jwt:ExpiresMinutes`)
* **Refresh token lifetime:** 30 days by default (`Jwt:RefreshTokenExpiresDays`)
* **Issuer:** `Triply`
* **Audience:** `TriplyClients`
* `expiresAtUtc`, `refreshToken`, and `refreshTokenExpiresAtUtc` are returned by both Register and Login.

Flutter should use the returned expiration values rather than hardcoding them. Refresh tokens rotate on successful refresh; the server stores only a SHA-256 hash of each refresh token.

### Refresh access token

**Endpoint:** `POST /api/auth/refresh` (no access-token authorization required)

```json
{
  "refreshToken": "<REFRESH_TOKEN>"
}
```

On success, `200 OK` returns the same `AuthResponse` shape as Register/Login with a new access token and rotated refresh token. An invalid, expired, or revoked refresh token returns `401 Unauthorized`.

### Logout

**Endpoint:** `POST /api/auth/logout`

**Authentication:** JWT Bearer required.

Request body uses the same `refreshToken` shape as the refresh endpoint. The matching token is revoked when it belongs to the authenticated user; the endpoint returns `204 No Content`, including when the token is already revoked or does not belong to that user.

---

## 4. Summary

| Endpoint | Status | Meaning                             |
| -------- | -----: | ----------------------------------- |
| Register |    200 | Account created and JWT returned    |
| Register |    400 | Validation error or duplicate email |
| Login    |    200 | Login successful and JWT returned   |
| Login    |    401 | Invalid email or password           |
| Login    |    429 | Too many login attempts             |
| Refresh  |    200 | Access and refresh tokens rotated   |
| Refresh  |    401 | Invalid or expired refresh token    |
| Logout   |    204 | Refresh token revoked (idempotent)  |

### Tested cases

* Successful registration
* Successful login
* Wrong password → `401`
* Invalid/non-existing email → `401`
* Multiple login attempts → `429`
* Registering an already registered account → `400`
* Invalid registration data → `400`


---


---

# 5. Reference Data for Flutter

These authenticated read-only endpoints provide the data used by the Flutter planning flow. They return simple JSON arrays.

## 5.1 Supported Destinations

**Endpoint:** `GET /api/destinations`

**Authentication:** JWT Bearer required.

Only destinations with `isSupported = true` are returned. The curated dataset currently supports Paris (France), Amman (Jordan), and New York (United States).

### Success response — `200 OK`

```json
[
  {
    "id": 1,
    "name": "Paris",
    "countryName": "France",
    "description": "Capital of France, known for iconic landmarks, museums, and cuisine.",
    "latitude": 48.8566,
    "longitude": 2.3522
  }
]
```

## 5.2 Interest Categories

**Endpoint:** `GET /api/interest-categories`

**Authentication:** JWT Bearer required.

### Success response — `200 OK`

```json
[
  {
    "id": 1,
    "code": "NATURE",
    "label": "Nature"
  }
]
```

## 5.3 Currencies

**Endpoint:** `GET /api/currencies`

**Authentication:** JWT Bearer required.

### Success response — `200 OK`

```json
[
  {
    "id": 1,
    "isoCode": "EUR",
    "symbol": "€"
  }
]
```

These endpoints are intentionally read-only. Flutter should use their returned IDs when creating/updating trips or requesting destination suggestions.

# 5. Budget-First Destination Suggestions

**Endpoint:** `POST /api/destinations/suggestions`

**Authentication:** JWT Bearer required.

### Request body

```json
{
  "budgetAmount": 700.00,
  "budgetCurrencyId": 1,
  "interestCategoryIds": [1, 2]
}
```

`budgetAmount` must be greater than zero, the currency must exist, and at least one valid interest category must be supplied.

### Success response — `200 OK`

```json
{
  "suggestions": [
    {
      "destinationId": 1,
      "destinationName": "Paris",
      "countryName": "France",
      "estimatedCost": 600.00,
      "currency": "EUR",
      "estimatedCostInBudgetCurrency": 600.00,
      "budgetCurrencyId": 1,
      "isEstimated": true,
      "isWithinBudget": true
    }
  ],
  "count": 1,
  "message": "Choose one suggested destination, then generate the trip."
}
```

Candidates are calculated from active internal `Place` rows in supported destinations that match at least one requested interest through `PlaceInterest`. Reference prices are aggregated in each destination's native currency and converted to the requested budget currency using the exchange-rate table. The response retains the native `estimatedCost` and `currency` and also returns `estimatedCostInBudgetCurrency` and `budgetCurrencyId`. Only destinations within budget are returned, up to three, ordered by distinct matched-interest count descending and converted estimated cost ascending.

### No matching destination — `200 OK`

```json
{
  "suggestions": [],
  "count": 0,
  "message": "No supported destinations match the selected interests and budget."
}
```

Interest-aware suggestions use the internal `PlaceInterest` ground-truth mapping. Destinations with zero overlap are excluded, so a budget-first request cannot silently fall back to budget-only matching.

---

# 6. Cost Estimate Aggregation

**Endpoint:** `GET /api/trips/{tripId}/cost-estimate`

**Authentication:** JWT Bearer required. The trip must belong to the authenticated user.

The endpoint deterministically aggregates `CostEstimates` for the trip by `CostCategory`, includes every configured cost category (including categories with no estimate, returned as `0.00`), computes the total as the sum of category amounts, and persists the result to `Trip.TotalEstimatedCost`.

Every returned figure is explicitly marked with `isEstimated: true`.

### Success response — `200 OK`

```json
{
  "tripId": "00000000-0000-0000-0000-000000000000",
  "categories": [
    {
      "costCategoryId": 1,
      "categoryCode": "ACCOMMODATION",
      "categoryName": "Accommodation",
      "amount": 120.00,
      "currency": "USD",
      "isEstimated": true
    },
    {
      "costCategoryId": 2,
      "categoryCode": "FOOD",
      "categoryName": "Food",
      "amount": 80.00,
      "currency": "USD",
      "isEstimated": true
    }
  ],
  "totalEstimatedCost": 200.00,
  "currency": "USD",
  "isEstimated": true
}
```

Cost estimates for one trip must use a single currency before aggregation. A trip with no cost rows still returns all configured categories with zero amounts and uses the trip budget currency when available.

# 7. Itinerary Read / Write Scaffolding

**Endpoints:**
- `GET /api/trips/{tripId}/itinerary`
- `POST /api/trips/{tripId}/itinerary`

**Authentication:** JWT Bearer required. The trip must belong to the authenticated user.

The endpoint persists and returns the current itinerary with days ordered by `dayNumber` and items ordered by `timeSlot` (`MORNING`, `AFTERNOON`, `EVENING`) and then `orderIndex`.

### Write request

```json
{
  "days": [
    {
      "dayNumber": 1,
      "date": "2026-10-01",
      "items": [
        {
          "placeId": 1,
          "timeSlot": "MORNING",
          "orderIndex": 0,
          "estimatedCost": 25.00,
          "notes": "Start early",
          "isAiGenerated": true
        }
      ]
    }
  ]
}
```

Validation requires at least one day, unique positive day numbers, valid time slots, non-negative order indexes and estimated costs, and valid place IDs. Places must exist and be active; when the trip has a destination, every itinerary place must belong to that destination.

The write operation replaces the existing itinerary for the trip atomically. This manual write endpoint is separate from AI generation; it validates supplied itinerary payloads and does not call an LLM.

### Read response — `200 OK`

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "tripId": "00000000-0000-0000-0000-000000000000",
  "generatedAt": "2026-09-15T18:00:00Z",
  "days": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "dayNumber": 1,
      "date": "2026-10-01",
      "items": [
        {
          "id": "00000000-0000-0000-0000-000000000000",
          "placeId": 1,
          "placeName": "Example Place",
          "timeSlot": "MORNING",
          "orderIndex": 0,
          "estimatedCost": 25.00,
          "notes": "Start early",
          "isAiGenerated": true,
          "modifiedAt": null
        }
      ]
    }
  ]
}
```

A trip without an itinerary returns `404 Not Found`. A different user's itinerary also returns `404 Not Found` and never exposes itinerary data.


---

# 8. Trip Save / Retrieve and Status Lifecycle

**Endpoints:**
- `GET /api/trips/{id}`
- `POST /api/trips/{id}/generate`
- `POST /api/trips/{id}/save`
- `POST /api/trips/{id}/archive`
- `POST /api/trips/{id}/restore`

**Authentication:** JWT Bearer required. The trip must belong to the authenticated user.

Trip status follows the database lifecycle:

`DRAFT → GENERATING → GENERATED → MODIFIED → SAVED → ARCHIVED`

A generation request moves `DRAFT` to `GENERATING`. Writing the generated itinerary moves `GENERATING` to `GENERATED`; changes to a generated or saved trip move it to `MODIFIED`. Saving a generated/modified trip moves it to `SAVED` and increments `version`. A saved trip can be archived and an archived trip can be restored to `SAVED`.

### AI generation / partial regeneration

`POST /api/trips/{id}/generate`

The endpoint supports:

- `FULL`: generates/replaces the complete itinerary. `expectedVersion` is not required.
- `DAY`: regenerates only the requested `dayNumber`; every other day is preserved.
- `ITEM`: regenerates only the requested activity `itemId`; every other item is preserved.

For `DAY` and `ITEM`, `expectedVersion` is required and must equal the current trip `version`. A stale value returns `409 Conflict` and no itinerary content is replaced.

Example partial regeneration request:

```json
{
  "scope": "DAY",
  "dayNumber": 2,
  "expectedVersion": 4
}
```

Successful partial regeneration increments `Trip.version` exactly once. Direct item edits through `PATCH /api/trips/{id}/itinerary/items/{itemId}` set only the edited item's `isAiGenerated` to `false`; untouched AI-generated items remain unchanged.

Successful generation responses include `tripVersion` so the client can use the returned version for the next optimistic-concurrency write.

Successful generation returns one selected `itinerary`, its deterministic `cost` estimate, `aiGenerationId`, `attemptsUsed`, the updated `tripVersion`, and `isOverBudget`. For `BUDGET_FIRST`, the backend checks grounded candidate options in the model's returned order, persists the first option within budget, and fails validation if none fit; it does not return the candidates as a choice list. The internal `AIGeneration` row stores the schema version used (`2.0.0` for current requests); that provenance value is not part of the client response.

### Save

`POST /api/trips/{id}/save`

Only `GENERATED` and `MODIFIED` trips can be saved. The response returns the complete persisted trip representation.

### Retrieve

`GET /api/trips/{id}` returns the trip together with its current itinerary and cost estimates, including the persisted status and version.

A different user's trip returns `404 Not Found`.

### Trip response

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "planningMode": "DESTINATION_FIRST",
  "status": "SAVED",
  "destinationId": 1,
  "destinationName": "Example City",
  "startDate": "2026-10-01",
  "endDate": "2026-10-05",
  "travelerCount": 2,
  "budgetAmount": 1500.00,
  "budgetCurrencyId": 1,
  "interestCategoryIds": [1, 2],
  "itinerary": null,
  "costEstimate": null,
  "version": 2
}
```
