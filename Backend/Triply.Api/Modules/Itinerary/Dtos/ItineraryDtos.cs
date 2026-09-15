namespace Triply.Api.Modules.Itinerary.Dtos;

public sealed class WriteItineraryRequest
{
    public List<WriteItineraryDayRequest> Days { get; set; } = new();
}

public sealed class WriteItineraryDayRequest
{
    public int DayNumber { get; set; }
    public DateOnly Date { get; set; }
    public List<WriteItineraryItemRequest> Items { get; set; } = new();
}

public sealed class WriteItineraryItemRequest
{
    public long PlaceId { get; set; }
    public string TimeSlot { get; set; } = default!;
    public int OrderIndex { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? Notes { get; set; }
    public bool IsAiGenerated { get; set; } = true;
}

public sealed class ItineraryResponse
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<ItineraryDayResponse> Days { get; set; } = new();
}

public sealed class ItineraryDayResponse
{
    public Guid Id { get; set; }
    public int DayNumber { get; set; }
    public DateOnly Date { get; set; }
    public List<ItineraryItemResponse> Items { get; set; } = new();
}

public sealed class ItineraryItemResponse
{
    public Guid Id { get; set; }
    public long PlaceId { get; set; }
    public string PlaceName { get; set; } = default!;
    public string TimeSlot { get; set; } = default!;
    public int OrderIndex { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? Notes { get; set; }
    public bool IsAiGenerated { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
