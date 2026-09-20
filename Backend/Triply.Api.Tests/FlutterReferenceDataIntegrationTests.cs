using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Triply.Api.Modules.Auth.Dtos;
using Xunit;

namespace Triply.Api.Tests;

public class FlutterReferenceDataIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FlutterReferenceDataIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail()
        => $"flutter_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync(string? displayName = "Flutter Test User")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                UniqueEmail(),
                "P@ssw0rd123",
                displayName));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        return body!.Token;
    }

    [Fact]
    public async Task Register_ReturnsDisplayName()
    {
        var displayName = "Dana Flutter";
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                UniqueEmail(),
                "P@ssw0rd123",
                displayName));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal(displayName, body!.DisplayName);
    }

    [Fact]
    public async Task Login_ReturnsDisplayName()
    {
        var email = UniqueEmail();
        var displayName = "Dana Login";

        await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", displayName));

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "P@ssw0rd123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal(displayName, body!.DisplayName);
    }

    [Fact]
    public async Task ReferenceEndpoints_RequireAuthentication()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/destinations")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/interest-categories")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/currencies")).StatusCode);
    }

    [Fact]
    public async Task GetDestinations_ReturnsSupportedDestinations()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/destinations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<DestinationResponse>>();
        Assert.NotNull(body);
        Assert.Contains(body!, x => x.Id == 1 && x.Name == "Paris");
        Assert.Contains(body!, x => x.Id == 2 && x.Name == "Amman");
        Assert.Contains(body!, x => x.Id == 3 && x.Name == "New York");
    }

    [Fact]
    public async Task GetInterestCategories_ReturnsReferenceCategories()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/interest-categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<InterestCategoryResponse>>();
        Assert.NotNull(body);
        Assert.Contains(body!, x => x.Id == 1 && x.Code == "NATURE");
        Assert.Contains(body!, x => x.Label == "Food");
    }

    [Fact]
    public async Task GetCurrencies_ReturnsReferenceCurrencies()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/currencies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<CurrencyResponse>>();
        Assert.NotNull(body);
        Assert.Contains(body!, x => x.Id == 1 && x.IsoCode == "USD" && x.Symbol == "$");
        Assert.Contains(body!, x => x.Id == 2 && x.IsoCode == "JOD" && x.Symbol == "JD");
    }

    private sealed class DestinationResponse
    {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public string CountryName { get; set; } = default!;
        public string? Description { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    private sealed class InterestCategoryResponse
    {
        public long Id { get; set; }
        public string Code { get; set; } = default!;
        public string Label { get; set; } = default!;
    }

    private sealed class CurrencyResponse
    {
        public long Id { get; set; }
        public string IsoCode { get; set; } = default!;
        public string Symbol { get; set; } = default!;
    }
}
