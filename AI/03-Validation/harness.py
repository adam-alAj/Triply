#!/usr/bin/env python3
"""
Triply — AI-Output Validation Harness
Implements: V-001 (0%-Invented-Place Check) + V-002 (Budget Feasibility Check)
Schema version: 2.0.0
JSON Schema draft: 2020-12

Validates Gemini-generated itinerary outputs against the internal dataset
per AI_OUTPUT_VALIDATION_RULES.md specification.

Usage:
    python harness.py                                    # Validate all real Gemini generations
    python harness.py --input results.json               # Validate a specific batch file
    python harness.py --input results.json --budget 400  # With budget for V-002
    python harness.py --test                             # Run fixture tests only
"""

import csv
import json
import sys
from dataclasses import dataclass, field
from datetime import date, timedelta
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from typing import Any

try:
    from jsonschema import validate, ValidationError, Draft202012Validator
except ImportError:
    print("ERROR: jsonschema not installed. Run: pip install jsonschema")
    sys.exit(1)

# ============================================================================
# PATHS
# ============================================================================

SCRIPT_DIR = Path(__file__).resolve().parent
AI_DIR = SCRIPT_DIR.parent
SCHEMA_PATH = AI_DIR / "02-Prompt-Engineering" / "json-schemas" / "triply-trip-plan-generation.schema.json"
DATASET_DIR = AI_DIR / "01-Dataset" / "curated-data"
GEMINI_RESULTS_DIR = AI_DIR / "06-Gemini-Prototype" / "results"

# ============================================================================
# DATA MODELS (per spec §7)
# ============================================================================

@dataclass
class SchemaValidationResult:
    passed: bool = True
    errors: list[str] = field(default_factory=list)

@dataclass
class PlaceGroundingResult:
    passed: bool = True
    generated_place_count: int = 0
    resolved_place_count: int = 0
    invalid_place_count: int = 0
    invented_place_rate: float = 0.0
    invalid_places: list[str] = field(default_factory=list)
    failure_codes: list[str] = field(default_factory=list)

@dataclass
class BudgetFeasibilityResult:
    checked: bool = False
    passed: bool = True
    options_total: int = 0
    options_within_budget: int = 0
    over_budget_options: list[dict] = field(default_factory=list)
    option_costs: list[dict] = field(default_factory=list)
    failure_codes: list[str] = field(default_factory=list)

@dataclass
class OptionValidationResult:
    destination_name: str = ""
    passed: bool = True
    place_grounding: PlaceGroundingResult = field(default_factory=PlaceGroundingResult)
    budget_feasibility: BudgetFeasibilityResult = field(default_factory=BudgetFeasibilityResult)
    errors: list[str] = field(default_factory=list)

@dataclass
class GenerationResult:
    test_id: str = ""
    source_file: str = ""
    planning_mode: str = ""
    schema_validation: SchemaValidationResult = field(default_factory=SchemaValidationResult)
    options: list[OptionValidationResult] = field(default_factory=list)
    overall_passed: bool = True
    failure_codes: list[str] = field(default_factory=list)

@dataclass
class BatchResult:
    total_generations: int = 0
    valid_json: int = 0
    schema_valid: int = 0
    overall_pass: int = 0
    overall_fail: int = 0
    total_place_refs: int = 0
    invalid_place_refs: int = 0
    invented_place_rate: float = 0.0
    generations_passing_place: int = 0
    generations_failing_place: int = 0
    budget_checked: int = 0
    budget_pass: int = 0
    budget_fail: int = 0
    generation_results: list[GenerationResult] = field(default_factory=list)

# ============================================================================
# DATASET LOADING
# ============================================================================

CATEGORY_MAP = {
    "1": "ATTRACTION",
    "2": "RESTAURANT",
    "3": "ACTIVITY",
    "4": "ACCOMMODATION",
    "5": "TRANSPORT",
}

# Spec §6 Step 1 — the only time slots the contract permits.
VALID_TIME_SLOTS = {"MORNING", "AFTERNOON", "EVENING"}


def load_csv(filename: str) -> list[dict]:
    filepath = DATASET_DIR / filename
    with open(filepath, encoding="utf-8-sig") as f:
        return list(csv.DictReader(f))


def load_dataset() -> dict:
    """Load the authoritative internal dataset from CSV files."""
    places = load_csv("Place.csv")
    destinations = load_csv("Destination.csv")
    categories = load_csv("PlaceCategory.csv")
    currencies = load_csv("Currency.csv")
    cost_categories = load_csv("CostCategory.csv")

    # Build lookup structures
    # Authorized active places keyed by name (exact string match per spec §4.9).
    # `active_places_by_name` keeps EVERY active row per name, because the same
    # name may legitimately exist in more than one destination. Building a flat
    # name->row dict let a duplicate silently overwrite the earlier row (last CSV
    # row wins), which is neither spec §4.9 nor what the C# validator does.
    active_places: dict[str, dict] = {}
    active_places_by_name: dict[str, list[dict]] = {}
    for p in places:
        if p["is_active"] == "True":
            cat_code = CATEGORY_MAP.get(p["place_category_id"], "UNKNOWN")
            entry = {
                "id": int(p["id"]),
                "destination_id": int(p["destination_id"]),
                "name": p["name"],
                "category": cat_code,
                "reference_price": Decimal(p["reference_price"]),
                "currency_id": int(p["currency_id"]),
                "cost_category_id": int(p["cost_category_id"]),
            }
            active_places.setdefault(p["name"], entry)
            active_places_by_name.setdefault(p["name"], []).append(entry)

    # Supported destinations keyed by name
    supported_destinations: dict[str, dict] = {}
    for d in destinations:
        if d["is_supported"] == "True":
            supported_destinations[d["name"]] = {
                "id": int(d["id"]),
                "name": d["name"],
            }

    # Currency lookup
    currency_by_id = {int(c["id"]): c["iso_code"] for c in currencies}

    return {
        "places": active_places,
        "places_by_name": active_places_by_name,
        "destinations": supported_destinations,
        "currencies": currency_by_id,
        "total_places": len(active_places),
        "total_destinations": len(supported_destinations),
    }


