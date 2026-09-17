using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Modules.Cost.Dtos;

namespace Triply.Api.Modules.Cost;

public class CostAggregationService : ICostAggregationService
{
    private readonly ApplicationDbContext _db;

    public CostAggregationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CostEstimateResponse> CalculateAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .Include(t => t.BudgetCurrency)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
            throw new KeyNotFoundException("Trip does not exist.");

        var categories = await _db.CostCategories
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CostCategoryEstimateResponse
            {
                CostCategoryId = c.Id,
                CategoryCode = c.Code,
                CategoryName = c.Label,
                Amount = 0,
                Currency = string.Empty,
                IsEstimated = true
            })
            .ToListAsync(cancellationToken);

        var rows = await _db.CostEstimates
            .AsNoTracking()
            .Where(x => x.TripId == tripId)
            .Select(x => new
            {
                x.CostCategoryId,
                x.Amount,
                Currency = x.Currency.IsoCode
            })
            .ToListAsync(cancellationToken);

        var currencies = rows
            .Select(x => x.Currency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (currencies.Count > 1)
        {
            throw new InvalidOperationException(
                "Cost estimates for a trip must use the same currency before they can be aggregated.");
        }

        var currency = currencies.FirstOrDefault()
            ?? trip.BudgetCurrency?.IsoCode
            ?? string.Empty;
        var grouped = rows
            .GroupBy(x => x.CostCategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        foreach (var category in categories)
        {
            if (grouped.TryGetValue(category.CostCategoryId, out var amount))
                category.Amount = amount;

            category.Currency = currency;
            category.IsEstimated = true;
        }

        var total = categories.Sum(x => x.Amount);

        await _db.Trips
            .Where(t => t.Id == tripId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.TotalEstimatedCost, total)
                    .SetProperty(t => t.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        return new CostEstimateResponse
        {
            TripId = tripId,
            Categories = categories,
            TotalEstimatedCost = total,
            Currency = currency,
            IsEstimated = true
        };
    }
}
