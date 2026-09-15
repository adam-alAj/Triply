import '../models/home_trip.dart';
import 'home_repository.dart';

class MockHomeRepository implements HomeRepository {
  @override
  Future<List<HomeTrip>> getRecentTrips() async {
    await Future.delayed(const Duration(milliseconds: 400));

    return const [
      HomeTrip(
        id: 'mock-trip-1',
        destination: 'Kyoto & Tokyo',
        dateRange: 'Oct 14 – 22',
        travelersLabel: '2 Travelers',
        estimatedCost: '\$2,450 Est.',
        tripTitle: 'Kyoto & Tokyo Cultural Discovery',
        nextActivity: 'Gion Lantern Walking Tour',
        nextActivityTime: 'Next up • 7:30 JST',
        dayLabel: 'Day 3 of 9',
        totalDays: 9,
        imageAsset: 'assets/images/home_active_trip.png',
      ),
    ];
  }
}