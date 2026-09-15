namespace Triply.Api.Modules.Cost.Dtos;

public class CostCategoryEstimateResponse
{
    public long CostCategoryId { get; set; }
    public string CategoryCode { get; set; } = default!;
    public string CategoryName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public bool IsEstimated { get; set; } = true;
}

public class CostEstimateResponse
{
    public Guid TripId { get; set; }
    public List<CostCategoryEstimateResponse> Categories { get; set; } = new();
    public decimal TotalEstimatedCost { get; set; }
    public string Currency { get; set; } = default!;
    public bool IsEstimated { get; set; } = true;
}
