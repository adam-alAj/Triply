import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/models/trip_overview_data.dart';

enum PlaceDetailAction { edit, remove }

/// Place/Item Detail Sheet (UI Pages §6): hero image with overlay badges,
/// location/time pills, price, an "Optimal Crowd Cadence" panel where the
/// item has one, then description, notes, and Edit/Remove actions. Returns
/// which action the user picked (if any) so the caller can open the Edit
/// Item Modal or remove the item — this sheet itself doesn't touch trip
/// state.
Future<PlaceDetailAction?> showPlaceDetailSheet(
  BuildContext context,
  ItineraryItemData item,
) {
  return showModalBottomSheet<PlaceDetailAction>(
    context: context,
    isScrollControlled: true,
    backgroundColor: AppColors.surfaceContainerLowest,
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
    final timeLabel = _timeRangeLabel(item);
    final locationLabel = item.locationLabel ?? item.subtitle;

    return DraggableScrollableSheet(
      initialChildSize: 0.9,
      minChildSize: 0.5,
      maxChildSize: 0.95,
      expand: false,
      builder: (context, scrollController) {
        return SafeArea(
          top: false,
          child: SingleChildScrollView(
            controller: scrollController,
            padding: const EdgeInsets.fromLTRB(0, 12, 0, 24),
            child: Column(
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
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: _HeroImage(item: item),
                ),
                const SizedBox(height: 14),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          _InfoPill(icon: Icons.place_outlined, label: locationLabel),
                          if (timeLabel != null)
                            _InfoPill(icon: Icons.schedule, label: timeLabel),
                        ],
                      ),
                      const SizedBox(height: 12),
                      Text(item.placeName, style: AppTextStyles.headlineMd),
                      const SizedBox(height: 8),
                      _PriceRow(item: item),
                      if (item.isPlaceholderEnrichment) ...[
                        const SizedBox(height: 8),
                        Row(
                          children: [
                            const Icon(Icons.info_outline, size: 13, color: AppColors.textMuted),
                            const SizedBox(width: 4),
                            Text(
                              'Preview data — pending backend integration',
                              style: AppTextStyles.labelSm,
                            ),
                          ],
                        ),
                      ],
                      if (item.crowdCadence != null) ...[
                        const SizedBox(height: 14),
                        _CrowdCadenceCard(cadence: item.crowdCadence!),
                      ],
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
                      const SizedBox(height: 18),
                      Row(
                        children: [
                          Expanded(
                            child: OutlinedButton.icon(
                              onPressed: () => Navigator.of(context)
                                  .pop(PlaceDetailAction.remove),
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
              ],
            ),
          ),
        );
      },
    );
  }

  String? _timeRangeLabel(ItineraryItemData item) {
    final start = item.startTimeMinutes;
    final end = item.endTimeMinutes;
    if (start == null || end == null) return null;
    final duration = formatDurationLabel(item.durationMinutes ?? 0);
    return '${formatClockTime(start)} – ${formatClockTime(end)} • $duration';
  }
}

class _HeroImage extends StatelessWidget {
  const _HeroImage({required this.item});

  final ItineraryItemData item;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(20),
      child: Stack(
        children: [
          SizedBox(
            width: double.infinity,
            height: 200,
            child: item.heroImageUrl != null
                ? Image.network(item.heroImageUrl!, fit: BoxFit.cover)
                : Container(
                    color: AppColors.surfaceContainer,
                    alignment: Alignment.center,
                    child: const Icon(
                      Icons.image_outlined,
                      size: 32,
                      color: AppColors.textMuted,
                    ),
                  ),
          ),
          if (item.heroBadges.isNotEmpty)
            Positioned(
              left: 12,
              right: 12,
              top: 12,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  for (final badge in item.heroBadges) _HeroBadgePill(badge: badge),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _HeroBadgePill extends StatelessWidget {
  const _HeroBadgePill({required this.badge});

  final HeroBadgeData badge;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.92),
        borderRadius: BorderRadius.circular(9999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(badge.icon, size: 13, color: AppColors.onSurface),
          const SizedBox(width: 4),
          Text(
            badge.label,
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.onSurface,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

class _InfoPill extends StatelessWidget {
  const _InfoPill({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainer,
        borderRadius: BorderRadius.circular(9999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: AppColors.secondary),
          const SizedBox(width: 6),
          Text(label, style: AppTextStyles.labelMd),
        ],
      ),
    );
  }
}

class _PriceRow extends StatelessWidget {
  const _PriceRow({required this.item});

  final ItineraryItemData item;

  @override
  Widget build(BuildContext context) {
    final usd = item.priceUsdLabel;
    final local = item.priceLocalLabel;
    final priceContext = item.priceContextLabel;

    if (usd == null) {
      return Text(item.estimatedCostLabel, style: AppTextStyles.labelLg);
    }

    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Text(
          'Estimated $usd',
          style: AppTextStyles.headlineSm.copyWith(
            color: AppColors.primary,
            fontSize: 16,
          ),
        ),
        if (local != null)
          Text(
            ' ($local)',
            style: AppTextStyles.labelMd.copyWith(color: AppColors.primary),
          ),
        if (priceContext != null) ...[
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 8),
            child: Text('•', style: TextStyle(color: AppColors.textMuted)),
          ),
          Text(priceContext, style: AppTextStyles.bodySm),
        ],
      ],
    );
  }
}

class _CrowdCadenceCard extends StatelessWidget {
  const _CrowdCadenceCard({required this.cadence});

  final CrowdCadenceData cadence;

  Color get _accent => switch (cadence.level) {
        CrowdCadenceLevel.low => AppColors.success,
        CrowdCadenceLevel.moderate => AppColors.warning,
        CrowdCadenceLevel.high => AppColors.error,
      };

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  Container(
                    width: 8,
                    height: 8,
                    decoration: BoxDecoration(color: _accent, shape: BoxShape.circle),
                  ),
                  const SizedBox(width: 8),
                  Text('Optimal Crowd Cadence', style: AppTextStyles.labelLg),
                ],
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: _accent.withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(9999),
                ),
                child: Text(
                  '${cadence.levelLabel} / ${cadence.moodLabel}',
                  style: AppTextStyles.labelSm.copyWith(
                    color: _accent,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          ClipRRect(
            borderRadius: BorderRadius.circular(9999),
            child: LinearProgressIndicator(
              value: (cadence.currentCapacityPercent / 100).clamp(0, 1),
              minHeight: 6,
              backgroundColor: AppColors.surfaceContainerHigh,
              valueColor: AlwaysStoppedAnimation(_accent),
            ),
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Text(cadence.currentTimeLabel, style: AppTextStyles.labelSm),
              ),
              Text(
                cadence.peakTimeLabel,
                style: AppTextStyles.labelSm,
                textAlign: TextAlign.right,
              ),
            ],
          ),
        ],
      ),
    );
  }
}
