import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/presentation/widgets/estimated_badge.dart';

import 'test_helpers.dart';

void main() {
  group('EstimatedBadge', () {
    testWidgets('renders the default label', (tester) async {
      await pumpApp(tester, const EstimatedBadge());

      expect(find.text('Estimated'), findsOneWidget);
    });

    testWidgets('renders a custom label when provided', (tester) async {
      await pumpApp(tester, const EstimatedBadge(label: 'Estimated cost'));

      expect(find.text('Estimated cost'), findsOneWidget);
      expect(find.text('Estimated'), findsNothing);
    });

    testWidgets('is a text-only pill so it never reads like the AI marker',
        (tester) async {
      await pumpApp(tester, const EstimatedBadge());

      expect(find.byType(Icon), findsNothing);
    });
  });
}
