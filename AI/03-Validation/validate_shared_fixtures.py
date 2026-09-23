#!/usr/bin/env python3
"""
Triply — shared V-001/V-002 parity fixture runner (Python side).

Loads AI/03-Validation/fixtures/v001-v002-parity-fixtures.json and runs each case
through the harness rule implementations (harness.validate_places +
harness.validate_budget), comparing the observed V-001 (grounding) and V-002
(budget) outcomes against the fixture's expected values.

The exact same fixture file is consumed by the C# test
Backend/Triply.Api.Tests/ValidationParityFixtureTests.cs, so a rule difference
between the Python harness and the C# validator surfaces as a mismatch on one
side (see AI/03-Validation/AI_OUTPUT_VALIDATION_RULES.md §12).

Usage:
    python validate_shared_fixtures.py
"""

import json
import sys
from decimal import Decimal
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))

import harness  # noqa: E402  (harness.py lives next to this script)

FIXTURES_PATH = SCRIPT_DIR / "fixtures" / "v001-v002-parity-fixtures.json"


def build_dataset(case: dict) -> dict:
    """Translate the fixture's dataset section into the harness's dataset dict.

    Inactive places are intentionally omitted: the rules only authorize
    is_active = true places (spec §4.4), so an inactive name must not resolve.
    """
    all_destination_ids: dict[str, int] = {}
    supported_destinations: dict[str, dict] = {}
    for i, dest in enumerate(case["dataset"]["destinations"], start=1):
        all_destination_ids[dest["name"]] = i
        if dest.get("isSupported", True):
            supported_destinations[dest["name"]] = {"id": i, "name": dest["name"]}

    places: dict[str, dict] = {}
    # The harness resolves names through `places_by_name` (every active row per
    # name) so duplicate names fail closed instead of last-wins. Mirror the same
    # shape here, otherwise every lookup misses.
    places_by_name: dict[str, list[dict]] = {}
    currencies: dict[int, str] = {}
    currency_ids: dict[str, int] = {}
    next_place_id = 1
    for place in case["dataset"]["places"]:
        if not place.get("isActive", True):
            continue
        iso = place.get("currency", "USD")
        currency_id = currency_ids.setdefault(iso, len(currency_ids) + 1)
        currencies[currency_id] = iso
        entry = {
            "id": next_place_id,
            "destination_id": all_destination_ids.get(place["destination"], -1),
            "name": place["name"],
            "category": place["category"],
            "reference_price": Decimal(str(place.get("referencePrice", 0))),
            "currency_id": currency_id,
            "cost_category_id": 1,
            "is_active": "True",
        }
        places.setdefault(place["name"], entry)
        places_by_name.setdefault(place["name"], []).append(entry)
        next_place_id += 1

    return {
        "places": places,
        "places_by_name": places_by_name,
        "destinations": supported_destinations,
        "currencies": currencies,
        "total_places": len(places),
        "total_destinations": len(supported_destinations),
    }


def evaluate(
    case: dict,
    schema: dict,
    trip_start: str | None = None,
    trip_end: str | None = None,
) -> tuple[dict, bool, bool]:
    dataset = build_dataset(case)
    budget = Decimal(str(case["budget"])) if case.get("budget") is not None else None

    result = harness.validate_generation(
        itinerary=case["itinerary"],
        schema=schema,
        dataset=dataset,
        budget_amount=budget,
        test_id=case["id"],
        source_file=str(FIXTURES_PATH),
        trip_start_date=trip_start,
        trip_end_date=trip_end,
    )

    # Mirrors the C# runner, which asserts `ItineraryValidationResult.IsValid`.
    # `opt.passed` therefore covers Step 1 structural consistency as well as the
    # V-001 grounding rules, so the two runners compare the same notion of
    # "option is valid" (see the expected.v001Passed note in the fixture file).
    # Every failure code the harness observed, from the generation level and from
    # each option's grounding/structural result.
    observed_codes: set[str] = set(result.failure_codes)
    for opt in result.options:
        observed_codes.update(opt.place_grounding.failure_codes)

    v001_passed = bool(result.options) and all(opt.passed for opt in result.options)
    v002_passed = all(
        (not opt.budget_feasibility.checked) or opt.budget_feasibility.passed
        for opt in result.options
    )
    return result, v001_passed, v002_passed, observed_codes


