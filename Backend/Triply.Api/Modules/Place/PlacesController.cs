using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Modules.Place.Dtos;

namespace Triply.Api.Modules.Place;

[ApiController]
[Route("api/places")]
[Authorize]
[EnableRateLimiting("fixed")]
public sealed class PlacesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public PlacesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var place = await _db.Places
            .AsNoTracking()
            .Include(x => x.PlaceCategory)
            .Include(x => x.Destination)
                .ThenInclude(x => x.Country)
            .Include(x => x.Currency)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);

        if (place is null)
            return NotFound();

        // The current curated Place model has no image/opening-hours source yet.
        // Keep the response shape ready for Flutter and return empty collections
        // until those dataset/storage fields exist.
        return Ok(new PlaceDetailsResponse
        {
            Id = place.Id,
            Name = place.Name,
            Description = place.Description,
            Category = place.PlaceCategory.Code,
            DestinationId = place.DestinationId,
            DestinationName = place.Destination.Name,
            CountryName = place.Destination.Country.Name,
            ReferencePrice = place.ReferencePrice,
            Currency = place.Currency.IsoCode,
            Images = [],
            OpeningHours = []
        });
    }
}
