#!/usr/bin/env python3
"""
Triply — Gemini Prototype Experiments
Task: Prototype Gemini Calls in Google AI Studio
Owner: Adam Alafandi
Track: AI/ML
Schema version: 2.0.0

Runs controlled prototype experiments against Gemini Flash-family models using
the project's actual itinerary prompts, finalized JSON schema, and real internal
dataset context.

Acceptance target: At least 10 sample generations produce schema-valid JSON.

Usage:
    python run_experiments.py                  # Run all experiments
    python run_experiments.py --scenario A     # Run a specific scenario
    python run_experiments.py --model MODEL    # Override model
    python run_experiments.py --dry-run        # Build prompts without calling Gemini
"""

import csv
import json
import os
import sys
import time
from datetime import datetime, timezone, timedelta
from pathlib import Path
from typing import Any

# --- Paths ---
SCRIPT_DIR = Path(__file__).resolve().parent
AI_DIR = SCRIPT_DIR.parent
SCHEMA_PATH = AI_DIR / "02-Prompt-Engineering" / "json-schemas" / "triply-trip-plan-generation.schema.json"
DATASET_DIR = AI_DIR / "01-Dataset" / "curated-data"
RESULTS_DIR = SCRIPT_DIR / "results"
PROMPTS_DIR = SCRIPT_DIR / "prompts"

# --- Imports ---
try:
    from google import genai
    from google.genai import types
except ImportError:
    print("ERROR: google-genai not installed. Run: pip install google-genai")
    sys.exit(1)

try:
    from jsonschema import validate, ValidationError, Draft202012Validator
except ImportError:
    print("ERROR: jsonschema not installed. Run: pip install jsonschema")
    sys.exit(1)


# ============================================================================
# DATA LOADING
# ============================================================================

