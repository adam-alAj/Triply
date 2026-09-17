import 'interest_category_ref.dart';

enum PlanningMode {
  destinationFirst,
  budgetFirst,
}

class TripCreationData {
  PlanningMode? planningMode;

  String? destination;
  String? destinationCountry;

  /// Real backend Destination.Id, set once a suggestion backed by the
  /// destinations API is picked. Null for the destination-first path until
  /// the backend exposes GET /api/destinations (see budget_destination_screen.dart).
  int? destinationId;

  double? budget;

  /// Fixed USD reference (Currency.Id = 1, see ApplicationDbContext seed).
  /// No currency picker exists in the UI yet, so this is the only value used.
  static const int budgetCurrencyId = 1;

  DateTime? startDate;
  DateTime? endDate;

  int travelers = 2;

  final List<String> interests = [];

  /// Backend-facing ids for [interests], mapped via the fixed reference list.
  List<int> get interestCategoryIds => interests
      .map((title) => InterestCategoryRef.idsByTitle[title])
      .whereType<int>()
      .toList();

  String? pacing;

  TripCreationData();

  TripCreationData copy() {
    return TripCreationData()
      ..planningMode = planningMode
      ..destination = destination
      ..destinationCountry = destinationCountry
      ..destinationId = destinationId
      ..budget = budget
      ..startDate = startDate
      ..endDate = endDate
      ..travelers = travelers
      ..interests.addAll(interests)
      ..pacing = pacing;
  }

  void clear() {
    planningMode = null;
    destination = null;
    destinationCountry = null;
    destinationId = null;
    budget = null;
    startDate = null;
    endDate = null;
    travelers = 2;
    interests.clear();
    pacing = null;
  }
}
