#!/usr/bin/env python3
"""
Triply — Latency Testing: Gemini Flash vs Flash-Lite
Task: Latency Testing — Gemini Flash vs Flash-Lite
Owner: Adam Alafandi
Track: AI/ML

Measures real generation latency for gemini-3.6-flash and
gemini-3.5-flash-lite against >=20 representative prompts each, and reports
p50/p95 per model. Reuses the prompt-building, dataset, and schema logic
from AI/06-Gemini-Prototype/run_experiments.py so prompts are identical in
shape to what production will actually send.

This only measures the Gemini call itself (network + generation). Time
spent sleeping for 503/backoff retries is NOT counted toward latency (that's
a quota artifact, not model latency) but IS logged in the raw CSV.

Usage:
    # Run 20 NEW successful calls against one model (resumable across days --
    # free-tier daily quota is 20 requests/day/model, so budget one model's
    # 20 prompts per day, or use two separate API keys/projects):
    python latency_test.py --model gemini-3.6-flash --count 20
    python latency_test.py --model gemini-3.5-flash-lite --count 20

    # Once both models have >=20 successful rows in results/latency_raw.csv,
    # generate the report with no further API calls:
    python latency_test.py --report

    # See what would be sent without calling Gemini:
    python latency_test.py --model gemini-3.6-flash --count 20 --dry-run
"""

import argparse
import csv
import statistics
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

# --- Reuse the prototype's dataset/prompt/schema/call logic ---
SCRIPT_DIR = Path(__file__).resolve().parent
AI_DIR = SCRIPT_DIR.parent
PROTOTYPE_DIR = AI_DIR / "06-Gemini-Prototype"
sys.path.insert(0, str(PROTOTYPE_DIR))

try:
    import run_experiments as proto  # noqa: E402
except ImportError as e:
    print(f"ERROR: couldn't import run_experiments.py from {PROTOTYPE_DIR}: {e}")
    sys.exit(1)

RESULTS_DIR = SCRIPT_DIR / "results"
RAW_CSV = RESULTS_DIR / "latency_raw.csv"
REPORT_MD = SCRIPT_DIR / "LATENCY_REPORT.md"
CSV_FIELDS = ["timestamp_utc", "model", "prompt_id", "mode", "attempts", "latency_seconds", "status", "error"]

MODELS = ["gemini-3.6-flash", "gemini-3.5-flash-lite"]

# ============================================================================
# PROMPT POOL — 20 distinct, representative prompts (roughly matching the
# DESTINATION_FIRST : BUDGET_FIRST mix already used in the prototype
# experiments: ~70/30). Reuses the 12 scenarios from run_experiments.py and
# adds 8 more to reach 20 without duplicating any existing scenario id.
# ============================================================================

PROMPT_POOL = proto.SCENARIOS + [
    {
        "id": "M", "name": "Amman 2-day weekend", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Amman", "start_date": "2026-11-20", "end_date": "2026-11-21",
                   "budget_amount": "250", "budget_currency": "JOD", "interests": "Food, Culture"},
    },
    {
        "id": "N", "name": "Paris solo history trip", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Paris", "start_date": "2026-10-05", "end_date": "2026-10-08",
                   "budget_amount": "600", "budget_currency": "EUR", "interests": "History"},
    },
    {
        "id": "O", "name": "New York shopping weekend", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "New York", "start_date": "2026-11-01", "end_date": "2026-11-03",
                   "budget_amount": "1200", "budget_currency": "USD", "interests": "Shopping, Food"},
    },
    {
        "id": "P", "name": "Amman adventure 4-day", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Amman", "start_date": "2026-12-01", "end_date": "2026-12-04",
                   "budget_amount": "500", "budget_currency": "JOD", "interests": "Adventure, Nature"},
    },
    {
        "id": "Q", "name": "Budget-first relaxed 3-day", "mode": "BUDGET_FIRST",
        "params": {"start_date": "2026-10-15", "end_date": "2026-10-17",
                   "budget_amount": "700", "budget_currency": "USD", "interests": "Relaxation, Culture"},
    },
    {
        "id": "R", "name": "Budget-first shoestring 2-day", "mode": "BUDGET_FIRST",
        "params": {"start_date": "2026-11-25", "end_date": "2026-11-26",
                   "budget_amount": "120", "budget_currency": "USD", "interests": "Food, History"},
    },
    {
        "id": "S", "name": "Paris family 5-day", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Paris", "start_date": "2026-12-10", "end_date": "2026-12-14",
                   "budget_amount": "1800", "budget_currency": "EUR", "interests": "Culture, Food, Shopping"},
    },
    {
        "id": "T", "name": "Budget-first generous 4-day", "mode": "BUDGET_FIRST",
        "params": {"start_date": "2026-12-05", "end_date": "2026-12-08",
                   "budget_amount": "1500", "budget_currency": "USD", "interests": "Adventure, Shopping"},
    },
]

