import '../models/trip_creation_data.dart';

abstract class TripCreationRepository {
  Future<List<Map<String, dynamic>>> getDestinationSuggestions(
    TripCreationData data,
  );

  /// Creates the trip on the backend and returns its id.
  Future<String> createTrip(TripCreationData data);

  /// Starts AI generation for an already-created trip.
  Future<void> startGeneration(String tripId);
}
