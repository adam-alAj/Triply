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
