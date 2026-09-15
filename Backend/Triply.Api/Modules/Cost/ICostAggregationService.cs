using Triply.Api.Modules.Cost.Dtos;

namespace Triply.Api.Modules.Cost;

public interface ICostAggregationService
{
    Task<CostEstimateResponse> CalculateAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}
