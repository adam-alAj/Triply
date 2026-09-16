namespace Triply.Api.Modules.InterestCategory.Dtos;

public class InterestCategoryResponse
{
    public long Id { get; set; }
    public string Code { get; set; } = default!;
    public string Label { get; set; } = default!;
}
