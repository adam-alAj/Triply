using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip.Dtos;
using Xunit;

namespace Triply.Api.Tests;

public class TripIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TripIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
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

    private async Task<long> GetOrCreateTestPlaceAsync(long destinationId)
    {
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

      var existingPlace = await db.Places
    .Where(x =>
        x.DestinationId == destinationId &&
        x.IsActive &&
        x.Name.StartsWith("Integration Test Place"))
    .OrderBy(x => x.Id)
    .FirstOrDefaultAsync();

        if (existingPlace is not null)
            return existingPlace.Id;

        var destinationExists = await db.Destinations
            .AnyAsync(x => x.Id == destinationId);

        Assert.True(
            destinationExists,
            $"Destination {destinationId} does not exist.");

        var currencyExists = await db.Currencies
            .AnyAsync(x => x.Id == 1);

        Assert.True(
            currencyExists,
            "Currency 1 does not exist.");

        var placeCategoryExists = await db.PlaceCategories
            .AnyAsync(x => x.Id == 1);

        Assert.True(
            placeCategoryExists,
            "PlaceCategory 1 does not exist.");

        var costCategoryExists = await db.CostCategories
            .AnyAsync(x => x.Id == 1);

        Assert.True(
            costCategoryExists,
            "CostCategory 1 does not exist.");

        var place = new Place
        {
            DestinationId = destinationId,
            PlaceCategoryId = 1,
            Name = $"Integration Test Place {Guid.NewGuid():N}",
            Description = "Place created for integration tests.",
            ReferencePrice = 25m,
            CurrencyId = 1,
            CostCategoryId = 1,
            PriceUpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        db.Places.Add(place);
        await db.SaveChangesAsync();

        return place.Id;
    }

    [Fact]
    public async Task BudgetFirst_UserCanSelectSuggestedDestinationBeforeGeneration()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                planningMode = "BUDGET_FIRST",
                destinationId = (long?)null,
                startDate = "2026-10-01",
                endDate = "2026-10-03",
                travelerCount = 2,
                budgetAmount = 1000,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1, 3 }
            });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TripResponse>();
        Assert.NotNull(created);
        Assert.Null(created!.DestinationId);

        var selectResponse = await _client.PatchAsJsonAsync(
            $"/api/trips/{created.Id}/destination",
            new
            {
                destinationId = 2,
                expectedVersion = created.Version
            });

        Assert.Equal(HttpStatusCode.OK, selectResponse.StatusCode);
        var selected = await selectResponse.Content.ReadFromJsonAsync<DestinationSelectionResponse>();
        Assert.NotNull(selected);
        Assert.Equal(2, selected!.DestinationId);
        Assert.Equal("Amman", selected.DestinationName);
        Assert.Equal(created.Version + 1, selected.Version);
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
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
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
        interestCategoryIds = new[] { 3, 4 },
        expectedVersion = created.Version
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
        Assert.Equal(
            new long[] { 3, 4 },
            updated.InterestCategoryIds);

            
    }
    [Fact]
public async Task UpdateTrip_StaleVersion_ReturnsConflict()
{
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

    Assert.NotNull(created);

    var expectedVersion = created!.Version;

    // First write succeeds and increments the version.
    var firstUpdate = await _client.PutAsJsonAsync(
        $"/api/trips/{created.Id}",
        new
        {
            destinationId = 1,
            startDate = "2026-11-01",
            endDate = "2026-11-07",
            travelerCount = 3,
            budgetAmount = 1500,
            budgetCurrencyId = 1,
            interestCategoryIds = new[] { 1, 2 },
            expectedVersion
        });

    Assert.Equal(
        HttpStatusCode.OK,
        firstUpdate.StatusCode);

    // Second write still uses the old version.
    var secondUpdate = await _client.PutAsJsonAsync(
        $"/api/trips/{created.Id}",
        new
        {
            destinationId = 1,
            startDate = "2026-12-01",
            endDate = "2026-12-07",
            travelerCount = 4,
            budgetAmount = 2000,
            budgetCurrencyId = 1,
            interestCategoryIds = new[] { 1, 2 },
            expectedVersion
        });

    Assert.Equal(
        HttpStatusCode.Conflict,
        secondUpdate.StatusCode);
    }

    [Fact]
    public async Task SaveThenRetrieve_ReturnsLastConfirmedTripState()
    {
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
                budgetAmount = 1500m,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1, 2 }
            });

        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content
            .ReadFromJsonAsync<TripResponse>();

        Assert.NotNull(created);

