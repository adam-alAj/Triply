#!/usr/bin/env python3
"""
Triply — Gemini JSON Output Schema Validation Harness
Schema version: 2.0.0
JSON Schema draft: 2020-12

Validates representative Gemini itinerary outputs against the finalized schema.
Used by AI/ML track for contract enforcement and by Backend for integration testing.

Usage:
    python validate_schema.py                  # Run all test cases
    python validate_schema.py --schema-only    # Validate schema file itself
    python validate_schema.py --json FILE      # Validate a specific JSON file
"""

import json
import sys
import os
from pathlib import Path

try:
    from jsonschema import validate, ValidationError, Draft202012Validator
    from jsonschema.exceptions import SchemaError
except ImportError:
    print("ERROR: jsonschema not installed. Run: pip install jsonschema")
    sys.exit(1)


SCRIPT_DIR = Path(__file__).resolve().parent
SCHEMA_PATH = SCRIPT_DIR.parent / "02-Prompt-Engineering" / "json-schemas" / "triply-trip-plan-generation.schema.json"


def load_schema():
    """Load and validate the schema file itself."""
    with open(SCHEMA_PATH, encoding="utf-8") as f:
        schema = json.load(f)
    # Validate the schema is a valid JSON Schema
    Draft202012Validator.check_schema(schema)
    return schema


def validate_output(schema, instance, label=""):
    """Validate an output against the schema. Returns (passed, error_msg)."""
    try:
        validate(instance=instance, schema=schema, cls=Draft202012Validator)
        return True, None
    except ValidationError as e:
        path = " → ".join(str(p) for p in e.absolute_path) if e.absolute_path else "(root)"
        return False, f"[{path}] {e.message}"


# ============================================================================
# TEST CASES
# ============================================================================

def case_1_valid_destination_first():
    """Case 1: Valid DESTINATION_FIRST itinerary — should PASS."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {
                    "place_name": "Jordan Tower Hotel",
                    "nights": 3
                },
                "days": [
                    {
                        "day_number": 1,
                        "date": "2026-11-10",
                        "items": [
                            {
                                "time_slot": "MORNING",
                                "order_index": 1,
                                "place_name": "Roman Theatre & Museum of Popular Traditions",
                                "notes": "Start early to avoid crowds."
                            },
                            {
                                "time_slot": "AFTERNOON",
                                "order_index": 1,
                                "place_name": "Hashem Restaurant",
                                "notes": None
                            },
                            {
                                "time_slot": "EVENING",
                                "order_index": 1,
                                "place_name": "Rainbow Street Evening Walk"
                            }
                        ]
                    },
                    {
                        "day_number": 2,
                        "date": "2026-11-11",
                        "items": [
                            {
                                "time_slot": "MORNING",
                                "order_index": 1,
                                "place_name": "Amman Citadel & Jordan Archaeological Museum"
                            },
                            {
                                "time_slot": "AFTERNOON",
                                "order_index": 1,
                                "place_name": "Jordan Heritage Restaurant"
                            },
                            {
                                "time_slot": "EVENING",
                                "order_index": 1,
                                "place_name": "City Mall Amman"
                            }
                        ]
                    },
                    {
                        "day_number": 3,
                        "date": "2026-11-12",
                        "items": [
                            {
                                "time_slot": "MORNING",
                                "order_index": 1,
                                "place_name": "Airport Transfer – QAIA to Amman"
                            },
                            {
                                "time_slot": "AFTERNOON",
                                "order_index": 1,
                                "place_name": "Levant Restaurant"
                            },
                            {
                                "time_slot": "EVENING",
                                "order_index": 1,
                                "place_name": "Intra-city Taxi (short ride)"
                            }
                        ]
                    }
                ]
            }
        ]
    }, "PASS"


def case_2_valid_budget_first():
    """Case 2: Valid BUDGET_FIRST itinerary with 2 options — should PASS."""
    return {
        "planning_mode": "BUDGET_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {
                    "place_name": "Jordan Tower Hotel",
                    "nights": 3
                },
                "days": [
                    {
                        "day_number": 1,
                        "date": "2026-11-10",
                        "items": [
                            {
                                "time_slot": "MORNING",
                                "order_index": 1,
                                "place_name": "Roman Theatre & Museum of Popular Traditions"
                            },
                            {
                                "time_slot": "AFTERNOON",
                                "order_index": 1,
                                "place_name": "Hashem Restaurant"
                            },
                            {
                                "time_slot": "EVENING",
                                "order_index": 1,
                                "place_name": "Airport Transfer – QAIA to Amman"
                            }
                        ]
                    }
                ]
            },
            {
                "destination_name": "Paris",
                "accommodation": {
                    "place_name": "Hôtel Le Clos Notre-Dame",
                    "nights": 3
                },
                "days": [
                    {
                        "day_number": 1,
                        "date": "2026-11-10",
                        "items": [
                            {
                                "time_slot": "MORNING",
                                "order_index": 1,
                                "place_name": "Eiffel Tower (2nd floor, lift)"
                            },
                            {
                                "time_slot": "AFTERNOON",
                                "order_index": 1,
                                "place_name": "Bouillon Chartier Grands Boulevards"
                            },
                            {
                                "time_slot": "EVENING",
                                "order_index": 1,
                                "place_name": "Seine River Cruise"
                            }
                        ]
                    }
                ]
            }
        ]
    }, "PASS"


def case_3_missing_required_field():
    """Case 3: Missing required field 'planning_mode' — should FAIL."""
    return {
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
                "days": [{"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Hashem Restaurant"}
                ]}]
            }
        ]
    }, "FAIL"


def case_4_wrong_type():
    """Case 4: Wrong data type — 'nights' as string instead of integer — should FAIL."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": "three"},
                "days": [{"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Hashem Restaurant"}
                ]}]
            }
        ]
    }, "FAIL"


