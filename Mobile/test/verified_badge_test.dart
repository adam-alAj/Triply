import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/presentation/widgets/verified_badge.dart';

import 'test_helpers.dart';

void main() {
  group('VerifiedBadge', () {
    testWidgets('renders the default label with the verified icon',
        (tester) async {
      await pumpApp(tester, const VerifiedBadge());

      expect(find.text('Verified'), findsOneWidget);
      expect(find.byIcon(Icons.verified_rounded), findsOneWidget);
    });

    testWidgets('renders a custom label when provided', (tester) async {
      await pumpApp(tester, const VerifiedBadge(label: 'Checked'));

      expect(find.text('Checked'), findsOneWidget);
      expect(find.text('Verified'), findsNothing);
    });
  });
}