def load_csv(filename: str) -> list[dict]:
    """Load a CSV from the curated-data directory."""
    filepath = DATASET_DIR / filename
    with open(filepath, encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        return list(reader)


def load_schema() -> dict:
    """Load the finalized JSON Schema."""
    with open(SCHEMA_PATH, encoding="utf-8") as f:
        schema = json.load(f)
    Draft202012Validator.check_schema(schema)
    return schema


def load_dataset() -> dict:
    """Load all relevant dataset files."""
    return {
        "destinations": load_csv("Destination.csv"),
        "places": load_csv("Place.csv"),
        "place_categories": load_csv("PlaceCategory.csv"),
        "cost_categories": load_csv("CostCategory.csv"),
        "currencies": load_csv("Currency.csv"),
        "countries": load_csv("Country.csv"),
    }


# ============================================================================
# CONTEXT BUILDING
# ============================================================================

CATEGORY_MAP = {
    "1": "ATTRACTION",
    "2": "RESTAURANT",
    "3": "ACTIVITY",
    "4": "ACCOMMODATION",
    "5": "TRANSPORT",
}


def build_place_context(dataset: dict, destination_id: str) -> str:
    """Build the place list context for a specific destination, grouped by category."""
    places = [
        p for p in dataset["places"]
        if p["destination_id"] == destination_id and p["is_active"] == "True"
    ]

    grouped: dict[str, list] = {}
    for p in places:
        cat_code = CATEGORY_MAP.get(p["place_category_id"], "UNKNOWN")
        if cat_code not in grouped:
            grouped[cat_code] = []
        grouped[cat_code].append(p)

    lines = []
    for cat in ["ACCOMMODATION", "RESTAURANT", "ATTRACTION", "ACTIVITY", "TRANSPORT"]:
        if cat in grouped:
            lines.append(f"[{cat}]")
            for p in grouped[cat]:
                lines.append(f"- {p['name']}")
            lines.append("")

    return "\n".join(lines)


def build_budget_first_context(dataset: dict) -> str:
    """Build context for all supported destinations (budget-first mode)."""
    destinations = [d for d in dataset["destinations"] if d["is_supported"] == "True"]
    lines = []

    for dest in destinations:
        lines.append(f"=== {dest['name']} ===")
        lines.append(f"  {dest['description']}")
        lines.append("")

        places = [
            p for p in dataset["places"]
            if p["destination_id"] == dest["id"] and p["is_active"] == "True"
        ]

        grouped: dict[str, list] = {}
        for p in places:
            cat_code = CATEGORY_MAP.get(p["place_category_id"], "UNKNOWN")
            if cat_code not in grouped:
                grouped[cat_code] = []
            grouped[cat_code].append(p)

        for cat in ["ACCOMMODATION", "RESTAURANT", "ATTRACTION", "ACTIVITY", "TRANSPORT"]:
            if cat in grouped:
                lines.append(f"  [{cat}]")
                for p in grouped[cat]:
                    lines.append(f"  - {p['name']}")
        lines.append("")

    return "\n".join(lines)


# ============================================================================
# PROMPT CONSTRUCTION
# ============================================================================

DESTINATION_FIRST_SYSTEM = """You are Triply's trip-planning assistant. You generate a structured trip
itinerary in JSON only, following the provided response schema exactly.
Follow these rules with zero exceptions:

1. Never output any database ID of any kind (no Place.id, no
   Destination.id). Refer to places and destinations only by their exact
   name.
2. Every `place_name` and `destination_name` you output must be copied
   exactly, character-for-character, from the place list given to you
   below. Do not invent, merge, abbreviate, or guess a name. If you are
   not certain a place is on the list, do not use it.
3. Never output any price, cost, currency amount, or cost estimate -- not
   as a number, not as a string, not inside `notes`. All costs are
   computed separately from internal pricing data.
4. Never include confidence scores, explanations of your reasoning, or
   any field not defined in the response schema.
5. Each day must include at least one RESTAURANT-category place. The full
   plan must include at least one TRANSPORT-category place somewhere
   across all days.
6. Exactly one ACCOMMODATION-category place must be chosen and returned
   only inside the `accommodation` object -- never repeated inside `days`.
7. Return exactly one entry in `destination_options`, for the destination
   given below.
8. `days` must have exactly {day_count} entries, `day_number`
   1-indexed with no gaps, and each `date` consistent with the trip's
   date range.
9. Set `planning_mode` to "DESTINATION_FIRST".
10. Return ONLY valid JSON. No markdown fences, no commentary, no explanation."""

BUDGET_FIRST_SYSTEM = """You are Triply's trip-planning assistant. You generate 1 to 3 candidate
trip itineraries in JSON only, following the provided response schema
exactly. Follow these rules with zero exceptions:

1. Never output any database ID of any kind (no Place.id, no
   Destination.id). Refer to places and destinations only by their exact
   name.
2. Every `place_name` and `destination_name` you output must be copied
   exactly, character-for-character, from the place lists given to you
   below. Do not invent, merge, abbreviate, or guess a name. If you are
   not certain a place is on the list, do not use it. Only use
   destinations from the supported destination list below.
3. Never output any price, cost, currency amount, or cost estimate -- not
   as a number, not as a string, not inside `notes`. All costs are
   computed separately from internal pricing data.
4. Never include confidence scores, explanations of your reasoning, or
   any field not defined in the response schema.
5. Each day, in each destination option, must include at least one
   RESTAURANT-category place. Each destination option's full plan must
   include at least one TRANSPORT-category place somewhere across all its
   days.
6. Exactly one ACCOMMODATION-category place must be chosen per
   destination option, returned only inside that option's `accommodation`
   object -- never repeated inside `days`.
7. Return between 1 and 3 entries in `destination_options`, each for a
   different destination from the supported list below. Each option must
   be a complete, self-contained plan.
8. Within each destination option, `days` must have exactly
   {day_count} entries, `day_number` 1-indexed with no gaps,
   and each `date` consistent with the trip's date range.
9. Set `planning_mode` to "BUDGET_FIRST".
10. Return ONLY valid JSON. No markdown fences, no commentary, no explanation."""


def build_destination_first_prompt(
    dataset: dict,
    destination_name: str,
    start_date: str,
    end_date: str,
    budget_amount: str,
    budget_currency: str,
    interests: str,
) -> tuple[str, str]:
    """Build system instruction and user prompt for DESTINATION_FIRST mode."""
    dest = next(d for d in dataset["destinations"] if d["name"] == destination_name)
    day_count = (datetime.strptime(end_date, "%Y-%m-%d") - datetime.strptime(start_date, "%Y-%m-%d")).days + 1

    system = DESTINATION_FIRST_SYSTEM.format(day_count=day_count)
    place_context = build_place_context(dataset, dest["id"])

    user = f"""Plan a trip to {destination_name} for a traveler with the following preferences:

- Trip dates: {start_date} to {end_date}
- Budget: {budget_amount} {budget_currency} (context only -- do not mention, estimate, or calculate any cost in your response)
- Interests: {interests}

Only use places from the list below. Do not use any place that is not on this list, and do not use a place from a different destination:

{place_context}

Return your response as JSON matching the required response schema exactly."""

    return system, user


def build_budget_first_prompt(
    dataset: dict,
    start_date: str,
    end_date: str,
    budget_amount: str,
    budget_currency: str,
    interests: str,
) -> tuple[str, str]:
    """Build system instruction and user prompt for BUDGET_FIRST mode."""
    day_count = (datetime.strptime(end_date, "%Y-%m-%d") - datetime.strptime(start_date, "%Y-%m-%d")).days + 1

    system = BUDGET_FIRST_SYSTEM.format(day_count=day_count)
    context = build_budget_first_context(dataset)

    user = f"""Suggest 1 to 3 destinations for a traveler with the following preferences:

- Trip dates: {start_date} to {end_date}
- Budget: {budget_amount} {budget_currency} for the whole trip (context only -- do not mention, estimate, or calculate any cost in your response)
- Interests: {interests}

Choose only from the supported destinations and places below. Do not use any destination or place that is not on this list:

{context}

Return your response as JSON matching the required response schema exactly."""

    return system, user


# ============================================================================
# VALIDATION
# ============================================================================

def validate_json_parse(raw_text: str) -> tuple[bool, Any | None, str | None]:
    """Extract and parse JSON from Gemini response."""
    text = raw_text.strip()

    # Strip markdown fences if present
    if text.startswith("```"):
        first_newline = text.index("\n")
        text = text[first_newline + 1:]
    if text.endswith("```"):
        text = text[:-3]
    text = text.strip()

    try:
        parsed = json.loads(text)
        return True, parsed, None
    except json.JSONDecodeError as e:
        return False, None, f"JSON parse error: {e}"


def validate_schema_response(schema: dict, instance: Any) -> tuple[bool, str | None]:
    """Validate parsed JSON against the schema."""
    try:
        validate(instance=instance, schema=schema, cls=Draft202012Validator)
        return True, None
    except ValidationError as e:
        path = " -> ".join(str(p) for p in e.absolute_path) if e.absolute_path else "(root)"
        return False, f"[{path}] {e.message}"


def validate_grounding(dataset: dict, instance: Any) -> tuple[bool, list[str]]:
    """Check that all place_name and destination_name values exist in the dataset."""
    errors = []
    valid_place_names = {
        p["name"] for p in dataset["places"]
        if p["is_active"] == "True"
    }
    valid_dest_names = {
        d["name"] for d in dataset["destinations"]
        if d["is_supported"] == "True"
    }

    accommodation_categories = {
        p["name"] for p in dataset["places"]
        if p["place_category_id"] == "4" and p["is_active"] == "True"
    }
    restaurant_names = {
        p["name"] for p in dataset["places"]
        if p["place_category_id"] == "2" and p["is_active"] == "True"
    }
    transport_names = {
        p["name"] for p in dataset["places"]
        if p["place_category_id"] == "5" and p["is_active"] == "True"
    }

    if not isinstance(instance, dict):
        return False, ["Root is not an object"]

    planning_mode = instance.get("planning_mode")
    if planning_mode not in ("DESTINATION_FIRST", "BUDGET_FIRST"):
        errors.append(f"Invalid planning_mode: {planning_mode}")

    options = instance.get("destination_options", [])
    if not options:
        errors.append("No destination_options")

    for i, option in enumerate(options):
        prefix = f"destination_options[{i}]"

        dest_name = option.get("destination_name", "")
        if dest_name not in valid_dest_names:
            errors.append(f"{prefix}: destination_name '{dest_name}' not in dataset")

        acc = option.get("accommodation", {})
        acc_name = acc.get("place_name", "")
        if acc_name not in valid_place_names:
            errors.append(f"{prefix}: accommodation.place_name '{acc_name}' not in dataset")
        if acc_name not in accommodation_categories:
            errors.append(f"{prefix}: accommodation '{acc_name}' is not ACCOMMODATION category")

        days = option.get("days", [])
        has_restaurant_per_day = []
        has_transport = False

        for j, day in enumerate(days):
            day_prefix = f"{prefix}.days[{j}]"
            items = day.get("items", [])
            day_has_restaurant = False

            for item in items:
                pname = item.get("place_name", "")

                if pname not in valid_place_names:
                    errors.append(f"{day_prefix}: place_name '{pname}' not in dataset")

                if pname in restaurant_names:
                    day_has_restaurant = True
                if pname in transport_names:
                    has_transport = True
                if pname in accommodation_categories:
                    errors.append(f"{day_prefix}: ACCOMMODATION '{pname}' found in days")

            has_restaurant_per_day.append(day_has_restaurant)

        for j, has_rest in enumerate(has_restaurant_per_day):
            if not has_rest:
                errors.append(f"{prefix}.days[{j}]: no RESTAURANT-category place")

        if not has_transport:
            errors.append(f"{prefix}: no TRANSPORT-category place across all days")

    return len(errors) == 0, errors


def classify_failure(schema_valid: bool, grounding_valid: bool, errors: list[str]) -> str:
    """Classify the failure category."""
    if not schema_valid:
        err_str = " ".join(errors).lower()
        if "additional propert" in err_str:
            return "UNEXPECTED_FIELD"
        if "required property" in err_str or "is a required" in err_str:
            return "MISSING_REQUIRED_FIELD"
        if "is not of type" in err_str:
            return "WRONG_TYPE"
        if "is not one of" in err_str:
            return "INVALID_ENUM"
        return "SCHEMA_VIOLATION"

    if not grounding_valid:
        err_str = " ".join(errors).lower()
        if "not in dataset" in err_str:
            return "DATASET_HALLUCINATION"
        if "not accommodation" in err_str or "accommodation" in err_str:
            return "CATEGORY_VIOLATION"
        if "no restaurant" in err_str:
            return "MISSING_RESTAURANT"
        if "no transport" in err_str:
            return "MISSING_TRANSPORT"
        return "GROUNDING_VIOLATION"

    return "SUCCESS"


# ============================================================================
# EXPERIMENT SCENARIOS
# ============================================================================

SCENARIOS = [
    {
        "id": "A", "name": "Standard Amman 3-day", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Amman", "start_date": "2026-11-10", "end_date": "2026-11-12",
                   "budget_amount": "400", "budget_currency": "JOD", "interests": "History, Food"},
    },
    {
        "id": "B", "name": "Budget-constrained Amman", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Amman", "start_date": "2026-11-10", "end_date": "2026-11-12",
                   "budget_amount": "150", "budget_currency": "JOD", "interests": "History, Food"},
    },
    {
        "id": "C", "name": "Paris 4-day multi-interest", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Paris", "start_date": "2026-11-03", "end_date": "2026-11-06",
                   "budget_amount": "800", "budget_currency": "EUR", "interests": "History, Food, Shopping, Culture"},
    },
    {
        "id": "D", "name": "New York 3-day", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "New York", "start_date": "2026-12-20", "end_date": "2026-12-22",
                   "budget_amount": "1000", "budget_currency": "USD", "interests": "Culture, Food, Adventure"},
    },
    {
        "id": "E", "name": "Budget-first multi-destination", "mode": "BUDGET_FIRST",
        "params": {"start_date": "2026-11-10", "end_date": "2026-11-12",
                   "budget_amount": "500", "budget_currency": "USD", "interests": "History, Nature"},
    },
    {
        "id": "F", "name": "Amman consistency test 1", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Amman", "start_date": "2026-11-10", "end_date": "2026-11-12",
                   "budget_amount": "400", "budget_currency": "JOD", "interests": "History, Food"},
    },
    {
        "id": "G", "name": "Amman consistency test 2", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Amman", "start_date": "2026-11-10", "end_date": "2026-11-12",
                   "budget_amount": "400", "budget_currency": "JOD", "interests": "History, Food"},
    },
    {
        "id": "H", "name": "Paris 2-day short trip", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Paris", "start_date": "2026-11-03", "end_date": "2026-11-04",
                   "budget_amount": "300", "budget_currency": "EUR", "interests": "Food, Relaxation"},
    },
    {
        "id": "I", "name": "New York 5-day long trip", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "New York", "start_date": "2026-12-20", "end_date": "2026-12-24",
                   "budget_amount": "2000", "budget_currency": "USD", "interests": "Culture, Shopping, Food, Adventure"},
    },
    {
        "id": "J", "name": "Budget-first tight budget", "mode": "BUDGET_FIRST",
        "params": {"start_date": "2026-11-10", "end_date": "2026-11-11",
                   "budget_amount": "200", "budget_currency": "USD", "interests": "Food"},
    },
    {
        "id": "K", "name": "Amman nature/adventure 5-day", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Amman", "start_date": "2026-11-10", "end_date": "2026-11-14",
                   "budget_amount": "600", "budget_currency": "JOD", "interests": "Adventure, Nature, Relaxation"},
    },
    {
        "id": "L", "name": "Paris luxury 3-day", "mode": "DESTINATION_FIRST",
        "params": {"destination_name": "Paris", "start_date": "2026-11-03", "end_date": "2026-11-05",
                   "budget_amount": "3000", "budget_currency": "EUR", "interests": "Food, Culture, Shopping"},
    },
]


