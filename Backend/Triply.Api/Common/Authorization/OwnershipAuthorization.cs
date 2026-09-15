using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Triply.Api.Data;

namespace Triply.Api.Common.Authorization;

// NFR-PRIV-001: a user can only access their own Trip resources.
// Usage in a controller action:
//   var trip = await _db.Trips.FindAsync(id);
//   if (trip is null) return NotFound();
//   var authResult = await _authorizationService.AuthorizeAsync(User, trip, "TripOwner");
//   if (!authResult.Succeeded) return Forbid(); // -> 403, never leaks data

public class TripOwnerRequirement : IAuthorizationRequirement { }

public class TripOwnerHandler : AuthorizationHandler<TripOwnerRequirement, Entities.Trip>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TripOwnerRequirement requirement,
        Entities.Trip resource)
    {
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? context.User.FindFirstValue("sub");

        if (Guid.TryParse(userIdClaim, out var userId) && resource.UserId == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
