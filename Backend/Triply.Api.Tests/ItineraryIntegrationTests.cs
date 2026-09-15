using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Itinerary.Dtos;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Tests;

public class ItineraryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ItineraryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"itinerary_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(UniqueEmail(), "P@ssw0rd123", "Itinerary Test User"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private async Task<Guid> CreateTripAsync(string token, long destinationId = 1)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                planningMode = "DESTINATION_FIRST",
                destinationId,
                startDate = "2026-10-01",
                endDate = "2026-10-03",
                travelerCount = 2,
                budgetAmount = 1000,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1, 2 }
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TripResponse>();
        return body!.Id;
    }

    private async Task<long[]> SeedPlacesAsync(long destinationId, int count = 3)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var placeCategoryId = await db.PlaceCategories.Select(x => x.Id).FirstAsync();
        var costCategoryId = await db.CostCategories.Select(x => x.Id).FirstAsync();

        var places = Enumerable.Range(1, count)
            .Select(i => new Place
            {
                DestinationId = destinationId,
                PlaceCategoryId = placeCategoryId,
                CostCategoryId = costCategoryId,
                CurrencyId = 1,
                Name = $"ItineraryPlace_{Guid.NewGuid():N}_{i}",
                ReferencePrice = 20m + i,
                IsActive = true
            })
            .ToList();

        db.Places.AddRange(places);
        await db.SaveChangesAsync();
        return places.Select(x => x.Id).ToArray();
    }

    [Fact]
    public async Task WriteThenRead_ReturnsDaysAndItemsInRequiredOrderWithPlaceNames()
    {
        var token = await RegisterAndGetTokenAsync();
        var tripId = await CreateTripAsync(token);
        var placeIds = await SeedPlacesAsync(1, 3);

        var writeResponse = await _client.PostAsJsonAsync(
            $"/api/trips/{tripId}/itinerary",
            new
            {
                days = new[]
                {
                    new
                    {
                        dayNumber = 2,
                        date = "2026-10-02",
                        items = new[]
                        {
                            new { placeId = placeIds[0], timeSlot = "EVENING", orderIndex = 1, estimatedCost = 30m, notes = "Dinner", isAiGenerated = true }
                        }
                    },
                    new
                    {
                        dayNumber = 1,
                        date = "2026-10-01",
                        items = new[]
                        {
                            new { placeId = placeIds[1], timeSlot = "AFTERNOON", orderIndex = 0, estimatedCost = 20m, notes = (string?)null, isAiGenerated = true },
                            new { placeId = placeIds[2], timeSlot = "MORNING", orderIndex = 1, estimatedCost = 10m, notes = "Early visit", isAiGenerated = false }
                        }
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, writeResponse.StatusCode);

        var readResponse = await _client.GetAsync($"/api/trips/{tripId}/itinerary");
        readResponse.EnsureSuccessStatusCode();

        var body = await readResponse.Content.ReadFromJsonAsync<ItineraryResponse>();

        Assert.NotNull(body);
        Assert.Equal(tripId, body!.TripId);
        Assert.Equal(new[] { 1, 2 }, body.Days.Select(x => x.DayNumber));
        Assert.Equal(new[] { "MORNING", "AFTERNOON" }, body.Days[0].Items.Select(x => x.TimeSlot));
        Assert.Equal(placeIds[2], body.Days[0].Items[0].PlaceId);
        Assert.Equal(placeIds[1], body.Days[0].Items[1].PlaceId);
        Assert.False(string.IsNullOrWhiteSpace(body.Days[0].Items[0].PlaceName));
    }

    [Fact]
    public async Task Write_InvalidTimeSlot_ReturnsBadRequest()
    {
        var token = await RegisterAndGetTokenAsync();
        var tripId = await CreateTripAsync(token);
        var placeIds = await SeedPlacesAsync(1, 1);

        var response = await _client.PostAsJsonAsync(
            $"/api/trips/{tripId}/itinerary",
            new
            {
                days = new[]
                {
                    new
                    {
                        dayNumber = 1,
                        date = "2026-10-01",
                        items = new[]
                        {
                            new { placeId = placeIds[0], timeSlot = "NIGHT", orderIndex = 0, estimatedCost = 10m, isAiGenerated = true }
                        }
                    }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("TimeSlot", body);
    }

    [Fact]
    public async Task Write_UnknownPlace_ReturnsBadRequest()
    {
        var token = await RegisterAndGetTokenAsync();
        var tripId = await CreateTripAsync(token);

        var response = await _client.PostAsJsonAsync(
            $"/api/trips/{tripId}/itinerary",
            new
            {
                days = new[]
                {
                    new
                    {
                        dayNumber = 1,
                        date = "2026-10-01",
                        items = new[]
                        {
                            new { placeId = 999999999L, timeSlot = "MORNING", orderIndex = 0, estimatedCost = 10m, isAiGenerated = true }
                        }
                    }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      var body = await response.Content.ReadAsStringAsync();
Assert.Contains("placeId", body);
    }

    [Fact]
    public async Task Get_DifferentUser_ReturnsNotFound()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var tripId = await CreateTripAsync(ownerToken);
        var placeIds = await SeedPlacesAsync(1, 1);

        await _client.PostAsJsonAsync(
            $"/api/trips/{tripId}/itinerary",
            new
            {
                days = new[]
                {
                    new
                    {
                        dayNumber = 1,
                        date = "2026-10-01",
                        items = new[]
                        {
                            new { placeId = placeIds[0], timeSlot = "MORNING", orderIndex = 0, estimatedCost = 10m, isAiGenerated = true }
                        }
                    }
                }
            });

        var otherToken = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otherToken);

        var response = await _client.GetAsync($"/api/trips/{tripId}/itinerary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ItineraryPlace_", body);
    }
}
