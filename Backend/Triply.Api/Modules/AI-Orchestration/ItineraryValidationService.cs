using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration.Dtos;

namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// TASK46 — validation rules the AI/ML track designed (dataset-existence + cost-tolerance),
/// implemented here against the real Place table. Exact tolerance value / matching rule
/// beyond "must exist and belong to the trip's destination" should be confirmed with the
/// AI track (their "Design AI-Output Validation Rules" deliverable) — this is a faithful,
/// literal reading of FR-AI-002 / SRS D1 in the meantime.
/// </summary>
public interface IItineraryValidator
{
    Task<ItineraryValidationResult> ValidateAsync(
       Triply.Api.Entities.Trip trip,
        GeminiItineraryOutputDto output,
        CancellationToken cancellationToken = default);
}

public class ItineraryValidationService : IItineraryValidator
{
    private readonly ApplicationDbContext _db;

    public ItineraryValidationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ItineraryValidationResult> ValidateAsync(
       Triply.Api.Entities.Trip trip,
        GeminiItineraryOutputDto output,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (output.Days is null || output.Days.Count == 0)
        {
            errors.Add("Generated itinerary has no days.");
            return new ItineraryValidationResult { IsValid = false, Errors = errors };
        }

        var dayNumbers = output.Days.Select(d => d.DayNumber).ToList();
        if (dayNumbers.Distinct().Count() != dayNumbers.Count)
            errors.Add("Duplicate day numbers in generated itinerary.");

        var allItems = output.Days.SelectMany(d => d.Items).ToList();
        if (allItems.Count == 0)
        {
            errors.Add("Generated itinerary has no items.");
            return new ItineraryValidationResult { IsValid = false, Errors = errors };
        }

        var placeIds = allItems.Select(i => i.PlaceId).Distinct().ToList();

        var places = await _db.Places
            .AsNoTracking()
            .Where(p => placeIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // --- 0% invented-place rate — FR-AI-002, the project's non-negotiable rule ---
        foreach (var id in placeIds)
        {
            if (!places.TryGetValue(id, out var place))
            {
                errors.Add($"placeId {id} does not exist in the internal dataset (invented place).");
                continue;
            }

            if (!place.IsActive)
                errors.Add($"placeId {id} ('{place.Name}') is inactive and cannot be used.");

            if (trip.DestinationId.HasValue && place.DestinationId != trip.DestinationId.Value)
                errors.Add($"placeId {id} ('{place.Name}') does not belong to the trip's destination.");
        }

        var validTimeSlots = new[] { "MORNING", "AFTERNOON", "EVENING" };
        foreach (var item in allItems)
        {
            if (!validTimeSlots.Contains(item.TimeSlot?.ToUpperInvariant()))
                errors.Add($"placeId {item.PlaceId} has an invalid timeSlot '{item.TimeSlot}'.");
        }

        foreach (var day in output.Days)
        {
            var duplicateInDay = day.Items
                .GroupBy(i => i.PlaceId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateInDay is not null)
                errors.Add($"Day {day.DayNumber} repeats placeId {duplicateInDay.Key}.");
        }

        // Note: cost is never taken from the model's output — ItineraryItem.EstimatedCost
        // is always copied from Place.ReferencePrice at persistence time (Database Design §15),
        // so there is nothing to tolerance-check here unless/until the agreed schema adds a
        // model-provided price field. GeminiOptions.CostTolerancePercent is reserved for that.

        return new ItineraryValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
