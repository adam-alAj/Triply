import '../../core/network/api_client.dart';
import '../models/home_region.dart';
import '../models/home_trip.dart';
import 'home_repository.dart';

/// Real backend-backed implementation, calling the Trip and Destination
/// modules (see Backend/Triply.Api/Modules/{Trip,Destination}).
class ApiHomeRepository implements HomeRepository {
  ApiHomeRepository({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  // Local rotation — the backend has no destination imagery yet (same gap
  // as My Trips' cards).
  static const _tripImages = [
    'assets/images/home_active_trip.png',
    'assets/images/trip_creation/kyoto_tokyo.jpg',
    'assets/images/trip_creation/amalfi_rome.jpg',
  ];

  static const _regionImages = {
    'Japan': 'assets/images/home_japan.jpg',
    'Italy': 'assets/images/home_italy.jpg',
  };

  @override
  Future<List<HomeRegion>> getFeaturedRegions() async {
    final response = await _apiClient.get<List<dynamic>>('/api/destinations');
    final destinations = response.cast<Map<String, dynamic>>();

    return destinations.take(2).map((destination) {
      final country = destination['countryName'] as String;
      return HomeRegion(
        name: destination['name'] as String,
        country: country,
        description: destination['description'] as String? ?? '',
        imageAsset: _regionImages[country] ?? 'assets/images/home_japan.jpg',
      );
    }).toList();
  }

  @override
  Future<List<HomeTrip>> getRecentTrips() async {
    // Already ordered most-recent-first by the backend (TripsController
    // .GetMyTrips) — the first non-archived trip is the "active" one.
    final response = await _apiClient.get<List<dynamic>>('/api/trips');
    final trips = response.cast<Map<String, dynamic>>();

    final active = trips.cast<Map<String, dynamic>?>().firstWhere(
          (trip) => trip!['status'] != 'ARCHIVED',
          orElse: () => null,
        );

    if (active == null) return [];

    final tripId = active['id'] as String;
    final detail =
        await _apiClient.get<Map<String, dynamic>>('/api/trips/$tripId');

    return [_toHomeTrip(detail)];
  }

  HomeTrip _toHomeTrip(Map<String, dynamic> trip) {
    final destinationName = trip['destinationName'] as String? ?? 'Your Trip';
    final startDate = _parseDate(trip['startDate']);
    final endDate = _parseDate(trip['endDate']);
    final travelerCount = trip['travelerCount'] as int? ?? 1;
    final budgetAmount = (trip['budgetAmount'] as num?)?.toDouble();

    final totalDays = (startDate != null && endDate != null)
        ? endDate.difference(startDate).inDays + 1
        : 1;

    final dayNumber = startDate == null
        ? 1
        : (DateTime.now().difference(startDate).inDays + 1)
            .clamp(1, totalDays);

    final nextActivity = _findNextActivity(trip['itinerary'], dayNumber);

    return HomeTrip(
      id: trip['id'] as String,
      destination: destinationName,
      dateRange: _formatDateRange(startDate, endDate),
      travelersLabel: '$travelerCount ${travelerCount == 1 ? 'Traveler' : 'Travelers'}',
      estimatedCost: budgetAmount != null
          ? '\$${budgetAmount.round()} Est.'
          : 'Est. pending',
      tripTitle: destinationName,
      nextActivity: nextActivity?.$1 ?? 'Itinerary not generated yet',
      nextActivityTime: nextActivity?.$2 ?? 'Check back soon',
      dayLabel: 'Day $dayNumber of $totalDays',
      totalDays: totalDays,
      imageAsset: _tripImages[destinationName.hashCode.abs() % _tripImages.length],
    );
  }

  /// Returns (placeName, timeSlotLabel) for the first item of the given day,
  /// or null if no itinerary has been generated yet.
  (String, String)? _findNextActivity(dynamic itinerary, int dayNumber) {
    if (itinerary is! Map<String, dynamic>) return null;

    final days = (itinerary['days'] as List<dynamic>?)
        ?.cast<Map<String, dynamic>>();
    if (days == null || days.isEmpty) return null;

    final day = days.firstWhere(
      (d) => d['dayNumber'] == dayNumber,
      orElse: () => days.first,
    );

    final items =
        (day['items'] as List<dynamic>?)?.cast<Map<String, dynamic>>();
    if (items == null || items.isEmpty) return null;

    final item = items.first;
    final placeName = item['placeName'] as String? ?? 'Activity';
    final timeSlot = item['timeSlot'] as String? ?? '';

    return (placeName, timeSlot.isEmpty ? 'Next up' : 'Next up • $timeSlot');
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
}
