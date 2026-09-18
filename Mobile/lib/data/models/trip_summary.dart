/// One row of `GET /api/trips` (see Backend/Triply.Api/Modules/Trip/Dtos/TripDtos.cs
/// `TripResponse`). Only the fields the My Trips list needs are kept here —
/// full itinerary/cost data is fetched separately by Trip Overview.
class TripSummary {
  const TripSummary({
    required this.id,
    required this.status,
    required this.destinationName,
    required this.startDate,
    required this.endDate,
    required this.travelerCount,
    required this.budgetAmount,
    required this.budgetCurrencyId,
  });

  factory TripSummary.fromJson(Map<String, dynamic> json) {
    return TripSummary(
      id: json['id'] as String,
      status: json['status'] as String,
      destinationName: json['destinationName'] as String?,
      startDate: _parseDate(json['startDate']),
      endDate: _parseDate(json['endDate']),
      travelerCount: json['travelerCount'] as int? ?? 1,
      budgetAmount: (json['budgetAmount'] as num?)?.toDouble(),
      budgetCurrencyId: json['budgetCurrencyId'] as int?,
    );
  }

  static DateTime? _parseDate(dynamic value) {
    if (value is! String || value.isEmpty) return null;
    return DateTime.tryParse(value);
  }

  final String id;
  final String status;
  final String? destinationName;
  final DateTime? startDate;
  final DateTime? endDate;
  final int travelerCount;
  final double? budgetAmount;
  final int? budgetCurrencyId;

  /// The backend has no separate trip-title field yet — the destination
  /// name doubles as the title until one is added.
  String get title => destinationName ?? 'Untitled Trip';

  bool get isArchived => status == 'ARCHIVED';

  static const _currencySymbols = {1: '€', 2: 'د.أ', 3: '\$'};

  String? get formattedBudget {
    if (budgetAmount == null) return null;
    final symbol = _currencySymbols[budgetCurrencyId] ?? '\$';
    return '$symbol${budgetAmount!.round()}';
  }

  String? get formattedDateRange {
    if (startDate == null || endDate == null) return null;
    const months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
    ];
    final startMonth = months[startDate!.month - 1];
    final days = endDate!.difference(startDate!).inDays + 1;
    return '$days Days ($startMonth ${startDate!.day} – ${endDate!.day})';
  }
}
