import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Cost provenance marker: every monetary figure Triply shows is an
/// *estimate*, never verified pricing, so it must never look like a
/// guaranteed price (08_SYSTEM_DESIGN.md Principle 05, §44 Risk 03).
///
/// This is deliberately distinct from [AIGeneratedBadge], which marks
/// *content* provenance (the itinerary was authored by AI). Estimated data
/// uses the warm coral treatment; AI-generated content uses the cool
/// blue-grey treatment — a reader can tell "this number is a guess" apart
/// from "this text was AI-written" at a glance (07_UI_PAGES.md §13, SRS §8).
/// One implementation, reused everywhere money is shown as an estimate.
class EstimatedBadge extends StatelessWidget {
  const EstimatedBadge({super.key, this.label = 'Estimated'});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.primaryContainerLight,
        borderRadius: BorderRadius.circular(9999),
      ),
      child: Text(
        label,
        style: AppTextStyles.labelSm.copyWith(
          color: AppColors.onPrimaryContainerLight,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
