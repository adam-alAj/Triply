namespace Triply.Api.Modules.Currency.Dtos;

public class CurrencyResponse
{
    public long Id { get; set; }
    public string IsoCode { get; set; } = default!;
    public string Symbol { get; set; } = default!;
}
