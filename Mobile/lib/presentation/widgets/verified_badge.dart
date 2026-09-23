import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Data provenance marker: this value was checked against verified,
/// bookable reference data rather than generated or guessed
/// (08_SYSTEM_DESIGN.md Principle 05 — verified facts must read differently
/// from estimates and AI output).
///
/// Deliberately uses the semantic success treatment + a check-shield icon so
/// it is never confused with [EstimatedBadge] (warm coral) or
/// [AIGeneratedBadge] (cool blue-grey). One implementation, reused everywhere
/// a value is confirmed against the dataset.
class VerifiedBadge extends StatelessWidget {
  const VerifiedBadge({super.key, this.label = 'Verified'});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.successBg,
        borderRadius: BorderRadius.circular(9999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.verified_rounded, size: 12, color: AppColors.success),
          const SizedBox(width: 4),
          Text(
            label,
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.success,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}