def load_schema() -> dict:
    with open(SCHEMA_PATH, encoding="utf-8") as f:
        schema = json.load(f)
    Draft202012Validator.check_schema(schema)
    return schema

# ============================================================================
# SCHEMA VALIDATION (Step 0 per spec §6)
# ============================================================================

def validate_schema(schema: dict, instance: Any) -> SchemaValidationResult:
    """Validate parsed JSON against the finalized JSON Schema."""
    result = SchemaValidationResult()
    try:
        validate(instance=instance, schema=schema, cls=Draft202012Validator)
    except ValidationError as e:
        result.passed = False
        path = " -> ".join(str(p) for p in e.absolute_path) if e.absolute_path else "(root)"
        result.errors.append(f"[{path}] {e.message}")
    return result

# ============================================================================
# V-001: 0%-INVENTED-PLACE CHECK (Steps 2-3 per spec §6)
# ============================================================================

def validate_places(
    option: dict,
    destination_name: str,
    dataset: dict,
) -> OptionValidationResult:
    """
    Implement V-001 per AI_OUTPUT_VALIDATION_RULES.md §4.

    Validates:
    1. destination_name resolves to a supported Destination
    2. Every place_name resolves to an active Place scoped to that destination
    3. Category rules (ACCOMMODATION placement, RESTAURANT per day, TRANSPORT)
    """
    result = OptionValidationResult(destination_name=destination_name)
    place_result = result.place_grounding

    # Step 1: Resolve destination
    dest = dataset["destinations"].get(destination_name)
    if dest is None:
        result.passed = False
        result.errors.append(f"DESTINATION_NOT_FOUND: '{destination_name}' not in supported destinations")
        result.place_grounding.passed = False
        result.place_grounding.failure_codes.append("DESTINATION_NOT_FOUND")
        return result

    dest_id = dest["id"]

    # Step 2: Collect all place names
    all_place_names: list[str] = []

    # Accommodation
    acc_name = option.get("accommodation", {}).get("place_name", "")
    if acc_name:
        all_place_names.append(acc_name)

    # Daily items
    for day in option.get("days", []):
        for item in day.get("items", []):
            pname = item.get("place_name", "")
            if pname:
                all_place_names.append(pname)

    place_result.generated_place_count = len(set(all_place_names))

    # Step 3: Resolve each place name (exact match, destination-scoped)
    resolved_places: dict[str, dict] = {}
    invalid_places: list[str] = []

    for name in set(all_place_names):
        if not name or not name.strip():
            invalid_places.append(name or "<empty>")
            place_result.failure_codes.append("PLACE_NAME_EMPTY")
            continue

        matches = dataset.get("places_by_name", {}).get(name, [])
        if not matches:
            invalid_places.append(name)
            place_result.failure_codes.append("PLACE_NOT_FOUND")
            continue

        in_destination = [m for m in matches if m["destination_id"] == dest_id]

        if len(in_destination) > 1:
            # An active duplicate name inside one destination is invalid curated
            # data. Fail closed instead of picking arbitrarily (parity with the C#
            # validator's duplicate-name handling).
            invalid_places.append(name)
            place_result.failure_codes.append("DUPLICATE_PLACE_NAME")
            continue

        if not in_destination:
            invalid_places.append(name)
            place_result.failure_codes.append("PLACE_WRONG_DESTINATION")
            continue

        # A name that also exists in other destinations is legitimate curated
        # data; the option's destination selects the intended row.
        resolved_places[name] = in_destination[0]

    place_result.resolved_place_count = len(resolved_places)
    place_result.invalid_place_count = len(invalid_places)
    place_result.invalid_places = invalid_places

    if place_result.generated_place_count > 0:
        place_result.invented_place_rate = float(
            Decimal(str(place_result.invalid_place_count))
            / Decimal(str(place_result.generated_place_count))
        )

    # Step 4: Category checks
    # 4a: accommodation must be ACCOMMODATION category
    if acc_name and acc_name in resolved_places:
        acc_place = resolved_places[acc_name]
        if acc_place["category"] != "ACCOMMODATION":
            result.passed = False
            place_result.passed = False
            result.errors.append(
                f"WRONG_CATEGORY: accommodation '{acc_name}' has category "
                f"'{acc_place['category']}', expected 'ACCOMMODATION'"
            )
            place_result.failure_codes.append("WRONG_CATEGORY")

    # 4b: No ACCOMMODATION in daily items
    for day in option.get("days", []):
        for item in day.get("items", []):
            pname = item.get("place_name", "")
            if pname in resolved_places and resolved_places[pname]["category"] == "ACCOMMODATION":
                result.passed = False
                place_result.passed = False
                result.errors.append(
                    f"ACCOMMODATION_IN_DAYS: '{pname}' is ACCOMMODATION but found in day {day.get('day_number')}"
                )
                place_result.failure_codes.append("ACCOMMODATION_IN_DAYS")

    # 4c: At least one RESTAURANT per day
    for day in option.get("days", []):
        day_has_restaurant = False
        for item in day.get("items", []):
            pname = item.get("place_name", "")
            if pname in resolved_places and resolved_places[pname]["category"] == "RESTAURANT":
                day_has_restaurant = True
                break
        if not day_has_restaurant:
            result.passed = False
            place_result.passed = False
            result.errors.append(
                f"MISSING_RESTAURANT: day {day.get('day_number')} has no RESTAURANT-category place"
            )
            place_result.failure_codes.append("MISSING_RESTAURANT")

    # 4d: At least one TRANSPORT across all days
    has_transport = False
    for day in option.get("days", []):
        for item in day.get("items", []):
            pname = item.get("place_name", "")
            if pname in resolved_places and resolved_places[pname]["category"] == "TRANSPORT":
                has_transport = True
                break
        if has_transport:
            break
    if not has_transport:
        result.passed = False
        place_result.passed = False
        result.errors.append("MISSING_TRANSPORT: no TRANSPORT-category place across all days")
        place_result.failure_codes.append("MISSING_TRANSPORT")

    # Overall place grounding pass/fail
    if invalid_places or not place_result.passed:
        place_result.passed = False

    # Deduplicate failure codes
    place_result.failure_codes = list(dict.fromkeys(place_result.failure_codes))

    # Overall option pass
    result.passed = place_result.passed
    result.errors = list(dict.fromkeys(result.errors))

    # Store resolved places for cost calculation
    result._resolved_places = resolved_places  # type: ignore

    return result

