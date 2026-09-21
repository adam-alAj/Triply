using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration.Dtos;
using Triply.Api.Modules.Cost;
using Triply.Api.Modules.Cost.Dtos;
using Triply.Api.Modules.Currency;
using Triply.Api.Modules.Itinerary.Dtos;
using Triply.Api.Modules.Trip;

namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// Orchestrates the full AI generation lifecycle (Architecture §9 sequence diagram):
/// fetch dataset context -> build prompt -> call Gemini -> validate -> persist
/// Itinerary/Days/Items + CostEstimate rows, or retry a bounded number of times,
/// or fail cleanly with an auditable AIGeneration row.
///
/// v2.0.0 — aligned with AI JSON Schema Contract v2.0.0.
/// Uses place_name (string) for grounding; never uses PlaceId from model output.
/// Supports both DESTINATION_FIRST and BUDGET_FIRST planning modes.
/// </summary>
public interface IAiOrchestrationService
{
    Task<AiGenerationResult> GenerateItineraryAsync(Guid tripId, CancellationToken cancellationToken = default);

    Task<AiGenerationResult> RegeneratePartialAsync(
        Guid tripId,
        GenerateItineraryRequest request,
        CancellationToken cancellationToken = default);
}

public class AiOrchestrationService : IAiOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApplicationDbContext _db;
    private readonly IGeminiClient _geminiClient;
    private readonly IItineraryPromptBuilder _promptBuilder;
    private readonly IItineraryValidator _validator;
    private readonly ICostAggregationService _costAggregationService;
    private readonly GeminiOptions _options;
    private readonly ILogger<AiOrchestrationService> _logger;
    private readonly IExtraAiContextReader _extraAiContextReader;
    private readonly ICurrencyConversionService _currencyConversion;
    private readonly IHostEnvironment _environment;

    public AiOrchestrationService(
        ApplicationDbContext db,
        IGeminiClient geminiClient,
        IItineraryPromptBuilder promptBuilder,
        IItineraryValidator validator,
        ICostAggregationService costAggregationService,
        IOptions<GeminiOptions> options,
        ILogger<AiOrchestrationService> logger,
        IExtraAiContextReader extraAiContextReader,
        ICurrencyConversionService currencyConversion,
        IHostEnvironment environment)
    {
        _db = db;
        _geminiClient = geminiClient;
        _promptBuilder = promptBuilder;
        _validator = validator;
        _costAggregationService = costAggregationService;
        _options = options.Value;
        _logger = logger;
        _extraAiContextReader = extraAiContextReader;
        _currencyConversion = currencyConversion;
        _environment = environment;
    }

    public async Task<AiGenerationResult> GenerateItineraryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await _db.Trips
            .Include(t => t.TripInterests)
                .ThenInclude(ti => ti.InterestCategory)
            .Include(t => t.BudgetCurrency)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
            throw new KeyNotFoundException("Trip does not exist.");

        if (trip.PlanningMode == "DESTINATION_FIRST" && trip.DestinationId is null)
            throw new InvalidOperationException(
                "Destination-first trips require a confirmed destination before generation.");

        if (trip.PlanningMode == "BUDGET_FIRST" && trip.DestinationId is null)
            throw new InvalidOperationException(
                "Budget-first trips require the user to select one of the suggested destinations before generation.");

        if (!TripLifecycle.CanTransition(trip.Status, TripLifecycle.Generating) && trip.Status != TripLifecycle.Generating)
            throw new InvalidOperationException($"Trip in status '{trip.Status}' cannot start generation.");

        if (trip.Status != TripLifecycle.Generating)
            TripLifecycle.Transition(trip, TripLifecycle.Generating);
var trackerDebug = _db.ChangeTracker
    .Entries<ItineraryItem>()
    .Select(e => new
    {
        Id = e.Entity.Id,
        State = e.State.ToString(),
        DayId = e.Entity.ItineraryDayId,
        PlaceId = e.Entity.PlaceId,
        TimeSlot = e.Entity.TimeSlot,
        IsAiGenerated = e.Entity.IsAiGenerated
    })
    .ToList();