assert len({s["id"] for s in PROMPT_POOL}) == len(PROMPT_POOL), "duplicate scenario id in PROMPT_POOL"
assert len(PROMPT_POOL) >= 20, "need at least 20 prompts in the pool"


def build_prompt(dataset: dict, scenario: dict) -> tuple[str, str]:
    """Build (system, user) prompt text for one scenario, mode-aware."""
    if scenario["mode"] == "DESTINATION_FIRST":
        return proto.build_destination_first_prompt(dataset, **scenario["params"])
    return proto.build_budget_first_prompt(dataset, **scenario["params"])


def timed_call(model_name: str, system: str, user: str, schema: dict, mode: str,
                max_retries: int = 3) -> dict:
    """
    Call Gemini once, timing ONLY the request/response — 503 backoff sleeps
    are excluded from latency_seconds but attempts is recorded so retries are
    visible in the raw data.
    """
    import os
    try:
        from google import genai
        from google.genai import types
    except ImportError:
        print("ERROR: google-genai not installed. Run: pip install google-genai")
        sys.exit(1)

    api_key = os.environ.get("GEMINI_API_KEY", "")
    if not api_key:
        raise ValueError("GEMINI_API_KEY environment variable not set")
    client = genai.Client(api_key=api_key)

    import json
    schema_copy = json.loads(json.dumps(schema))
    schema_copy["properties"]["destination_options"]["maxItems"] = 1 if mode == "DESTINATION_FIRST" else 3

    attempts = 0
    last_error = None
    for attempt in range(max_retries):
        attempts += 1
        try:
            start = time.perf_counter()
            client.models.generate_content(
                model=model_name,
                contents=user,
                config=types.GenerateContentConfig(
                    system_instruction=system,
                    response_mime_type="application/json",
                    response_json_schema=schema_copy,
                    temperature=0.4,
                ),
            )
            latency = time.perf_counter() - start
            return {"status": "SUCCESS", "latency_seconds": round(latency, 3),
                    "attempts": attempts, "error": ""}
        except Exception as e:
            last_error = e
            error_str = str(e)
            if "503" in error_str or "UNAVAILABLE" in error_str or "overloaded" in error_str.lower():
                wait = (2 ** attempt) * 5
                print(f"    Rate-limited (attempt {attempts}/{max_retries}), waiting {wait}s (not counted)...")
                time.sleep(wait)
                continue
            return {"status": "FAILED", "latency_seconds": None, "attempts": attempts,
                     "error": error_str[:300]}
    return {"status": "FAILED", "latency_seconds": None, "attempts": attempts,
             "error": f"exhausted retries: {last_error}"}


def load_existing_rows() -> list[dict]:
    if not RAW_CSV.exists():
        return []
    with open(RAW_CSV, encoding="utf-8") as f:
        return list(csv.DictReader(f))


def append_row(row: dict) -> None:
    RESULTS_DIR.mkdir(exist_ok=True)
    is_new = not RAW_CSV.exists()
    with open(RAW_CSV, "a", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=CSV_FIELDS)
        if is_new:
            writer.writeheader()
        writer.writerow(row)


def run(model_name: str, count: int, dry_run: bool, delay: float) -> None:
    dataset = proto.load_dataset()
    schema = proto.load_schema()
    existing = load_existing_rows()
    already_done_ids = {r["prompt_id"] for r in existing
                         if r["model"] == model_name and r["status"] == "SUCCESS"}

    todo = [s for s in PROMPT_POOL if s["id"] not in already_done_ids][:count]
    if len(todo) < count:
        print(f"WARNING: only {len(todo)} unused prompts left in the pool for "
              f"{model_name} ({len(already_done_ids)} already succeeded). "
              f"Add more scenarios to PROMPT_POOL if you need more.")

    print(f"Model: {model_name} | already succeeded: {len(already_done_ids)} | "
          f"running {len(todo)} more (target {count})\n")

    for i, scenario in enumerate(todo, 1):
        system, user = build_prompt(dataset, scenario)
        print(f"[{i}/{len(todo)}] {scenario['id']} — {scenario['name']} ({scenario['mode']})")

        if dry_run:
            print("    (dry-run, not calling Gemini)")
            continue

        result = timed_call(model_name, system, user, schema, scenario["mode"])
        row = {
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            "model": model_name,
            "prompt_id": scenario["id"],
            "mode": scenario["mode"],
            "attempts": result["attempts"],
            "latency_seconds": result["latency_seconds"],
            "status": result["status"],
            "error": result["error"],
        }
        append_row(row)

        if result["status"] == "SUCCESS":
            print(f"    -> {result['latency_seconds']}s (attempts: {result['attempts']})")
        else:
            print(f"    -> FAILED: {result['error']}")

        if i < len(todo):
            time.sleep(delay)

    print(f"\nDone. Raw results in {RAW_CSV}")


