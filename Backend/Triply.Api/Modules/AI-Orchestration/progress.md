# AI-Orchestration Progress

**Owner:** Leen Sharbati  
**Track:** Backend  
**Status:** In Progress

## TASK45 — Gemini HTTP Call
- [x] Gemini client and configuration
- [x] Prompt builder and response parsing
- [x] `responseJsonSchema` support
- [x] v2.0.0 schema added as a source-controlled backend artifact
- [ ] Test with real Gemini API

## TASK46 — AI Validation
- [x] Place existence and active-status validation
- [x] Destination grounding and category validation
- [x] v2.0.0 name-based grounding rules
- [x] Budget-first destination scoping validation
- [ ] Test validation with a real Gemini response

## TASK47 — AI Generation
- [x] AI orchestration and generation endpoint
- [x] Bounded retry handling
- [x] Name-to-ID resolution and itinerary persistence
- [x] `DESTINATION_FIRST` and `BUDGET_FIRST` prompt selection
- [x] Gemini generation now uses `GenerateJsonWithSchemaAsync`
- [ ] Test real Gemini generation end-to-end

## TASK48 — Cost Aggregation
- [x] Calculate costs from persisted itinerary values
- [x] Accommodation cost uses `reference_price × nights`
- [x] Persist `CostEstimate` rows
- [x] Update Trip total estimated cost
- [ ] Verify end-to-end AI generation → cost calculation

## Budget-first dataset dependency
- [x] Backend loader added for `Extra_AI_Context.csv`
- [x] Supports lookup by `place_id`/`id` or `place_name`/`name`
- [ ] Populate `budget_tier` data in `Extra_AI_Context.csv` (the supplied file is currently empty)
- [ ] Run BUDGET_FIRST generation after dataset is populated

## Current Status
- Build: **not run in this environment** (the uploaded ZIP environment does not include the .NET SDK)
- Real Gemini test: **PENDING** (requires a valid Gemini API key)
- Schema alignment: **DONE — v2.0.0**
- Backend JSON-schema generation path: **DONE**
- Budget-tier integration: **CODE READY / DATA BLOCKED** because the supplied `Extra_AI_Context.csv` is 0 bytes

## Next
1. Run `dotnet build` and `dotnet test` on the development machine.
2. Populate `Extra_AI_Context.csv` with the AI team's approved `budget_tier` values.
3. Run a real Gemini generation using the v2.0.0 JSON schema.
4. Verify the persisted itinerary, CostEstimate rows, and Trip total.