def case_5_invalid_enum():
    """Case 5: Invalid enum value for planning_mode — should FAIL."""
    return {
        "planning_mode": "BUDGET_BASED",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
                "days": [{"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Hashem Restaurant"}
                ]}]
            }
        ]
    }, "FAIL"


def case_6_invalid_time_slot():
    """Case 6: Invalid time_slot enum — should FAIL."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
                "days": [{"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "NOON", "order_index": 1, "place_name": "Hashem Restaurant"}
                ]}]
            }
        ]
    }, "FAIL"


def case_7_optional_notes_omitted():
    """Case 7: Optional 'notes' field omitted — should PASS."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
                "days": [{"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Hashem Restaurant"}
                ]}]
            }
        ]
    }, "PASS"


def case_8_unexpected_field():
    """Case 8: Unexpected field at root level — should FAIL (additionalProperties: false)."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
                "days": [{"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Hashem Restaurant"}
                ]}]
            }
        ],
        "cost_summary": {"total": 200}
    }, "FAIL"


def case_9_realistic_dataset_values():
    """Case 9: Realistic values from actual dataset (Paris) — should PASS."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Paris",
                "accommodation": {
                    "place_name": "Novotel Paris 20 Belleville",
                    "nights": 4
                },
                "days": [
                    {
                        "day_number": 1,
                        "date": "2026-11-03",
                        "items": [
                            {
                                "time_slot": "MORNING",
                                "order_index": 1,
                                "place_name": "Airport Transfer – CDG to city centre",
                                "notes": "RER B train recommended."
                            },
                            {
                                "time_slot": "AFTERNOON",
                                "order_index": 1,
                                "place_name": "Eiffel Tower (2nd floor, lift)",
                                "notes": "Book online to skip the queue."
                            },
                            {
                                "time_slot": "EVENING",
                                "order_index": 1,
                                "place_name": "Chez Le Libanais",
                                "notes": None
                            }
                        ]
                    },
                    {
                        "day_number": 2,
                        "date": "2026-11-04",
                        "items": [
                            {
                                "time_slot": "MORNING",
                                "order_index": 1,
                                "place_name": "Louvre Museum"
                            },
                            {
                                "time_slot": "AFTERNOON",
                                "order_index": 1,
                                "place_name": "Galeries Lafayette Haussmann"
                            },
                            {
                                "time_slot": "EVENING",
                                "order_index": 1,
                                "place_name": "Bouillon Chartier Grands Boulevards"
                            }
                        ]
                    }
                ]
            }
        ]
    }, "PASS"


