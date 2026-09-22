using System.ComponentModel.DataAnnotations;

namespace Triply.Api.Entities;

/// <summary>
/// A rotating refresh token used to obtain new short-lived access tokens without
/// re-entering credentials, and to support logout / revocation (Security Task 1).
/// Only a SHA-256 hash of the raw token is ever persisted — the raw value is
/// returned to the client once, at issuance time, and never stored.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [MaxLength(128)]
    public string TokenHash { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Hash of the token that replaced this one, when rotated (audit trail).</summary>
    [MaxLength(128)]
    public string? ReplacedByTokenHash { get; set; }

    [MaxLength(64)]
    public string? CreatedByIp { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
