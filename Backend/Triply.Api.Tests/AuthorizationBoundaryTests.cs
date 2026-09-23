// Backend/Triply.Api.Tests/AuthorizationBoundaryTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip.Dtos;
using Xunit;

namespace Triply.Api.Tests;

// Task 9 AC: no JWT -> 401; another user's trip -> 403/404, never the data.
public class AuthorizationBoundaryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthorizationBoundaryTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"user_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(UniqueEmail(), "P@ssw0rd123", "Test User"));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    /// <summary>
    /// Creates a trip through the real, validated endpoint. The former test-only
    /// POST /api/trips/test-create endpoint was removed (Gap 2) and nothing may
    /// depend on it anymore.
    /// </summary>
    private async Task<Guid> CreateTripAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                planningMode = "DESTINATION_FIRST",
                destinationId = 1,
                startDate = "2026-10-01",
                endDate = "2026-10-03",
                travelerCount = 1
            });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"HTTP {(int)response.StatusCode}\n{body}");

        var trip = await response.Content.ReadFromJsonAsync<TripResponse>();
        Assert.NotNull(trip);
        return trip!.Id;
    }

    [Fact]
    public async Task PostTestCreate_IsRemoved_ReturnsNotFound()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync("/api/trips/test-create", null);

        // The test-only endpoint must not exist in any build anymore: no POST
        // handler is routable on this path. Routing answers 404, or 405 because
        // the guid-constrained sibling routes (GET/PATCH/PUT api/trips/{id:guid})
        // claim the path for method selection — both prove the action is gone
        // (verified against the actual Release binary). If the endpoint still
        // existed, this POST would return 200/401 instead.
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected test-create to be unexposed, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task GetTrip_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync($"/api/trips/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTrip_OwnTrip_ReturnsOk()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tripId = await CreateTripAsync();

        var getResponse = await _client.GetAsync($"/api/trips/{tripId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetTrip_AnotherUsersTrip_ReturnsNotFound_NeverTheData()
    {
        var tokenA = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var tripId = await CreateTripAsync();

        var tokenB = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var getResponse = await _client.GetAsync($"/api/trips/{tripId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        var body = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("DRAFT", body);
    }

    [Fact]
    public async Task GetTrip_NonexistentTrip_ReturnsNotFound()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/trips/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}