def case_10_empty_destination_options():
    """Case 10: Empty destination_options array — should FAIL (minItems: 1)."""
    return {
        "planning_mode": "BUDGET_FIRST",
        "destination_options": []
    }, "FAIL"


def case_11_notes_with_null():
    """Case 11: notes explicitly set to null — should PASS."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
                "days": [{"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Hashem Restaurant", "notes": None}
                ]}]
            }
        ]
    }, "PASS"


def case_12_multiple_items_same_slot():
    """Case 12: Multiple items in same time_slot with different order_index — should PASS."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Paris",
                "accommodation": {"place_name": "Hôtel Le Clos Notre-Dame", "nights": 2},
                "days": [{"day_number": 1, "date": "2026-11-03", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Eiffel Tower (2nd floor, lift)"},
                    {"time_slot": "MORNING", "order_index": 2, "place_name": "Seine River Cruise"},
                    {"time_slot": "AFTERNOON", "order_index": 1, "place_name": "Louvre Museum"},
                    {"time_slot": "EVENING", "order_index": 1, "place_name": "Bouillon Chartier Grands Boulevards"}
                ]}]
            }
        ]
    }, "PASS"


def case_13_nights_zero():
    """Case 13: nights = 0 (below minimum 1) — should FAIL."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 0},
                "days": [{"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 1, "place_name": "Hashem Restaurant"}
                ]}]
            }
        ]
    }, "FAIL"


def case_14_order_index_zero():
    """Case 14: order_index = 0 (below minimum 1) — should FAIL."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "Amman",
                "accommodation": {"place_name": "Jordan Tower Hotel", "nights": 3},
                "days": [{"day_number": 1, "date": "2026-11-10", "items": [
                    {"time_slot": "MORNING", "order_index": 0, "place_name": "Hashem Restaurant"}
                ]}]
            }
        ]
    }, "FAIL"


def case_15_new_york_realistic():
    """Case 15: Realistic New York itinerary — should PASS."""
    return {
        "planning_mode": "DESTINATION_FIRST",
        "destination_options": [
            {
                "destination_name": "New York",
                "accommodation": {
                    "place_name": "Hampton Inn Manhattan Grand Central",
                    "nights": 4
                },
                "days": [
                    {
                        "day_number": 1,
                        "date": "2026-12-20",
                        "items": [
                            {
                                "time_slot": "MORNING",
                                "order_index": 1,
                                "place_name": "Airport Transfer – JFK to Manhattan"
                            },
                            {
                                "time_slot": "AFTERNOON",
                                "order_index": 1,
                                "place_name": "Central Park"
                            },
                            {
                                "time_slot": "EVENING",
                                "order_index": 1,
                                "place_name": "Joe's Pizza"
                            }
                        ]
                    },
                    {
                        "day_number": 2,
                        "date": "2026-12-21",
                        "items": [
                            {
                                "time_slot": "MORNING",
                                "order_index": 1,
                                "place_name": "Empire State Building (86th floor)"
                            },
                            {
                                "time_slot": "AFTERNOON",
                                "order_index": 1,
                                "place_name": "Brooklyn Bridge Walk"
                            },
                            {
                                "time_slot": "EVENING",
                                "order_index": 1,
                                "place_name": "Wo Hop"
                            }
                        ]
                    }
                ]
            }
        ]
    }, "PASS"


# ============================================================================
# RUNNER
# ============================================================================

