import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// ASSUMPTION FLAGGED: not yet cross-checked against Figma "Planning
/// Mode" screen (node 1:487) — built from 09_DESIGN.md tokens +
/// 08_SYSTEM_DESIGN.md §15 (two large selection cards, e.g. Destination
/// First vs Budget First). Re-verify against Figma before final sign-off.
class SelectionCard extends StatelessWidget {
  const SelectionCard({
    super.key,
    required this.title,
    required this.description,
    required this.icon,
    required this.selected,
    required this.onTap,
  });

  final String title;
  final String description;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: selected ? AppColors.surfaceContainer : AppColors.surfaceContainerLowest,
          borderRadius: BorderRadius.circular(32),
          border: Border.all(color: selected ? AppColors.primary : AppColors.borderSubtle, width: selected ? 2 : 1),
          boxShadow: const [
            BoxShadow(color: AppColors.shadowAmbient, blurRadius: 4, offset: Offset(0, 2)),
          ],
        ),
        child: Row(
          children: [
            Container(
              width: 48,
              height: 48,
              decoration: BoxDecoration(
                color: selected ? AppColors.primary : AppColors.surfaceContainer,
                shape: BoxShape.circle,
              ),
              child: Icon(icon, color: selected ? AppColors.onPrimary : AppColors.secondary),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: AppTextStyles.headlineSm),
                  const SizedBox(height: 2),
                  Text(description, style: AppTextStyles.bodySm),
                ],
              ),
            ),
            if (selected) const Icon(Icons.check_circle, color: AppColors.primary),
          ],
        ),
      ),
    );
  }
}
