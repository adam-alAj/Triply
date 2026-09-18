import '../models/trip_summary.dart';
import 'my_trips_repository.dart';

/// Fallback used only when no backend is reachable (see [MyTripsRepository]).
class MockTripsRepository implements MyTripsRepository {
  @override
  Future<List<TripSummary>> getMyTrips() async {
    await Future.delayed(const Duration(milliseconds: 600));

    final now = DateTime.now();

    return [
      TripSummary(
        id: 'mock-1',
        status: 'SAVED',
        destinationName: 'Kyoto & Tokyo Discovery',
        startDate: DateTime(now.year, 10, 14),
        endDate: DateTime(now.year, 10, 21),
        travelerCount: 2,
        budgetAmount: 2450,
        budgetCurrencyId: 3,
      ),
      TripSummary(
        id: 'mock-2',
        status: 'GENERATED',
        destinationName: 'Amalfi Coast & Rome',
        startDate: DateTime(now.year, 5, 10),
        endDate: DateTime(now.year, 5, 15),
        travelerCount: 2,
        budgetAmount: 2600,
        budgetCurrencyId: 3,
      ),
      TripSummary(
        id: 'mock-3',
        status: 'ARCHIVED',
        destinationName: 'Oaxaca Food Trail',
        startDate: DateTime(now.year - 1, 3, 2),
        endDate: DateTime(now.year - 1, 3, 9),
        travelerCount: 1,
        budgetAmount: 1400,
        budgetCurrencyId: 3,
      ),
    ];
  }
}
