using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Triply.Api.Data;
using Triply.Api.Modules.AIOrchestration;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Tests;

public sealed class SeedProvisioningTestFactory : CustomWebApplicationFactory
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

        // The real curated dataset is imported here — keep it out of the shared
        // test database so concurrently running test classes are unaffected.
        IsolatedTestDb.Configure(builder, "Seed");
    }
}

/// <summary>
/// Gap 1 regression tests: a fresh development database can be provisioned from the
/// real curated CSVs automatically, re-running never duplicates rows, existing
/// user/trip data is never destroyed, and destination-first / budget-first flows
/// work against the provisioned reference data.
/// </summary>
[Collection("GeminiFake")]
public sealed class DatasetProvisioningTests : IClassFixture<SeedProvisioningTestFactory>
{
    private readonly SeedProvisioningTestFactory _factory;
    private readonly HttpClient _client;

    public DatasetProvisioningTests(SeedProvisioningTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"seed_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(UniqueEmail(), "P@ssw0rd123", "Seed Test"));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Token));
        return body!.Token;
    }

    private void UseToken(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<SeedSummary> RunProvisioningAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SeedDataTest");

        return await SeedData.EnsureCuratedDatasetAsync(db, configuration, environment, logger);
    }

    private sealed record DbCounts(
        int Countries,
        int Currencies,
        int PlaceCategories,
        int CostCategories,
        int InterestCategories,
        int Destinations,
        int Places,
        int PlaceInterests,
        int ExchangeRates);

    private static async Task<DbCounts> CountAsync(ApplicationDbContext db) => new(
        await db.Countries.CountAsync(),
        await db.Currencies.CountAsync(),
        await db.PlaceCategories.CountAsync(),
        await db.CostCategories.CountAsync(),
        await db.InterestCategories.CountAsync(),
        await db.Destinations.CountAsync(),
        await db.Places.CountAsync(),
        await db.PlaceInterests.CountAsync(),
        await db.ExchangeRates.CountAsync());

    [Fact]
    public async Task FreshDatabase_ProvisioningTwice_LoadsCuratedDataWithoutDuplicates_AndGenerationWorks()
    {
        // --- User data exists BEFORE provisioning (must survive it) ---
        var token = await RegisterAndGetTokenAsync();

        long parisId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            parisId = await db.Destinations.Where(d => d.Name == "Paris").Select(d => d.Id).FirstAsync();
        }

        UseToken(token);
        var createResponse = await _client.PostAsJsonAsync("/api/trips", new
        {
            planningMode = "DESTINATION_FIRST",
            destinationId = parisId,
            startDate = "2026-10-01",
            endDate = "2026-10-03",
            travelerCount = 2
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);
        var preSeedTrip = await createResponse.Content.ReadFromJsonAsync<TripResponse>();
        Assert.NotNull(preSeedTrip);

        // --- First provisioning run imports the real curated dataset ---
        var first = await RunProvisioningAsync();
        Assert.True(
            Directory.Exists(first.DataDirectory),
            $"Curated dataset directory must exist after provisioning (looked at '{first.DataDirectory}').");
        Assert.True(first.AnyRowsInserted, "First run should insert the curated reference rows.");
        Assert.True(first.PlacesAdded > 0, "First run should insert curated places.");

        // The test fixture already seeded the same three countries/currencies/
        // categories/destinations — provisioning must NOT duplicate them.
        Assert.Equal(0, first.CountriesAdded);
        Assert.Equal(0, first.CurrenciesAdded);
        Assert.Equal(0, first.PlaceCategoriesAdded);
        Assert.Equal(0, first.CostCategoriesAdded);
        Assert.Equal(0, first.InterestCategoriesAdded);
        Assert.Equal(0, first.DestinationsAdded);

        // Expected row counts come from the CSVs themselves (self-consistent).
        var placeCsvPath = Path.Combine(first.DataDirectory, "Place.csv");
        var expectedPlaces = File.ReadAllLines(placeCsvPath, Encoding.UTF8)
            .Count(l => !string.IsNullOrWhiteSpace(l)) - 1;
        var linkCsvPath = Path.Combine(first.DataDirectory, "PlaceInterest_seed_draft.csv");
        var expectedLinks = File.ReadAllLines(linkCsvPath, Encoding.UTF8)
            .Count(l => !string.IsNullOrWhiteSpace(l)) - 1;

        DbCounts counts1;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            counts1 = await CountAsync(db);

            Assert.Equal(expectedPlaces, counts1.Places);
            Assert.True(counts1.Places > 0);

            // Every supported curated destination has active curated places
            // (destination-first generation pre-condition).
            foreach (var destinationName in new[] { "Paris", "Amman", "New York" })
            {
                var activePlaces = await db.Places.CountAsync(p =>
                    p.IsActive && p.Destination!.Name == destinationName);
                Assert.True(
                    activePlaces > 0,
                    $"No active places provisioned for destination {destinationName}.");
            }
        }

