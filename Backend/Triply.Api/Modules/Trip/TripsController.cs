// Backend/Triply.Api/Modules/Trip/TripsController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;

using Triply.Api.Entities;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Modules.Trip;
[ApiController]
[Route("api/trips")]
[Authorize]
[EnableRateLimiting("fixed")]  
public class TripsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthorizationService _authorizationService;

    public TripsController(ApplicationDbContext db, IAuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
    }
[HttpGet]
public async Task<IActionResult> GetMyTrips()
{
    var userIdClaim =
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    if (!Guid.TryParse(userIdClaim, out var userId))
        return Unauthorized();

    var trips = await _db.Trips
        .Where(t => t.UserId == userId)
        .OrderByDescending(t => t.CreatedAt)
        .Select(t => new TripResponse
        {
            Id = t.Id,
            PlanningMode = t.PlanningMode,
            Status = t.Status,
            DestinationId = t.DestinationId,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            TravelerCount = t.TravelerCount,
            BudgetAmount = t.BudgetAmount,
            BudgetCurrencyId = t.BudgetCurrencyId,
            InterestCategoryIds = t.TripInterests
                .Select(ti => ti.InterestCategoryId)
                .ToList()
        })
        .ToListAsync();

    return Ok(trips);
}


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id);
        if (trip is null)
            return NotFound(); // never reveals whether it exists but belongs to someone else

        var authResult = await _authorizationService.AuthorizeAsync(User, trip, "TripOwner");
        if (!authResult.Succeeded)
            return NotFound(); // 404, not 403 — avoids confirming the resource exists at all

        return Ok(new { trip.Id, trip.Status, trip.PlanningMode });
    }

    // Helper endpoint used only so tests can create a Trip owned by the
    // authenticated caller, without needing the full Trip-creation feature yet.
    [HttpPost("test-create")]
    public async Task<IActionResult> TestCreate()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var trip = new Entities.Trip
        {
            UserId = userId,
            PlanningMode = "DESTINATION_FIRST",
            Status = "DRAFT",
            TravelerCount = 1
        };
        _db.Trips.Add(trip);
        await _db.SaveChangesAsync();

        return Ok(new { trip.Id });
    }

    [HttpPost]
public async Task<IActionResult> Create(
    [FromBody] CreateTripRequest request)
{
    var userIdClaim =
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    if (!Guid.TryParse(userIdClaim, out var userId))
        return Unauthorized();

    if (request.DestinationId.HasValue)
    {
        var destinationExists = await _db.Destinations
            .AnyAsync(d => d.Id == request.DestinationId.Value);

        if (!destinationExists)
            return BadRequest(new
            {
                message = "Destination does not exist."
            });
    }

    if (request.BudgetCurrencyId.HasValue)
    {
        var currencyExists = await _db.Currencies
            .AnyAsync(c => c.Id == request.BudgetCurrencyId.Value);

        if (!currencyExists)
            return BadRequest(new
            {
                message = "Budget currency does not exist."
            });
    }

    var interestIds = request.InterestCategoryIds
        .Distinct()
        .ToList();

    if (interestIds.Count > 0)
    {
        var existingInterestIds = await _db.InterestCategories
            .Where(x => interestIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        if (existingInterestIds.Count != interestIds.Count)
        {
            return BadRequest(new
            {
                message = "One or more interest categories do not exist."
            });
        }
    }

    await using var transaction =
        await _db.Database.BeginTransactionAsync();

var trip = new Triply.Api.Entities.Trip   {
        UserId = userId,
        PlanningMode = request.PlanningMode,
        Status = "DRAFT",
        DestinationId = request.DestinationId,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        TravelerCount = request.TravelerCount,
        BudgetAmount = request.BudgetAmount,
        BudgetCurrencyId = request.BudgetCurrencyId,
        Version = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    _db.Trips.Add(trip);

    foreach (var interestId in interestIds)
    {
        trip.TripInterests.Add(new Triply.Api.Entities.TripInterest
        {
            TripId = trip.Id,
            InterestCategoryId = interestId
        });
    }

    await _db.SaveChangesAsync();
    await transaction.CommitAsync();

    var response = new TripResponse
    {
        Id = trip.Id,
        PlanningMode = trip.PlanningMode,
        Status = trip.Status,
        DestinationId = trip.DestinationId,
        StartDate = trip.StartDate,
        EndDate = trip.EndDate,
        TravelerCount = trip.TravelerCount,
        BudgetAmount = trip.BudgetAmount,
        BudgetCurrencyId = trip.BudgetCurrencyId,
        InterestCategoryIds = interestIds
    };

    return CreatedAtAction(
        nameof(GetById),
        new { id = trip.Id },
        response);
}
[HttpPut("{id:guid}")]
public async Task<IActionResult> Update(
    Guid id,
    [FromBody] UpdateTripRequest request)
{
    var userIdClaim =
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    if (!Guid.TryParse(userIdClaim, out var userId))
        return Unauthorized();

    var trip = await _db.Trips
        .Include(t => t.TripInterests)
        .FirstOrDefaultAsync(t => t.Id == id);

    if (trip is null)
        return NotFound();

    // Ownership check
    if (trip.UserId != userId)
        return NotFound();

    // Destination validation
    if (request.DestinationId.HasValue)
    {
        var destinationExists = await _db.Destinations
            .AnyAsync(d => d.Id == request.DestinationId.Value);

        if (!destinationExists)
        {
            return BadRequest(new
            {
                errors = new
                {
                    DestinationId = new[]
                    {
                        "Destination does not exist."
                    }
                }
            });
        }
    }

    // Currency validation
    if (request.BudgetCurrencyId.HasValue)
    {
        var currencyExists = await _db.Currencies
            .AnyAsync(c => c.Id == request.BudgetCurrencyId.Value);

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
    }

    // Interest validation
    var interestIds = request.InterestCategoryIds
        .Distinct()
        .ToList();

    if (interestIds.Count > 0)
    {
        var existingInterestIds = await _db.InterestCategories
            .Where(x => interestIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        if (existingInterestIds.Count != interestIds.Count)
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
    }

    await using var transaction =
        await _db.Database.BeginTransactionAsync();

    trip.DestinationId = request.DestinationId;
    trip.StartDate = request.StartDate;
    trip.EndDate = request.EndDate;
    trip.TravelerCount = request.TravelerCount;
    trip.BudgetAmount = request.BudgetAmount;
    trip.BudgetCurrencyId = request.BudgetCurrencyId;
    trip.UpdatedAt = DateTime.UtcNow;
    trip.Version++;

    // Replace interests
    _db.TripInterests.RemoveRange(trip.TripInterests);

    foreach (var interestId in interestIds)
    {
        trip.TripInterests.Add(
            new Triply.Api.Entities.TripInterest
            {
                TripId = trip.Id,
                InterestCategoryId = interestId
            });
    }

    await _db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Ok(new TripResponse
    {
        Id = trip.Id,
        PlanningMode = trip.PlanningMode,
        Status = trip.Status,
        DestinationId = trip.DestinationId,
        StartDate = trip.StartDate,
        EndDate = trip.EndDate,
        TravelerCount = trip.TravelerCount,
        BudgetAmount = trip.BudgetAmount,
        BudgetCurrencyId = trip.BudgetCurrencyId,
        InterestCategoryIds = interestIds
    });
}

}