# ============================================================================
# GEMINI CALL
# ============================================================================

def call_gemini(
    model_name: str,
    system_instruction: str,
    user_prompt: str,
    schema: dict,
    planning_mode: str,
    temperature: float = 0.4,
    max_retries: int = 3,
) -> tuple[str, dict | None]:
    """Call Gemini using google-genai with retry logic for 503 errors."""
    api_key = os.environ.get("GEMINI_API_KEY", "")
    if not api_key:
        raise ValueError("GEMINI_API_KEY environment variable not set")

    client = genai.Client(api_key=api_key)

    # Set maxItems per planning_mode (Contract section 5, step 0)
    schema_copy = json.loads(json.dumps(schema))
    schema_copy["properties"]["destination_options"]["maxItems"] = (
        1 if planning_mode == "DESTINATION_FIRST" else 3
    )

    last_error = None
    for attempt in range(max_retries):
        try:
            response = client.models.generate_content(
                model=model_name,
                contents=user_prompt,
                config=types.GenerateContentConfig(
                    system_instruction=system_instruction,
                    response_mime_type="application/json",
                    response_json_schema=schema_copy,
                    temperature=temperature,
                ),
            )

            raw_text = response.text
            usage = None
            if hasattr(response, "usage_metadata") and response.usage_metadata:
                um = response.usage_metadata
                usage = {
                    "prompt_tokens": getattr(um, "prompt_token_count", 0),
                    "candidates_tokens": getattr(um, "candidates_token_count", 0),
                    "total_tokens": getattr(um, "total_token_count", 0),
                }

            return raw_text, usage
        except Exception as e:
            last_error = e
            error_str = str(e)
            if "503" in error_str or "UNAVAILABLE" in error_str or "overloaded" in error_str.lower():
                wait = (2 ** attempt) * 5  # 5s, 10s, 20s
                print(f"    Rate limited (attempt {attempt+1}/{max_retries}), waiting {wait}s...")
                time.sleep(wait)
            else:
                raise

    raise last_error


