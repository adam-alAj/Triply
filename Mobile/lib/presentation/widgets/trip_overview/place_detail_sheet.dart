import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/models/trip_overview_data.dart';

enum PlaceDetailAction { edit, remove }

/// Place/Item Detail Sheet (UI Pages §6): "place description, reference
/// price, notes, 'Edit' and 'Remove' actions". Returns which action the
/// user picked (if any) so the caller can open the Edit Item Modal or
/// remove the item — this sheet itself doesn't touch trip state.
///
/// PENDING BACKEND: there is no place-details endpoint yet (only
/// `PlaceName` comes through the itinerary response) — description/hours
/// below are placeholders until one exists.
Future<PlaceDetailAction?> showPlaceDetailSheet(
  BuildContext context,
  ItineraryItemData item,
) {
  return showModalBottomSheet<PlaceDetailAction>(
    context: context,
    isScrollControlled: true,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    builder: (context) => _PlaceDetailSheet(item: item),
  );
}

class _PlaceDetailSheet extends StatelessWidget {
  const _PlaceDetailSheet({required this.item});

  final ItineraryItemData item;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 36,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.surfaceContainerHigh,
                  borderRadius: BorderRadius.circular(999),
                ),
              ),
            ),
            const SizedBox(height: 16),
            ClipRRect(
              borderRadius: BorderRadius.circular(18),
              child: Container(
                width: double.infinity,
                height: 140,
                color: AppColors.surfaceContainer,
                alignment: Alignment.center,
                child: const Icon(
                  Icons.image_outlined,
                  size: 32,
                  color: AppColors.textMuted,
                ),
              ),
            ),
            const SizedBox(height: 14),
            Text(item.placeName, style: AppTextStyles.headlineSm),
            const SizedBox(height: 2),
            Text(item.subtitle, style: AppTextStyles.bodySm),
            const SizedBox(height: 14),
            Text(
              // Placeholder copy — real place descriptions need a backend
              // Place-details endpoint that doesn't exist yet.
              'A curated stop matched to your interests. More details '
              '(opening hours, reviews, booking links) will appear here '
              'once the backend exposes full place data.',
              style: AppTextStyles.bodyMd,
            ),
            if (item.notes != null && item.notes!.isNotEmpty) ...[
              const SizedBox(height: 10),
              Text('Your notes', style: AppTextStyles.labelMd),
              const SizedBox(height: 2),
              Text(item.notes!, style: AppTextStyles.bodySm),
            ],
            const SizedBox(height: 14),
            Row(
              children: [
                const Icon(Icons.schedule, size: 16, color: AppColors.textMuted),
                const SizedBox(width: 6),
                Text(item.estimatedCostLabel, style: AppTextStyles.labelMd),
              ],
            ),
            const SizedBox(height: 18),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () =>
                        Navigator.of(context).pop(PlaceDetailAction.remove),
                    icon: const Icon(Icons.delete_outline, color: AppColors.error),
                    label: const Text('Remove', style: TextStyle(color: AppColors.error)),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: () =>
                        Navigator.of(context).pop(PlaceDetailAction.edit),
                    icon: const Icon(Icons.edit_outlined),
                    label: const Text('Edit'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
