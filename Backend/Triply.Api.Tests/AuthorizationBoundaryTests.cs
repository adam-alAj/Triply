// Backend/Triply.Api.Tests/AuthorizationBoundaryTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Triply.Api.Modules.Auth.Dtos;
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

        var createResponse = await _client.PostAsync("/api/trips/test-create", null);
        var created = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var tripId = created!["id"];

        var getResponse = await _client.GetAsync($"/api/trips/{tripId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetTrip_AnotherUsersTrip_ReturnsNotFound_NeverTheData()
    {
        var tokenA = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var createResponse = await _client.PostAsync("/api/trips/test-create", null);
        var created = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var tripId = created!["id"];

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