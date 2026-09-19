using System.Text.Json;
using Triply.Api.Modules.AIOrchestration.Dtos;

namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// Builds the structured, dataset-grounded prompt sent to Gemini.
/// v2.0.0 — aligned with AI/docs/TRIPLY_AI_JSON_SCHEMA_CONTRACT_v2.md.
///
/// Uses place_name (string) for grounding — the AI never sees or returns IDs.
/// Supports both DESTINATION_FIRST and BUDGET_FIRST planning modes.
/// </summary>
public interface IItineraryPromptBuilder
{
    string Build(
        Entities.Trip trip,
        IReadOnlyList<PlaceContextDto> places,
        IReadOnlyList<string> interestLabels,
        int dayCount);

    /// <summary>
    /// Build prompt for BUDGET_FIRST mode with multiple destination contexts.
    /// </summary>
    string BuildBudgetFirst(
        Entities.Trip trip,
        IReadOnlyList<PlaceContextDto> places,
        IReadOnlyList<DestinationContextDto> destinations,
        IReadOnlyList<string> interestLabels,
        int dayCount);
}

public class ItineraryPromptBuilder : IItineraryPromptBuilder
{
    public string Build(
        Entities.Trip trip,
        IReadOnlyList<PlaceContextDto> places,
        IReadOnlyList<string> interestLabels,
        int dayCount)
    {
        var interests = interestLabels.Count > 0
            ? string.Join(", ", interestLabels)
            : "no specific interests provided - pick a balanced mix";

        // Build the place list grouped by category, using names only (no IDs)
        var placeList = BuildPlaceList(places);
        var dateRule = BuildDateRule(trip.StartDate, trip.EndDate, dayCount);

        return $$"""
        You are Triply's trip-planning assistant. You generate a structured trip
        itinerary in JSON only, following the provided response schema exactly.
        Follow these rules with zero exceptions:

        1. Never output any database ID of any kind (no Place.id, no
           Destination.id). Refer to places and destinations only by their exact
           name.
        2. Every `place_name` and `destination_name` you output must be copied
           exactly, character-for-character, from the place list given to you
           below. Do not invent, merge, abbreviate, or guess a name. If you are
           not certain a place is on the list, do not use it.
        3. Never output any price, cost, currency amount, or cost estimate - not
           as a number, not as a string, not inside `notes`. All costs are
           computed separately from internal pricing data.
        4. Never include confidence scores, explanations of your reasoning, or
           any field not defined in the response schema.
        5. Each day must include at least one RESTAURANT-category place. The full
           plan must include at least one TRANSPORT-category place somewhere
           across all days.
        6. Exactly one ACCOMMODATION-category place must be chosen and returned
           only inside the `accommodation` object - never repeated inside `days`.
        7. Return exactly one entry in `destination_options`, for the destination
           given below.
        8. `days` must have exactly {{dayCount}} entries, `day_number`
           1-indexed with no gaps. {{dateRule}}
        9. Set `planning_mode` to `"DESTINATION_FIRST"`.

        Trip preferences:
        - Travelers: {{trip.TravelerCount}}
        - Interests: {{interests}}
        - Budget: {{trip.BudgetAmount}} (context only - do not mention or calculate any cost)

        Only use places from the list below. Do not use any place that is not on
        this list, and do not use a place from a different destination:

        {{placeList}}

        Return your response as JSON matching the required response schema exactly.
        """;
    }

