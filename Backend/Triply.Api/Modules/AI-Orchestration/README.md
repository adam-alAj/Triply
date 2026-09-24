# AI-Orchestration Module

Calls Gemini and makes sure the result is safe to use.

**Flow:** load dataset places → build prompt → call Gemini (JSON schema v2.0.0) → validate → retry or fail → save itinerary + costs.

| File | Job |
|---|---|
| `AiGenerationController.cs` | `POST /api/trips/{id}/generate` (`FULL` / `DAY` / `ITEM`) |
| `AiOrchestrationService.cs` | Runs the whole flow, retries, budget rules |
| `GeminiClient.cs` / `GeminiOptions.cs` | The HTTP call and its settings |
| `ItineraryPromptBuilder.cs` | Builds the prompt per planning mode |
| `ItineraryGenerationSchema.cs` | Loads the JSON schema (`AI-Schemas/`) |
| `ItineraryValidationService.cs` | Checks every place against the dataset |
| `DatasetContextService.cs` | Supplies candidate places and prices |
| `AiRawOutputRetention.cs` | Clears raw Gemini output after 30 days |

**Rule:** the AI never writes to the database directly. Only validated data is saved. Failed attempts return `422` (validation) or `502` (Gemini error), never a made-up plan.

Status and details: [progress.md](progress.md).
