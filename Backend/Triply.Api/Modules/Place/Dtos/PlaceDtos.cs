namespace Triply.Api.Modules.Place.Dtos;

public sealed class PlaceDetailsResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Category { get; set; } = default!;
    public long DestinationId { get; set; }
    public string DestinationName { get; set; } = default!;
    public string CountryName { get; set; } = default!;
    public decimal ReferencePrice { get; set; }
    public string Currency { get; set; } = default!;
    public List<string> Images { get; set; } = new();
    public List<PlaceOpeningHourResponse> OpeningHours { get; set; } = new();
}

public sealed class PlaceOpeningHourResponse
{
    public string Day { get; set; } = default!;
    public string? OpensAt { get; set; }
    public string? ClosesAt { get; set; }
    public bool IsClosed { get; set; }
}
