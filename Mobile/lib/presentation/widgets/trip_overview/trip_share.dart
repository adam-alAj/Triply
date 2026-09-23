import 'package:flutter/widgets.dart';
import 'package:share_plus/share_plus.dart';

import '../../../data/models/trip_overview_data.dart';

/// Share / Invite / Export actions on the Trip Overview header and Costs tab.
/// There's no public trip link or collaborator backend yet, so all three
/// hand a plain-text summary to the OS share sheet (WhatsApp, email, notes…).

Future<void> shareTripSummary(BuildContext context, TripOverviewData trip) {
  return _share(context, subject: trip.tripTitle, text: buildTripSummaryText(trip));
}

Future<void> shareTripInvite(BuildContext context, TripOverviewData trip) {
  final text = StringBuffer()
    ..writeln("I'm planning \"${trip.tripTitle}\" on Triply and would love you to join!")
    ..writeln()
    ..writeln('📍 ${trip.regionLabel}')
    ..writeln('📅 ${trip.dateRangeLabel} (${trip.totalDays} days)')
    ..writeln('💰 Est. ${_money(trip.totalEstimatedCostUsd)} total')
    ..writeln()
    ..write('Download Triply to plan trips with me.');
  return _share(context, subject: 'Join my trip: ${trip.tripTitle}', text: text.toString());
}

Future<void> shareCostBreakdown(BuildContext context, TripOverviewData trip) {
  return _share(
    context,
    subject: '${trip.tripTitle} — cost breakdown',
    text: buildCostBreakdownText(trip),
  );
}

String buildTripSummaryText(TripOverviewData trip) {
  final text = StringBuffer()
    ..writeln(trip.tripTitle)
    ..writeln('${trip.regionLabel} • ${trip.dateRangeLabel}')
    ..writeln('${trip.totalDays} days • ${trip.travelerCount} traveler(s)')
    ..writeln('Est. total: ${_money(trip.totalEstimatedCostUsd)}');

  for (final day in trip.days) {
    text
      ..writeln()
      ..writeln('Day ${day.dayNumber}${day.dateLabel.isEmpty ? '' : ' — ${day.dateLabel}'}');
    for (final item in day.items) {
      text.writeln('• ${_slot(item.timeSlot)}: ${item.placeName} (${item.estimatedCostLabel})');
    }
  }

  text
    ..writeln()
    ..write('Planned with Triply. Costs are estimates.');
  return text.toString();
}

String buildCostBreakdownText(TripOverviewData trip) {
  final text = StringBuffer()
    ..writeln('${trip.tripTitle} — Cost Breakdown')
    ..writeln('${trip.regionLabel} • ${trip.dateRangeLabel}')
    ..writeln();

  for (final category in trip.costCategories) {
    text.writeln('${category.label}: ${_money(category.amountUsd)} (${category.percentOfTotal}%)');
  }

  text
    ..writeln()
    ..writeln('Total: ${_money(trip.totalEstimatedCostUsd)}')
    ..writeln('Budget: ${_money(trip.budgetHealth.targetCapUsd)}')
    ..writeln('Per traveler / day: ${_money(trip.avgPerDayPerTravelerUsd)}')
    ..writeln()
    ..write('All amounts are estimates in USD.');
  return text.toString();
}

Future<void> _share(
  BuildContext context, {
  required String subject,
  required String text,
}) async {
  // iPad needs an anchor rect for the share popover.
  final box = context.findRenderObject() as RenderBox?;
  await SharePlus.instance.share(
    ShareParams(
      text: text,
      subject: subject,
      sharePositionOrigin:
          box == null ? null : box.localToGlobal(Offset.zero) & box.size,
    ),
  );
}

String _money(double usd) => '\$${usd.round()}';

String _slot(String timeSlot) => switch (timeSlot) {
      'MORNING' => 'Morning',
      'AFTERNOON' => 'Afternoon',
      'EVENING' => 'Evening',
      _ => timeSlot,
    };
