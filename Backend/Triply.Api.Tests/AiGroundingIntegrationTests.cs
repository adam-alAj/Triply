using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Tests;

/// <summary>
/// Runs the real AI generation pipeline (controller → orchestration → validator →
/// persistence) against a scripted Gemini client, so the FR-AI-002 guarantee is
/// proven at the HTTP layer instead of only on the validator in isolation.
/// </summary>
public sealed class AiGroundingTestFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGeminiClient>();
            services.AddSingleton<IGeminiClient, ScriptedGeminiClient>();
        });

        // This class seeds its own destinations/places — keep it off the shared DB.
        IsolatedTestDb.Configure(builder, "AiGround");

        // BUDGET_FIRST refuses to run unless every active place for the destination
        // carries a budget_tier from the curated Extra_AI_Context.csv, which synthetic
        // test places can never satisfy. Point the reader at a temporary file covering
        // this class's synthetic places so the budget paths are reachable. Only
        // BUDGET_FIRST reads it, so the DESTINATION_FIRST tests are unaffected.
        var contextPath = Path.Combine(
            Path.GetTempPath(), $"triply-ai-context-{Guid.NewGuid():N}.csv");

        File.WriteAllText(
            contextPath,
            "place_name,budget_tier\n" +
            $"{BudgetFirstTestPlaces.Hotel},BUDGET\n" +
            $"{BudgetFirstTestPlaces.Restaurant},BUDGET\n" +
            $"{BudgetFirstTestPlaces.Transport},BUDGET\n");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:ExtraAiContextPath"] = contextPath,
                // Keep retry tests fast and explicit: total attempts = MaxRetries + 1.
                ["Gemini:MaxRetries"] = "1"
            }));
    }
}

/// <summary>
/// Fixed place names for the BUDGET_FIRST tests. They must be stable because the
/// factory writes them into the temporary budget-tier CSV before any test runs,
/// while the places themselves are seeded per test.
/// </summary>
public static class BudgetFirstTestPlaces
{
    public const string Suffix = " Budget";
    public const string Hotel = "Ground Hotel Budget";
    public const string Restaurant = "Ground Restaurant Budget";
    public const string Transport = "Ground Transport Budget";
}

/// <summary>
/// Returns the raw JSON the current test configured, and records the
/// <c>destination_options.maxItems</c> of the last schema it received so a test
/// can prove the per-mode schema (Contract §5 step 0) through the real path.
/// </summary>
public sealed class ScriptedGeminiClient : IGeminiClient
{
    private static readonly object SyncRoot = new();

    private static string? _response;
    private static Queue<object>? _outcomes;
    private static int? _lastDestinationOptionsMaxItems;
    private static int _callCount;

    public static void SetResponse(string response)
    {
        lock (SyncRoot)
        {
            _response = response;
            _outcomes = null;
            _callCount = 0;
        }
    }

    public static void SetOutcomes(params object[] outcomes)
    {
        lock (SyncRoot)
        {
            _response = null;
            _outcomes = new Queue<object>(outcomes);
            _callCount = 0;
        }
    }

    public static int? LastDestinationOptionsMaxItems
    {
        get { lock (SyncRoot) return _lastDestinationOptionsMaxItems; }
    }

    public static int CallCount
    {
        get { lock (SyncRoot) return _callCount; }
    }

    public Task<string> GenerateJsonAsync(
        string prompt,
        CancellationToken cancellationToken = default)
        => Task.FromResult(CurrentResponse());

    public Task<string> GenerateJsonWithSchemaAsync(
        string prompt,
        string systemInstruction,
        JsonDocument schema,
        CancellationToken cancellationToken = default)
    {
        lock (SyncRoot)
        {
            _lastDestinationOptionsMaxItems = null;

            if (schema.RootElement.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty("destination_options", out var destinationOptions) &&
                destinationOptions.TryGetProperty("maxItems", out var maxItems) &&
                maxItems.TryGetInt32(out var value))
            {
                _lastDestinationOptionsMaxItems = value;
            }
        }

        return Task.FromResult(CurrentResponse());
    }