# ============================================================================
# STRUCTURAL CONSISTENCY (Step 1 per spec §6)
# ============================================================================

def _parse_date(value: Any) -> date | None:
    """Parse an ISO ``YYYY-MM-DD`` value; returns None when absent/unparseable."""
    if value is None:
        return None
    if isinstance(value, date):
        return value
    try:
        return date.fromisoformat(str(value))
    except ValueError:
        return None


def validate_structure(
    option: dict,
    option_prefix: str,
    start_date: date | None = None,
    expected_day_count: int | None = None,
) -> list[str]:
    """
    Implement Step 1 per AI_OUTPUT_VALIDATION_RULES.md §6.

    Mirrors the C# validator: day count, 1-indexed contiguous ``day_number``,
    date alignment against the trip's start_date, non-empty days/items, valid
    time_slot, ``order_index`` >= 1, and no duplicate (time_slot, order_index)
    within a day.

    The day-count and date-alignment checks only run when the caller supplies
    trip dates — the rules derive them from the trip, not from the itinerary.
    """
    errors: list[str] = []

    days = option.get("days") or []
    if not days:
        return [f"{option_prefix}: days is empty or missing."]

    if expected_day_count is not None and len(days) != expected_day_count:
        errors.append(
            f"{option_prefix}: days has {len(days)} entries, "
            f"expected {expected_day_count} (trip duration)."
        )

    sorted_days = sorted(days, key=lambda d: d.get("day_number") or 0)

    for i, day in enumerate(sorted_days):
        day_number = day.get("day_number")
        expected_day_number = i + 1

        if day_number != expected_day_number:
            errors.append(
                f"{option_prefix} day[{i}]: day_number is {day_number}, "
                f"expected {expected_day_number} (must be contiguous starting at 1)."
            )

        if start_date is not None:
            expected_date = start_date + timedelta(days=i)
            if _parse_date(day.get("date")) != expected_date:
                errors.append(
                    f"{option_prefix} day {day_number}: date is {day.get('date')}, "
                    f"expected {expected_date.isoformat()} (start_date + day_number - 1)."
                )

        items = day.get("items") or []
        if not items:
            errors.append(f"{option_prefix} day {day_number}: items is empty.")
            continue

        slot_counts: dict[tuple[str | None, Any], int] = {}
        for item in items:
            place_name = item.get("place_name")
            if not str(place_name or "").strip():
                errors.append(f"{option_prefix} day {day_number}: item has empty place_name.")
                continue

            slot = item.get("time_slot")
            normalized_slot = slot.upper() if isinstance(slot, str) else None
            if normalized_slot not in VALID_TIME_SLOTS:
                errors.append(
                    f"{option_prefix} day {day_number}, item '{place_name}': "
                    f"invalid time_slot '{slot}'."
                )

            order_index = item.get("order_index")
            if not isinstance(order_index, int) or order_index < 1:
                errors.append(
                    f"{option_prefix} day {day_number}, item '{place_name}': "
                    f"order_index must be >= 1, got {order_index}."
                )

            key = (normalized_slot, order_index)
            slot_counts[key] = slot_counts.get(key, 0) + 1

        duplicates = [key for key, count in slot_counts.items() if count > 1]
        if duplicates:
            slot_name, order_index = duplicates[0]
            errors.append(
                f"{option_prefix} day {day_number}: duplicate "
                f"(time_slot='{slot_name}', order_index={order_index})."
            )

    return errors


