import '../models/trip_creation_data.dart';

abstract class TripCreationRepository {
  Future<List<Map<String, dynamic>>> getDestinationSuggestions(
    TripCreationData data,
  );

  /// Plain supported-destinations list for the destination-first path
  /// (`GET /api/destinations`) — no budget/interest filtering, unlike
  /// [getDestinationSuggestions].
  Future<List<Map<String, dynamic>>> getDestinations();

  /// Cover image URL per destination name, from `GET /api/destinations/
  /// assets` — a separate static asset manifest, not part of the
  /// destination row itself. Keyed by exact destination name.
  Future<Map<String, String>> getDestinationImages();

  /// Creates the trip on the backend and returns its id.
  Future<String> createTrip(TripCreationData data);

  /// Starts AI generation for an already-created trip.
  Future<void> startGeneration(String tripId);
}
