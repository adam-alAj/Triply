using System.Text.Json;
using Triply.Api.Modules.AIOrchestration.Dtos;

namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// Builds the structured, dataset-grounded prompt sent to Gemini.
/// PROVISIONAL: replace the template text below once AI/ML hands off the prompt
/// they validated in Google AI Studio (Dependency #4 on TASK45). The JSON *shape*
/// requested here must stay in sync with GeminiItineraryOutputDto.
/// </summary>
public interface IItineraryPromptBuilder
{
    string Build(
        Entities.Trip trip,
        IReadOnlyList<PlaceContextDto> places,
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
        var placesJson = JsonSerializer.Serialize(places);
        var interests = interestLabels.Count > 0
            ? string.Join(", ", interestLabels)
            : "no specific interests provided — pick a balanced mix";

        return $$"""
        You are Triply's itinerary planning assistant.
        Build a {{dayCount}}-day day-by-day travel itinerary using ONLY the places listed
        in "availablePlaces" below. Never invent a place, name, or id that is not in this list —
        every itinerary item you return will be rejected unless its placeId exists in this list.

        Trip preferences:
        - Travelers: {{trip.TravelerCount}}
        - Interests: {{interests}}

        availablePlaces (id, name, category, referencePrice, currency):
        {{placesJson}}

        Return ONLY valid JSON (no markdown fences, no commentary) matching exactly this shape:
        {
          "days": [
            {
              "dayNumber": 1,
              "date": "YYYY-MM-DD",
              "items": [
                {
                  "placeId": <must be one of the ids in availablePlaces>,
                  "timeSlot": "MORNING" | "AFTERNOON" | "EVENING",
                  "orderIndex": 0,
                  "notes": "short optional note, may be empty"
                }
              ]
            }
          ]
        }

        Rules:
        - Produce exactly {{dayCount}} day object(s), dayNumber starting at 1.
        - Each day should have between 2 and 4 items, spread across MORNING/AFTERNOON/EVENING.
        - Do not reuse the same placeId twice within the same day.
        - Prefer places whose category matches the traveler's interests where possible.
        """;
    }
}
