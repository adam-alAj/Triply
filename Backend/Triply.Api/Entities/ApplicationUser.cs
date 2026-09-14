using Microsoft.AspNetCore.Identity;

namespace Triply.Api.Entities;

// Database Design §6.1 — wraps ASP.NET Core Identity (Task 3)
// Identity already provides Email + PasswordHash (never plaintext, NFR-SEC-002).
public class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; } // soft-delete, §16
}
