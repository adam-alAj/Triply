import 'api_client.dart';

/// In-memory, app-session-lifetime cache for `GET /api/destinations` and
/// `GET /api/destinations/assets` — both are near-static reference data
/// (destinations don't change mid-session) fetched independently by Home,
/// My Trips, Trip Creation, and Trip Overview on every single screen visit.
/// Without this, bouncing between those screens a few times inside a
/// minute is enough to trip the backend's fixed-window rate limiter
/// (429 Too Many Requests) — each fetch here is now shared and made once.
class DestinationAssetsCache {
  DestinationAssetsCache._();

  static final DestinationAssetsCache instance = DestinationAssetsCache._();

  Future<List<Map<String, dynamic>>>? _destinations;
  Future<Map<String, String>>? _images;

  Future<List<Map<String, dynamic>>> getDestinations(ApiClient apiClient) {
    return _destinations ??= _loadDestinations(apiClient);
  }

  Future<Map<String, String>> getImages(ApiClient apiClient) {
    return _images ??= _loadImages(apiClient);
  }

  Future<List<Map<String, dynamic>>> _loadDestinations(
    ApiClient apiClient,
  ) async {
    try {
      final response = await apiClient.get<List<dynamic>>('/api/destinations');
      return response.cast<Map<String, dynamic>>();
    } catch (error) {
      // Don't cache a failure — the next caller should be free to retry.
      _destinations = null;
      rethrow;
    }
  }

  Future<Map<String, String>> _loadImages(ApiClient apiClient) async {
    try {
      final response =
          await apiClient.get<Map<String, dynamic>>('/api/destinations/assets');
      final entries = (response['destinations'] as List<dynamic>?) ?? [];

      return {
        for (final entry in entries.cast<Map<String, dynamic>>())
          entry['destinationName'] as String: entry['url'] as String,
      };
    } catch (error) {
      _images = null;
      rethrow;
    }
  }
}
