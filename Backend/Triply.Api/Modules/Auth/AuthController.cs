using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Triply.Api.Entities;
using Triply.Api.Modules.Auth.Dtos;

namespace Triply.Api.Modules.Auth;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("fixed")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenService _jwt;

    public AuthController(UserManager<ApplicationUser> userManager, JwtTokenService jwt)
    {
        _userManager = userManager;
        _jwt = jwt;
    }

    // FR-AUTH-001 — duplicate email rejected with field-level error
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(request.Email), "An account with this email already exists.");
            return ValidationProblem(ModelState);
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return ValidationProblem(ModelState);
        }

        var (token, expires) = _jwt.CreateToken(user);
        return Ok(new AuthResponse(token, expires, user.Id, user.Email!));
    }

    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        var genericError = () => Problem(
            title: "Invalid email or password.",
            statusCode: StatusCodes.Status401Unauthorized);

        if (user is null || user.DeletedAt is not null)
            return genericError();

        var valid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!valid)
            return genericError();

        var (token, expires) = _jwt.CreateToken(user);
        return Ok(new AuthResponse(token, expires, user.Id, user.Email!));
    }
}