# ============================================================================
# EXPERIMENT RUNNER
# ============================================================================

def run_single_experiment(
    scenario: dict,
    schema: dict,
    dataset: dict,
    model_name: str,
    dry_run: bool = False,
) -> dict:
    """Run a single experiment scenario."""
    now = datetime.now(timezone.utc)
    test_id = f"test_{scenario['id']}_{now.strftime('%Y%m%d_%H%M%S')}"
    result = {
        "test_id": test_id,
        "scenario_id": scenario["id"],
        "scenario_name": scenario["name"],
        "mode": scenario["mode"],
        "model": model_name,
        "date": now.isoformat(),
        "schema_version": "2.0.0",
        "status": "PENDING",
        "json_parse": "PENDING",
        "schema_valid": "PENDING",
        "grounding_valid": "PENDING",
        "contract_valid": "PENDING",
        "failure_category": None,
        "errors": [],
        "usage": None,
    }

    # Build prompts
    if scenario["mode"] == "DESTINATION_FIRST":
        system, user = build_destination_first_prompt(dataset, **scenario["params"])
    else:
        system, user = build_budget_first_prompt(dataset, **scenario["params"])

    result["system_instruction_length"] = len(system)
    result["user_prompt_length"] = len(user)

    # Save prompts
    (PROMPTS_DIR / f"{scenario['id']}_system.txt").write_text(system, encoding="utf-8")
    (PROMPTS_DIR / f"{scenario['id']}_user.txt").write_text(user, encoding="utf-8")

    if dry_run:
        result["status"] = "DRY_RUN"
        result["json_parse"] = "DRY_RUN"
        result["schema_valid"] = "DRY_RUN"
        result["grounding_valid"] = "DRY_RUN"
        result["contract_valid"] = "DRY_RUN"
        print(f"  [{scenario['id']}] DRY RUN - prompts saved")
        return result

    # Call Gemini
    try:
        raw_text, usage = call_gemini(
            model_name=model_name,
            system_instruction=system,
            user_prompt=user,
            schema=schema,
            planning_mode=scenario["mode"],
        )
        result["usage"] = usage
    except Exception as e:
        result["status"] = "ERROR"
        result["failure_category"] = "API_ERROR"
        result["errors"] = [str(e)]
        print(f"  [{scenario['id']}] ERROR: {e}")
        return result

    # Save raw response
    raw_file = RESULTS_DIR / f"{scenario['id']}_raw.json"
    raw_file.write_text(json.dumps({
        "raw_text": raw_text,
        "usage": usage,
    }, indent=2, ensure_ascii=False), encoding="utf-8")

    # Step 1-2: JSON parse
    json_ok, parsed, parse_error = validate_json_parse(raw_text)
    if not json_ok:
        result["json_parse"] = "FAIL"
        result["schema_valid"] = "FAIL"
        result["grounding_valid"] = "FAIL"
        result["contract_valid"] = "FAIL"
        result["failure_category"] = "INVALID_JSON"
        result["errors"] = [parse_error]
        result["status"] = "FAIL"
        print(f"  [{scenario['id']}] FAIL - JSON parse: {parse_error}")
        return result

    result["json_parse"] = "PASS"

    # Save parsed JSON
    (RESULTS_DIR / f"{scenario['id']}_parsed.json").write_text(
        json.dumps(parsed, indent=2, ensure_ascii=False), encoding="utf-8"
    )

    # Step 3: Schema validation
    schema_ok, schema_error = validate_schema_response(schema, parsed)
    if not schema_ok:
        result["schema_valid"] = "FAIL"
        result["grounding_valid"] = "N/A"
        result["contract_valid"] = "FAIL"
        result["failure_category"] = classify_failure(False, False, [schema_error] if schema_error else [])
        result["errors"] = [schema_error] if schema_error else ["Unknown schema error"]
        result["status"] = "FAIL"
        print(f"  [{scenario['id']}] FAIL - Schema: {schema_error}")
        return result

    result["schema_valid"] = "PASS"

    # Step 4: Grounding validation
    grounding_ok, grounding_errors = validate_grounding(dataset, parsed)
    if not grounding_ok:
        result["grounding_valid"] = "FAIL"
        result["contract_valid"] = "FAIL"
        result["failure_category"] = classify_failure(True, False, grounding_errors)
        result["errors"] = grounding_errors
        result["status"] = "FAIL"
        print(f"  [{scenario['id']}] FAIL - Grounding: {len(grounding_errors)} error(s)")
        return result

    result["grounding_valid"] = "PASS"
    result["contract_valid"] = "PASS"
    result["failure_category"] = "SUCCESS"
    result["status"] = "PASS"
    print(f"  [{scenario['id']}] PASS")
    return result


