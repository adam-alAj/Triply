import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/core/theme/app_colors.dart';
import 'package:triply_project/presentation/widgets/budget_status_chip.dart';

import 'test_helpers.dart';

void main() {
  group('BudgetStatusChip', () {
    testWidgets('shows the on-target state', (tester) async {
      await pumpApp(tester, const BudgetStatusChip(isOnTarget: true));

      expect(find.text('On Target'), findsOneWidget);
      expect(find.byIcon(Icons.check_circle_rounded), findsOneWidget);
      expect(find.text('Over budget'), findsNothing);
    });

    // Regression: the over-budget state previously rendered nothing at all, so an
    // over-budget trip was indistinguishable from one with no budget shown.
    testWidgets('shows an over-budget state instead of rendering nothing',
        (tester) async {
      await pumpApp(tester, const BudgetStatusChip(isOnTarget: false));

      expect(find.text('Over budget'), findsOneWidget);
      expect(find.byIcon(Icons.trending_up_rounded), findsOneWidget);
      expect(find.text('On Target'), findsNothing);
    });

    testWidgets('uses the warning treatment when over budget', (tester) async {
      await pumpApp(tester, const BudgetStatusChip(isOnTarget: false));

      final label = tester.widget<Text>(find.text('Over budget'));
      expect(label.style?.color, AppColors.warning);
    });

    testWidgets('uses the success treatment when on target', (tester) async {
      await pumpApp(tester, const BudgetStatusChip(isOnTarget: true));

      final label = tester.widget<Text>(find.text('On Target'));
      expect(label.style?.color, AppColors.success);
    });

    testWidgets('announces its state to screen readers', (tester) async {
      await pumpApp(tester, const BudgetStatusChip(isOnTarget: false));

      expect(
        tester
            .getSemantics(find.byType(BudgetStatusChip))
            .getSemanticsData()
            .label,
        contains('over budget'),
      );
    });
  });
}
