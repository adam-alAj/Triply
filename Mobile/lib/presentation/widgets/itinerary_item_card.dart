import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';
import 'ai_generated_badge.dart';
import 'status_badge.dart';

/// Figma source: Trip Overview - Itinerary (node 1:2), "Article - Activity".
/// Hierarchy per 08_SYSTEM_DESIGN.md §22:
/// Time -> Place/activity -> Supporting info -> Estimated cost -> AI badge.
/// No domain model dependency (no ItineraryItem entity) — pure display
/// widget taking primitives, so it stays reusable across screens/tests.
class ItineraryItemCard extends StatelessWidget {
  const ItineraryItemCard({
    super.key,
    required this.time,
    required this.title,
    required this.subtitle,
    required this.estimatedCostLabel,
    this.tipText,
    this.imageUrl,
    this.isAiGenerated = true,
    this.isUserModified = false,
    this.onEdit,
    this.onRegenerate,
    this.onTap,
  });

  final String time; // e.g. "09:00 AM"
  final String title;
  final String subtitle;
  final String estimatedCostLabel; // e.g. "Est. $15" — always pre-labeled Estimated
  final String? tipText;
  final String? imageUrl;
  final bool isAiGenerated;
  final bool isUserModified;
  final VoidCallback? onEdit;
  final VoidCallback? onRegenerate;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.surfaceContainerLowest,
          borderRadius: BorderRadius.circular(32),
          boxShadow: const [
            BoxShadow(color: AppColors.shadowAmbient, blurRadius: 1, offset: Offset(0, 1)),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: AppColors.surfaceContainer,
                        borderRadius: BorderRadius.circular(9999),
                      ),
                      child: Text(time,
                          style: AppTextStyles.bodySm.copyWith(color: AppColors.primary, fontWeight: FontWeight.bold)),
                    ),
                    const SizedBox(width: 4),
                    if (isUserModified)
                      const StatusBadge(label: 'User Modified', variant: StatusBadgeVariant.userModified, icon: Icons.edit)
                    else if (isAiGenerated)
                      const AIGeneratedBadge(),
                  ],
                ),
                Text(estimatedCostLabel, style: AppTextStyles.labelMd),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: imageUrl != null
                      ? Image.network(imageUrl!, width: 80, height: 80, fit: BoxFit.cover)
                      : Container(width: 80, height: 80, color: AppColors.surfaceContainer),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(title, style: AppTextStyles.headlineSm, maxLines: 1, overflow: TextOverflow.ellipsis),
                      Text(subtitle, style: AppTextStyles.bodySm),
                      if (tipText != null) ...[
                        const SizedBox(height: 2),
                        Text(tipText!,
                            style: AppTextStyles.labelSm.copyWith(color: AppColors.primary)),
                      ],
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Tap for place details', style: AppTextStyles.labelSm),
                Row(
                  children: [
                    if (onEdit != null) _iconButton(Icons.edit_outlined, onEdit!),
                    if (onRegenerate != null) ...[
                      const SizedBox(width: 4),
                      _iconButton(Icons.autorenew, onRegenerate!),
                    ],
                  ],
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _iconButton(IconData icon, VoidCallback onTap) {
    return InkWell(
      onTap: onTap,
      customBorder: const CircleBorder(),
      child: Container(
        width: 32,
        height: 32,
        decoration: const BoxDecoration(color: AppColors.surfaceContainer, shape: BoxShape.circle),
        child: Icon(icon, size: 14, color: AppColors.secondary),
      ),
    );
  }
}