# ============================================================================
# RESULTS REPORT
# ============================================================================

def generate_report(results: list[dict], model_name: str) -> str:
    """Generate a Markdown report of experiment results."""
    total = len(results)
    json_pass = sum(1 for r in results if r["json_parse"] == "PASS")
    schema_pass = sum(1 for r in results if r["schema_valid"] == "PASS")
    grounding_pass = sum(1 for r in results if r["grounding_valid"] == "PASS")
    contract_pass = sum(1 for r in results if r["contract_valid"] == "PASS")
    failures = [r for r in results if r["status"] != "PASS"]

    acceptance = "PASS" if schema_pass >= 10 else "FAIL"

    report = f"""# Triply -- Gemini Prototype Experiment Results

**Date:** {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}
**Model:** {model_name}
**Schema version:** 2.0.0
**JSON Schema draft:** 2020-12

---

## Acceptance Results

| Metric | Count |
|--------|-------|
| Total generations | {total} |
| Valid JSON | {json_pass} |
| Schema-valid | {schema_pass} |
| Dataset-grounded | {grounding_pass} |
| Contract-valid | {contract_pass} |

**Acceptance criterion: 10+ schema-valid generations -> {acceptance}**

---

## Scenario Results

| Test | Scenario | Mode | JSON | Schema | Grounded | Contract | Result |
|------|----------|------|------|--------|----------|----------|--------|
"""

    for r in results:
        report += f"| {r['scenario_id']} | {r['scenario_name'][:30]} | {r['mode'][:4]} | {r['json_parse']} | {r['schema_valid']} | {r['grounding_valid']} | {r['contract_valid']} | {r['status']} |\n"

    if failures:
        report += "\n---\n\n## Failure Analysis\n\n"
        for f in failures:
            report += f"""### Test {f['scenario_id']} -- {f['scenario_name']}

- **Failure category:** {f['failure_category']}
- **JSON parse:** {f['json_parse']}
- **Schema valid:** {f['schema_valid']}
- **Grounding valid:** {f['grounding_valid']}
- **Errors:** {json.dumps(f['errors'], indent=2) if f['errors'] else 'None'}

"""

    failure_categories = {}
    for f in failures:
        cat = f.get("failure_category", "UNKNOWN")
        failure_categories[cat] = failure_categories.get(cat, 0) + 1

    if failure_categories:
        report += "### Failure Mode Summary\n\n"
        report += "| Category | Count |\n|----------|-------|\n"
        for cat, count in sorted(failure_categories.items(), key=lambda x: -x[1]):
            report += f"| {cat} | {count} |\n"

    report += "\n---\n\n## Configuration\n\n"
    report += f"- **Model:** {model_name}\n"
    report += "- **Temperature:** 0.4\n"
    report += "- **Response MIME type:** application/json\n"
    report += "- **Response schema:** responseJsonSchema (full JSON Schema draft 2020-12)\n"
    report += f"- **Schema file:** `{SCHEMA_PATH.relative_to(AI_DIR.parent)}`\n"
    report += "- **Dataset:** 57 active places, 3 destinations (Paris, Amman, New York)\n"

    return report


