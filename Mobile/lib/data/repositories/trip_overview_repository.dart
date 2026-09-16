import '../models/trip_overview_data.dart';

abstract class TripOverviewRepository {
  Future<TripOverviewData> getTrip(String tripId);
}
