import 'package:flutter/material.dart' show Icons;

import '../../core/network/api_client.dart';
import '../../core/network/destination_assets_cache.dart';
import '../models/trip_overview_data.dart';
import 'trip_overview_repository.dart';

/// Real backend-backed implementation, calling `GET /api/trips/{id}` (see
/// Backend/Triply.Api/Modules/Trip/TripsController.cs `GetById`), which
/// already embeds the itinerary and cost estimate in one response.
class ApiTripOverviewRepository implements TripOverviewRepository {
  ApiTripOverviewRepository({required ApiClient apiClient})
      : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<TripOverviewData> getTrip(String tripId) async {
    final trip = await _apiClient.get<Map<String, dynamic>>('/api/trips/$tripId');

    final destinationName = trip['destinationName'] as String? ?? 'Your Trip';
    final title = trip['title'] as String? ?? destinationName;
    final coverImageUrl = trip['coverImageUrl'] as String?;
    final countryName = await _fetchCountryName(destinationName);
    final startDate = _parseDate(trip['startDate']);
    final endDate = _parseDate(trip['endDate']);
    final travelerCount = trip['travelerCount'] as int? ?? 1;
    final budgetAmount = (trip['budgetAmount'] as num?)?.toDouble();
    final status = trip['status'] as String? ?? 'DRAFT';
    final version = trip['version'] as int? ?? 1;

    final totalDays = (startDate != null && endDate != null)
        ? endDate.difference(startDate).inDays + 1
        : 1;

    final itinerary = trip['itinerary'] as Map<String, dynamic>?;
    final costEstimate = trip['costEstimate'] as Map<String, dynamic>?;

    final days = _mapDays(itinerary);
    final costCategories = _mapCostCategories(costEstimate);
    final stayHighlight = await _buildStayHighlight(days, destinationName);

    final totalEstimatedCostUsd =
        (costEstimate?['totalEstimatedCost'] as num?)?.toDouble() ??
            budgetAmount ??
            0;

    final avgPerDayPerTravelerUsd = (totalDays > 0 && travelerCount > 0)
        ? totalEstimatedCostUsd / (totalDays * travelerCount)
        : 0.0;

    final targetCapUsd = budgetAmount ?? totalEstimatedCostUsd;

    return TripOverviewData(
      id: trip['id'] as String,
      tripTitle: title,
      regionLabel: countryName == null
          ? destinationName
          : '$destinationName • $countryName',
      dateRangeLabel: _formatDateRange(startDate, endDate),
      totalDays: totalDays,
      travelerCount: travelerCount,
      status: status,
      version: version,
      totalEstimatedCostUsd: totalEstimatedCostUsd,
      avgPerDayPerTravelerUsd: avgPerDayPerTravelerUsd,
      isOnTarget: totalEstimatedCostUsd <= targetCapUsd,
      budgetHealth: BudgetHealthData(
        targetCapUsd: targetCapUsd,
        spentUsd: totalEstimatedCostUsd,
      ),
      days: days,
      costCategories: costCategories,
      coverImageUrl: coverImageUrl,
      stayHighlight: stayHighlight,
    );
  }

  /// There's no dedicated "accommodation" field on the trip response — the
  /// AI/prompt convention (see AiOrchestrationService's Gemini schema)
  /// flags the accommodation item by prefixing its `Notes` with
  /// "Accommodation:" (e.g. "Accommodation: 2 nights"), the same
  /// convention the backend itself checks for elsewhere (ItinerariesController
  /// blocks editing that item as a regular activity). Scans for it instead
  /// of requiring a new backend field. Returns null if the trip has no
  /// itinerary yet, or no item matches.
  Future<StayHighlightData?> _buildStayHighlight(
    List<ItineraryDayData> days,
    String destinationName,
  ) async {
    for (final day in days) {
      for (final item in day.items) {
        final notes = item.notes;
        if (notes == null || !notes.toLowerCase().startsWith('accommodation:')) {
          continue;
        }

        final nightsLabel = notes.substring('accommodation:'.length).trim();
        String? imageUrl;
        try {
          final images = await DestinationAssetsCache.instance.getImages(_apiClient);
          imageUrl = images[destinationName];
        } catch (_) {
          // Best-effort — falls back to the local asset below.
        }

        return StayHighlightData(
          imageAsset: 'assets/images/trip_creation/kyoto_tokyo.jpg',
          imageUrl: imageUrl,
          title: item.placeName,
          subtitle: nightsLabel.isEmpty
              ? item.estimatedCostLabel
              : '$nightsLabel • ${item.estimatedCostLabel}',
        );
      }
    }

    return null;
  }

