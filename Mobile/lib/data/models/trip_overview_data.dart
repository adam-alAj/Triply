import 'package:flutter/material.dart' show IconData;

/// A small pill badge overlaid on an item's hero image (e.g. "UNESCO
/// Sanctuary", "Official Partner") — icon + label, purely decorative.
class HeroBadgeData {
  const HeroBadgeData({required this.icon, required this.label});

  final IconData icon;
  final String label;
}

/// How busy a place currently is vs. its daily peak — drives the "Optimal
/// Crowd Cadence" panel on the Place Detail Sheet. No backend field for
/// this yet; populated only where the mock/demo data supplies it.
enum CrowdCadenceLevel { low, moderate, high }

class CrowdCadenceData {
  const CrowdCadenceData({
    required this.level,
    required this.moodLabel, // "SERENE"
    required this.currentTimeLabel, // "09:30 AM"
    required this.currentCapacityPercent, // 18
    required this.peakTimeLabel, // "12:00 PM"
    required this.peakCapacityPercent, // 85
  });

  final CrowdCadenceLevel level;
  final String moodLabel;
  final String currentTimeLabel;
  final int currentCapacityPercent;
  final String peakTimeLabel;
  final int peakCapacityPercent;

  String get levelLabel => switch (level) {
        CrowdCadenceLevel.low => 'LOW',
        CrowdCadenceLevel.moderate => 'MODERATE',
        CrowdCadenceLevel.high => 'HIGH',
      };
}

/// Formats a minutes-from-midnight clock value as "09:30 AM".
String formatClockTime(int minutesFromMidnight) {
  final normalized = minutesFromMidnight % (24 * 60);
  final hour24 = normalized ~/ 60;
  final minute = normalized % 60;
  final period = hour24 >= 12 ? 'PM' : 'AM';
  final hour12 = hour24 % 12 == 0 ? 12 : hour24 % 12;
  return '${hour12.toString().padLeft(2, '0')}:${minute.toString().padLeft(2, '0')} $period';
}

/// Formats a duration in minutes as "2 hrs" / "1 hr 30 min" / "45 min".
String formatDurationLabel(int minutes) {
  final hours = minutes ~/ 60;
  final mins = minutes % 60;
  if (hours == 0) return '$mins min';
  if (mins == 0) return '$hours hr${hours == 1 ? '' : 's'}';
  return '$hours hr${hours == 1 ? '' : 's'} $mins min';
}

/// Mirrors the backend's `ItineraryItemResponse` shape (Backend/Triply.Api/
/// Modules/Itinerary/Dtos) so swapping the mock repository for a real one
/// later is a pure data-source change, not a model rewrite.
class ItineraryItemData {
  const ItineraryItemData({
    required this.id,
    required this.placeId,
    required this.timeSlot,
    required this.orderIndex,
    required this.placeName,
    required this.subtitle,
    required this.estimatedCostLabel,
    required this.isAiGenerated,
    this.tipText,
    this.notes,
    this.heroImageUrl,
    this.locationLabel,
    this.startTimeMinutes,
    this.durationMinutes,
    this.priceUsdLabel,
    this.priceLocalLabel,
    this.priceContextLabel,
    this.heroBadges = const [],
    this.crowdCadence,
    this.isPlaceholderEnrichment = false,
  });

  /// The itinerary item's own GUID — needed for `PATCH .../items/{itemId}`.
  final String id;

  /// The place this item currently points at — resent unchanged on edit
  /// since there's no place picker UI yet.
  final int placeId;
  final String timeSlot; // MORNING, AFTERNOON, EVENING
  final int orderIndex;
  final String placeName;
  final String subtitle;
  final String estimatedCostLabel;
  final bool isAiGenerated;
  final String? tipText;
  final String? notes;

  // Rich Place Detail Sheet / Edit Item Modal fields — no backend
  // equivalent yet (PENDING BACKEND), null/empty until one exists.
  final String? heroImageUrl;
  final String? locationLabel; // "Arashiyama District • West Kyoto"
  final int? startTimeMinutes; // minutes since midnight
  final int? durationMinutes;
  final String? priceUsdLabel; // "~$12"
  final String? priceLocalLabel; // "¥1,800"
  final String? priceContextLabel; // "Verified Entry Fee (Gardens + Hodo)"
  final List<HeroBadgeData> heroBadges;
  final CrowdCadenceData? crowdCadence;

