// Backend/Triply.Api/Modules/Trip/TripsController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;

namespace Triply.Api.Modules.Trip;

[ApiController]
[Route("api/trips")]
[Authorize] // NFR-PRIV-001: every action here requires a valid JWT
public class TripsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthorizationService _authorizationService;

    public TripsController(ApplicationDbContext db, IAuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
    }

    // Minimal endpoint whose only purpose right now is to prove the
    // ownership boundary (Task 6/9). Full Trip CRUD is a separate task.
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
}