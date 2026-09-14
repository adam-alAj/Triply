import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Figma source: Trip Overview - Itinerary (node 1:2), "AI-generated" pill.
/// Per 08_SYSTEM_DESIGN.md Principle 04: AI should feel helpful, not
/// magical — subtle badge only, no fake confidence scores/animations.
class AIGeneratedBadge extends StatelessWidget {
  const AIGeneratedBadge({super.key, this.label = 'AI-generated'});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.tertiaryFixed,
        borderRadius: BorderRadius.circular(9999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.auto_awesome, size: 12, color: AppColors.secondary),
          const SizedBox(width: 4),
          Text(label, style: AppTextStyles.labelSm.copyWith(color: AppColors.secondary)),
        ],
      ),
    );
  }
}