  /// True when the fields above were filled with generic placeholder
  /// values by [ApiTripOverviewRepository] rather than real backend data
  /// (no source fields exist there yet) — drives a small "Preview data"
  /// label on the Place Detail Sheet instead of presenting them as fact.
  final bool isPlaceholderEnrichment;

  int? get endTimeMinutes => (startTimeMinutes != null && durationMinutes != null)
      ? startTimeMinutes! + durationMinutes!
      : null;

  ItineraryItemData copyWith({
    String? timeSlot,
    int? orderIndex,
    String? placeName,
    String? subtitle,
    String? estimatedCostLabel,
    bool? isAiGenerated,
    String? notes,
    int? startTimeMinutes,
    int? durationMinutes,
  }) {
    return ItineraryItemData(
      id: id,
      placeId: placeId,
      timeSlot: timeSlot ?? this.timeSlot,
      orderIndex: orderIndex ?? this.orderIndex,
      placeName: placeName ?? this.placeName,
      subtitle: subtitle ?? this.subtitle,
      estimatedCostLabel: estimatedCostLabel ?? this.estimatedCostLabel,
      isAiGenerated: isAiGenerated ?? this.isAiGenerated,
      tipText: tipText,
      notes: notes ?? this.notes,
      heroImageUrl: heroImageUrl,
      locationLabel: locationLabel,
      startTimeMinutes: startTimeMinutes ?? this.startTimeMinutes,
      durationMinutes: durationMinutes ?? this.durationMinutes,
      priceUsdLabel: priceUsdLabel,
      priceLocalLabel: priceLocalLabel,
      priceContextLabel: priceContextLabel,
      heroBadges: heroBadges,
      crowdCadence: crowdCadence,
      isPlaceholderEnrichment: isPlaceholderEnrichment,
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
    this.imageUrl,
  });

  final String imageAsset;
  final String title;
  final String subtitle;

  /// Real cover photo, preferred over [imageAsset] when present. There's no
  /// per-place image yet (see progress.md), so this is the trip's
  /// destination cover photo as a stand-in — not the hotel itself, but a
  /// real photo of where it is rather than a generic local asset.
  final String? imageUrl;
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
    required this.version,
    required this.totalEstimatedCostUsd,
    required this.avgPerDayPerTravelerUsd,
    required this.isOnTarget,
    required this.budgetHealth,
    required this.days,
    required this.costCategories,
    this.stayHighlight,
    this.coverImageUrl,
  });

  final String id;
  final String tripTitle; // "Kyoto & Tokyo Discovery"
  final String regionLabel; // "Japan • Honshu Region"
  final String dateRangeLabel; // "Oct 14 – 21"
  final int totalDays;
  final int travelerCount;
  final String status; // DRAFT, GENERATING, GENERATED, MODIFIED, SAVED, ARCHIVED

  /// Optimistic-concurrency token (`Trip.Version` server-side). Sent back as
  /// `expectedVersion` on partial regeneration so a stale regenerate-sheet
  /// request can't overwrite a trip that changed elsewhere.
  final int version;
  final double totalEstimatedCostUsd;
  final double avgPerDayPerTravelerUsd;
  final bool isOnTarget;
  final BudgetHealthData budgetHealth;
  final List<ItineraryDayData> days;
  final List<CostCategoryEstimateData> costCategories;
  final StayHighlightData? stayHighlight;
  final String? coverImageUrl;

  TripOverviewData copyWith({
    String? status,
    int? version,
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
      version: version ?? this.version,
      totalEstimatedCostUsd: totalEstimatedCostUsd,
      avgPerDayPerTravelerUsd: avgPerDayPerTravelerUsd,
      isOnTarget: isOnTarget,
      budgetHealth: budgetHealth,
      days: days ?? this.days,
      costCategories: costCategories,
      stayHighlight: stayHighlight,
      coverImageUrl: coverImageUrl,
    );
  }
}
