using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Tests;

public sealed class DuplicatePlaceTestFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Same deterministic fake-Gemini pipeline as AiTestWebApplicationFactory
        // (that class is sealed, so the swap is re-applied here).
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGeminiClient>();
            services.AddSingleton<IGeminiClient, FakeGeminiClient>();
        });

        // These tests temporarily drop the Places unique index to CONSTRUCT the
        // malformed condition (an active duplicate place name within a destination)
        // — they must never run against the shared test database.
        IsolatedTestDb.Configure(builder, "Dup");
    }
}

/// <summary>
/// Gap 6 regression tests: an active duplicate place name within a destination must
/// surface through the AI pipeline's FAILED_VALIDATION semantics (HTTP 422, auditable
/// AIGeneration rows, nothing persisted as generated) instead of an unhandled
/// exception (HTTP 400/500).
/// </summary>
[Collection("GeminiFake")]
public sealed class DuplicatePlaceValidationTests : IClassFixture<DuplicatePlaceTestFactory>
{
    private readonly DuplicatePlaceTestFactory _factory;
    private readonly HttpClient _client;

    public DuplicatePlaceValidationTests(DuplicatePlaceTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"dupplace_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(UniqueEmail(), "P@ssw0rd123", "Duplicate Test"));

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

    private async Task<Place> AddPlaceAsync(long destinationId, int placeCategoryId, int costCategoryId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var place = new Place
        {
            DestinationId = destinationId,
            PlaceCategoryId = placeCategoryId,
            Name = name,
            Description = "Duplicate-place validation test place",
            ReferencePrice = 30m,
            CurrencyId = 1,
            CostCategoryId = costCategoryId,
            PriceUpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        db.Places.Add(place);
        await db.SaveChangesAsync();
        return place;
    }

    private async Task SeedGroundedPlacesAsync(long destinationId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var accommodation = await AddPlaceAsync(destinationId, 4, 1, $"Dup Hotel {suffix}");
        var restaurantA = await AddPlaceAsync(destinationId, 2, 3, $"Dup Restaurant A {suffix}");
        var restaurantB = await AddPlaceAsync(destinationId, 2, 3, $"Dup Restaurant B {suffix}");
        var transport = await AddPlaceAsync(destinationId, 5, 2, $"Dup Transport {suffix}");

        PartialRegenerationTestPlaceNames.Set(
            accommodation.Name,
            restaurantA.Name,
            restaurantB.Name,
            transport.Name);
    }

    private async Task DropPlaceUniqueIndexAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "DROP INDEX [IX_Places_DestinationId_Name] ON [Places];");
    }

    /// <summary>
    /// Removes every test row with the duplicated name and restores the unique
    /// index exactly as the migration created it. Always run from a finally block:
    /// the product's DB constraint must be present again after the test.
    /// </summary>
    private async Task CleanupAndRestorePlaceUniqueIndexAsync(long destinationId, string duplicatedName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Places
            .Where(p => p.DestinationId == destinationId && p.Name == duplicatedName)
            .ExecuteDeleteAsync();

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX [IX_Places_DestinationId_Name] ON [Places]([DestinationId], [Name]);");
    }

    private async Task<Guid> CreateDestinationFirstTripAsync(string token, long destinationId)
    {
        UseToken(token);

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
        return trip!.Id;
    }

    private async Task AssertFailedValidationAsync(Guid tripId, string expectedErrorFragment)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Auditable FAILED_VALIDATION attempts — never SUCCEEDED.
        var generations = await db.AIGenerations.AsNoTracking()
            .Where(g => g.TripId == tripId)
            .ToListAsync();

        Assert.NotEmpty(generations);
        Assert.All(generations, g => Assert.Equal("FAILED_VALIDATION", g.Status));
        Assert.Contains(
            generations,
            g => g.ValidationErrors != null &&
                 g.ValidationErrors.Contains(expectedErrorFragment, StringComparison.OrdinalIgnoreCase));

        // No invalid itinerary is persisted as successfully generated.
        Assert.False(await db.Itineraries.AsNoTracking().AnyAsync(i => i.TripId == tripId));

        // Trip is back to DRAFT and retry-able.
        var trip = await db.Trips.AsNoTracking().SingleAsync(x => x.Id == tripId);
        Assert.Equal(TripLifecycle.Draft, trip.Status);
    }

    [Fact]
    public async Task Generate_DuplicateNameReferencedByOutput_ReturnsFailedValidation_NotUnhandledError()
    {
        var token = await RegisterAndGetTokenAsync();
        var destinationId = await GetParisDestinationIdAsync();
        await SeedGroundedPlacesAsync(destinationId);

        // The fake output always references "Restaurant A" — duplicate THAT name so
        // the validator's place lookup encounters the duplicate condition.
        string duplicatedName;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            duplicatedName = await db.Places
                .Where(p => p.DestinationId == destinationId && p.Name.StartsWith("Dup Restaurant A "))
                .Select(p => p.Name)
                .FirstAsync();
        }

        await DropPlaceUniqueIndexAsync();
        try
        {
            // The unique index normally prevents this; the application must still
            // fail gracefully (legacy/pre-index data). Constructed only inside this
            // isolated test database, and the index is restored in finally.
            await AddPlaceAsync(destinationId, 2, 3, duplicatedName);

            var tripId = await CreateDestinationFirstTripAsync(token, destinationId);

            UseToken(token);
            var response = await _client.PostAsJsonAsync(
                $"/api/trips/{tripId}/generate",
                new { scope = "FULL" });

            var body = await response.Content.ReadAsStringAsync();

            // Existing project convention for AI validation failures: 422, never an
            // unhandled 400/500.
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("duplicate active place_name", body, StringComparison.OrdinalIgnoreCase);

            await AssertFailedValidationAsync(tripId, "duplicate active place_name");
        }
        finally
        {
            await CleanupAndRestorePlaceUniqueIndexAsync(destinationId, duplicatedName);
        }
    }

    [Fact]
    public async Task Generate_DuplicateNameNotReferencedByOutput_ReturnsFailedValidation_NotUnhandledError()
    {
        var token = await RegisterAndGetTokenAsync();
        var destinationId = await GetParisDestinationIdAsync();
        await SeedGroundedPlacesAsync(destinationId);

        // An attraction that the fake output never references — the validator only
        // looks up names present in the output, so the duplicate surfaces when the
        // orchestration builds its destination-scoped name lookup.
        var extraSuffix = Guid.NewGuid().ToString("N")[..8];
        var extraName = $"Dup Unreferenced Attraction {extraSuffix}";

        await DropPlaceUniqueIndexAsync();
        try
        {
            await AddPlaceAsync(destinationId, 1, 4, extraName);
            await AddPlaceAsync(destinationId, 1, 4, extraName);

            var tripId = await CreateDestinationFirstTripAsync(token, destinationId);

            UseToken(token);
            var response = await _client.PostAsJsonAsync(
                $"/api/trips/{tripId}/generate",
                new { scope = "FULL" });

            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("duplicate active place name", body, StringComparison.OrdinalIgnoreCase);

            await AssertFailedValidationAsync(tripId, "duplicate active place name");
        }
        finally
        {
            await CleanupAndRestorePlaceUniqueIndexAsync(destinationId, extraName);
        }
    }
}
