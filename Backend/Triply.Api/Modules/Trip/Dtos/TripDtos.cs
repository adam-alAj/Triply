using Triply.Api.Modules.Cost.Dtos;
using Triply.Api.Modules.Itinerary.Dtos;

namespace Triply.Api.Modules.Trip.Dtos;

public class CreateTripRequest
{
    public string PlanningMode { get; set; } = default!;
    public long? DestinationId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int TravelerCount { get; set; } = 1;
    public decimal? BudgetAmount { get; set; }
    public long? BudgetCurrencyId { get; set; }
    public List<long> InterestCategoryIds { get; set; } = new();
}

public class TripResponse
{
    public Guid Id { get; set; }
    public string PlanningMode { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Title { get; set; }
    public string? CoverImageUrl { get; set; }
    public long? DestinationId { get; set; }
    public string? DestinationName { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int TravelerCount { get; set; }
    public decimal? BudgetAmount { get; set; }
    public long? BudgetCurrencyId { get; set; }
    public List<long> InterestCategoryIds { get; set; } = new();
    public ItineraryResponse? Itinerary { get; set; }
    public CostEstimateResponse? CostEstimate { get; set; }
    public int Version { get; set; }
}

public class UpdateTripRequest
{
    public long? DestinationId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int TravelerCount { get; set; }
    public decimal? BudgetAmount { get; set; }
    public long? BudgetCurrencyId { get; set; }
    public List<long> InterestCategoryIds { get; set; } = new();
    public int ExpectedVersion { get; set; }
}

public class UpdateTripMetadataRequest
{
    public string? Title { get; set; }
    public string? CoverImageUrl { get; set; }
    public int? ExpectedVersion { get; set; }
}


/// <summary>
/// Selects one destination from the budget-first suggestions before AI generation.
/// </summary>
public class SelectDestinationRequest
{
    public long DestinationId { get; set; }
    public int ExpectedVersion { get; set; }
}
