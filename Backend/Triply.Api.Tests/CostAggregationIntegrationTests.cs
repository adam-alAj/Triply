using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Cost.Dtos;
using Triply.Api.Modules.Trip.Dtos;
using Xunit;

namespace Triply.Api.Tests;

public class CostAggregationIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CostAggregationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail()
        => $"cost_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                UniqueEmail(),
                "P@ssw0rd123",
                "Cost Test User"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private async Task<Guid> CreateTripAsync(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                planningMode = "DESTINATION_FIRST",
                destinationId = 1,
                travelerCount = 2,
                budgetAmount = 5000,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1, 2 }
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TripResponse>();
        return body!.Id;
    }

    private async Task SeedCostEstimatesAsync(
        Guid tripId,
        params (long categoryId, decimal amount)[] estimates)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var estimate in estimates)
        {
            db.CostEstimates.Add(new CostEstimate
            {
                TripId = tripId,
                CostCategoryId = estimate.categoryId,
                Amount = estimate.amount,
                CurrencyId = 1,
                ComputedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CostEstimate_AggregatesCategoriesAndPersistsTotal()
    {
        var token = await RegisterAndGetTokenAsync();
        var tripId = await CreateTripAsync(token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var categoryIds = await db.CostCategories
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .Take(2)
                .ToListAsync();

            await SeedCostEstimatesAsync(
                tripId,
                (categoryIds[0], 120m),
                (categoryIds[1], 80m));
        }

        var response = await _client.GetAsync(
            $"/api/trips/{tripId}/cost-estimate");

        response.EnsureSuccessStatusCode();
        var body = await response.Content
            .ReadFromJsonAsync<CostEstimateResponse>();

        Assert.NotNull(body);
        Assert.Equal(tripId, body!.TripId);
        Assert.Equal(200m, body.TotalEstimatedCost);
        Assert.Equal("USD", body.Currency);
        Assert.True(body.IsEstimated);
        Assert.All(body.Categories, x => Assert.True(x.IsEstimated));

        Assert.Equal(2, body.Categories.Count(x => x.Amount > 0));
        Assert.Contains(body.Categories, x => x.Amount == 120m);
        Assert.Contains(body.Categories, x => x.Amount == 80m);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storedTotal = await verifyDb.Trips
            .Where(x => x.Id == tripId)
            .Select(x => x.TotalEstimatedCost)
            .SingleAsync();

        Assert.Equal(200m, storedTotal);
    }

    [Fact]
    public async Task CostEstimate_ReturnsAllCategoriesWithZeroForMissingRows()
    {
        var token = await RegisterAndGetTokenAsync();
        var tripId = await CreateTripAsync(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var categoryCount = await db.CostCategories.CountAsync();

        var response = await _client.GetAsync(
            $"/api/trips/{tripId}/cost-estimate");

        response.EnsureSuccessStatusCode();
        var body = await response.Content
            .ReadFromJsonAsync<CostEstimateResponse>();

        Assert.NotNull(body);
        Assert.Equal(categoryCount, body!.Categories.Count);
        Assert.All(body.Categories, x => Assert.Equal(0m, x.Amount));
        Assert.Equal(0m, body.TotalEstimatedCost);
        Assert.Equal("USD", body.Currency);
        Assert.True(body.IsEstimated);
    }

    [Fact]
    public async Task CostEstimate_DifferentUser_ReturnsNotFound()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var tripId = await CreateTripAsync(ownerToken);

        var otherUserToken = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otherUserToken);

        var response = await _client.GetAsync(
            $"/api/trips/{tripId}/cost-estimate");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
