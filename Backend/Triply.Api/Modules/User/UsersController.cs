using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.User.Dtos;

namespace Triply.Api.Modules.User;

[ApiController]
[Route("api/users/me")]
[Authorize]
[EnableRateLimiting("fixed")]
public sealed class UsersController : ControllerBase
{
    private static readonly string[] AllowedDistanceUnits = ["KM", "MILES"];
    private static readonly string[] AllowedPacing = ["RELAXED", "BALANCED", "FAST"];

    private readonly ApplicationDbContext _db;

    public UsersController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId.Value && x.DeletedAt == null, cancellationToken);

        if (user is null) return Unauthorized();

        return Ok(ToProfileResponse(user));
    }

    [HttpPatch]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return BadRequest(new ValidationProblemDetails(
    new Dictionary<string, string[]>            {
                [nameof(request.DisplayName)] = ["DisplayName is required and cannot be empty."]
            }));
        }

        if (displayName.Length > 100)
        {
            return BadRequest(new ValidationProblemDetails(
    new Dictionary<string, string[]>            {
                [nameof(request.DisplayName)] = ["DisplayName must be at most 100 characters."]
            }));
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == userId.Value && x.DeletedAt == null, cancellationToken);

        if (user is null) return Unauthorized();

        user.DisplayName = displayName;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToProfileResponse(user));
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var userExists = await _db.Users.AnyAsync(
            x => x.Id == userId.Value && x.DeletedAt == null,
            cancellationToken);
        if (!userExists) return Unauthorized();

        var preferences = await _db.UserPreferences
            .AsNoTracking()
            .Include(x => x.PreferredCurrency)
            .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

        if (preferences is null)
        {
            preferences = new UserPreferences
            {
                UserId = userId.Value,
                PreferredCurrencyId = await _db.Currencies
                    .OrderBy(x => x.Id)
                    .Select(x => (long?)x.Id)
                    .FirstOrDefaultAsync(cancellationToken),
                DistanceUnit = "KM",
                Pacing = "BALANCED"
            };

            _db.UserPreferences.Add(preferences);
            await _db.SaveChangesAsync(cancellationToken);

            await _db.Entry(preferences)
                .Reference(x => x.PreferredCurrency)
                .LoadAsync(cancellationToken);
        }

        return Ok(ToPreferencesResponse(preferences));
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var userExists = await _db.Users.AnyAsync(
            x => x.Id == userId.Value && x.DeletedAt == null,
            cancellationToken);
        if (!userExists) return Unauthorized();

        var distanceUnit = request.DistanceUnit?.Trim().ToUpperInvariant();
        var pacing = request.Pacing?.Trim().ToUpperInvariant();

        var errors = new Dictionary<string, string[]>();
        if (request.PreferredCurrencyId.HasValue && request.PreferredCurrencyId.Value <= 0)
            errors[nameof(request.PreferredCurrencyId)] = ["PreferredCurrencyId must be greater than 0."];

        if (!AllowedDistanceUnits.Contains(distanceUnit, StringComparer.Ordinal))
            errors[nameof(request.DistanceUnit)] = ["DistanceUnit must be KM or MILES."];

        if (!AllowedPacing.Contains(pacing, StringComparer.Ordinal))
            errors[nameof(request.Pacing)] = ["Pacing must be RELAXED, BALANCED, or FAST."];

        if (request.PreferredCurrencyId.HasValue &&
            !await _db.Currencies.AnyAsync(
                x => x.Id == request.PreferredCurrencyId.Value,
                cancellationToken))
        {
            errors[nameof(request.PreferredCurrencyId)] = ["Preferred currency does not exist."];
        }

        if (errors.Count > 0)
return BadRequest(new ValidationProblemDetails(errors));
        var preferences = await _db.UserPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

        if (preferences is null)
        {
            preferences = new UserPreferences { UserId = userId.Value };
            _db.UserPreferences.Add(preferences);
        }

        preferences.PreferredCurrencyId = request.PreferredCurrencyId;
        preferences.DistanceUnit = distanceUnit!;
        preferences.Pacing = pacing!;

        await _db.SaveChangesAsync(cancellationToken);

        await _db.Entry(preferences)
            .Reference(x => x.PreferredCurrency)
            .LoadAsync(cancellationToken);

        return Ok(ToPreferencesResponse(preferences));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var userExists = await _db.Users.AnyAsync(
            x => x.Id == userId.Value && x.DeletedAt == null,
            cancellationToken);
        if (!userExists) return Unauthorized();

        var trips = _db.Trips
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value);

        var totalTrips = await trips.CountAsync(cancellationToken);

        // "Saved places" are the distinct places contained in the user's SAVED trips.
        var totalSavedPlaces = await _db.ItineraryItems
            .AsNoTracking()
            .Where(x => x.ItineraryDay.Itinerary.Trip.UserId == userId.Value &&
                        x.ItineraryDay.Itinerary.Trip.Status == Triply.Api.Modules.Trip.TripLifecycle.Saved)
            .Select(x => x.PlaceId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalCountries = await trips
            .Where(x => x.Destination != null)
            .Select(x => x.Destination!.CountryId)
            .Distinct()
            .CountAsync(cancellationToken);

        return Ok(new UserStatsResponse
        {
            TotalTrips = totalTrips,
            TotalSavedPlaces = totalSavedPlaces,
            TotalCountries = totalCountries
        });
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private static UserProfileResponse ToProfileResponse(ApplicationUser user) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        DisplayName = user.DisplayName
    };

    private static UserPreferencesResponse ToPreferencesResponse(UserPreferences preferences) => new()
    {
        PreferredCurrencyId = preferences.PreferredCurrencyId,
        PreferredCurrency = preferences.PreferredCurrency?.IsoCode,
        DistanceUnit = preferences.DistanceUnit,
        Pacing = preferences.Pacing
    };
}
