import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';
import 'status_badge.dart';

/// ASSUMPTION FLAGGED: not yet cross-checked against Figma "My Trips"
/// screen (node 1:5240) — built from 08_SYSTEM_DESIGN.md §11 ("Trip Card
/// should communicate: Destination, Dates, Status, Estimated cost,
/// clear tap affordance"). Re-verify against Figma before final sign-off.
class TripCard extends StatelessWidget {
  const TripCard({
    super.key,
    required this.destinationName,
    required this.dateRangeLabel,
    required this.statusLabel,
    required this.statusVariant,
    this.estimatedCostLabel,
    this.imageUrl,
    this.onTap,
  });

  final String destinationName;
  final String dateRangeLabel;
  final String statusLabel;
  final StatusBadgeVariant statusVariant;
  final String? estimatedCostLabel;
  final String? imageUrl;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surfaceContainerLowest,
          borderRadius: BorderRadius.circular(32),
          boxShadow: const [
            BoxShadow(color: AppColors.shadowAmbient, blurRadius: 4, offset: Offset(0, 2)),
          ],
        ),
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(20),
              child: imageUrl != null
                  ? Image.network(imageUrl!, width: 64, height: 64, fit: BoxFit.cover)
                  : Container(width: 64, height: 64, color: AppColors.surfaceContainer),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(destinationName, style: AppTextStyles.headlineSm),
                  Text(dateRangeLabel, style: AppTextStyles.bodySm),
                  const SizedBox(height: 4),
                  Row(
                    children: [
                      StatusBadge(label: statusLabel, variant: statusVariant),
                      if (estimatedCostLabel != null) ...[
                        const SizedBox(width: 8),
                        Text(estimatedCostLabel!, style: AppTextStyles.labelMd),
                      ],
                    ],
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right, color: AppColors.textMuted),
          ],
        ),
      ),
    );
  }
}