_logger.LogWarning(
    "DAY regeneration ItineraryItem tracker: {@Items}",
    trackerDebug);
        await _db.SaveChangesAsync(cancellationToken);

        // --- Fetch dataset context (active places; destination-scoped for destination-first) ---
        var placeQuery = _db.Places
            .AsNoTracking()
            .Include(p => p.PlaceCategory)
            .Include(p => p.Currency)
            .Include(p => p.Destination)
            .Where(p => p.IsActive);

        if (trip.PlanningMode == "DESTINATION_FIRST" ||
            (trip.PlanningMode == "BUDGET_FIRST" && trip.DestinationId.HasValue))
            placeQuery = placeQuery.Where(p => p.DestinationId == trip.DestinationId!.Value);

        var places = await placeQuery
            .Select(p => new PlaceContextDto
            {
                Id = p.Id,
                DestinationId = p.DestinationId,
                DestinationName = p.Destination.Name,
                Name = p.Name,
                Category = p.PlaceCategory.Code,
                ReferencePrice = p.ReferencePrice,
                Currency = p.Currency.IsoCode,
                CurrencyId = p.CurrencyId,
                BudgetTier = null
            })
            .ToListAsync(cancellationToken);

        if (trip.PlanningMode == "BUDGET_FIRST")
        {
            var budgetTiers = await _extraAiContextReader.ReadBudgetTiersAsync(cancellationToken);
            foreach (var place in places)
            {
                // Prefer stable Place.id keys; support place names for the AI team's CSV variant.
                budgetTiers.TryGetValue(place.Id.ToString(), out var tier);
                if (tier is null)
                    budgetTiers.TryGetValue(place.Name, out tier);
                place.BudgetTier = tier;
            }

            if (places.Any(p => string.IsNullOrWhiteSpace(p.BudgetTier)))
            {
                var missing = places.Count(p => string.IsNullOrWhiteSpace(p.BudgetTier));
                throw new InvalidOperationException(
                    $"Extra_AI_Context.csv does not provide budget_tier for {missing} active place(s). " +
                    "BUDGET_FIRST generation cannot safely continue without complete budget context.");
            }
        }

        if (places.Count == 0)
        {
            TripLifecycle.Transition(trip, TripLifecycle.Draft);
            await _db.SaveChangesAsync(cancellationToken);

            return new AiGenerationResult
            {
                Success = false,
                Status = "FAILED_ERROR",
                Errors = { "No active places are curated yet for this destination - cannot ground a generation." }
            };
        }

        var interestLabels = trip.TripInterests
            .Select(ti => ti.InterestCategory.Label)
            .ToList();

        var dayCount = trip.StartDate.HasValue && trip.EndDate.HasValue
            ? Math.Max(1, trip.EndDate.Value.DayNumber - trip.StartDate.Value.DayNumber + 1)
            : 3; // sensible default when dates are not yet fixed

        var inputSnapshot = JsonSerializer.Serialize(new
        {
            trip.DestinationId,
            trip.TravelerCount,
            trip.StartDate,
            trip.EndDate,
            Interests = interestLabels,
            DayCount = dayCount,
            trip.PlanningMode
        });

        var destinations = trip.PlanningMode == "BUDGET_FIRST"
            ? await _db.Destinations
                .AsNoTracking()
                .Where(d => d.IsSupported &&
                    (!trip.DestinationId.HasValue || d.Id == trip.DestinationId.Value))
                .Select(d => new DestinationContextDto { Name = d.Name, Description = d.Description })
                .OrderBy(d => d.Name)
                .ToListAsync(cancellationToken)
            : new List<DestinationContextDto>();

        var schemaPath = Path.Combine(_environment.ContentRootPath, "AI-Schemas", "triply-trip-plan-generation.schema.json");
        if (!File.Exists(schemaPath))
            throw new InvalidOperationException($"AI response schema was not found at '{schemaPath}'.");

        await using var schemaStream = File.OpenRead(schemaPath);
        using var responseSchema = await JsonDocument.ParseAsync(schemaStream, cancellationToken: cancellationToken);
        var systemInstruction = "You are Triply's backend itinerary generator. Return only data allowed by the supplied JSON schema. Never invent destinations or places, never output database IDs or prices, and use only the grounded dataset provided in the user prompt.";

        var maxAttempts = _options.MaxRetries + 1;
        var allErrors = new List<string>();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var aiGeneration = new AIGeneration
            {
                TripId = tripId,
                AttemptNumber = attempt,
                ModelProvider = _options.Model,
                InputSnapshot = inputSnapshot,
                Status = "PENDING"
            };
            _db.AIGenerations.Add(aiGeneration);
            await _db.SaveChangesAsync(cancellationToken);

            string rawText;
            try
            {
                var prompt = trip.PlanningMode == "BUDGET_FIRST"
                    ? _promptBuilder.BuildBudgetFirst(trip, places, destinations, interestLabels, dayCount)
                    : _promptBuilder.Build(trip, places, interestLabels, dayCount);

                rawText = await _geminiClient.GenerateJsonWithSchemaAsync(
                    prompt, systemInstruction, responseSchema, cancellationToken);
            }
            catch (GeminiApiException ex)
            {
                _logger.LogError(ex, "Gemini call failed on attempt {Attempt} for trip {TripId}", attempt, tripId);

                aiGeneration.Status = "FAILED_ERROR";
                aiGeneration.ValidationErrors = ex.Message;
                aiGeneration.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                allErrors.Add($"Attempt {attempt}: Gemini call failed - {ex.Message}");
                continue; // bounded retry also covers transient API failures
            }
            catch (InvalidOperationException ex)
            {
                // A config problem (e.g. Gemini:ApiKey not set) — not a transient
                // Gemini API failure, so retrying won't help. Without this catch,
                // the exception escapes GenerateItineraryAsync entirely and the
                // trip is left stuck at GENERATING forever (the status was
                // already committed above, and nothing else here reverts it).
                _logger.LogError(ex, "Generation aborted on attempt {Attempt} for trip {TripId}: {Message}", attempt, tripId, ex.Message);

                aiGeneration.Status = "FAILED_ERROR";
                aiGeneration.ValidationErrors = ex.Message;
                aiGeneration.CompletedAt = DateTime.UtcNow;
                TripLifecycle.Transition(trip, TripLifecycle.Draft);
                await _db.SaveChangesAsync(cancellationToken);

                return new AiGenerationResult
                {
                    Success = false,
                    AiGenerationId = aiGeneration.Id,
                    AttemptsUsed = attempt,
                    Status = "FAILED_ERROR",
                    Errors = { ex.Message }
                };
            }

            aiGeneration.RawOutput = rawText;

            GeminiItineraryOutputDto? output;
            try
            {
                output = JsonSerializer.Deserialize<GeminiItineraryOutputDto>(rawText, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Gemini output did not parse as JSON on attempt {Attempt}", attempt);
                output = null;
            }

            if (output is null)
            {
                aiGeneration.Status = "FAILED_VALIDATION";
                aiGeneration.ValidationErrors = "Response was not valid JSON against the agreed schema.";
                aiGeneration.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                allErrors.Add($"Attempt {attempt}: response was not valid JSON.");
                continue;
            }

            var validation = await _validator.ValidateAsync(trip, output, cancellationToken);

            if (!validation.IsValid)
            {
                aiGeneration.Status = "FAILED_VALIDATION";
                aiGeneration.ValidationErrors = string.Join(" | ", validation.Errors);
                aiGeneration.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                allErrors.AddRange(validation.Errors.Select(e => $"Attempt {attempt}: {e}"));
                continue; // FR-AI-002: reject and regenerate, never fabricate
            }

            // --- Valid — persist itinerary + cost estimates as one all-or-nothing transaction ---
            // Use the first destination option (DESTINATION_FIRST always has 1,
            // BUDGET_FIRST picks the first valid one — user selection comes later).
            var selectedOption = output.DestinationOptions.First();

            // Resolve place names to Place IDs for persistence
            var nameToPlace = places.ToDictionary(p => p.Name, p => p);
            var accommodationPlaceId = nameToPlace[selectedOption.Accommodation.PlaceName].Id;

            var selectedDestination = await _db.Destinations
                .FirstOrDefaultAsync(d => d.Name == selectedOption.DestinationName, cancellationToken);

            if (selectedDestination is null)
                throw new InvalidOperationException(
                    $"Generated destination '{selectedOption.DestinationName}' could not be resolved to the internal dataset.");

            if (trip.DestinationId is null)
                trip.DestinationId = selectedDestination.Id;

            var selectedPlaceLookup = places
                .Where(p => p.DestinationId == selectedDestination.Id)
                .ToDictionary(p => p.Name, p => p);

            if (!selectedPlaceLookup.ContainsKey(selectedOption.Accommodation.PlaceName))
                throw new InvalidOperationException(
                    $"Accommodation '{selectedOption.Accommodation.PlaceName}' is not grounded to the selected destination.");

            nameToPlace = selectedPlaceLookup;

            // Deterministic backend budget guard. The AI never supplies prices:
            // every amount is resolved from the authoritative Place.reference_price,
            // including accommodation nights.
            if (trip.BudgetAmount.HasValue && trip.BudgetCurrencyId.HasValue)
            {
                // Deterministically calculate every line from Place.reference_price,
                // convert each currency into the user's budget currency, and compare
                // the real total before anything is persisted. If the result is over
                // budget, this attempt is rejected and the bounded AI retry loop gets
                // another chance with the same budget constraint.
                var totalInBudgetCurrency = await CalculateGeneratedOptionCostInBudgetCurrencyAsync(
                    selectedOption,
                    nameToPlace,
                    trip.BudgetCurrencyId.Value,
                    cancellationToken);

                var budgetCheck = (
                    IsWithinBudget: totalInBudgetCurrency <= trip.BudgetAmount.Value,
                    TotalInBudgetCurrency: totalInBudgetCurrency);

                if (!budgetCheck.IsWithinBudget)
                {
                    aiGeneration.Status = "FAILED_VALIDATION";
                    aiGeneration.ValidationErrors =
                        $"Generated itinerary exceeds budget: {budgetCheck.TotalInBudgetCurrency:F2} " +
                        $"{trip.BudgetCurrency?.IsoCode ?? "budget currency"} > {trip.BudgetAmount.Value:F2}.";

                    aiGeneration.CompletedAt = DateTime.UtcNow;
                    if (trip.PlanningMode == "BUDGET_FIRST")
                        trip.DestinationId = null;
                    await _db.SaveChangesAsync(cancellationToken);
                    allErrors.Add(
                        $"Attempt {attempt}: generated itinerary exceeds the requested budget " +
                        $"({budgetCheck.TotalInBudgetCurrency:F2} > {trip.BudgetAmount.Value:F2}).");
                    continue;
                }
            }

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            // Remove any existing itinerary for this trip
            var existing = await _db.Itineraries
                .Include(i => i.Days)
                    .ThenInclude(d => d.Items)
                .FirstOrDefaultAsync(i => i.TripId == tripId, cancellationToken);

            if (existing is not null)
            {
                _db.ItineraryItems.RemoveRange(existing.Days.SelectMany(d => d.Items));
                _db.ItineraryDays.RemoveRange(existing.Days);
                await _db.SaveChangesAsync(cancellationToken);
                _db.Itineraries.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }

            var itinerary = new Entities.Itinerary
            {
                TripId = tripId,
                GeneratedAt = DateTime.UtcNow,
                AiGenerationId = aiGeneration.Id
            };

            // Persist accommodation as an ItineraryItem on day 1 (Backend convention per §4.3)
            var firstDay = selectedOption.Days.OrderBy(d => d.DayNumber).First();
            var accommodationPrice = nameToPlace[selectedOption.Accommodation.PlaceName].ReferencePrice;

            var accDay = new ItineraryDay
            {
                DayNumber = firstDay.DayNumber,
                Date = firstDay.Date
            };

            accDay.Items.Add(new ItineraryItem
            {
                PlaceId = accommodationPlaceId,
                TimeSlot = "MORNING", // Convention: accommodation placed in MORNING slot on day 1
                OrderIndex = 0, // Accommodation is a special item, not user-ordered
                EstimatedCost = accommodationPrice * selectedOption.Accommodation.Nights,
                Notes = $"Accommodation: {selectedOption.Accommodation.Nights} nights",
                IsAiGenerated = true
            });

            itinerary.Days.Add(accDay);

            // Persist daily itinerary items
            foreach (var dayDto in selectedOption.Days.OrderBy(d => d.DayNumber))
            {
                // Check if we already created this day (for accommodation)
                var existingDay = itinerary.Days.FirstOrDefault(d => d.DayNumber == dayDto.DayNumber);
                ItineraryDay day;

                if (existingDay is not null)
                {
                    day = existingDay;
                }
                else
                {
                    day = new ItineraryDay
                    {
                        DayNumber = dayDto.DayNumber,
                        Date = dayDto.Date
                    };
                    itinerary.Days.Add(day);
                }

                foreach (var itemDto in dayDto.Items.OrderBy(i => i.OrderIndex))
                {
                    var placeId = nameToPlace[itemDto.PlaceName].Id;
                    var referencePrice = nameToPlace[itemDto.PlaceName].ReferencePrice;

                    day.Items.Add(new ItineraryItem
                    {
                        PlaceId = placeId,
                        TimeSlot = itemDto.TimeSlot.ToUpperInvariant(),
                        OrderIndex = itemDto.OrderIndex,
                        EstimatedCost = referencePrice,
                        Notes = itemDto.Notes,
                        IsAiGenerated = true
                    });
                }
            }

            _db.Itineraries.Add(itinerary);

            aiGeneration.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // TASK48 — write CostEstimate rows from the itinerary just persisted.
            // This MUST succeed before the trip is considered "generated" - it
            // used to run after the trip was already transitioned to Generated
            // and committed, so a cost-aggregation failure (e.g. mismatched
            // place currencies) left the trip stuck in GENERATED with no cost
            // data and no way to retry (a fresh /generate call is only allowed
            // from DRAFT). Computing cost first and only THEN flipping the
            // trip's status keeps "GENERATED" meaning "fully generated,
            // including cost", and leaves the trip retry-able from DRAFT if
            // this step fails - the itinerary just written above gets cleanly
            // replaced by the "remove any existing itinerary" step above on
            // the next attempt.
            CostEstimateResponse costResult;
            try
            {
                costResult = await _costAggregationService.GenerateFromItineraryAsync(tripId, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Cost aggregation failed after generation on attempt {Attempt} for trip {TripId}", attempt, tripId);

                aiGeneration.Status = "FAILED_ERROR";
                aiGeneration.ValidationErrors = $"Cost aggregation failed: {ex.Message}";
                TripLifecycle.Transition(trip, TripLifecycle.Draft);
                await _db.SaveChangesAsync(cancellationToken);

                throw;
            }

            // Final persisted-cost guard. The cost service recomputes the exact
            // persisted itinerary from Place.reference_price; compare its result
            // once more before allowing GENERATED.
            if (trip.BudgetAmount.HasValue && trip.BudgetCurrencyId.HasValue)
            {
                var persistedCurrencyId = await _db.CostEstimates
                    .AsNoTracking()
                    .Where(x => x.TripId == tripId)
                    .Select(x => (long?)x.CurrencyId)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? trip.BudgetCurrencyId.Value;

                var totalInBudgetCurrency = await _currencyConversion.ConvertAsync(
                    costResult.TotalEstimatedCost,
                    persistedCurrencyId,
                    trip.BudgetCurrencyId.Value,
                    cancellationToken);

                if (totalInBudgetCurrency > trip.BudgetAmount.Value)
                {
                    // The same deterministic calculation already passed before
                    // persistence; reaching this branch means the persisted-cost
                    // service disagrees with the pre-persistence calculation.
                    // Fail closed instead of accepting an over-budget itinerary.
                    throw new InvalidOperationException(
                        $"Generated itinerary exceeds budget after cost aggregation: " +
                        $"{totalInBudgetCurrency:F2} > {trip.BudgetAmount.Value:F2}.");
                }
            }

            TripLifecycle.Transition(trip, TripLifecycle.Generated);
            trip.UpdatedAt = DateTime.UtcNow;
            trip.Version++;

            aiGeneration.Status = "SUCCEEDED";
            await _db.SaveChangesAsync(cancellationToken);

            return new AiGenerationResult
            {
                Success = true,
                Status = "SUCCEEDED",
                AiGenerationId = aiGeneration.Id,
                AttemptsUsed = attempt,
                TripVersion = trip.Version,
                Itinerary = ToItineraryResponse(itinerary, nameToPlace),
                Cost = costResult
            };
        }

        // Every attempt failed — never return a fabricated result (FR-AI-002).
        TripLifecycle.Transition(trip, TripLifecycle.Draft);
        await _db.SaveChangesAsync(cancellationToken);

        var lastFailedGeneration = await _db.AIGenerations
            .Where(g => g.TripId == tripId)
            .OrderByDescending(g => g.AttemptNumber)
            .FirstAsync(cancellationToken);

        return new AiGenerationResult
        {
            Success = false,
            Status = "FAILED_VALIDATION",
            AiGenerationId = lastFailedGeneration.Id,
            AttemptsUsed = maxAttempts,
            Errors = allErrors
        };
    }

    public async Task<AiGenerationResult> RegeneratePartialAsync(
        Guid tripId,
        GenerateItineraryRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = request.Scope?.Trim().ToUpperInvariant() ?? "FULL";
        if (scope is not "DAY" and not "ITEM")
            throw new ArgumentException("Partial regeneration scope must be DAY or ITEM.", nameof(request));

        var trip = await _db.Trips
            .AsNoTracking()
            .Include(t => t.TripInterests)
                .ThenInclude(ti => ti.InterestCategory)
            .Include(t => t.BudgetCurrency)
            .Include(t => t.Destination)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
            throw new KeyNotFoundException("Trip does not exist.");

        if (trip.Status == TripLifecycle.Archived || trip.Status == TripLifecycle.Generating)
            throw new InvalidOperationException($"Trip cannot be partially regenerated while status is {trip.Status}.");

        if (!request.ExpectedVersion.HasValue || request.ExpectedVersion.Value < 1)
            throw new ArgumentException("ExpectedVersion is required for partial regeneration.", nameof(request));

        if (request.ExpectedVersion.Value != trip.Version)
            throw new InvalidOperationException("Trip has been modified by another request.");

        if (scope == "DAY" && (!request.DayNumber.HasValue || request.DayNumber.Value <= 0))
            throw new ArgumentException("DayNumber is required for DAY regeneration.", nameof(request));

        if (scope == "ITEM" && !request.ItemId.HasValue)
            throw new ArgumentException("ItemId is required for ITEM regeneration.", nameof(request));

        var itinerary = await _db.Itineraries
            .Include(i => i.Days)
                .ThenInclude(d => d.Items)
                    .ThenInclude(i => i.Place)
                        .ThenInclude(p => p.PlaceCategory)
            .FirstOrDefaultAsync(i => i.TripId == tripId, cancellationToken);

        if (itinerary is null)
            throw new KeyNotFoundException("Trip does not have an itinerary to partially regenerate.");

        ItineraryDay? targetDay = null;
        ItineraryItem? targetItem = null;

        if (scope == "DAY")
        {
            targetDay = itinerary.Days.FirstOrDefault(d => d.DayNumber == request.DayNumber!.Value);
            if (targetDay is null)
                throw new KeyNotFoundException("The requested itinerary day does not exist.");
        }
        else
        {
            targetItem = itinerary.Days
                .SelectMany(d => d.Items)
                .FirstOrDefault(i => i.Id == request.ItemId!.Value);

            if (targetItem is null)
                throw new KeyNotFoundException("The requested itinerary item does not exist in this trip.");

            if (string.Equals(targetItem.Place.PlaceCategory.Code, "ACCOMMODATION", StringComparison.OrdinalIgnoreCase) ||
                (targetItem.Notes?.StartsWith("Accommodation:", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                throw new InvalidOperationException("Accommodation cannot be partially regenerated as an activity.");
            }

            targetDay = targetItem.ItineraryDay;
        }

        var placeQuery = _db.Places
            .AsNoTracking()
            .Include(p => p.PlaceCategory)
            .Include(p => p.Currency)
            .Include(p => p.Destination)
            .Where(p => p.IsActive);

        if (trip.PlanningMode == "DESTINATION_FIRST")
        {
            if (trip.DestinationId is null)
                throw new InvalidOperationException("Destination-first trips require a destination.");
            placeQuery = placeQuery.Where(p => p.DestinationId == trip.DestinationId.Value);
        }

        var places = await placeQuery
            .Select(p => new PlaceContextDto
            {
                Id = p.Id,
                DestinationId = p.DestinationId,
                DestinationName = p.Destination.Name,
                Name = p.Name,
                Category = p.PlaceCategory.Code,
                ReferencePrice = p.ReferencePrice,
                Currency = p.Currency.IsoCode,
                CurrencyId = p.CurrencyId,
                BudgetTier = null
            })
            .ToListAsync(cancellationToken);

        if (trip.PlanningMode == "BUDGET_FIRST")
        {
            var budgetTiers = await _extraAiContextReader.ReadBudgetTiersAsync(cancellationToken);
            foreach (var place in places)
            {
                budgetTiers.TryGetValue(place.Id.ToString(), out var tier);
                if (tier is null)
                    budgetTiers.TryGetValue(place.Name, out tier);
                place.BudgetTier = tier;
            }

            if (places.Any(p => string.IsNullOrWhiteSpace(p.BudgetTier)))
                throw new InvalidOperationException("BUDGET_FIRST partial regeneration requires complete budget_tier context.");
        }

        var interestLabels = trip.TripInterests.Select(ti => ti.InterestCategory.Label).ToList();
        var dayCount = trip.StartDate.HasValue && trip.EndDate.HasValue
            ? Math.Max(1, trip.EndDate.Value.DayNumber - trip.StartDate.Value.DayNumber + 1)
            : Math.Max(1, itinerary.Days.Count);

        var destinations = trip.PlanningMode == "BUDGET_FIRST"
            ? await _db.Destinations
                .AsNoTracking()
                .Where(d => d.IsSupported)
                .Select(d => new DestinationContextDto { Name = d.Name, Description = d.Description })
                .OrderBy(d => d.Name)
                .ToListAsync(cancellationToken)
            : new List<DestinationContextDto>();

        var schemaPath = Path.Combine(_environment.ContentRootPath, "AI-Schemas", "triply-trip-plan-generation.schema.json");
        if (!File.Exists(schemaPath))
            throw new InvalidOperationException($"AI response schema was not found at '{schemaPath}'.");

        await using var schemaStream = File.OpenRead(schemaPath);
        using var responseSchema = await JsonDocument.ParseAsync(schemaStream, cancellationToken: cancellationToken);

        var basePrompt = trip.PlanningMode == "BUDGET_FIRST"
            ? _promptBuilder.BuildBudgetFirst(trip, places, destinations, interestLabels, dayCount)
            : _promptBuilder.Build(trip, places, interestLabels, dayCount);

        var targetInstruction = scope == "DAY"
            ? $"This is a PARTIAL REGENERATION request. Regenerate only day_number {targetDay!.DayNumber}. The backend will preserve every other day. Keep the requested day consistent with the existing trip dates."
            : $"This is a PARTIAL REGENERATION request. Generate a replacement activity for the existing day_number {targetDay!.DayNumber}. Prefer the same time slot ({targetItem!.TimeSlot}) and do not generate accommodation. The backend will replace only that one activity and preserve everything else.";

        var prompt = basePrompt + "\n\n" + targetInstruction;
        var inputSnapshot = JsonSerializer.Serialize(new
        {
            trip.DestinationId,
            trip.TravelerCount,
            trip.StartDate,
            trip.EndDate,
            Interests = interestLabels,
            DayCount = dayCount,
            trip.PlanningMode,
            Scope = scope,
            request.DayNumber,
            request.ItemId,
            request.ExpectedVersion
        });

        var allErrors = new List<string>();
        var maxAttempts = _options.MaxRetries + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var aiGeneration = new AIGeneration
            {
                TripId = tripId,
                AttemptNumber = attempt,
                ModelProvider = _options.Model,
                InputSnapshot = inputSnapshot,
                Status = "PENDING"
            };
            _db.AIGenerations.Add(aiGeneration);
            await _db.SaveChangesAsync(cancellationToken);

            string rawText;
            try
            {
                rawText = await _geminiClient.GenerateJsonWithSchemaAsync(
                    prompt,
                    "You are Triply's backend partial-regeneration assistant. Return only data allowed by the supplied JSON schema and use only the grounded dataset.",
                    responseSchema,
                    cancellationToken);
            }
            catch (GeminiApiException ex)
            {
                aiGeneration.Status = "FAILED_ERROR";
                aiGeneration.ValidationErrors = ex.Message;
                aiGeneration.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                allErrors.Add($"Attempt {attempt}: Gemini call failed - {ex.Message}");
                continue;
            }

            aiGeneration.RawOutput = rawText;
            GeminiItineraryOutputDto? output;
            try
            {
                output = JsonSerializer.Deserialize<GeminiItineraryOutputDto>(rawText, JsonOptions);
            }
            catch (JsonException)
            {
                output = null;
            }

            if (output is null)
            {
                aiGeneration.Status = "FAILED_VALIDATION";
                aiGeneration.ValidationErrors = "Response was not valid JSON against the agreed schema.";
                aiGeneration.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                allErrors.Add($"Attempt {attempt}: response was not valid JSON.");
                continue;
            }

            var validation = await _validator.ValidateAsync(trip, output, cancellationToken);
            if (!validation.IsValid)
            {
                aiGeneration.Status = "FAILED_VALIDATION";
                aiGeneration.ValidationErrors = string.Join(" | ", validation.Errors);
                aiGeneration.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                allErrors.AddRange(validation.Errors.Select(e => $"Attempt {attempt}: {e}"));
                continue;
            }

            var selectedOption = trip.DestinationId.HasValue
                ? output.DestinationOptions.FirstOrDefault(o =>
                    places.Any(p => p.DestinationId == trip.DestinationId.Value &&
                                    string.Equals(p.DestinationName, o.DestinationName, StringComparison.Ordinal)))
                  ?? output.DestinationOptions.First()
                : output.DestinationOptions.First();

            var selectedDay = selectedOption.Days.FirstOrDefault(d => d.DayNumber == targetDay!.DayNumber);
            if (selectedDay is null)
            {
                aiGeneration.Status = "FAILED_VALIDATION";
                aiGeneration.ValidationErrors = $"Generated output did not contain target day {targetDay!.DayNumber}.";
                aiGeneration.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                allErrors.Add($"Attempt {attempt}: generated output did not contain target day {targetDay.DayNumber}.");
                continue;
            }

            var placeLookup = places
                .Where(p => p.DestinationName == selectedOption.DestinationName)
                .ToDictionary(p => p.Name, p => p);

            if (scope == "ITEM")
            {
                var replacement = selectedDay.Items
                    .FirstOrDefault(i => string.Equals(i.TimeSlot, targetItem!.TimeSlot, StringComparison.OrdinalIgnoreCase))
                    ?? selectedDay.Items.FirstOrDefault();

                if (replacement is null || !placeLookup.ContainsKey(replacement.PlaceName))
                {
                    aiGeneration.Status = "FAILED_VALIDATION";
                    aiGeneration.ValidationErrors = "Generated target day did not contain a grounded replacement activity.";
                    aiGeneration.CompletedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                    allErrors.Add($"Attempt {attempt}: no grounded replacement activity was generated.");
                    continue;
                }

                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                // Claim the expected Trip version before mutating any itinerary rows.
                // The row update holds the write lock until this transaction commits,
                // so another writer cannot slip in between the concurrency check and
                // the partial replacement.
                var nextVersion = request.ExpectedVersion.Value + 1;
                var updated = await _db.Trips
                    .Where(t => t.Id == tripId && t.Version == request.ExpectedVersion.Value)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(t => t.Status, TripLifecycle.Modified)
                        .SetProperty(t => t.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(t => t.Version, nextVersion), cancellationToken);

                if (updated != 1)
                    throw new DbUpdateConcurrencyException(
                        "Trip has been modified by another request.");

               var oldItemId = targetItem!.Id;
var targetDayId = targetItem.ItineraryDayId;

_db.ItineraryItems.Remove(targetItem);
targetDay.Items.Remove(targetItem);

var newItem = new ItineraryItem
{
    ItineraryDayId = targetDayId,
    PlaceId = placeLookup[replacement.PlaceName].Id,
    TimeSlot = replacement.TimeSlot.ToUpperInvariant(),
    OrderIndex = targetItem.OrderIndex,
    EstimatedCost = placeLookup[replacement.PlaceName].ReferencePrice,
    Notes = replacement.Notes,
    IsAiGenerated = true,
    ModifiedAt = null
};

_db.ItineraryItems.Add(newItem);

aiGeneration.Status = "SUCCEEDED";
aiGeneration.CompletedAt = DateTime.UtcNow;

await _db.SaveChangesAsync(cancellationToken);
await transaction.CommitAsync(cancellationToken);

                // ExecuteUpdate bypasses EF's change tracker. Clear the stale
                // tracked Trip before cost aggregation uses this DbContext.
                _db.ChangeTracker.Clear();
                var cost = await _costAggregationService.GenerateFromItineraryAsync(tripId, cancellationToken);
                return new AiGenerationResult
                {
                    Success = true,
                    Status = "SUCCEEDED",
                    AiGenerationId = aiGeneration.Id,
                    AttemptsUsed = attempt,
                    TripVersion = request.ExpectedVersion.Value + 1,
                    Itinerary = ToItineraryResponse(itinerary, placeLookup),
                    Cost = cost
                };
            }

            await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken))
            {
                // Claim the expected Trip version before changing the target day.
                // This makes the version check and the replacement one atomic unit.
                var nextVersion = request.ExpectedVersion.Value + 1;
                var updated = await _db.Trips
                    .Where(t => t.Id == tripId && t.Version == request.ExpectedVersion.Value)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(t => t.Status, TripLifecycle.Modified)
                        .SetProperty(t => t.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(t => t.Version, nextVersion), cancellationToken);

                if (updated != 1)
                    throw new DbUpdateConcurrencyException(
                        "Trip has been modified by another request.");

var accommodationItems = targetDay!.Items
    .Where(i =>
        string.Equals(
            i.Place.PlaceCategory.Code,
            "ACCOMMODATION",
            StringComparison.OrdinalIgnoreCase) ||
        (i.Notes?.StartsWith(
            "Accommodation:",
            StringComparison.OrdinalIgnoreCase) ?? false))
    .ToList();

// Delete only non-accommodation items directly from the database.
// Do not keep deleted items tracked while rebuilding the day.
var nonAccommodationIds = targetDay.Items
    .Where(i => !accommodationItems.Contains(i))
    .Select(i => i.Id)
    .ToList();

if (nonAccommodationIds.Count > 0)
{
    await _db.ItineraryItems
        .Where(i => nonAccommodationIds.Contains(i.Id))
        .ExecuteDeleteAsync(cancellationToken);
}

// Detach all old non-accommodation entities from EF tracking.
foreach (var item in targetDay.Items
             .Where(i => !accommodationItems.Contains(i))
             .ToList())
{
    _db.Entry(item).State = EntityState.Detached;
}

// Remove the deleted items from the in-memory navigation collection.
foreach (var item in targetDay.Items
             .Where(i => !accommodationItems.Contains(i))
             .ToList())
{
    targetDay.Items.Remove(item);
}

// Add the newly generated items as completely new entities.
foreach (var itemDto in selectedDay.Items.OrderBy(i => i.OrderIndex))
{
    if (!placeLookup.TryGetValue(itemDto.PlaceName, out var place))
        continue;

   var newItem = new ItineraryItem
{
    ItineraryDayId = targetDay.Id,
    PlaceId = place.Id,
    TimeSlot = itemDto.TimeSlot.ToUpperInvariant(),
    OrderIndex = itemDto.OrderIndex,
    EstimatedCost = place.ReferencePrice,
    Notes = itemDto.Notes,
    IsAiGenerated = true,
    ModifiedAt = null
};

_db.ItineraryItems.Add(newItem);
}

aiGeneration.Status = "SUCCEEDED";
aiGeneration.CompletedAt = DateTime.UtcNow;

try
{
    await _db.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateConcurrencyException ex)
{
    var details = ex.Entries
        .Select(e =>
        {
            var id = e.Properties
                .FirstOrDefault(p => p.Metadata.Name == "Id")
                ?.CurrentValue;

            var modifiedProperties = e.Properties
                .Where(p => p.IsModified)
                .Select(p => p.Metadata.Name);

            return $"{e.Entity.GetType().Name}" +
                   $":State={e.State}" +
                   $":Id={id}" +
                   $":Modified=[{string.Join(",", modifiedProperties)}]";
        });

    throw new DbUpdateConcurrencyException(
        "DAY partial regeneration concurrency conflict. " +
        string.Join(" | ", details),
        ex);
}
await transaction.CommitAsync(cancellationToken);
            }

            _db.ChangeTracker.Clear();
            var dayCost = await _costAggregationService.GenerateFromItineraryAsync(tripId, cancellationToken);
            return new AiGenerationResult
            {
                Success = true,
                Status = "SUCCEEDED",
                AiGenerationId = aiGeneration.Id,
                AttemptsUsed = attempt,
                TripVersion = request.ExpectedVersion.Value + 1,
                Itinerary = ToItineraryResponse(itinerary, placeLookup),
                Cost = dayCost
            };
        }

        return new AiGenerationResult
        {
            Success = false,
            Status = "FAILED_VALIDATION",
            AttemptsUsed = maxAttempts,
            Errors = allErrors
        };
    }

    private async Task<decimal> CalculateGeneratedOptionCostInBudgetCurrencyAsync(
        GeminiDestinationOptionDto option,
        IReadOnlyDictionary<string, PlaceContextDto> places,
        long budgetCurrencyId,
        CancellationToken cancellationToken)
    {
        // Keep costs in their stored/native currencies first. This preserves the
        // team's currency decision: database prices are never rewritten. Only the
        // budget comparison converts them to the user's budget currency.
        var totalsByCurrency = new Dictionary<long, decimal>();

        void Add(decimal amount, long currencyId)
        {
            totalsByCurrency[currencyId] =
                totalsByCurrency.GetValueOrDefault(currencyId) + amount;
        }

        var accommodationPlace = places[option.Accommodation.PlaceName];
        Add(
            accommodationPlace.ReferencePrice * option.Accommodation.Nights,
            accommodationPlace.CurrencyId);

        foreach (var item in option.Days.SelectMany(d => d.Items ?? []))
        {
            var place = places[item.PlaceName];
            Add(place.ReferencePrice, place.CurrencyId);
        }

        var converted = await _currencyConversion.ConvertManyAsync(
            totalsByCurrency,
            budgetCurrencyId,
            cancellationToken);

        return converted.Values.Sum();
    }

    private static ItineraryResponse ToItineraryResponse(
        Entities.Itinerary itinerary,
        Dictionary<string, PlaceContextDto> placeLookup)
    {
        return new ItineraryResponse
        {
            Id = itinerary.Id,
            TripId = itinerary.TripId,
            GeneratedAt = itinerary.GeneratedAt,
            Days = itinerary.Days
                .OrderBy(d => d.DayNumber)
                .Select(d => new ItineraryDayResponse
                {
                    Id = d.Id,
                    DayNumber = d.DayNumber,
                    Date = d.Date,
                    Items = d.Items
                        .OrderBy(i => i.OrderIndex)
                        .Select(i => new ItineraryItemResponse
                        {
                            Id = i.Id,
                            PlaceId = i.PlaceId,
                            PlaceName = placeLookup.Values
                                .FirstOrDefault(p => p.Id == i.PlaceId)?.Name ?? string.Empty,
                            TimeSlot = i.TimeSlot,
                            OrderIndex = i.OrderIndex,
                            EstimatedCost = i.EstimatedCost,
                            Notes = i.Notes,
                            IsAiGenerated = i.IsAiGenerated,
                            ModifiedAt = i.ModifiedAt
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}