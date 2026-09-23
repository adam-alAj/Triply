import 'package:flutter/foundation.dart';

import '../../core/network/api_client.dart';
import '../../data/models/trip_overview_data.dart';
import '../../data/repositories/trip_overview_repository.dart';

enum TripOverviewStatus { loading, success, failure }

class TripOverviewProvider extends ChangeNotifier {
  TripOverviewProvider({
    required TripOverviewRepository repository,
    required String tripId,
  })  : _repository = repository,
        _tripId = tripId {
    _load();
  }

  final TripOverviewRepository _repository;
  final String _tripId;

  TripOverviewStatus _status = TripOverviewStatus.loading;
  TripOverviewData? _trip;
  String? _errorMessage;
  int _selectedDayIndex = 0;
  bool _isRegenerating = false;

  TripOverviewStatus get status => _status;

  TripOverviewData? get trip => _trip;

  String? get errorMessage => _errorMessage;

  int get selectedDayIndex => _selectedDayIndex;

  bool get isRegenerating => _isRegenerating;

  void selectDay(int index) {
    if (_trip == null || index < 0 || index >= _trip!.days.length) return;

    _selectedDayIndex = index;
    notifyListeners();
  }

  Future<void> reload() => _load();

  /// Applies an edit to one itinerary item via `PATCH /api/trips/{tripId}
  /// /itinerary/items/{itemId}` (marks it user-modified server-side too).
  /// Returns whether it succeeded; check [errorMessage] on failure.
  ///
  /// The backend always recomputes `EstimatedCost` from the place's
  /// reference price (it doesn't accept a client-supplied cost), so the
  /// edited item's cost label is refreshed from the server response rather
  /// than from whatever the Edit Item Modal showed.
  Future<bool> updateItem(
    ApiClient apiClient,
    int dayIndex,
    int itemIndex,
    ItineraryItemData updated,
  ) async {
    final trip = _trip;
    if (trip == null) return false;

    try {
      final response = await apiClient.patch<Map<String, dynamic>>(
        '/api/trips/$_tripId/itinerary/items/${updated.id}',
        data: {
          'placeId': updated.placeId,
          'timeSlot': updated.timeSlot,
          'orderIndex': updated.orderIndex,
          'notes': updated.notes,
        },
      );

      final cost = (response['estimatedCost'] as num?)?.toDouble() ?? 0;
      final saved = updated.copyWith(
        isAiGenerated: response['isAiGenerated'] as bool? ?? false,
        estimatedCostLabel: 'Est. \$${cost.round()}',
        notes: response['notes'] as String?,
      );

      final day = trip.days[dayIndex];
      final items = [...day.items];
      items[itemIndex] = saved;

      final days = [...trip.days];
      days[dayIndex] = day.copyWith(items: items);

      final nextStatus = trip.status == 'ARCHIVED' ? trip.status : 'MODIFIED';
      _trip = trip.copyWith(days: days, status: nextStatus);
      notifyListeners();
      return true;
    } catch (error) {
      _errorMessage = error is ApiException
          ? error.message
          : 'Unable to save this change. Please try again.';
      notifyListeners();
      return false;
    }
  }

  /// Removes an item entirely — the Place Detail Sheet's "Remove" action.
  ///
  /// PENDING BACKEND: there is no delete-itinerary-item endpoint yet, so
  /// this stays in-memory only until one exists — flagged separately to the
  /// backend track. Same status-transition rule as [updateItem]: the trip
  /// moves to MODIFIED once a human has touched the AI-generated plan.
  void removeItem(int dayIndex, int itemIndex) {
    final trip = _trip;
    if (trip == null) return;

    final day = trip.days[dayIndex];
    final items = [...day.items]..removeAt(itemIndex);

    final days = [...trip.days];
    days[dayIndex] = day.copyWith(items: items);

    final nextStatus = trip.status == 'ARCHIVED' ? trip.status : 'MODIFIED';
    _trip = trip.copyWith(days: days, status: nextStatus);
    notifyListeners();
  }

  /// Real backend call — `POST /api/trips/{id}/save`. Only a SAVED trip can
  /// be archived (`TripLifecycle.CanTransition`: only `Saved → Archived` is
  /// allowed), so this is the required step before Archive can ever
  /// succeed. Returns whether it succeeded; check [errorMessage] on failure.
  Future<bool> saveTrip(ApiClient apiClient) async {
    try {
      await apiClient.post<Map<String, dynamic>>('/api/trips/$_tripId/save');

      if (_trip != null) {
        _trip = _trip!.copyWith(status: 'SAVED');
      }
      notifyListeners();
      return true;
    } catch (error) {
      _errorMessage = error is ApiException
          ? error.message
          : 'Unable to save this trip. Please try again.';
      notifyListeners();
      return false;
    }
  }