using (var scope = _factory.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var tripToGenerate = await db.Trips
        .FirstAsync(x => x.Id == created!.Id);

    tripToGenerate.Status = "GENERATED";
    await db.SaveChangesAsync();
}
        var placeId = await GetOrCreateTestPlaceAsync(1);

        var itineraryResponse = await _client.PostAsJsonAsync(
            $"/api/trips/{created.Id}/itinerary",
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
                            new
                            {
                                placeId,
                                timeSlot = "MORNING",
                                orderIndex = 0,
                                estimatedCost = 25m,
                                notes = "Confirmed breakfast",
                                isAiGenerated = false
                            }
                        }
                    }
                }
            });

        itineraryResponse.EnsureSuccessStatusCode();

        var saveResponse = await _client.PostAsync(
            $"/api/trips/{created.Id}/save",
            null);

        saveResponse.EnsureSuccessStatusCode();

        var saved = await saveResponse.Content
            .ReadFromJsonAsync<TripResponse>();

        var retrieveResponse = await _client.GetAsync(
            $"/api/trips/{created.Id}");

        retrieveResponse.EnsureSuccessStatusCode();

        var retrieved = await retrieveResponse.Content
            .ReadFromJsonAsync<TripResponse>();

        Assert.NotNull(saved);
        Assert.NotNull(retrieved);

        Assert.Equal("SAVED", saved!.Status);
        Assert.Equal(saved.Status, retrieved!.Status);
        Assert.Equal(saved.Version, retrieved.Version);
        Assert.Equal(saved.DestinationId, retrieved.DestinationId);
        Assert.Equal(saved.StartDate, retrieved.StartDate);
        Assert.Equal(saved.EndDate, retrieved.EndDate);
        Assert.Equal(saved.TravelerCount, retrieved.TravelerCount);
        Assert.Equal(saved.BudgetAmount, retrieved.BudgetAmount);
        Assert.Equal(saved.BudgetCurrencyId, retrieved.BudgetCurrencyId);

        Assert.Equal(
            new long[] { 1, 2 },
            retrieved.InterestCategoryIds);

        Assert.NotNull(retrieved.Itinerary);
        Assert.Single(retrieved.Itinerary!.Days);
        Assert.Single(retrieved.Itinerary.Days[0].Items);

        var item = retrieved.Itinerary.Days[0].Items[0];

        Assert.Equal(placeId, item.PlaceId);
        Assert.Equal(25m, item.EstimatedCost);
        Assert.Equal(
            "Confirmed breakfast",
            item.Notes);

        Assert.False(item.IsAiGenerated);
    }

    [Fact]
    public async Task TripStatus_FollowsGenerateGenerateAndSaveLifecycle()
    {
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
                endDate = "2026-10-03",
                travelerCount = 1,
                budgetAmount = 500,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1 }
            });

        createResponse.EnsureSuccessStatusCode();

        var trip = await createResponse.Content
            .ReadFromJsonAsync<TripResponse>();

        Assert.NotNull(trip);
        Assert.Equal("DRAFT", trip!.Status);

       using (var scope = _factory.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var tripToGenerate = await db.Trips
        .FirstAsync(x => x.Id == trip.Id);

    tripToGenerate.Status = "GENERATING";

    await db.SaveChangesAsync();
}
        var placeId = await GetOrCreateTestPlaceAsync(1);

        var write = await _client.PostAsJsonAsync(
            $"/api/trips/{trip.Id}/itinerary",
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
                            new
                            {
                                placeId,
                                timeSlot = "MORNING",
                                orderIndex = 0,
                                estimatedCost = 10m,
                                isAiGenerated = true
                            }
                        }
                    }
                }
            });

        write.EnsureSuccessStatusCode();

        var generatedTrip =
            await _client.GetFromJsonAsync<TripResponse>(
                $"/api/trips/{trip.Id}");

        Assert.NotNull(generatedTrip);

        Assert.Equal(
            "GENERATED",
            generatedTrip!.Status);

        var save = await _client.PostAsync(
            $"/api/trips/{trip.Id}/save",
            null);

        save.EnsureSuccessStatusCode();

        var savedTrip =
            await _client.GetFromJsonAsync<TripResponse>(
                $"/api/trips/{trip.Id}");

        Assert.NotNull(savedTrip);

        Assert.Equal(
            "SAVED",
            savedTrip!.Status);
    }
    private sealed class DestinationSelectionResponse
    {
        public Guid TripId { get; set; }
        public long DestinationId { get; set; }
        public string DestinationName { get; set; } = default!;
        public int Version { get; set; }
    }

}
