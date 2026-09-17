import '../models/trip_creation_data.dart';
import 'trip_creation_repository.dart';

class MockTripCreationRepository implements TripCreationRepository {
  @override
  Future<List<Map<String, dynamic>>> getDestinationSuggestions(
      TripCreationData data,
      ) async {
    await Future.delayed(const Duration(milliseconds: 500));

    // -------------------------------------------------------------------------
    // BUDGET-FIRST
    // User does not know the destination yet.
    // We suggest destinations based on the selected budget.
    // -------------------------------------------------------------------------
    if (data.planningMode == PlanningMode.budgetFirst) {
      return [
        {
          'id': 'oaxaca',
          'name': 'Oaxaca de Juárez',
          'country': 'Mexico',
          'image': 'assets/images/trip_creation/oaxaca.jpg',
          'description': 'Cultural & Gastronomy',
          'estimatedCost': '\$1,850 avg.',
        },
        {
          'id': 'sintra_cascais',
          'name': 'Sintra & Cascais',
          'country': 'Portugal',
          'image': 'assets/images/trip_creation/sintra_cascais.jpg',
          'description': 'Coastal Heritage',
          'estimatedCost': '\$2,320 avg.',
        },
      ];
    }

    // -------------------------------------------------------------------------
    // DESTINATION-FIRST
    // User already selected a destination.
    // We suggest experiences / regions within that destination.
    // -------------------------------------------------------------------------

    if (data.destination == 'Japan') {
      return [
        {
          'id': 'kyoto_tokyo',
          'name': 'Kyoto & Tokyo',
          'country': 'Japan',
          'image': 'assets/images/trip_creation/kyoto_tokyo.jpg',
          'description': 'Culture, Food & Tradition',
          'estimatedCost': '\$2,500 avg.',
        },
      ];
    }

    if (data.destination == 'Italy') {
      return [
        {
          'id': 'amalfi_rome',
          'name': 'Amalfi Coast & Rome',
          'country': 'Italy',
          'image': 'assets/images/trip_creation/amalfi_rome.jpg',
          'description': 'Coastal Views & History',
          'estimatedCost': '\$2,600 avg.',
        },
      ];
    }

    if (data.destination == 'Turkey') {
      return [
        {
          'id': 'turkey_cultural',
          'name': 'Istanbul & Cappadocia',
          'country': 'Turkey',
          'image': 'assets/images/trip_creation/kyoto_tokyo.jpg',
          'description': 'History, Food & Landscapes',
          'estimatedCost': '\$1,900 avg.',
        },
      ];
    }

    return [];
  }

  @override
  Future<String> createTrip(TripCreationData data) async {
    await Future.delayed(const Duration(milliseconds: 500));
    return 'mock-trip-id';
  }

  @override
  Future<void> startGeneration(String tripId) async {
    await Future.delayed(const Duration(milliseconds: 300));
  }
}