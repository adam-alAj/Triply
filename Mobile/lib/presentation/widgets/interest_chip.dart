import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// ASSUMPTION FLAGGED: not yet cross-checked against the Figma "Interests"
/// screen (node 1:4057) — built from 09_DESIGN.md "Chips & Badges" /
/// "Selection Chips" section. Re-verify against Figma before final sign-off.
/// Selection state must not rely on color alone (accessibility rule,
/// 08_SYSTEM_DESIGN.md §37) — hence the check icon when selected.
class InterestChip extends StatelessWidget {
  const InterestChip({
    super.key,
    required this.label,
    required this.selected,
    required this.onTap,
    this.icon,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
        decoration: BoxDecoration(
          color: selected ? AppColors.primary : AppColors.surfaceContainerLowest,
          borderRadius: BorderRadius.circular(9999),
          border: selected ? null : Border.all(color: AppColors.borderSubtle),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              Icon(icon, size: 16, color: selected ? AppColors.onPrimary : AppColors.textMuted),
              const SizedBox(width: 6),
            ],
            Text(label,
                style: AppTextStyles.labelMd.copyWith(
                    color: selected ? AppColors.onPrimary : AppColors.textMuted)),
            if (selected) ...[
              const SizedBox(width: 6),
              const Icon(Icons.check, size: 14, color: AppColors.onPrimary),
            ],
          ],
        ),
      ),
    );
  }
}
