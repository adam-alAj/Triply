import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Surfaces the Backend's `isOverBudget` flag (AI validation rules V-002 §5.3).
///
/// `DESTINATION_FIRST` generation succeeds even when the plan exceeds the
/// budget — the user picked the destination, so they receive the itinerary plus
/// this signal rather than an error. It is deliberately an advisory notice, not
/// an error state: nothing failed and the itinerary is complete and persisted.
class OverBudgetNotice extends StatelessWidget {
  const OverBudgetNotice({super.key, this.message});

  /// Optional detail line; a sensible default is shown when omitted.
  final String? message;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      liveRegion: true,
      container: true,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: AppColors.warningBg,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.warning),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Icon(Icons.trending_up_rounded,
                size: 18, color: AppColors.warning),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Over your budget',
                    style: AppTextStyles.labelMd
                        .copyWith(color: AppColors.onSurface),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    message ??
                        'The estimated cost of this itinerary is above the '
                            'budget you set. You can keep it or adjust the plan.',
                    style: AppTextStyles.bodySm
                        .copyWith(color: AppColors.onSurfaceVariant),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