        Assert.True(counts1.PlaceInterests >= expectedLinks);

        // --- Second run: fully idempotent, zero inserts, counts unchanged ---
        var second = await RunProvisioningAsync();
        Assert.False(second.AnyRowsInserted,
            $"Second run must insert nothing but inserted: places={second.PlacesAdded}, " +
            $"destinations={second.DestinationsAdded}, links={second.PlaceInterestsAdded}, " +
            $"rates={second.ExchangeRatesAdded}.");

        DbCounts counts2;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            counts2 = await CountAsync(db);
        }

        Assert.Equal(counts1, counts2);

        // --- Existing user/trip data was not deleted ---
        UseToken(token);
        var getResponse = await _client.GetAsync($"/api/trips/{preSeedTrip!.Id}");
        Assert.Equal(System.Net.HttpStatusCode.OK, getResponse.StatusCode);

        // --- Budget-tier context (BUDGET_FIRST prerequisite) is readable ---
        using (var scope = _factory.Services.CreateScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IExtraAiContextReader>();
            var tiers = await reader.ReadBudgetTiersAsync();
            Assert.NotEmpty(tiers);
        }

        // --- Destination-first generation works end to end on provisioned data ---
        // (fake Gemini grounded in the real curated place names)
        PartialRegenerationTestPlaceNames.Set(
            "Hôtel Le Clos Notre-Dame",
            "Chez Le Libanais",
            "Bouillon Chartier Grands Boulevards",
            "Intra-city Metro/Bus Single Ticket");

        var generateResponse = await _client.PostAsJsonAsync(
            $"/api/trips/{preSeedTrip.Id}/generate",
            new { scope = "FULL" });
        var generateBody = await generateResponse.Content.ReadAsStringAsync();
        Assert.True(
            generateResponse.IsSuccessStatusCode,
            $"HTTP {(int)generateResponse.StatusCode}\nResponse body:\n{generateBody}");

        var generated = JsonSerializer.Deserialize<JsonElement>(generateBody);
        Assert.Equal(3, generated.GetProperty("tripVersion").GetInt32());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Every persisted item resolves to a curated Place row of the selected
            // destination — 0% invented places.
            var itemPlaceIds = await db.ItineraryItems
                .Where(i => i.ItineraryDay.Itinerary.TripId == preSeedTrip.Id)
                .Select(i => i.PlaceId)
                .ToListAsync();
            Assert.NotEmpty(itemPlaceIds);

            var curatedParisPlaceIds = await db.Places
                .Where(p => p.DestinationId == parisId)
                .Select(p => p.Id)
                .ToListAsync();
            Assert.All(itemPlaceIds, id => Assert.Contains(id, curatedParisPlaceIds));
        }

        // --- Budget-first reference flow works against provisioned data ---
        var suggestionsResponse = await _client.PostAsJsonAsync(
            "/api/destinations/suggestions",
            new
            {
                budgetAmount = 500,
                budgetCurrencyId = 1,
                interestCategoryIds = new[] { 1 }
            });
        var suggestionsBody = await suggestionsResponse.Content.ReadAsStringAsync();
        Assert.True(
            suggestionsResponse.IsSuccessStatusCode,
            $"HTTP {(int)suggestionsResponse.StatusCode}\n{suggestionsBody}");

        var suggestions = JsonSerializer.Deserialize<JsonElement>(suggestionsBody);
        Assert.True(
            suggestions.GetProperty("suggestions").GetArrayLength() >= 1,
            $"Expected at least one budget-fit suggestion from curated data.\n{suggestionsBody}");
    }

    [Fact]
    public async Task MissingDatasetDirectory_ProvisioningThrows_NeverContinuesSilently()
    {
        // GAP-001: a provisioning failure must be visible — startup must never
        // continue with a silently unprovisioned reference database.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SeedDataTest");

        var missingPath = Path.Combine(
            Path.GetTempPath(), $"triply-missing-dataset-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:CuratedDataPath"] = missingPath
            })
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeedData.EnsureCuratedDatasetAsync(db, configuration, environment, logger));

        Assert.Contains("Curated dataset directory not found", ex.Message);
    }
}
