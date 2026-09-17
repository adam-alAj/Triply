using Triply.Api.Modules.Cost.Dtos;

namespace Triply.Api.Modules.Cost;

public interface ICostAggregationService
{
    Task<CostEstimateResponse> CalculateAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// TASK48 — writes/refreshes the CostEstimate rows for a trip from its currently
    /// persisted Itinerary (ItineraryItem.EstimatedCost, which was itself copied from
    /// Place.ReferencePrice at generation time — Database Design §15), then calls
    /// CalculateAsync to refresh Trip.TotalEstimatedCost and return the response.
    /// Called right after a real Gemini-driven generation succeeds (TASK47).
    /// </summary>
    Task<CostEstimateResponse> GenerateFromItineraryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}
