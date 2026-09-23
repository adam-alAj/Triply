import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';
import 'estimated_badge.dart';

/// Figma source: Trip Overview - Itinerary (node 1:2), "Total Cost
/// Banner Card". Per Design Principle 05 (08_SYSTEM_DESIGN.md): estimated
/// costs must NEVER visually resemble guaranteed prices — the
/// [EstimatedBadge] is mandatory and non-optional in this widget.
class CostCategoryRow extends StatelessWidget {
  const CostCategoryRow({
    super.key,
    required this.categoryLabel,
    required this.amountLabel,
    this.icon,
  });

  final String categoryLabel; // e.g. "Accommodation"
  final String amountLabel; // e.g. "$540"
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              if (icon != null) ...[
                Icon(icon, size: 16, color: AppColors.secondary),
                const SizedBox(width: 8),
              ],
              Text(categoryLabel.toUpperCase(),
                  style: AppTextStyles.labelSm.copyWith(letterSpacing: 0.5)),
            ],
          ),
          Row(
            children: [
              Text(amountLabel, style: AppTextStyles.headlineSm),
              const SizedBox(width: 8),
              const EstimatedBadge(),
            ],
          ),
        ],
      ),
    );
  }
}
