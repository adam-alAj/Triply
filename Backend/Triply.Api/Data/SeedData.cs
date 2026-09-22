using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Triply.Api.Entities;

namespace Triply.Api.Data;

/// <summary>
/// DEV/LOCAL curated reference-data provisioning (Gap 1, FR-DATA-001).
///
/// PR #66 removed the backend's static seed on purpose: the AI track's versioned
/// CSVs under AI/01-Dataset/curated-data are the single source of truth. Nothing
/// used to load them automatically, so a fresh database started with zero
/// reference rows and destination-first / budget-first generation failed.
///
/// This importer is the deterministic provisioning path for development and local
/// environments. It:
///   - runs only when Program.cs calls it, and only for the Development environment,
///   - reads the REAL curated CSVs (never fabricated or minimal rows),
///   - is additive and idempotent: every row is matched by natural key
///     (ISO code / code / (country, name) / (destination, name) / (place, interest))
///     and only INSERTED when missing - re-running never duplicates and never
///     updates or deletes existing rows, so user and trip data is untouched,
///   - mirrors the semantics of AI/01-Dataset/seed/*.py but needs no Python,
///     pyodbc or ODBC driver inside the container (the backend image has none).
///
/// Production/Staging behavior is unchanged: Program.cs never calls this outside
/// Development, and there is no code path here that deletes data.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Placeholder exchange rates, mirrored from AI/01-Dataset/seed/seed_exchange_rates.py.
    /// That script is deliberately NOT a scheduled job: rates are manually maintained
    /// placeholders (see Entities/ExchangeRate.cs). Insert-if-missing only - an existing
    /// (manually refreshed) rate is never overwritten.
    /// </summary>
    private static readonly (string IsoCode, decimal RateToUsd)[] PlaceholderExchangeRates =
    {
        ("USD", 1.00m),
        ("JOD", 1.41m),
        ("EUR", 1.08m)
    };

    /// <summary>
    /// Provisions the curated reference dataset from curated-data/*.csv.
    /// Safe to call on every Development startup and safe to call repeatedly.
    /// Returns a summary of inserted rows (all zero on a fully provisioned database).
    /// </summary>
    public static async Task<SeedSummary> EnsureCuratedDatasetAsync(
        ApplicationDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var dataDirectory = ResolveDataDirectory(
            configuration["AI:CuratedDataPath"],
            environment.ContentRootPath);

        if (!Directory.Exists(dataDirectory))
        {
            logger.LogWarning(
                "Curated dataset directory not found at '{Path}' - skipping reference-data " +
                "provisioning. Set AI:CuratedDataPath (relative to the content root) or make " +
                "the AI/01-Dataset/curated-data folder available to enable it.",
                dataDirectory);
            return new SeedSummary(Skipped: true, DataDirectory: dataDirectory);
        }

        logger.LogInformation(
            "Provisioning curated reference data from '{Path}' (additive, idempotent).",
            dataDirectory);

        var summary = await SeedAsync(db, dataDirectory, cancellationToken);

        logger.LogInformation(
            "Reference-data provisioning finished: {Countries} countries, {Currencies} currencies, " +
            "{PlaceCategories} place categories, {CostCategories} cost categories, " +
            "{InterestCategories} interest categories, {Destinations} destinations, {Places} places, " +
            "{PlaceInterests} place-interest links, {ExchangeRates} exchange rates inserted.",
            summary.CountriesAdded,
            summary.CurrenciesAdded,
            summary.PlaceCategoriesAdded,
            summary.CostCategoriesAdded,
            summary.InterestCategoriesAdded,
            summary.DestinationsAdded,
            summary.PlacesAdded,
            summary.PlaceInterestsAdded,
            summary.ExchangeRatesAdded);

        return summary;
    }

    /// <summary>
    /// Same path convention as ExtraAiContextReader: an explicit (rooted or
    /// content-root-relative) AI:CuratedDataPath wins; otherwise the repository
    /// layout is used - locally Backend/Triply.Api -> repo root -> AI/01-Dataset/
    /// curated-data, and in Docker the compose mount maps that to /AI
    /// (ContentRootPath=/app, ../../AI resolves to /AI).
    /// </summary>
    private static string ResolveDataDirectory(string? configured, string contentRootPath)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(contentRootPath, configured));
        }

        return Path.GetFullPath(Path.Combine(
            contentRootPath, "..", "..", "AI", "01-Dataset", "curated-data"));
    }

    private static async Task<SeedSummary> SeedAsync(
        ApplicationDbContext db,
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        // ---------------- Load CSVs + translate CSV foreign keys ----------------
        // CSV foreign keys are CSV row ids - translate through the CSVs exactly like
        // the Python seeders do, never assuming the database's identity values.
        var countriesCsv = ReadCsvRequired(dataDirectory, "Country.csv");
        var currenciesCsv = ReadCsvRequired(dataDirectory, "Currency.csv");
        var placeCategoriesCsv = ReadCsvRequired(dataDirectory, "PlaceCategory.csv");
        var costCategoriesCsv = ReadCsvRequired(dataDirectory, "CostCategory.csv");
        var interestCategoriesCsv = ReadCsvRequired(dataDirectory, "InterestCategory.csv");
        var destinationsCsv = ReadCsvRequired(dataDirectory, "Destination.csv");
        var placesCsv = ReadCsvRequired(dataDirectory, "Place.csv");
        var placeInterestsCsv = ReadCsvRequired(dataDirectory, "PlaceInterest_seed_draft.csv");

        var countryIsoByCsvId = countriesCsv.ToDictionary(
            r => Field(r, "id"), r => Field(r, "iso_code"), StringComparer.Ordinal);
        var currencyIsoByCsvId = currenciesCsv.ToDictionary(
            r => Field(r, "id"), r => Field(r, "iso_code"), StringComparer.Ordinal);
        var placeCategoryCodeByCsvId = placeCategoriesCsv.ToDictionary(
            r => Field(r, "id"), r => Field(r, "code"), StringComparer.Ordinal);
        var costCategoryCodeByCsvId = costCategoriesCsv.ToDictionary(
            r => Field(r, "id"), r => Field(r, "code"), StringComparer.Ordinal);
        var destinationNameByCsvId = destinationsCsv.ToDictionary(
            r => Field(r, "id"), r => Field(r, "name"), StringComparer.Ordinal);

        // ---------------- Countries ----------------
        var countries = await db.Countries.AsNoTracking()
            .ToDictionaryAsync(x => x.IsoCode, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var addedCountries = new List<(string IsoCode, Country Entity)>();

        foreach (var row in countriesCsv)
        {
            var isoCode = Field(row, "iso_code");
            if (isoCode.Length == 0)
                throw new InvalidOperationException("Country.csv contains a row with an empty iso_code.");

            if (countries.ContainsKey(isoCode))
                continue;

            var entity = new Country { IsoCode = isoCode, Name = Field(row, "name") };
            db.Countries.Add(entity);
            addedCountries.Add((isoCode, entity));
        }

        if (addedCountries.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var (isoCode, entity) in addedCountries)
                countries[isoCode] = entity.Id;
        }

        // ---------------- Currencies ----------------
        var currencies = await db.Currencies.AsNoTracking()
            .ToDictionaryAsync(x => x.IsoCode, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var addedCurrencies = new List<(string IsoCode, Currency Entity)>();

        foreach (var row in currenciesCsv)
        {
            var isoCode = Field(row, "iso_code");
            if (isoCode.Length == 0)
                throw new InvalidOperationException("Currency.csv contains a row with an empty iso_code.");

            if (currencies.ContainsKey(isoCode))
                continue;

            var entity = new Currency { IsoCode = isoCode, Symbol = Field(row, "symbol") };
            db.Currencies.Add(entity);
            addedCurrencies.Add((isoCode, entity));
        }

        if (addedCurrencies.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var (isoCode, entity) in addedCurrencies)
                currencies[isoCode] = entity.Id;
        }

        // ---------------- Code/Label reference tables ----------------
        var placeCategoryIds = await db.PlaceCategories.AsNoTracking()
            .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var addedPlaceCategories = new List<(string Code, PlaceCategory Entity)>();

        foreach (var row in placeCategoriesCsv)
        {
            var code = Field(row, "code");
            if (code.Length == 0)
                throw new InvalidOperationException("PlaceCategory.csv contains a row with an empty code.");
            if (placeCategoryIds.ContainsKey(code))
                continue;

            var entity = new PlaceCategory { Code = code, Label = Field(row, "label") };
            db.PlaceCategories.Add(entity);
            addedPlaceCategories.Add((code, entity));
        }

        if (addedPlaceCategories.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var (code, entity) in addedPlaceCategories)
                placeCategoryIds[code] = entity.Id;
        }

        var costCategoryIds = await db.CostCategories.AsNoTracking()
            .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var addedCostCategories = new List<(string Code, CostCategory Entity)>();

        foreach (var row in costCategoriesCsv)
        {
            var code = Field(row, "code");
            if (code.Length == 0)
                throw new InvalidOperationException("CostCategory.csv contains a row with an empty code.");
            if (costCategoryIds.ContainsKey(code))
                continue;

            var entity = new CostCategory { Code = code, Label = Field(row, "label") };
            db.CostCategories.Add(entity);
            addedCostCategories.Add((code, entity));
        }

        if (addedCostCategories.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var (code, entity) in addedCostCategories)
                costCategoryIds[code] = entity.Id;
        }

        var interestCategoryIds = await db.InterestCategories.AsNoTracking()
            .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var addedInterestCategories = new List<(string Code, InterestCategory Entity)>();

        foreach (var row in interestCategoriesCsv)
        {
            var code = Field(row, "code");
            if (code.Length == 0)
                throw new InvalidOperationException("InterestCategory.csv contains a row with an empty code.");
            if (interestCategoryIds.ContainsKey(code))
                continue;

            var entity = new InterestCategory { Code = code, Label = Field(row, "label") };
            db.InterestCategories.Add(entity);
            addedInterestCategories.Add((code, entity));
        }

        if (addedInterestCategories.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var (code, entity) in addedInterestCategories)
                interestCategoryIds[code] = entity.Id;
        }

        // ---------------- Destinations ----------------
        var existingDestinations = await db.Destinations.AsNoTracking()
            .Select(x => new { x.Id, x.CountryId, x.Name })
            .ToListAsync(cancellationToken);

        var destinationIdByKey = existingDestinations.ToDictionary(
            x => NaturalKey(x.CountryId, x.Name), x => x.Id, StringComparer.OrdinalIgnoreCase);
        var destinationIdByName = existingDestinations.ToDictionary(
            x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var addedDestinations = new List<(string Name, Destination Entity)>();

        foreach (var row in destinationsCsv)
        {
            var csvId = Field(row, "id");
            var name = Field(row, "name");
            if (name.Length == 0)
                throw new InvalidOperationException("Destination.csv contains a row with an empty name.");

            if (!countryIsoByCsvId.TryGetValue(csvId, out var countryIso) ||
                !countries.TryGetValue(countryIso, out var countryId))
            {
                throw new InvalidOperationException(
                    $"Destination.csv row '{name}' references country_id '{csvId}' with no Country.csv row.");
            }

            if (destinationIdByKey.ContainsKey(NaturalKey(countryId, name)))
                continue;

            var entity = new Destination
            {
                CountryId = countryId,
                Name = name,
                Description = Field(row, "description"),
                Latitude = ParseOptionalDecimal(Field(row, "latitude"), $"Destination '{name}' latitude"),
                Longitude = ParseOptionalDecimal(Field(row, "longitude"), $"Destination '{name}' longitude"),
                IsSupported = ParseBool(Field(row, "is_supported"), $"Destination '{name}' is_supported")
            };

            db.Destinations.Add(entity);
            addedDestinations.Add((name, entity));
        }

        if (addedDestinations.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var (name, entity) in addedDestinations)
            {
                destinationIdByKey[NaturalKey(entity.CountryId, entity.Name)] = entity.Id;
                destinationIdByName.TryAdd(name, entity.Id);
            }
        }

        // ---------------- Places ----------------
        var existingPlaceKeys = (await db.Places.AsNoTracking()
                .Select(x => new { x.DestinationId, x.Name })
                .ToListAsync(cancellationToken))
            .Select(x => NaturalKey(x.DestinationId, x.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedPlaces = new List<Place>();

        foreach (var row in placesCsv)
        {
            var name = Field(row, "name");
            if (name.Length == 0)
                throw new InvalidOperationException("Place.csv contains a row with an empty name.");

            var destinationCsvId = Field(row, "destination_id");
            if (!destinationNameByCsvId.TryGetValue(destinationCsvId, out var destinationName) ||
                !destinationIdByName.TryGetValue(destinationName, out var destinationId))
            {
                throw new InvalidOperationException(
                    $"Place.csv row '{name}' references destination_id '{destinationCsvId}' " +
                    "with no Destination.csv row.");
            }

            var placeCategoryCsvId = Field(row, "place_category_id");
            if (!placeCategoryCodeByCsvId.TryGetValue(placeCategoryCsvId, out var placeCategoryCode) ||
                !placeCategoryIds.TryGetValue(placeCategoryCode, out var placeCategoryId))
            {
                throw new InvalidOperationException(
                    $"Place.csv row '{name}' references place_category_id '{placeCategoryCsvId}' " +
                    "with no PlaceCategory.csv row.");
            }

            var costCategoryCsvId = Field(row, "cost_category_id");
            if (!costCategoryCodeByCsvId.TryGetValue(costCategoryCsvId, out var costCategoryCode) ||
                !costCategoryIds.TryGetValue(costCategoryCode, out var costCategoryId))
            {
                throw new InvalidOperationException(
                    $"Place.csv row '{name}' references cost_category_id '{costCategoryCsvId}' " +
                    "with no CostCategory.csv row.");
            }

            var currencyCsvId = Field(row, "currency_id");
            if (!currencyIsoByCsvId.TryGetValue(currencyCsvId, out var currencyIso) ||
                !currencies.TryGetValue(currencyIso, out var currencyId))
            {
                throw new InvalidOperationException(
                    $"Place.csv row '{name}' references currency_id '{currencyCsvId}' " +
                    "with no Currency.csv row.");
            }

            if (existingPlaceKeys.Contains(NaturalKey(destinationId, name)))
                continue;

            var price = ParseDecimal(Field(row, "reference_price"), $"Place '{name}' reference_price");
            if (price < 0)
                throw new InvalidOperationException($"Place '{name}' reference_price must be >= 0.");

            var entity = new Place
            {
                DestinationId = destinationId,
                PlaceCategoryId = placeCategoryId,
                Name = name,
                Description = Field(row, "description"),
                ReferencePrice = price,
                CurrencyId = currencyId,
                CostCategoryId = costCategoryId,
                PriceUpdatedAt = ParseTimestamp(
                    Field(row, "price_updated_at"), $"Place '{name}' price_updated_at"),
                IsActive = ParseBool(Field(row, "is_active"), $"Place '{name}' is_active")
            };

            db.Places.Add(entity);
            existingPlaceKeys.Add(NaturalKey(destinationId, name));
            addedPlaces.Add(entity);
        }

        if (addedPlaces.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        // ---------------- Place <-> Interest links ----------------
        var placeIdByKey = (await db.Places.AsNoTracking()
                .Select(x => new { x.Id, x.DestinationId, x.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                x => NaturalKey(x.DestinationId, x.Name),
                x => x.Id,
                StringComparer.OrdinalIgnoreCase);

        var existingLinks = (await db.PlaceInterests.AsNoTracking()
                .Select(x => new { x.PlaceId, x.InterestCategoryId })
                .ToListAsync(cancellationToken))
            .Select(x => (x.PlaceId, x.InterestCategoryId))
            .ToHashSet();

        var addedLinks = 0;

        foreach (var row in placeInterestsCsv)
        {
            var destinationName = Field(row, "destination_name");
            var placeName = Field(row, "place_name");
            var interestCode = Field(row, "interest_category_code");

            if (!destinationIdByName.TryGetValue(destinationName, out var destinationId) ||
                !placeIdByKey.TryGetValue(NaturalKey(destinationId, placeName), out var placeId))
            {
                // Same behavior as Seed_place_interests.py: skip rows whose place is
                // not present rather than failing provisioning of everything else.
                continue;
            }

            if (!interestCategoryIds.TryGetValue(interestCode, out var interestCategoryId))
                throw new InvalidOperationException(
                    $"PlaceInterest row '{destinationName}/{placeName}' references unknown " +
                    $"interest_category_code '{interestCode}'.");

            if (!existingLinks.Add((placeId, interestCategoryId)))
                continue;

            db.PlaceInterests.Add(new PlaceInterest
            {
                PlaceId = placeId,
                InterestCategoryId = interestCategoryId
            });
            addedLinks++;
        }

        if (addedLinks > 0)
            await db.SaveChangesAsync(cancellationToken);

        // ---------------- Exchange rates (placeholder, insert-if-missing) ----------------
        var existingRateCurrencyIds = (await db.ExchangeRates.AsNoTracking()
                .Select(x => x.CurrencyId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var addedRates = 0;

        foreach (var (isoCode, rateToUsd) in PlaceholderExchangeRates)
        {
            if (!currencies.TryGetValue(isoCode, out var currencyId))
                continue;
            if (!existingRateCurrencyIds.Add(currencyId))
                continue;

            db.ExchangeRates.Add(new ExchangeRate
            {
                CurrencyId = currencyId,
                RateToUsd = rateToUsd,
                UpdatedAt = DateTime.UtcNow
            });
            addedRates++;
        }

        if (addedRates > 0)
            await db.SaveChangesAsync(cancellationToken);

        return new SeedSummary(
            Skipped: false,
            DataDirectory: dataDirectory,
            CountriesAdded: addedCountries.Count,
            CurrenciesAdded: addedCurrencies.Count,
            PlaceCategoriesAdded: addedPlaceCategories.Count,
            CostCategoriesAdded: addedCostCategories.Count,
            InterestCategoriesAdded: addedInterestCategories.Count,
            DestinationsAdded: addedDestinations.Count,
            PlacesAdded: addedPlaces.Count,
            PlaceInterestsAdded: addedLinks,
            ExchangeRatesAdded: addedRates);
    }

    private static List<Dictionary<string, string>> ReadCsvRequired(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"Curated dataset file missing: '{path}'. The curated-data folder is present " +
                "but incomplete - restore the file or point AI:CuratedDataPath at a full dataset.");

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var rows = new List<Dictionary<string, string>>(Math.Max(0, lines.Length - 1));
        if (lines.Length == 0)
            return rows;

        var header = ParseCsvLine(lines[0]);
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, string>(header.Count, StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < header.Count; c++)
                row[header[c].Trim()] = c < values.Count ? values[c] : string.Empty;
            rows.Add(row);
        }

        return rows;
    }

    private static string Field(Dictionary<string, string> row, string name)
        => row.TryGetValue(name, out var value) ? value.Trim() : string.Empty;

    /// <summary>
    /// Natural-key composite used for insert-if-missing matching. The id part is
    /// always numeric and never contains the separator, so keys cannot collide.
    /// </summary>
    private static string NaturalKey(long id, string name) => id + "|" + name;

    private static decimal ParseDecimal(string value, string context)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            throw new InvalidOperationException($"{context}: invalid decimal '{value}'.");
        return result;
    }

    private static decimal? ParseOptionalDecimal(string value, string context)
        => string.IsNullOrWhiteSpace(value) ? null : ParseDecimal(value, context);

    private static DateTime ParseTimestamp(string value, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{context}: value is required.");

        foreach (var format in new[] { "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss" })
        {
            if (DateTime.TryParseExact(
                    value, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                return parsed;
        }

        throw new InvalidOperationException($"{context}: unparseable timestamp '{value}'.");
    }

    private static bool ParseBool(string value, string context)
    {
        value = value.Trim().ToLowerInvariant();
        return value switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => throw new InvalidOperationException($"{context}: expected boolean, got '{value}'.")
        };
    }

    /// <summary>Quote-aware CSV line parser (same convention as ExtraAiContextReader).</summary>
    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (ch == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}

/// <summary>Row counts inserted by one provisioning run (all zero when already provisioned).</summary>
public sealed record SeedSummary(
    bool Skipped,
    string DataDirectory,
    int CountriesAdded = 0,
    int CurrenciesAdded = 0,
    int PlaceCategoriesAdded = 0,
    int CostCategoriesAdded = 0,
    int InterestCategoriesAdded = 0,
    int DestinationsAdded = 0,
    int PlacesAdded = 0,
    int PlaceInterestsAdded = 0,
    int ExchangeRatesAdded = 0)
{
    public bool AnyRowsInserted =>
        CountriesAdded + CurrenciesAdded + PlaceCategoriesAdded + CostCategoriesAdded +
        InterestCategoriesAdded + DestinationsAdded + PlacesAdded + PlaceInterestsAdded +
        ExchangeRatesAdded > 0;
}
