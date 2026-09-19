using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Tests;

public class ProfileAndMobileFeatureIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProfileAndMobileFeatureIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"profile_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync(string displayName = "Profile User")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(UniqueEmail(), "P@ssw0rd123", displayName));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private void UseToken(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<(Guid TripId, int Version)> CreateTripAsync(string token)
    {
        UseToken(token);
        var response = await _client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                planningMode = "DESTINATION_FIRST",
                destinationId = 1,
                startDate = "2026-10-01",
                endDate = "2026-10-02",
                travelerCount = 1,
                budgetAmount = 500,
                budgetCurrencyId = 1,
                interestCategoryIds = Array.Empty<long>()
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TripResponse>();
        return (body!.Id, body.Version);
    }

    private async Task<long[]> SeedPlacesAsync(long destinationId, int count = 2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var places = Enumerable.Range(1, count)
            .Select(i => new Place
            {
                DestinationId = destinationId,
                PlaceCategoryId = 1,
                CostCategoryId = 4,
                CurrencyId = 1,
                Name = $"ProfilePlace_{Guid.NewGuid():N}_{i}",
                Description = "Profile feature test place",
                ReferencePrice = 20 + i,
                IsActive = true
            })
            .ToList();

        db.Places.AddRange(places);
        await db.SaveChangesAsync();
        return places.Select(x => x.Id).ToArray();
    }

    [Fact]
    public async Task Profile_GetAndPatch_UsesAuthenticatedUser()
    {
        var token = await RegisterAndGetTokenAsync("Original Name");
        UseToken(token);

        var get = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var before = await get.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal("Original Name", before!.DisplayName);

        var patch = await _client.PatchAsJsonAsync(
            "/api/users/me",
            new { displayName = "Updated Name" });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var afterPatch = await patch.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal("Updated Name", afterPatch!.DisplayName);

        var getAgain = await _client.GetFromJsonAsync<UserProfileDto>("/api/users/me");
        Assert.Equal("Updated Name", getAgain!.DisplayName);
    }

    [Fact]
    public async Task ProfileEndpoints_RejectUnauthenticatedRequests()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/users/me/preferences")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/users/me/stats")).StatusCode);
    }

    [Fact]
    public async Task Preferences_PersistAndValidateValues()
    {
        var token = await RegisterAndGetTokenAsync();
        UseToken(token);

        var update = await _client.PutAsJsonAsync(
            "/api/users/me/preferences",
            new
            {
                preferredCurrencyId = 2,
                distanceUnit = "MILES",
                pacing = "RELAXED"
            });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var response = await _client.GetFromJsonAsync<UserPreferencesDto>("/api/users/me/preferences");
        Assert.Equal(2, response!.PreferredCurrencyId);
        Assert.Equal("JOD", response.PreferredCurrency);
        Assert.Equal("MILES", response.DistanceUnit);
        Assert.Equal("RELAXED", response.Pacing);

        var invalid = await _client.PutAsJsonAsync(
            "/api/users/me/preferences",
            new
            {
                preferredCurrencyId = 999999,
                distanceUnit = "LIGHTYEARS",
                pacing = "EXTREME"
            });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task TripMetadata_PatchPersistsWithoutChangingItinerary()
    {
        var token = await RegisterAndGetTokenAsync();
        var (tripId, version) = await CreateTripAsync(token);
        var places = await SeedPlacesAsync(1, 2);

        UseToken(token);
        var write = await _client.PostAsJsonAsync(
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
                            new { placeId = places[0], timeSlot = "MORNING", orderIndex = 0, estimatedCost = 0, isAiGenerated = true }
                        }
                    }
                }
            });
        write.EnsureSuccessStatusCode();

        var patch = await _client.PatchAsJsonAsync(
            $"/api/trips/{tripId}",
            new { title = "Jerusalem Weekend", coverImageUrl = "https://example.com/cover.jpg", expectedVersion = version + 1 });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var trip = await patch.Content.ReadFromJsonAsync<TripResponse>();
        Assert.Equal("Jerusalem Weekend", trip!.Title);
        Assert.Equal("https://example.com/cover.jpg", trip.CoverImageUrl);
        Assert.Single(trip.Itinerary!.Days);
        Assert.Single(trip.Itinerary.Days[0].Items);
    }

    [Fact]
    public async Task ItineraryItemPatch_ChangesOnlyRequestedItem()
    {
        var token = await RegisterAndGetTokenAsync();
        var (tripId, _) = await CreateTripAsync(token);
        var places = await SeedPlacesAsync(1, 2);
        UseToken(token);

        var write = await _client.PostAsJsonAsync(
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
                            new { placeId = places[0], timeSlot = "MORNING", orderIndex = 0, estimatedCost = 0, notes = "Keep me", isAiGenerated = true },
                            new { placeId = places[1], timeSlot = "AFTERNOON", orderIndex = 0, estimatedCost = 0, notes = "Change me", isAiGenerated = true }
                        }
                    }
                }
            });
        write.EnsureSuccessStatusCode();
        var itinerary = await write.Content.ReadFromJsonAsync<Triply.Api.Modules.Itinerary.Dtos.ItineraryResponse>();
        var targetId = itinerary!.Days[0].Items[1].Id;
        var untouchedId = itinerary.Days[0].Items[0].Id;

        var patch = await _client.PatchAsJsonAsync(
            $"/api/trips/{tripId}/itinerary/items/{targetId}",
            new { placeId = places[0], timeSlot = "EVENING", orderIndex = 0, notes = "Changed" });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<Triply.Api.Modules.Itinerary.Dtos.ItineraryItemResponse>();
        Assert.Equal(targetId, updated!.Id);
        Assert.Equal("Changed", updated.Notes);
        Assert.Equal("EVENING", updated.TimeSlot);

        var after = await _client.GetFromJsonAsync<Triply.Api.Modules.Itinerary.Dtos.ItineraryResponse>(
            $"/api/trips/{tripId}/itinerary");
        var untouched = after!.Days[0].Items.Single(x => x.Id == untouchedId);
        Assert.Equal("Keep me", untouched.Notes);
    }

    [Fact]
    public async Task PlaceDetails_ReturnsDatasetInformation()
    {
        var token = await RegisterAndGetTokenAsync();
        UseToken(token);
        var placeId = (await SeedPlacesAsync(1, 1))[0];

        var response = await _client.GetAsync($"/api/places/{placeId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PlaceDetailsDto>();
        Assert.Equal(placeId, body!.Id);
        Assert.NotNull(body.Description);
        Assert.Equal("Jerusalem", body.DestinationName);
        Assert.Empty(body.Images);
        Assert.Empty(body.OpeningHours);
    }

    [Fact]
    public async Task UserStats_ReturnsZeroForNewUserAndSavedPlaceCountForSavedTrip()
    {
        var token = await RegisterAndGetTokenAsync();
        UseToken(token);

        var initial = await _client.GetFromJsonAsync<UserStatsDto>("/api/users/me/stats");
        Assert.Equal(0, initial!.TotalTrips);
        Assert.Equal(0, initial.TotalSavedPlaces);
        Assert.Equal(0, initial.TotalCountries);

        var (tripId, _) = await CreateTripAsync(token);
        var places = await SeedPlacesAsync(1, 1);
        var write = await _client.PostAsJsonAsync(
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
                            new { placeId = places[0], timeSlot = "MORNING", orderIndex = 0, estimatedCost = 0, isAiGenerated = true }
                        }
                    }
                }
            });
        write.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tripToSave = await db.Trips.FirstAsync(x => x.Id == tripId);
            tripToSave.Status = "GENERATED";
            await db.SaveChangesAsync();
        }

        var save = await _client.PostAsync($"/api/trips/{tripId}/save", null);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var stats = await _client.GetFromJsonAsync<UserStatsDto>("/api/users/me/stats");
        Assert.Equal(1, stats!.TotalTrips);
        Assert.Equal(1, stats.TotalSavedPlaces);
        Assert.Equal(1, stats.TotalCountries);
    }

    private sealed class UserProfileDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = default!;
        public string? DisplayName { get; set; }
    }

    private sealed class UserPreferencesDto
    {
        public long? PreferredCurrencyId { get; set; }
        public string? PreferredCurrency { get; set; }
        public string DistanceUnit { get; set; } = default!;
        public string Pacing { get; set; } = default!;
    }

    private sealed class UserStatsDto
    {
        public int TotalTrips { get; set; }
        public int TotalSavedPlaces { get; set; }
        public int TotalCountries { get; set; }
    }

    private sealed class PlaceDetailsDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public string Category { get; set; } = default!;
        public long DestinationId { get; set; }
        public string DestinationName { get; set; } = default!;
        public string CountryName { get; set; } = default!;
        public decimal ReferencePrice { get; set; }
        public string Currency { get; set; } = default!;
        public List<string> Images { get; set; } = new();
        public List<object> OpeningHours { get; set; } = new();
    }
}
