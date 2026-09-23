using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration;
using Triply.Api.Modules.AIOrchestration.Dtos;

namespace Triply.Api.Tests;

/// <summary>
/// Gives this test its own database (it seeds a small reference dataset and must
/// not interfere with concurrently running classes).
/// </summary>
public sealed class ParityFixtureTestFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        IsolatedTestDb.Configure(builder, "Parity");
    }
}

/// <summary>
/// Python ↔ C# V-001 parity guard. Both sides consume the SAME shared fixture
/// file (AI/03-Validation/fixtures/v001-v002-parity-fixtures.json):
///   - Python: AI/03-Validation/validate_shared_fixtures.py
///   - C#:     this test (runs the real ItineraryValidationService)
/// V-002 lives in the orchestration service, not the validator, so it is asserted
/// only on the Python side (see AI_OUTPUT_VALIDATION_RULES.md §12).
/// </summary>
public sealed class ValidationParityFixtureTests : IClassFixture<ParityFixtureTestFactory>
{
    private readonly ParityFixtureTestFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ValidationParityFixtureTests(ParityFixtureTestFactory factory)
    {
        _factory = factory;
    }

    private static string FixturesPath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "v001-v002-parity-fixtures.json");

    [Fact]
    public async Task SharedV001Fixtures_MatchExpectedOutcomes()
    {
        var fixtureFile = JsonSerializer.Deserialize<ParityFixtureFile>(
            await File.ReadAllTextAsync(FixturesPath), JsonOptions);

        Assert.NotNull(fixtureFile);
        Assert.NotEmpty(fixtureFile!.Cases);

        // A single scope covers seeding AND validation: ItineraryValidationService
        // depends on the scoped ApplicationDbContext, so the validator must not
        // outlive the scope that created it.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var validator = scope.ServiceProvider.GetRequiredService<IItineraryValidator>();

        var destinationIds = await SeedDatasetAsync(db, fixtureFile);

        // The trip window comes from the fixture file so the Python and C# runners
        // apply Step 1 (day count / date alignment) to identical trip context.
        var startDate = DateOnly.Parse(fixtureFile.TripStartDate);
        var endDate = DateOnly.Parse(fixtureFile.TripEndDate);

        foreach (var testCase in fixtureFile.Cases)
        {
            var destinationId = destinationIds.TryGetValue(testCase.Destination, out var id)
                ? id
                : (long?)null;

            var trip = new Trip
            {
                PlanningMode = testCase.PlanningMode,
                // Budget-first plans are validated before the user picks a
                // destination, so no trip-level destination scope applies.
                DestinationId = testCase.PlanningMode == "BUDGET_FIRST" ? null : destinationId,
                StartDate = startDate,
                EndDate = endDate,
                TravelerCount = 2
            };

            var output = JsonSerializer.Deserialize<GeminiItineraryOutputDto>(
                testCase.Itinerary.GetRawText(), JsonOptions);

            Assert.NotNull(output);

            var result = await validator.ValidateAsync(trip, output!);

            var joinedErrors = string.Join(" | ", result.Errors);

            Assert.True(
                result.IsValid == testCase.Expected.V001Passed,
                $"{testCase.Id} ({testCase.Name}): expected IsValid={testCase.Expected.V001Passed} " +
                $"but got {result.IsValid}. Errors: {joinedErrors}");

            // Reason check (containment): a case expecting failure must fail for the
            // documented reason, not merely for some reason.
            if (fixtureFile.ExpectedFailureChecks.TryGetValue(testCase.Id, out var failureCheck))
            {
                foreach (var fragment in failureCheck.Fragments)
                {
                    Assert.True(
                        joinedErrors.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                        $"{testCase.Id} ({testCase.Name}): expected an error containing " +
                        $"'{fragment}' but got: {joinedErrors}");
                }
            }
        }
    }

    private static async Task<Dictionary<string, long>> SeedDatasetAsync(
        ApplicationDbContext db,
        ParityFixtureFile fixtureFile)
    {
        var placeCategoryIds = await db.PlaceCategories
            .ToDictionaryAsync(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var costCategoryId = await db.CostCategories.Select(c => c.Id).FirstAsync();
        var currencyId = await db.Currencies
            .Where(c => c.IsoCode == "USD")
            .Select(c => c.Id)
            .FirstOrDefaultAsync();
        if (currencyId == 0)
            currencyId = await db.Currencies.Select(c => c.Id).FirstAsync();

        var countryId = await db.Countries.Select(c => c.Id).FirstAsync();

        var destinationIds = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var testCase in fixtureFile.Cases)
        {
            foreach (var destination in testCase.Dataset.Destinations)
            {
                if (destinationIds.ContainsKey(destination.Name))
                    continue;

                var entity = new Destination
                {
                    CountryId = countryId,
                    Name = destination.Name,
                    Description = "Parity fixture destination",
                    IsSupported = destination.IsSupported
                };

                db.Destinations.Add(entity);
                await db.SaveChangesAsync();
                destinationIds[destination.Name] = entity.Id;
            }

            foreach (var place in testCase.Dataset.Places)
            {
                if (!destinationIds.TryGetValue(place.Destination, out var placeDestinationId))
                    continue;

                if (!placeCategoryIds.TryGetValue(place.Category, out var categoryId))
                    continue;

                db.Places.Add(new Place
                {
                    DestinationId = placeDestinationId,
                    PlaceCategoryId = categoryId,
                    Name = place.Name,
                    Description = "Parity fixture place",
                    ReferencePrice = place.ReferencePrice,
                    CurrencyId = currencyId,
                    CostCategoryId = costCategoryId,
                    PriceUpdatedAt = DateTime.UtcNow,
                    IsActive = place.IsActive
                });
            }

            await db.SaveChangesAsync();
        }

        return destinationIds;
    }

    // --- Fixture file DTOs (snake/camel case matched case-insensitively) ---

    private sealed class ParityFixtureFile
    {
        public string TripStartDate { get; set; } = "";
        public string TripEndDate { get; set; } = "";

        /// <summary>
        /// Per-case failure-reason expectations. Only `fragments` is consumed here:
        /// `codes` are the Python harness's failure codes, while the C# validator
        /// reports human-readable error text. See the fixture's
        /// `expectedFailureNotes`.
        /// </summary>
        public Dictionary<string, ParityFailureCheck> ExpectedFailureChecks { get; set; } =
            new(StringComparer.Ordinal);

        public List<ParityCase> Cases { get; set; } = new();
    }

    private sealed class ParityFailureCheck
    {
        public List<string> Codes { get; set; } = new();
        public List<string> Fragments { get; set; } = new();
    }

    private sealed class ParityCase
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string PlanningMode { get; set; } = "";
        public string Destination { get; set; } = "";
        public double? Budget { get; set; }
        public ParityDataset Dataset { get; set; } = new();
        public JsonElement Itinerary { get; set; }
        public ParityExpected Expected { get; set; } = new();
    }

    private sealed class ParityDataset
    {
        public List<ParityDestination> Destinations { get; set; } = new();
        public List<ParityPlace> Places { get; set; } = new();
    }

    private sealed class ParityDestination
    {
        public string Name { get; set; } = "";
        public bool IsSupported { get; set; } = true;
    }

    private sealed class ParityPlace
    {
        public string Name { get; set; } = "";
        public string Destination { get; set; } = "";
        public string Category { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public decimal ReferencePrice { get; set; }
        public string Currency { get; set; } = "USD";
    }

    private sealed class ParityExpected
    {
        public bool V001Passed { get; set; }
        public bool V002Passed { get; set; }
    }
}
