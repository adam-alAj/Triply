import '../models/home_trip.dart';

abstract class HomeRepository {
  Future<List<HomeTrip>> getRecentTrips();
}