# ============================================================================
# V-002: BUDGET FEASIBILITY CHECK (Step 4 per spec §6)
# ============================================================================

def compute_option_cost(
    option: dict,
    resolved_places: dict[str, dict],
) -> Decimal:
    """
    Compute the deterministic total cost for a destination option.
    Per spec §5.3:
        EXPECTED_TOTAL =
            (accommodation.nights * accommodation_place.reference_price)
            + SUM(item_place.reference_price for each daily item)
    """
    total = Decimal("0")

    # Accommodation cost
    acc_name = option.get("accommodation", {}).get("place_name", "")
    nights = option.get("accommodation", {}).get("nights", 0)
    if acc_name in resolved_places:
        total += Decimal(str(nights)) * resolved_places[acc_name]["reference_price"]

    # Daily item costs
    for day in option.get("days", []):
        for item in day.get("items", []):
            pname = item.get("place_name", "")
            if pname in resolved_places:
                total += resolved_places[pname]["reference_price"]

    return total


def validate_budget(
    option_results: list[OptionValidationResult],
    planning_mode: str,
    budget_amount: Decimal | None,
    dataset: dict,
    option_data: list[tuple[dict, str]],
) -> BudgetFeasibilityResult:
    """
    Implement V-002 per AI_OUTPUT_VALIDATION_RULES.md §5.

    For each option that passed V-001:
    - Compute cost from Place.reference_price
    - DESTINATION_FIRST: flag over-budget, don't auto-fail
    - BUDGET_FIRST: drop over-budget options; fail if zero survive
    """
    result = BudgetFeasibilityResult()
    result.options_total = len(option_data)

    if budget_amount is None:
        result.checked = False
        result.passed = True
        return result

    result.checked = True

    for opt_result, (opt_data, _) in zip(option_results, option_data):
        if not opt_result.place_grounding.passed:
            # V-001 failed for this option — V-002 not executed per spec §6
            continue

        resolved = getattr(opt_result, "_resolved_places", {})
        cost = compute_option_cost(opt_data, resolved)

        opt_cost_info = {
            "destination": opt_result.destination_name,
            "cost": str(cost),
            "budget": str(budget_amount),
            "currency": dataset["currencies"].get(
                list(resolved.values())[0]["currency_id"] if resolved else 0, "N/A"
            ),
            "within_budget": cost <= budget_amount,
        }
        result.option_costs.append(opt_cost_info)

        if cost <= budget_amount:
            result.options_within_budget += 1
        else:
            result.over_budget_options.append(opt_cost_info)

    # Per spec §5.3:
    # DESTINATION_FIRST: flag over-budget, don't auto-fail
    # BUDGET_FIRST: drop over-budget; fail if zero survive
    if planning_mode == "BUDGET_FIRST":
        if result.options_within_budget == 0 and result.options_total > 0:
            result.passed = False
            result.failure_codes.append("ALL_OPTIONS_OVER_BUDGET")
    # DESTINATION_FIRST: always passes (over-budget is flagged, not failed)

    return result

# ============================================================================
# GENERATION VALIDATION PIPELINE
# ============================================================================

def validate_generation(
    itinerary: dict,
    schema: dict,
    dataset: dict,
    budget_amount: Decimal | None = None,
    test_id: str = "",
    source_file: str = "",
    trip_start_date: str | date | None = None,
    trip_end_date: str | date | None = None,
) -> GenerationResult:
    """
    Validate a single generation through the full pipeline per spec §6:
    Step 0: Schema validation
    Step 1: Structural consistency (day count, contiguity, date alignment, slots)
    Step 2: Dataset grounding (V-001)
    Step 3: Category rules (part of V-001)
    Step 4: Budget feasibility (V-002)

    Trip dates are optional; when supplied they drive the day-count and
    date-alignment checks (the rules derive expected dates from the trip, not
    from the itinerary).
    """
    result = GenerationResult(test_id=test_id, source_file=source_file)
    result.planning_mode = itinerary.get("planning_mode", "")

    # Step 0: Schema validation
    result.schema_validation = validate_schema(schema, itinerary)
    if not result.schema_validation.passed:
        result.overall_passed = False
        result.failure_codes.extend(result.schema_validation.errors)
        return result

    # Step 1 context: expected duration comes from the trip, when known.
    start_date = _parse_date(trip_start_date)
    end_date = _parse_date(trip_end_date)
    expected_day_count = (
        (end_date - start_date).days + 1
        if start_date is not None and end_date is not None and end_date >= start_date
        else None
    )

    # Process each destination option
    options = itinerary.get("destination_options", [])
    option_results: list[OptionValidationResult] = []
    option_data: list[tuple[dict, str]] = []

    option_destination_names = [
        (opt.get("destination_name") or "").strip() for opt in options
    ]
    named = [n for n in option_destination_names if n]
    if len(named) != len(set(named)):
        result.overall_passed = False
        result.failure_codes.append(
            "destination_options must contain distinct destination_name values."
        )

    for opt in options:
        dest_name = opt.get("destination_name", "")
        opt_result = validate_places(opt, dest_name, dataset)

        # Step 1: structural consistency (independent of grounding)
        structure_errors = validate_structure(
            opt,
            f"destination_option('{dest_name}')",
            start_date=start_date,
            expected_day_count=expected_day_count,
        )
        if structure_errors:
            opt_result.passed = False
            opt_result.errors = list(dict.fromkeys(opt_result.errors + structure_errors))
            opt_result.place_grounding.failure_codes.append("STRUCTURE_INVALID")

        option_results.append(opt_result)
        option_data.append((opt, dest_name))

        if not opt_result.passed:
            result.overall_passed = False
            result.failure_codes.extend(opt_result.place_grounding.failure_codes)

    result.failure_codes = list(dict.fromkeys(result.failure_codes))
    result.options = option_results

    # Step 4: Budget feasibility (V-002)
    # Only check budget if all options passed V-001
    all_places_passed = all(opt.place_grounding.passed for opt in option_results)

    if all_places_passed and budget_amount is not None:
        budget_result = validate_budget(
            option_results, result.planning_mode, budget_amount, dataset, option_data
        )
        for opt_result in option_results:
            opt_result.budget_feasibility = budget_result

        if not budget_result.passed:
            result.overall_passed = False
            result.failure_codes.extend(budget_result.failure_codes)

    return result

