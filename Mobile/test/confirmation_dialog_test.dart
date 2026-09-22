import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/core/theme/app_colors.dart';
import 'package:triply_project/presentation/widgets/confirmation_dialog.dart';

void main() {
  group('showAppConfirmationDialog', () {
    Future<bool>? result;

    Widget buildTrigger({
      String confirmLabel = 'Delete Trip',
      bool isDestructive = true,
      String cancelLabel = 'Cancel',
    }) {
      return MaterialApp(
        home: Builder(
          builder: (context) {
            return Scaffold(
              body: Center(
                child: ElevatedButton(
                  onPressed: () {
                    result = showAppConfirmationDialog(
                      context: context,
                      title: 'Delete this trip?',
                      message: 'This action cannot be undone.',
                      confirmLabel: confirmLabel,
                      isDestructive: isDestructive,
                      cancelLabel: cancelLabel,
                    );
                  },
                  child: const Text('Open dialog'),
                ),
              ),
            );
          },
        ),
      );
    }

    setUp(() {
      result = null;
    });

    testWidgets('shows the title and message', (tester) async {
      await tester.pumpWidget(buildTrigger());

      await tester.tap(find.text('Open dialog'));
      await tester.pumpAndSettle();

      expect(find.text('Delete this trip?'), findsOneWidget);
      expect(find.text('This action cannot be undone.'), findsOneWidget);
    });

    testWidgets('uses the given confirm and cancel labels', (tester) async {
      await tester.pumpWidget(buildTrigger(confirmLabel: 'Archive Trip', cancelLabel: 'Not now'));

      await tester.tap(find.text('Open dialog'));
      await tester.pumpAndSettle();

      expect(find.text('Archive Trip'), findsOneWidget);
      expect(find.text('Not now'), findsOneWidget);
    });

    testWidgets('resolves to true and dismisses when the confirm action is tapped', (tester) async {
      await tester.pumpWidget(buildTrigger(confirmLabel: 'Delete Trip'));

      await tester.tap(find.text('Open dialog'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Delete Trip'));
      await tester.pumpAndSettle();

      expect(await result, isTrue);
      expect(find.text('Delete this trip?'), findsNothing);
    });

    testWidgets('resolves to false and dismisses when cancel is tapped', (tester) async {
      await tester.pumpWidget(buildTrigger());

      await tester.tap(find.text('Open dialog'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();

      expect(await result, isFalse);
      expect(find.text('Delete this trip?'), findsNothing);
    });

    testWidgets('colors the confirm label with the error color when isDestructive is true', (tester) async {
      await tester.pumpWidget(buildTrigger(isDestructive: true));

      await tester.tap(find.text('Open dialog'));
      await tester.pumpAndSettle();

      final confirmText = tester.widget<Text>(find.text('Delete Trip'));
      expect(confirmText.style?.color, AppColors.error);
    });

    testWidgets('colors the confirm label with the primary color when isDestructive is false', (tester) async {
      await tester.pumpWidget(buildTrigger(isDestructive: false));

      await tester.tap(find.text('Open dialog'));
      await tester.pumpAndSettle();

      final confirmText = tester.widget<Text>(find.text('Delete Trip'));
      expect(confirmText.style?.color, AppColors.primary);
    });
  });
}
