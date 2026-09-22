import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/presentation/widgets/error_state.dart';
import 'package:triply_project/presentation/widgets/primary_button.dart';

import 'test_helpers.dart';

void main() {
  group('ErrorState', () {
    testWidgets('renders the title and description', (tester) async {
      await pumpApp(
        tester,
        const ErrorState(
          title: 'Something went wrong',
          description: 'We could not load your trips. Please try again.',
        ),
      );

      expect(find.text('Something went wrong'), findsOneWidget);
      expect(find.text('We could not load your trips. Please try again.'), findsOneWidget);
    });

    testWidgets('renders the error icon', (tester) async {
      await pumpApp(
        tester,
        const ErrorState(
          title: 'Something went wrong',
          description: 'We could not load your trips. Please try again.',
        ),
      );

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
    });

    testWidgets('defaults the action label to "Retry"', (tester) async {
      await pumpApp(
        tester,
        ErrorState(
          title: 'Something went wrong',
          description: 'We could not load your trips. Please try again.',
          onAction: () {},
        ),
      );

      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('uses a custom action label when provided', (tester) async {
      await pumpApp(
        tester,
        ErrorState(
          title: 'Something went wrong',
          description: 'We could not load your trips. Please try again.',
          actionLabel: 'Try again',
          onAction: () {},
        ),
      );

      expect(find.text('Try again'), findsOneWidget);
      expect(find.text('Retry'), findsNothing);
    });

    testWidgets('invokes onAction when the retry button is tapped', (tester) async {
      var tapCount = 0;
      await pumpApp(
        tester,
        ErrorState(
          title: 'Something went wrong',
          description: 'We could not load your trips. Please try again.',
          onAction: () => tapCount++,
        ),
      );

      await tester.tap(find.byType(PrimaryButton));
      await tester.pump();

      expect(tapCount, 1);
    });

    testWidgets('hides the action button when onAction is not provided', (tester) async {
      await pumpApp(
        tester,
        const ErrorState(
          title: 'Something went wrong',
          description: 'We could not load your trips. Please try again.',
        ),
      );

      expect(find.byType(PrimaryButton), findsNothing);
    });
  });
}