# ============================================================================
# BATCH EXECUTION
# ============================================================================

def load_generation_files(directory: Path) -> list[tuple[str, dict]]:
    """Load all *_parsed.json files from the Gemini prototype results."""
    generations = []
    for f in sorted(directory.glob("*_parsed.json")):
        try:
            with open(f, encoding="utf-8") as fh:
                data = json.load(fh)
            generations.append((f.name, data))
        except (json.JSONDecodeError, OSError) as e:
            print(f"WARNING: Could not load {f}: {e}")
    return generations


def run_batch(
    generations: list[tuple[str, dict]],
    schema: dict,
    dataset: dict,
    budget_amount: Decimal | None = None,
) -> BatchResult:
    """Validate a batch of generations and produce aggregate metrics."""
    batch = BatchResult()

    for filename, itinerary in generations:
        gen_result = validate_generation(
            itinerary=itinerary,
            schema=schema,
            dataset=dataset,
            budget_amount=budget_amount,
            test_id=filename.replace("_parsed.json", ""),
            source_file=filename,
        )
        batch.generation_results.append(gen_result)

        batch.total_generations += 1

        if gen_result.schema_validation.passed:
            batch.valid_json += 1
            batch.schema_valid += 1

        # Aggregate place metrics
        for opt in gen_result.options:
            pg = opt.place_grounding
            batch.total_place_refs += pg.generated_place_count
            batch.invalid_place_refs += pg.invalid_place_count
            if pg.passed:
                batch.generations_passing_place += 1
            else:
                batch.generations_failing_place += 1

        # Budget metrics
        for opt in gen_result.options:
            bf = opt.budget_feasibility
            if bf.checked:
                batch.budget_checked += 1
                if bf.passed:
                    batch.budget_pass += 1
                else:
                    batch.budget_fail += 1

        if gen_result.overall_passed:
            batch.overall_pass += 1
        else:
            batch.overall_fail += 1

    # Aggregate invented-place rate
    if batch.total_place_refs > 0:
        batch.invented_place_rate = float(
            Decimal(str(batch.invalid_place_refs))
            / Decimal(str(batch.total_place_refs))
        )

    return batch

# ============================================================================
# REPORTING
# ============================================================================

def generate_report(batch: BatchResult, budget_amount: Decimal | None) -> str:
    """Generate a human-readable Markdown report."""
    lines = []
    lines.append("# Triply — AI-Output Validation Harness Report\n")
    lines.append(f"**Schema version:** 2.0.0")
    lines.append(f"**Validation rules:** AI_OUTPUT_VALIDATION_RULES.md v2.0.0")
    if budget_amount is not None:
        lines.append(f"**Budget amount:** {budget_amount}")
    lines.append("")

    # Summary
    lines.append("## Summary\n")
    lines.append("| Metric | Value |")
    lines.append("|--------|-------|")
    lines.append(f"| Total generations | {batch.total_generations} |")
    lines.append(f"| Valid JSON | {batch.valid_json} |")
    lines.append(f"| Schema-valid | {batch.schema_valid} |")
    lines.append(f"| **Overall PASS** | **{batch.overall_pass}** |")
    lines.append(f"| Overall FAIL | {batch.overall_fail} |")
    lines.append("")

    # Place metrics
    lines.append("## Place Grounding (V-001)\n")
    lines.append("| Metric | Value |")
    lines.append("|--------|-------|")
    lines.append(f"| Total place references | {batch.total_place_refs} |")
    lines.append(f"| Invalid place references | {batch.invalid_place_refs} |")
    lines.append(f"| **Invented-place rate** | **{batch.invented_place_rate:.2%}** |")
    lines.append(f"| Generations passing | {batch.generations_passing_place} |")
    lines.append(f"| Generations failing | {batch.generations_failing_place} |")

    if batch.invalid_place_refs == 0:
        lines.append(f"\n**V-001 requirement: 0% invented places -> PASS**\n")
    else:
        lines.append(f"\n**V-001 requirement: 0% invented places -> FAIL**\n")

    # Budget metrics
    if batch.budget_checked > 0:
        lines.append("## Budget Feasibility (V-002)\n")
        lines.append("| Metric | Value |")
        lines.append("|--------|-------|")
        lines.append(f"| Options checked | {batch.budget_checked} |")
        lines.append(f"| Within budget | {batch.budget_pass} |")
        lines.append(f"| Over budget | {batch.budget_fail} |")
        lines.append("")

    # Per-generation details
    lines.append("## Per-Generation Results\n")
    lines.append("| Test | Mode | Schema | Places | Budget | Overall |")
    lines.append("|------|------|--------|--------|--------|---------|")

    for gr in batch.generation_results:
        schema_status = "PASS" if gr.schema_validation.passed else "FAIL"
        place_status = "PASS" if all(o.place_grounding.passed for o in gr.options) else "FAIL"
        budget_status = "PASS"
        for o in gr.options:
            if o.budget_feasibility.checked and not o.budget_feasibility.passed:
                budget_status = "FAIL"
        overall = "PASS" if gr.overall_passed else "FAIL"
        lines.append(
            f"| {gr.test_id} | {gr.planning_mode[:4]} | {schema_status} | "
            f"{place_status} | {budget_status} | {overall} |"
        )

    # Failure details
    failures = [gr for gr in batch.generation_results if not gr.overall_passed]
    if failures:
        lines.append("\n## Failure Details\n")
        for gr in failures:
            lines.append(f"### {gr.test_id}\n")
            for code in gr.failure_codes:
                lines.append(f"- `{code}`")
            for opt in gr.options:
                if not opt.place_grounding.passed:
                    for ip in opt.place_grounding.invalid_places:
                        lines.append(f"  - Invalid place: `{ip}`")
                    for fc in opt.place_grounding.failure_codes:
                        lines.append(f"  - Failure: `{fc}`")
            lines.append("")

    return "\n".join(lines)


