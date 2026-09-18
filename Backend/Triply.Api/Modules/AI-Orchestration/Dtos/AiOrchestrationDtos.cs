using Triply.Api.Modules.Cost.Dtos;
using Triply.Api.Modules.Itinerary.Dtos;

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
    /// <summary>Echo of Trip.planning_mode. Backend cross-checks it matches the request.</summary>
    public string PlanningMode { get; set; } = default!;

    /// <summary>
    /// DESTINATION_FIRST: exactly 1 entry.
    /// BUDGET_FIRST: 1–3 entries, each a complete self-contained plan.
    /// </summary>
    public List<GeminiDestinationOptionDto> DestinationOptions { get; set; } = new();
}

/// <summary>
/// One complete destination plan (v2.0.0 §4.2).
/// </summary>
public sealed class GeminiDestinationOptionDto
{
    /// <summary>
    /// Must exactly match a Destination.name from the supported destination list.
    /// Resolved server-side to Destination.id by exact lookup.
    /// </summary>
    public string DestinationName { get; set; } = default!;

    /// <summary>
    /// Exactly one hotel/accommodation for the whole stay. Not repeated inside days.
    /// </summary>
    public GeminiAccommodationDto Accommodation { get; set; } = default!;

    /// <summary>
    /// One entry per day, in order. Length must match trip duration.
    /// </summary>
    public List<GeminiItineraryDayDto> Days { get; set; } = new();
}

/// <summary>
/// Accommodation for a destination option (v2.0.0 §4.3).
/// Trip-scoped, not day-scoped. Backend computes nights × Place.reference_price.
/// </summary>
public sealed class GeminiAccommodationDto
{
    /// <summary>
    /// Must exactly match a Place.name whose category is ACCOMMODATION,
    /// scoped to this option's destination_name.
    /// </summary>
    public string PlaceName { get; set; } = default!;

    /// <summary>
    /// Number of nights. Backend multiplies by Place.reference_price (per-night rate).
    /// </summary>
    public int Nights { get; set; }
}

/// <summary>
/// One day in the itinerary (v2.0.0 §4.4).
/// </summary>
public sealed class GeminiItineraryDayDto
{
    /// <summary>1-based sequential day number. Maps to ItineraryDay.DayNumber.</summary>
    public int DayNumber { get; set; }

    /// <summary>ISO 8601 date (YYYY-MM-DD). Maps to ItineraryDay.Date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Non-accommodation entries (restaurants, attractions, activities, transport).
    /// Must include at least one RESTAURANT-category place per day.
    /// </summary>
    public List<GeminiItineraryItemDto> Items { get; set; } = new();
}

/// <summary>
/// One itinerary item within a day (v2.0.0 §4.5).
/// </summary>
public sealed class GeminiItineraryItemDto
{
    /// <summary>MORNING | AFTERNOON | EVENING. Matches DB CHECK constraint.</summary>
    public string TimeSlot { get; set; } = default!;

    /// <summary>Order within the time_slot, starting at 1. Unique per (day_number, time_slot).</summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// FR-AI-002 enforcement field. Must exactly match a Place.name for an
    /// is_active place whose destination_id matches the option's destination_name.
    /// Must NOT be an ACCOMMODATION-category place (that belongs in accommodation).
    /// </summary>
    public string PlaceName { get; set; } = default!;

    /// <summary>
    /// Optional free-text user-facing note. Never used for structural purposes.
    /// </summary>
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
