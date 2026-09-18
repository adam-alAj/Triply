import '../models/home_region.dart';
import '../models/home_trip.dart';

abstract class HomeRepository {
  Future<List<HomeTrip>> getRecentTrips();

  Future<List<HomeRegion>> getFeaturedRegions();
}