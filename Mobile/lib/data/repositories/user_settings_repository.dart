import '../models/user_settings_data.dart';

abstract class UserSettingsRepository {
  Future<UserPreferencesData> getPreferences();

  Future<UserPreferencesData> updatePreferences(UserPreferencesData preferences);

  Future<UserStatsData> getStats();

  Future<List<CurrencyOption>> getCurrencies();
}
