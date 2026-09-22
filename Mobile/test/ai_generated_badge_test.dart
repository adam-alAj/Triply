import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/presentation/widgets/ai_generated_badge.dart';

import 'test_helpers.dart';

void main() {
  group('AIGeneratedBadge', () {
    testWidgets('renders the default label', (tester) async {
      await pumpApp(tester, const AIGeneratedBadge());

      expect(find.text('AI-generated'), findsOneWidget);
    });

    testWidgets('renders a custom label when provided', (tester) async {
      await pumpApp(tester, const AIGeneratedBadge(label: 'AI suggestion'));

      expect(find.text('AI suggestion'), findsOneWidget);
      expect(find.text('AI-generated'), findsNothing);
    });

    testWidgets('renders the sparkle icon', (tester) async {
      await pumpApp(tester, const AIGeneratedBadge());

      expect(find.byIcon(Icons.auto_awesome), findsOneWidget);
    });
  });
}
