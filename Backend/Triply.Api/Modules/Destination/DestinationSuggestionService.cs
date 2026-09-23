using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Modules.Currency;
using Triply.Api.Modules.Destination.Dtos;

namespace Triply.Api.Modules.Destination;

public class DestinationSuggestionService : IDestinationSuggestionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrencyConversionService _currencyConversion;

    public DestinationSuggestionService(
        ApplicationDbContext db,
        ICurrencyConversionService currencyConversion)
    {
        _db = db;
        _currencyConversion = currencyConversion;
    }

    public async Task<IReadOnlyList<DestinationSuggestionResponse>> SuggestAsync(
        DestinationSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Budget-first suggestions are interest-aware. Only active places that
        // match at least one requested interest contribute to the destination
        // estimate; this keeps the budget comparison aligned with the user's
        // selected interests instead of charging unrelated places.
        var requestedInterestIds = request.InterestCategoryIds
            .Distinct()
            .ToList();

        // CHANGED: no longer filters candidates by p.CurrencyId == request.BudgetCurrencyId.
        // That filter silently dropped every destination whose currency didn't
        // exactly match the user's budget currency (e.g. a EUR budget would
        // never even see Amman/JOD or New York/USD as candidates). Every
        // active/supported destination is now a candidate; the totals are
        // converted into the user's budget currency below before comparing.
        var candidates = await _db.Places
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.Destination.IsSupported &&
                p.PlaceInterests.Any(pi => requestedInterestIds.Contains(pi.InterestCategoryId)))
            .GroupBy(p => new
            {
                p.DestinationId,
                DestinationName = p.Destination.Name,
                CountryName = p.Destination.Country.Name,
                CurrencyId = p.CurrencyId,
                Currency = p.Currency.IsoCode
            })
            .Select(g => new
            {
                DestinationId = g.Key.DestinationId,
                DestinationName = g.Key.DestinationName,
                CountryName = g.Key.CountryName,
                EstimatedCost = g.Sum(p => p.ReferencePrice),
                CurrencyId = g.Key.CurrencyId,
                Currency = g.Key.Currency,
                MatchCount = _db.PlaceInterests
                    .Where(pi =>
                        pi.Place.DestinationId == g.Key.DestinationId &&
                        requestedInterestIds.Contains(pi.InterestCategoryId))
                    .Select(pi => pi.InterestCategoryId)
                    .Distinct()
                    .Count()
            })
            .Where(x => x.MatchCount > 0)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return Array.Empty<DestinationSuggestionResponse>();

        // One conversion round-trip total, for however many distinct currencies
        // appear among the candidates (3 today) — not one round-trip per
        // destination and never one per place. We resolve "1 unit of each
        // currency, expressed in the budget currency" once, then scale each
        // destination's own native EstimatedCost by that factor below — this
        // avoids ConvertManyAsync's per-currency-sum shape, which would
        // incorrectly collapse two destinations that happen to share a currency.
        var distinctCurrencyIds = candidates.Select(x => x.CurrencyId).Distinct().ToArray();
        var oneUnitByCurrencyId = distinctCurrencyIds.ToDictionary(id => id, _ => 1m);
        var rateFactorInBudgetCurrency = await _currencyConversion.ConvertManyAsync(
            oneUnitByCurrencyId, request.BudgetCurrencyId, cancellationToken);

        var results = candidates
            .Select(x => new
            {
                x.DestinationId,
                x.DestinationName,
                x.CountryName,
                x.EstimatedCost,
                x.Currency,
                x.MatchCount,
                // EstimatedCost (native currency) * (1 native unit expressed in the
                // user's budget currency) = EstimatedCost expressed in budget currency.
                EstimatedCostInBudgetCurrency = x.EstimatedCost * rateFactorInBudgetCurrency[x.CurrencyId]
            })
            // Return up to three destinations that actually fit the requested
            // budget. Over-budget destinations are not suggestions because the
            // user must choose from budget-suitable options.
            .Where(x => x.EstimatedCostInBudgetCurrency <= request.BudgetAmount)
            .OrderByDescending(x => x.MatchCount)
            .ThenBy(x => x.EstimatedCostInBudgetCurrency)
            .Take(3)
            .Select(x => new DestinationSuggestionResponse
            {
                DestinationId = x.DestinationId,
                DestinationName = x.DestinationName,
                CountryName = x.CountryName,
                EstimatedCost = x.EstimatedCost,
                Currency = x.Currency,
                // ADDED: lets the frontend show "38,000 THB (~950 EUR of your budget)"
                // without doing its own conversion. Native EstimatedCost/Currency
                // above are unchanged — this is purely additive.
                EstimatedCostInBudgetCurrency = Math.Round(x.EstimatedCostInBudgetCurrency, 2),
                BudgetCurrencyId = request.BudgetCurrencyId,
                IsEstimated = true,
                IsWithinBudget = x.EstimatedCostInBudgetCurrency <= request.BudgetAmount
            })
            .ToList();

        return results;
    }
}
