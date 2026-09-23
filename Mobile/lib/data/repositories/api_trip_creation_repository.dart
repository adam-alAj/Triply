import '../../core/network/api_client.dart';
import '../../core/network/destination_assets_cache.dart';
import '../models/generation_outcome.dart';
import '../models/trip_creation_data.dart';
import 'trip_creation_repository.dart';

/// Real backend-backed implementation, calling the Trip and Destination
/// modules (see Backend/Triply.Api/Modules/{Trip,Destination}).
class ApiTripCreationRepository implements TripCreationRepository {
  ApiTripCreationRepository({required ApiClient apiClient})
      : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<Map<String, dynamic>>> getDestinations() async {
    final destinations =
        await DestinationAssetsCache.instance.getDestinations(_apiClient);

    return destinations.map((destination) {
      return <String, dynamic>{
        'id': destination['id'].toString(),
        'destinationId': destination['id'] as int,
        'name': destination['name'] as String,
        'country': destination['countryName'] as String,
        'description': destination['description'] as String? ?? '',
      };
    }).toList();
  }

  @override
  Future<Map<String, String>> getDestinationImages() {
    return DestinationAssetsCache.instance.getImages(_apiClient);
  }

  @override
  Future<List<Map<String, dynamic>>> getDestinationSuggestions(
    TripCreationData data,
  ) async {
    if (data.planningMode != PlanningMode.budgetFirst) {
      return [];
    }

    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/destinations/suggestions',
      data: {
        'budgetAmount': data.budget,
        'budgetCurrencyId': TripCreationData.budgetCurrencyId,
        'interestCategoryIds': data.interestCategoryIds,
      },
    );

    final suggestions =
        (response['suggestions'] as List<dynamic>? ?? const [])
            .cast<Map<String, dynamic>>();

    final images = await DestinationAssetsCache.instance.getImages(_apiClient);

    return suggestions.map((suggestion) {
      final destinationId = suggestion['destinationId'] as int;
      final name = suggestion['destinationName'] as String;
      final country = suggestion['countryName'] as String;
      final cost = suggestion['estimatedCost'];
      final currency = suggestion['currency'] as String;

      return <String, dynamic>{
        'id': destinationId.toString(),
        'destinationId': destinationId,
        'name': name,
        'country': country,
        'imageUrl': images[name],
        // Falls back to a placeholder icon (see _SuggestionCard's
        // errorBuilder) when there's no real cover photo for this name.
        'image': 'assets/images/trip_creation/placeholder.jpg',
        'description': 'AI-curated pick in $country',
        'estimatedCost': '$currency $cost avg.',
      };
    }).toList();
  }

  @override
  Future<String> createTrip(TripCreationData data) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/trips',
      data: {
        'planningMode': data.planningMode == PlanningMode.budgetFirst
            ? 'BUDGET_FIRST'
            : 'DESTINATION_FIRST',
        'destinationId': data.destinationId,
        'startDate':
            data.startDate != null ? _dateOnly(data.startDate!) : null,
        'endDate': data.endDate != null ? _dateOnly(data.endDate!) : null,
        'travelerCount': data.travelers,
        'budgetAmount': data.budget,
        'budgetCurrencyId':
            data.budget != null ? TripCreationData.budgetCurrencyId : null,
        'interestCategoryIds': data.interestCategoryIds,
      },
    );

    return response['id'] as String;
  }

  @override
  Future<GenerationOutcome> startGeneration(String tripId) async {
    // AI generation runs up to 3 bounded-retry attempts server-side (each
    // its own Gemini call), easily exceeding the client's default 15s
    // timeout even on a successful run.
    final response = await _apiClient.post<Map<String, dynamic>?>(
      '/api/trips/$tripId/generate',
      receiveTimeout: const Duration(seconds: 120),
    );

    if (response == null) return GenerationOutcome.unknown;

    // Only the over-budget signal lives here; the itinerary itself is loaded
    // from GET /api/trips/{id} when Trip Overview opens.
    return GenerationOutcome.fromJson(response);
  }

  String _dateOnly(DateTime date) {
    final year = date.year.toString().padLeft(4, '0');
    final month = date.month.toString().padLeft(2, '0');
    final day = date.day.toString().padLeft(2, '0');
    return '$year-$month-$day';
  }
}
