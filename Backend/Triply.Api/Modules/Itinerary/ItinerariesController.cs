using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Itinerary.Dtos;

namespace Triply.Api.Modules.Itinerary;


[ApiController]
[Route("api/trips/{tripId:guid}/itinerary")]
[Authorize]
[EnableRateLimiting("fixed")]
public sealed class ItinerariesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthorizationService _authorizationService;

    public ItinerariesController(
        ApplicationDbContext db,
        IAuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);

        if (trip is null)
            return NotFound();

        var authResult = await _authorizationService
            .AuthorizeAsync(User, trip, "TripOwner");

        if (!authResult.Succeeded)
            return NotFound();

        var itinerary = await _db.Itineraries
            .AsNoTracking()
            .Include(x => x.Days)
                .ThenInclude(x => x.Items)
                    .ThenInclude(x => x.Place)
            .FirstOrDefaultAsync(x => x.TripId == tripId, cancellationToken);

        if (itinerary is null)
            return NotFound();

        return Ok(ToResponse(itinerary));
    }

    [HttpPost]
    public async Task<IActionResult> Write(
        Guid tripId,
        [FromBody] WriteItineraryRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await _db.Trips
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);

        if (trip is null)
            return NotFound();

        var authResult = await _authorizationService
            .AuthorizeAsync(User, trip, "TripOwner");

        if (!authResult.Succeeded)
            return NotFound();

        var placeIds = request.Days
            .SelectMany(day => day.Items)
            .Select(item => item.PlaceId)
            .Distinct()
            .ToList();

        var places = await _db.Places
            .Where(x => placeIds.Contains(x.Id) && x.IsActive)
            .ToListAsync(cancellationToken);

        if (places.Count != placeIds.Count)
        {
           return BadRequest(new
{
    errors = new Dictionary<string, string[]>
    {
        ["placeId"] = ["One or more places do not exist or are inactive."]
    }
});
        }

        if (trip.DestinationId.HasValue &&
            places.Any(place => place.DestinationId != trip.DestinationId.Value))
        {
           return BadRequest(new
{
    errors = new Dictionary<string, string[]>
    {
        ["placeId"] = ["All itinerary places must belong to the trip destination."]
    }
});
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await _db.Itineraries
            .Include(x => x.Days)
                .ThenInclude(x => x.Items)
            .FirstOrDefaultAsync(x => x.TripId == tripId, cancellationToken);

        if (existing is not null)
        {
            _db.ItineraryItems.RemoveRange(existing.Days.SelectMany(x => x.Items));
            _db.ItineraryDays.RemoveRange(existing.Days);
            await _db.SaveChangesAsync(cancellationToken);
            _db.Itineraries.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

var itinerary = new Triply.Api.Entities.Itinerary
        {
            TripId = tripId,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var dayRequest in request.Days.OrderBy(x => x.DayNumber))
        {
            var day = new ItineraryDay
            {
                DayNumber = dayRequest.DayNumber,
                Date = dayRequest.Date
            };

            foreach (var itemRequest in dayRequest.Items
                         .OrderBy(x => TimeSlotOrder(x.TimeSlot))
                         .ThenBy(x => x.OrderIndex))
            {
                day.Items.Add(new ItineraryItem
                {
                    PlaceId = itemRequest.PlaceId,
                    TimeSlot = itemRequest.TimeSlot.ToUpperInvariant(),
                    OrderIndex = itemRequest.OrderIndex,
                    EstimatedCost = itemRequest.EstimatedCost,
                    Notes = itemRequest.Notes,
                    IsAiGenerated = itemRequest.IsAiGenerated,
                    ModifiedAt = itemRequest.IsAiGenerated ? null : DateTime.UtcNow
                });
            }

            itinerary.Days.Add(day);
        }

        _db.Itineraries.Add(itinerary);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ToResponse(itinerary));
    }

    private static int TimeSlotOrder(string timeSlot) => timeSlot.ToUpperInvariant() switch
    {
        "MORNING" => 1,
        "AFTERNOON" => 2,
        "EVENING" => 3,
        _ => 99
    };

    private static ItineraryResponse ToResponse(Triply.Api.Entities.Itinerary itinerary)
    {
        return new ItineraryResponse
        {
            Id = itinerary.Id,
            TripId = itinerary.TripId,
            GeneratedAt = itinerary.GeneratedAt,
            Days = itinerary.Days
                .OrderBy(day => day.DayNumber)
                .Select(day => new ItineraryDayResponse
                {
                    Id = day.Id,
                    DayNumber = day.DayNumber,
                    Date = day.Date,
                    Items = day.Items
                        .OrderBy(item => TimeSlotOrder(item.TimeSlot))
                        .ThenBy(item => item.OrderIndex)
                        .Select(item => new ItineraryItemResponse
                        {
                            Id = item.Id,
                            PlaceId = item.PlaceId,
                            PlaceName = item.Place.Name,
                            TimeSlot = item.TimeSlot,
                            OrderIndex = item.OrderIndex,
                            EstimatedCost = item.EstimatedCost,
                            Notes = item.Notes,
                            IsAiGenerated = item.IsAiGenerated,
                            ModifiedAt = item.ModifiedAt
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
