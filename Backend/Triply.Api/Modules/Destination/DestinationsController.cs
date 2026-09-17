using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Triply.Api.Data;
using Triply.Api.Modules.Destination.Dtos;

namespace Triply.Api.Modules.Destination;

[ApiController]
[Route("api/destinations")]
[Authorize]
[EnableRateLimiting("fixed")]
public class DestinationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IDestinationSuggestionService _suggestionService;

    public DestinationsController(
        ApplicationDbContext db,
        IDestinationSuggestionService suggestionService)
    {
        _db = db;
        _suggestionService = suggestionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSupportedDestinations(
        CancellationToken cancellationToken)
    {
        var destinations = await _db.Destinations
            .AsNoTracking()
            .Where(d => d.IsSupported)
            .OrderBy(d => d.Name)
            .Select(d => new DestinationListResponse
            {
                Id = d.Id,
                Name = d.Name,
                CountryName = d.Country.Name,
                Description = d.Description,
                Latitude = d.Latitude,
                Longitude = d.Longitude
            })
            .ToListAsync(cancellationToken);

        return Ok(destinations);
    }

    [HttpPost("suggestions")]
    public async Task<IActionResult> Suggest(
        [FromBody] DestinationSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var currencyExists = await _db.Currencies
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.BudgetCurrencyId, cancellationToken);

        if (!currencyExists)
        {
            return BadRequest(new
            {
                errors = new
                {
                    BudgetCurrencyId = new[]
                    {
                        "Budget currency does not exist."
                    }
                }
            });
        }

        var interestIds = request.InterestCategoryIds
            .Distinct()
            .ToList();

        var existingInterestCount = await _db.InterestCategories
            .AsNoTracking()
            .CountAsync(
                x => interestIds.Contains(x.Id),
                cancellationToken);

        if (existingInterestCount != interestIds.Count)
        {
            return BadRequest(new
            {
                errors = new
                {
                    InterestCategoryIds = new[]
                    {
                        "One or more interest categories do not exist."
                    }
                }
            });
        }

        var suggestions = await _suggestionService.SuggestAsync(
            request,
            cancellationToken);

        return Ok(new
        {
            suggestions,
            count = suggestions.Count,
            message = suggestions.Count == 0
                ? "No supported destinations match the requested budget and currency."
                : null
        });
    }
}
