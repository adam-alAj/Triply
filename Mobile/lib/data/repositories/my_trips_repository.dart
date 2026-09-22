import '../models/trip_summary.dart';

abstract class MyTripsRepository {
  Future<List<TripSummary>> getMyTrips();

  /// Cover image URL per destination name, from `GET /api/destinations/
  /// assets` — used as a fallback when a trip has no `coverImageUrl` of its
  /// own (e.g. one from before AI generation ran, or on an older trip).
  Future<Map<String, String>> getDestinationImages();
}
