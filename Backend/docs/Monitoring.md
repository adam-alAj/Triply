# Monitoring

Simple monitoring for the deployed API.

## Current state

| Item | Status |
|---|---|
| Hosting | Render (Docker, free plan) — https://triply-api-za13.onrender.com |
| Health check | `GET /health` → `{"status":"ok"}` (public) |
| UptimeRobot | 2 HTTP monitors on `/health`, every 5 min — both **Up**, 100% |
| Logs | JSON console logs in Staging (view them in the Render dashboard → Logs) |

Monitors: `Triply Staging API` and `triply-api-za13.onrender.com`.
The free Render plan sleeps when idle; the 5-minute checks also help keep it awake, but the first request after a long idle can still take ~50 seconds.

## What is logged in Staging

Config file: `Triply.Api/appsettings.Staging.json`. It turns on when `ASPNETCORE_ENVIRONMENT=Staging`.

- Structured `ILogger` events in JSON.
- One request line per call: method, path, status code, duration.
- SQL commands from EF Core (`Microsoft.EntityFrameworkCore.Database.Command`, level `Information`).
- Sensitive parameter values are **not** logged (`EnableSensitiveDataLogging` is never used).

## Add a new UptimeRobot monitor

1. Monitor type: **HTTP(s)**
2. URL: `https://<host>/health`
3. Interval: 5 minutes, expected status `200`
4. Optional keyword check: `ok`

Do not commit UptimeRobot API keys.

## Acceptance checklist

- [x] UptimeRobot shows the `/health` monitors as **Up**
- [x] Smoke test `GET /health` works
- [x] Render logs show the request as a JSON event (check Render → Logs; needs `ASPNETCORE_ENVIRONMENT=Staging`)
- [x] Database calls show `Microsoft.EntityFrameworkCore.Database.Command` events (same place)
- [x] Real Gemini generation works on the deployed service (24 Sep 2026)
