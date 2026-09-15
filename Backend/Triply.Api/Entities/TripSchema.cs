namespace Triply.Api.Entities;

// Database Design §6.9
public class Trip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = default!;
    public long? DestinationId { get; set; }
    public Destination? Destination { get; set; }
    public string PlanningMode { get; set; } = default!; // DESTINATION_FIRST | BUDGET_FIRST
    public string Status { get; set; } = "DRAFT";         // §19 lifecycle
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int TravelerCount { get; set; } = 1;           // CHECK > 0
    public decimal? BudgetAmount { get; set; }
    public long? BudgetCurrencyId { get; set; }
    public Currency? BudgetCurrency { get; set; }
    public decimal? TotalEstimatedCost { get; set; }      // denormalized cache §15
    public int Version { get; set; } = 1;                 // optimistic concurrency
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public ICollection<TripInterest> TripInterests { get; set; } = new List<TripInterest>();
    public Itinerary? Itinerary { get; set; }
    public ICollection<CostEstimate> CostEstimates { get; set; } = new List<CostEstimate>();
    public ICollection<AIGeneration> AIGenerations { get; set; } = new List<AIGeneration>();
}

// §6.10 — junction, composite PK
public class TripInterest
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = default!;
    public long InterestCategoryId { get; set; }
    public InterestCategory InterestCategory { get; set; } = default!;
}

// §6.11
public class Itinerary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = default!;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public Guid? AiGenerationId { get; set; }

    public ICollection<ItineraryDay> Days { get; set; } = new List<ItineraryDay>();
}

// §6.12
public class ItineraryDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItineraryId { get; set; }
    public Itinerary Itinerary { get; set; } = default!;
    public int DayNumber { get; set; }   // CHECK > 0, UNIQUE(itinerary_id, day_number)
    public DateOnly Date { get; set; }

    public ICollection<ItineraryItem> Items { get; set; } = new List<ItineraryItem>();
}

// §6.13 — the FR-AI-002 enforcement point: place_id is NOT NULL
public class ItineraryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItineraryDayId { get; set; }
    public ItineraryDay ItineraryDay { get; set; } = default!;
    public long PlaceId { get; set; }              // mandatory FK -> Place
    public Place Place { get; set; } = default!;
    public string TimeSlot { get; set; } = default!; // MORNING|AFTERNOON|EVENING
    public int OrderIndex { get; set; }
    public decimal EstimatedCost { get; set; }       // snapshot copy, §15
    public string? Notes { get; set; }
    public bool IsAiGenerated { get; set; } = true;
    public DateTime? ModifiedAt { get; set; }
}

// §6.14
public class CostEstimate
{
    public long Id { get; set; }
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = default!;
    public long CostCategoryId { get; set; }
    public CostCategory CostCategory { get; set; } = default!;
    public decimal Amount { get; set; }              // CHECK >= 0
    public long CurrencyId { get; set; }
    public Currency Currency { get; set; } = default!;
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    // UNIQUE(trip_id, cost_category_id) — enforced in DbContext
}

// §6.15 — AI generation attempt tracking (backend-owned call, ADR-01)
public class AIGeneration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = default!;
    public int AttemptNumber { get; set; } = 1;
    public string ModelProvider { get; set; } = default!;
    public string InputSnapshot { get; set; } = default!; // JSON column
    public string? RawOutput { get; set; }                 // JSON, retention-limited §16
    public string Status { get; set; } = "PENDING";        // PENDING|SUCCEEDED|FAILED_VALIDATION|FAILED_ERROR
    public string? ValidationErrors { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
