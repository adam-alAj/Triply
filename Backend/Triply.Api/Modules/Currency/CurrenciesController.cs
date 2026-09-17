using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Modules.Currency.Dtos;

namespace Triply.Api.Modules.Currency;

[ApiController]
[Route("api/currencies")]
[Authorize]
[EnableRateLimiting("fixed")]
public class CurrenciesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CurrenciesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var currencies = await _db.Currencies
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new CurrencyResponse
            {
                Id = x.Id,
                IsoCode = x.IsoCode,
                Symbol = x.Symbol
            })
            .ToListAsync(cancellationToken);

        return Ok(currencies);
    }
}