    private static string CurrentResponse()
    {
        lock (SyncRoot)
        {
            _callCount++;

            if (_outcomes is { Count: > 0 })
            {
                var outcome = _outcomes.Dequeue();
                if (outcome is Exception exception)
                    throw exception;
                return (string)outcome;
            }

            return _response ?? throw new InvalidOperationException(
                "ScriptedGeminiClient response was not initialized for this test.");
        }
    }
}

public sealed class AiGroundingIntegrationTests : IClassFixture<AiGroundingTestFactory>
{
    private readonly AiGroundingTestFactory _factory;
    private readonly HttpClient _client;

    public AiGroundingIntegrationTests(AiGroundingTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"aiground_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(UniqueEmail(), "P@ssw0rd123", "AI Grounding Test"));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Token));
        return body!.Token;
    }

    private void UseToken(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<(long Id, string Name)> SeedDestinationAsync(bool isSupported)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var countryId = await db.Countries.Select(c => c.Id).FirstAsync();
        var name = $"GroundDest{Guid.NewGuid():N}";

        var destination = new Destination
        {
            CountryId = countryId,
            Name = name,
            Description = "AI grounding test destination",
            IsSupported = isSupported
        };

        db.Destinations.Add(destination);
        await db.SaveChangesAsync();

        return (destination.Id, destination.Name);
    }

    private async Task<(string Hotel, string Restaurant, string Transport)> SeedPlacesAsync(
        long destinationId,
        string? suffixOverride = null,
        decimal hotelPrice = 100m,
        decimal restaurantPrice = 20m,
        decimal transportPrice = 10m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var categories = await db.PlaceCategories
            .ToDictionaryAsync(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase);
        var costCategoryId = await db.CostCategories.Select(c => c.Id).FirstAsync();
        var currencyId = await db.Currencies
            .Where(c => c.IsoCode == "USD")
            .Select(c => c.Id)
            .FirstAsync();

        var suffix = suffixOverride ?? $" {Guid.NewGuid().ToString("N")[..8]}";
        var hotel = $"Ground Hotel{suffix}";
        var restaurant = $"Ground Restaurant{suffix}";
        var transport = $"Ground Transport{suffix}";

        db.Places.AddRange(
            new Place
            {
                DestinationId = destinationId,
                PlaceCategoryId = categories["ACCOMMODATION"],
                Name = hotel,
                ReferencePrice = hotelPrice,
                CurrencyId = currencyId,
                CostCategoryId = costCategoryId,
                IsActive = true
            },
            new Place
            {
                DestinationId = destinationId,
                PlaceCategoryId = categories["RESTAURANT"],
                Name = restaurant,
                ReferencePrice = restaurantPrice,
                CurrencyId = currencyId,
                CostCategoryId = costCategoryId,
                IsActive = true
            },
            new Place
            {
                DestinationId = destinationId,
                PlaceCategoryId = categories["TRANSPORT"],
                Name = transport,
                ReferencePrice = transportPrice,
                CurrencyId = currencyId,
                CostCategoryId = costCategoryId,
                IsActive = true
            });

        await db.SaveChangesAsync();

        return (hotel, restaurant, transport);
    }

    /// <summary>
    /// Builds a BUDGET_FIRST payload with several distinct destination options — the
    /// shape the mode exists for: propose destinations the budget can afford.
    /// </summary>
    private static string BuildMultiOptionResponse(
        string planningMode,
        params (string Destination, string Hotel, string Restaurant, string Transport)[] options)
    {
        return JsonSerializer.Serialize(new
        {
            planning_mode = planningMode,
            destination_options = options.Select(option => new
            {
                destination_name = option.Destination,
                accommodation = new { place_name = option.Hotel, nights = 2 },
                days = new object[]
                {
                    new
                    {
                        day_number = 1,
                        date = "2026-10-01",
                        items = new object[]
                        {
                            new { time_slot = "MORNING", order_index = 1, place_name = option.Restaurant, notes = (string?)null },
                            new { time_slot = "AFTERNOON", order_index = 1, place_name = option.Transport, notes = (string?)null }
                        }
                    },
                    new
                    {
                        day_number = 2,
                        date = "2026-10-02",
                        items = new object[]
                        {
                            new { time_slot = "EVENING", order_index = 1, place_name = option.Restaurant, notes = (string?)null }
                        }
                    }
                }
            }).ToArray()
        });
    }

