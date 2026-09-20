using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;

namespace Triply.Api.Modules.Currency;

public class CurrencyConversionService : ICurrencyConversionService
{
    private readonly ApplicationDbContext _db;

    public CurrencyConversionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> ConvertAsync(
        decimal amount,
        long fromCurrencyId,
        long toCurrencyId,
        CancellationToken cancellationToken = default)
    {
        if (fromCurrencyId == toCurrencyId)
            return amount;

        var rates = await LoadRatesAsync(new[] { fromCurrencyId, toCurrencyId }, cancellationToken);
        return Convert(amount, fromCurrencyId, toCurrencyId, rates);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> ConvertManyAsync(
        IReadOnlyDictionary<long, decimal> amountsByCurrencyId,
        long toCurrencyId,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<long, decimal>(amountsByCurrencyId.Count);

        // Same-currency amounts never need a rate (same short-circuit as ConvertAsync).
        var foreignIds = amountsByCurrencyId.Keys.Where(id => id != toCurrencyId).ToArray();
        if (foreignIds.Length == 0)
        {
            foreach (var (id, amount) in amountsByCurrencyId)
                result[id] = amount;
            return result;
        }

        var rates = await LoadRatesAsync(
            foreignIds.Append(toCurrencyId).Distinct().ToArray(), cancellationToken);

        foreach (var (fromCurrencyId, amount) in amountsByCurrencyId)
        {
            result[fromCurrencyId] = Convert(amount, fromCurrencyId, toCurrencyId, rates);
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadRatesAsync(
        IReadOnlyCollection<long> currencyIds,
        CancellationToken cancellationToken)
    {
        var rates = await _db.ExchangeRates
            .AsNoTracking()
            .Where(r => currencyIds.Contains(r.CurrencyId))
            .ToDictionaryAsync(r => r.CurrencyId, r => r.RateToUsd, cancellationToken);

        var missing = currencyIds.Where(id => !rates.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"No ExchangeRate configured for CurrencyId(s) {string.Join(", ", missing)}. " +
                "Seed ExchangeRates before comparing amounts across currencies.");
        }

        return rates;
    }

    private static decimal Convert(
        decimal amount,
        long fromCurrencyId,
        long toCurrencyId,
        IReadOnlyDictionary<long, decimal> rates)
    {
        if (fromCurrencyId == toCurrencyId)
            return amount;

        var amountInUsd = amount * rates[fromCurrencyId];
        return amountInUsd / rates[toCurrencyId];
    }
}