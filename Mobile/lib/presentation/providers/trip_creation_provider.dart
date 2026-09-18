import 'package:flutter/foundation.dart';

import '../../core/network/api_client.dart';
import '../../data/models/trip_creation_data.dart';
import '../../data/repositories/trip_creation_repository.dart';

enum TripCreationStatus {
  idle,
  loading,
  success,
  failure,
}

class TripCreationProvider extends ChangeNotifier {
  TripCreationProvider({
    required TripCreationRepository repository,
  }) : _repository = repository;

  final TripCreationRepository _repository;

  final TripCreationData _data = TripCreationData();

  int _currentStep = 0;

  TripCreationStatus _status = TripCreationStatus.idle;

  List<Map<String, dynamic>> _suggestions = [];

  List<Map<String, dynamic>> _destinations = [];
  bool _destinationsLoading = false;

  String? _errorMessage;

  String? _createdTripId;

  TripCreationData get data => _data;

  int get currentStep => _currentStep;

  TripCreationStatus get status => _status;

  List<Map<String, dynamic>> get suggestions => _suggestions;

  List<Map<String, dynamic>> get destinations => _destinations;

  bool get destinationsLoading => _destinationsLoading;

  String? get errorMessage => _errorMessage;

  String? get createdTripId => _createdTripId;

  bool get isFirstStep => _currentStep == 0;

  bool get isLastStep => _currentStep == 5;

  bool get _isBudgetFirst => _data.planningMode == PlanningMode.budgetFirst;

  /// Screen order differs by planning mode: budget-first must collect
  /// interests *before* requesting suggestions, because the backend's
  /// `POST /api/destinations/suggestions` rejects an empty
  /// `interestCategoryIds` list (see DestinationSuggestionRequestValidator).
  /// Destination-first doesn't have this constraint, so it keeps the
  /// original order. Both paths still have 6 steps (0-5) — only steps 2-4
  /// swap position.
  ///
  /// destination-first: Mode, Destination, Suggestions, Details, Interests, Review
  /// budget-first:      Mode, Budget,      Interests,   Suggestions, Details, Review
  int get interestsStepIndex => _isBudgetFirst ? 2 : 4;

  int get suggestionsStepIndex => _isBudgetFirst ? 3 : 2;

  int get tripDetailsStepIndex => _isBudgetFirst ? 4 : 3;

  void selectPlanningMode(PlanningMode mode) {
    _data.planningMode = mode;

    // Clear values that depend on the previous planning path.
    _data.destination = null;
    _data.destinationCountry = null;
    _data.destinationId = null;
    _data.budget = null;

    notifyListeners();

    if (mode == PlanningMode.destinationFirst && _destinations.isEmpty) {
      loadDestinations();
    }
  }

  Future<void> loadDestinations() async {
    _destinationsLoading = true;
    notifyListeners();

    try {
      _destinations = await _repository.getDestinations();
    } catch (_) {
      _destinations = [];
    }

    _destinationsLoading = false;
    notifyListeners();
  }

  void selectDestination({
    required String name,
    required String country,
    int? destinationId,
  }) {
    _data.destination = name;
    _data.destinationCountry = country;
    _data.destinationId = destinationId;

    notifyListeners();
  }

  void setBudget(double value) {
    _data.budget = value;
    notifyListeners();
  }

  void setDates({
    required DateTime startDate,
    required DateTime endDate,
  }) {
    _data.startDate = startDate;
    _data.endDate = endDate;

    notifyListeners();
  }

  void setTravelers(int value) {
    if (value < 1) return;

    _data.travelers = value;
    notifyListeners();
  }

  void toggleInterest(String interest) {
    if (_data.interests.contains(interest)) {
      _data.interests.remove(interest);
    } else {
      _data.interests.add(interest);
    }

    notifyListeners();
  }

  void setPacing(String value) {
    _data.pacing = value;
    notifyListeners();
  }

  Future<void> next() async {
    if (_currentStep >= 5) return;

    // Fire the suggestions request right as the user leaves the step just
    // before the suggestions screen (Budget for destination-first is
    // already picked directly, so it never routes through here for that
    // mode's real data — see suggestionsStepIndex).
    if (_currentStep == suggestionsStepIndex - 1) {
      await loadSuggestions();
    }

    _currentStep++;
    notifyListeners();
  }

  void back() {
    if (_currentStep <= 0) return;

    _currentStep--;
    notifyListeners();
  }

  void jumpToStep(int step) {
    if (step < 0 || step > 6) return;

    _currentStep = step;
    notifyListeners();
  }

  /// Step 6 — the Generating screen. Reached only from Review's explicit
  /// "Generate My Trip" action, never via next()/back().
  void goToGenerating() => jumpToStep(6);

  /// Step 1 (Budget/Destination) is at the same index in both modes.
  void jumpToDestinationStep() => jumpToStep(1);

  void jumpToTripDetailsStep() => jumpToStep(tripDetailsStepIndex);

  void jumpToInterestsStep() => jumpToStep(interestsStepIndex);

  Future<void> loadSuggestions() async {
    _status = TripCreationStatus.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _suggestions = await _repository.getDestinationSuggestions(_data);
      _status = TripCreationStatus.success;
    } catch (error) {
      _status = TripCreationStatus.failure;
      _errorMessage = error is ApiException
          ? error.message
          : 'Unable to load destination suggestions.';
    }

    notifyListeners();
  }

  /// Creates the trip and kicks off AI generation. Returns whether it
  /// succeeded; check [errorMessage] on failure and [createdTripId] on
  /// success.
  Future<bool> submitTrip() async {
    // Destination-first still runs on local placeholder destinations
    // (no real Destination.Id) until GET /api/destinations exists — see
    // budget_destination_screen.dart. Fail clearly instead of silently
    // creating a trip with no real destination attached.
    if (_data.planningMode == PlanningMode.destinationFirst &&
        _data.destinationId == null) {
      _status = TripCreationStatus.failure;
      _errorMessage =
          'This destination isn\'t connected to the backend yet — pick a '
          'budget-first suggestion instead, or wait for the destinations API.';
      notifyListeners();
      return false;
    }

    _status = TripCreationStatus.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      final tripId = await _repository.createTrip(_data);
      await _repository.startGeneration(tripId);

      _createdTripId = tripId;
      _status = TripCreationStatus.success;
      notifyListeners();
      return true;
    } catch (error) {
      _errorMessage =
          error is ApiException ? error.message : 'Unable to create your trip.';
      _status = TripCreationStatus.failure;
      notifyListeners();
      return false;
    }
  }

  void reset() {
    _data.clear();
    _currentStep = 0;
    _status = TripCreationStatus.idle;
    _suggestions = [];
    _errorMessage = null;
    _createdTripId = null;

    notifyListeners();
  }
}