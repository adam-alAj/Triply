# Triply API Contract

For the Flutter team. Every route starts with `/api`.
JSON is `camelCase`, dates are `yyyy-MM-dd`, timestamps are UTC (`2026-09-24T10:15:52Z`).
Rules and error format: [API_CONVENTIONS.md](API_CONVENTIONS.md).

| Environment | Base URL |
|---|---|
| Live (Render) | `https://triply-api-za13.onrender.com` |
| Local (Docker) | `http://localhost:8080` |

Live Swagger is off. Use the local one: `http://localhost:8080/swagger`.

---

## 0. Quick reference

🔒 = needs `Authorization: Bearer <token>`.

| Area | Method + route | 🔒 |
|---|---|:-:|
| Auth | `POST /auth/register` · `/auth/login` · `/auth/refresh` | |
| Auth | `POST /auth/logout` | ✅ |
| Auth | `GET /auth/confirm-email` · `POST /auth/resend-confirmation` · `/auth/forgot-password` · `/auth/reset-password` | |
| Profile | `GET/PATCH /users/me` · `GET/PUT /users/me/preferences` · `GET /users/me/stats` | ✅ |
| Reference | `GET /destinations` · `/currencies` · `/interest-categories` · `/places/{id}` | ✅ |
| Reference | `GET /destinations/assets` | |
| Suggestions | `POST /destinations/suggestions` | ✅ |
| Trips | `POST /trips` · `GET /trips` · `GET /trips/{id}` · `PUT /trips/{id}` · `PATCH /trips/{id}` · `PATCH /trips/{id}/destination` | ✅ |
| Lifecycle | `POST /trips/{id}/save` · `/archive` · `/restore` | ✅ |
| AI | `POST /trips/{id}/generate` | ✅ |
| Itinerary | `GET/POST /trips/{id}/itinerary` · `PATCH /trips/{id}/itinerary/items/{itemId}` | ✅ |
| Costs | `GET /trips/{id}/cost-estimate` | ✅ |
| Health | `GET /health` | |

### Status codes you must handle

| Code | Meaning | What Flutter should do |
|---|---|---|
| `400` | Validation error | Show messages from `errors` next to the fields |
| `401` | Not logged in / bad or expired token | Try `/auth/refresh` once, else go to Login |
| `404` | Not found, **or not your trip** | Show "not found" |
| `409` | Old `expectedVersion` or wrong trip status | Reload the trip, then retry |
| `422` | AI plan failed validation | Show error + Retry |
| `429` | Too many requests | Wait, then retry |
| `502` | Gemini failed | Show error + Retry |

Branch on the **status code**, not the message text.

### Rate limits (per user, or per IP when not logged in)

| Scope | Limit |
|---|---|
| Login | 5 per minute |
| Everything else | 10 per minute |
| AI generation | 10 per hour |

---

## 1. Auth

Password rule: at least 8 characters, with an uppercase letter, a lowercase letter, a digit, and a symbol.

### 1.1 Register — `POST /api/auth/register`

```json
{ "email": "leen.test2026@example.com", "password": "Test1234!", "displayName": "Leen Test" }
```
`displayName` is optional (max 100).

**`200 OK`** (same shape for login and refresh)
```json
{
  "token": "<JWT>",
  "expiresAtUtc": "2026-09-14T16:16:57Z",
  "refreshToken": "<REFRESH_TOKEN>",
  "refreshTokenExpiresAtUtc": "2026-10-14T16:01:57Z",
  "userId": "<USER_ID>",
  "email": "leen.test2026@example.com",
  "displayName": "Leen Test"
}
```
Register already logs the user in, so no separate login call is needed.

**`400`** invalid data or duplicate email:
```json
{ "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "Email": ["An account with this email already exists."] } }
```

![Register](image.png) ![Duplicate email](image-4.png)

### 1.2 Login — `POST /api/auth/login`

```json
{ "email": "leen.test2026@example.com", "password": "Test1234!" }
```
**`200`** same shape as register.
**`401`** `"Invalid email or password."` — the same message for a wrong password and an unknown email.
**`429`** after 5 attempts in a minute.
**`403`** only if email confirmation is turned on and the email is not confirmed.

