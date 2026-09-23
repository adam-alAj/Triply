import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/presentation/widgets/cost_category_row.dart';
import 'package:triply_project/presentation/widgets/estimated_badge.dart';

import 'test_helpers.dart';

void main() {
  group('CostCategoryRow', () {
    testWidgets('renders the category label in uppercase', (tester) async {
      await pumpApp(
        tester,
        const CostCategoryRow(categoryLabel: 'Accommodation', amountLabel: r'$540'),
      );

      expect(find.text('ACCOMMODATION'), findsOneWidget);
    });

    testWidgets('renders the amount label', (tester) async {
      await pumpApp(
        tester,
        const CostCategoryRow(categoryLabel: 'Accommodation', amountLabel: r'$540'),
      );

      expect(find.text(r'$540'), findsOneWidget);
    });

    testWidgets('always renders the mandatory "Estimated" pill', (tester) async {
      await pumpApp(
        tester,
        const CostCategoryRow(categoryLabel: 'Food', amountLabel: r'$120'),
      );

      expect(find.text('Estimated'), findsOneWidget);
    });

    testWidgets('renders the estimate marker via the shared EstimatedBadge',
        (tester) async {
      await pumpApp(
        tester,
        const CostCategoryRow(categoryLabel: 'Food', amountLabel: r'$120'),
      );

      expect(find.byType(EstimatedBadge), findsOneWidget);
    });

    testWidgets('renders the provided icon', (tester) async {
      await pumpApp(
        tester,
        const CostCategoryRow(
          categoryLabel: 'Transport',
          amountLabel: r'$80',
          icon: Icons.directions_car,
        ),
      );

      expect(find.byIcon(Icons.directions_car), findsOneWidget);
    });

    testWidgets('renders no icon when not provided', (tester) async {
      await pumpApp(
        tester,
        const CostCategoryRow(categoryLabel: 'Food', amountLabel: r'$120'),
      );

      expect(find.byType(Icon), findsNothing);
    });
  });
}
