import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/core/theme/app_colors.dart';
import 'package:triply_project/presentation/widgets/over_budget_notice.dart';

import 'test_helpers.dart';

void main() {
  group('OverBudgetNotice', () {
    testWidgets('renders the default heading and explanation', (tester) async {
      await pumpApp(tester, const OverBudgetNotice());

      expect(find.text('Over your budget'), findsOneWidget);
      expect(
        find.textContaining('above the budget you set'),
        findsOneWidget,
      );
    });

    testWidgets('renders a custom detail message when provided', (tester) async {
      await pumpApp(
        tester,
        const OverBudgetNotice(message: 'Estimated 320 JOD of 250 JOD.'),
      );

      expect(find.text('Estimated 320 JOD of 250 JOD.'), findsOneWidget);
      expect(find.textContaining('above the budget you set'), findsNothing);
      // The heading is always present — only the detail line is overridable.
      expect(find.text('Over your budget'), findsOneWidget);
    });

    testWidgets('announces itself as a live region for screen readers',
        (tester) async {
      await pumpApp(tester, const OverBudgetNotice());

      final semantics = tester
          .getSemantics(find.byType(OverBudgetNotice))
          .getSemanticsData();
      expect(semantics.flagsCollection.isLiveRegion, isTrue);
    });

    testWidgets('uses the warning treatment, not the error treatment',
        (tester) async {
      await pumpApp(tester, const OverBudgetNotice());

      final container = tester.widget<Container>(
        find.descendant(
          of: find.byType(OverBudgetNotice),
          matching: find.byType(Container),
        ),
      );
      final decoration = container.decoration! as BoxDecoration;

      expect(decoration.color, AppColors.warningBg);
      expect((decoration.border! as Border).top.color, AppColors.warning);
    });
  });
}