![Login](image-1.png) ![Wrong password](image-2.png) ![Invalid email](image-5.png) ![Rate limit](image-3.png)

### 1.3 Tokens

Send on every 🔒 request: `Authorization: Bearer <token>`.

- Access token: **15 minutes**. Refresh token: **30 days**. Issuer `Triply`, audience `TriplyClients`.
- Use `expiresAtUtc` from the response; don't hardcode the times.

**Refresh — `POST /api/auth/refresh`**
```json
{ "refreshToken": "<REFRESH_TOKEN>" }
```
`200` returns a new access token **and a new refresh token** (the old one stops working — save the new one). `401` if invalid, expired, or already used.

**Logout — `POST /api/auth/logout`** 🔒 — same body. Always `204`.

### 1.4 Email and password reset

| Endpoint | Body | Result |
|---|---|---|
| `GET /auth/confirm-email?userId=&token=` | — | `200` or `400` |
| `POST /auth/resend-confirmation` | `{ "email": "..." }` | Always `200` (never reveals if the account exists) |
| `POST /auth/forgot-password` | `{ "email": "..." }` | Always `200` |
| `POST /auth/reset-password` | `{ "userId": "...", "token": "...", "newPassword": "..." }` | `200`, or `400` for a bad token / weak password |

A successful reset revokes all refresh tokens.
⚠️ Emails are only written to the server log for now (no real email provider yet), so these flows cannot be tested end-to-end from the app.

---

## 2. Profile

| Endpoint | Body | Response |
|---|---|---|
| `GET /users/me` | — | `{ "id", "email", "displayName" }` |
| `PATCH /users/me` | `{ "displayName": "New name" }` | Updated profile |
| `GET /users/me/preferences` | — | Preferences (defaults are created on first read) |
| `PUT /users/me/preferences` | Full set, see below | Updated preferences |
| `GET /users/me/stats` | — | `{ "totalTrips", "totalSavedPlaces", "totalCountries" }` |

```json
{ "preferredCurrencyId": 1, "preferredCurrency": "EUR", "distanceUnit": "KM", "pacing": "BALANCED" }
```
`distanceUnit`: `KM` or `MILES`. `pacing`: `RELAXED`, `BALANCED`, or `FAST`. (`preferredCurrency` is only in the response.)

---

## 3. Reference data

Read-only. Flutter should use these, not hard-coded lists.

**`GET /destinations`** — only supported destinations (now: Paris, Amman, New York)
```json
[{ "id": 1, "name": "Paris", "countryName": "France",
   "description": "Capital of France...", "latitude": 48.8566, "longitude": 2.3522 }]
```
**`GET /destinations/assets`** (public) — destination image URLs: `{ "version": "1.0", "destinations": [{ "destinationName": "Paris", "url": "https://..." }] }`

**`GET /interest-categories`** → `[{ "id": 1, "code": "NATURE", "label": "Nature" }]`

**`GET /currencies`** → `[{ "id": 1, "isoCode": "EUR", "symbol": "€" }]`

**`GET /places/{id}`** (example values)
```json
{ "id": 20, "name": "Jordan Tower Hotel", "description": "...", "category": "ACCOMMODATION",
  "destinationId": 2, "destinationName": "Amman", "countryName": "Jordan",
  "referencePrice": 25.00, "currency": "USD",
  "images": [], "openingHours": [{ "day": "Monday", "opensAt": "09:00", "closesAt": "17:00", "isClosed": false }] }
```

---

## 4. Budget-first suggestions — `POST /api/destinations/suggestions`

```json
{ "budgetAmount": 700.00, "budgetCurrencyId": 1, "interestCategoryIds": [1, 2] }
```
Budget must be > 0, the currency must exist, and at least one valid interest is required.

