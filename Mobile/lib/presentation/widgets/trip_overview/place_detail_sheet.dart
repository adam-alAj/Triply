import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/models/trip_overview_data.dart';

enum PlaceDetailAction { edit, remove }

/// Place/Item Detail Sheet (UI Pages §6): "place description, reference
/// price, notes, 'Edit' and 'Remove' actions". Returns which action the
/// user picked (if any) so the caller can open the Edit Item Modal or
/// remove the item — this sheet itself doesn't touch trip state.
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

class _PlaceDetailSheet extends StatefulWidget {
  const _PlaceDetailSheet({required this.item});

  final ItineraryItemData item;

  @override
  State<_PlaceDetailSheet> createState() => _PlaceDetailSheetState();
}

class _PlaceDetailSheetState extends State<_PlaceDetailSheet> {
  late Future<Map<String, dynamic>> _details;

  @override
  void initState() {
    super.initState();
    _details = _fetchDetails();
  }

  Future<Map<String, dynamic>> _fetchDetails() {
    if (widget.item.placeId <= 0) {
      return Future.error(
        StateError('This item has no linked place yet.'),
      );
    }
    return context
        .read<ApiClient>()
        .get<Map<String, dynamic>>('/api/places/${widget.item.placeId}');
  }

  @override
  Widget build(BuildContext context) {
    final item = widget.item;

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
            FutureBuilder<Map<String, dynamic>>(
              future: _details,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: Center(
                      child: SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      ),
                    ),
                  );
                }

                if (snapshot.hasError) {
                  return Text(
                    'More details for this place aren\'t available right now.',
                    style: AppTextStyles.bodyMd,
                  );
                }

                final data = snapshot.data!;
                final description = data['description'] as String?;
                final countryName = data['countryName'] as String?;
                final destinationName = data['destinationName'] as String?;

                return Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      (description == null || description.isEmpty)
                          ? 'A curated stop matched to your interests.'
                          : description,
                      style: AppTextStyles.bodyMd,
                    ),
                    if (destinationName != null) ...[
                      const SizedBox(height: 8),
                      Row(
                        children: [
                          const Icon(Icons.place_outlined,
                              size: 16, color: AppColors.textMuted),
                          const SizedBox(width: 6),
                          Text(
                            countryName != null
                                ? '$destinationName, $countryName'
                                : destinationName,
                            style: AppTextStyles.labelMd,
                          ),
                        ],
                      ),
                    ],
                  ],
                );
              },
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