def generate_machine_report(batch: BatchResult, budget_amount: Decimal | None) -> dict:
    """Generate a machine-readable JSON report per spec §7."""
    return {
        "summary": {
            "total_generations": batch.total_generations,
            "valid_json": batch.valid_json,
            "schema_valid": batch.schema_valid,
            "overall_pass": batch.overall_pass,
            "overall_fail": batch.overall_fail,
            "total_place_refs": batch.total_place_refs,
            "invalid_place_refs": batch.invalid_place_refs,
            "invented_place_rate": batch.invented_place_rate,
            "generations_passing_place": batch.generations_passing_place,
            "generations_failing_place": batch.generations_failing_place,
            "budget_checked": batch.budget_checked,
            "budget_pass": batch.budget_pass,
            "budget_fail": batch.budget_fail,
            "v001_requirement_met": batch.invalid_place_refs == 0,
        },
        "generation_results": [
            {
                "test_id": gr.test_id,
                "source_file": gr.source_file,
                "planning_mode": gr.planning_mode,
                "overall_passed": gr.overall_passed,
                "failure_codes": gr.failure_codes,
                "schema_validation": {
                    "passed": gr.schema_validation.passed,
                    "errors": gr.schema_validation.errors,
                },
                "options": [
                    {
                        "destination_name": opt.destination_name,
                        "passed": opt.passed,
                        "place_grounding": {
                            "passed": opt.place_grounding.passed,
                            "generated_place_count": opt.place_grounding.generated_place_count,
                            "resolved_place_count": opt.place_grounding.resolved_place_count,
                            "invalid_place_count": opt.place_grounding.invalid_place_count,
                            "invented_place_rate": opt.place_grounding.invented_place_rate,
                            "invalid_places": opt.place_grounding.invalid_places,
                            "failure_codes": opt.place_grounding.failure_codes,
                        },
                        "budget_feasibility": {
                            "checked": opt.budget_feasibility.checked,
                            "passed": opt.budget_feasibility.passed,
                            "options_total": opt.budget_feasibility.options_total,
                            "options_within_budget": opt.budget_feasibility.options_within_budget,
                            "over_budget_options": opt.budget_feasibility.over_budget_options,
                            "option_costs": opt.budget_feasibility.option_costs,
                            "failure_codes": opt.budget_feasibility.failure_codes,
                        },
                        "errors": opt.errors,
                    }
                    for opt in gr.options
                ],
            }
            for gr in batch.generation_results
        ],
    }

# ============================================================================
# FIXTURE TESTS
# ============================================================================

