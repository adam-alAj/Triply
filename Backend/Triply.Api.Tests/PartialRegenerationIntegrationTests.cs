using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration;
using Triply.Api.Modules.AIOrchestration.Dtos;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Tests;

public sealed class PartialRegenerationIntegrationTests
    : IClassFixture<AiTestWebApplicationFactory>
{
    private readonly AiTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PartialRegenerationIntegrationTests(AiTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail()
        => $"partial_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                UniqueEmail(),
                "P@ssw0rd123",
                "Partial Test"));

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"HTTP {(int)response.StatusCode} ({response.StatusCode})\nResponse body:\n{responseBody}");

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));

        return body.Token;
    }

    private async Task<(long accommodation, long restaurantA, long restaurantB, long transport)>
        SeedPlacesAsync(long destinationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];

        var accommodation = new Place
        {
            DestinationId = destinationId,
            PlaceCategoryId = 4,
            Name = $"Partial Hotel {suffix}",
            Description = "Partial regeneration test accommodation",
            ReferencePrice = 100m,
            CurrencyId = 1,
            CostCategoryId = 1,
            PriceUpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var restaurantA = new Place
        {
            DestinationId = destinationId,
            PlaceCategoryId = 2,
            Name = $"Partial Restaurant A {suffix}",
            Description = "Original restaurant",
            ReferencePrice = 25m,
            CurrencyId = 1,
            CostCategoryId = 3,
            PriceUpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var restaurantB = new Place
        {
            DestinationId = destinationId,
            PlaceCategoryId = 2,
            Name = $"Partial Restaurant B {suffix}",
            Description = "Replacement restaurant",
            ReferencePrice = 35m,
            CurrencyId = 1,
            CostCategoryId = 3,
            PriceUpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var transport = new Place
        {
            DestinationId = destinationId,
            PlaceCategoryId = 5,
            Name = $"Partial Transport {suffix}",
            Description = "Transport test place",
            ReferencePrice = 10m,
            CurrencyId = 1,
            CostCategoryId = 2,
            PriceUpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        db.Places.AddRange(
            accommodation,
            restaurantA,
            restaurantB,
            transport);

        await db.SaveChangesAsync();

        // The fake Gemini client runs inside the TestServer request pipeline.
        // Use shared test state instead of AsyncLocal so the request can resolve
        // the exact place names created by this test.
        PartialRegenerationTestPlaceNames.Set(
            accommodation.Name,
            restaurantA.Name,
            restaurantB.Name,
            transport.Name);

        return (
            accommodation.Id,
            restaurantA.Id,
            restaurantB.Id,
            transport.Id);
    }

    private async Task<TripResponse> CreateTripAsync(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                planningMode = "DESTINATION_FIRST",
                destinationId = 1,
                startDate = "2026-10-01",
                endDate = "2026-10-03",
                travelerCount = 2,
                budgetAmount = 1000,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1, 3 }
            });

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"HTTP {(int)response.StatusCode} ({response.StatusCode})\nResponse body:\n{responseBody}");

        var trip = await response.Content.ReadFromJsonAsync<TripResponse>();

        Assert.NotNull(trip);

        return trip;
    }

    private async Task<(Guid day1ItemId, Guid day2ItemId, Guid day3ItemId, int version)>
        SeedItineraryAsync(
            Guid tripId,
            (long accommodation, long restaurantA, long restaurantB, long transport) p)
    {
        // The normal itinerary endpoint increments Trip.Version.
        // This gives the partial-regeneration request a realistic
        // client-observed version.
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
                            new
                            {
                                placeId = p.accommodation,
                                timeSlot = "MORNING",
                                orderIndex = 0,
                                isAiGenerated = true,
                                notes = "Accommodation: 2 nights"
                            },
                            new
                            {
                                placeId = p.restaurantA,
                                timeSlot = "EVENING",
                                orderIndex = 1,
                                isAiGenerated = true,
                                notes = "Day 1 original"
                            },
                            new
                            {
                                placeId = p.transport,
                                timeSlot = "AFTERNOON",
                                orderIndex = 1,
                                isAiGenerated = true,
                                notes = "Day 1 transport"
                            }
                        }
                    },
                    new
                    {
                        dayNumber = 2,
                        date = "2026-10-02",
                        items = new[]
                        {
                            new
                            {
                                placeId = p.restaurantA,
                                timeSlot = "EVENING",
                                orderIndex = 1,
                                isAiGenerated = true,
                                notes = "Day 2 original"
                            }
                        }
                    },
                    new
                    {
                        dayNumber = 3,
                        date = "2026-10-03",
                        items = new[]
                        {
                            new
                            {
                                placeId = p.restaurantA,
                                timeSlot = "EVENING",
                                orderIndex = 1,
                                isAiGenerated = true,
                                notes = "Day 3 original"
                            }
                        }
                    }
                }
            });

        var writeBody = await write.Content.ReadAsStringAsync();

        Assert.True(
            write.IsSuccessStatusCode,
            $"HTTP {(int)write.StatusCode} ({write.StatusCode})\nResponse body:\n{writeBody}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var trip = await db.Trips.FirstAsync(x => x.Id == tripId);

        trip.Status = TripLifecycle.Generated;

        await db.SaveChangesAsync();

        var itinerary = await db.Itineraries
            .Include(x => x.Days)
                .ThenInclude(x => x.Items)
            .FirstAsync(x => x.TripId == tripId);

        return (
            itinerary.Days
                .Single(x => x.DayNumber == 1)
                .Items
                .Single(x => x.Notes == "Day 1 original")
                .Id,

            itinerary.Days
                .Single(x => x.DayNumber == 2)
                .Items
                .Single(x => x.Notes == "Day 2 original")
                .Id,

            itinerary.Days
                .Single(x => x.DayNumber == 3)
                .Items
                .Single(x => x.Notes == "Day 3 original")
                .Id,

            trip.Version);
    }

    [Fact]
    public async Task RegenerateDay_ReplacesOnlyTargetDay_AndIncrementsVersion()
    {
        var token = await RegisterAndGetTokenAsync();
        var places = await SeedPlacesAsync(1);
        var trip = await CreateTripAsync(token);
        var seeded = await SeedItineraryAsync(trip.Id, places);

        var response = await _client.PostAsJsonAsync(
            $"/api/trips/{trip.Id}/generate",
            new
            {
                scope = "DAY",
                dayNumber = 2,
                expectedVersion = seeded.version
            });

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"HTTP {(int)response.StatusCode} ({response.StatusCode})\nResponse body:\n{responseBody}");

        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);

        Assert.Equal(
            seeded.version + 1,
            body.GetProperty("tripVersion").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var persisted = await db.Itineraries
            .Include(x => x.Days)
                .ThenInclude(x => x.Items)
            .FirstAsync(x => x.TripId == trip.Id);

        var day1 = persisted.Days.Single(x => x.DayNumber == 1);
        var day2 = persisted.Days.Single(x => x.DayNumber == 2);
        var day3 = persisted.Days.Single(x => x.DayNumber == 3);

        Assert.Contains(
            day1.Items,
            x => x.Id == seeded.day1ItemId);

        Assert.Contains(
            day3.Items,
            x => x.Id == seeded.day3ItemId);

        Assert.DoesNotContain(
            day2.Items,
            x => x.Id == seeded.day2ItemId);

        Assert.Contains(
            day2.Items,
            x =>
                x.PlaceId == places.restaurantB &&
                x.IsAiGenerated &&
                x.Notes == "AI replacement day 2");

        Assert.Contains(
            day1.Items,
            x =>
                x.PlaceId == places.accommodation &&
                x.Notes!.StartsWith(
                    "Accommodation:",
                    StringComparison.OrdinalIgnoreCase));

        var tripAfter = await db.Trips
            .AsNoTracking()
            .SingleAsync(x => x.Id == trip.Id);

        Assert.Equal(
            seeded.version + 1,
            tripAfter.Version);
    }

    [Fact]
    public async Task RegenerateDay_WithStaleVersion_ReturnsConflict_AndDoesNotReplaceContent()
    {
        var token = await RegisterAndGetTokenAsync();
        var places = await SeedPlacesAsync(1);
        var trip = await CreateTripAsync(token);
        var seeded = await SeedItineraryAsync(trip.Id, places);

        // Simulate another legitimate concurrent writer by moving
        // the stored Trip.Version forward without changing the
        // client-observed version.
        using (var concurrentScope = _factory.Services.CreateScope())
        {
            var concurrentDb =
                concurrentScope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var concurrentTrip =
                await concurrentDb.Trips
                    .SingleAsync(x => x.Id == trip.Id);

            concurrentTrip.Title = "Changed before regeneration";
            concurrentTrip.Version++;
            concurrentTrip.UpdatedAt = DateTime.UtcNow;

            await concurrentDb.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/trips/{trip.Id}/generate",
            new
            {
                scope = "DAY",
                dayNumber = 2,
                expectedVersion = seeded.version
            });

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        Assert.False(
            string.IsNullOrWhiteSpace(responseBody));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var day2 = await db.ItineraryDays
            .Include(x => x.Items)
            .SingleAsync(
                x =>
                    x.Itinerary.TripId == trip.Id &&
                    x.DayNumber == 2);

        Assert.Contains(
            day2.Items,
            x => x.Id == seeded.day2ItemId);

        Assert.DoesNotContain(
            day2.Items,
            x => x.PlaceId == places.restaurantB);
    }

    [Fact]
    public async Task RegenerateItem_ReplacesOnlyTargetItem_AndIncrementsVersion()
    {
        var token = await RegisterAndGetTokenAsync();
        var places = await SeedPlacesAsync(1);
        var trip = await CreateTripAsync(token);
        var seeded = await SeedItineraryAsync(trip.Id, places);

        var response = await _client.PostAsJsonAsync(
            $"/api/trips/{trip.Id}/generate",
            new
            {
                scope = "ITEM",
                itemId = seeded.day2ItemId,
                expectedVersion = seeded.version
            });

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"HTTP {(int)response.StatusCode} ({response.StatusCode})\nResponse body:\n{responseBody}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var itinerary = await db.Itineraries
            .Include(x => x.Days)
                .ThenInclude(x => x.Items)
            .FirstAsync(x => x.TripId == trip.Id);

        var day1 = itinerary.Days.Single(x => x.DayNumber == 1);
        var day2 = itinerary.Days.Single(x => x.DayNumber == 2);
        var day3 = itinerary.Days.Single(x => x.DayNumber == 3);

        Assert.Contains(
            day1.Items,
            x => x.Id == seeded.day1ItemId);

        Assert.Contains(
            day3.Items,
            x => x.Id == seeded.day3ItemId);

        Assert.DoesNotContain(
            day2.Items,
            x => x.Id == seeded.day2ItemId);

        Assert.Contains(
            day2.Items,
            x =>
                x.PlaceId == places.restaurantB &&
                x.IsAiGenerated &&
                x.Notes == "AI replacement day 2");

        var tripAfter = await db.Trips
            .AsNoTracking()
            .SingleAsync(x => x.Id == trip.Id);

        Assert.Equal(
            seeded.version + 1,
            tripAfter.Version);
    }

    [Fact]
    public async Task DirectItemEdit_FlipsOnlyEditedItemToUserGenerated()
    {
        var token = await RegisterAndGetTokenAsync();
        var places = await SeedPlacesAsync(1);
        var trip = await CreateTripAsync(token);
        var seeded = await SeedItineraryAsync(trip.Id, places);

        var response = await _client.PatchAsJsonAsync(
            $"/api/trips/{trip.Id}/itinerary/items/{seeded.day2ItemId}",
            new
            {
                placeId = places.restaurantB,
                timeSlot = "EVENING",
                orderIndex = 1,
                notes = "User edited this item"
            });

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"HTTP {(int)response.StatusCode} ({response.StatusCode})\nResponse body:\n{responseBody}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var itinerary = await db.Itineraries
            .Include(x => x.Days)
                .ThenInclude(x => x.Items)
            .FirstAsync(x => x.TripId == trip.Id);

        var edited = itinerary.Days
            .Single(x => x.DayNumber == 2)
            .Items
            .Single();

        Assert.Equal(
            places.restaurantB,
            edited.PlaceId);

        Assert.False(edited.IsAiGenerated);

        Assert.Equal(
            "User edited this item",
            edited.Notes);

        var untouchedAiItems = itinerary.Days
            .Where(x => x.DayNumber != 2)
            .SelectMany(x => x.Items)
            .Where(
                x =>
                    x.Id != edited.Id &&
                    x.PlaceId != places.accommodation);

        Assert.All(
            untouchedAiItems,
            item => Assert.True(item.IsAiGenerated));
    }

    [Fact]
    public async Task PartialRegeneration_RequiresExpectedVersion()
    {
        var token = await RegisterAndGetTokenAsync();
        var places = await SeedPlacesAsync(1);
        var trip = await CreateTripAsync(token);

        await SeedItineraryAsync(trip.Id, places);

        var response = await _client.PostAsJsonAsync(
            $"/api/trips/{trip.Id}/generate",
            new
            {
                scope = "DAY",
                dayNumber = 2
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
}

public sealed class AiTestWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGeminiClient>();
            services.AddSingleton<IGeminiClient, FakeGeminiClient>();
        });
    }
}

