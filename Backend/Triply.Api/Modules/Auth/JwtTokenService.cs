using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Triply.Api.Entities;

namespace Triply.Api.Modules.Auth;

public class JwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    // Reads the signing key from configuration/environment — never hardcoded (see .env.example).
    // Access tokens are intentionally short-lived (default 15 min) now that refresh tokens
    // exist to renew them — a stolen access token has a small blast radius, and a stolen
    // refresh token can be revoked (see RefreshToken entity / AuthController.Logout).
    public (string token, DateTime expiresAtUtc) CreateToken(ApplicationUser user)
    {
        var jwtKey = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured. Set it via environment/user-secrets.");
        var issuer = _config["Jwt:Issuer"] ?? "Triply";
        var audience = _config["Jwt:Audience"] ?? "TriplyClients";
        var expiresMinutes = int.TryParse(_config["Jwt:ExpiresMinutes"], out var m) ? m : 15;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var token = new JwtSecurityToken(issuer, audience, claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    /// <summary>
    /// Generates a cryptographically random refresh token (raw value, returned to the
    /// client once) and how long it should live. Only <see cref="HashToken"/> of this
    /// value is ever persisted — see RefreshToken.TokenHash.
    /// </summary>
    public (string rawToken, DateTime expiresAtUtc) GenerateRefreshToken()
    {
        var days = int.TryParse(_config["Jwt:RefreshTokenExpiresDays"], out var d) ? d : 30;
        var bytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (rawToken, DateTime.UtcNow.AddDays(days));
    }

    /// <summary>SHA-256 hash of a raw refresh token, for storage/lookup — never store the raw value.</summary>
    public static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}