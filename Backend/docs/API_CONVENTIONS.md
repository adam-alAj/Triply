# Triply Backend — API Contract Conventions

**Status:** Agreed — Sprint 0
**Owner:** Leen Sharbati (Backend)
**Referenced by:** Backend track, Flutter track (Dana)
**Source:** SRS §9, §11 · System Architecture §10, §14 · Tech Stack (Swashbuckle row)

This document is the single source of truth for how the Flutter client and the
ASP.NET Core backend talk to each other. Any change here must be communicated
to Dana (Flutter) before merging, per the Project Rules communication clause.

---

## 1. Transport

- REST over HTTPS only. No GraphQL, no gRPC (matches team capability, Tech Stack §1).
- All request/response bodies are `application/json`, UTF-8.
- Base path in all environments: `/api`.

## 2. Resource naming

| Rule | Example |
|---|---|
| Plural nouns for collections | `GET /api/trips` |
| Singular resource by id | `GET /api/trips/{id}` |
| Nested resource under its parent | `GET /api/trips/{id}/itinerary` |
| Actions that aren't pure CRUD are verbs under the resource | `POST /api/trips/{id}/regenerate` |
| No verbs in resource names | ❌ `/api/getTrips` |

Confirmed module routes:
```
/api/auth/register
/api/auth/login
/api/trips
/api/trips/{id}
/api/trips/{id}/itinerary
/api/trips/{id}/cost-estimate
/api/destinations
/api/destinations/suggestions   (FR-TRIP-002, budget-first)
```

## 3. HTTP methods & status codes

| Method | Use | Success code |
|---|---|---|
| GET | Read | 200 |
| POST | Create / trigger an action | 201 (create) or 200 (action) |
| PATCH | Partial update (e.g. edit one itinerary item) | 200 |
| DELETE | Soft-delete/archive | 200 or 204 |

No PUT is used — every update in Triply is partial (FR-TRIP-003), so PATCH is
the consistent choice; avoids ambiguity about full-resource replacement.

## 4. DTO shape

- Clients never see EF Core entities. Every endpoint has a dedicated
  Request DTO and Response DTO (`record` types), even if they mirror the
  entity closely today — this keeps the API contract independent of schema
  changes (e.g. Database Design §15 denormalized fields stay internal).
- Field naming: `camelCase` in JSON (ASP.NET Core's default JSON serializer
  already does this conversion from C# `PascalCase`).
- Dates: `yyyy-MM-dd` for date-only fields (`startDate`, `endDate`), full
  ISO-8601 UTC (`2026-09-14T10:00:00Z`) for timestamps.
- Money: always a `decimal` amount **plus** a currency code field next to it
  — never a bare number (matches SRS §8 "Estimated" labeling requirement).
- IDs: GUIDs are serialized as strings; reference-table IDs (Country,
  Currency, etc.) are serialized as numbers, matching Database Design §7.

Example — Trip response DTO:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "GENERATED",
  "destination": { "id": 12, "name": "Petra", "countryName": "Jordan" },
  "startDate": "2026-11-01",
  "endDate": "2026-11-05",
  "travelerCount": 2,
  "totalEstimatedCost": { "amount": 850.00, "currency": "USD" },
  "isEstimated": true
}
```

## 5. Error format — ProblemDetails (RFC 7807)

Every non-2xx response uses `application/problem+json`. This is built into
ASP.NET Core (`ProblemDetails` / `ValidationProblemDetails`) — no custom
error wrapper is invented.

**Validation error (400):**
```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "email": ["'Email' is not a valid email address."],
    "travelerCount": ["'Traveler Count' must be greater than 0."]
  }
}
```

**Auth error (401) — no leakage of which field was wrong:**
```json
{
  "title": "Invalid email or password.",
  "status": 401
}
```

**Forbidden (403) — accessing another user's trip:**
```json
{
  "title": "You do not have access to this resource.",
  "status": 403
}
```

**Server error (500) — never a stack trace:**
```json
{
  "title": "An unexpected error occurred.",
  "status": 500,
  "instance": "/api/trips/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Rule:** Flutter should branch on `status`, never parse `title` strings —
`title` text may change; `status` codes are stable.

## 6. Versioning

**Decision: no URL/header versioning in MVP.** One backend, one client,
released together each sprint — a version prefix would add complexity with
no current benefit (matches "simplest architecture the team can own"
principle, Architecture §2). Revisit only if a public/third-party API
consumer appears post-MVP.

## 7. Authentication

- `Authorization: Bearer <JWT>` header on every protected endpoint.
- `/api/auth/register` and `/api/auth/login` are the only unauthenticated
  endpoints.
- Token expiry and refresh strategy: short-lived JWT (60 min default,
  configurable via `Jwt:ExpiresMinutes`); no refresh-token flow in MVP —
  user re-logs in on expiry (simplest option, consistent with team capability).

## 8. Swagger / OpenAPI — how Flutter consumes the contract

- Swashbuckle generates the live spec at `GET /swagger/v1/swagger.json` and
  a browsable UI at `/swagger`, from the controllers/DTOs directly — the
  spec can never drift from the real code.
- A static snapshot (`docs/openapi.yaml`) is committed to the repo for
  offline reference and version history; regenerate it at the end of each
  sprint (see `docs/README.md` in the same folder for the command).
- Dana can either read the Swagger UI directly, or feed
  `docs/openapi.yaml` into a client generator if the team wants typed Dio
  models later (optional, not required for MVP).

## 9. Pagination (for future list endpoints)

Not required for MVP (dataset sizes are small — bounded destination list,
per-user trip counts are low). If added later: `?page=1&pageSize=20` with a
response envelope `{ "items": [...], "page": 1, "pageSize": 20, "total": 42 }`.

## 10. Change process

Any change to this document must be flagged to the Flutter track before
merging (Project Rules §12 — Communication Rule), and the corresponding
Trello card must record the decision.
