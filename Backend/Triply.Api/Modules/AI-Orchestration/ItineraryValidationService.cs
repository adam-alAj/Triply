using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration.Dtos;
using TripEntity = Triply.Api.Entities.Trip;

namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// v2.0.0 validation rules per AI JSON Schema Contract §5.
///
/// Validation pipeline (in order):
/// 0. Schema-shape validation — JSON parses, required fields present, types/enums correct
///    (handled by Gemini structured output mode + System.Text.Json deserialization)
/// 1. Structural consistency — day count, date alignment, no duplicate slots
/// 2. Dataset grounding (FR-AI-002, 0% tolerance) — every name resolves to an active row
/// 3. Category rules — accommodation not in days, at least one restaurant per day,
///    at least one transport across the option
/// 4. Budget check (deterministic, Backend-computed — not validated here)
/// </summary>
public interface IItineraryValidator
{
    Task<ItineraryValidationResult> ValidateAsync(
        TripEntity trip,
        GeminiItineraryOutputDto output,
        CancellationToken cancellationToken = default);
}

public class ItineraryValidationService : IItineraryValidator
{
    private readonly ApplicationDbContext _db;

    private static readonly HashSet<string> ValidTimeSlots = new()
    {
        "MORNING", "AFTERNOON", "EVENING"
    };

