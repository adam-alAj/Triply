import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/presentation/widgets/empty_state.dart';
import 'package:triply_project/presentation/widgets/primary_button.dart';
import 'package:triply_project/presentation/widgets/secondary_button.dart';

import 'test_helpers.dart';

void main() {
  group('EmptyState', () {
    testWidgets('renders the title and description', (tester) async {
      await pumpApp(
        tester,
        const EmptyState(
          title: 'No saved trips yet',
          description: 'Plan a trip to see it here.',
        ),
      );

      expect(find.text('No saved trips yet'), findsOneWidget);
      expect(find.text('Plan a trip to see it here.'), findsOneWidget);
    });

    testWidgets('renders the provided icon', (tester) async {
      await pumpApp(
        tester,
        const EmptyState(
          title: 'No saved trips yet',
          description: 'Plan a trip to see it here.',
          icon: Icons.luggage_outlined,
        ),
      );

      expect(find.byIcon(Icons.luggage_outlined), findsOneWidget);
    });

    testWidgets('renders no icon when not provided', (tester) async {
      await pumpApp(
        tester,
        const EmptyState(
          title: 'No saved trips yet',
          description: 'Plan a trip to see it here.',
        ),
      );

      expect(find.byType(Icon), findsNothing);
    });

    testWidgets('shows an action button when both actionLabel and onAction are given', (tester) async {
      var tapCount = 0;
      await pumpApp(
        tester,
        EmptyState(
          title: 'No saved trips yet',
          description: 'Plan a trip to see it here.',
          actionLabel: 'Plan Your First Trip',
          onAction: () => tapCount++,
        ),
      );

      expect(find.byType(PrimaryButton), findsOneWidget);
      expect(find.text('Plan Your First Trip'), findsOneWidget);

      await tester.tap(find.text('Plan Your First Trip'));
      await tester.pump();
      expect(tapCount, 1);
    });

    testWidgets('hides the action button when actionLabel is missing', (tester) async {
      await pumpApp(
        tester,
        const EmptyState(
          title: 'No saved trips yet',
          description: 'Plan a trip to see it here.',
          onAction: null,
        ),
      );

      expect(find.byType(PrimaryButton), findsNothing);
    });

    testWidgets('hides the action button when onAction is missing', (tester) async {
      await pumpApp(
        tester,
        const EmptyState(
          title: 'No saved trips yet',
          description: 'Plan a trip to see it here.',
          actionLabel: 'Plan Your First Trip',
        ),
      );

      expect(find.byType(PrimaryButton), findsNothing);
    });

    testWidgets('shows a secondary action when both label and callback are given',
        (tester) async {
      var tapCount = 0;
      await pumpApp(
        tester,
        EmptyState(
          title: 'No destinations match your budget',
          description: 'Try a higher budget.',
          actionLabel: 'Adjust budget',
          onAction: () {},
          secondaryActionLabel: 'Change interests',
          onSecondaryAction: () => tapCount++,
        ),
      );

      expect(find.byType(SecondaryButton), findsOneWidget);
      expect(find.text('Change interests'), findsOneWidget);

      await tester.tap(find.text('Change interests'));
      await tester.pump();
      expect(tapCount, 1);
    });

    testWidgets('hides the secondary action when only the label is given',
        (tester) async {
      await pumpApp(
        tester,
        const EmptyState(
          title: 'No destinations match your budget',
          description: 'Try a higher budget.',
          secondaryActionLabel: 'Change interests',
        ),
      );

      expect(find.byType(SecondaryButton), findsNothing);
    });
  });
}