  /// Real backend call — `POST /api/trips/{id}/archive`. Returns whether it
  /// succeeded; check [errorMessage] on failure. The backend's 409 here is
  /// always `TripLifecycle`'s "cannot archive from status X" business rule
  /// (only a SAVED trip can be archived) — never an optimistic-concurrency
  /// conflict (Archive doesn't take an `ExpectedVersion`) — so its message
  /// is shown as-is rather than replaced with a generic one.
  Future<bool> archiveTrip(ApiClient apiClient) async {
    try {
      await apiClient.post<Map<String, dynamic>>('/api/trips/$_tripId/archive');

      if (_trip != null) {
        _trip = _trip!.copyWith(status: 'ARCHIVED');
      }
      notifyListeners();
      return true;
    } catch (error) {
      if (error is ApiException && error.statusCode == 409) {
        _errorMessage = error.message;
      } else {
        _errorMessage = error is ApiException
            ? error.message
            : 'Unable to archive this trip. Please try again.';
      }
      notifyListeners();
      return false;
    }
  }

  /// Real backend call — `PUT /api/trips/{id}`. That endpoint replaces the
  /// whole trip setup, so the current raw trip is fetched first and every
  /// other field is sent back unchanged. Returns whether it succeeded; check
  /// [errorMessage] on failure.
  Future<bool> updateBudget(ApiClient apiClient, double budgetAmount) async {
    try {
      final current =
          await apiClient.get<Map<String, dynamic>>('/api/trips/$_tripId');

      await apiClient.put<Map<String, dynamic>>(
        '/api/trips/$_tripId',
        data: {
          'destinationId': current['destinationId'],
          'startDate': current['startDate'],
          'endDate': current['endDate'],
          'travelerCount': current['travelerCount'],
          'budgetAmount': budgetAmount,
          'budgetCurrencyId': current['budgetCurrencyId'],
          'interestCategoryIds': current['interestCategoryIds'] ?? const [],
          'expectedVersion': current['version'],
        },
      );

      await _load();
      return true;
    } catch (error) {
      _errorMessage = error is ApiException
          ? error.message
          : 'Unable to update the budget. Please try again.';
      notifyListeners();
      return false;
    }
  }

  /// Partial regeneration — `POST /api/trips/{id}/generate` with
  /// `scope: DAY` (whole day re-planned) or `ITEM` (one item re-planned).
  /// Reloads the trip afterward rather than trying to splice the response's
  /// partial itinerary shape into local state. Returns whether it succeeded;
  /// check [errorMessage] on failure.
  Future<bool> regenerateDay(ApiClient apiClient, int dayNumber) =>
      _regenerate(apiClient, {'scope': 'DAY', 'dayNumber': dayNumber});

  Future<bool> regenerateItem(ApiClient apiClient, String itemId) =>
      _regenerate(apiClient, {'scope': 'ITEM', 'itemId': itemId});

  Future<bool> _regenerate(
    ApiClient apiClient,
    Map<String, dynamic> body,
  ) async {
    final trip = _trip;
    if (trip == null) return false;

    body = {...body, 'expectedVersion': trip.version};

    _isRegenerating = true;
    _errorMessage = null;
    notifyListeners();

    try {
      await apiClient.post<Map<String, dynamic>>(
        '/api/trips/$_tripId/generate',
        data: body,
        // Same bounded-retry AI call as full generation — needs the same
        // longer-than-default timeout (see ApiTripCreationRepository).
        receiveTimeout: const Duration(seconds: 120),
      );
      await _load();
      _isRegenerating = false;
      notifyListeners();
      return true;
    } catch (error) {
      _isRegenerating = false;
      _errorMessage = error is ApiException && error.statusCode == 409
          ? 'This trip changed elsewhere — reload before regenerating.'
          : error is ApiException
              ? error.message
              : 'Unable to regenerate right now. Please try again.';
      notifyListeners();
      return false;
    }
  }

  Future<void> _load() async {
    _status = TripOverviewStatus.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _trip = await _repository.getTrip(_tripId);
      _selectedDayIndex = 0;
      _status = TripOverviewStatus.success;
    } catch (error) {
      _status = TripOverviewStatus.failure;
      _errorMessage = error is ApiException
          ? error.message
          : 'Unable to load this trip.';
    }

    notifyListeners();
  }
}
