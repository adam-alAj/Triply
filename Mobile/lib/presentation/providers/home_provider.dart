import 'package:flutter/foundation.dart';

import '../../data/models/home_region.dart';
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
  List<HomeRegion> _regions = const [];
  String? _errorMessage;

  HomeStatus get status => _status;
  List<HomeTrip> get recentTrips => _recentTrips;
  List<HomeRegion> get regions => _regions;
  String? get errorMessage => _errorMessage;

  bool get hasRecentTrip => _recentTrips.isNotEmpty;

  Future<void> loadHome() async {
    if (_status == HomeStatus.loading) return;

    _status = HomeStatus.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      final results = await Future.wait([
        _repository.getRecentTrips(),
        _repository.getFeaturedRegions(),
      ]);
      _recentTrips = results[0] as List<HomeTrip>;
      _regions = results[1] as List<HomeRegion>;
      _status = HomeStatus.success;
    } catch (_) {
      _errorMessage = 'We could not load your trips. Please try again.';
      _status = HomeStatus.failure;
    }

    notifyListeners();
  }
}