def build_fixtures() -> list[tuple[str, dict, str, Decimal | None]]:
    """
    Build deterministic test fixtures per spec §20.
    Returns: (name, itinerary, expected_overall, budget_amount)
    """
    fixtures = []

    # Fixture 1: Valid output — PASS
    fixtures.append(("FIX-01 Valid Amman", {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [{
            "destination_name": "Amman",
            "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
            "days": [
                {"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Airport Transfer \u2013 QAIA to Amman"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Roman Theatre & Museum of Popular Traditions"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Hashem Restaurant"},
                ]},
                {"day_number": 2, "date": "2026-11-11", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Amman Citadel & Jordan Archaeological Museum"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Jordan Heritage Restaurant"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Rainbow Street Evening Walk"},
                ]},
                {"day_number": 3, "date": "2026-11-12", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Intra-city Taxi (short ride)"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Levant Restaurant"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "City Mall Amman"},
                ]},
            ],
        }],
    }, "PASS", Decimal("400")))

    # Fixture 2: One invented place — FAIL
    fixtures.append(("FIX-02 Invented Place", {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [{
            "destination_name": "Amman",
            "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
            "days": [
                {"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Amman Citadel Museum"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Hashem Restaurant"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Rainbow Street Evening Walk"},
                ]},
                {"day_number": 2, "date": "2026-11-11", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Roman Theatre & Museum of Popular Traditions"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Jordan Heritage Restaurant"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "City Mall Amman"},
                ]},
                {"day_number": 3, "date": "2026-11-12", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Airport Transfer \u2013 QAIA to Amman"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Levant Restaurant"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Intra-city Taxi (short ride)"},
                ]},
            ],
        }],
    }, "FAIL", None))

    # Fixture 3: Budget over (DESTINATION_FIRST — flag only, PASS)
    fixtures.append(("FIX-03 Over Budget DEST", {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [{
            "destination_name": "New York",
            "accommodation": {"place_name": "The Ritz-Carlton New York, Central Park", "nights": 3},
            "days": [
                {"day_number": 1, "date": "2026-12-20", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Airport Transfer \u2013 JFK to Manhattan"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Empire State Building (86th floor)"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Le Bernardin"},
                ]},
                {"day_number": 2, "date": "2026-12-21", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Central Park"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Broadway Show (standard ticket)"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Wo Hop"},
                ]},
            ],
        }],
    }, "PASS", Decimal("500")))

    # Fixture 4: BUDGET_FIRST, all over budget — FAIL
    fixtures.append(("FIX-04 All Over Budget BUDG", {
        "planning_mode": "BUDGET_FIRST",
        "destination_options": [{
            "destination_name": "Paris",
            "accommodation": {"place_name": "Ritz Paris", "nights": 3},
            "days": [
                {"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Airport Transfer \u2013 CDG to city centre"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Louvre Museum"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Epicure (Le Bristol Paris)"},
                ]},
            ],
        }],
    }, "FAIL", Decimal("200")))

    # Fixture 5: Accommodation in days — FAIL
    fixtures.append(("FIX-05 Accommodation In Days", {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [{
            "destination_name": "Amman",
            "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 2},
            "days": [
                {"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Jordan Tower Hotel"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Hashem Restaurant"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Rainbow Street Evening Walk"},
                ]},
                {"day_number": 2, "date": "2026-11-11", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Roman Theatre & Museum of Popular Traditions"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Jordan Heritage Restaurant"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "City Mall Amman"},
                ]},
            ],
        }],
    }, "FAIL", None))

    # Fixture 6: Missing restaurant — FAIL
    fixtures.append(("FIX-06 Missing Restaurant", {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [{
            "destination_name": "Amman",
            "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 2},
            "days": [
                {"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Airport Transfer \u2013 QAIA to Amman"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Roman Theatre & Museum of Popular Traditions"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Rainbow Street Evening Walk"},
                ]},
                {"day_number": 2, "date": "2026-11-11", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Amman Citadel & Jordan Archaeological Museum"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "City Mall Amman"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Abdali Mall & Boulevard"},
                ]},
            ],
        }],
    }, "FAIL", None))

    # Fixture 7: Missing transport — FAIL
    fixtures.append(("FIX-07 Missing Transport", {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [{
            "destination_name": "Amman",
            "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 2},
            "days": [
                {"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Roman Theatre & Museum of Popular Traditions"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Hashem Restaurant"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Rainbow Street Evening Walk"},
                ]},
                {"day_number": 2, "date": "2026-11-11", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Amman Citadel & Jordan Archaeological Museum"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Jordan Heritage Restaurant"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "City Mall Amman"},
                ]},
            ],
        }],
    }, "FAIL", None))

    # Fixture 8: Invalid JSON (schema failure) — FAIL
    fixtures.append(("FIX-08 Invalid Schema", {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [],
    }, "FAIL", None))

    # Fixture 9: Budget-first with one option within budget — PASS
    fixtures.append(("FIX-09 Budget First Mixed", {
        "planning_mode": "BUDGET_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 2},
                "days": [
                    {"day_number": 1, "date": "2026-11-10", "items": [
                        {"time_slot": "MORNING", "order_index": 1, "place_name": "Airport Transfer \u2013 QAIA to Amman"},
                        {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Roman Theatre & Museum of Popular Traditions"},
                        {"time_slot": "EVENING", "order_index": 1, "place_name": "Hashem Restaurant"},
                    ]},
                    {"day_number": 2, "date": "2026-11-11", "items": [
                        {"time_slot": "MORNING", "order_index": 1, "place_name": "Amman Citadel & Jordan Archaeological Museum"},
                        {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Jordan Heritage Restaurant"},
                        {"time_slot": "EVENING", "order_index": 1, "place_name": "Rainbow Street Evening Walk"},
                    ]},
                ],
            },
            {
                "destination_name": "Paris",
                "accommodation": {"place_name": "Ritz Paris", "nights": 2},
                "days": [
                    {"day_number": 1, "date": "2026-11-10", "items": [
                        {"time_slot": "MORNING", "order_index": 1, "place_name": "Airport Transfer \u2013 CDG to city centre"},
                        {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Louvre Museum"},
                        {"time_slot": "EVENING", "order_index": 1, "place_name": "Epicure (Le Bristol Paris)"},
                    ]},
                ],
            },
        ],
    }, "PASS", Decimal("300")))

    # Fixture 10: Destination not found — FAIL
    fixtures.append(("FIX-10 Unknown Destination", {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [{
            "destination_name": "Tokyo",
            "accommodation": {"place_name": "Shinjuku Hotel", "nights": 3},
            "days": [
                {"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Senso-ji Temple"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Sushi Dai"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Tokyo Station Transfer"},
                ]},
            ],
        }],
    }, "FAIL", None))

    return fixtures


