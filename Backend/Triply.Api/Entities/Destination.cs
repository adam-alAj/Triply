namespace Triply.Api.Entities;

// Database Design §6.3
public class Destination
{
    public long Id { get; set; }
    public long CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsSupported { get; set; } = true;

    public ICollection<Place> Places { get; set; } = new List<Place>();
}

// §6.5 — the internal ground-truth for FR-AI-002
public class Place
{
    public long Id { get; set; }
    public long DestinationId { get; set; }
    public Destination Destination { get; set; } = default!;
    public long PlaceCategoryId { get; set; }
    public PlaceCategory PlaceCategory { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal ReferencePrice { get; set; }
    public long CurrencyId { get; set; }
    public Currency Currency { get; set; } = default!;
    public long CostCategoryId { get; set; }
    public CostCategory CostCategory { get; set; } = default!;
    public DateTime PriceUpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
   public ICollection<PlaceInterest> PlaceInterests { get; set; } = new List<PlaceInterest>();
}
