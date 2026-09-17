import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

class DaySelectorItem {
  const DaySelectorItem({required this.label, required this.dayIndex});
  final String label; // e.g. "Day 2 • Oct 15"
  final int dayIndex;
}

/// Figma source: Trip Overview - Itinerary (node 1:2), "Day Horizontal
/// Selector". Inactive = white pill, active = bg #49607c white text.
/// Stateless — selection is controlled by the parent (no Provider here),
/// per shared-widget reusability rule.
class DaySelector extends StatelessWidget {
  const DaySelector({
    super.key,
    required this.days,
    required this.selectedIndex,
    required this.onDaySelected,
  });

  final List<DaySelectorItem> days;
  final int selectedIndex;
  final ValueChanged<int> onDaySelected;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 38,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: days.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final isActive = index == selectedIndex;
          return GestureDetector(
            onTap: () => onDaySelected(days[index].dayIndex),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              decoration: BoxDecoration(
                color: isActive ? AppColors.secondary : AppColors.surfaceContainerLowest,
                borderRadius: BorderRadius.circular(9999),
                boxShadow: const [
                  BoxShadow(color: AppColors.shadowAmbient, blurRadius: 1, offset: Offset(0, 1)),
                ],
              ),
              alignment: Alignment.center,
              child: Text(
                days[index].label,
                style: AppTextStyles.labelMd.copyWith(
                  color: isActive ? AppColors.onPrimary : AppColors.textMuted,
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
