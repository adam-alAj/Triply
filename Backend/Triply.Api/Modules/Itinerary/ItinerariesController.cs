using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Itinerary.Dtos;
using Triply.Api.Modules.Trip;

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
                    EstimatedCost = places.Single(place => place.Id == itemRequest.PlaceId).ReferencePrice,
                    Notes = itemRequest.Notes,
                    IsAiGenerated = itemRequest.IsAiGenerated,
                    ModifiedAt = itemRequest.IsAiGenerated ? null : DateTime.UtcNow
                });
            }

            itinerary.Days.Add(day);
        }

        _db.Itineraries.Add(itinerary);

        if (trip.Status == TripLifecycle.Generating)
            TripLifecycle.Transition(trip, TripLifecycle.Generated);
        else if (trip.Status is TripLifecycle.Generated or TripLifecycle.Saved)
            TripLifecycle.Transition(trip, TripLifecycle.Modified);
        else if (trip.Status == TripLifecycle.Modified)
            TripLifecycle.Transition(trip, TripLifecycle.Modified);
        else if (trip.Status == TripLifecycle.Archived)
            return Conflict(new { message = "Archived trips cannot be modified." });

        trip.UpdatedAt = DateTime.UtcNow;
        trip.Version++;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ToResponse(itinerary));
    }

    [HttpPatch("items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid tripId,
        Guid itemId,
        [FromBody] UpdateItineraryItemRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await _db.Trips
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);

        if (trip is null) return NotFound();

        var authResult = await _authorizationService
            .AuthorizeAsync(User, trip, "TripOwner");
        if (!authResult.Succeeded) return NotFound();

        if (trip.Status == TripLifecycle.Archived || trip.Status == TripLifecycle.Generating)
            return Conflict(new { message = $"Trip cannot be modified while status is {trip.Status}." });

        var timeSlot = request.TimeSlot?.Trim().ToUpperInvariant();
        if (request.PlaceId <= 0 || !new[] { "MORNING", "AFTERNOON", "EVENING" }.Contains(timeSlot))
        {
            return BadRequest(new ValidationProblemDetails(
    new Dictionary<string, string[]>            {
                ["request"] = ["PlaceId must be greater than 0 and TimeSlot must be MORNING, AFTERNOON, or EVENING."]
            }));
        }

        if (request.OrderIndex < 0)
        {
            return BadRequest(new ValidationProblemDetails(
    new Dictionary<string, string[]>            {
                [nameof(request.OrderIndex)] = ["OrderIndex cannot be negative."]
            }));
        }

        if (request.Notes?.Length > 1000)
        {
            return BadRequest(new ValidationProblemDetails(
    new Dictionary<string, string[]>            {
                [nameof(request.Notes)] = ["Notes must be at most 1000 characters."]
            }));
        }

        var item = await _db.ItineraryItems
            .Include(x => x.ItineraryDay)
                .ThenInclude(x => x.Itinerary)
            .Include(x => x.Place)
                .ThenInclude(x => x.PlaceCategory)
            .FirstOrDefaultAsync(x => x.Id == itemId &&
                                      x.ItineraryDay.Itinerary.TripId == tripId,
                cancellationToken);

        if (item is null) return NotFound();

        // Accommodation is a trip-scoped special item and is not an editable activity.
        if (string.Equals(item.Place.PlaceCategory.Code, "ACCOMMODATION", StringComparison.OrdinalIgnoreCase) ||
            (item.Notes?.StartsWith("Accommodation:", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return Conflict(new { message = "Accommodation cannot be edited as a regular itinerary activity." });
        }

        var place = await _db.Places
            .FirstOrDefaultAsync(x => x.Id == request.PlaceId && x.IsActive, cancellationToken);

        if (place is null)
        {
            return BadRequest(new
            {
                errors = new Dictionary<string, string[]>
                {
                    [nameof(request.PlaceId)] = ["Place does not exist or is inactive."]
                }
            });
        }

        if (trip.DestinationId.HasValue && place.DestinationId != trip.DestinationId.Value)
        {
            return BadRequest(new
            {
                errors = new Dictionary<string, string[]>
                {
                    [nameof(request.PlaceId)] = ["Place must belong to the trip destination."]
                }
            });
        }

        var duplicate = await _db.ItineraryItems.AnyAsync(x =>
            x.Id != itemId &&
            x.ItineraryDayId == item.ItineraryDayId &&
            x.TimeSlot == timeSlot &&
            x.OrderIndex == request.OrderIndex, cancellationToken);

        if (duplicate)
        {
            return Conflict(new { message = "Another itinerary item already uses the requested TimeSlot and OrderIndex on this day." });
        }

        item.PlaceId = place.Id;
        item.TimeSlot = timeSlot!;
        item.OrderIndex = request.OrderIndex;
        item.EstimatedCost = place.ReferencePrice;
        item.Notes = request.Notes;
        item.IsAiGenerated = false;
        item.ModifiedAt = DateTime.UtcNow;

        if (trip.Status is TripLifecycle.Generated or TripLifecycle.Saved)
            TripLifecycle.Transition(trip, TripLifecycle.Modified);

        trip.UpdatedAt = DateTime.UtcNow;
        trip.Version++;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _db.Entry(item).Reference(x => x.Place).LoadAsync(cancellationToken);

        return Ok(new ItineraryItemResponse
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
        });
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