**`200`**
```json
{
  "suggestions": [{
    "destinationId": 1, "destinationName": "Paris", "countryName": "France",
    "estimatedCost": 600.00, "currency": "EUR",
    "estimatedCostInBudgetCurrency": 600.00, "budgetCurrencyId": 1,
    "isEstimated": true, "isWithinBudget": true
  }],
  "count": 1,
  "message": "Choose one suggested destination, then generate the trip."
}
```
- Up to 3 destinations, only those within budget, matching at least one selected interest.
- Sorted by most matched interests, then lowest cost.
- No match → `200` with `"suggestions": []` and a message (so show an "adjust budget" screen, not an error).

---

## 5. Trips

### Trip object (used in most responses)
```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "planningMode": "DESTINATION_FIRST",
  "status": "DRAFT",
  "title": null,
  "coverImageUrl": null,
  "destinationId": 1,
  "destinationName": "Paris",
  "startDate": "2026-10-01",
  "endDate": "2026-10-05",
  "travelerCount": 2,
  "budgetAmount": 1500.00,
  "budgetCurrencyId": 1,
  "interestCategoryIds": [1, 2],
  "itinerary": null,
  "costEstimate": null,
  "version": 1
}
```
`itinerary` and `costEstimate` are filled in by `GET /trips/{id}`. The list (`GET /trips`) leaves them out.

**Status:** `DRAFT → GENERATING → GENERATED → MODIFIED → SAVED → ARCHIVED`

### 5.1 Create — `POST /api/trips` → `201`
```json
{
  "planningMode": "DESTINATION_FIRST",
  "destinationId": 1,
  "startDate": "2026-10-01",
  "endDate": "2026-10-05",
  "travelerCount": 2,
  "budgetAmount": 1500.00,
  "budgetCurrencyId": 1,
  "interestCategoryIds": [1, 2]
}
```
- `planningMode`: `DESTINATION_FIRST` (needs `destinationId`) or `BUDGET_FIRST` (needs `budgetAmount`).
- `travelerCount` > 0, `endDate` ≥ `startDate`, budget ≥ 0, max 50 interests.
- Response: the trip object with status `DRAFT`.

### 5.2 Read
- `GET /trips` → array of the user's trips, newest first.
- `GET /trips/{id}` → trip + itinerary + cost estimate. Someone else's trip → `404`.

### 5.3 Change
| Endpoint | Body | Notes |
|---|---|---|
| `PUT /trips/{id}` | Same fields as create (without `planningMode`) + `expectedVersion` | Full update of details |
| `PATCH /trips/{id}` | `{ "title": "...", "coverImageUrl": "...", "expectedVersion": 3 }` | Title / cover only |
| `PATCH /trips/{id}/destination` | `{ "destinationId": 1, "expectedVersion": 1 }` | Pick a destination after budget-first suggestions |

Old `expectedVersion` → `409`. Archived or generating trips cannot be edited (`409`).

### 5.4 Save / archive / restore
- `POST /trips/{id}/save` — only `GENERATED` or `MODIFIED` trips. Returns the full trip.
- `POST /trips/{id}/archive` — `SAVED` → `ARCHIVED`. Returns `{ "id", "status", "version" }`.
- `POST /trips/{id}/restore` — `ARCHIVED` → `SAVED`. Returns `{ "id", "status", "version" }`.
- Wrong current status → `409`.