def percentile(values: list[float], pct: float) -> float:
    if not values:
        return float("nan")
    return statistics.quantiles(values, n=100, method="inclusive")[pct - 1] if len(values) > 1 else values[0]


def generate_report() -> None:
    rows = load_existing_rows()
    if not rows:
        print("No results yet — run some prompts first.")
        return

    lines = [
        "# Latency Test Report — Gemini Flash vs Flash-Lite",
        "",
        "**Task:** Latency Testing — Gemini Flash vs Flash-Lite",
        "**Owner:** Adam Alafandi",
        "**Reference:** Tech Stack §8 item 3; SRS NFR-PERF-001",
        f"**Generated:** {datetime.now(timezone.utc).isoformat()}",
        "",
        "**Method:** each row times a single `generate_content` call against the "
        "real finalized schema and dataset context, using the same prompt-building "
        "logic as `AI/06-Gemini-Prototype/run_experiments.py`. Retry/backoff sleep "
        "time for 503 errors is excluded from `latency_seconds` (it's a quota "
        "artifact, not model latency) but retried attempts are recorded.",
        "",
        "## Summary",
        "",
        "| Model | Successful samples | p50 (s) | p95 (s) | min (s) | max (s) | Failures |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ]

    per_model = {}
    for r in rows:
        per_model.setdefault(r["model"], {"latencies": [], "failures": 0})
        if r["status"] == "SUCCESS" and r["latency_seconds"]:
            per_model[r["model"]]["latencies"].append(float(r["latency_seconds"]))
        else:
            per_model[r["model"]]["failures"] += 1

    for model in MODELS:
        stats = per_model.get(model, {"latencies": [], "failures": 0})
        lat = sorted(stats["latencies"])
        n = len(lat)
        if n == 0:
            lines.append(f"| `{model}` | 0 | — | — | — | — | {stats['failures']} |")
            continue
        p50 = round(percentile(lat, 50), 2)
        p95 = round(percentile(lat, 95), 2)
        lines.append(
            f"| `{model}` | {n} | {p50} | {p95} | {round(lat[0], 2)} | "
            f"{round(lat[-1], 2)} | {stats['failures']} |"
        )

    lines += [
        "",
        "## Acceptance criteria check",
        "",
    ]
    for model in MODELS:
        n = len(per_model.get(model, {"latencies": []})["latencies"])
        status = "✅ Met (≥20 samples)" if n >= 20 else f"⬜ Not yet met ({n}/20 samples)"
        lines.append(f"- `{model}`: {status}")

    lines += [
        "",
        "## Raw data",
        "",
        f"See `{RAW_CSV.relative_to(AI_DIR)}` for the per-call log "
        "(timestamp, prompt id, mode, attempts, latency, status, error).",
        "",
        "## Notes for the model-choice decision (Tech Stack §8 item 3)",
        "",
        "- Fill in once both rows above show ≥20 samples: which model meets the "
        "target latency, and whether the gap justifies trading Flash's quality "
        "for Flash-Lite's speed/higher RPM.",
        "- This report's p50/p95 figures are the input for setting NFR-PERF-001's "
        "target number (currently TBD in the SRS).",
        "",
    ]

    REPORT_MD.write_text("\n".join(lines), encoding="utf-8")
    print(f"Report written to {REPORT_MD}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--model", choices=MODELS, help="Model to test")
    parser.add_argument("--count", type=int, default=20, help="How many NEW successful calls to run")
    parser.add_argument("--dry-run", action="store_true", help="Build prompts without calling Gemini")
    parser.add_argument("--delay", type=float, default=4.0, help="Seconds to sleep between calls (respect RPM)")
    parser.add_argument("--report", action="store_true", help="Only regenerate the report from existing raw data")
    args = parser.parse_args()

    if args.report:
        generate_report()
        return 0

    if not args.model:
        parser.error("--model is required unless --report is passed")

    run(args.model, args.count, args.dry_run, args.delay)
    generate_report()
    return 0


if __name__ == "__main__":
    sys.exit(main())