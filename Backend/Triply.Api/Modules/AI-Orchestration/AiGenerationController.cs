using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Modules.AIOrchestration.Dtos;

namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// TASK47 — real endpoint replacing any mocked generation path. Ownership is checked
/// the same way ItinerariesController does (resource-based "TripOwner" policy) before
/// the orchestration service is ever invoked.
/// </summary>
[ApiController]
[Route("api/trips/{tripId:guid}/generate")]
[Authorize]
[EnableRateLimiting("ai-generation")]
public class AiGenerationController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAiOrchestrationService _orchestrationService;

    public AiGenerationController(
        ApplicationDbContext db,
        IAuthorizationService authorizationService,
        IAiOrchestrationService orchestrationService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _orchestrationService = orchestrationService;
    }

    [HttpPost]
    public async Task<IActionResult> Generate(Guid tripId, [FromBody] GenerateItineraryRequest? request, CancellationToken cancellationToken)
    {
        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);
        if (trip is null)
            return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(User, trip, "TripOwner");
        if (!authResult.Succeeded)
            return NotFound();

        request ??= new GenerateItineraryRequest();
        var scope = request.Scope?.Trim().ToUpperInvariant() ?? "FULL";

        if (scope is not "FULL" and not "DAY" and not "ITEM")
            return BadRequest(new { errors = new Dictionary<string, string[]> { [nameof(request.Scope)] = ["Scope must be FULL, DAY, or ITEM."] } });

        if (scope is "DAY" or "ITEM")
        {
            if (!request.ExpectedVersion.HasValue || request.ExpectedVersion.Value < 1)
            {
                return BadRequest(new
                {
                    errors = new Dictionary<string, string[]>
                    {
                        [nameof(request.ExpectedVersion)] = ["ExpectedVersion is required for partial regeneration."]
                    }
                });
            }
        }

        AiGenerationResult result;
        try
        {
            result = scope == "FULL"
                ? await _orchestrationService.GenerateItineraryAsync(tripId, cancellationToken)
                : await _orchestrationService.RegeneratePartialAsync(tripId, request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
       catch (InvalidOperationException ex)
{
    return Conflict(new
    {
        message = ex.Message
    });
}
catch (DbUpdateConcurrencyException ex)
{
    return Conflict(new
    {
        message = "Trip has been modified by another request.",
        detail = ex.Message,
        inner = ex.InnerException?.Message
    });
}
        if (!result.Success)
        {
            // Never a fabricated result (FR-AI-002) — a clear, distinguishable failure instead.
            return UnprocessableEntity(new
            {
                message = "AI generation failed validation after bounded retries.",
                aiGenerationId = result.AiGenerationId,
                attemptsUsed = result.AttemptsUsed,
                errors = result.Errors
            });
        }

        return Ok(new
        {
            aiGenerationId = result.AiGenerationId,
            attemptsUsed = result.AttemptsUsed,
            tripVersion = result.TripVersion ?? trip.Version,
            itinerary = result.Itinerary,
            cost = result.Cost
        });
    }
}
