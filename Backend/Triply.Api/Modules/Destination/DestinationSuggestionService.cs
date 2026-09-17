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
        // The current approved schema has no Destination/Place-to-Interest
        // relationship. Interests are therefore validated by the controller,
        // while the deterministic candidate query follows Database Design
        // §23 query #7: aggregate active Place.reference_price by destination
        // and keep destinations whose aggregate fits the requested budget.
        var candidates = await _db.Places
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.CurrencyId == request.BudgetCurrencyId)
            .GroupBy(p => new
            {
                p.DestinationId,
                DestinationName = p.Destination.Name,
                CountryName = p.Destination.Country.Name,
                Currency = p.Currency.IsoCode
            })
            .Select(g => new DestinationSuggestionResponse
            {
                DestinationId = g.Key.DestinationId,
                DestinationName = g.Key.DestinationName,
                CountryName = g.Key.CountryName,
                EstimatedCost = g.Sum(p => p.ReferencePrice),
                Currency = g.Key.Currency,
                IsEstimated = true
            })
            .Where(x => x.EstimatedCost <= request.BudgetAmount)
            .OrderBy(x => x.EstimatedCost)
            .ToListAsync(cancellationToken);

        return candidates;
    }
}