# ============================================================================
# MAIN
# ============================================================================

def main():
    import argparse
    parser = argparse.ArgumentParser(description="Triply Gemini Prototype Experiments")
    parser.add_argument("--scenario", help="Run a specific scenario (A-L)")
    parser.add_argument("--model", default="gemini-2.0-flash", help="Model to use")
    parser.add_argument("--dry-run", action="store_true", help="Build prompts without calling Gemini")
    parser.add_argument("--temperature", type=float, default=0.4, help="Temperature setting")
    args = parser.parse_args()

    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    PROMPTS_DIR.mkdir(parents=True, exist_ok=True)

    print("=" * 70)
    print("TRIPLY -- Gemini Prototype Experiments")
    print(f"Model: {args.model}")
    print(f"Schema: v2.0.0 (draft 2020-12)")
    print(f"Dry run: {args.dry_run}")
    print("=" * 70)

    # Load data
    print("\nLoading dataset...")
    dataset = load_dataset()
    print(f"  Destinations: {len(dataset['destinations'])}")
    print(f"  Places: {len(dataset['places'])}")
    print(f"  Active places: {sum(1 for p in dataset['places'] if p['is_active'] == 'True')}")

    print("\nLoading schema...")
    schema = load_schema()
    print(f"  Schema: {schema['title']}")
    print(f"  Required: {schema['required']}")
    print(f"  Defs: {list(schema.get('$defs', {}).keys())}")

    # Filter scenarios
    scenarios = SCENARIOS
    if args.scenario:
        scenarios = [s for s in SCENARIOS if s["id"] == args.scenario]
        if not scenarios:
            print(f"ERROR: Scenario '{args.scenario}' not found")
            sys.exit(1)

    # Run experiments
    results = []
    for scenario in scenarios:
        print(f"\n--- Scenario {scenario['id']}: {scenario['name']} ---")
        result = run_single_experiment(
            scenario=scenario,
            schema=schema,
            dataset=dataset,
            model_name=args.model,
            dry_run=args.dry_run,
        )
        results.append(result)

        if not args.dry_run:
            time.sleep(2)

    # Summary
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)
    total = len(results)
    json_pass = sum(1 for r in results if r["json_parse"] == "PASS")
    schema_pass = sum(1 for r in results if r["schema_valid"] == "PASS")
    grounding_pass = sum(1 for r in results if r["grounding_valid"] == "PASS")
    contract_pass = sum(1 for r in results if r["contract_valid"] == "PASS")
    failures = sum(1 for r in results if r["status"] != "PASS")

    print(f"Total generations: {total}")
    print(f"Valid JSON: {json_pass}")
    print(f"Schema-valid: {schema_pass}")
    print(f"Dataset-grounded: {grounding_pass}")
    print(f"Contract-valid: {contract_pass}")
    print(f"Failures: {failures}")

    acceptance = "PASS" if schema_pass >= 10 else "FAIL"
    print(f"\nAcceptance: 10+ schema-valid generations -> {acceptance}")

    # Save results — accumulate across runs
    results_file = RESULTS_DIR / "experiment_results.json"
    existing_results = []
    if results_file.exists():
        try:
            with open(results_file, encoding="utf-8") as f:
                existing = json.load(f)
                existing_results = existing.get("results", [])
        except (json.JSONDecodeError, KeyError):
            pass

    # Merge: keep existing, add new (deduplicate by test_id prefix scenario_id)
    seen = {r["scenario_id"] for r in existing_results}
    for r in results:
        if r["scenario_id"] not in seen:
            existing_results.append(r)
            seen.add(r["scenario_id"])

    all_results = existing_results
    total_all = len(all_results)
    json_pass_all = sum(1 for r in all_results if r["json_parse"] == "PASS")
    schema_pass_all = sum(1 for r in all_results if r["schema_valid"] == "PASS")
    grounding_pass_all = sum(1 for r in all_results if r["grounding_valid"] == "PASS")
    contract_pass_all = sum(1 for r in all_results if r["contract_valid"] == "PASS")
    acceptance_all = "PASS" if schema_pass_all >= 10 else "FAIL"

    with open(results_file, "w", encoding="utf-8") as f:
        json.dump({
            "experiment_date": datetime.now(timezone.utc).isoformat(),
            "model": args.model,
            "schema_version": "2.0.0",
            "total_generations": total_all,
            "json_pass": json_pass_all,
            "schema_pass": schema_pass_all,
            "grounding_pass": grounding_pass_all,
            "contract_pass": contract_pass_all,
            "acceptance": acceptance_all,
            "results": all_results,
        }, f, indent=2, ensure_ascii=False)
    print(f"\nResults saved to {results_file}")

    # Generate report using all accumulated results
    report = generate_report(all_results, args.model)
    report_file = SCRIPT_DIR / "EXPERIMENT_REPORT.md"
    report_file.write_text(report, encoding="utf-8")
    print(f"Report saved to {report_file}")
    print(f"\nAccumulated totals: {total_all} generations, {schema_pass_all} schema-valid")
    print(f"Overall acceptance: {acceptance_all}")

    return 0 if acceptance_all == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
