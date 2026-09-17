import 'package:flutter/foundation.dart';

import '../../data/models/home_trip.dart';
import '../../data/repositories/home_repository.dart';

enum HomeStatus {
  idle,
  loading,
  success,
  failure,
}

class HomeProvider extends ChangeNotifier {
  HomeProvider({
    required HomeRepository repository,
  }) : _repository = repository;

  final HomeRepository _repository;

  HomeStatus _status = HomeStatus.idle;
  List<HomeTrip> _recentTrips = const [];
  String? _errorMessage;

  HomeStatus get status => _status;
  List<HomeTrip> get recentTrips => _recentTrips;
  String? get errorMessage => _errorMessage;

  bool get hasRecentTrip => _recentTrips.isNotEmpty;

  Future<void> loadHome() async {
    if (_status == HomeStatus.loading) return;

    _status = HomeStatus.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _recentTrips = await _repository.getRecentTrips();
      _status = HomeStatus.success;
    } catch (_) {
      _errorMessage = 'We could not load your trips. Please try again.';
      _status = HomeStatus.failure;
    }

    notifyListeners();
  }
}