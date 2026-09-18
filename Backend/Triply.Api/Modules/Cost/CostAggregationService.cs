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
            .ToListAsync(cancellationToken);

        // Cost aggregation is deterministic Backend logic. Resolve the
        // itinerary places against the internal dataset and aggregate the
        // authoritative/snapshotted item costs by CostCategory.
        var items = await _db.ItineraryItems
            .AsNoTracking()
            .Where(item => item.ItineraryDay.Itinerary.TripId == tripId)
            .Include(item => item.Place)
                .ThenInclude(place => place.Currency)
            .ToListAsync(cancellationToken);

        var currencies = items
            .Select(item => item.Place.Currency.IsoCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (currencies.Count > 1)
        {
            throw new InvalidOperationException(
                "Itinerary places for a trip must use the same currency before costs can be aggregated.");
        }

        var currency = currencies.FirstOrDefault()
            ?? trip.BudgetCurrency?.IsoCode
            ?? string.Empty;

        var grouped = items
            .GroupBy(item => item.Place.CostCategoryId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Place.ReferencePrice));

        var categoryAmounts = categories
            .Select(category => new
            {
                Category = category,
                Amount = grouped.TryGetValue(category.Id, out var amount)
                    ? amount
                    : 0m
            })
            .ToList();

        var total = categoryAmounts.Sum(x => x.Amount);

        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        var existingEstimates = await _db.CostEstimates
            .Where(x => x.TripId == tripId)
            .ToListAsync(cancellationToken);

        _db.CostEstimates.RemoveRange(existingEstimates);

        foreach (var item in categoryAmounts.Where(x => x.Amount > 0))
        {
            var currencyId = items
                .Where(x => x.Place.CostCategoryId == item.Category.Id)
                .Select(x => x.Place.CurrencyId)
                .FirstOrDefault();

            if (currencyId == 0)
                currencyId = trip.BudgetCurrencyId
                    ?? throw new InvalidOperationException(
                        "A currency is required to persist a cost estimate.");

            _db.CostEstimates.Add(new CostEstimate
            {
                TripId = tripId,
                CostCategoryId = item.Category.Id,
                Amount = item.Amount,
                CurrencyId = currencyId,
                ComputedAt = DateTime.UtcNow
            });
        }

        await _db.Trips
            .Where(t => t.Id == tripId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.TotalEstimatedCost, total)
                    .SetProperty(t => t.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CostEstimateResponse
        {
            TripId = tripId,
            Categories = categoryAmounts
                .Select(x => new CostCategoryEstimateResponse
                {
                    CostCategoryId = x.Category.Id,
                    CategoryCode = x.Category.Code,
                    CategoryName = x.Category.Label,
                    Amount = x.Amount,
                    Currency = currency,
                    IsEstimated = true
                })
                .ToList(),
            TotalEstimatedCost = total,
            Currency = currency,
            IsEstimated = true
        };
    }
}
