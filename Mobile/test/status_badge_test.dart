import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/core/theme/app_colors.dart';
import 'package:triply_project/presentation/widgets/status_badge.dart';

import 'test_helpers.dart';

void main() {
  group('StatusBadge', () {
    testWidgets('renders the label text', (tester) async {
      await pumpApp(
        tester,
        const StatusBadge(label: 'Saved', variant: StatusBadgeVariant.success),
      );

      expect(find.text('Saved'), findsOneWidget);
    });

    testWidgets('renders the provided icon', (tester) async {
      await pumpApp(
        tester,
        const StatusBadge(
          label: 'User Modified',
          variant: StatusBadgeVariant.userModified,
          icon: Icons.edit,
        ),
      );

      expect(find.byIcon(Icons.edit), findsOneWidget);
    });

    testWidgets('renders no icon when not provided', (tester) async {
      await pumpApp(
        tester,
        const StatusBadge(label: 'Saved', variant: StatusBadgeVariant.success),
      );

      expect(find.byType(Icon), findsNothing);
    });

    Color? containerBg(WidgetTester tester) {
      final container = tester.widget<Container>(find.byType(Container));
      final decoration = container.decoration as BoxDecoration;
      return decoration.color;
    }

    testWidgets('uses the success background color for the success variant', (tester) async {
      await pumpApp(
        tester,
        const StatusBadge(label: 'Saved', variant: StatusBadgeVariant.success),
      );

      expect(containerBg(tester), AppColors.successBg);
    });

    testWidgets('uses the warning background color for the warning variant', (tester) async {
      await pumpApp(
        tester,
        const StatusBadge(label: 'Pending', variant: StatusBadgeVariant.warning),
      );

      expect(containerBg(tester), AppColors.warningBg);
    });

    testWidgets('uses the warning background color for the userModified variant', (tester) async {
      await pumpApp(
        tester,
        const StatusBadge(label: 'User Modified', variant: StatusBadgeVariant.userModified),
      );

      expect(containerBg(tester), AppColors.warningBg);
    });

    testWidgets('uses the error background color for the error variant', (tester) async {
      await pumpApp(
        tester,
        const StatusBadge(label: 'Failed', variant: StatusBadgeVariant.error),
      );

      expect(containerBg(tester), AppColors.errorBg);
    });

    testWidgets('uses the neutral surface color for the neutral variant', (tester) async {
      await pumpApp(
        tester,
        const StatusBadge(label: 'Draft', variant: StatusBadgeVariant.neutral),
      );

      expect(containerBg(tester), AppColors.surfaceContainerHigh);
    });
  });
}
