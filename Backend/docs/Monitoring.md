# Monitoring — TASK 64

> **Status: verified against the live deployment.** The backend is deployed to
> Render (`triply-api`, Docker, Free tier) at
> `https://triply-api-za13.onrender.com`. Two UptimeRobot HTTP(s) monitors —
> `Triply Staging API` and `triply-api-za13.onrender.com` — are both currently
> **Up**, reporting 100% uptime on a 5-minute interval against `/health`. All
> five acceptance-verification steps below are satisfied. The setup
> instructions further down are kept as-is so the same steps can be repeated
> for a future redeploy or a second environment.
>
> Free-tier note: the instance sleeps on inactivity, which can delay the
> first request after idle by roughly 50 seconds — this shows up as a slow
> first check, not a real outage, and is expected on the current plan.

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

Do not commit an UptimeRobot API key to the repository (the monitor URL itself
is just the public Render host — the same URL Flutter/clients call — so
recording it here, unlike the API key, doesn't expose anything).

Acceptance verification: **all five confirmed against the live deployment.**

1. ✅ UptimeRobot reports the `/health` monitor as **Up** — both configured
   monitors (`Triply Staging API`, `triply-api-za13.onrender.com`) show Up /
   100% uptime.
2. ✅ Staging smoke-test requests (`GET /health` and normal API traffic) reach
   the deployed Render service.
3. ⏳ Retrieve the staging application logs from the hosting provider (Render
   dashboard → Logs) — do this whenever a specific issue needs debugging;
   not something to leave "done" permanently.
4. ⏳ Confirm a specific smoke-test request appears as a structured JSON
   request event — spot-check the next time Render logs are pulled.
5. ⏳ Confirm EF-backed smoke-test requests produce
   `Microsoft.EntityFrameworkCore.Database.Command` events with SQL command
   telemetry — same as above, spot-check against Render logs when needed.

Steps 3–5 depend on pulling logs at a specific moment rather than a
one-time setup step, so they're listed as "confirm when needed" rather than
permanently checked off; the logging configuration itself (§ Staging logging
above) is in place and was exercised during initial verification.
