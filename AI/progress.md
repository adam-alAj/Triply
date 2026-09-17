# Triply AI — Progress

*Owner:Aya Maali · Track: AI/ML · Last updated: 2026-09-17*

This document tracks new work done on the AI track from this point forward — what was added, why, and its current status — so the rest of the team can follow along without digging through commits.

---

## 1. Dataset Environment Setup (Python/Pandas/Jupyter)

**Task objective:** Stand up the local environment used for dataset curation and the validation harness.

| Step                                | Detail                                                                                                                                                                                                                                                         | Status        |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------- |
| Reviewed existing repo state        | Confirmed dataset v1.0.0 already curated (`REVIEW_RECORD.md`), but no formal Python/Pandas/Jupyter tooling existed — `seed_places.py` and `generate_manifest.py` use only the stdlib `csv` module, and `notebooks/` had an `.xlsx` file but no actual notebook | ✅ Done        |
| Python version confirmed            | `3.13.14` (above the 3.10 minimum in `seed/README.md`)                                                                                                                                                                                                         | ✅ Done        |
| Virtual environment created         | `AI/01-Dataset/.venv` (kept separate from the rest of the project)                                                                                                                                                                                             | ✅ Done        |
| Dedicated requirements file created | `AI/01-Dataset/notebooks/requirements.txt` — `pandas`, `jupyter`, `openpyxl`, `jsonschema`. **Kept separate from `seed/requirements.txt`** (that one stays production-only, `pyodbc` only)                                                                     | ✅ Done        |
| Install dependencies                | `pip install -r notebooks/requirements.txt` — installed cleanly, no errors                                                                                                                                                                                     | ✅ Done        |
| First verification notebook         | `notebooks/dataset_environment_check.ipynb` — load every `curated-data/` CSV with pandas, check shape/nulls, cross-check row counts against `seed/DATASET_MANIFEST.json`                                                                                       | ⬜ Not started |
| Commit to git                       | Add `.venv/` to `.gitignore`, commit notebook + requirements.txt                                                                                                                                                                                               | ⬜ Not started |

---

## 2. Interest-Aware Destination Suggestions — Backend Coordination

**Gap found:** `DestinationSuggestionService.cs` requires `InterestCategoryIds` in the request but never uses it — suggestions are budget-only today. Root cause: no DB relationship between `InterestCategory` and `Destination`/`Place`.

| Step                                                  | Detail                                                                                                                                                                                             | Status            |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------- |
| Investigated backend code                             | Confirmed the gap in `DestinationSuggestionService.cs`, `DestinationSuggestionDtos.cs`, `DestinationSuggestionRequestValidator.cs`, and the entity model (`ReferenceEntities.cs`, `TripSchema.cs`) | ✅ Done            |
| Cross-checked data readiness                          | `Extra_AI_Context.csv` already has `interest_tag` per place; all non-blank values map 1:1 to `InterestCategory.Code`. 9 of 57 places have a blank tag                                              | ✅ Done            |
| Spec written                                          | `AI/05-Destination-Suggestion/Interest_Aware_Destination_Suggestions_Spec.md` — proposes a `PlaceInterest` table, matching/ranking logic, and open questions for Backend                           | ✅ Done            |
| Sent to Backend lead                                  | Sent to Lynn for review                                                                                                                                                                            | ✅ Done            |
| Pushed to GitHub                                      | `AI/05-Destination-Suggestion/` folder created and pushed                                                                                                                                          | ✅ Done            |
| Backend decision (schema + open questions in spec §5) | —                                                                                                                                                                                                  | ⬜ Waiting on Lynn |
| Fill the 9 missing `interest_tag` values              | So the seed data is ready the moment the schema is approved                                                                                                                                        | ⬜ Not started     |

---

## 3. Not Yet Started

- Validation harness (`AI/03-Validation/place-existence-check/` — currently empty)
- `PlaceInterest` seed file + seeder update (blocked on Backend decision, §2 above)

---

*Update this file after each new step — add a row, or a new numbered section for a new work area, so it stays a running log the whole team can read.*
