/// Mirrors the backend's `ItineraryItemResponse` shape (Backend/Triply.Api/
/// Modules/Itinerary/Dtos) so swapping the mock repository for a real one
/// later is a pure data-source change, not a model rewrite.
class ItineraryItemData {
  const ItineraryItemData({
    required this.timeSlot,
    required this.orderIndex,
    required this.placeName,
    required this.subtitle,
    required this.estimatedCostLabel,
    required this.isAiGenerated,
    this.tipText,
    this.notes,
  });

  final String timeSlot; // MORNING, AFTERNOON, EVENING
  final int orderIndex;
  final String placeName;
  final String subtitle;
  final String estimatedCostLabel;
  final bool isAiGenerated;
  final String? tipText;
  final String? notes;

  ItineraryItemData copyWith({
    String? timeSlot,
    String? subtitle,
    String? estimatedCostLabel,
    bool? isAiGenerated,
    String? notes,
  }) {
    return ItineraryItemData(
      timeSlot: timeSlot ?? this.timeSlot,
      orderIndex: orderIndex,
      placeName: placeName,
      subtitle: subtitle ?? this.subtitle,
      estimatedCostLabel: estimatedCostLabel ?? this.estimatedCostLabel,
      isAiGenerated: isAiGenerated ?? this.isAiGenerated,
      tipText: tipText,
      notes: notes ?? this.notes,
    );
  }
}

/// Mirrors `ItineraryDayResponse`.
class ItineraryDayData {
  const ItineraryDayData({
    required this.dayNumber,
    required this.dateLabel,
    required this.items,
  });

  final int dayNumber;
  final String dateLabel; // e.g. "Oct 15"
  final List<ItineraryItemData> items;

  ItineraryDayData copyWith({List<ItineraryItemData>? items}) {
    return ItineraryDayData(
      dayNumber: dayNumber,
      dateLabel: dateLabel,
      items: items ?? this.items,
    );
  }
}

/// A booked/planned line under a cost category, e.g. "4 nights Kyoto
/// Machiya Ryokan — $680". Amounts are raw USD numbers (not pre-formatted
/// strings) so the screen's currency toggle can convert and format them
/// consistently at render time.
class CostLineItemData {
  const CostLineItemData({required this.label, required this.amountUsd});

  final String label;
  final double amountUsd;
}

/// How confident this category's estimate is — drives the badge shown.
/// Most categories are plain estimates; a category checked against real,
/// bookable data (e.g. ticketed activities) can be marked as matched.
enum CostAccuracy { estimated, verifiedAiMatched }

/// Mirrors `CostCategoryEstimateResponse`, extended with the breakdown
/// (percent of trip total, itemized lines) the richer Costs tab design
/// needs. Fixed category `code`s per the backend's seeded `CostCategory`
/// rows (ApplicationDbContext.cs): ACCOMMODATION, TRANSPORTATION, FOOD,
/// ACTIVITIES, OTHER — `label` is just the display name and can be styled
/// per trip (e.g. OTHER shown as "Reserve / Contingency").
class CostCategoryEstimateData {
  const CostCategoryEstimateData({
    required this.code,
    required this.label,
    required this.amountUsd,
    required this.percentOfTotal,
    required this.accuracy,
    this.contextLabel,
    this.items = const [],
  });

  final String code;
  final String label;
  final double amountUsd;
  final int percentOfTotal;
  final CostAccuracy accuracy;
  final String? contextLabel; // "7 Nights", "2 Travelers", "~$38/day pp"...
  final List<CostLineItemData> items;
}

/// Tracks the trip's spend against a target budget cap for the "Budget
/// Health" section. `targetCapUsd` is the user's own target (Trip.budget_
/// amount), not a backend-computed figure.
class BudgetHealthData {
  const BudgetHealthData({
    required this.targetCapUsd,
    required this.spentUsd,
  });

  final double targetCapUsd;
  final double spentUsd;

  double get remainingUsd => targetCapUsd - spentUsd;

  double get spentFraction =>
      targetCapUsd <= 0 ? 0 : (spentUsd / targetCapUsd).clamp(0, 1.2);

  bool get isUnderBudget => spentUsd <= targetCapUsd;
}

/// A single featured booking (e.g. the main accommodation) surfaced at the
/// bottom of the Costs tab.
class StayHighlightData {
  const StayHighlightData({
    required this.imageAsset,
    required this.title,
    required this.subtitle,
  });

  final String imageAsset;
  final String title;
  final String subtitle;
}

/// The subset of `TripResponse` the Trip Overview screen needs: header
/// info, the day-by-day itinerary, and the cost breakdown.
class TripOverviewData {
  const TripOverviewData({
    required this.id,
    required this.tripTitle,
    required this.regionLabel,
    required this.dateRangeLabel,
    required this.totalDays,
    required this.travelerCount,
    required this.status,
    required this.totalEstimatedCostUsd,
    required this.avgPerDayPerTravelerUsd,
    required this.isOnTarget,
    required this.budgetHealth,
    required this.days,
    required this.costCategories,
    this.stayHighlight,
  });

  final String id;
  final String tripTitle; // "Kyoto & Tokyo Discovery"
  final String regionLabel; // "Japan • Honshu Region"
  final String dateRangeLabel; // "Oct 14 – 21"
  final int totalDays;
  final int travelerCount;
  final String status; // DRAFT, GENERATING, GENERATED, MODIFIED, SAVED, ARCHIVED
  final double totalEstimatedCostUsd;
  final double avgPerDayPerTravelerUsd;
  final bool isOnTarget;
  final BudgetHealthData budgetHealth;
  final List<ItineraryDayData> days;
  final List<CostCategoryEstimateData> costCategories;
  final StayHighlightData? stayHighlight;

  TripOverviewData copyWith({
    String? status,
    List<ItineraryDayData>? days,
  }) {
    return TripOverviewData(
      id: id,
      tripTitle: tripTitle,
      regionLabel: regionLabel,
      dateRangeLabel: dateRangeLabel,
      totalDays: totalDays,
      travelerCount: travelerCount,
      status: status ?? this.status,
      totalEstimatedCostUsd: totalEstimatedCostUsd,
      avgPerDayPerTravelerUsd: avgPerDayPerTravelerUsd,
      isOnTarget: isOnTarget,
      budgetHealth: budgetHealth,
      days: days ?? this.days,
      costCategories: costCategories,
      stayHighlight: stayHighlight,
    );
  }
}
