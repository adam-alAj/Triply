namespace Triply.Api.Modules.User.Dtos;

public sealed class UserProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? DisplayName { get; set; }
}

public sealed class UpdateUserProfileRequest
{
    public string? DisplayName { get; set; }
}

public sealed class UserPreferencesResponse
{
    public long? PreferredCurrencyId { get; set; }
    public string? PreferredCurrency { get; set; }
    public string DistanceUnit { get; set; } = default!;
    public string Pacing { get; set; } = default!;
}

public sealed class UpdateUserPreferencesRequest
{
    public long? PreferredCurrencyId { get; set; }
    public string DistanceUnit { get; set; } = default!;
    public string Pacing { get; set; } = default!;
}

public sealed class UserStatsResponse
{
    public int TotalTrips { get; set; }
    public int TotalSavedPlaces { get; set; }
    public int TotalCountries { get; set; }
}
