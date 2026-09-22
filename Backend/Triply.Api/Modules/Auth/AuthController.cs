using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Triply.Api.Data;
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
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwt,
        ApplicationDbContext db,
        IEmailSender emailSender,
        IConfiguration config)
    {
        _userManager = userManager;
        _jwt = jwt;
        _db = db;
        _emailSender = emailSender;
        _config = config;
    }

    // Whether login is blocked until the user confirms their email. Off by default
    // so the app keeps working end-to-end before the client has a "check your inbox"
    // screen wired up — flip Auth:RequireConfirmedEmail to true once it does.
    private bool RequireConfirmedEmail =>
        bool.TryParse(_config["Auth:RequireConfirmedEmail"], out var v) && v;

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

        await SendEmailConfirmationAsync(user);

        var authResponse = await IssueTokensAsync(user);
        return Ok(authResponse);
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

        if (RequireConfirmedEmail && !user.EmailConfirmed)
        {
            return Problem(
                title: "Please confirm your email address before logging in.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var authResponse = await IssueTokensAsync(user);
        return Ok(authResponse);
    }

    // ---- Refresh / logout (Security Task 1: refresh tokens + revocation) ----

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var tokenHash = JwtTokenService.HashToken(request.RefreshToken);
        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (existing is null || !existing.IsActive || existing.User is null || existing.User.DeletedAt is not null)
        {
            return Problem(
                title: "Invalid or expired refresh token.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Rotate: the presented token is consumed even on success, so a stolen
        // (already-used) token can't be replayed once the legitimate client rotates it.
        var (newRawToken, newExpiresAtUtc) = _jwt.GenerateRefreshToken();
        existing.RevokedAtUtc = DateTime.UtcNow;
        existing.ReplacedByTokenHash = JwtTokenService.HashToken(newRawToken);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = existing.ReplacedByTokenHash,
            ExpiresAtUtc = newExpiresAtUtc,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await _db.SaveChangesAsync();

        var (accessToken, accessExpires) = _jwt.CreateToken(existing.User);
        return Ok(new AuthResponse(
            accessToken, accessExpires,
            newRawToken, newExpiresAtUtc,
            existing.User.Id, existing.User.Email!, existing.User.DisplayName));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request)
    {
        var userId = GetUserId();
        var tokenHash = JwtTokenService.HashToken(request.RefreshToken);

        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.UserId == userId);

        // No-op (still 204) if the token is already gone/foreign — logout is idempotent
        // and shouldn't leak whether a given token string ever existed.
        if (token is not null && token.RevokedAtUtc is null)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    // ---- Email verification ----

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Problem(title: "Invalid confirmation link.", statusCode: StatusCodes.Status400BadRequest);

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return Problem(title: "Invalid or expired confirmation link.", statusCode: StatusCodes.Status400BadRequest);

        return Ok(new { message = "Email confirmed." });
    }

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        // Always 200 regardless of whether the account exists or is already confirmed —
        // otherwise this endpoint becomes a user-enumeration oracle.
        if (user is not null && !user.EmailConfirmed)
            await SendEmailConfirmationAsync(user);

        return Ok(new { message = "If that account exists, a confirmation email has been sent." });
    }

    // ---- Password reset ----

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is not null && user.DeletedAt is null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _emailSender.SendAsync(
                user.Email!,
                "Reset your Triply password",
                $"Use this token to reset your password: {token}\nUserId: {user.Id}");
        }

        // Same generic response whether or not the account exists — avoids leaking
        // which emails are registered.
        return Ok(new { message = "If that account exists, a password reset email has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Problem(title: "Invalid or expired reset token.", statusCode: StatusCodes.Status400BadRequest);

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return ValidationProblem(ModelState);
        }

        // Resetting the password invalidates every outstanding refresh token —
        // a leaked/stolen session shouldn't survive a password reset.
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync();
        foreach (var t in activeTokens)
            t.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password has been reset." });
    }

    // ---- helpers ----

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user)
    {
        var (accessToken, accessExpires) = _jwt.CreateToken(user);
        var (refreshToken, refreshExpires) = _jwt.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = JwtTokenService.HashToken(refreshToken),
            ExpiresAtUtc = refreshExpires,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return new AuthResponse(
            accessToken, accessExpires,
            refreshToken, refreshExpires,
            user.Id, user.Email!, user.DisplayName);
    }

    private async Task SendEmailConfirmationAsync(ApplicationUser user)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _emailSender.SendAsync(
            user.Email!,
            "Confirm your Triply account",
            $"Use this token to confirm your account: {token}\nUserId: {user.Id}");
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("No user id claim present."));
}