    public ItineraryValidationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ItineraryValidationResult> ValidateAsync(
        TripEntity trip,
        GeminiItineraryOutputDto output,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // --- Step 0: Top-level structure ---
        if (string.IsNullOrWhiteSpace(output.PlanningMode))
        {
            errors.Add("planning_mode is missing or empty.");
            return Fail(errors);
        }

        if (output.PlanningMode != trip.PlanningMode)
        {
            errors.Add(
                $"planning_mode '{output.PlanningMode}' does not match trip planning_mode '{trip.PlanningMode}'.");
        }

        if (output.DestinationOptions is null || output.DestinationOptions.Count == 0)
        {
            errors.Add("destination_options is empty or missing.");
            return Fail(errors);
        }

        var expectedMax = trip.PlanningMode == "DESTINATION_FIRST" ? 1 : 3;
        if (output.DestinationOptions.Count > expectedMax)
        {
            errors.Add(
                $"destination_options has {output.DestinationOptions.Count} entries, " +
                $"but {trip.PlanningMode} mode allows at most {expectedMax}.");
        }

        // --- Per-option validation ---
        var dayCount = trip.StartDate.HasValue && trip.EndDate.HasValue
            ? Math.Max(1, trip.EndDate.Value.DayNumber - trip.StartDate.Value.DayNumber + 1)
            : 3;

        var optionDestinationNames = output.DestinationOptions
            .Select(o => o.DestinationName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        var supportedDestinations = await _db.Destinations
            .AsNoTracking()
            .Where(d => optionDestinationNames.Contains(d.Name))
            .Select(d => new DestinationValidationContext(d.Id, d.Name))
            .ToListAsync(cancellationToken);

        if (output.DestinationOptions.Count != optionDestinationNames.Distinct(StringComparer.Ordinal).Count())
            errors.Add("destination_options must contain distinct destination_name values.");

        foreach (var option in output.DestinationOptions)
        {
            await ValidateDestinationOption(trip, option, dayCount, supportedDestinations, errors, cancellationToken);
        }

        return new ItineraryValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private sealed record DestinationValidationContext(long Id, string Name);

    private async Task ValidateDestinationOption(
        TripEntity trip,
        GeminiDestinationOptionDto option,
        int expectedDayCount,
        IReadOnlyList<DestinationValidationContext> supportedDestinations,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var optionPrefix = $"destination_option('{option.DestinationName}')";

        // --- Step 1: Structural consistency ---

        // Check accommodation
        if (option.Accommodation is null)
        {
            errors.Add($"{optionPrefix}: accommodation is missing.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(option.Accommodation.PlaceName))
                errors.Add($"{optionPrefix}: accommodation.place_name is missing.");

            if (option.Accommodation.Nights < 1)
                errors.Add($"{optionPrefix}: accommodation.nights must be >= 1, got {option.Accommodation.Nights}.");
        }

        // Check days
        if (option.Days is null || option.Days.Count == 0)
        {
            errors.Add($"{optionPrefix}: days is empty or missing.");
            return;
        }

        if (option.Days.Count != expectedDayCount)
        {
            errors.Add(
                $"{optionPrefix}: days has {option.Days.Count} entries, " +
                $"expected {expectedDayCount} (trip duration).");
        }

        // Day number continuity and date alignment
        var sortedDays = option.Days.OrderBy(d => d.DayNumber).ToList();
        for (int i = 0; i < sortedDays.Count; i++)
        {
            var day = sortedDays[i];
            var expectedDayNumber = i + 1;

            if (day.DayNumber != expectedDayNumber)
            {
                errors.Add(
                    $"{optionPrefix} day[{i}]: day_number is {day.DayNumber}, " +
                    $"expected {expectedDayNumber} (must be contiguous starting at 1).");
            }

            if (trip.StartDate.HasValue)
            {
                var expectedDate = trip.StartDate.Value.AddDays(i);
                if (day.Date != expectedDate)
                {
                    errors.Add(
                        $"{optionPrefix} day {day.DayNumber}: date is {day.Date}, " +
                        $"expected {expectedDate} (start_date + day_number - 1).");
                }
            }

            if (day.Items is null || day.Items.Count == 0)
            {
                errors.Add($"{optionPrefix} day {day.DayNumber}: items is empty.");
                continue;
            }

            // Check item-level structure
            var slotCounts = new Dictionary<string, int>();
            foreach (var item in day.Items)
            {
                if (string.IsNullOrWhiteSpace(item.PlaceName))
                {
                    errors.Add($"{optionPrefix} day {day.DayNumber}: item has empty place_name.");
                    continue;
                }

                if (!ValidTimeSlots.Contains(item.TimeSlot?.ToUpperInvariant()))
                {
                    errors.Add(
                        $"{optionPrefix} day {day.DayNumber}, item '{item.PlaceName}': " +
                        $"invalid time_slot '{item.TimeSlot}'.");
                }

                if (item.OrderIndex < 1)
                {
                    errors.Add(
                        $"{optionPrefix} day {day.DayNumber}, item '{item.PlaceName}': " +
                        $"order_index must be >= 1, got {item.OrderIndex}.");
                }

                // Check unique (day_number, time_slot, order_index)
                var slotKey = item.TimeSlot?.ToUpperInvariant() ?? "NULL";
                if (!slotCounts.TryAdd(slotKey, 1))
                {
                    // Multiple items in same slot is allowed with different order_index
                    // but same order_index in same slot is a problem
                    // (we'll check duplicates after the loop)
                }
            }

            // Check for duplicate (time_slot, order_index) within a day
            var duplicateSlots = day.Items
                .GroupBy(i => $"{i.TimeSlot?.ToUpperInvariant()}_{i.OrderIndex}")
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicateSlots is not null)
            {
                errors.Add(
                    $"{optionPrefix} day {day.DayNumber}: duplicate " +
                    $"(time_slot='{duplicateSlots.First().TimeSlot}', " +
                    $"order_index={duplicateSlots.First().OrderIndex}).");
            }
        }

        // --- Step 2 & 3: Dataset grounding + category rules ---

        // Collect all place names from this option
        var allPlaceNames = new HashSet<string>(StringComparer.Ordinal);
        allPlaceNames.Add(option.Accommodation?.PlaceName ?? "");
        foreach (var day in option.Days)
        {
            if (day.Items is null) continue;
            foreach (var item in day.Items)
            {
                allPlaceNames.Add(item.PlaceName ?? "");
            }
        }
        allPlaceNames.Remove(""); // remove empties

        var destination = supportedDestinations.FirstOrDefault(d =>
            string.Equals(d.Name, option.DestinationName, StringComparison.Ordinal));

        if (destination is null)
        {
            errors.Add(
                $"{optionPrefix}: destination_name '{option.DestinationName}' does not exist in the supported destination dataset.");
        }

        // Resolve all names to Place rows
        var places = await _db.Places
            .AsNoTracking()
            .Include(p => p.PlaceCategory)
            .Where(p => allPlaceNames.Contains(p.Name) && p.IsActive)
            .ToListAsync(cancellationToken);

        var placeByName = places.ToDictionary(p => p.Name, p => p);

        // 0% tolerance: every name must resolve
        foreach (var name in allPlaceNames)
        {
            if (!placeByName.TryGetValue(name, out var place))
            {
                errors.Add(
                    $"{optionPrefix}: place_name '{name}' does not exist " +
                    "or is inactive (FR-AI-002, 0% invented places).");
                continue;
            }

            // Destination scoping: option destination is authoritative in BUDGET_FIRST;
            // trip destination is additionally enforced when already selected.
            if (destination is not null && place.DestinationId != destination.Id)
            {
                errors.Add(
                    $"{optionPrefix}: place_name '{name}' belongs to destination_id {place.DestinationId}, " +
                    $"not option destination '{destination.Name}' ({destination.Id}).");
            }

            if (trip.DestinationId.HasValue && place.DestinationId != trip.DestinationId.Value)
            {
                errors.Add(
                    $"{optionPrefix}: place_name '{name}' belongs to " +
                    $"destination_id {place.DestinationId}, not the trip's destination " +
                    $"({trip.DestinationId.Value}).");
            }
        }

        // Category rules
        if (option.Accommodation is not null &&
            placeByName.TryGetValue(option.Accommodation.PlaceName, out var accPlace))
        {
            if (accPlace.PlaceCategory?.Code != "ACCOMMODATION")
            {
                errors.Add(
                    $"{optionPrefix}: accommodation '{option.Accommodation.PlaceName}' " +
                    $"has category '{accPlace.PlaceCategory?.Code}', expected 'ACCOMMODATION'.");
            }
        }

        // Check at least one restaurant per day
        foreach (var day in option.Days)
        {
            if (day.Items is null) continue;

            var hasRestaurant = day.Items.Any(item =>
                placeByName.TryGetValue(item.PlaceName ?? "", out var p) &&
                p.PlaceCategory?.Code == "RESTAURANT");

            if (!hasRestaurant)
            {
                errors.Add(
                    $"{optionPrefix} day {day.DayNumber}: no RESTAURANT-category place found.");
            }

            // No ACCOMMODATION in days
            var accommodationInDay = day.Items.FirstOrDefault(item =>
                placeByName.TryGetValue(item.PlaceName ?? "", out var p) &&
                p.PlaceCategory?.Code == "ACCOMMODATION");

            if (accommodationInDay is not null)
            {
                errors.Add(
                    $"{optionPrefix} day {day.DayNumber}: ACCOMMODATION-category place " +
                    $"'{accommodationInDay.PlaceName}' found in days (must be in accommodation only).");
            }
        }

        // Check at least one transport across all days
        var hasTransport = option.Days
            .Where(d => d.Items != null)
            .SelectMany(d => d.Items)
            .Any(item =>
                placeByName.TryGetValue(item.PlaceName ?? "", out var p) &&
                p.PlaceCategory?.Code == "TRANSPORT");

        if (!hasTransport)
        {
            errors.Add($"{optionPrefix}: no TRANSPORT-category place found across all days.");
        }
    }

    private static ItineraryValidationResult Fail(List<string> errors)
    {
        return new ItineraryValidationResult { IsValid = false, Errors = errors };
    }
}
