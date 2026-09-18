import '../models/trip_summary.dart';

abstract class MyTripsRepository {
  Future<List<TripSummary>> getMyTrips();
}
