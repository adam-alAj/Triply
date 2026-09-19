namespace Triply.Api.Entities;

/// <summary>
/// Server-side application preferences for the authenticated user.
/// One row per user so preferences persist across devices and sessions.
/// </summary>
public class UserPreferences
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = default!;

    public long? PreferredCurrencyId { get; set; }
    public Currency? PreferredCurrency { get; set; }

    public string DistanceUnit { get; set; } = "KM"; // KM | MILES
    public string Pacing { get; set; } = "BALANCED"; // RELAXED | BALANCED | FAST
}
