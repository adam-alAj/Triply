# Latency Testing — Gemini Flash vs Flash-Lite

**Task objective:** Measure real generation latency for `gemini-3.6-flash`
and `gemini-3.5-flash-lite` against representative prompts, and produce a
p50/p95 report for both.

**Owner:** Adam Alafandi · **Track:** AI/ML · **Priority:** P1 · **Sprint:** 2

**Dependencies:** `AI/06-Gemini-Prototype/` (Prototype Gemini Calls in Google
AI Studio) — this reuses its dataset loading, prompt building, and schema
logic so the timed prompts are shaped exactly like production calls.

**Blocks:** Wiring real Gemini itinerary generation end-to-end, and the
Tech Stack §8 item 3 model decision / SRS NFR-PERF-001 target.

**Acceptance criteria:** a latency report with p50/p95 for both models
against at least 20 sample prompts each.

## Why this can't run from this chat

Generating real numbers means actually calling the Gemini API, and this
sandbox's network is locked to an allowlist that doesn't include Google's
API domain — so the script below has to be run on your own machine, with
your own `GEMINI_API_KEY` (same setup as `run_experiments.py`).

## How to run it

```bash
pip install google-genai jsonschema
export GEMINI_API_KEY="..."   # same key used for the prototype experiments

cd AI/07-Latency-Testing

# Run 20 timed calls against Flash
python latency_test.py --model gemini-3.6-flash --count 20

# Run 20 timed calls against Flash-Lite
python latency_test.py --model gemini-3.5-flash-lite --count 20

# Regenerate the report at any point (no API calls) once you have data
python latency_test.py --report
```

- Each run appends to `results/latency_raw.csv`, so you can stop and resume
  — it skips prompt ids that already succeeded for that model.
- **Free-tier quota is 20 requests/day/model** (per
  `AI/06-Gemini-Prototype/EXPERIMENT_REPORT.md`). Since the target is 20
  successful samples per model, run one model's batch per day (or use two
  separate API keys/projects if you want to do both in one day). A failed
  call (e.g. a 503 that exhausts retries) still counts against the daily
  quota but is *not* counted as one of the 20 successful samples, so budget
  a little slack.
- `--delay` (default 4s) is the pause between calls — raise it if you start
  seeing 429/rate errors; the exact RPM for these models isn't documented
  anywhere in this repo, so treat 4s as a starting point, not a guarantee.
- `latency_seconds` measures only the `generate_content` call itself.
  503-retry backoff sleeps are excluded from the number but every attempt
  is logged, so retries are still visible in the raw CSV.
- `python latency_test.py --report` writes `LATENCY_REPORT.md` — that's
  the deliverable for this task.

## Prompt pool

20 distinct prompts in `PROMPT_POOL` (`latency_test.py`): the 12 scenarios
already used in `run_experiments.py` (A–L) plus 8 new ones (M–T), keeping
roughly the same DESTINATION_FIRST : BUDGET_FIRST mix (15:5) so the sample
is representative of real traffic, not skewed toward one mode.