using Triply.Api.Modules.Cost.Dtos;
using Triply.Api.Modules.Itinerary.Dtos;
using System.Text.Json.Serialization;
namespace Triply.Api.Modules.AIOrchestration.Dtos;

// ============================================================================
// v2.0.0 — Aligned with AI/ML JSON Schema Contract v2.0.0
// (AI/docs/TRIPLY_AI_JSON_SCHEMA_CONTRACT_v2.md)
//
// Field names match the finalized schema exactly:
//   triply-trip-plan-generation.schema.json
//
// Key v2.0.0 changes from v1.0.0:
//   - Root shape: { planning_mode, destination_options[] } (not flat Days[])
//   - place_name (string) replaces PlaceId (numeric) — AI never outputs IDs
//   - accommodation object is separate from days (hotel is trip-scoped)
//   - No cost/price fields — all pricing is server-side from Place.reference_price
//   - planning_mode enum: DESTINATION_FIRST | BUDGET_FIRST
// ============================================================================

/// <summary>
/// Top-level Gemini response matching the v2.0.0 contract root shape.
/// </summary>
public sealed class GeminiItineraryOutputDto
{
    [JsonPropertyName("planning_mode")]
    public string PlanningMode { get; set; } = default!;

    [JsonPropertyName("destination_options")]
    public List<GeminiDestinationOptionDto> DestinationOptions { get; set; } = new();
}
/// <summary>
/// One complete destination plan (v2.0.0 §4.2).
/// </summary>
public sealed class GeminiDestinationOptionDto
{
    [JsonPropertyName("destination_name")]
    public string DestinationName { get; set; } = default!;

    [JsonPropertyName("accommodation")]
    public GeminiAccommodationDto Accommodation { get; set; } = default!;

    [JsonPropertyName("days")]
    public List<GeminiItineraryDayDto> Days { get; set; } = new();
}

/// <summary>
/// Accommodation for a destination option (v2.0.0 §4.3).
/// Trip-scoped, not day-scoped. Backend computes nights × Place.reference_price.
/// </summary>
public sealed class GeminiAccommodationDto
{
    [JsonPropertyName("place_name")]
    public string PlaceName { get; set; } = default!;

    [JsonPropertyName("nights")]
    public int Nights { get; set; }
}

/// <summary>
/// One day in the itinerary (v2.0.0 §4.4).
/// </summary>
public sealed class GeminiItineraryDayDto
{
    [JsonPropertyName("day_number")]
    public int DayNumber { get; set; }

    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }

    [JsonPropertyName("items")]
    public List<GeminiItineraryItemDto> Items { get; set; } = new();
}
/// <summary>
/// One itinerary item within a day (v2.0.0 §4.5).
/// </summary>
public sealed class GeminiItineraryItemDto
{
    [JsonPropertyName("time_slot")]
    public string TimeSlot { get; set; } = default!;

    [JsonPropertyName("order_index")]
    public int OrderIndex { get; set; }

    [JsonPropertyName("place_name")]
    public string PlaceName { get; set; } = default!;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
// ============================================================================
// Dataset context fed to the model as grounding (Architecture §9).
// Only what the model needs to choose validly — no IDs in the prompt context
// that the model could echo back; names only.
// ============================================================================

/// <summary>
/// Place context for the prompt's grounding list. Includes budget_tier for
/// budget-first mode so the model can judge affordability without seeing prices.
/// </summary>
public sealed class PlaceContextDto
{
    public long Id { get; set; }
    public long DestinationId { get; set; }
    public string DestinationName { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public decimal ReferencePrice { get; set; }
    public string Currency { get; set; } = default!;
    public string? BudgetTier { get; set; }
}

/// <summary>Destination context for budget-first mode.</summary>
public sealed class DestinationContextDto
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}

// ============================================================================
// Validation result
// ============================================================================

public sealed class ItineraryValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

// ============================================================================
// Orchestration result
// ============================================================================

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

public sealed class GenerateItineraryRequest
{
    /// <summary>FULL, DAY, or ITEM. Omit or use FULL for the existing full-trip flow.</summary>
    public string Scope { get; set; } = "FULL";
    public int? DayNumber { get; set; }
    public Guid? ItemId { get; set; }
}