    private static string BuildValidResponse(
        string planningMode,
        string destinationName,
        string hotel,
        string restaurant,
        string transport)
    {
        // The validator rejects a payload whose planning_mode disagrees with the trip,
        // so the mode has to be part of the scripted response.
        return JsonSerializer.Serialize(new
        {
            planning_mode = planningMode,
            destination_options = new object[]
            {
                new
                {
                    destination_name = destinationName,
                    accommodation = new { place_name = hotel, nights = 2 },
                    days = new object[]
                    {
                        new
                        {
                            day_number = 1,
                            date = "2026-10-01",
                            items = new object[]
                            {
                                new { time_slot = "MORNING", order_index = 1, place_name = restaurant, notes = (string?)null },
                                new { time_slot = "AFTERNOON", order_index = 1, place_name = transport, notes = (string?)null }
                            }
                        },
                        new
                        {
                            day_number = 2,
                            date = "2026-10-02",
                            items = new object[]
                            {
                                new { time_slot = "EVENING", order_index = 1, place_name = restaurant, notes = (string?)null }
                            }
                        }
                    }
                }
            }
        });
    }

    private async Task<long> GetUsdCurrencyIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Currencies
            .Where(c => c.IsoCode == "USD")
            .Select(c => c.Id)
            .FirstAsync();
    }

    private async Task<Guid> CreateTripAsync(
        string token,
        string planningMode,
        long? destinationId = null,
        decimal? budgetAmount = null,
        long? budgetCurrencyId = null)
    {
        UseToken(token);

        var response = await _client.PostAsJsonAsync("/api/trips", new
        {
            planningMode,
            destinationId,
            startDate = "2026-10-01",
            endDate = "2026-10-02",
            travelerCount = 2,
            budgetAmount,
            budgetCurrencyId
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"HTTP {(int)response.StatusCode} ({response.StatusCode})\nResponse body:\n{body}");

        var trip = await response.Content.ReadFromJsonAsync<TripResponse>();
        Assert.NotNull(trip);
        return trip!.Id;
    }

    private Task<Guid> CreateDestinationFirstTripAsync(string token, long destinationId)
        => CreateTripAsync(token, "DESTINATION_FIRST", destinationId);

    private async Task<HttpResponseMessage> GenerateAsync(string token, Guid tripId)
    {
        UseToken(token);
        return await _client.PostAsJsonAsync($"/api/trips/{tripId}/generate", new { scope = "FULL" });
    }

    private async Task AssertFailedValidationAsync(Guid tripId, string expectedErrorFragment)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var generations = await db.AIGenerations.AsNoTracking()
            .Where(g => g.TripId == tripId)
            .ToListAsync();

        Assert.NotEmpty(generations);
        Assert.All(generations, g => Assert.Equal("FAILED_VALIDATION", g.Status));
        Assert.Contains(
            generations,
            g => g.ValidationErrors != null &&
                 g.ValidationErrors.Contains(expectedErrorFragment, StringComparison.OrdinalIgnoreCase));

        // No invalid itinerary is ever persisted as a generated trip.
        Assert.False(await db.Itineraries.AsNoTracking().AnyAsync(i => i.TripId == tripId));

        // Trip is left retry-able from DRAFT.
        var trip = await db.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(TripLifecycle.Draft, trip.Status);
    }

    [Fact]
    public async Task Generate_ValidGroundedPlan_SucceedsAndUsesModeSpecificMaxItems()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, destinationName) = await SeedDestinationAsync(isSupported: true);
        var places = await SeedPlacesAsync(destinationId);

        ScriptedGeminiClient.SetResponse(BuildValidResponse(
            "DESTINATION_FIRST", destinationName, places.Hotel, places.Restaurant, places.Transport));

        var tripId = await CreateDestinationFirstTripAsync(token, destinationId);
        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}\n{body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.True(await db.Itineraries.AsNoTracking().AnyAsync(i => i.TripId == tripId));
        var generation = await db.AIGenerations.AsNoTracking().FirstAsync(g => g.TripId == tripId);
        Assert.Equal("SUCCEEDED", generation.Status);

        var trip = await db.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(TripLifecycle.Generated, trip.Status);

        // Contract §5 step 0: DESTINATION_FIRST must ask Gemini for exactly 1 option.
        Assert.Equal(1, ScriptedGeminiClient.LastDestinationOptionsMaxItems.GetValueOrDefault(-1));
    }

    [Fact]
    public async Task Generate_InventedPlaceName_Returns422_AndPersistsNothing()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, destinationName) = await SeedDestinationAsync(isSupported: true);
        var places = await SeedPlacesAsync(destinationId);

        var invented = $"Invented Place {Guid.NewGuid():N}";
        ScriptedGeminiClient.SetResponse(
            BuildValidResponse(
                    "DESTINATION_FIRST", destinationName, places.Hotel, places.Restaurant, places.Transport)
                .Replace(places.Restaurant, invented));

        var tripId = await CreateDestinationFirstTripAsync(token, destinationId);
        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(invented, body, StringComparison.OrdinalIgnoreCase);

        await AssertFailedValidationAsync(tripId, invented);
    }

    [Fact]
    public async Task CreateTrip_RealButUnsupportedDestination_Returns400_BeforeGeneration()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, _) = await SeedDestinationAsync(isSupported: false);

        UseToken(token);

        var response = await _client.PostAsJsonAsync("/api/trips", new
        {
            planningMode = "DESTINATION_FIRST",
            destinationId,
            startDate = "2026-10-01",
            endDate = "2026-10-02",
            travelerCount = 2
        });
        var body = await response.Content.ReadAsStringAsync();

        // Rejected specifically because the destination is not IsSupported — not
        // merely because it is unknown.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not supported", body, StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(await db.Trips.AsNoTracking().AnyAsync(t => t.DestinationId == destinationId));
    }

    [Fact]
    public async Task Generate_OutputWithUnknownField_IsRejected()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, destinationName) = await SeedDestinationAsync(isSupported: true);
        var places = await SeedPlacesAsync(destinationId);

        // additionalProperties:false — an unmapped member must not be silently ignored.
        var payload = BuildValidResponse(
                "DESTINATION_FIRST", destinationName, places.Hotel, places.Restaurant, places.Transport)
            .Replace(
                "\"planning_mode\":\"DESTINATION_FIRST\"",
                "\"planning_mode\":\"DESTINATION_FIRST\",\"unexpected\":true");

        ScriptedGeminiClient.SetResponse(payload);

        var tripId = await CreateDestinationFirstTripAsync(token, destinationId);
        var response = await GenerateAsync(token, tripId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await AssertFailedValidationAsync(tripId, "not valid JSON");
    }

    [Fact]
    public async Task Generate_NonJsonOutput_Returns422_AndPersistsNothing()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, _) = await SeedDestinationAsync(isSupported: true);

        // Places must exist before the request reaches the parse step: with no
        // curated places for the destination the orchestrator returns its own
        // "No active places are curated yet" 422 and never calls Gemini at all,
        // which would make this test pass for the wrong reason.
        await SeedPlacesAsync(destinationId);

        ScriptedGeminiClient.SetResponse("this is not json");

        var tripId = await CreateDestinationFirstTripAsync(token, destinationId);
        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.UnprocessableEntity,
            $"HTTP {(int)response.StatusCode} ({response.StatusCode})\nResponse body:\n{body}");

        await AssertFailedValidationAsync(tripId, "not valid JSON");
    }

    // --- V-002 §5.3 budget policy: rules-as-written ---

    [Fact]
    public async Task Generate_InvalidJsonThenValidRetry_SucceedsOnSecondAttempt()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, destinationName) = await SeedDestinationAsync(isSupported: true);
        var places = await SeedPlacesAsync(destinationId);

        ScriptedGeminiClient.SetOutcomes(
            "this is not json",
            BuildValidResponse(
                "DESTINATION_FIRST", destinationName, places.Hotel, places.Restaurant, places.Transport));

        var tripId = await CreateDestinationFirstTripAsync(token, destinationId);
        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}\n{body}");
        Assert.Equal(2, ScriptedGeminiClient.CallCount);

        var payload = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(2, payload.GetProperty("attemptsUsed").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var generations = await db.AIGenerations.AsNoTracking()
            .Where(g => g.TripId == tripId)
            .OrderBy(g => g.AttemptNumber)
            .ToListAsync();

        Assert.Equal(2, generations.Count);
        Assert.Equal("FAILED_VALIDATION", generations[0].Status);
        Assert.Equal("SUCCEEDED", generations[1].Status);
        Assert.True(await db.Itineraries.AsNoTracking().AnyAsync(i => i.TripId == tripId));

        var trip = await db.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(TripLifecycle.Generated, trip.Status);
    }

    [Fact]
    public async Task Generate_InvalidJsonExhaustsRetries_Returns422AndPersistsNoItinerary()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, _) = await SeedDestinationAsync(isSupported: true);
        await SeedPlacesAsync(destinationId);

        ScriptedGeminiClient.SetResponse("this is not json");

        var tripId = await CreateDestinationFirstTripAsync(token, destinationId);
        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("not valid JSON", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, ScriptedGeminiClient.CallCount);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var generations = await db.AIGenerations.AsNoTracking()
            .Where(g => g.TripId == tripId)
            .OrderBy(g => g.AttemptNumber)
            .ToListAsync();

        Assert.Equal(2, generations.Count);
        Assert.All(generations, g => Assert.Equal("FAILED_VALIDATION", g.Status));
        Assert.False(await db.Itineraries.AsNoTracking().AnyAsync(i => i.TripId == tripId));

        var trip = await db.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(TripLifecycle.Draft, trip.Status);
    }

    [Fact]
    public async Task Generate_Gemini5xxThenValidRetry_SucceedsWithoutPersistingFailedAttempt()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, destinationName) = await SeedDestinationAsync(isSupported: true);
        var places = await SeedPlacesAsync(destinationId);

        ScriptedGeminiClient.SetOutcomes(
            new GeminiApiException("Gemini API returned 503 ServiceUnavailable.", "{\"error\":\"overloaded\"}"),
            BuildValidResponse(
                "DESTINATION_FIRST", destinationName, places.Hotel, places.Restaurant, places.Transport));

        var tripId = await CreateDestinationFirstTripAsync(token, destinationId);
        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}\n{body}");
        Assert.Equal(2, ScriptedGeminiClient.CallCount);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var generations = await db.AIGenerations.AsNoTracking()
            .Where(g => g.TripId == tripId)
            .OrderBy(g => g.AttemptNumber)
            .ToListAsync();

        Assert.Equal(2, generations.Count);
        Assert.Equal("FAILED_ERROR", generations[0].Status);
        Assert.Equal("SUCCEEDED", generations[1].Status);
        Assert.True(await db.Itineraries.AsNoTracking().AnyAsync(i => i.TripId == tripId));
    }

    [Fact]
    public async Task Generate_GeminiTimeoutExhaustsRetries_ReturnsBadGatewayAndLeavesTripRetryable()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, _) = await SeedDestinationAsync(isSupported: true);
        await SeedPlacesAsync(destinationId);

        ScriptedGeminiClient.SetOutcomes(
            new GeminiApiException("Gemini API call timed out.", string.Empty),
            new GeminiApiException("Gemini API call timed out.", string.Empty));

        var tripId = await CreateDestinationFirstTripAsync(token, destinationId);
        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("upstream", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, ScriptedGeminiClient.CallCount);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var generations = await db.AIGenerations.AsNoTracking()
            .Where(g => g.TripId == tripId)
            .OrderBy(g => g.AttemptNumber)
            .ToListAsync();

        Assert.Equal(2, generations.Count);
        Assert.All(generations, g => Assert.Equal("FAILED_ERROR", g.Status));
        Assert.False(await db.Itineraries.AsNoTracking().AnyAsync(i => i.TripId == tripId));

        var trip = await db.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(TripLifecycle.Draft, trip.Status);
    }

    [Fact]
    public async Task Generate_DestinationFirstOverBudget_FlagsInsteadOfFailing()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, destinationName) = await SeedDestinationAsync(isSupported: true);
        var places = await SeedPlacesAsync(destinationId);
        var currencyId = await GetUsdCurrencyIdAsync();

        ScriptedGeminiClient.SetResponse(BuildValidResponse(
            "DESTINATION_FIRST", destinationName, places.Hotel, places.Restaurant, places.Transport));

        // Deterministic plan cost = 100 (hotel) x 2 nights + 20 + 10 + 20 = 250.
        var tripId = await CreateTripAsync(
            token, "DESTINATION_FIRST", destinationId, budgetAmount: 100m, budgetCurrencyId: currencyId);

        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        // V-002 §5.3: DESTINATION_FIRST flags an over-budget plan instead of failing it,
        // because the user explicitly chose the destination.
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}\n{body}");

        var payload = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(payload.GetProperty("isOverBudget").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.True(await db.Itineraries.AsNoTracking().AnyAsync(i => i.TripId == tripId));
        var trip = await db.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(TripLifecycle.Generated, trip.Status);
    }

    [Fact]
    public async Task Generate_DestinationFirstWithinBudget_DoesNotSetOverBudgetFlag()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, destinationName) = await SeedDestinationAsync(isSupported: true);
        var places = await SeedPlacesAsync(destinationId);
        var currencyId = await GetUsdCurrencyIdAsync();

        ScriptedGeminiClient.SetResponse(BuildValidResponse(
            "DESTINATION_FIRST", destinationName, places.Hotel, places.Restaurant, places.Transport));

        var tripId = await CreateTripAsync(
            token, "DESTINATION_FIRST", destinationId, budgetAmount: 10000m, budgetCurrencyId: currencyId);

        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}\n{body}");
        Assert.False(JsonSerializer.Deserialize<JsonElement>(body).GetProperty("isOverBudget").GetBoolean());
    }

    [Fact]
    public async Task Generate_BudgetFirstNoOptionFitsBudget_Returns422WithAllOptionsOverBudget()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, destinationName) = await SeedDestinationAsync(isSupported: true);
        var places = await SeedPlacesAsync(destinationId, BudgetFirstTestPlaces.Suffix);
        var currencyId = await GetUsdCurrencyIdAsync();

        ScriptedGeminiClient.SetResponse(BuildValidResponse(
            "BUDGET_FIRST", destinationName, places.Hotel, places.Restaurant, places.Transport));

        // Deterministic plan cost = 100 (hotel) x 2 nights + 20 + 10 + 20 = 250.
        // V-002 §5.3: BUDGET_FIRST fails only when NO option survives.
        var tripId = await CreateTripAsync(
            token, "BUDGET_FIRST", destinationId, budgetAmount: 100m, budgetCurrencyId: currencyId);

        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("ALL_OPTIONS_OVER_BUDGET", body, StringComparison.OrdinalIgnoreCase);

        await AssertFailedValidationAsync(tripId, "ALL_OPTIONS_OVER_BUDGET");
    }

    [Fact]
    public async Task Generate_BudgetFirstWithinBudget_Succeeds()
    {
        var token = await RegisterAndGetTokenAsync();
        var (destinationId, destinationName) = await SeedDestinationAsync(isSupported: true);
        var places = await SeedPlacesAsync(destinationId, BudgetFirstTestPlaces.Suffix);
        var currencyId = await GetUsdCurrencyIdAsync();

        ScriptedGeminiClient.SetResponse(BuildValidResponse(
            "BUDGET_FIRST", destinationName, places.Hotel, places.Restaurant, places.Transport));

        var tripId = await CreateTripAsync(
            token, "BUDGET_FIRST", destinationId, budgetAmount: 10000m, budgetCurrencyId: currencyId);

        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}\n{body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.True(await db.Itineraries.AsNoTracking().AnyAsync(i => i.TripId == tripId));
        Assert.False(JsonSerializer.Deserialize<JsonElement>(body).GetProperty("isOverBudget").GetBoolean());

        var trip = await db.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(TripLifecycle.Generated, trip.Status);
    }

    [Fact]
    public async Task Generate_BudgetFirstWithoutSelectedDestination_KeepsTheOptionThatFits()
    {
        var token = await RegisterAndGetTokenAsync();
        var currencyId = await GetUsdCurrencyIdAsync();

        // Two supported destinations the budget could be spent in. They deliberately
        // reuse the same place names (covered by the temporary tier CSV) at different
        // prices, which also exercises cross-destination name resolution.
        var (expensiveId, expensiveName) = await SeedDestinationAsync(isSupported: true);
        var expensive = await SeedPlacesAsync(expensiveId, BudgetFirstTestPlaces.Suffix);
        var (cheapId, cheapName) = await SeedDestinationAsync(isSupported: true);
        var cheap = await SeedPlacesAsync(
            cheapId,
            BudgetFirstTestPlaces.Suffix,
            hotelPrice: 10m,
            restaurantPrice: 2m,
            transportPrice: 1m);

        // Expensive plan = 100x2 + 20 + 10 + 20 = 250 (over a 100 budget).
        // Cheap plan     = 10x2  +  2 +  1 +  2 =  25 (fits).
        ScriptedGeminiClient.SetResponse(BuildMultiOptionResponse(
            "BUDGET_FIRST",
            (expensiveName, expensive.Hotel, expensive.Restaurant, expensive.Transport),
            (cheapName, cheap.Hotel, cheap.Restaurant, cheap.Transport)));

        // No destination selected: the mode proposes destinations the budget can afford.
        var tripId = await CreateTripAsync(
            token, "BUDGET_FIRST", destinationId: null,
            budgetAmount: 100m, budgetCurrencyId: currencyId);

        var response = await GenerateAsync(token, tripId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}\n{body}");
        Assert.False(JsonSerializer.Deserialize<JsonElement>(body).GetProperty("isOverBudget").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // The affordable option won, and its destination was persisted onto the trip so
        // the user can keep or change it via SelectDestination.
        var trip = await db.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(cheapId, trip.DestinationId.GetValueOrDefault(-1));
        Assert.Equal(TripLifecycle.Generated, trip.Status);

        var itinerary = await db.Itineraries.AsNoTracking()
            .Include(i => i.Days)
                .ThenInclude(d => d.Items)
            .SingleAsync(i => i.TripId == tripId);

        var cheapPlaceIds = await db.Places.AsNoTracking()
            .Where(p => p.DestinationId == cheapId)
            .Select(p => p.Id)
            .ToListAsync();

        var itemPlaceIds = itinerary.Days
            .SelectMany(d => d.Items)
            .Select(i => i.PlaceId)
            .ToList();

        // The over-budget candidate was dropped, not persisted: every stored item
        // belongs to the affordable destination.
        Assert.NotEmpty(itemPlaceIds);
        Assert.All(itemPlaceIds, id => Assert.Contains(id, cheapPlaceIds));
    }
}
