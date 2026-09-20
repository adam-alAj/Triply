namespace Triply.Api.Modules.Currency;

/// <summary>
/// Single reusable currency-conversion boundary. Converts one already-computed
/// total (a destination's summed ReferencePrice, or a Trip's BudgetAmount) from
/// one Currency to another via ExchangeRate.RateToUsd.
///
/// Deliberately NOT used per-Place: Place.ReferencePrice / Place.CurrencyId are
/// never converted or duplicated — every place keeps exactly one price, in its
/// own destination's currency, same as today. This service only converts
/// destination-level or trip-level *sums*, at the point where two different
/// currencies actually need to be compared:
///   - DestinationSuggestionService: ranking destinations against a BUDGET_FIRST
///     budget when destinations don't all share the user's budget currency.
///   - DESTINATION_FIRST-with-budget: comparing Trip.BudgetAmount against the
///     generated itinerary's Trip.TotalEstimatedCost, if the user's budget
///     currency differs from the chosen destination's currency.
/// </summary>
public interface ICurrencyConversionService
{
    /// <summary>
    /// Converts <paramref name="amount"/> from <paramref name="fromCurrencyId"/>
    /// to <paramref name="toCurrencyId"/>. Returns <paramref name="amount"/>
    /// unchanged (no DB lookup) when the two currency ids are equal.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No ExchangeRate row exists for one of the given currency ids.
    /// </exception>
    Task<decimal> ConvertAsync(
        decimal amount,
        long fromCurrencyId,
        long toCurrencyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch form: converts every amount in <paramref name="amountsByCurrencyId"/>
    /// (keyed by source CurrencyId) into <paramref name="toCurrencyId"/> using a
    /// single ExchangeRates round-trip, instead of one query per amount. Use this
    /// from DestinationSuggestionService, which needs to convert one total per
    /// candidate destination.
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>> ConvertManyAsync(
        IReadOnlyDictionary<long, decimal> amountsByCurrencyId,
        long toCurrencyId,
        CancellationToken cancellationToken = default);
}