  /// `GET /api/trips/{id}` only returns the destination's name, not its
  /// country — cross-referenced against `GET /api/destinations` (a short,
  /// cheap list) for a "Amman • Jordan" style region label. Best-effort:
  /// falls back to just the destination name on any failure.
  Future<String?> _fetchCountryName(String destinationName) async {
    try {
      final destinations =
          await DestinationAssetsCache.instance.getDestinations(_apiClient);

      final match = destinations.cast<Map<String, dynamic>?>().firstWhere(
            (d) => d!['name'] == destinationName,
            orElse: () => null,
          );

      return match?['countryName'] as String?;
    } catch (_) {
      return null;
    }
  }

  List<ItineraryDayData> _mapDays(Map<String, dynamic>? itinerary) {
    final days = (itinerary?['days'] as List<dynamic>?)
        ?.cast<Map<String, dynamic>>();
    if (days == null) return [];

    return days.map((day) {
      final items = (day['items'] as List<dynamic>?)
              ?.cast<Map<String, dynamic>>() ??
          [];

      return ItineraryDayData(
        dayNumber: day['dayNumber'] as int,
        dateLabel: _formatDate(_parseDate(day['date'])),
        items: items.map((item) {
          final cost = (item['estimatedCost'] as num?)?.toDouble() ?? 0;
          final timeSlot = item['timeSlot'] as String? ?? 'MORNING';
          final placeId = (item['placeId'] as num?)?.toInt() ?? 0;

          return ItineraryItemData(
            id: item['id'] as String? ?? '',
            placeId: placeId,
            timeSlot: timeSlot,
            orderIndex: item['orderIndex'] as int? ?? 0,
            placeName: item['placeName'] as String? ?? 'Activity',
            // PENDING BACKEND: no place description — see progress.md.
            subtitle: '',
            estimatedCostLabel: 'Est. \$${cost.round()}',
            isAiGenerated: item['isAiGenerated'] as bool? ?? true,
            notes: item['notes'] as String?,
            // PENDING BACKEND: the Place Detail Sheet's hero image, crowd
            // cadence and verification badges have no source fields on
            // ItineraryItemResponse yet (flagged to the backend track).
            // Filled with clearly-labeled placeholder data below instead
            // of hiding the design — swap for real fields once they land.
            heroImageUrl: 'https://picsum.photos/seed/place$placeId/800/400',
            startTimeMinutes: _placeholderStartMinutes(timeSlot),
            durationMinutes: 90,
            priceContextLabel: 'Estimate — pricing verification pending',
            heroBadges: const [
              HeroBadgeData(icon: Icons.info_outline, label: 'Preview Data'),
            ],
            crowdCadence: const CrowdCadenceData(
              level: CrowdCadenceLevel.moderate,
              moodLabel: 'DEMO',
              currentTimeLabel: 'Sample data — live feed pending',
              currentCapacityPercent: 50,
              peakTimeLabel: 'Backend integration pending',
              peakCapacityPercent: 80,
            ),
            isPlaceholderEnrichment: true,
          );
        }).toList(),
      );
    }).toList();
  }

  List<CostCategoryEstimateData> _mapCostCategories(
    Map<String, dynamic>? costEstimate,
  ) {
    final categories = (costEstimate?['categories'] as List<dynamic>?)
        ?.cast<Map<String, dynamic>>();
    if (categories == null || categories.isEmpty) return [];

    final total = (costEstimate!['totalEstimatedCost'] as num?)?.toDouble() ?? 0;

    return categories.map((category) {
      final amount = (category['amount'] as num?)?.toDouble() ?? 0;
      final percent = total > 0 ? ((amount / total) * 100).round() : 0;

      return CostCategoryEstimateData(
        code: category['categoryCode'] as String? ?? 'OTHER',
        label: category['categoryName'] as String? ?? 'Other',
        amountUsd: amount,
        percentOfTotal: percent,
        accuracy: (category['isEstimated'] as bool? ?? true)
            ? CostAccuracy.estimated
            : CostAccuracy.verifiedAiMatched,
      );
    }).toList();
  }

  /// Rough start time per time-slot bucket — the backend only sends
  /// MORNING/AFTERNOON/EVENING, not a real clock time, so this is a
  /// placeholder until `startTime` exists on `ItineraryItemResponse`.
  int _placeholderStartMinutes(String timeSlot) => switch (timeSlot) {
        'MORNING' => 9 * 60,
        'AFTERNOON' => 13 * 60,
        'EVENING' => 19 * 60,
        _ => 9 * 60,
      };

  DateTime? _parseDate(dynamic value) {
    if (value is! String || value.isEmpty) return null;
    return DateTime.tryParse(value);
  }

  String _formatDateRange(DateTime? start, DateTime? end) {
    if (start == null || end == null) return 'Dates not set';

    const months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
    ];

    return '${months[start.month - 1]} ${start.day} – ${end.day}';
  }

  String _formatDate(DateTime? date) {
    if (date == null) return '';

    const months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
    ];

    return '${months[date.month - 1]} ${date.day}';
  }
}
