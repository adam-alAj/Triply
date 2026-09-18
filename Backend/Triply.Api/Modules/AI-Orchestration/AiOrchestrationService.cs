using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration.Dtos;
using Triply.Api.Modules.Cost;
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

    public AiOrchestrationService(
        ApplicationDbContext db,
        IGeminiClient geminiClient,
        IItineraryPromptBuilder promptBuilder,
        IItineraryValidator validator,
        ICostAggregationService costAggregationService,
        IOptions<GeminiOptions> options,
        ILogger<AiOrchestrationService> logger)
    {
        _db = db;
        _geminiClient = geminiClient;
        _promptBuilder = promptBuilder;
        _validator = validator;
        _costAggregationService = costAggregationService;
        _options = options.Value;
        _logger = logger;
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

        if (trip.DestinationId is null)
            throw new InvalidOperationException(
                "Trip has no confirmed destination yet - budget-first destination suggestion must run first.");

        if (!TripLifecycle.CanTransition(trip.Status, TripLifecycle.Generating) && trip.Status != TripLifecycle.Generating)
            throw new InvalidOperationException($"Trip in status '{trip.Status}' cannot start generation.");

        if (trip.Status != TripLifecycle.Generating)
            TripLifecycle.Transition(trip, TripLifecycle.Generating);

        await _db.SaveChangesAsync(cancellationToken);

        // --- Fetch dataset context (places for this destination, active only) ---
        var places = await _db.Places
            .AsNoTracking()
            .Include(p => p.PlaceCategory)
            .Include(p => p.Currency)
            .Where(p => p.DestinationId == trip.DestinationId.Value && p.IsActive)
            .Select(p => new PlaceContextDto
            {
                Id = p.Id,
                Name = p.Name,
                Category = p.PlaceCategory.Code,
                ReferencePrice = p.ReferencePrice,
                Currency = p.Currency.IsoCode,
                BudgetTier = null // TODO: join with Extra_AI_Context.csv for budget_tier
            })
            .ToListAsync(cancellationToken);

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
                var prompt = _promptBuilder.Build(trip, places, interestLabels, dayCount);
                rawText = await _geminiClient.GenerateJsonAsync(prompt, cancellationToken);
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

            TripLifecycle.Transition(trip, TripLifecycle.Generated);
            trip.UpdatedAt = DateTime.UtcNow;
            trip.Version++;

            aiGeneration.Status = "SUCCEEDED";
            aiGeneration.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // TASK48 — write CostEstimate rows from the itinerary just persisted
            var costResult = await _costAggregationService.GenerateFromItineraryAsync(tripId, cancellationToken);

            return new AiGenerationResult
            {
                Success = true,
                Status = "SUCCEEDED",
                AiGenerationId = aiGeneration.Id,
                AttemptsUsed = attempt,
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
