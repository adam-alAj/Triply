import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Compact budget-status chip for the Trip Overview cost summary.
///
/// Both states are rendered deliberately, not just the happy one: the previous
/// implementation showed an "On Target" chip and rendered *nothing* when the plan
/// exceeded the budget, so an over-budget trip looked identical to one with no
/// budget information at all.
///
/// The over-budget state reuses the warning treatment of [OverBudgetNotice] and the
/// same icon, so the signal is recognisable whether it appears at generation time or
/// on a later visit (where it is recomputed from the trip's budget vs. total
/// estimated cost).
class BudgetStatusChip extends StatelessWidget {
  const BudgetStatusChip({super.key, required this.isOnTarget});

  /// True when the estimated total is within the trip's budget cap.
  final bool isOnTarget;

  @override
  Widget build(BuildContext context) {
    final color = isOnTarget ? AppColors.success : AppColors.warning;
    final icon =
        isOnTarget ? Icons.check_circle_rounded : Icons.trending_up_rounded;
    final label = isOnTarget ? 'On Target' : 'Over budget';

    return Semantics(
      label: isOnTarget ? 'Within budget' : 'Estimated cost over budget',
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(width: 9),
          Icon(icon, size: 14, color: color),
          const SizedBox(width: 3),
          Text(
            label,
            style: AppTextStyles.labelSm.copyWith(
              color: color,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}
