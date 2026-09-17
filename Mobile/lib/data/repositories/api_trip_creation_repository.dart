import '../../core/network/api_client.dart';
import '../models/trip_creation_data.dart';
import 'trip_creation_repository.dart';

/// Real backend-backed implementation, calling the Trip and Destination
/// modules (see Backend/Triply.Api/Modules/{Trip,Destination}).
///
/// NOTE: destination-first suggestions still need `GET /api/destinations`
/// (a plain supported-destinations list), which doesn't exist on the
/// backend yet — flagged separately. Only the budget-first path
/// (`POST /api/destinations/suggestions`) is wired to real data here.
class ApiTripCreationRepository implements TripCreationRepository {
  ApiTripCreationRepository({required ApiClient apiClient})
      : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<Map<String, dynamic>>> getDestinationSuggestions(
    TripCreationData data,
  ) async {
    if (data.planningMode != PlanningMode.budgetFirst) {
      // PENDING BACKEND: GET /api/destinations. Destination-first still
      // renders the local placeholder list in budget_destination_screen.dart.
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
        // The backend doesn't serve destination imagery yet; the suggestion
        // card already falls back to a placeholder icon on a missing asset.
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
  Future<void> startGeneration(String tripId) async {
    await _apiClient.post<Map<String, dynamic>>(
      '/api/trips/$tripId/generate',
    );
  }

  String _dateOnly(DateTime date) {
    final year = date.year.toString().padLeft(4, '0');
    final month = date.month.toString().padLeft(2, '0');
    final day = date.day.toString().padLeft(2, '0');
    return '$year-$month-$day';
  }
}
