import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Figma source: Trip Overview - Itinerary (node 1:2).
/// Examples inspected: "SAVED" (success), "User Modified" (warning).
/// One widget, one enum — never duplicate per status per
/// 08_SYSTEM_DESIGN.md §41 and this task's Acceptance Criteria.
enum StatusBadgeVariant { success, warning, error, neutral, userModified }

class StatusBadge extends StatelessWidget {
  const StatusBadge({super.key, required this.label, required this.variant, this.icon});

  final String label;
  final StatusBadgeVariant variant;
  final IconData? icon;

  ({Color bg, Color fg}) _colorsFor(StatusBadgeVariant v) {
    switch (v) {
      case StatusBadgeVariant.success:
        return (bg: AppColors.successBg, fg: AppColors.success);
      case StatusBadgeVariant.warning:
      case StatusBadgeVariant.userModified:
        return (bg: AppColors.warningBg, fg: AppColors.warning);
      case StatusBadgeVariant.error:
        return (bg: AppColors.errorBg, fg: AppColors.error);
      case StatusBadgeVariant.neutral:
        return (bg: AppColors.surfaceContainerHigh, fg: AppColors.secondary);
    }
  }

  @override
  Widget build(BuildContext context) {
    final colors = _colorsFor(variant);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      decoration: BoxDecoration(color: colors.bg, borderRadius: BorderRadius.circular(9999)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 12, color: colors.fg),
            const SizedBox(width: 4),
          ],
          Text(
            label,
            style: AppTextStyles.labelSm.copyWith(
              color: colors.fg,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}
