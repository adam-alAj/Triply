# Monitoring — TASK 64

## Staging logging

The API uses the built-in ASP.NET Core console logger with the JSON formatter in
`Staging`.

Staging logging includes:

- Structured `ILogger` events in JSON.
- ASP.NET Core request/response telemetry for smoke-test requests.
- EF Core SQL command/query logs through
  `Microsoft.EntityFrameworkCore.Database.Command` at `Information`.

Sensitive EF parameter values are not enabled, so `EnableSensitiveDataLogging()`
is intentionally not used.

The configuration lives in:

```text
Triply.Api/appsettings.Staging.json
```

The application only enables HTTP request logging when:

```text
ASPNETCORE_ENVIRONMENT=Staging
```

## Health check

The backend exposes:

```text
GET /health
```

Expected response:

```json
{"status":"ok"}
```

The endpoint is intentionally unauthenticated so an external uptime monitor can
check the staging service.

## UptimeRobot

After the backend is deployed to staging, create an UptimeRobot **HTTP(s) monitor**
with:

- **Monitor Type:** HTTP(s)
- **URL:** `https://<STAGING_HOST>/health`
- **Monitoring Interval:** 5 minutes
- **Expected status:** HTTP 200
- **Keyword check:** optional; if enabled, check for `status` / `ok`

Do not commit a real staging URL or UptimeRobot API key to the repository.

Acceptance verification:

1. UptimeRobot reports the `/health` monitor as **Up**.
2. Send a staging smoke-test request such as `GET /health`.
3. Retrieve the staging application logs from the hosting provider.
4. Confirm the smoke-test request appears as a structured JSON request event.
5. Confirm EF-backed smoke-test requests produce `Microsoft.EntityFrameworkCore.Database.Command`
   events with SQL command telemetry.

UptimeRobot itself is an external service configuration and therefore must be
created against the actual deployed staging URL after the staging deployment is
available.
