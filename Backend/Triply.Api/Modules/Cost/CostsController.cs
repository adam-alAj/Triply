using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Modules.Cost.Dtos;

namespace Triply.Api.Modules.Cost;

[ApiController]
[Route("api/trips/{tripId:guid}/cost-estimate")]
[Authorize]
[EnableRateLimiting("fixed")]
public class CostsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICostAggregationService _costAggregationService;

    public CostsController(
        ApplicationDbContext db,
        IAuthorizationService authorizationService,
        ICostAggregationService costAggregationService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _costAggregationService = costAggregationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCostEstimate(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
            return NotFound();

        var authResult = await _authorizationService
            .AuthorizeAsync(User, trip, "TripOwner");

        if (!authResult.Succeeded)
            return NotFound();

        try
        {
            var response = await _costAggregationService
                .CalculateAsync(tripId, cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                errors = new
                {
                    Currency = new[] { ex.Message }
                }
            });
        }
    }
}
