# Trip Module

Everything about a trip except AI generation.

| Action | Endpoint |
|---|---|
| Create | `POST /api/trips` |
| List mine / get one | `GET /api/trips` · `GET /api/trips/{id}` |
| Update details | `PUT /api/trips/{id}` |
| Title / cover | `PATCH /api/trips/{id}` |
| Choose destination (budget-first) | `PATCH /api/trips/{id}/destination` |
| Save / archive / restore | `POST /api/trips/{id}/save` · `/archive` · `/restore` |

**Lifecycle:** `DRAFT → GENERATING → GENERATED → MODIFIED → SAVED → ARCHIVED` (rules in `TripLifecycle.cs`). Status changes only through these actions.

**Ownership:** another user's trip returns `404`.

**Version:** updates send `expectedVersion`; an old one returns `409`.

Files: `TripsController.cs`, `TripLifecycle.cs`, `Validators/TripValidators.cs`, `Dtos/TripDtos.cs`.
