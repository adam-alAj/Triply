namespace Triply.Api.Modules.Destination.Dtos;

public class DestinationListResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public string CountryName { get; set; } = default!;
    public string? Description { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
