using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Tests;

/// <summary>
/// Gap 3 regression tests: full generation must acquire a DRAFT trip through the
/// same atomic version-guard principle partial regeneration already uses
/// (conditional UPDATE + affected-row count), so two parallel /generate requests
/// can never both start generating the same trip.
/// </summary>
[Collection("GeminiFake")]
public sealed class FullGenerationConcurrencyTests : IClassFixture<AiTestWebApplicationFactory>
{
    private readonly AiTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FullGenerationConcurrencyTests(AiTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"forgen_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(UniqueEmail(), "P@ssw0rd123", "Concurrency Test"));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Token));
        return body!.Token;
    }

    private void UseToken(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<long> GetParisDestinationIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Destinations
            .Where(d => d.Name == "Paris")
            .Select(d => d.Id)
            .FirstAsync();
    }

    /// <summary>
    /// Seeds a grounded place set the FakeGeminiClient can safely reference
    /// (same pattern as PartialRegenerationIntegrationTests.SeedPlacesAsync).
    /// </summary>
    private async Task SeedGroundedPlacesAsync(long destinationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];

        var accommodation = new Place
        {
            DestinationId = destinationId,
            PlaceCategoryId = 4,
            Name = $"Concurrency Hotel {suffix}",
            Description = "Concurrency test accommodation",
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
            Name = $"Concurrency Restaurant A {suffix}",
            Description = "Restaurant A",
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
            Name = $"Concurrency Restaurant B {suffix}",
            Description = "Restaurant B",
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
            Name = $"Concurrency Transport {suffix}",
            Description = "Transport",
            ReferencePrice = 10m,
            CurrencyId = 1,
            CostCategoryId = 2,
            PriceUpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        db.Places.AddRange(accommodation, restaurantA, restaurantB, transport);
        await db.SaveChangesAsync();

        PartialRegenerationTestPlaceNames.Set(
            accommodation.Name,
            restaurantA.Name,
            restaurantB.Name,
            transport.Name);
    }

    private async Task<(Guid Id, int Version)> CreateDestinationFirstTripAsync(string token)
    {
        UseToken(token);
        var destinationId = await GetParisDestinationIdAsync();

        var response = await _client.PostAsJsonAsync("/api/trips", new
        {
            planningMode = "DESTINATION_FIRST",
            destinationId,
            startDate = "2026-10-01",
            endDate = "2026-10-03",
            travelerCount = 2
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"HTTP {(int)response.StatusCode} ({response.StatusCode})\nResponse body:\n{responseBody}");

        var trip = await response.Content.ReadFromJsonAsync<TripResponse>();
        Assert.NotNull(trip);
        return (trip!.Id, trip.Version);
    }

    [Fact]
    public async Task Generate_SequentialDraftTrip_StartsGenerationAndCompletes()
    {
        var token = await RegisterAndGetTokenAsync();
        await SeedGroundedPlacesAsync(await GetParisDestinationIdAsync());
        var (tripId, version) = await CreateDestinationFirstTripAsync(token);
        Assert.Equal(1, version);

        UseToken(token);
        var response = await _client.PostAsJsonAsync(
            $"/api/trips/{tripId}/generate",
            new { scope = "FULL" });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"HTTP {(int)response.StatusCode} ({response.StatusCode})\nResponse body:\n{body}");

        // Claim DRAFT@1 -> GENERATING@2, then success GENERATED@2 -> @3.
        var parsed = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(3, parsed.GetProperty("tripVersion").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var trip = await db.Trips.AsNoTracking().SingleAsync(x => x.Id == tripId);
        Assert.Equal(TripLifecycle.Generated, trip.Status);
        Assert.Equal(3, trip.Version);

        var succeeded = await db.AIGenerations.AsNoTracking()
            .CountAsync(g => g.TripId == tripId && g.Status == "SUCCEEDED");
        Assert.Equal(1, succeeded);
    }

    [Fact]
    public async Task Generate_TwoParallelRequests_OnlyOneAcquiresTheTrip()
    {
        var token = await RegisterAndGetTokenAsync();
        await SeedGroundedPlacesAsync(await GetParisDestinationIdAsync());
        var (tripId, version) = await CreateDestinationFirstTripAsync(token);
        Assert.Equal(1, version);

        UseToken(token);

        var requestA = _client.PostAsJsonAsync($"/api/trips/{tripId}/generate", new { scope = "FULL" });
        var requestB = _client.PostAsJsonAsync($"/api/trips/{tripId}/generate", new { scope = "FULL" });
        var responses = await Task.WhenAll(requestA, requestB);

        var statuses = responses.Select(r => r.StatusCode).ToList();

        // Exactly one winner, exactly one rejected/conflicting request. The loser
        // must be a 409 (claim lost / trip no longer DRAFT), never a second success.
        Assert.Equal(2, statuses.Count);
        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Contains(HttpStatusCode.Conflict, statuses);
        Assert.Single(statuses, s => s == HttpStatusCode.OK);
        Assert.Single(statuses, s => s == HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var trip = await db.Trips.AsNoTracking().SingleAsync(x => x.Id == tripId);
        Assert.Equal(TripLifecycle.Generated, trip.Status);

        // Exactly one claim happened: claim 1 -> 2, success 2 -> 3. A second
        // generation that had also acquired the trip would have pushed this to 4+.
        Assert.Equal(3, trip.Version);

        // No duplicate generation ownership: the losing request never created an
        // AIGeneration row.
        var succeeded = await db.AIGenerations.AsNoTracking()
            .CountAsync(g => g.TripId == tripId && g.Status == "SUCCEEDED");
        Assert.Equal(1, succeeded);

        var totalGenerations = await db.AIGenerations.AsNoTracking()
            .CountAsync(g => g.TripId == tripId);
        Assert.Equal(1, totalGenerations);
    }

    [Fact]
    public async Task Generate_WithStaleExpectedVersion_ReturnsConflict_WithoutClaiming()
    {
        var token = await RegisterAndGetTokenAsync();
        var (tripId, version) = await CreateDestinationFirstTripAsync(token);
        Assert.Equal(1, version);

        // Move Trip.Version forward through a legitimate concurrent writer
        // (metadata PATCH), leaving the client-observed version at 1.
        UseToken(token);
        var patch = await _client.PatchAsJsonAsync(
            $"/api/trips/{tripId}",
            new { title = "Renamed before generation", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var response = await _client.PostAsJsonAsync(
            $"/api/trips/{tripId}/generate",
            new { scope = "FULL", expectedVersion = 1 });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var trip = await db.Trips.AsNoTracking().SingleAsync(x => x.Id == tripId);
        Assert.Equal(TripLifecycle.Draft, trip.Status);
        Assert.Equal(2, trip.Version);

        // A stale request must not have claimed the trip or started any attempt.
        Assert.False(await db.AIGenerations.AsNoTracking().AnyAsync(g => g.TripId == tripId));
    }
}
