using Triply.Api.Modules.Cost.Dtos;
using Triply.Api.Modules.Itinerary.Dtos;

namespace Triply.Api.Modules.AIOrchestration.Dtos;

// ============================================================================
// PROVISIONAL — this is the JSON contract Gemini is asked to return.
// Field names here MUST be reconciled with AI/ML's "Finalize Gemini JSON Output
// Schema Against the Agreed Contract" deliverable (Dependency #3 on TASK45).
// Only this file + the prompt template in ItineraryPromptBuilder need to change
// if the agreed contract uses different field names.
// ============================================================================

public sealed class GeminiItineraryOutputDto
{
    public List<GeminiItineraryDayDto> Days { get; set; } = new();
}

public sealed class GeminiItineraryDayDto
{
    public int DayNumber { get; set; }
    public DateOnly Date { get; set; }
    public List<GeminiItineraryItemDto> Items { get; set; } = new();
}

public sealed class GeminiItineraryItemDto
{
    // Must be one of the Place.Id values handed to the model in the grounding context —
    // this is the field the 0%-invented-place check (FR-AI-002) is enforced against.
    public long PlaceId { get; set; }
    public string TimeSlot { get; set; } = default!; // MORNING | AFTERNOON | EVENING
    public int OrderIndex { get; set; }
    public string? Notes { get; set; }
}

// Dataset context fed to the model as grounding (Architecture §9: "Fetch candidate
// places/pricing for context"). Only what the model needs to choose validly.
public sealed class PlaceContextDto
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public decimal ReferencePrice { get; set; }
    public string Currency { get; set; } = default!;
}

public sealed class ItineraryValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public sealed class AiGenerationResult
{
    public bool Success { get; set; }
    public Guid AiGenerationId { get; set; }
    public string Status { get; set; } = default!; // SUCCEEDED | FAILED_VALIDATION | FAILED_ERROR
    public int AttemptsUsed { get; set; }
    public List<string> Errors { get; set; } = new();
    public ItineraryResponse? Itinerary { get; set; }
    public CostEstimateResponse? Cost { get; set; }
}
