/// Mirrors the backend's `UserPreferencesResponse`
/// (Backend/Triply.Api/Modules/User/Dtos/UserDtos.cs).
class UserPreferencesData {
  const UserPreferencesData({
    required this.preferredCurrencyId,
    required this.preferredCurrency,
    required this.distanceUnit,
    required this.pacing,
  });

  factory UserPreferencesData.fromJson(Map<String, dynamic> json) {
    return UserPreferencesData(
      preferredCurrencyId: (json['preferredCurrencyId'] as num?)?.toInt(),
      preferredCurrency: json['preferredCurrency'] as String?,
      distanceUnit: json['distanceUnit'] as String? ?? 'KM',
      pacing: json['pacing'] as String? ?? 'BALANCED',
    );
  }

  final int? preferredCurrencyId;
  final String? preferredCurrency; // ISO code, e.g. "USD"
  final String distanceUnit; // KM | MILES
  final String pacing; // RELAXED | BALANCED | FAST

  UserPreferencesData copyWith({
    int? preferredCurrencyId,
    String? preferredCurrency,
    String? distanceUnit,
    String? pacing,
  }) {
    return UserPreferencesData(
      preferredCurrencyId: preferredCurrencyId ?? this.preferredCurrencyId,
      preferredCurrency: preferredCurrency ?? this.preferredCurrency,
      distanceUnit: distanceUnit ?? this.distanceUnit,
      pacing: pacing ?? this.pacing,
    );
  }

  String get pacingLabel => switch (pacing) {
        'RELAXED' => 'Relaxed (2-3)',
        'FAST' => 'Fast-Paced (6+)',
        _ => 'Balanced (4-5)',
      };
}

/// Mirrors the backend's `CurrencyResponse` (`GET /api/currencies`).
class CurrencyOption {
  const CurrencyOption({
    required this.id,
    required this.isoCode,
    required this.symbol,
  });

  factory CurrencyOption.fromJson(Map<String, dynamic> json) {
    return CurrencyOption(
      id: (json['id'] as num).toInt(),
      isoCode: json['isoCode'] as String? ?? '',
      symbol: json['symbol'] as String? ?? '',
    );
  }

  final int id;
  final String isoCode;
  final String symbol;
}

/// Mirrors the backend's `UserStatsResponse`.
class UserStatsData {
  const UserStatsData({
    required this.totalTrips,
    required this.totalSavedPlaces,
    required this.totalCountries,
  });

  factory UserStatsData.fromJson(Map<String, dynamic> json) {
    return UserStatsData(
      totalTrips: json['totalTrips'] as int? ?? 0,
      totalSavedPlaces: json['totalSavedPlaces'] as int? ?? 0,
      totalCountries: json['totalCountries'] as int? ?? 0,
    );
  }

  final int totalTrips;
  final int totalSavedPlaces;
  final int totalCountries;
}
