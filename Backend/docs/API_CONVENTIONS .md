# Triply Backend — API Contract Conventions
**Status:** Agreed — Sprint 0
**Owner:** Backend Team
**Referenced by:** Backend and Flutter tracks
 
**Source:** SRS §9, §11 · System Architecture §10, §14 · Tech Stack (Swashbuckle row)

This document is the single source of truth for how the Flutter client and the ASP.NET Core backend communicate. Any change here must be communicated to Dana (Flutter) before merging, according to the Project Rules communication clause.

---

## 1. Transport

* REST over HTTPS only.
* No GraphQL and no gRPC.
* All request/response bodies use `application/json` with UTF-8 encoding.
* Base path in all environments: `/api`.

---

## 2. Resource Naming

| Rule                                                        | Example                           |
| ----------------------------------------------------------- | --------------------------------- |
| Plural nouns for collections                                | `GET /api/trips`                  |
| Singular resource by ID                                     | `GET /api/trips/{id}`             |
| Nested resource under its parent                            | `GET /api/trips/{id}/itinerary`   |
| Actions that are not pure CRUD use verbs under the resource | `POST /api/trips/{id}/regenerate` |
| No verbs in resource names                                  | ❌ `/api/getTrips`                 |

### Planned / Agreed Module Routes

```text
/api/auth/register
/api/auth/login
/api/trips
/api/trips/{id}
/api/trips/{id}/itinerary
/api/trips/{id}/cost-estimate
/api/destinations
/api/destinations/suggestions
```

> These routes describe the agreed API structure. They do not mean that every route is already implemented.

---

## 3. HTTP Methods & Status Codes

| Method | Use                        | Success Code                 |
| ------ | -------------------------- | ---------------------------- |
| GET    | Read                       | 200                          |
| POST   | Create / trigger an action | 201 (create) or 200 (action) |
| PATCH  | Partial update             | 200                          |
| DELETE | Soft-delete / archive      | 200 or 204                   |

No PUT is used. Updates are handled using PATCH because Triply uses partial updates.

---

## 4. DTO Shape

* Clients never receive EF Core entities directly.
* Every endpoint should use a dedicated Request DTO and Response DTO.
* JSON field names use `camelCase`.
* ASP.NET Core's default JSON serializer handles the conversion from C# `PascalCase`.
* Date-only values use:

```text
yyyy-MM-dd
```

Example:

```text
2026-11-01
```

* Timestamps use ISO-8601 UTC:

```text
2026-09-14T10:00:00Z
```

* Money uses a decimal amount together with a currency code.
* IDs are serialized according to their underlying type. GUIDs are serialized as strings.

### Example — Trip Response DTO

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "GENERATED",
  "destination": {
    "id": 12,
    "name": "Petra",
    "countryName": "Jordan"
  },
  "startDate": "2026-11-01",
  "endDate": "2026-11-05",
  "travelerCount": 2,
  "totalEstimatedCost": {
    "amount": 850.00,
    "currency": "USD"
  },
  "isEstimated": true
}
```

---

## 5. Error Format — ProblemDetails

API errors follow the ASP.NET Core `ProblemDetails` / `ValidationProblemDetails` format where applicable.

### Validation Error — `400`

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "email": [
      "'Email' is not a valid email address."
    ],
    "travelerCount": [
      "'Traveler Count' must be greater than 0."
    ]
  }
}
```

### Authentication Error — `401`

The API does not reveal which authentication field was incorrect.

```json
{
  "title": "Invalid email or password.",
  "status": 401
}
```

### Forbidden — `403`

Example: accessing another user's trip.

```json
{
  "title": "You do not have access to this resource.",
  "status": 403
}
```

### Server Error — `500`

The API never returns a stack trace to the client.

```json
{
  "title": "An unexpected error occurred.",
  "status": 500,
  "instance": "/api/trips/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### Flutter Error Handling Rule

Flutter should branch on the HTTP `status` code instead of parsing the `title` text.

The `title` message may change, while HTTP status codes are stable.

---

## 6. Versioning

**Decision: No URL or header versioning in MVP.**

The backend and Flutter client are released together during development, so API versioning would add unnecessary complexity.

Versioning can be reconsidered if Triply later exposes a public or third-party API.

---

## 7. Authentication

Protected endpoints use JWT Bearer authentication.

### Header

```http
Authorization: Bearer <JWT_TOKEN>
```

Register and Login are public endpoints.

### JWT Configuration

```text
Token lifetime: 60 minutes
Issuer: Triply
Audience: TriplyClients
```

The authentication contract is documented in:

```text
docs/API Contract.md
```

---

## 8. Swagger / OpenAPI

Swagger is the main source for the currently implemented API endpoints.

### Swagger UI

```text
https://localhost:8080/swagger
```

### OpenAPI JSON

```text
https://localhost:8080/swagger/v1/swagger.json
```

Swagger is generated directly from the ASP.NET Core controllers and DTOs.

The API consumers can use Swagger UI to:

* View available endpoints
* Check request models
* Check response models
* Test endpoints
* Review authentication requirements

A static OpenAPI snapshot can also be maintained in:

```text
docs/openapi.yaml
```

if the team needs an offline API reference or client generation later.

---

## 9. Pagination

Pagination is not required for the current MVP because the expected dataset sizes are small.

If pagination is added later, the agreed format is:

### Request

```text
GET /api/trips?page=1&pageSize=20
```

### Response

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "total": 42
}
```

---

## 10. Change Process

Any API contract change must be communicated to the Flutter track before merging.

When an endpoint changes, update the corresponding API contract documentation and notify Dana.

The related Trello card should also record important API contract decisions.
