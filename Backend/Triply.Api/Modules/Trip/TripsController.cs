using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Cost.Dtos;
using Triply.Api.Modules.Itinerary.Dtos;
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

    public TripsController(
        ApplicationDbContext db,
        IAuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyTrips(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var trips = await _db.Trips
            .AsNoTracking()
            .Where(t => t.UserId == userId.Value)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TripResponse
            {
                Id = t.Id,
                PlanningMode = t.PlanningMode,
                Status = t.Status,
                Title = t.Title,
                CoverImageUrl = t.CoverImageUrl,
                DestinationId = t.DestinationId,
                DestinationName = t.Destination == null ? null : t.Destination.Name,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                TravelerCount = t.TravelerCount,
                BudgetAmount = t.BudgetAmount,
                BudgetCurrencyId = t.BudgetCurrencyId,
                InterestCategoryIds = t.TripInterests
                    .Select(x => x.InterestCategoryId)
                    .ToList(),
                Version = t.Version
            })
            .ToListAsync(cancellationToken);

        return Ok(trips);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .Include(t => t.Destination)
            .Include(t => t.TripInterests)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

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
            .FirstOrDefaultAsync(
                x => x.TripId == id,
                cancellationToken);

        var costEstimate = await _db.CostEstimates
            .AsNoTracking()
            .Include(x => x.CostCategory)
            .Include(x => x.Currency)
            .Where(x => x.TripId == id)
            .OrderBy(x => x.CostCategoryId)
            .ToListAsync(cancellationToken);

        return Ok(ToResponse(trip, itinerary, costEstimate));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTripRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (request.DestinationId.HasValue &&
            !await _db.Destinations.AnyAsync(
                d => d.Id == request.DestinationId.Value && d.IsSupported,
                cancellationToken))
        {
            return BadRequest(new
            {
                message = "Destination does not exist or is not supported."
            });
        }

        if (request.BudgetCurrencyId.HasValue &&
            !await _db.Currencies.AnyAsync(
                c => c.Id == request.BudgetCurrencyId.Value,
                cancellationToken))
        {
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
                .ToListAsync(cancellationToken);

            if (existingInterestIds.Count != interestIds.Count)
            {
                return BadRequest(new
                {
                    message = "One or more interest categories do not exist."
                });
            }
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        var trip = new Triply.Api.Entities.Trip
        {
            UserId = userId.Value,
            PlanningMode = request.PlanningMode,
            Status = TripLifecycle.Draft,
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

        foreach (var interestId in interestIds)
        {
            trip.TripInterests.Add(new TripInterest
            {
                TripId = trip.Id,
                InterestCategoryId = interestId
            });
        }

        _db.Trips.Add(trip);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = trip.Id },
            ToResponse(trip, null, []));
    }


    [HttpPatch("{id:guid}/destination")]
    public async Task<IActionResult> SelectDestination(
        Guid id,
        [FromBody] SelectDestinationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var trip = await _db.Trips
            .Include(t => t.Destination)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId.Value, cancellationToken);

        if (trip is null) return NotFound();

        if (!string.Equals(trip.PlanningMode, "BUDGET_FIRST", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Destination selection is only used for BUDGET_FIRST trips."
            });
        }

        if (trip.Status is TripLifecycle.Generating or TripLifecycle.Archived or TripLifecycle.Generated or TripLifecycle.Saved)
        {
            return Conflict(new
            {
                message = $"Destination cannot be selected while trip status is {trip.Status}."
            });
        }

        if (request.ExpectedVersion != trip.Version)
        {
            return Conflict(new
            {
                message = "Trip has been modified by another request.",
                currentVersion = trip.Version
            });
        }

        var destination = await _db.Destinations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.Id == request.DestinationId && d.IsSupported,
                cancellationToken);

        if (destination is null)
        {
            return BadRequest(new
            {
                message = "Destination does not exist or is not supported."
            });
        }

        trip.DestinationId = destination.Id;
        trip.UpdatedAt = DateTime.UtcNow;
        trip.Version++;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            tripId = trip.Id,
            destinationId = destination.Id,
            destinationName = destination.Name,
            version = trip.Version,
            message = "Destination selected. The client can now generate the trip itinerary."
        });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateMetadata(
        Guid id,
        [FromBody] UpdateTripMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (request.Title is null && request.CoverImageUrl is null)
        {
            return BadRequest(new ValidationProblemDetails(
    new Dictionary<string, string[]>            {
                ["request"] = ["At least one metadata field must be provided."]
            }));
        }

        if (request.Title is not null && request.Title.Trim().Length > 200)
        {
            return BadRequest(new ValidationProblemDetails(
    new Dictionary<string, string[]>            {
                [nameof(request.Title)] = ["Title must be at most 200 characters."]
            }));
        }

        if (request.CoverImageUrl is not null)
        {
            var cover = request.CoverImageUrl.Trim();
            if (cover.Length > 1000)
            {
                return BadRequest(new ValidationProblemDetails(
    new Dictionary<string, string[]>                {
                    [nameof(request.CoverImageUrl)] = ["CoverImageUrl must be at most 1000 characters."]
                }));
            }

            if (cover.Length > 0 &&
                (!Uri.TryCreate(cover, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                return BadRequest(new ValidationProblemDetails(
    new Dictionary<string, string[]>                {
                    [nameof(request.CoverImageUrl)] = ["CoverImageUrl must be a valid HTTP or HTTPS URL."]
                }));
            }
        }

        var trip = await _db.Trips
            .Include(t => t.TripInterests)
            .Include(t => t.Destination)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId.Value, cancellationToken);

        if (trip is null) return NotFound();

        if (trip.Status == TripLifecycle.Generating || trip.Status == TripLifecycle.Archived)
        {
            return Conflict(new { message = $"Trip cannot be modified while status is {trip.Status}." });
        }

        if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != trip.Version)
        {
            return Conflict(new
            {
                message = "Trip has been modified by another request.",
                currentVersion = trip.Version
            });
        }

        if (request.Title is not null)
            trip.Title = request.Title.Trim();

        if (request.CoverImageUrl is not null)
            trip.CoverImageUrl = request.CoverImageUrl.Trim();

        if (trip.Status is TripLifecycle.Generated or TripLifecycle.Saved)
            TripLifecycle.Transition(trip, TripLifecycle.Modified);

        trip.UpdatedAt = DateTime.UtcNow;
        trip.Version++;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Trip has been modified by another request." });
        }

        var itinerary = await _db.Itineraries
            .AsNoTracking()
            .Include(x => x.Days)
                .ThenInclude(x => x.Items)
                    .ThenInclude(x => x.Place)
            .FirstOrDefaultAsync(x => x.TripId == id, cancellationToken);

        var costEstimate = await _db.CostEstimates
            .AsNoTracking()
            .Include(x => x.CostCategory)
            .Include(x => x.Currency)
            .Where(x => x.TripId == id)
            .OrderBy(x => x.CostCategoryId)
            .ToListAsync(cancellationToken);

        return Ok(ToResponse(trip, itinerary, costEstimate));
    }

    [HttpPost("{id:guid}/save")]
    public async Task<IActionResult> Save(
        Guid id,
        CancellationToken cancellationToken)
    {
        var trip = await GetOwnedTrip(id, cancellationToken);

        if (trip is null)
            return NotFound();

        if (!TripLifecycle.CanTransition(
                trip.Status,
                TripLifecycle.Saved))
        {
            return Conflict(new
            {
                message =
                    $"Trip cannot be saved from status {trip.Status}."
            });
        }

        TripLifecycle.Transition(
            trip,
            TripLifecycle.Saved);

        trip.UpdatedAt = DateTime.UtcNow;
        trip.Version++;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetById(id, cancellationToken);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(
        Guid id,
        CancellationToken cancellationToken)
    {
        var trip = await GetOwnedTrip(id, cancellationToken);

        if (trip is null)
            return NotFound();

        if (!TripLifecycle.CanTransition(
                trip.Status,
                TripLifecycle.Archived))
        {
            return Conflict(new
            {
                message =
                    $"Trip cannot be archived from status {trip.Status}."
            });
        }

        TripLifecycle.Transition(
            trip,
            TripLifecycle.Archived);

        trip.UpdatedAt = DateTime.UtcNow;
        trip.Version++;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            trip.Id,
            trip.Status,
            trip.Version
        });
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(
        Guid id,
        CancellationToken cancellationToken)
    {
        var trip = await GetOwnedTrip(id, cancellationToken);

        if (trip is null)
            return NotFound();

        if (!TripLifecycle.CanTransition(
                trip.Status,
                TripLifecycle.Saved))
        {
            return Conflict(new
            {
                message =
                    $"Trip cannot be restored from status {trip.Status}."
            });
        }

        TripLifecycle.Transition(
            trip,
            TripLifecycle.Saved);

        trip.UpdatedAt = DateTime.UtcNow;
        trip.Version++;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            trip.Id,
            trip.Status,
            trip.Version
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTripRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        var trip = await _db.Trips
            .Include(t => t.TripInterests)
            .FirstOrDefaultAsync(
                t => t.Id == id,
                cancellationToken);

        if (trip is null || trip.UserId != userId.Value)
            return NotFound();

        if (request.ExpectedVersion != trip.Version)
        {
            return Conflict(new
            {
                message =
                    "Trip has been modified by another request.",
                currentVersion = trip.Version
            });
        }

        if (trip.Status == TripLifecycle.Generating ||
            trip.Status == TripLifecycle.Archived)
        {
            return Conflict(new
            {
                message =
                    $"Trip cannot be modified while status is {trip.Status}."
            });
        }

        if (request.DestinationId.HasValue &&
            !await _db.Destinations.AnyAsync(
                d => d.Id == request.DestinationId.Value && d.IsSupported,
                cancellationToken))
        {
            return BadRequest(new
            {
                errors = new
                {
                    DestinationId = new[]
                    {
                        "Destination does not exist or is not supported."
                    }
                }
            });
        }

        if (request.BudgetCurrencyId.HasValue &&
            !await _db.Currencies.AnyAsync(
                c => c.Id == request.BudgetCurrencyId.Value,
                cancellationToken))
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

        if (interestIds.Count > 0)
        {
            var existingInterestIds =
                await _db.InterestCategories
                    .Where(x => interestIds.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);

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
            await _db.Database.BeginTransactionAsync(cancellationToken);

        trip.DestinationId = request.DestinationId;
        trip.StartDate = request.StartDate;
        trip.EndDate = request.EndDate;
        trip.TravelerCount = request.TravelerCount;
        trip.BudgetAmount = request.BudgetAmount;
        trip.BudgetCurrencyId = request.BudgetCurrencyId;

        if (trip.Status is TripLifecycle.Generated or TripLifecycle.Saved)
        {
            TripLifecycle.Transition(
                trip,
                TripLifecycle.Modified);
        }

        trip.UpdatedAt = DateTime.UtcNow;
        trip.Version++;

        _db.TripInterests.RemoveRange(trip.TripInterests);

        foreach (var interestId in interestIds)
        {
            trip.TripInterests.Add(new TripInterest
            {
                TripId = trip.Id,
                InterestCategoryId = interestId
            });
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);

            return Conflict(new
            {
                message =
                    "Trip has been modified by another request."
            });
        }

        return await GetById(id, cancellationToken);
    }

    private async Task<Triply.Api.Entities.Trip?> GetOwnedTrip(
        Guid id,
        CancellationToken cancellationToken)
    {
        var trip = await _db.Trips
            .FirstOrDefaultAsync(
                t => t.Id == id,
                cancellationToken);

        if (trip is null)
            return null;

        var authResult = await _authorizationService
            .AuthorizeAsync(User, trip, "TripOwner");

        return authResult.Succeeded ? trip : null;
    }

    private Guid? GetUserId()
    {
        var claim =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claim, out var userId)
            ? userId
            : null;
    }

    private static TripResponse ToResponse(
        Triply.Api.Entities.Trip trip,
        Triply.Api.Entities.Itinerary? itinerary,
        List<CostEstimate> costEstimates)
    {
        ItineraryResponse? itineraryResponse = null;

        if (itinerary is not null)
        {
            itineraryResponse = new ItineraryResponse
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
                            .OrderBy(item =>
                                TimeSlotOrder(item.TimeSlot))
                            .ThenBy(item => item.OrderIndex)
                            .Select(item =>
                                new ItineraryItemResponse
                                {
                                    Id = item.Id,
                                    PlaceId = item.PlaceId,
                                    PlaceName = item.Place.Name,
                                    TimeSlot = item.TimeSlot,
                                    OrderIndex = item.OrderIndex,
                                    EstimatedCost = item.EstimatedCost,
                                    Notes = item.Notes,
                                    IsAiGenerated =
                                        item.IsAiGenerated,
                                    ModifiedAt = item.ModifiedAt
                                })
                            .ToList()
                    })
                    .ToList()
            };
        }

        CostEstimateResponse? costResponse = null;

        if (costEstimates.Count > 0)
        {
            costResponse = new CostEstimateResponse
            {
                TripId = trip.Id,
                Categories = costEstimates
                    .Select(x =>
                        new CostCategoryEstimateResponse
                        {
                            CostCategoryId =
                                x.CostCategoryId,
                            CategoryCode =
                                x.CostCategory.Code,
                            CategoryName =
                                x.CostCategory.Label,
                            Amount = x.Amount,
                            Currency =
                                x.Currency.IsoCode,
                            IsEstimated = true
                        })
                    .ToList(),
                TotalEstimatedCost =
                    costEstimates.Sum(x => x.Amount),
                Currency =
                    costEstimates[0].Currency.IsoCode,
                IsEstimated = true
            };
        }

        return new TripResponse
        {
            Id = trip.Id,
            PlanningMode = trip.PlanningMode,
            Status = trip.Status,
            Title = trip.Title,
            CoverImageUrl = trip.CoverImageUrl,
            DestinationId = trip.DestinationId,
            DestinationName = trip.Destination?.Name,
            StartDate = trip.StartDate,
            EndDate = trip.EndDate,
            TravelerCount = trip.TravelerCount,
            BudgetAmount = trip.BudgetAmount,
            BudgetCurrencyId = trip.BudgetCurrencyId,
            InterestCategoryIds =
                trip.TripInterests
                    .Select(x => x.InterestCategoryId)
                    .ToList(),
            Itinerary = itineraryResponse,
            CostEstimate = costResponse,
            Version = trip.Version
        };
    }

    private static int TimeSlotOrder(string timeSlot) =>
        timeSlot.ToUpperInvariant() switch
        {
            "MORNING" => 1,
            "AFTERNOON" => 2,
            "EVENING" => 3,
            _ => 99
        };
}
