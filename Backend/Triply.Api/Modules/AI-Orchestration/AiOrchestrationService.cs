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
/// fetch dataset context → build prompt → call Gemini (TASK45) → validate (TASK46)
/// → persist Itinerary/Days/Items + CostEstimate rows (TASK47/TASK48), or retry a
/// bounded number of times, or fail cleanly with an auditable AIGeneration row.
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
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
            throw new KeyNotFoundException("Trip does not exist.");

        if (trip.DestinationId is null)
            throw new InvalidOperationException(
                "Trip has no confirmed destination yet — budget-first destination suggestion must run first.");

        if (!TripLifecycle.CanTransition(trip.Status, TripLifecycle.Generating) && trip.Status != TripLifecycle.Generating)
            throw new InvalidOperationException($"Trip in status '{trip.Status}' cannot start generation.");

        if (trip.Status != TripLifecycle.Generating)
            TripLifecycle.Transition(trip, TripLifecycle.Generating);

        await _db.SaveChangesAsync(cancellationToken);

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
                Currency = p.Currency.IsoCode
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
                Errors = { "No active places are curated yet for this destination — cannot ground a generation." }
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
            DayCount = dayCount
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

                allErrors.Add($"Attempt {attempt}: Gemini call failed — {ex.Message}");
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

            // Valid — persist itinerary + cost estimates as one all-or-nothing transaction
            // (Database Design §24: a partially written itinerary must never be visible).
            var placeLookup = places.ToDictionary(p => p.Id);

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

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

            foreach (var dayDto in output.Days.OrderBy(d => d.DayNumber))
            {
                var day = new ItineraryDay
                {
                    DayNumber = dayDto.DayNumber,
                    Date = dayDto.Date
                };

                foreach (var itemDto in dayDto.Items.OrderBy(i => i.OrderIndex))
                {
                    // EstimatedCost is deterministically copied from Place.ReferencePrice —
                    // never taken from the model — per Database Design §15.
                    var referencePrice = placeLookup[itemDto.PlaceId].ReferencePrice;

                    day.Items.Add(new ItineraryItem
                    {
                        PlaceId = itemDto.PlaceId,
                        TimeSlot = itemDto.TimeSlot.ToUpperInvariant(),
                        OrderIndex = itemDto.OrderIndex,
                        EstimatedCost = referencePrice,
                        Notes = itemDto.Notes,
                        IsAiGenerated = true
                    });
                }

                itinerary.Days.Add(day);
            }

            _db.Itineraries.Add(itinerary);

            TripLifecycle.Transition(trip, TripLifecycle.Generated);
            trip.UpdatedAt = DateTime.UtcNow;
            trip.Version++;

            aiGeneration.Status = "SUCCEEDED";
            aiGeneration.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // TASK48 — write CostEstimate rows from the itinerary just persisted,
            // then refresh Trip.TotalEstimatedCost via the existing aggregation service.
            var costResult = await _costAggregationService.GenerateFromItineraryAsync(tripId, cancellationToken);

            return new AiGenerationResult
            {
                Success = true,
                Status = "SUCCEEDED",
                AiGenerationId = aiGeneration.Id,
                AttemptsUsed = attempt,
                Itinerary = ToItineraryResponse(itinerary, placeLookup),
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
        Dictionary<long, PlaceContextDto> placeLookup)
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
                            PlaceName = placeLookup.TryGetValue(i.PlaceId, out var p) ? p.Name : string.Empty,
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
