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

  TripOverviewStatus get status => _status;

  TripOverviewData? get trip => _trip;

  String? get errorMessage => _errorMessage;

  int get selectedDayIndex => _selectedDayIndex;

  void selectDay(int index) {
    if (_trip == null || index < 0 || index >= _trip!.days.length) return;

    _selectedDayIndex = index;
    notifyListeners();
  }

  Future<void> reload() => _load();

  /// Applies an edit to one itinerary item. Per the Edit Item Modal's spec:
  /// marks the item user-modified (`isAiGenerated = false`) and moves the
  /// trip out of a purely AI-generated state into MODIFIED.
  ///
  /// NOTE: Trip Overview is still on mock data end-to-end (see progress.md),
  /// so this only updates in-memory state — nothing is persisted to a
  /// backend yet. When it is, the real endpoint is `POST /api/trips/{id}
  /// /itinerary`, which re-writes the *whole* itinerary (no per-item PATCH
  /// exists), so this method's shape (whole day back out) already matches
  /// what that call will need.
  void updateItem(
    int dayIndex,
    int itemIndex,
    ItineraryItemData updated,
  ) {
    final trip = _trip;
    if (trip == null) return;

    final day = trip.days[dayIndex];
    final items = [...day.items];
    items[itemIndex] = updated.copyWith(isAiGenerated: false);

    final days = [...trip.days];
    days[dayIndex] = day.copyWith(items: items);

    final nextStatus = trip.status == 'ARCHIVED' ? trip.status : 'MODIFIED';
    _trip = trip.copyWith(days: days, status: nextStatus);
    notifyListeners();
  }

  /// Removes an item entirely — the Place Detail Sheet's "Remove" action.
  /// Same status-transition rule as [updateItem]: the trip moves to
  /// MODIFIED once a human has touched the AI-generated plan.
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

  /// Real backend call — `POST /api/trips/{id}/archive`. Returns whether it
  /// succeeded; check [errorMessage] on failure. A 409 means someone else
  /// (or another device) changed this trip since it was loaded here —
  /// surfaced as a distinct "stale version" message rather than a generic
  /// failure, per UI Pages §8's optimistic-concurrency special state.
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
        _errorMessage = 'This trip changed elsewhere — reload to see the '
            'latest version before archiving.';
      } else {
        _errorMessage = error is ApiException
            ? error.message
            : 'Unable to archive this trip. Please try again.';
      }
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
