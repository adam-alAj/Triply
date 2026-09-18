import 'package:flutter/foundation.dart';

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

  List<TripSummary> get visibleTrips => _trips
      .where((trip) => filter == MyTripsFilter.archived
          ? trip.isArchived
          : !trip.isArchived)
      .toList();

  int get activeCount => _trips.where((trip) => !trip.isArchived).length;

  int get archivedCount => _trips.where((trip) => trip.isArchived).length;

  Future<void> loadTrips() async {
    status = MyTripsStatus.loading;
    notifyListeners();

    try {
      _trips = await _repository.getMyTrips();
      status = MyTripsStatus.success;
    } catch (error) {
      errorMessage = error.toString();
      status = MyTripsStatus.failure;
    }

    notifyListeners();
  }

  void setFilter(MyTripsFilter value) {
    if (filter == value) return;
    filter = value;
    notifyListeners();
  }
}
