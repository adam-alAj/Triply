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
    public decimal EstimatedCost { get; set; }
    public string Currency { get; set; } = default!;
    public bool IsEstimated { get; set; } = true;
}