def run_fixture_tests(schema: dict, dataset: dict) -> bool:
    """Run all fixture tests and report results."""
    print("=" * 70)
    print("TRIPLY — Validation Harness Fixture Tests")
    print("=" * 70)

    fixtures = build_fixtures()
    all_pass = True

    for name, itinerary, expected, budget in fixtures:
        result = validate_generation(
            itinerary=itinerary,
            schema=schema,
            dataset=dataset,
            budget_amount=budget,
            test_id=name,
        )

        actual = "PASS" if result.overall_passed else "FAIL"
        status = "OK" if actual == expected else "UNEXPECTED"

        if actual != expected:
            all_pass = False

        print(f"\n{name}")
        print(f"  Expected: {expected} | Got: {actual} | {status}")
        if result.failure_codes:
            for fc in result.failure_codes[:3]:
                print(f"  Code: {fc}")
        for opt in result.options:
            if opt.place_grounding.invalid_places:
                for ip in opt.place_grounding.invalid_places[:2]:
                    print(f"  Invalid: {ip}")

    print("\n" + "=" * 70)
    if all_pass:
        print("ALL FIXTURE TESTS PASSED")
    else:
        print("SOME FIXTURE TESTS FAILED")
    print("=" * 70)

    return all_pass

# ============================================================================
# MAIN
# ============================================================================

def main():
    import argparse
    parser = argparse.ArgumentParser(description="Triply AI-Output Validation Harness")
    parser.add_argument("--input", help="Path to batch results JSON file or directory")
    parser.add_argument("--budget", type=str, help="Budget amount for V-002 (e.g. 400)")
    parser.add_argument("--test", action="store_true", help="Run fixture tests only")
    parser.add_argument("--output-dir", default=str(SCRIPT_DIR / "reports"),
                        help="Output directory for reports")
    args = parser.parse_args()

    # Load authoritative data
    print("Loading dataset...")
    dataset = load_dataset()
    print(f"  Active places: {dataset['total_places']}")
    print(f"  Supported destinations: {dataset['total_destinations']}")

    print("Loading schema...")
    schema = load_schema()
    print(f"  Schema: {schema['title']} v2.0.0")

    budget = Decimal(args.budget) if args.budget else None

    # Fixture tests
    if args.test:
        success = run_fixture_tests(schema, dataset)
        sys.exit(0 if success else 1)

    # Load generations
    if args.input:
        input_path = Path(args.input)
        if input_path.is_dir():
            generations = load_generation_files(input_path)
        elif input_path.is_file():
            with open(input_path, encoding="utf-8") as f:
                data = json.load(f)
            if isinstance(data, list):
                generations = [(f"gen_{i}", item) for i, item in enumerate(data)]
            elif isinstance(data, dict) and "destination_options" in data:
                generations = [("gen_0", data)]
            else:
                print(f"ERROR: Unrecognized input format in {input_path}")
                sys.exit(1)
        else:
            print(f"ERROR: Input path not found: {input_path}")
            sys.exit(1)
    else:
        # Default: load from Gemini prototype results
        generations = load_generation_files(GEMINI_RESULTS_DIR)
        if not generations:
            print("ERROR: No generation files found. Run --test or provide --input.")
            sys.exit(1)

    print(f"\nLoaded {len(generations)} generations")

    # Run batch validation
    print("Validating...")
    batch = run_batch(generations, schema, dataset, budget)

    # Generate reports
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    # Machine-readable report
    machine_report = generate_machine_report(batch, budget)
    machine_path = output_dir / "validation_results.json"
    with open(machine_path, "w", encoding="utf-8") as f:
        json.dump(machine_report, f, indent=2, ensure_ascii=False)
    print(f"\nMachine-readable report: {machine_path}")

    # Human-readable report
    md_report = generate_report(batch, budget)
    md_path = output_dir / "VALIDATION_REPORT.md"
    with open(md_path, "w", encoding="utf-8") as f:
        f.write(md_report)
    print(f"Human-readable report: {md_path}")

    # Console summary
    print("\n" + "=" * 70)
    print("VALIDATION SUMMARY")
    print("=" * 70)
    print(f"Total generations: {batch.total_generations}")
    print(f"Valid JSON: {batch.valid_json}")
    print(f"Schema-valid: {batch.schema_valid}")
    print(f"Overall PASS: {batch.overall_pass}")
    print(f"Overall FAIL: {batch.overall_fail}")
    print(f"\nPlace Grounding (V-001):")
    print(f"  Total place references: {batch.total_place_refs}")
    print(f"  Invalid place references: {batch.invalid_place_refs}")
    print(f"  Invented-place rate: {batch.invented_place_rate:.2%}")
    print(f"  V-001 requirement (0%): {'PASS' if batch.invalid_place_refs == 0 else 'FAIL'}")

    if batch.budget_checked > 0:
        print(f"\nBudget Feasibility (V-002):")
        print(f"  Options checked: {batch.budget_checked}")
        print(f"  Within budget: {batch.budget_pass}")
        print(f"  Over budget: {batch.budget_fail}")
    print("=" * 70)

    return 0 if batch.overall_fail == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