def check_schema_file_parity() -> bool:
    """Guard against the generated-schema artifact drifting between its two copies.

    The schema exists in the AI track and in the Backend (which copies its own file
    into the structured-output request). Nothing links the two, so an edit to only
    one would silently change what Gemini is asked to produce — and validation
    would then be checking a contract the model was never given.
    """
    backend_schema = (
        harness.AI_DIR.parent
        / "Backend"
        / "Triply.Api"
        / "AI-Schemas"
        / "triply-trip-plan-generation.schema.json"
    )

    print("\n" + "-" * 78)
    print("Schema file parity")
    print(f"  AI track : {harness.SCHEMA_PATH}")
    print(f"  Backend  : {backend_schema}")

    if not backend_schema.exists():
        print("  MISSING backend schema copy")
        return False

    if harness.SCHEMA_PATH.read_bytes() != backend_schema.read_bytes():
        print("  DRIFT: the two schema copies differ")
        return False

    print("  IDENTICAL")
    return True


def main() -> int:
    print("=" * 78)
    print("TRIPLY — Shared V-001/V-002 Parity Fixtures (Python harness)")
    print(f"Fixtures: {FIXTURES_PATH}")
    print("=" * 78)

    with open(FIXTURES_PATH, encoding="utf-8") as f:
        fixtures = json.load(f)

    schema = harness.load_schema()
    unexpected = 0

    trip_start = fixtures.get("tripStartDate")
    trip_end = fixtures.get("tripEndDate")

    failure_checks = fixtures.get("expectedFailureChecks", {})

    for case in fixtures["cases"]:
        result, v001, v002, observed_codes = evaluate(case, schema, trip_start, trip_end)
        expected = case["expected"]

        v001_ok = v001 == expected["v001Passed"]
        v002_ok = v002 == expected["v002Passed"]

        # Reason check (containment): a case expecting failure must fail for the
        # documented reason, not merely for some reason.
        expected_codes = list(failure_checks.get(case["id"], {}).get("codes", []))
        missing_codes = sorted(set(expected_codes) - observed_codes)
        reason_ok = not missing_codes

        case_ok = v001_ok and v002_ok and reason_ok
        if not case_ok:
            unexpected += 1

        print(f"\n{case['id']}: {case['name']}")
        print(
            f"  V-001 expected={expected['v001Passed']!s:<5} got={v001!s:<5} "
            f"{'OK' if v001_ok else 'MISMATCH'}"
        )
        print(
            f"  V-002 expected={expected['v002Passed']!s:<5} got={v002!s:<5} "
            f"{'OK' if v002_ok else 'MISMATCH'}"
        )
        if expected_codes:
            print(
                f"  reason expected={'+'.join(expected_codes)} "
                f"{'OK' if reason_ok else 'MISSING ' + '+'.join(missing_codes)}"
            )

        if not case_ok:
            for opt in result.options:
                for code in opt.place_grounding.failure_codes:
                    print(f"    code: {code}")
                for ip in opt.place_grounding.invalid_places:
                    print(f"    invalid place: {ip}")

    total = len(fixtures["cases"])
    schema_ok = check_schema_file_parity()

    print("\n" + "=" * 78)
    print(f"RESULT: {total - unexpected}/{total} fixtures matched expectations")
    if not schema_ok:
        print("SCHEMA FILE DRIFT DETECTED")
    elif unexpected == 0:
        print("ALL PARITY FIXTURES MATCHED")
    else:
        print("PARITY MISMATCH DETECTED")
    print("=" * 78)

    return 0 if (unexpected == 0 and schema_ok) else 1


if __name__ == "__main__":
    sys.exit(main())
