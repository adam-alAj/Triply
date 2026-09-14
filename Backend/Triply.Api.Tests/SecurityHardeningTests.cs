using System.Net;
using System.Net.Http.Json;
using Triply.Api.Modules.Auth.Dtos;
using Xunit;

namespace Triply.Api.Tests;

// Task 8 — Rate Limiting, CORS, Security Headers
public class SecurityHardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityHardeningTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"user_{Guid.NewGuid():N}@triply.dev";

    // AC 1: Requests exceeding the rate limit receive 429
    [Fact]
    public async Task Login_ExceedingRateLimit_Returns429()
    {
        var body = new LoginRequest(UniqueEmail(), "WrongPassword1");

        HttpResponseMessage? lastResponse = null;

        // "login" limiter is 5/min — fire 6 requests back to back
        for (int i = 0; i < 6; i++)
        {
            lastResponse = await _client.PostAsJsonAsync("/api/auth/login", body);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }

    // Sanity check: Register is NOT limited by the strict "login" policy —
    // it should still succeed after several calls (uses "fixed", 100/min).
    [Fact]
    public async Task Register_NotAffectedByLoginRateLimit()
    {
        HttpResponseMessage? lastResponse = null;

        for (int i = 0; i < 6; i++)
        {
            lastResponse = await _client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(UniqueEmail(), "P@ssw0rd123", "Test"));
        }

        Assert.Equal(HttpStatusCode.OK, lastResponse!.StatusCode);
    }

    // AC 2: CORS blocks unauthorized origins
    [Fact]
    public async Task Preflight_FromDisallowedOrigin_DoesNotEchoThatOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/register");
        request.Headers.Add("Origin", "https://not-allowed-origin.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        var allowOriginHeader = response.Headers.Contains("Access-Control-Allow-Origin")
            ? string.Join(",", response.Headers.GetValues("Access-Control-Allow-Origin"))
            : null;

        Assert.NotEqual("https://not-allowed-origin.com", allowOriginHeader);
    }

    // AC 2b: CORS allows the configured Flutter origin
    [Fact]
    public async Task Preflight_FromAllowedOrigin_EchoesThatOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/register");
        request.Headers.Add("Origin", "http://localhost:5173"); // default AllowedOrigins value
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Contains("http://localhost:5173", values!);
    }

    // AC 3: Security header checklist present on every response
    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "no-referrer")]
    public async Task Response_IncludesSecurityHeader(string header, string expectedValue)
    {
        var response = await _client.GetAsync("/health");

        Assert.True(response.Headers.TryGetValues(header, out var values),
            $"Missing expected security header: {header}");
        Assert.Contains(expectedValue, values!);
    }
}