public sealed class FakeGeminiClient : IGeminiClient
{
    public Task<string> GenerateJsonAsync(
        string prompt,
        CancellationToken cancellationToken = default)
        => Task.FromResult(BuildResponse());

    public Task<string> GenerateJsonWithSchemaAsync(
        string prompt,
        string systemInstruction,
        JsonDocument schema,
        CancellationToken cancellationToken = default)
        => Task.FromResult(BuildResponse());

    private static string BuildResponse()
    {
        return JsonSerializer.Serialize(new
        {
            planning_mode = "DESTINATION_FIRST",

            destination_options = new[]
            {
                new
                {
                    destination_name = "Jerusalem",

                    accommodation = new
                    {
                        place_name = CurrentTestPlace("hotel"),
                        nights = 2
                    },

                    days = new[]
                    {
                        new
                        {
                            day_number = 1,
                            date = "2026-10-01",

                            items = new[]
                            {
                                new
                                {
                                    time_slot = "EVENING",
                                    order_index = 1,
                                    place_name = CurrentTestPlace("restaurantA"),
                                    notes = "AI day 1"
                                },
                                new
                                {
                                    time_slot = "AFTERNOON",
                                    order_index = 2,
                                    place_name = CurrentTestPlace("transport"),
                                    notes = "AI transport"
                                }
                            }
                        },

                        new
                        {
                            day_number = 2,
                            date = "2026-10-02",

                            items = new[]
                            {
                                new
                                {
                                    time_slot = "EVENING",
                                    order_index = 1,
                                    place_name = CurrentTestPlace("restaurantB"),
                                    notes = "AI replacement day 2"
                                },
                                new
                                {
                                    time_slot = "AFTERNOON",
                                    order_index = 2,
                                    place_name = CurrentTestPlace("transport"),
                                    notes = "AI transport"
                                }
                            }
                        },

                        new
                        {
                            day_number = 3,
                            date = "2026-10-03",

                            items = new[]
                            {
                                new
                                {
                                    time_slot = "EVENING",
                                    order_index = 1,
                                    place_name = CurrentTestPlace("restaurantA"),
                                    notes = "AI day 3"
                                },
                                new
                                {
                                    time_slot = "AFTERNOON",
                                    order_index = 2,
                                    place_name = CurrentTestPlace("transport"),
                                    notes = "AI transport"
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    private static string CurrentTestPlace(string kind)
        => PartialRegenerationTestPlaceNames.Get(kind);
}

internal static class PartialRegenerationTestPlaceNames
{
    private static readonly object SyncRoot = new();

    private static Dictionary<string, string>? Current;

    public static void Set(
        string hotel,
        string restaurantA,
        string restaurantB,
        string transport)
    {
        lock (SyncRoot)
        {
            Current = new Dictionary<string, string>
            {
                ["hotel"] = hotel,
                ["restaurantA"] = restaurantA,
                ["restaurantB"] = restaurantB,
                ["transport"] = transport
            };
        }
    }

    public static string Get(string key)
    {
        lock (SyncRoot)
        {
            if (Current is null ||
                !Current.TryGetValue(key, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    "Test place names were not initialized.");
            }

            return value;
        }
    }
}