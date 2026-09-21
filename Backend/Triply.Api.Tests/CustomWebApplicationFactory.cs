using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Triply.Api.Data;
using Triply.Api.Entities;

namespace Triply.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName =
        $"TriplyDb_Tests_{Guid.NewGuid():N}";

    private const string TestJwtKey =
        "Integration-Test-Only-Signing-Key-Not-For-Production-12345";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Test-only reference data. Production Backend has no static seed;
        // tests create the minimal AI-shaped reference catalog they depend on.
        SeedReferenceData(db);

        return host;
    }

    private static void SeedReferenceData(ApplicationDbContext db)
    {
        // Tests use a deterministic reference catalog, but production no longer
        // owns static reference data. Ensure every test dependency exists even
        // when an older migration has left a partial reference dataset behind.

        var countriesByIso = db.Countries
            .ToDictionary(x => x.IsoCode, StringComparer.OrdinalIgnoreCase);

        foreach (var country in new[]
        {
            new Country { Name = "France", IsoCode = "FR" },
            new Country { Name = "Jordan", IsoCode = "JO" },
            new Country { Name = "United States", IsoCode = "US" }
        })
        {
            if (!countriesByIso.ContainsKey(country.IsoCode))
            {
                db.Countries.Add(country);
                countriesByIso[country.IsoCode] = country;
            }
        }

        db.SaveChanges();

        var currenciesByIso = db.Currencies
            .ToDictionary(x => x.IsoCode, StringComparer.OrdinalIgnoreCase);

        foreach (var currency in new[]
        {
            new Currency { IsoCode = "USD", Symbol = "$" },
            new Currency { IsoCode = "JOD", Symbol = "JD" },
            new Currency { IsoCode = "EUR", Symbol = "€" }
        })
        {
            if (!currenciesByIso.ContainsKey(currency.IsoCode))
            {
                db.Currencies.Add(currency);
                currenciesByIso[currency.IsoCode] = currency;
            }
        }

        db.SaveChanges();

        var placeCategories = new[]
        {
            ("ATTRACTION", "Attraction"),
            ("RESTAURANT", "Restaurant"),
            ("ACTIVITY", "Activity"),
            ("ACCOMMODATION", "Accommodation"),
            ("TRANSPORT", "Transport")
        };

        var existingPlaceCategoryCodes = db.PlaceCategories
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, label) in placeCategories)
        {
            if (!existingPlaceCategoryCodes.Contains(code))
                db.PlaceCategories.Add(new PlaceCategory { Code = code, Label = label });
        }

        var costCategories = new[]
        {
            ("ACCOMMODATION", "Accommodation"),
            ("TRANSPORTATION", "Transportation"),
            ("FOOD", "Food"),
            ("ACTIVITIES", "Activities"),
            ("OTHER", "Other")
        };

        var existingCostCategoryCodes = db.CostCategories
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, label) in costCategories)
        {
            if (!existingCostCategoryCodes.Contains(code))
                db.CostCategories.Add(new CostCategory { Code = code, Label = label });
        }

        var interestCategories = new[]
        {
            ("NATURE", "Nature"),
            ("HISTORY", "History"),
            ("FOOD", "Food"),
            ("SHOPPING", "Shopping"),
            ("ADVENTURE", "Adventure"),
            ("CULTURE", "Culture"),
            ("RELAXATION", "Relaxation"),
            ("OTHER", "Other")
        };

        var existingInterestCodes = db.InterestCategories
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, label) in interestCategories)
        {
            if (!existingInterestCodes.Contains(code))
                db.InterestCategories.Add(new InterestCategory { Code = code, Label = label });
        }

        db.SaveChanges();

        // The tests need these three supported destinations regardless of whether
        // an older migration left a partial dataset behind.
        var franceId = countriesByIso["FR"].Id;
        var jordanId = countriesByIso["JO"].Id;
        var usId = countriesByIso["US"].Id;

        // Test fixture mirrors the three destinations owned by the AI dataset.
        // This is intentionally test-only; production reference data is never
        // seeded by the Backend application.
        var destinationsByName = db.Destinations
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var destination in new[]
        {
            new Destination
            {
                CountryId = franceId,
                Name = "Paris",
                Description = "Capital of France, known for iconic landmarks, museums, and cuisine.",
                Latitude = 48.8566m,
                Longitude = 2.3522m,
                IsSupported = true
            },
            new Destination
            {
                CountryId = jordanId,
                Name = "Amman",
                Description = "Capital of Jordan, blending ancient Roman ruins with a modern Middle Eastern city.",
                Latitude = 31.9539m,
                Longitude = 35.9106m,
                IsSupported = true
            },
            new Destination
            {
                CountryId = usId,
                Name = "New York",
                Description = "Major US city known for iconic skyline, museums, and entertainment.",
                Latitude = 40.7128m,
                Longitude = -74.0060m,
                IsSupported = true
            }
        })
        {
            if (!destinationsByName.ContainsKey(destination.Name))
            {
                db.Destinations.Add(destination);
                destinationsByName[destination.Name] = destination;
            }
        }

        db.SaveChanges();

        var currencies = db.Currencies
            .ToDictionary(x => x.IsoCode, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var existingRateCurrencyIds = db.ExchangeRates
            .Select(x => x.CurrencyId)
            .ToHashSet();

        foreach (var (isoCode, rateToUsd) in new[]
        {
            ("EUR", 1.08m),
            ("JOD", 1.41m),
            ("USD", 1.00m)
        })
        {
            var currencyId = currencies[isoCode];
            if (!existingRateCurrencyIds.Contains(currencyId))
            {
                db.ExchangeRates.Add(new ExchangeRate
                {
                    CurrencyId = currencyId,
                    RateToUsd = rateToUsd,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        db.SaveChanges();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var connectionString =
            Environment.GetEnvironmentVariable("TRIPLY_TEST_DB_CONNECTION")
            ?? $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True;";


        // Force the test configuration values into the application
        // configuration used by Program.cs.
        builder.UseSetting(
            "ConnectionStrings:Default",
            connectionString);

        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Jwt:Issuer", "Triply");
        builder.UseSetting("Jwt:Audience", "TriplyClients");
        builder.UseSetting("Jwt:ExpiresMinutes", "60");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] =
                    connectionString,

                ["Jwt:Key"] = TestJwtKey,

                ["Jwt:Issuer"] = "Triply",

                ["Jwt:Audience"] = "TriplyClients",

                ["Jwt:ExpiresMinutes"] = "60"
            });
        });
    }
}