using System.Net;
using System.Net.Http.Json;
using Triply.Api.Modules.Auth.Dtos;
using Xunit;

namespace Triply.Api.Tests;
public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"user_{Guid.NewGuid():N}@triply.dev";

    [Fact]
    public async Task Register_NewUser_ReturnsOkWithToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(UniqueEmail(), "P@ssw0rd123", "Test User"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsFieldLevelValidationError()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "First"));

        var second = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "AnotherP@ss1", "Second"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
      var body = await second.Content.ReadAsStringAsync();

Assert.Contains("\"errors\"", body);
Assert.Contains("Email", body);
    }

    [Theory]
    [InlineData("short1A")]       // < 8 chars
    [InlineData("alllowercase1")] // no uppercase
    [InlineData("NoDigitsHere")]  // no digit
    public async Task Register_WeakPassword_RejectedByPolicy(string weakPassword)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(UniqueEmail(), weakPassword, "Test"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Test"));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "P@ssw0rd123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsGenericUnauthorized()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Test"));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "WrongPassword1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_NonexistentEmail_ReturnsSameGenericErrorAsWrongPassword()
    {

        var existingEmail = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(existingEmail, "P@ssw0rd123", "Test"));

        var wrongPasswordResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(existingEmail, "WrongPassword1"));
        var nonexistentResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(UniqueEmail(), "WrongPassword1"));

        Assert.Equal(wrongPasswordResponse.StatusCode, nonexistentResponse.StatusCode);
        var body1 = await wrongPasswordResponse.Content.ReadAsStringAsync();
        var body2 = await nonexistentResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        // Same generic title text in both responses — no "user not found" vs "wrong password" distinction
        Assert.Contains("Invalid email or password", body1);
        Assert.Contains("Invalid email or password", body2);
    }
}
