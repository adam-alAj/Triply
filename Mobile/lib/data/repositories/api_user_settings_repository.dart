import '../../core/network/api_client.dart';
import '../models/user_settings_data.dart';
import 'user_settings_repository.dart';

/// Real backend-backed implementation, calling `GET/PUT /api/users/me/
/// preferences` and `GET /api/users/me/stats` (see Backend/Triply.Api/
/// Modules/User/UsersController.cs).
class ApiUserSettingsRepository implements UserSettingsRepository {
  ApiUserSettingsRepository({required ApiClient apiClient})
      : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<UserPreferencesData> getPreferences() async {
    final json = await _apiClient
        .get<Map<String, dynamic>>('/api/users/me/preferences');
    return UserPreferencesData.fromJson(json);
  }

  @override
  Future<UserPreferencesData> updatePreferences(
    UserPreferencesData preferences,
  ) async {
    final json = await _apiClient.put<Map<String, dynamic>>(
      '/api/users/me/preferences',
      data: {
        'preferredCurrencyId': preferences.preferredCurrencyId,
        'distanceUnit': preferences.distanceUnit,
        'pacing': preferences.pacing,
      },
    );
    return UserPreferencesData.fromJson(json);
  }

  @override
  Future<UserStatsData> getStats() async {
    final json =
        await _apiClient.get<Map<String, dynamic>>('/api/users/me/stats');
    return UserStatsData.fromJson(json);
  }
}
