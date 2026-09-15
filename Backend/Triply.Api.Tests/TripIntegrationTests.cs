using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip.Dtos;
using Xunit;

namespace Triply.Api.Tests;

public class TripIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TripIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail()
        => $"user_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                UniqueEmail(),
                "P@ssw0rd123",
                "Test User"));

        response.EnsureSuccessStatusCode();

        var body = await response.Content
            .ReadFromJsonAsync<AuthResponse>();

        return body!.Token;
    }

    [Fact]
    public async Task GetTrip_DifferentUser_ReturnsNotFound()
    {
        // Arrange
        var ownerToken = await RegisterAndGetTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                planningMode = "DESTINATION_FIRST",
                destinationId = 1,
                startDate = "2026-10-01",
                endDate = "2026-10-05",
                travelerCount = 2,
                budgetAmount = 1000,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1, 2 }
            });

        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content
            .ReadFromJsonAsync<TripResponse>();

        var tripId = created!.Id;

        // Register another user
        var otherUserToken = await RegisterAndGetTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otherUserToken);

        // Act
        var response = await _client.GetAsync(
            $"/api/trips/{tripId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrip_Owner_CanUpdateEditableFields()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                planningMode = "DESTINATION_FIRST",
                destinationId = 1,
                startDate = "2026-10-01",
                endDate = "2026-10-05",
                travelerCount = 2,
                budgetAmount = 1000,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1, 2 }
            });

        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content
            .ReadFromJsonAsync<TripResponse>();

        // Act
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/trips/{created!.Id}",
            new
            {
                destinationId = 2,
                startDate = "2026-11-01",
                endDate = "2026-11-07",
                travelerCount = 3,
                budgetAmount = 1500,
                budgetCurrencyId = 2,
                interestCategoryIds = new[] { 3, 4 }
            });

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        var updated = await updateResponse.Content
            .ReadFromJsonAsync<TripResponse>();

        Assert.Equal(2, updated!.DestinationId);
        Assert.Equal(3, updated.TravelerCount);
        Assert.Equal(1500, updated.BudgetAmount);
        Assert.Equal(new long[] { 3, 4 }, updated.InterestCategoryIds);
    }
}