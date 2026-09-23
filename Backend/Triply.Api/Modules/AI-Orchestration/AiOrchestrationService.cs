using System.Text.Json;
using System.Text.Json.Serialization;
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
    Task<AiGenerationResult> GenerateItineraryAsync(
        Guid tripId,
        GenerateItineraryRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<AiGenerationResult> RegeneratePartialAsync(
        Guid tripId,
        GenerateItineraryRequest request,
        CancellationToken cancellationToken = default);
}

public class AiOrchestrationService : IAiOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,

        // Independent in-process schema strictness: the contract schema declares
        // "additionalProperties": false at every object level (Design Principle 2 —
        // "No invented fields"). System.Text.Json would otherwise silently ignore
        // unknown members, so an unmapped field now throws JsonException and the
        // attempt becomes FAILED_VALIDATION instead of being accepted. This closes
        // the one schema constraint DTO binding alone could not enforce without
        // adding a new JSON-Schema dependency (see AI_OUTPUT_VALIDATION_RULES §6).
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
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
        GenerateItineraryRequest? request = null,
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

        // BUDGET_FIRST deliberately allows generation with NO destination selected yet:
        // proposing destinations the budget can actually afford is the point of the
        // mode. With no destination the prompt offers every supported destination, the
        // model may return up to three distinct candidate options, and the ones within
        // budget are kept (V-002 §5.3). The chosen option's destination is persisted
        // onto the trip, and the user can switch via the SelectDestination endpoint.
        // When a destination IS already set the prompt stays scoped to it, so the
        // selected-destination flow behaves exactly as before.

        // --- Full-generation concurrency guard (Gap 3) ---
        // Mirrors the proven partial-regeneration pattern (ExpectedVersion +
        // ExecuteUpdate + affected-row check): a single conditional UPDATE claims the
        // trip (DRAFT@version N -> GENERATING@version N+1) atomically. Two parallel
        // /generate requests can both read DRAFT/N, but only one conditional UPDATE
        // can match it; the loser gets 0 affected rows and is rejected with 409
        // (InvalidOperationException mapping in AiGenerationController) before any
        // generation work or Gemini spend happens.
        if (trip.Status == TripLifecycle.Generating)
            throw new InvalidOperationException(
                "Trip is already generating. Only one generation request can run at a time.");

        if (!TripLifecycle.CanTransition(trip.Status, TripLifecycle.Generating))
            throw new InvalidOperationException(
                $"Trip in status '{trip.Status}' cannot start generation.");

        // Optional client-observed version for FULL generation — same semantics as
        // partial regeneration's ExpectedVersion: a stale value is rejected.
        if (request?.ExpectedVersion is { } expectedVersion && expectedVersion != trip.Version)
            throw new InvalidOperationException("Trip has been modified by another request.");

        var claimVersion = trip.Version + 1;
        var claimed = await _db.Trips
            .Where(t => t.Id == tripId &&
                        t.Status == TripLifecycle.Draft &&
                        t.Version == trip.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Status, TripLifecycle.Generating)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow)
                .SetProperty(t => t.Version, claimVersion), cancellationToken);

        if (claimed != 1)
            throw new InvalidOperationException(
                "Another generation request has claimed this trip, or the trip was modified by another request.");

        // ExecuteUpdate bypasses EF's change tracker: reload the tracked trip so its
        // original values match the committed claim — every later SaveChanges must
        // see GENERATING@claimVersion, not the pre-claim snapshot (Trip.Version is an
        // EF concurrency token).
        await _db.Entry(trip).ReloadAsync(cancellationToken);

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

        // Contract §5 step 0: derive destination_options.maxItems from the trip's
        // planning mode (1 for DESTINATION_FIRST, 3 for BUDGET_FIRST) before the
        // schema is sent to Gemini — never a single universal value.
        using var responseSchema = ItineraryGenerationSchema.LoadForMode(_environment, trip.PlanningMode);
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

                // Exponential backoff before the next attempt — Gemini's
                // transient 503/429s (observed in practice) don't always
                // clear within a couple of seconds, so a longer wait before
                // later attempts gives a real chance of landing outside the
                // overload/quota window instead of immediately repeating
                // into the same one.
                if (attempt < maxAttempts)
                    await Task.Delay(RetryBackoffDelay(attempt), cancellationToken);

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
            // Contract §5 step 4 / V-002 §5.3 budget policy:
            //   DESTINATION_FIRST — exactly one option; an over-budget plan is FLAGGED
            //                       (`isOverBudget`), never failed.
            //   BUDGET_FIRST      — up to three candidate options; the ones that fit are
            //                       kept and the attempt fails only when none survive.
            var budgetEnforced = trip.BudgetAmount.HasValue && trip.BudgetCurrencyId.HasValue;
            var isOverBudget = false;

            var selectedOption = output.DestinationOptions.First();

            if (trip.PlanningMode == "BUDGET_FIRST" && budgetEnforced)
            {
                var budgetRejections = new List<string>();

                var optionWithinBudget = await SelectFirstOptionWithinBudgetAsync(
                    output.DestinationOptions, trip, places, budgetRejections, cancellationToken);

                if (optionWithinBudget is null)
                {
                    aiGeneration.Status = "FAILED_VALIDATION";
                    aiGeneration.ValidationErrors =
                        "ALL_OPTIONS_OVER_BUDGET: no generated destination option fits the budget. " +
                        string.Join(" | ", budgetRejections);
                    aiGeneration.CompletedAt = DateTime.UtcNow;

                    // Release the destination so the user can pick another suggestion.
                    trip.DestinationId = null;
                    await _db.SaveChangesAsync(cancellationToken);

                    // Carry the V-002 failure code into the orchestrator's error list too,
                    // so the 422 body identifies the cause (")V-002 §5.7).
                    allErrors.Add(
                        $"Attempt {attempt}: ALL_OPTIONS_OVER_BUDGET — no BUDGET_FIRST destination " +
                        "option fits the requested budget.");
                    continue;
                }

                selectedOption = optionWithinBudget;
            }

            var selectedDestination = await _db.Destinations
                .FirstOrDefaultAsync(d => d.Name == selectedOption.DestinationName, cancellationToken);

            if (selectedDestination is null)
                throw new InvalidOperationException(
                    $"Generated destination '{selectedOption.DestinationName}' could not be resolved to the internal dataset.");

            // Duplicate-name handling (Gap 6): build the destination-scoped name
            // lookup defensively so an active duplicate place name inside the curated
            // dataset becomes an explicit FAILED_VALIDATION attempt failure instead of
            // an ArgumentException escaping from ToDictionary (unhandled 400/500).
            // Fail closed: ambiguous curated data can never produce an accepted itinerary.
            var scopedPlaces = places
                .Where(p => p.DestinationId == selectedDestination.Id)
                .ToList();

            var duplicatePlaceNames = scopedPlaces
                .GroupBy(p => p.Name, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatePlaceNames.Count > 0)
            {
                var duplicateList = string.Join(", ", duplicatePlaceNames);

                aiGeneration.Status = "FAILED_VALIDATION";
                aiGeneration.ValidationErrors =
                    "duplicate active place_name(s) in curated dataset for destination " +
                    $"'{selectedDestination.Name}': {duplicateList} " +
                    "(duplicate active place name).";
                aiGeneration.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                allErrors.Add(
                    $"Attempt {attempt}: duplicate active place_name(s) in curated dataset: {duplicateList} " +
                    "(duplicate active place name).");
                continue;
            }

            if (trip.DestinationId is null)
                trip.DestinationId = selectedDestination.Id;

            // Resolve place names to Place IDs for persistence. Validation above has
            // already grounded every referenced name to an active row in this
            // destination, and duplicates were rejected just above.
            var nameToPlace = scopedPlaces.ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);

            if (!nameToPlace.TryGetValue(selectedOption.Accommodation.PlaceName, out var accommodationPlace))
                throw new InvalidOperationException(
                    $"Accommodation '{selectedOption.Accommodation.PlaceName}' is not grounded to the selected destination.");

            var accommodationPlaceId = accommodationPlace.Id;

            // Deterministic backend budget guard. The AI never supplies prices:
            // every amount is resolved from the authoritative Place.reference_price,
            // including accommodation nights.
            if (budgetEnforced)
            {
                // Deterministically calculate every line from Place.reference_price,
                // convert each currency into the user's budget currency, and compare
                // the real total before anything is persisted.
                var totalInBudgetCurrency = await CalculateGeneratedOptionCostInBudgetCurrencyAsync(
                    selectedOption,
                    nameToPlace,
                    // GetValueOrDefault() rather than .Value: the enclosing
                    // `budgetEnforced` guard already proved HasValue, but the compiler
                    // cannot see through a bool local and warns CS8629.
                    trip.BudgetCurrencyId.GetValueOrDefault(),
                    cancellationToken);

                var isWithinBudget = totalInBudgetCurrency <= trip.BudgetAmount.GetValueOrDefault();

                if (!isWithinBudget && trip.PlanningMode == "BUDGET_FIRST")
                {
                    // BUDGET_FIRST was already filtered to a fitting option above, so
                    // reaching this branch means the selected option's cost moved
                    // between selection and persistence. Reject and retry rather than
                    // persist a plan the user's budget cannot cover.
                    aiGeneration.Status = "FAILED_VALIDATION";
                    aiGeneration.ValidationErrors =
                        $"Generated option exceeds budget: {totalInBudgetCurrency:F2} " +
                        $"{trip.BudgetCurrency?.IsoCode ?? "budget currency"} > {trip.BudgetAmount.GetValueOrDefault():F2}.";

                    aiGeneration.CompletedAt = DateTime.UtcNow;
                    trip.DestinationId = null;
                    await _db.SaveChangesAsync(cancellationToken);
                    allErrors.Add(
                        $"Attempt {attempt}: generated itinerary exceeds the requested budget " +
                        $"({totalInBudgetCurrency:F2} > {trip.BudgetAmount.GetValueOrDefault():F2}).");
                    continue;
                }

                if (!isWithinBudget)
                {
                    // DESTINATION_FIRST — V-002 §5.3: flag an over-budget plan instead of
                    // failing it. The user picked the destination, so they receive the
                    // itinerary together with the over-budget signal.
                    isOverBudget = true;
                    _logger.LogInformation(
                        "Attempt {Attempt} for trip {TripId} is over budget ({Total:F2} > {Budget:F2}); " +
                        "flagging the generated plan for DESTINATION_FIRST.",
                        attempt, tripId, totalInBudgetCurrency, trip.BudgetAmount.GetValueOrDefault());
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

            // Final persisted-cost guard, for BUDGET_FIRST only. The cost service
            // recomputes the exact persisted itinerary from Place.reference_price;
            // compare its result once more before allowing GENERATED. DESTINATION_FIRST
            // is deliberately excluded — V-002 §5.3 flags an over-budget plan instead of
            // failing it (see `isOverBudget` above).
            if (trip.PlanningMode == "BUDGET_FIRST" &&
                trip.BudgetAmount.HasValue &&
                trip.BudgetCurrencyId.HasValue)
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
                IsOverBudget = isOverBudget,
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

        // Contract §5 step 0: derive destination_options.maxItems from the trip's
        // planning mode (1 for DESTINATION_FIRST, 3 for BUDGET_FIRST) before the
        // schema is sent to Gemini — never a single universal value.
        using var responseSchema = ItineraryGenerationSchema.LoadForMode(_environment, trip.PlanningMode);

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

                // See the matching comment in GenerateItineraryAsync — an
                // exponential backoff so later attempts have a real chance
                // of landing outside a transient 503/429 window.
                if (attempt < maxAttempts)
                    await Task.Delay(RetryBackoffDelay(attempt), cancellationToken);

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

            var scopedPlaces = places
                .Where(p => p.DestinationName == selectedOption.DestinationName)
                .ToList();

            // Duplicate-name handling (Gap 6): same fail-closed guard as full
            // generation — an active duplicate place name becomes FAILED_VALIDATION
            // for this attempt instead of an ArgumentException from ToDictionary.
            var duplicatePlaceNames = scopedPlaces
                .GroupBy(p => p.Name, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatePlaceNames.Count > 0)
            {
                var duplicateList = string.Join(", ", duplicatePlaceNames);

                aiGeneration.Status = "FAILED_VALIDATION";
                aiGeneration.ValidationErrors =
                    $"Duplicate active place name(s) in curated dataset: {duplicateList}.";
                aiGeneration.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                allErrors.Add(
                    $"Attempt {attempt}: duplicate active place name(s) in curated dataset: {duplicateList}.");
                continue;
            }

            var placeLookup = scopedPlaces.ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);

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

    /// <summary>
    /// Contract §5 step 4 / V-002 §5.3 for BUDGET_FIRST: the model returns up to three
    /// candidate options and only some may fit the budget. Returns the first candidate
    /// whose deterministic cost (from <c>Place.reference_price</c>, converted into the
    /// trip's budget currency) is within budget, or null when none survives.
    /// A candidate that cannot be grounded is skipped — the attempt only fails when no
    /// candidate both resolves and fits. Rejection reasons are appended to
    /// <paramref name="rejectionReasons"/> for the auditable failure record.
    /// </summary>
    private async Task<GeminiDestinationOptionDto?> SelectFirstOptionWithinBudgetAsync(
        IReadOnlyList<GeminiDestinationOptionDto> options,
        Entities.Trip trip,
        IReadOnlyList<PlaceContextDto> places,
        List<string> rejectionReasons,
        CancellationToken cancellationToken)
    {
        foreach (var option in options)
        {
            // Resolve each candidate against its own named destination. In the current
            // BUDGET_FIRST flow the user has already selected one destination, so the
            // candidates normally share it — resolving per option keeps the rule general
            // and correct if the model is ever allowed to propose several.
            var destination = await _db.Destinations
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Name == option.DestinationName, cancellationToken);

            if (destination is null)
            {
                rejectionReasons.Add(
                    $"destination_option('{option.DestinationName}'): destination could not be resolved " +
                    "to the internal dataset.");
                continue;
            }

            var optionPlaces = places
                .Where(p => p.DestinationId == destination.Id)
                .ToList();

            // Fail closed on ambiguous curated data rather than scoring a candidate
            // whose costs could resolve to the wrong row.
            var duplicateNames = optionPlaces
                .GroupBy(p => p.Name, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateNames.Count > 0)
            {
                rejectionReasons.Add(
                    "Duplicate active place name(s) in curated dataset for destination " +
                    $"'{destination.Name}': {string.Join(", ", duplicateNames)}.");
                continue;
            }

            var optionLookup = optionPlaces.ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);

            if (!optionLookup.ContainsKey(option.Accommodation.PlaceName))
            {
                rejectionReasons.Add(
                    $"destination_option('{option.DestinationName}'): accommodation " +
                    $"'{option.Accommodation.PlaceName}' is not grounded to that destination.");
                continue;
            }

            var totalInBudgetCurrency = await CalculateGeneratedOptionCostInBudgetCurrencyAsync(
                option, optionLookup, trip.BudgetCurrencyId!.Value, cancellationToken);

            if (totalInBudgetCurrency <= trip.BudgetAmount!.Value)
                return option;

            rejectionReasons.Add(
                $"destination_option('{option.DestinationName}') exceeds budget: " +
                $"{totalInBudgetCurrency:F2} > {trip.BudgetAmount.Value:F2}.");
        }

        return null;
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

    /// Exponential backoff (3s, 6s, 12s, 24s, ...) between bounded-retry
    /// attempts, capped at 20s so it never eats an unreasonable share of the
    /// caller's own request timeout. `attempt` is 1-based (the attempt that
    /// just failed) — the delay is before the *next* one.
    private static TimeSpan RetryBackoffDelay(int attempt)
    {
        var seconds = Math.Min(3 * Math.Pow(2, attempt - 1), 20);
        return TimeSpan.FromSeconds(seconds);
    }
}