Evidence: ![Create](image-9.png) ![Get](image-7.png) ![Other user's trip blocked](image-8.png) ![Update](image-10.png)

---

## 6. AI generation — `POST /api/trips/{id}/generate`

Rate limit: 10 per hour per user. Can take several seconds — show a loading screen and allow for long waits.

| Scope | Body | Notes |
|---|---|---|
| `FULL` (default) | `{ "scope": "FULL" }` or empty body | Trip must be `DRAFT` |
| `DAY` | `{ "scope": "DAY", "dayNumber": 2, "expectedVersion": 4 }` | Only that day changes |
| `ITEM` | `{ "scope": "ITEM", "itemId": "<guid>", "expectedVersion": 4 }` | Only that activity changes |

`DAY` and `ITEM` require `expectedVersion`. A successful partial regeneration raises the trip version by one.

**`200 OK`** (real response from the live service)
```json
{
  "aiGenerationId": "9c16df01-2a0f-4600-a924-d593f13f1fef",
  "attemptsUsed": 1,
  "tripVersion": 3,
  "isOverBudget": false,
  "itinerary": { "...": "see section 7" },
  "cost": { "...": "see section 8" }
}
```
Keep `tripVersion` for the next edit.

**Budget rules**
- `BUDGET_FIRST`: the AI proposes up to 3 destinations; the first one within budget is saved onto the trip. If none fits → `422`.
- `DESTINATION_FIRST`: always saved; `isOverBudget: true` tells the user it is above budget.

**Errors**

| Code | When |
|---|---|
| `400` | Bad scope, or `expectedVersion` missing for DAY/ITEM |
| `404` | Trip not found or not yours |
| `409` | Old `expectedVersion`, or trip in the wrong status |
| `422` | Plan failed validation after retries (`message`, `attemptsUsed`, `errors`) |
| `502` | Gemini did not answer (`message`, `attemptsUsed`, `errors`) |
| `429` | Hourly limit reached |

On `422`/`502` nothing is saved.

---

## 7. Itinerary

**`GET /trips/{id}/itinerary`** → `200`, or `404` if there is none yet / not your trip.

```json
{
  "id": "e24fb5eb-e877-4beb-9b29-c48bde79eb49",
  "tripId": "8d96c1aa-f157-4cbd-8610-99bdecc64311",
  "generatedAt": "2026-09-24T10:15:52Z",
  "days": [{
    "id": "1381d7e8-c76e-48bb-8b4c-fd9b19f4d507",
    "dayNumber": 1,
    "date": "2024-05-01",
    "items": [{
      "id": "975801d2-d602-4827-bbe6-d8e3a8db8d53",
      "placeId": 20,
      "placeName": "Jordan Tower Hotel",
      "timeSlot": "MORNING",
      "orderIndex": 0,
      "estimatedCost": 50.00,
      "notes": "Accommodation: 2 nights",
      "isAiGenerated": true,
      "modifiedAt": null
    }]
  }]
}
```
Days are sorted by `dayNumber`; items by `timeSlot` (`MORNING`, `AFTERNOON`, `EVENING`) then `orderIndex`.

**`POST /trips/{id}/itinerary`** — write a whole itinerary by hand (does not call the AI). It **replaces** the current one.
```json
{ "days": [{ "dayNumber": 1, "date": "2026-10-01",
    "items": [{ "placeId": 1, "timeSlot": "MORNING", "orderIndex": 0,
                "estimatedCost": 25.00, "notes": "Start early", "isAiGenerated": true }] }] }
```
Rules: at least one day, unique positive day numbers, valid time slots, costs and indexes ≥ 0, every place must exist, be active, and belong to the trip's destination.

**`PATCH /trips/{id}/itinerary/items/{itemId}`** — edit one item
```json
{ "placeId": 5, "timeSlot": "EVENING", "orderIndex": 1, "notes": "Book a table" }
```
Only that item becomes `isAiGenerated: false`; the trip becomes `MODIFIED`. Notes max 1000 chars. `409` if the trip is archived or generating.

---

## 8. Costs — `GET /api/trips/{id}/cost-estimate`

```json
{
  "tripId": "00000000-0000-0000-0000-000000000000",
  "categories": [
    { "costCategoryId": 1, "categoryCode": "ACCOMMODATION", "categoryName": "Accommodation",
      "amount": 120.00, "currency": "USD", "isEstimated": true },
    { "costCategoryId": 2, "categoryCode": "FOOD", "categoryName": "Food",
      "amount": 80.00, "currency": "USD", "isEstimated": true }
  ],
  "totalEstimatedCost": 200.00,
  "currency": "USD",
  "isEstimated": true
}
```
All categories are always returned (`0.00` when empty). Total = sum of categories. Always show these as **estimated**.

---

## 9. Health — `GET /health`

Public. Returns `{ "status": "ok" }`.