ALL_CASES = [
    ("Case 1: Valid DESTINATION_FIRST itinerary", case_1_valid_destination_first),
    ("Case 2: Valid BUDGET_FIRST with 2 options", case_2_valid_budget_first),
    ("Case 3: Missing required field (planning_mode)", case_3_missing_required_field),
    ("Case 4: Wrong type (nights as string)", case_4_wrong_type),
    ("Case 5: Invalid enum (planning_mode)", case_5_invalid_enum),
    ("Case 6: Invalid enum (time_slot)", case_6_invalid_time_slot),
    ("Case 7: Optional notes omitted", case_7_optional_notes_omitted),
    ("Case 8: Unexpected root field", case_8_unexpected_field),
    ("Case 9: Realistic Paris dataset values", case_9_realistic_dataset_values),
    ("Case 10: Empty destination_options", case_10_empty_destination_options),
    ("Case 11: notes = null", case_11_notes_with_null),
    ("Case 12: Multiple items same time_slot", case_12_multiple_items_same_slot),
    ("Case 13: nights = 0 (below minimum)", case_13_nights_zero),
    ("Case 14: order_index = 0 (below minimum)", case_14_order_index_zero),
    ("Case 15: Realistic New York itinerary", case_15_new_york_realistic),
]


def run_all_cases(schema):
    print("=" * 70)
    print("TRIPLY — Gemini Output Schema Validation Harness")
    print(f"Schema: triply-trip-plan-generation.schema.json (v2.0.0)")
    print(f"JSON Schema: draft/2020-12")
    print("=" * 70)

    passed = 0
    failed = 0
    unexpected = 0

    for name, case_fn in ALL_CASES:
        instance, expected = case_fn()
        is_valid, error_msg = validate_output(schema, instance)

        actual = "PASS" if is_valid else "FAIL"
        status = "PASS" if actual == expected else "UNEXPECTED"

        if actual == expected:
            passed += 1
        else:
            unexpected += 1

        print(f"\n{name}")
        print(f"  Expected: {expected} | Got: {actual} | {status}")
        if error_msg and not is_valid:
            # Show a concise error (encode for Windows console safety)
            short_err = error_msg[:120] + "..." if len(error_msg) > 120 else error_msg
            try:
                print(f"  Error: {short_err}")
            except UnicodeEncodeError:
                print(f"  Error: {short_err.encode('ascii', 'replace').decode()}")

    total = len(ALL_CASES)
    print("\n" + "=" * 70)
    print(f"RESULTS: {passed}/{total} passed, {unexpected} unexpected, {total - passed - unexpected} expected failures")
    if unexpected == 0:
        print("ALL TEST CASES PASSED")
    else:
        print(f"WARNING: {unexpected} test(s) had unexpected results")
    print("=" * 70)

    return unexpected == 0


def validate_schema_file():
    """Validate the schema file itself."""
    print("Validating schema file...")
    try:
        schema = load_schema()
        print(f"  Schema file: {SCHEMA_PATH}")
        print(f"  JSON parse: PASS")
        print(f"  JSON Schema valid: PASS")
        print(f"  $schema: {schema.get('$schema')}")
        print(f"  title: {schema.get('title')}")
        print(f"  top-level required: {schema.get('required')}")
        print(f"  $defs: {list(schema.get('$defs', {}).keys())}")
        return schema
    except SchemaError as e:
        print(f"  JSON Schema valid: FAIL — {e.message}")
        return None
    except Exception as e:
        print(f"  ERROR: {e}")
        return None


def validate_specific_file(schema, filepath):
    """Validate a specific JSON file against the schema."""
    with open(filepath, encoding="utf-8") as f:
        instance = json.load(f)
    is_valid, error_msg = validate_output(schema, instance)
    if is_valid:
        print(f"PASS: {filepath} — VALID")
    else:
        print(f"FAIL: {filepath} — INVALID: {error_msg}")
    return is_valid


if __name__ == "__main__":
    schema = validate_schema_file()
    if schema is None:
        sys.exit(1)

    if "--schema-only" in sys.argv:
        sys.exit(0)

    if "--json" in sys.argv:
        idx = sys.argv.index("--json")
        if idx + 1 < len(sys.argv):
            ok = validate_specific_file(schema, sys.argv[idx + 1])
            sys.exit(0 if ok else 1)
        else:
            print("ERROR: --json requires a file path")
            sys.exit(1)

    success = run_all_cases(schema)
    sys.exit(0 if success else 1)
