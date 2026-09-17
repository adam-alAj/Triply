using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Modules.Destination.Dtos;

namespace Triply.Api.Modules.Destination;

public class DestinationSuggestionService : IDestinationSuggestionService
{
    private readonly ApplicationDbContext _db;

    public DestinationSuggestionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DestinationSuggestionResponse>> SuggestAsync(
        DestinationSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Budget-first suggestions are interest-aware. The destination cost
        // aggregation remains unchanged; PlaceInterest adds an interest
        // overlap score used for filtering and ranking.
        var requestedInterestIds = request.InterestCategoryIds
            .Distinct()
            .ToList();

        var candidates = await _db.Places
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.Destination.IsSupported &&
                p.CurrencyId == request.BudgetCurrencyId)
            .GroupBy(p => new
            {
                p.DestinationId,
                DestinationName = p.Destination.Name,
                CountryName = p.Destination.Country.Name,
                Currency = p.Currency.IsoCode
            })
            .Select(g => new
            {
                DestinationId = g.Key.DestinationId,
                DestinationName = g.Key.DestinationName,
                CountryName = g.Key.CountryName,
                EstimatedCost = g.Sum(p => p.ReferencePrice),
                Currency = g.Key.Currency,
                MatchCount = _db.PlaceInterests
                    .Where(pi =>
                        pi.Place.DestinationId == g.Key.DestinationId &&
                        requestedInterestIds.Contains(pi.InterestCategoryId))
                    .Select(pi => pi.InterestCategoryId)
                    .Distinct()
                    .Count()
            })
            .Where(x =>
                x.EstimatedCost <= request.BudgetAmount &&
                x.MatchCount > 0)
            .OrderByDescending(x => x.MatchCount)
            .ThenBy(x => x.EstimatedCost)
            .Select(x => new DestinationSuggestionResponse
            {
                DestinationId = x.DestinationId,
                DestinationName = x.DestinationName,
                CountryName = x.CountryName,
                EstimatedCost = x.EstimatedCost,
                Currency = x.Currency,
                IsEstimated = true
            })
            .ToListAsync(cancellationToken);

        return candidates;
    }
}
