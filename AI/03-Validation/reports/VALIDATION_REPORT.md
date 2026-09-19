# Triply — AI-Output Validation Harness Report

**Schema version:** 2.0.0
**Validation rules:** AI_OUTPUT_VALIDATION_RULES.md v2.0.0
**Budget amount:** 400

## Summary

| Metric | Value |
|--------|-------|
| Total generations | 10 |
| Valid JSON | 10 |
| Schema-valid | 10 |
| **Overall PASS** | **10** |
| Overall FAIL | 0 |

## Place Grounding (V-001)

| Metric | Value |
|--------|-------|
| Total place references | 138 |
| Invalid place references | 0 |
| **Invented-place rate** | **0.00%** |
| Generations passing | 13 |
| Generations failing | 0 |

**V-001 requirement: 0% invented places -> PASS**

## Budget Feasibility (V-002)

| Metric | Value |
|--------|-------|
| Options checked | 13 |
| Within budget | 13 |
| Over budget | 0 |

## Per-Generation Results

| Test | Mode | Schema | Places | Budget | Overall |
|------|------|--------|--------|--------|---------|
| A | DEST | PASS | PASS | PASS | PASS |
| B | DEST | PASS | PASS | PASS | PASS |
| C | DEST | PASS | PASS | PASS | PASS |
| D | DEST | PASS | PASS | PASS | PASS |
| E | BUDG | PASS | PASS | PASS | PASS |
| G | DEST | PASS | PASS | PASS | PASS |
| I | DEST | PASS | PASS | PASS | PASS |
| J | BUDG | PASS | PASS | PASS | PASS |
| K | DEST | PASS | PASS | PASS | PASS |
| L | DEST | PASS | PASS | PASS | PASS |