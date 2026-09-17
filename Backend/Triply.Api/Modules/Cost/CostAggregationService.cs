using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Cost.Dtos;

namespace Triply.Api.Modules.Cost;

public class CostAggregationService : ICostAggregationService
{
    private readonly ApplicationDbContext _db;

    public CostAggregationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CostEstimateResponse> GenerateFromItineraryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await _db.Trips
            .Include(t => t.BudgetCurrency)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
            throw new KeyNotFoundException("Trip does not exist.");

        // Pull every itinerary item's snapshot cost together with the Place's
        // cost bucket + currency — this is deterministic backend logic (Database
        // Design §12: CostEstimate is system-computed, never LLM-generated).
        var itemRows = await _db.ItineraryItems
            .AsNoTracking()
            .Where(item => item.ItineraryDay.Itinerary.TripId == tripId)
            .Select(item => new
            {
                item.EstimatedCost,
                item.Place.CostCategoryId,
                CurrencyId = item.Place.CurrencyId
            })
            .ToListAsync(cancellationToken);

        var currencyIds = itemRows.Select(x => x.CurrencyId).Distinct().ToList();

        if (currencyIds.Count > 1)
        {
            throw new InvalidOperationException(
                "Itinerary items reference places in more than one currency; cannot aggregate costs.");
        }

        var currencyId = currencyIds.FirstOrDefault(trip.BudgetCurrencyId ?? 0);

        var grouped = itemRows
            .GroupBy(x => x.CostCategoryId)
            .Select(g => new { CostCategoryId = g.Key, Amount = g.Sum(x => x.EstimatedCost) })
            .ToList();

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Replace any previous estimates for this trip (regeneration / partial-edit
        // recompute both land here) rather than trying to diff row by row.
        var existing = await _db.CostEstimates
            .Where(c => c.TripId == tripId)
            .ToListAsync(cancellationToken);

        _db.CostEstimates.RemoveRange(existing);

        if (currencyId != 0)
        {
            foreach (var group in grouped)
            {
                _db.CostEstimates.Add(new CostEstimate
                {
                    TripId = tripId,
                    CostCategoryId = group.CostCategoryId,
                    Amount = group.Amount,
                    CurrencyId = currencyId,
                    ComputedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Refresh Trip.TotalEstimatedCost and build the response from what was just written.
        return await CalculateAsync(tripId, cancellationToken);
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
