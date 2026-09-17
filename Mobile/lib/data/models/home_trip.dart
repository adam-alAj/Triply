class HomeTrip {
  const HomeTrip({
    required this.id,
    required this.destination,
    required this.dateRange,
    required this.travelersLabel,
    required this.estimatedCost,
    required this.tripTitle,
    required this.nextActivity,
    required this.nextActivityTime,
    required this.dayLabel,
    required this.totalDays,
    required this.imageAsset,
  });

  final String id;
  final String destination;
  final String dateRange;
  final String travelersLabel;
  final String estimatedCost;
  final String tripTitle;
  final String nextActivity;
  final String nextActivityTime;
  final String dayLabel;
  final int totalDays;
  final String imageAsset;
}