import '../../core/network/api_client.dart';
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
    final startDate = _parseDate(trip['startDate']);
    final endDate = _parseDate(trip['endDate']);
    final travelerCount = trip['travelerCount'] as int? ?? 1;
    final budgetAmount = (trip['budgetAmount'] as num?)?.toDouble();
    final status = trip['status'] as String? ?? 'DRAFT';

    final totalDays = (startDate != null && endDate != null)
        ? endDate.difference(startDate).inDays + 1
        : 1;

    final itinerary = trip['itinerary'] as Map<String, dynamic>?;
    final costEstimate = trip['costEstimate'] as Map<String, dynamic>?;

    final days = _mapDays(itinerary);
    final costCategories = _mapCostCategories(costEstimate);

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
      // PENDING BACKEND: no country/region breakdown beyond the
      // destination's own name — see progress.md's Place Detail gap.
      regionLabel: destinationName,
      dateRangeLabel: _formatDateRange(startDate, endDate),
      totalDays: totalDays,
      travelerCount: travelerCount,
      status: status,
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
    );
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
          return ItineraryItemData(
            id: item['id'] as String? ?? '',
            placeId: (item['placeId'] as num?)?.toInt() ?? 0,
            timeSlot: item['timeSlot'] as String? ?? 'MORNING',
            orderIndex: item['orderIndex'] as int? ?? 0,
            placeName: item['placeName'] as String? ?? 'Activity',
            // PENDING BACKEND: no place description — see progress.md.
            subtitle: '',
            estimatedCostLabel: 'Est. \$${cost.round()}',
            isAiGenerated: item['isAiGenerated'] as bool? ?? true,
            notes: item['notes'] as String?,
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
