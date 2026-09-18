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

    private async Task<long> GetDestinationIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.Destinations
            .AsNoTracking()
            .Where(x => x.IsSupported)
            .Select(x => x.Id)
            .FirstAsync();
    }

    private async Task<Guid> CreateTripAsync(string token, long destinationId)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                planningMode = "DESTINATION_FIRST",
                destinationId,
                travelerCount = 2,
                budgetAmount = 5000,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1, 2 }
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TripResponse>();
        return body!.Id;
    }

    private async Task<(long destinationId, long firstPlaceId, long secondPlaceId, decimal firstPrice, decimal secondPrice)>
        SeedTwoPlacesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var destinationId = await db.Destinations
            .AsNoTracking()
            .Where(x => x.IsSupported)
            .Select(x => x.Id)
            .FirstAsync();

        var placeCategoryId = await db.PlaceCategories
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        var costCategoryId = await db.CostCategories
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        var currencyId = await db.Currencies
            .Where(x => x.IsoCode == "USD")
            .Select(x => x.Id)
            .FirstAsync();

        var first = new Place
        {
            DestinationId = destinationId,
            PlaceCategoryId = placeCategoryId,
            Name = $"Cost Test Place A {Guid.NewGuid():N}",
            ReferencePrice = 120m,
            CurrencyId = currencyId,
            CostCategoryId = costCategoryId,
            IsActive = true
        };

        var second = new Place
        {
            DestinationId = destinationId,
            PlaceCategoryId = placeCategoryId,
            Name = $"Cost Test Place B {Guid.NewGuid():N}",
            ReferencePrice = 80m,
            CurrencyId = currencyId,
            CostCategoryId = costCategoryId,
            IsActive = true
        };

        db.Places.AddRange(first, second);
        await db.SaveChangesAsync();

        return (
            destinationId,
            first.Id,
            second.Id,
            first.ReferencePrice,
            second.ReferencePrice);
    }

    [Fact]
    public async Task CostEstimate_CalculatesFromItineraryAndPersistsTripTotal()
    {
        var token = await RegisterAndGetTokenAsync();
        var places = await SeedTwoPlacesAsync();
        var tripId = await CreateTripAsync(token, places.destinationId);

        // Deliberately send incorrect prices. The itinerary endpoint must
        // use the authoritative internal dataset price instead.
        var writeResponse = await _client.PostAsJsonAsync(
            $"/api/trips/{tripId}/itinerary",
            new
            {
                days = new[]
                {
                    new
                    {
                        dayNumber = 1,
                        date = "2026-09-20",
                        items = new[]
                        {
                            new
                            {
                                placeId = places.firstPlaceId,
                                timeSlot = "MORNING",
                                orderIndex = 1,
                                estimatedCost = 999999m,
                                isAiGenerated = true
                            },
                            new
                            {
                                placeId = places.secondPlaceId,
                                timeSlot = "AFTERNOON",
                                orderIndex = 2,
                                estimatedCost = 888888m,
                                isAiGenerated = true
                            }
                        }
                    }
                }
            });

        writeResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/trips/{tripId}/cost-estimate");

        response.EnsureSuccessStatusCode();

        var body = await response.Content
            .ReadFromJsonAsync<CostEstimateResponse>();

        Assert.NotNull(body);
        Assert.Equal(tripId, body!.TripId);
        Assert.Equal(places.firstPrice + places.secondPrice, body.TotalEstimatedCost);
        Assert.Equal("USD", body.Currency);
        Assert.True(body.IsEstimated);
        Assert.All(body.Categories, x => Assert.True(x.IsEstimated));

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var storedTotal = await verifyDb.Trips
            .Where(x => x.Id == tripId)
            .Select(x => x.TotalEstimatedCost)
            .SingleAsync();

        Assert.Equal(places.firstPrice + places.secondPrice, storedTotal);

        var persistedRows = await verifyDb.CostEstimates
            .Where(x => x.TripId == tripId)
            .ToListAsync();

        Assert.Equal(
            body.Categories.Count(x => x.Amount > 0),
            persistedRows.Count);
        Assert.Equal(
            places.firstPrice + places.secondPrice,
            persistedRows.Sum(x => x.Amount));
    }

    [Fact]
    public async Task CostEstimate_UsesPersistedItinerarySnapshotCost()
    {
        var token = await RegisterAndGetTokenAsync();
        var places = await SeedTwoPlacesAsync();
        var tripId = await CreateTripAsync(token, places.destinationId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var itinerary = new Itinerary { TripId = tripId, GeneratedAt = DateTime.UtcNow };
            var day = new ItineraryDay
            {
                Date = new DateOnly(2026, 9, 20),
                DayNumber = 1
            };
            day.Items.Add(new ItineraryItem
            {
                PlaceId = places.firstPlaceId,
                TimeSlot = "MORNING",
                OrderIndex = 1,
                EstimatedCost = 360m, // 3 nights x 120, persisted by AI orchestration
                IsAiGenerated = true
            });
            itinerary.Days.Add(day);
            db.Itineraries.Add(itinerary);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/trips/{tripId}/cost-estimate");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CostEstimateResponse>();

        Assert.NotNull(body);
        Assert.Equal(360m, body!.TotalEstimatedCost);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await verifyDb.CostEstimates
            .Where(x => x.TripId == tripId)
            .SumAsync(x => x.Amount);

        Assert.Equal(360m, persisted);
    }

    [Fact]
    public async Task CostEstimate_ReturnsAllCategoriesWithZeroForMissingRows()
    {
        var token = await RegisterAndGetTokenAsync();
        var tripId = await CreateTripAsync(token, await GetDestinationIdAsync());

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
        var tripId = await CreateTripAsync(ownerToken, await GetDestinationIdAsync());

        var otherUserToken = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otherUserToken);

        var response = await _client.GetAsync(
            $"/api/trips/{tripId}/cost-estimate");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
