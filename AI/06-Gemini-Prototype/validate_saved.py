#!/usr/bin/env python3
"""
Validate all saved Gemini responses against the schema and dataset.
Reconstructs the full experiment results from saved parsed JSON files.
"""

import csv
import json
import os
from pathlib import Path

try:
    from jsonschema import validate, ValidationError, Draft202012Validator
except ImportError:
    print("ERROR: jsonschema not installed")
    exit(1)

SCRIPT_DIR = Path(__file__).resolve().parent
AI_DIR = SCRIPT_DIR.parent
SCHEMA_PATH = AI_DIR / "02-Prompt-Engineering" / "json-schemas" / "triply-trip-plan-generation.schema.json"
DATASET_DIR = AI_DIR / "01-Dataset" / "curated-data"
RESULTS_DIR = SCRIPT_DIR / "results"

CATEGORY_MAP = {"1": "ATTRACTION", "2": "RESTAURANT", "3": "ACTIVITY", "4": "ACCOMMODATION", "5": "TRANSPORT"}


def load_csv(filename):
    with open(DATASET_DIR / filename, encoding="utf-8-sig") as f:
        return list(csv.DictReader(f))


def load_schema():
    with open(SCHEMA_PATH, encoding="utf-8") as f:
        schema = json.load(f)
    Draft202012Validator.check_schema(schema)
    return schema


def validate_grounding(dataset, instance):
    errors = []
    valid_places = {p["name"] for p in dataset["places"] if p["is_active"] == "True"}
    valid_dests = {d["name"] for d in dataset["destinations"] if d["is_supported"] == "True"}
    acc_places = {p["name"] for p in dataset["places"] if p["place_category_id"] == "4" and p["is_active"] == "True"}
    rest_names = {p["name"] for p in dataset["places"] if p["place_category_id"] == "2" and p["is_active"] == "True"}
    trans_names = {p["name"] for p in dataset["places"] if p["place_category_id"] == "5" and p["is_active"] == "True"}

    if not isinstance(instance, dict):
        return False, ["Root is not an object"]

    pm = instance.get("planning_mode")
    if pm not in ("DESTINATION_FIRST", "BUDGET_FIRST"):
        errors.append(f"Invalid planning_mode: {pm}")

    options = instance.get("destination_options", [])
    if not options:
        errors.append("No destination_options")

    for i, opt in enumerate(options):
        pfx = f"opt[{i}]"
        dn = opt.get("destination_name", "")
        if dn not in valid_dests:
            errors.append(f"{pfx}: dest '{dn}' not in dataset")

        acc = opt.get("accommodation", {})
        an = acc.get("place_name", "")
        if an not in valid_places:
            errors.append(f"{pfx}: acc '{an}' not in dataset")
        if an not in acc_places:
            errors.append(f"{pfx}: acc '{an}' not ACCOMMODATION category")

        days = opt.get("days", [])
        has_rest_per_day = []
        has_trans = False
        for j, day in enumerate(days):
            dpfx = f"{pfx}.d{j}"
            items = day.get("items", [])
            day_rest = False
            for item in items:
                pn = item.get("place_name", "")
                if pn not in valid_places:
                    errors.append(f"{dpfx}: '{pn}' not in dataset")
                if pn in rest_names:
                    day_rest = True
                if pn in trans_names:
                    has_trans = True
                if pn in acc_places:
                    errors.append(f"{dpfx}: ACCOMMODATION '{pn}' in days")
            has_rest_per_day.append(day_rest)

        for j, hr in enumerate(has_rest_per_day):
            if not hr:
                errors.append(f"{pfx}.d{j}: no RESTAURANT")
        if not has_trans:
            errors.append(f"{pfx}: no TRANSPORT across days")

    return len(errors) == 0, errors


