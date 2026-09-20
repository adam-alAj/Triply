namespace Triply.Api.Modules.Destination.Dtos;

public class DestinationSuggestionRequest
{
    public decimal BudgetAmount { get; set; }
    public long BudgetCurrencyId { get; set; }
    public List<long> InterestCategoryIds { get; set; } = new();
}

public class DestinationSuggestionResponse
{
    public long DestinationId { get; set; }
    public string DestinationName { get; set; } = default!;
    public string CountryName { get; set; } = default!;

    // Native totals — unchanged from before this change.
    public decimal EstimatedCost { get; set; }
    public string Currency { get; set; } = default!;

    // ADDED: same total, converted into the requester's BudgetCurrencyId, so the
    // frontend can show it against the budget the user actually typed in without
    // doing its own conversion (e.g. "38,000 THB — about 950 EUR of your budget").
    public decimal EstimatedCostInBudgetCurrency { get; set; }
    public long BudgetCurrencyId { get; set; }

    public bool IsEstimated { get; set; } = true;

    // True when the destination's estimated reference total is within the
    // requested budget. Up to three budget-suitable destinations are returned
    // so the user can choose one before BUDGET_FIRST generation.
    public bool IsWithinBudget { get; set; }
}
