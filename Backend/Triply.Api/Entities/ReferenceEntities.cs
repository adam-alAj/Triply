namespace Triply.Api.Entities;

// Database Design §6.2 — Country
public class Country
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;      // UNIQUE
    public string IsoCode { get; set; } = default!;    // UNIQUE, CHAR(2)
}

// §6.7 — Currency
public class Currency
{
    public long Id { get; set; }
    public string IsoCode { get; set; } = default!;    // UNIQUE, CHAR(3)
    public string Symbol { get; set; } = default!;
}

// §6.8 — InterestCategory
public class InterestCategory
{
    public long Id { get; set; }
    public string Code { get; set; } = default!;       // UNIQUE
    public string Label { get; set; } = default!;
}

// §6.6 — CostCategory
public class CostCategory
{
    public long Id { get; set; }
    public string Code { get; set; } = default!;       // UNIQUE
    public string Label { get; set; } = default!;
}

// §6.4 — PlaceCategory
public class PlaceCategory
{
    public long Id { get; set; }
    public string Code { get; set; } = default!;       // UNIQUE
    public string Label { get; set; } = default!;
}
