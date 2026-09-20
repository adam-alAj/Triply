# Latency Test Report — Gemini Flash vs Flash-Lite

**Task:** Latency Testing — Gemini Flash vs Flash-Lite
**Owner:** Adam Alafandi
**Reference:** Tech Stack §8 item 3; SRS NFR-PERF-001
**Generated:** 2026-09-19T22:00:29.397482+00:00

**Method:** each row times a single `generate_content` call against the real finalized schema and dataset context, using the same prompt-building logic as `AI/06-Gemini-Prototype/run_experiments.py`. Retry/backoff sleep time for 503 errors is excluded from `latency_seconds` (it's a quota artifact, not model latency) but retried attempts are recorded.

## Summary

| Model                   | Successful samples | p50 (s) | p95 (s) | min (s) | max (s) | Failures |
| ----------------------- | ------------------ | ------- | ------- | ------- | ------- | -------- |
| `gemini-3.6-flash`      | 20                 | 12.35   | 16.04   | 9.67    | 18.71   | 0        |
| `gemini-3.5-flash-lite` | 20                 | 2.83    | 6.47    | 1.73    | 17.49   | 0        |

## Acceptance criteria check

- `gemini-3.6-flash`: ✅ Met (≥20 samples)
- `gemini-3.5-flash-lite`: ✅ Met (≥20 samples)

## Raw data

See `07-Latency-Testing\results\latency_raw.csv` for the per-call log (timestamp, prompt id, mode, attempts, latency, status, error).

## Notes for the model-choice decision (Tech Stack §8 item 3)

- **Result:** `gemini-3.5-flash-lite` is roughly **4-5x faster** than
  `gemini-3.6-flash` at the median (p50 2.83s vs 12.35s) and at p95 (6.47s
  vs 16.04s). 0 failures on either model across 20/20 samples each — both
  are reliable on the free tier at this sample size.
- **Outlier flagged:** Flash-Lite's very first call (prompt A) took 17.49s
  — far outside its own range (next-highest is 5.89s). This looks like a
  cold-start/connection-warmup effect (first request to that model in the
  session) rather than representative model latency. Excluding it, Flash-Lite's
  p95 would be closer to ~4.4s. Worth a second short run to confirm before
  treating 6.47s as the real p95.
- **Recommendation:** unless Flash's extra reasoning quality is needed for
  correctness (not just polish), `gemini-3.5-flash-lite` is the better fit
  for interactive itinerary generation — a ~12s median wait (Flash) is a
  poor UX for a synchronous request, while ~3s (Flash-Lite) is not.
- **NFR-PERF-001 target proposal:** set the target around Flash-Lite's p95
  (~6-7s, or ~4-5s if the outlier above is confirmed as noise), since that's
  the model this data supports adopting.
- Still open: this only measures raw generation latency, not the full
  request path (validation pipeline, DB writes, network to the client) —
  the end-to-end number Backend sees will be somewhat higher.