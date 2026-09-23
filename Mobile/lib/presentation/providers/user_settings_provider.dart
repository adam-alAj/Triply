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

  Future<List<CurrencyOption>> getCurrencies() => _repository.getCurrencies();

  Future<void> setDistanceUnit(bool useKm) async {
    final unit = useKm ? 'KM' : 'MILES';
    if (_preferences == null || _preferences!.distanceUnit == unit) return;
    await _save(_preferences!.copyWith(distanceUnit: unit));
  }

  Future<void> setPacing(String pacing) async {
    if (_preferences == null || _preferences!.pacing == pacing) return;
    await _save(_preferences!.copyWith(pacing: pacing));
  }

  Future<void> setCurrency(CurrencyOption currency) async {
    if (_preferences == null ||
        _preferences!.preferredCurrencyId == currency.id) {
      return;
    }
    await _save(_preferences!.copyWith(
      preferredCurrencyId: currency.id,
      preferredCurrency: currency.isoCode,
    ));
  }

  /// Optimistic update: shows [next] immediately, rolls back on failure.
  Future<void> _save(UserPreferencesData next) async {
    final previous = _preferences;
    _preferences = next;
    _errorMessage = null;
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
