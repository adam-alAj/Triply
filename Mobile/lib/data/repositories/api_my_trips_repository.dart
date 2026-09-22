import '../../core/network/api_client.dart';
import '../../core/network/destination_assets_cache.dart';
import '../models/trip_summary.dart';
import 'my_trips_repository.dart';

/// Real backend-backed implementation, calling `GET /api/trips` (see
/// Backend/Triply.Api/Modules/Trip/TripsController.cs `GetMyTrips`).
class ApiMyTripsRepository implements MyTripsRepository {
  ApiMyTripsRepository({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<TripSummary>> getMyTrips() async {
    final response = await _apiClient.get<List<dynamic>>('/api/trips');

    return response
        .cast<Map<String, dynamic>>()
        .map(TripSummary.fromJson)
        .toList();
  }

  @override
  Future<Map<String, String>> getDestinationImages() {
    return DestinationAssetsCache.instance.getImages(_apiClient);
  }
}
