import 'package:flutter/foundation.dart';

import '../../core/network/api_client.dart';
import '../../data/models/user_settings_data.dart';
import '../../data/repositories/user_settings_repository.dart';

enum UserSettingsStatus { loading, success, failure }

class UserSettingsProvider extends ChangeNotifier {
  UserSettingsProvider({required UserSettingsRepository repository})
      : _repository = repository {
    _load();
  }

  final UserSettingsRepository _repository;

  UserSettingsStatus _status = UserSettingsStatus.loading;
  UserPreferencesData? _preferences;
  UserStatsData? _stats;
  String? _errorMessage;

  UserSettingsStatus get status => _status;
  UserPreferencesData? get preferences => _preferences;
  UserStatsData? get stats => _stats;
  String? get errorMessage => _errorMessage;

  Future<void> _load() async {
    _status = UserSettingsStatus.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      final results = await Future.wait([
        _repository.getPreferences(),
        _repository.getStats(),
      ]);
      _preferences = results[0] as UserPreferencesData;
      _stats = results[1] as UserStatsData;
      _status = UserSettingsStatus.success;
    } catch (error) {
      _status = UserSettingsStatus.failure;
      _errorMessage = error is ApiException
          ? error.message
          : 'Unable to load your preferences.';
    }

    notifyListeners();
  }

  Future<void> reload() => _load();

  Future<void> setDistanceUnit(bool useKm) async {
    final current = _preferences;
    if (current == null) return;

    final unit = useKm ? 'KM' : 'MILES';
    if (current.distanceUnit == unit) return;

    final previous = current;
    _preferences = current.copyWith(distanceUnit: unit);
    notifyListeners();

    try {
      _preferences = await _repository.updatePreferences(_preferences!);
    } catch (error) {
      _preferences = previous;
      _errorMessage = error is ApiException
          ? error.message
          : 'Unable to save that change.';
    }
    notifyListeners();
  }
}
