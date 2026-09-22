using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip.Dtos;
using Xunit;

namespace Triply.Api.Tests;

// Covers the "Needs to be fixed" security items: refresh tokens + logout/revocation,
// email verification, password reset, and the InterestCategoryIds size limit.
public class SecurityFixesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityFixesTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"user_{Guid.NewGuid():N}@triply.dev";

    private async Task<AuthResponse> RegisterAsync(string? email = null)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email ?? UniqueEmail(), "P@ssw0rd123", "Test User"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    // ---- Refresh tokens ----

    [Fact]
    public async Task Register_ReturnsRefreshTokenAlongsideAccessToken()
    {
        var auth = await RegisterAsync();

        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.True(auth.RefreshTokenExpiresAtUtc > auth.ExpiresAtUtc);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewTokenPair()
    {
        var auth = await RegisterAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.False(string.IsNullOrWhiteSpace(refreshed!.Token));
        Assert.NotEqual(auth.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Refresh_TokenIsRotated_OldTokenCanNoLongerBeUsed()
    {
        var auth = await RegisterAsync();

        var first = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));
        first.EnsureSuccessStatusCode();

        // Replaying the original (now-rotated) refresh token must fail.
        var replay = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_UnknownToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest("not-a-real-refresh-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Logout / revocation ----

    [Fact]
    public async Task Logout_RevokesRefreshToken_SubsequentRefreshFails()
    {
        var auth = await RegisterAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var logout = await _client.PostAsJsonAsync("/api/auth/logout",
            new RefreshTokenRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;
        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_RequiresAuthentication()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/logout",
            new RefreshTokenRequest("whatever"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Email verification ----

    [Fact]
    public async Task Register_SendsEmailConfirmationToken()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var sent = TestEmailSender.LastFor(email);
        Assert.NotNull(sent);
        Assert.Contains("confirm", sent!.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmEmail_ValidToken_Succeeds()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var sent = TestEmailSender.LastFor(email)!;
        var (token, userId) = TestEmailSender.ParseTokenAndUserId(sent);

        var response = await _client.GetAsync(
            $"/api/auth/confirm-email?userId={userId}&token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_ReturnsBadRequest()
    {
        var email = UniqueEmail();
        var auth = await RegisterAsync(email);

        var response = await _client.GetAsync(
            $"/api/auth/confirm-email?userId={auth.UserId}&token=not-a-real-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_UnknownEmail_StillReturnsOk()
    {
        // Must not leak whether the address is registered.
        var response = await _client.PostAsJsonAsync("/api/auth/resend-confirmation",
            new ResendConfirmationRequest(UniqueEmail()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Password reset ----

    [Fact]
    public async Task ForgotPassword_UnknownEmail_StillReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(UniqueEmail()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_AllowsLoginWithNewPassword()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email));
        forgot.EnsureSuccessStatusCode();

     var sent = TestEmailSender.LastFor(email)!;
var (token, userId) = TestEmailSender.ParseTokenAndUserId(sent);

var reset = await _client.PostAsJsonAsync("/api/auth/reset-password",
    new ResetPasswordRequest(userId, token, "NewP@ssw0rd456"));

if (reset.StatusCode != HttpStatusCode.OK)
{
    var body = await reset.Content.ReadAsStringAsync();
    throw new Exception($"RESET FAILED: {reset.StatusCode} | {body}");
}

var login = await _client.PostAsJsonAsync("/api/auth/login",
    new LoginRequest(email, "NewP@ssw0rd456"));
Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_RevokesExistingRefreshTokens()
    {
        var email = UniqueEmail();
        var auth = await RegisterAsync(email);

        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email));
        forgot.EnsureSuccessStatusCode();

        var sent = TestEmailSender.LastFor(email)!;
        var (token, userId) = TestEmailSender.ParseTokenAndUserId(sent);

        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(userId, token, "NewP@ssw0rd456"));
        reset.EnsureSuccessStatusCode();

        // The refresh token issued at registration, before the reset, must now be dead.
        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    // ---- InterestCategoryIds size limit ----

    [Fact]
    public async Task CreateTrip_TooManyInterestCategoryIds_ReturnsBadRequest()
    {
        var auth = await RegisterAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var tooMany = Enumerable.Range(1, 51).Select(i => (long)i).ToArray();

        var response = await _client.PostAsJsonAsync("/api/trips", new
        {
            planningMode = "BUDGET_FIRST",
            destinationId = (long?)null,
            startDate = "2026-10-01",
            endDate = "2026-10-03",
            travelerCount = 2,
            budgetAmount = 1000,
            budgetCurrencyId = 1,
            interestCategoryIds = tooMany
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTrip_WithinInterestCategoryLimit_Succeeds()
    {
        var auth = await RegisterAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var response = await _client.PostAsJsonAsync("/api/trips", new
        {
            planningMode = "BUDGET_FIRST",
            destinationId = (long?)null,
            startDate = "2026-10-01",
            endDate = "2026-10-03",
            travelerCount = 2,
            budgetAmount = 1000,
            budgetCurrencyId = 1,
            interestCategoryIds = new[] { 1, 3 }
        });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TripResponse>();
        Assert.NotNull(created);
    }
}
