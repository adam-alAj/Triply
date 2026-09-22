import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/core/theme/app_colors.dart';
import 'package:triply_project/presentation/widgets/day_selector.dart';

import 'test_helpers.dart';

void main() {
  const days = [
    DaySelectorItem(label: 'Day 1 • Oct 14', dayIndex: 0),
    DaySelectorItem(label: 'Day 2 • Oct 15', dayIndex: 1),
    DaySelectorItem(label: 'Day 3 • Oct 16', dayIndex: 2),
  ];

  group('DaySelector', () {
    testWidgets('renders a label for every day', (tester) async {
      await pumpApp(
        tester,
        DaySelector(days: days, selectedIndex: 0, onDaySelected: (_) {}),
      );

      expect(find.text('Day 1 • Oct 14'), findsOneWidget);
      expect(find.text('Day 2 • Oct 15'), findsOneWidget);
      expect(find.text('Day 3 • Oct 16'), findsOneWidget);
    });

    testWidgets('calls onDaySelected with the tapped day\'s dayIndex', (tester) async {
      int? selected;
      await pumpApp(
        tester,
        DaySelector(days: days, selectedIndex: 0, onDaySelected: (index) => selected = index),
      );

      await tester.tap(find.text('Day 3 • Oct 16'));
      await tester.pump();

      expect(selected, 2);
    });

    testWidgets('highlights the currently selected day with the active background', (tester) async {
      await pumpApp(
        tester,
        DaySelector(days: days, selectedIndex: 1, onDaySelected: (_) {}),
      );

      final activeContainer = tester.widget<Container>(
        find.ancestor(of: find.text('Day 2 • Oct 15'), matching: find.byType(Container)).first,
      );
      final activeDecoration = activeContainer.decoration as BoxDecoration;
      expect(activeDecoration.color, AppColors.secondary);

      final inactiveContainer = tester.widget<Container>(
        find.ancestor(of: find.text('Day 1 • Oct 14'), matching: find.byType(Container)).first,
      );
      final inactiveDecoration = inactiveContainer.decoration as BoxDecoration;
      expect(inactiveDecoration.color, AppColors.surfaceContainerLowest);
    });

    testWidgets('renders no days when the list is empty', (tester) async {
      await pumpApp(
        tester,
        DaySelector(days: const [], selectedIndex: 0, onDaySelected: (_) {}),
      );

      expect(find.byType(GestureDetector), findsNothing);
    });

    testWidgets('scrolls horizontally', (tester) async {
      await pumpApp(
        tester,
        DaySelector(days: days, selectedIndex: 0, onDaySelected: (_) {}),
      );

      final listView = tester.widget<ListView>(find.byType(ListView));
      expect(listView.scrollDirection, Axis.horizontal);
    });
  });
}
