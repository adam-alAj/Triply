import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/presentation/widgets/generation_wait_notice.dart';
import 'package:triply_project/presentation/widgets/secondary_button.dart';

import 'test_helpers.dart';

void main() {
  group('GenerationWaitNotice', () {
    Widget build({required Duration elapsed, VoidCallback? onCancel}) {
      return GenerationWaitNotice(
        spinner: const SizedBox(width: 72, height: 72),
        message: 'Matching activities to your interests…',
        elapsed: elapsed,
        onCancel: onCancel ?? () {},
      );
    }

    testWidgets('renders the title, status message and cancel action',
        (tester) async {
      await pumpApp(tester, build(elapsed: const Duration(seconds: 5)));

      expect(find.text('Creating your trip'), findsOneWidget);
      expect(
        find.text('Matching activities to your interests…'),
        findsOneWidget,
      );
      expect(find.byType(SecondaryButton), findsOneWidget);
      expect(find.text('Cancel'), findsOneWidget);
    });

    testWidgets('does not reassure before the 30s threshold', (tester) async {
      await pumpApp(tester, build(elapsed: const Duration(seconds: 29)));

      expect(find.textContaining('longer than usual'), findsNothing);
    });

    testWidgets('escalates at the 30s threshold but stays cancelable',
        (tester) async {
      await pumpApp(
        tester,
        build(elapsed: GenerationWaitNotice.longWaitThreshold),
      );

      expect(find.textContaining('longer than usual'), findsOneWidget);
      expect(find.textContaining('Working for 30 seconds'), findsOneWidget);
      expect(find.text('Cancel'), findsOneWidget);
    });

    testWidgets('reports elapsed time in minutes past 60s', (tester) async {
      await pumpApp(tester, build(elapsed: const Duration(seconds: 95)));

      expect(find.textContaining('Working for 1 min 35 sec'), findsOneWidget);
    });
  });
}
