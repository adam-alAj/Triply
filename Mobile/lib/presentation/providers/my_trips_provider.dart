import 'package:flutter/foundation.dart';

import '../../core/network/api_client.dart';
import '../../data/models/trip_summary.dart';
import '../../data/repositories/my_trips_repository.dart';

enum MyTripsStatus { loading, success, failure }

enum MyTripsFilter { active, archived }

class MyTripsProvider extends ChangeNotifier {
  MyTripsProvider({required MyTripsRepository repository})
      : _repository = repository;

  final MyTripsRepository _repository;

  MyTripsStatus status = MyTripsStatus.loading;
  String? errorMessage;
  MyTripsFilter filter = MyTripsFilter.active;

  List<TripSummary> _trips = [];
  Map<String, String> _destinationImages = {};

  List<TripSummary> get visibleTrips => _trips
      .where((trip) => filter == MyTripsFilter.archived
          ? trip.isArchived
          : !trip.isArchived)
      .toList();

  int get activeCount => _trips.where((trip) => !trip.isArchived).length;

  int get archivedCount => _trips.where((trip) => trip.isArchived).length;

  /// Fallback cover image for a trip with no `coverImageUrl` of its own,
  /// looked up by destination name.
  String? imageUrlFor(String destinationName) =>
      _destinationImages[destinationName];

  Future<void> loadTrips() async {
    status = MyTripsStatus.loading;
    notifyListeners();

    try {
      _trips = await _repository.getMyTrips();
      status = MyTripsStatus.success;
    } catch (error) {
      // Never show a raw exception (08_SYSTEM_DESIGN.md §36).
      errorMessage = error is ApiException
          ? error.message
          : 'Unable to load your trips. Please try again.';
      status = MyTripsStatus.failure;
    }

    notifyListeners();

    // Best-effort: cosmetic fallback images never block the trip list itself.
    try {
      _destinationImages = await _repository.getDestinationImages();
      notifyListeners();
    } catch (_) {
      // Leave whatever images (if any) were already loaded.
    }
  }

  void setFilter(MyTripsFilter value) {
    if (filter == value) return;
    filter = value;
    notifyListeners();
  }
}