    public string BuildBudgetFirst(
        Entities.Trip trip,
        IReadOnlyList<PlaceContextDto> places,
        IReadOnlyList<DestinationContextDto> destinations,
        IReadOnlyList<string> interestLabels,
        int dayCount)
    {
        var interests = interestLabels.Count > 0
            ? string.Join(", ", interestLabels)
            : "no specific interests provided - pick a balanced mix";

        var supportedDestinations = string.Join("\n",
            destinations.Select(d => $"- {d.Name}: {d.Description ?? "N/A"}"));

        var placeList = BuildPlaceListByDestination(places, destinations);
        var dateRule = BuildDateRule(trip.StartDate, trip.EndDate, dayCount);

        return $$"""
        You are Triply's trip-planning assistant. You generate 1 to 3 candidate
        trip itineraries in JSON only, following the provided response schema
        exactly. Follow these rules with zero exceptions:

        1. Never output any database ID of any kind (no Place.id, no
           Destination.id). Refer to places and destinations only by their exact
           name.
        2. Every `place_name` and `destination_name` you output must be copied
           exactly, character-for-character, from the place lists given to you
           below. Do not invent, merge, abbreviate, or guess a name. If you are
           not certain a place is on the list, do not use it. Only use
           destinations from the supported destination list below.
        3. Never output any price, cost, currency amount, or cost estimate - not
           as a number, not as a string, not inside `notes`. All costs are
           computed separately from internal pricing data.
        4. Never include confidence scores, explanations of your reasoning, or
           any field not defined in the response schema.
        5. Each day, in each destination option, must include at least one
           RESTAURANT-category place. Each destination option's full plan must
           include at least one TRANSPORT-category place somewhere across all its
           days.
        6. Exactly one ACCOMMODATION-category place must be chosen per
           destination option, returned only inside that option's `accommodation`
           object - never repeated inside `days`.
        7. Return between 1 and 3 entries in `destination_options`, each for a
           different destination from the supported list below. Each option must
           be a complete, self-contained plan that you judge as realistically
           fitting within the stated budget, using only the reference pricing
           implied by each place's `budget_tier` in the list below - never state
           or calculate an exact total.
        8. Within each destination option, `days` must have exactly {{dayCount}} entries,
           `day_number` 1-indexed with no gaps. {{dateRule}}
        9. Set `planning_mode` to `"BUDGET_FIRST"`.

        Trip preferences:
        - Travelers: {{trip.TravelerCount}}
        - Interests: {{interests}}
        - Budget: {{trip.BudgetAmount}} {{trip.BudgetCurrency?.IsoCode ?? ""}} for the whole trip
          (context only - do not mention, estimate, or calculate any cost in your response)

        Supported destinations:
        {{supportedDestinations}}

        Choose only from the supported destinations and places below. Do not use
        any destination or place that is not on this list:

        {{placeList}}

        Return your response as JSON matching the required response schema exactly.
        """;
    }

    /// <summary>
    /// Builds the explicit date instruction and, when the trip's start date is
    /// known, spells out the exact ISO date required for every day so the
    /// model has no ambiguity (it has no other way to know "today"/the trip's
    /// actual dates - previously this rule referenced "the trip's date range"
    /// without ever stating what that range was, which caused Gemini to
    /// hallucinate unrelated dates on every attempt).
    /// </summary>
    private static string BuildDateRule(DateOnly? startDate, DateOnly? endDate, int dayCount)
    {
        if (startDate is null)
        {
            return "Each `date` must be a valid ISO 8601 date (yyyy-MM-dd), " +
                   "with day 1 followed by consecutive calendar days.";
        }

        var lines = new List<string>();
        for (var i = 0; i < dayCount; i++)
        {
            var date = startDate.Value.AddDays(i);
            lines.Add($"day_number {i + 1} => date \"{date:yyyy-MM-dd}\"");
        }

        var mapping = string.Join(", ", lines);
        var endDateNote = endDate.HasValue ? $" (trip ends {endDate.Value:yyyy-MM-dd})" : "";

        return $"The trip starts on {startDate.Value:yyyy-MM-dd}{endDateNote}. " +
               $"Use exactly these dates, one per day_number: {mapping}. " +
               "Do not use any other date.";
    }

    /// <summary>
    /// Build a grouped place list for DESTINATION_FIRST mode.
    /// </summary>
    private static string BuildPlaceList(IReadOnlyList<PlaceContextDto> places)
    {
        var grouped = places
            .GroupBy(p => p.Category)
            .OrderBy(g => g.Key);

        var lines = new List<string>();
        foreach (var group in grouped)
        {
            lines.Add($"[{group.Key}]");
            foreach (var place in group.OrderBy(p => p.Name))
            {
                var tier = string.IsNullOrEmpty(place.BudgetTier) ? "" : $" ({place.BudgetTier})";
                lines.Add($"- {place.Name}{tier}");
            }
            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Build a grouped place list for BUDGET_FIRST mode, organized by destination.
    /// </summary>
    private static string BuildPlaceListByDestination(
        IReadOnlyList<PlaceContextDto> places,
        IReadOnlyList<DestinationContextDto> destinations)
    {
        var lines = new List<string>();

        foreach (var dest in destinations)
        {
            var destPlaces = places
                .Where(p => string.Equals(p.DestinationName, dest.Name, StringComparison.Ordinal))
                .GroupBy(p => p.Category)
                .OrderBy(g => g.Key);

            lines.Add($"== {dest.Name} ==");
            foreach (var group in destPlaces)
            {
                lines.Add($"  [{group.Key}]");
                foreach (var place in group.OrderBy(p => p.Name))
                {
                    var tier = string.IsNullOrEmpty(place.BudgetTier) ? "" : $" ({place.BudgetTier})";
                    lines.Add($"  - {place.Name}{tier}");
                }
            }
            lines.Add("");
        }

        return string.Join("\n", lines);
    }
}