def main():
    schema = load_schema()
    dataset = {
        "destinations": load_csv("Destination.csv"),
        "places": load_csv("Place.csv"),
    }

    # Scenario definitions
    scenarios = {
        "A": {"name": "Standard Amman 3-day", "mode": "DESTINATION_FIRST"},
        "B": {"name": "Budget-constrained Amman", "mode": "DESTINATION_FIRST"},
        "C": {"name": "Paris 4-day multi-interest", "mode": "DESTINATION_FIRST"},
        "D": {"name": "New York 3-day", "mode": "DESTINATION_FIRST"},
        "E": {"name": "Budget-first multi-destination", "mode": "BUDGET_FIRST"},
        "F": {"name": "Amman consistency test 1", "mode": "DESTINATION_FIRST"},
        "G": {"name": "Amman consistency test 2", "mode": "DESTINATION_FIRST"},
        "H": {"name": "Paris 2-day short trip", "mode": "DESTINATION_FIRST"},
        "I": {"name": "New York 5-day long trip", "mode": "DESTINATION_FIRST"},
        "J": {"name": "Budget-first tight budget", "mode": "BUDGET_FIRST"},
        "K": {"name": "Amman nature/adventure 5-day", "mode": "DESTINATION_FIRST"},
        "L": {"name": "Paris luxury 3-day", "mode": "DESTINATION_FIRST"},
    }

    results = []
    model = "gemini-3.6-flash"

    for sid, info in sorted(scenarios.items()):
        parsed_file = RESULTS_DIR / f"{sid}_parsed.json"
        raw_file = RESULTS_DIR / f"{sid}_raw.json"

        if not parsed_file.exists():
            results.append({
                "scenario_id": sid, "scenario_name": info["name"], "mode": info["mode"],
                "json_parse": "NOT_AVAILABLE", "schema_valid": "NOT_AVAILABLE",
                "grounding_valid": "NOT_AVAILABLE", "contract_valid": "NOT_AVAILABLE",
                "status": "NOT_AVAILABLE", "failure_category": "NO_RESPONSE",
                "errors": ["No saved response file"],
            })
            print(f"[{sid}] NOT AVAILABLE - no saved response")
            continue

        with open(parsed_file, encoding="utf-8") as f:
            parsed = json.load(f)

        # Schema validation
        try:
            validate(instance=parsed, schema=schema, cls=Draft202012Validator)
            schema_ok = True
            schema_err = None
        except ValidationError as e:
            schema_ok = False
            path = " -> ".join(str(p) for p in e.absolute_path) if e.absolute_path else "(root)"
            schema_err = f"[{path}] {e.message}"

        # Grounding validation
        grounding_ok, grounding_errors = validate_grounding(dataset, parsed)

        # Overall status
        json_ok = True  # We have parsed JSON, so it parsed OK
        contract_ok = schema_ok and grounding_ok

        failure_cat = None
        if not schema_ok:
            failure_cat = "SCHEMA_VIOLATION"
        elif not grounding_ok:
            err_str = " ".join(grounding_errors).lower()
            if "not in dataset" in err_str:
                failure_cat = "DATASET_HALLUCINATION"
            elif "not accommodation" in err_str or "accommodation" in err_str:
                failure_cat = "CATEGORY_VIOLATION"
            elif "no restaurant" in err_str:
                failure_cat = "MISSING_RESTAURANT"
            elif "no transport" in err_str:
                failure_cat = "MISSING_TRANSPORT"
            else:
                failure_cat = "GROUNDING_VIOLATION"
        else:
            failure_cat = "SUCCESS"

        status = "PASS" if contract_ok else "FAIL"
        errors = grounding_errors if grounding_errors else ([schema_err] if schema_err else [])

        results.append({
            "scenario_id": sid, "scenario_name": info["name"], "mode": info["mode"],
            "json_parse": "PASS" if json_ok else "FAIL",
            "schema_valid": "PASS" if schema_ok else "FAIL",
            "grounding_valid": "PASS" if grounding_ok else "FAIL",
            "contract_valid": "PASS" if contract_ok else "FAIL",
            "status": status, "failure_category": failure_cat,
            "errors": errors,
        })

        sym = "PASS" if status == "PASS" else "FAIL"
        print(f"[{sid}] {sym} - Schema:{'PASS' if schema_ok else 'FAIL'} Ground:{'PASS' if grounding_ok else 'FAIL'} ({info['name']})")
        if errors:
            for e in errors[:3]:
                print(f"       {e}")

    # Summary
    total = len(results)
    json_p = sum(1 for r in results if r["json_parse"] == "PASS")
    schema_p = sum(1 for r in results if r["schema_valid"] == "PASS")
    ground_p = sum(1 for r in results if r["grounding_valid"] == "PASS")
    contract_p = sum(1 for r in results if r["contract_valid"] == "PASS")
    avail = sum(1 for r in results if r["status"] != "NOT_AVAILABLE")

    acceptance = "PASS" if schema_p >= 10 else "FAIL"

    print(f"\n{'='*70}")
    print(f"SUMMARY (from {avail} available responses out of {total} scenarios)")
    print(f"{'='*70}")
    print(f"Valid JSON: {json_p}/{avail}")
    print(f"Schema-valid: {schema_p}/{avail}")
    print(f"Dataset-grounded: {ground_p}/{avail}")
    print(f"Contract-valid: {contract_p}/{avail}")
    print(f"\nAcceptance: 10+ schema-valid -> {acceptance}")

    # Save
    with open(RESULTS_DIR / "experiment_results.json", "w", encoding="utf-8") as f:
        json.dump({
            "model": model, "schema_version": "2.0.0",
            "total_scenarios": total, "available_responses": avail,
            "json_pass": json_p, "schema_pass": schema_p,
            "grounding_pass": ground_p, "contract_pass": contract_p,
            "acceptance": acceptance, "results": results,
        }, f, indent=2, ensure_ascii=False)

    # Generate report
    report = f"""# Triply -- Gemini Prototype Experiment Results

**Model:** {model}
**Schema version:** 2.0.0
**JSON Schema draft:** 2020-12

---

## Acceptance Results

| Metric | Count |
|--------|-------|
| Total scenarios | {total} |
| Available responses | {avail} |
| Valid JSON | {json_p} |
| Schema-valid | {schema_p} |
| Dataset-grounded | {ground_p} |
| Contract-valid | {contract_p} |

**Acceptance criterion: 10+ schema-valid generations -> {acceptance}**

---

## Scenario Results

| Test | Scenario | Mode | JSON | Schema | Grounded | Contract | Result |
|------|----------|------|------|--------|----------|----------|--------|
"""
    for r in results:
        report += f"| {r['scenario_id']} | {r['scenario_name'][:35]} | {r['mode'][:4]} | {r['json_parse']} | {r['schema_valid']} | {r['grounding_valid']} | {r['contract_valid']} | {r['status']} |\n"

    # Failure details
    failures = [r for r in results if r["status"] == "FAIL"]
    if failures:
        report += "\n---\n\n## Failure Analysis\n\n"
        for f in failures:
            report += f"### Test {f['scenario_id']} -- {f['scenario_name']}\n\n"
            report += f"- **Category:** {f['failure_category']}\n"
            report += f"- **Errors:**\n"
            for e in f["errors"]:
                report += f"  - {e}\n"
            report += "\n"

    not_avail = [r for r in results if r["status"] == "NOT_AVAILABLE"]
    if not_avail:
        report += "\n---\n\n## Unavailable Responses\n\n"
        report += "The following scenarios did not produce saved responses (rate-limited by free-tier quota):\n\n"
        for r in not_avail:
            report += f"- **{r['scenario_id']}**: {r['scenario_name']}\n"
        report += "\nThese scenarios should be re-run when quota resets or with a paid plan.\n"

    report += f"""
---

## Configuration

- **Model:** {model}
- **Temperature:** 0.4
- **Response MIME type:** application/json
- **Response schema:** responseJsonSchema (full JSON Schema draft 2020-12)
- **Dataset:** 57 active places, 3 destinations (Paris, Amman, New York)
- **Free-tier quota:** 20 requests/day per model

---

## Downstream Handoff

### For Design AI-Output Validation Rules

**Observed failure modes:**
- `DATASET_HALLUCINATION`: Model invents place names not in the dataset
- `CATEGORY_VIOLATION`: Model puts ACCOMMODATION places inside days
- `MISSING_RESTAURANT`: Day has no RESTAURANT-category place
- `MISSING_TRANSPORT`: No TRANSPORT-category place across all days

**What schema validation catches:** type errors, missing fields, invalid enums, unexpected fields, structural issues.

**What requires domain validation:** dataset grounding (place name existence), category rules, restaurant/transport requirements.

### For AI-Orchestration Backend Module

- **Model:** {model}
- **Final schema:** `triply-trip-plan-generation.schema.json` v2.0.0
- **Response format:** application/json with responseJsonSchema
- **Temperature:** 0.4
- **Validation flow:** JSON parse -> Schema validation -> Dataset grounding -> Category rules
- **Known issue:** Free-tier quota limits to 20 requests/day; production should use paid plan
"""
    (SCRIPT_DIR / "EXPERIMENT_REPORT.md").write_text(report, encoding="utf-8")
    print(f"\nReport saved to {SCRIPT_DIR / 'EXPERIMENT_REPORT.md'}")

    return 0 if acceptance == "PASS" else 1


if __name__ == "__main__":
    exit(main())
