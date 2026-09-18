import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/models/trip_overview_data.dart';
import '../../../data/repositories/api_trip_overview_repository.dart';
import '../../providers/trip_overview_provider.dart';
import '../../widgets/app_bottom_navigation.dart';
import '../../widgets/day_selector.dart';
import '../../widgets/error_state.dart';
import '../../widgets/itinerary_item_card.dart';
import '../../widgets/loading_skeleton.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/secondary_button.dart';
import '../../widgets/status_badge.dart';
import '../../widgets/trip_overview/archive_delete_dialog.dart';
import '../../widgets/trip_overview/edit_item_modal.dart';
import '../../widgets/trip_overview/place_detail_sheet.dart';
import '../../widgets/trip_overview/regenerate_sheet.dart';

/// MOB-TRIP-09 — Trip Overview
///
/// Tabs:
///   1. Itinerary
///   2. Costs & Split
///
/// The Costs & Split tab follows the supplied Figma reference.
class TripOverviewScreen extends StatelessWidget {
  const TripOverviewScreen({
    super.key,
    required this.tripId,
  });

  final String tripId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => TripOverviewProvider(
        repository: ApiTripOverviewRepository(
          apiClient: context.read<ApiClient>(),
        ),
        tripId: tripId,
      ),
      child: const _TripOverviewView(),
    );
  }
}

class _TripOverviewView extends StatelessWidget {
  const _TripOverviewView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripOverviewProvider>();

    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: switch (provider.status) {
          TripOverviewStatus.loading => const _LoadingBody(),

          TripOverviewStatus.failure => Center(
            child: ErrorState(
              title: "Couldn't load this trip",
              description:
              provider.errorMessage ?? 'Something went wrong.',
              onAction: provider.reload,
            ),
          ),

          TripOverviewStatus.success => _TabbedBody(
            trip: provider.trip!,
          ),
        },
      ),
      bottomNavigationBar: const AppBottomNavigation(),
    );
  }
}

class _LoadingBody extends StatelessWidget {
  const _LoadingBody();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          LoadingSkeleton(
            height: 28,
            width: 220,
          ),
          SizedBox(height: 12),
          LoadingSkeleton(
            height: 16,
            width: 160,
          ),
          SizedBox(height: 20),
          LoadingSkeleton(
            height: 120,
            width: double.infinity,
          ),
          SizedBox(height: 20),
          LoadingSkeleton(
            height: 38,
            width: double.infinity,
          ),
          SizedBox(height: 20),
          LoadingSkeleton(
            height: 140,
            width: double.infinity,
          ),
          SizedBox(height: 12),
          LoadingSkeleton(
            height: 140,
            width: double.infinity,
          ),
        ],
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// COST CATEGORY CONFIGURATION
// -----------------------------------------------------------------------------

const _categoryColors = {
  'ACCOMMODATION': Color(0xFFE0574A),
  'TRANSPORTATION': AppColors.secondary,
  'FOOD': Color(0xFF2E6F9E),
  'ACTIVITIES': Color(0xFFE8A93B),
  'OTHER': Color(0xFF8B3A3A),
};

const _categoryIcons = {
  'ACCOMMODATION': Icons.hotel_outlined,
  'TRANSPORTATION': Icons.directions_car_outlined,
  'FOOD': Icons.restaurant_outlined,
  'ACTIVITIES': Icons.local_activity_outlined,
  'OTHER': Icons.shield_outlined,
};

const _categoryChartLabels = {
  'ACCOMMODATION': 'Stay',
  'TRANSPORTATION': 'Transit',
  'FOOD': 'Food',
  'ACTIVITIES': 'Activity',
  'OTHER': 'Other',
};

// -----------------------------------------------------------------------------
// MAIN TABBED BODY
// -----------------------------------------------------------------------------

class _TabbedBody extends StatefulWidget {
  const _TabbedBody({
    required this.trip,
  });

  final TripOverviewData trip;

  @override
  State<_TabbedBody> createState() => _TabbedBodyState();
}

class _TabbedBodyState extends State<_TabbedBody> {
  bool _showJpy = false;

  void _toggleCurrency() {
    setState(() {
      _showJpy = !_showJpy;
    });
  }

  String _money(double usd) {
    if (!_showJpy) {
      return '\$${_withThousandsSeparator(usd.round())}';
    }

    final jpy = (usd * 150).round();

    return '¥${_withThousandsSeparator(jpy)}';
  }

  String _withThousandsSeparator(int amount) {
    return amount.toString().replaceAllMapped(
      RegExp(r'\B(?=(\d{3})+(?!\d))'),
          (match) => ',',
    );
  }

  @override
  Widget build(BuildContext context) {
    final trip = widget.trip;

    return DefaultTabController(
      length: 2,

      // IMPORTANT:
      // Start directly on Costs & Split to match the supplied design.
      initialIndex: 1,

      child: Column(
        children: [
          const _BrandBar(),

          _ActionBar(
            status: trip.status,
          ),

          Padding(
            padding: const EdgeInsets.fromLTRB(
              16,
              8,
              16,
              0,
            ),
            child: _TripTitleBlock(
              trip: trip,
            ),
          ),

          Padding(
            padding: const EdgeInsets.fromLTRB(
              16,
              12,
              16,
              0,
            ),
            child: _EstimateCard(
              trip: trip,
              money: _money,
              showJpy: _showJpy,
              onToggleCurrency: _toggleCurrency,
            ),
          ),

          const Padding(
            padding: EdgeInsets.fromLTRB(
              16,
              14,
              16,
              0,
            ),
            child: _TabPills(),
          ),

          const SizedBox(height: 4),

          Expanded(
            child: TabBarView(
              children: [
                _ItineraryBody(
                  trip: trip,
                ),
                _CostsBody(
                  trip: trip,
                  money: _money,
                  showJpy: _showJpy,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// BRAND BAR
// -----------------------------------------------------------------------------

class _BrandBar extends StatelessWidget {
  const _BrandBar();

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 52,
      padding: const EdgeInsets.symmetric(
        horizontal: 16,
      ),
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(
          bottom: BorderSide(
            color: AppColors.surfaceContainer,
          ),
        ),
      ),
      child: Row(
        children: [
          Image.asset(
            'assets/images/Triply Logo.png',
            width: 22,
            height: 22,
          ),

          const SizedBox(width: 6),

          Text(
            'Triply',
            style: AppTextStyles.labelLg.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),

          const Spacer(),

          const Icon(
            Icons.notifications_none_rounded,
            size: 21,
          ),

          const SizedBox(width: 12),

          Container(
            width: 28,
            height: 28,
            decoration: const BoxDecoration(
              color: AppColors.primary,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.person,
              color: AppColors.onPrimary,
              size: 15,
            ),
          ),
        ],
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// ACTION BAR
// -----------------------------------------------------------------------------

class _ActionBar extends StatelessWidget {
  const _ActionBar({
    required this.status,
  });

  final String status;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        8,
        6,
        8,
        0,
      ),
      child: Row(
        children: [
          IconButton(
            onPressed: () {
              Navigator.of(context).maybePop();
            },
            icon: const Icon(
              Icons.arrow_back,
              size: 21,
            ),
          ),

          StatusBadge(
            label: status,
            variant: switch (status) {
              'SAVED' || 'GENERATED' =>
              StatusBadgeVariant.success,
              'MODIFIED' =>
              StatusBadgeVariant.userModified,
              'ARCHIVED' =>
              StatusBadgeVariant.neutral,
              _ =>
              StatusBadgeVariant.neutral,
            },
          ),

          const Spacer(),

          _CircleIconButton(
            tooltip: 'Share',
            icon: Icons.ios_share_outlined,
            onPressed: () {
              _placeholder(
                context,
                'Sharing',
              );
            },
          ),

          const SizedBox(width: 8),

          _CircleIconButton(
            tooltip: 'Invite',
            icon: Icons.person_add_alt_outlined,
            onPressed: () {
              _placeholder(
                context,
                'Inviting collaborators',
              );
            },
          ),

          const SizedBox(width: 8),

          _CircleIconButton(
            tooltip: 'Regenerate',
            icon: Icons.autorenew,
            onPressed: () => _handleRegenerate(context),
          ),

          const SizedBox(width: 8),

          _CircleIconButton(
            tooltip: 'Archive trip',
            icon: Icons.archive_outlined,
            onPressed: () => _handleArchive(context),
          ),
        ],
      ),
    );
  }

  Future<void> _handleRegenerate(BuildContext context) async {
    final scope = await showRegenerateSheet(context);
    if (scope != null && context.mounted) {
      _placeholder(
        context,
        scope == RegenerateScope.item
            ? 'Regenerating this item'
            : 'Regenerating this day',
      );
    }
  }

  Future<void> _handleArchive(BuildContext context) async {
    final confirmed = await showArchiveTripDialog(context);
    if (!confirmed || !context.mounted) return;

    final provider = context.read<TripOverviewProvider>();
    await provider.archiveTrip(context.read<ApiClient>());
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Trip archived.')),
      );
    }
  }
}

// -----------------------------------------------------------------------------
// CIRCLE ACTION BUTTON
// -----------------------------------------------------------------------------

class _CircleIconButton extends StatelessWidget {
  const _CircleIconButton({
    required this.icon,
    required this.onPressed,
    required this.tooltip,
  });

  final IconData icon;
  final VoidCallback onPressed;
  final String tooltip;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      child: InkWell(
        onTap: onPressed,
        customBorder: const CircleBorder(),
        child: Container(
          width: 36,
          height: 36,
          decoration: const BoxDecoration(
            color: AppColors.surfaceContainerLow,
            shape: BoxShape.circle,
          ),
          child: Icon(
            icon,
            size: 17,
            color: AppColors.secondary,
          ),
        ),
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// PLACEHOLDER
// -----------------------------------------------------------------------------

void _placeholder(
    BuildContext context,
    String feature,
    ) {
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Text(
        '$feature is coming soon.',
      ),
      behavior: SnackBarBehavior.floating,
    ),
  );
}

// -----------------------------------------------------------------------------
// TRIP TITLE
// -----------------------------------------------------------------------------

class _TripTitleBlock extends StatelessWidget {
  const _TripTitleBlock({
    required this.trip,
  });

  final TripOverviewData trip;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Icon(
              Icons.location_on_outlined,
              size: 13,
              color: AppColors.primary,
            ),

            const SizedBox(width: 4),

            Expanded(
              child: Text(
                trip.regionLabel,
                overflow: TextOverflow.ellipsis,
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ],
        ),

        const SizedBox(height: 4),

        Text(
          trip.tripTitle,
          style: AppTextStyles.headlineLg.copyWith(
            fontSize: 24,
            fontWeight: FontWeight.w800,
          ),
        ),

        const SizedBox(height: 4),

        Text(
          '${trip.dateRangeLabel} · '
              '${trip.totalDays} Days · '
              '${trip.travelerCount} '
              '${trip.travelerCount == 1 ? 'Adult' : 'Adults'}',
          style: AppTextStyles.bodySm.copyWith(
            color: AppColors.secondary,
          ),
        ),
      ],
    );
  }
}

// -----------------------------------------------------------------------------
// ESTIMATE CARD
// -----------------------------------------------------------------------------

class _EstimateCard extends StatelessWidget {
  const _EstimateCard({
    required this.trip,
    required this.money,
    required this.showJpy,
    required this.onToggleCurrency,
  });

  final TripOverviewData trip;
  final String Function(double) money;
  final bool showJpy;
  final VoidCallback onToggleCurrency;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(
        16,
        14,
        16,
        15,
      ),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            AppColors.primaryContainerLight.withOpacity(0.55),
            AppColors.surfaceContainerLow,
          ],
        ),
        borderRadius: BorderRadius.circular(22),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Flexible(
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 9,
                    vertical: 4,
                  ),
                  decoration: BoxDecoration(
                    color: Colors.white.withOpacity(0.72),
                    borderRadius: BorderRadius.circular(9999),
                  ),
                  child: Text(
                    'Estimated (not verified pricing)',
                    overflow: TextOverflow.ellipsis,
                    style: AppTextStyles.labelSm.copyWith(
                      color: AppColors.primary,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ),

              const SizedBox(width: 8),

              InkWell(
                onTap: onToggleCurrency,
                borderRadius: BorderRadius.circular(9999),
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 9,
                    vertical: 4,
                  ),
                  decoration: BoxDecoration(
                    color: Colors.white.withOpacity(0.72),
                    borderRadius: BorderRadius.circular(9999),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        showJpy ? 'JPY (¥)' : 'USD (\$)',
                        style: AppTextStyles.labelSm.copyWith(
                          color: AppColors.secondary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(width: 4),
                      const Icon(
                        Icons.swap_horiz_rounded,
                        size: 14,
                        color: AppColors.secondary,
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: 10),

          Row(
            crossAxisAlignment: CrossAxisAlignment.baseline,
            textBaseline: TextBaseline.alphabetic,
            children: [
              Text(
                money(trip.totalEstimatedCostUsd),
                style: AppTextStyles.headlineLg.copyWith(
                  fontSize: 34,
                  fontWeight: FontWeight.w900,
                ),
              ),

              const SizedBox(width: 8),

              Text(
                'total estimate',
                style: AppTextStyles.bodySm.copyWith(
                  color: AppColors.secondary,
                ),
              ),
            ],
          ),

          const SizedBox(height: 6),

          Row(
            children: [
              const Icon(
                Icons.schedule_rounded,
                size: 14,
                color: AppColors.secondary,
              ),

              const SizedBox(width: 4),

              Flexible(
                child: Text(
                  '${money(trip.avgPerDayPerTravelerUsd)}'
                      '/day avg per traveler',
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.secondary,
                  ),
                ),
              ),

              if (trip.isOnTarget) ...[
                const SizedBox(width: 9),

                const Icon(
                  Icons.check_circle_rounded,
                  size: 14,
                  color: AppColors.success,
                ),

                const SizedBox(width: 3),

                Text(
                  'On Target',
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.success,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// TAB PILLS
// -----------------------------------------------------------------------------

class _TabPills extends StatelessWidget {
  const _TabPills();

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 42,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(9999),
      ),
      child: TabBar(
        indicator: BoxDecoration(
          color: AppColors.secondary,
          borderRadius: BorderRadius.circular(9999),
        ),
        indicatorSize: TabBarIndicatorSize.tab,
        dividerColor: Colors.transparent,
        labelColor: AppColors.onPrimary,
        unselectedLabelColor: AppColors.secondary,
        labelStyle: AppTextStyles.labelMd.copyWith(
          fontWeight: FontWeight.w700,
        ),
        tabs: const [
          Tab(
            text: 'Itinerary',
          ),
          Tab(
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(
                  Icons.description_outlined,
                  size: 15,
                ),
                SizedBox(width: 6),
                Text(
                  'Costs & Split',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// ITINERARY TAB
// -----------------------------------------------------------------------------

class _ItineraryBody extends StatelessWidget {
  const _ItineraryBody({
    required this.trip,
  });

  final TripOverviewData trip;

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripOverviewProvider>();

    final selectedDay =
    trip.days[provider.selectedDayIndex];

    final orderedItems =
    _orderedByTimeSlot(selectedDay.items);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: 12),

        Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: 16,
          ),
          child: DaySelector(
            days: [
              for (final day in trip.days)
                DaySelectorItem(
                  label:
                  'Day ${day.dayNumber} • ${day.dateLabel}',
                  dayIndex: day.dayNumber - 1,
                ),
            ],
            selectedIndex: provider.selectedDayIndex,
            onDaySelected: provider.selectDay,
          ),
        ),

        const SizedBox(height: 12),

        Expanded(
          child: ListView.separated(
            padding: const EdgeInsets.fromLTRB(
              16,
              0,
              16,
              24,
            ),
            itemCount: orderedItems.length,
            separatorBuilder: (_, __) =>
            const SizedBox(height: 12),
            itemBuilder: (context, index) {
              final item = orderedItems[index];

              return ItineraryItemCard(
                time: _timeSlotLabel(
                  item.timeSlot,
                ),
                title: item.placeName,
                subtitle: item.subtitle,
                estimatedCostLabel:
                item.estimatedCostLabel,
                tipText: item.tipText,
                isAiGenerated:
                item.isAiGenerated,
                isUserModified:
                !item.isAiGenerated,
                onTap: () async {
                  final action = await showPlaceDetailSheet(context, item);
                  if (!context.mounted) return;

                  final itemIndex = selectedDay.items.indexOf(item);
                  if (action == PlaceDetailAction.edit) {
                    await _editItem(context, provider.selectedDayIndex, itemIndex, item);
                  } else if (action == PlaceDetailAction.remove) {
                    context.read<TripOverviewProvider>().removeItem(
                          provider.selectedDayIndex,
                          itemIndex,
                        );
                  }
                },
                onEdit: () => _editItem(
                  context,
                  provider.selectedDayIndex,
                  selectedDay.items.indexOf(item),
                  item,
                ),
                onRegenerate: () async {
                  final scope = await showRegenerateSheet(context);
                  if (scope != null && context.mounted) {
                    _placeholder(
                      context,
                      scope == RegenerateScope.item
                          ? 'Regenerating this item'
                          : 'Regenerating this day',
                    );
                  }
                },
              );
            },
          ),
        ),
      ],
    );
  }

  Future<void> _editItem(
    BuildContext context,
    int dayIndex,
    int itemIndex,
    ItineraryItemData item,
  ) async {
    final edited = await showEditItemModal(context, item);
    if (edited != null && context.mounted) {
      context.read<TripOverviewProvider>().updateItem(dayIndex, itemIndex, edited);
    }
  }

  List<ItineraryItemData> _orderedByTimeSlot(
      List<ItineraryItemData> items,
      ) {
    const order = {
      'MORNING': 0,
      'AFTERNOON': 1,
      'EVENING': 2,
    };

    final sorted = [...items];

    sorted.sort(
          (a, b) {
        final slotCompare =
        (order[a.timeSlot] ?? 99)
            .compareTo(order[b.timeSlot] ?? 99);

        if (slotCompare != 0) {
          return slotCompare;
        }

        return a.orderIndex.compareTo(
          b.orderIndex,
        );
      },
    );

    return sorted;
  }

  String _timeSlotLabel(
      String timeSlot,
      ) {
    return switch (timeSlot) {
      'MORNING' => '09:00 AM',
      'AFTERNOON' => '01:00 PM',
      'EVENING' => '07:00 PM',
      _ => timeSlot,
    };
  }
}

// -----------------------------------------------------------------------------
// COSTS & SPLIT TAB
// -----------------------------------------------------------------------------

class _CostsBody extends StatelessWidget {
  const _CostsBody({
    required this.trip,
    required this.money,
    required this.showJpy,
  });

  final TripOverviewData trip;
  final String Function(double) money;
  final bool showJpy;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        16,
        12,
        16,
        24,
      ),
      children: [
        // Transparent breakdown
        _TransparentBreakdownBanner(
          trip: trip,
        ),

        const SizedBox(height: 12),

        // Budget Health
        _BudgetHealthCard(
          trip: trip,
          money: money,
          showJpy: showJpy,
        ),

        const SizedBox(height: 12),

        // Adjust / Export buttons
        Row(
          children: [
            Expanded(
              child: SecondaryButton(
                label: 'Adjust Budget',
                icon: Icons.tune_rounded,
                fullWidth: true,
                onPressed: () {
                  _placeholder(
                    context,
                    'Adjusting budget',
                  );
                },
              ),
            ),

            const SizedBox(width: 10),

            Expanded(
              child: PrimaryButton(
                label: 'Export Breakdown',
                icon: Icons.download_rounded,
                fullWidth: true,
                onPressed: () {
                  _placeholder(
                    context,
                    'Exporting',
                  );
                },
              ),
            ),
          ],
        ),

        const SizedBox(height: 12),

        // Category chart + legend
        _CategoryChart(
          categories: trip.costCategories,
        ),

        const SizedBox(height: 16),

        // Category cards
        for (final category
        in trip.costCategories) ...[
          _CategoryCard(
            category: category,
            money: money,
          ),
          const SizedBox(height: 12),
        ],

        // Stay highlight
        if (trip.stayHighlight != null)
          _StayHighlightCard(
            highlight: trip.stayHighlight!,
          ),
      ],
    );
  }
}

// -----------------------------------------------------------------------------
// TRANSPARENT BREAKDOWN
// -----------------------------------------------------------------------------

class _TransparentBreakdownBanner
    extends StatelessWidget {
  const _TransparentBreakdownBanner({
    required this.trip,
  });

  final TripOverviewData trip;

  @override
  Widget build(BuildContext context) {
    final region =
    trip.regionLabel.split('•').last.trim();

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(
        horizontal: 12,
        vertical: 12,
      ),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(18),
      ),
      child: Row(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.currency_exchange_rounded,
            size: 17,
            color: AppColors.primary,
          ),

          const SizedBox(width: 8),

          Expanded(
            child: RichText(
              text: TextSpan(
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.secondary,
                  height: 1.35,
                ),
                children: [
                  TextSpan(
                    text:
                    'Transparent Breakdown: ',
                    style:
                    AppTextStyles.labelSm.copyWith(
                      fontWeight: FontWeight.w700,
                      color: AppColors.onSurface,
                    ),
                  ),
                  TextSpan(
                    text:
                    'Estimates based on mid-tier '
                        'seasonal averages in $region. '
                        'All final costs subject to '
                        'partner booking.',
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// BUDGET HEALTH
// -----------------------------------------------------------------------------

class _BudgetHealthCard
    extends StatelessWidget {
  const _BudgetHealthCard({
    required this.trip,
    required this.money,
    required this.showJpy,
  });

  final TripOverviewData trip;
  final String Function(double) money;
  final bool showJpy;

  @override
  Widget build(BuildContext context) {
    final health = trip.budgetHealth;

    final fraction = health.spentFraction;

    final spentPercent = health.targetCapUsd <= 0
        ? 0
        : (health.spentUsd / health.targetCapUsd * 100).round();

    final remainingPercent =
        100 - spentPercent;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(
        14,
        14,
        14,
        12,
      ),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLowest,
        borderRadius: BorderRadius.circular(21),
        boxShadow: [
          BoxShadow(
            color: AppColors.shadowAmbient,
            blurRadius: 10,
            offset: const Offset(0, 3),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 34,
                height: 34,
                decoration: BoxDecoration(
                  color: const Color(0xFFE4FAF1),
                  borderRadius:
                  BorderRadius.circular(11),
                ),
                child: const Icon(
                  Icons.savings_outlined,
                  size: 19,
                  color: AppColors.success,
                ),
              ),

              const SizedBox(width: 9),

              Expanded(
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Budget Health',
                      style:
                      AppTextStyles.labelLg
                          .copyWith(
                        fontWeight:
                        FontWeight.w800,
                      ),
                    ),
                    Text(
                      health.isUnderBudget
                          ? '${money(health.remainingUsd.abs())} '
                          'under target budget'
                          : '${money(health.remainingUsd.abs())} '
                          'over target budget',
                      style:
                      AppTextStyles.labelSm
                          .copyWith(
                        color:
                        health.isUnderBudget
                            ? AppColors.success
                            : AppColors.error,
                        fontWeight:
                        FontWeight.w700,
                      ),
                    ),
                  ],
                ),
              ),

              Column(
                crossAxisAlignment:
                CrossAxisAlignment.end,
                children: [
                  Text(
                    'Target Cap',
                    style:
                    AppTextStyles.labelSm
                        .copyWith(
                      color: AppColors.textMuted,
                    ),
                  ),
                  Text(
                    money(
                      health.targetCapUsd,
                    ),
                    style:
                    AppTextStyles.labelMd
                        .copyWith(
                      fontWeight:
                      FontWeight.w900,
                    ),
                  ),
                ],
              ),
            ],
          ),

          const SizedBox(height: 11),

          ClipRRect(
            borderRadius:
            BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: fraction
                  .clamp(0, 1)
                  .toDouble(),
              minHeight: 8,
              backgroundColor:
              AppColors.surfaceContainer,
              valueColor:
              AlwaysStoppedAnimation<Color>(
                health.isUnderBudget
                    ? AppColors.primary
                    : AppColors.error,
              ),
            ),
          ),

          const SizedBox(height: 8),

          Row(
            children: [
              Expanded(
                child: Text(
                  'Est. Spent: '
                      '${money(health.spentUsd)} '
                      '($spentPercent%)',
                  style:
                  AppTextStyles.labelSm
                      .copyWith(
                    color:
                    AppColors.textMuted,
                    fontWeight:
                    FontWeight.w600,
                  ),
                ),
              ),

              Text(
                'Remaining: '
                    '${money(health.remainingUsd.abs())} '
                    '($remainingPercent%)',
                style:
                AppTextStyles.labelSm
                    .copyWith(
                  color:
                  health.isUnderBudget
                      ? AppColors.success
                      : AppColors.error,
                  fontWeight:
                  FontWeight.w700,
                ),
              ),
            ],
          ),

          const SizedBox(height: 9),

          Container(
            width: double.infinity,
            padding:
            const EdgeInsets.symmetric(
              horizontal: 10,
              vertical: 7,
            ),
            decoration: BoxDecoration(
              color:
              AppColors.surfaceContainerLow,
              borderRadius:
              BorderRadius.circular(18),
            ),
            child: Row(
              mainAxisAlignment:
              MainAxisAlignment.center,
              children: [
                const Icon(
                  Icons.currency_exchange_rounded,
                  size: 15,
                  color: AppColors.secondary,
                ),

                const SizedBox(width: 5),

                Flexible(
                  child: Text(
                    showJpy
                        ? 'Displayed in JPY (¥) • '
                        'Tap to view in USD (\$)'
                        : 'Displayed in USD (\$) • '
                        'Tap to view in JPY (¥)',
                    textAlign: TextAlign.center,
                    style:
                    AppTextStyles.labelSm
                        .copyWith(
                      color:
                      AppColors.secondary,
                      fontWeight:
                      FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// CATEGORY CHART
// -----------------------------------------------------------------------------

class _CategoryChart
    extends StatelessWidget {
  const _CategoryChart({
    required this.categories,
  });

  final List<CostCategoryEstimateData>
  categories;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment:
      CrossAxisAlignment.start,
      children: [
        ClipRRect(
          borderRadius:
          BorderRadius.circular(9999),
          child: SizedBox(
            height: 8,
            child: Row(
              children: [
                for (final category
                in categories)
                  Expanded(
                    flex:
                    category.percentOfTotal,
                    child: Container(
                      color: _categoryColors[
                      category.code] ??
                          AppColors.secondary,
                    ),
                  ),
              ],
            ),
          ),
        ),

        const SizedBox(height: 10),

        Wrap(
          spacing: 12,
          runSpacing: 6,
          children: [
            for (final category
            in categories)
              Row(
                mainAxisSize:
                MainAxisSize.min,
                children: [
                  Container(
                    width: 7,
                    height: 7,
                    decoration: BoxDecoration(
                      color: _categoryColors[
                      category.code] ??
                          AppColors.secondary,
                      shape: BoxShape.circle,
                    ),
                  ),

                  const SizedBox(width: 4),

                  Text(
                    '${_categoryChartLabels[category.code] ?? category.label} '
                        '(${category.percentOfTotal}%)',
                    style:
                    AppTextStyles.labelSm
                        .copyWith(
                      fontSize: 9,
                      color:
                      AppColors.secondary,
                      fontWeight:
                      FontWeight.w600,
                    ),
                  ),
                ],
              ),
          ],
        ),
      ],
    );
  }
}

// -----------------------------------------------------------------------------
// CATEGORY CARD
// -----------------------------------------------------------------------------

class _CategoryCard
    extends StatelessWidget {
  const _CategoryCard({
    required this.category,
    required this.money,
  });

  final CostCategoryEstimateData category;
  final String Function(double) money;

  @override
  Widget build(BuildContext context) {
    final color =
        _categoryColors[category.code] ??
            AppColors.secondary;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(
        14,
        14,
        14,
        12,
      ),
      decoration: BoxDecoration(
        color:
        AppColors.surfaceContainerLowest,
        borderRadius:
        BorderRadius.circular(22),
        boxShadow: [
          BoxShadow(
            color: AppColors.shadowAmbient,
            blurRadius: 8,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment:
            CrossAxisAlignment.start,
            children: [
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  color:
                  color.withOpacity(0.12),
                  borderRadius:
                  BorderRadius.circular(12),
                ),
                child: Icon(
                  _categoryIcons[
                  category.code] ??
                      Icons.category_outlined,
                  size: 19,
                  color: color,
                ),
              ),

              const SizedBox(width: 10),

              Expanded(
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.start,
                  children: [
                    Wrap(
                      crossAxisAlignment:
                      WrapCrossAlignment.center,
                      spacing: 6,
                      runSpacing: 4,
                      children: [
                        Text(
                          category.label,
                          style:
                          AppTextStyles
                              .labelLg
                              .copyWith(
                            fontWeight:
                            FontWeight.w800,
                          ),
                        ),
                        _AccuracyBadge(
                          accuracy:
                          category.accuracy,
                        ),
                      ],
                    ),

                    const SizedBox(height: 2),

                    Text(
                      '${category.percentOfTotal}% '
                          'of total trip budget',
                      style:
                      AppTextStyles.labelSm
                          .copyWith(
                        color:
                        AppColors.textMuted,
                      ),
                    ),
                  ],
                ),
              ),

              const SizedBox(width: 8),

              Column(
                crossAxisAlignment:
                CrossAxisAlignment.end,
                children: [
                  Text(
                    money(
                      category.amountUsd,
                    ),
                    style:
                    AppTextStyles.headlineSm
                        .copyWith(
                      fontWeight:
                      FontWeight.w900,
                    ),
                  ),

                  if (category.contextLabel !=
                      null)
                    Text(
                      category.contextLabel!,
                      textAlign:
                      TextAlign.end,
                      style:
                      AppTextStyles.labelSm
                          .copyWith(
                        color:
                        AppColors.textMuted,
                        height: 1.2,
                      ),
                    ),
                ],
              ),
            ],
          ),

          const SizedBox(height: 10),

          ClipRRect(
            borderRadius:
            BorderRadius.circular(9999),
            child: LinearProgressIndicator(
              value: (category.percentOfTotal /
                  100)
                  .clamp(0, 1)
                  .toDouble(),
              minHeight: 6,
              backgroundColor:
              AppColors.surfaceContainer,
              valueColor:
              AlwaysStoppedAnimation<Color>(
                color,
              ),
            ),
          ),

          if (category.items.isNotEmpty) ...[
            const SizedBox(height: 10),

            for (final item
            in category.items)
              Padding(
                padding:
                const EdgeInsets.only(
                  bottom: 6,
                ),
                child: Container(
                  width: double.infinity,
                  padding:
                  const EdgeInsets.symmetric(
                    horizontal: 9,
                    vertical: 8,
                  ),
                  decoration: BoxDecoration(
                    color:
                    AppColors
                        .surfaceContainerLow,
                    borderRadius:
                    BorderRadius.circular(15),
                  ),
                  child: Row(
                    children: [
                      Icon(
                        _itemIcon(
                          category.code,
                        ),
                        size: 15,
                        color:
                        AppColors.secondary,
                      ),

                      const SizedBox(width: 7),

                      Expanded(
                        child: Text(
                          item.label,
                          style:
                          AppTextStyles
                              .labelSm
                              .copyWith(
                            color:
                            AppColors
                                .secondary,
                            fontWeight:
                            FontWeight.w700,
                          ),
                        ),
                      ),

                      const SizedBox(width: 6),

                      Text(
                        money(
                          item.amountUsd,
                        ),
                        style:
                        AppTextStyles
                            .labelSm
                            .copyWith(
                          fontWeight:
                          FontWeight.w800,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
          ],
        ],
      ),
    );
  }

  IconData _itemIcon(
      String categoryCode,
      ) {
    switch (categoryCode) {
      case 'ACCOMMODATION':
        return Icons.hotel_outlined;

      case 'TRANSPORTATION':
        return Icons.train_outlined;

      case 'FOOD':
        return Icons.restaurant_outlined;

      case 'ACTIVITIES':
        return Icons.local_activity_outlined;

      case 'OTHER':
        return Icons.luggage_outlined;

      default:
        return Icons.circle_outlined;
    }
  }
}

// -----------------------------------------------------------------------------
// ACCURACY BADGE
// -----------------------------------------------------------------------------

class _AccuracyBadge
    extends StatelessWidget {
  const _AccuracyBadge({
    required this.accuracy,
  });

  final CostAccuracy accuracy;

  @override
  Widget build(BuildContext context) {
    if (accuracy ==
        CostAccuracy.estimated) {
      return Container(
        padding:
        const EdgeInsets.symmetric(
          horizontal: 8,
          vertical: 3,
        ),
        decoration: BoxDecoration(
          color:
          AppColors.surfaceContainerLow,
          borderRadius:
          BorderRadius.circular(9999),
        ),
        child: Text(
          'Estimated',
          style:
          AppTextStyles.labelSm.copyWith(
            fontSize: 9,
            color:
            AppColors.secondary,
            fontWeight:
            FontWeight.w700,
          ),
        ),
      );
    }

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          padding:
          const EdgeInsets.symmetric(
            horizontal: 8,
            vertical: 3,
          ),
          decoration: BoxDecoration(
            color:
            AppColors.tertiaryFixed,
            borderRadius:
            BorderRadius.circular(9999),
          ),
          child: Text(
            'Verified / AI-matched',
            style:
            AppTextStyles.labelSm.copyWith(
              fontSize: 9,
              color:
              AppColors.secondary,
              fontWeight:
              FontWeight.w700,
            ),
          ),
        ),

        const SizedBox(width: 5),

        Text(
          'Accurate',
          style:
          AppTextStyles.labelSm.copyWith(
            fontSize: 9,
            color: AppColors.success,
            fontWeight: FontWeight.w800,
          ),
        ),
      ],
    );
  }
}

// -----------------------------------------------------------------------------
// STAY HIGHLIGHT
// -----------------------------------------------------------------------------

class _StayHighlightCard
    extends StatelessWidget {
  const _StayHighlightCard({
    required this.highlight,
  });

  final StayHighlightData highlight;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius:
      BorderRadius.circular(25),
      child: SizedBox(
        height: 145,
        width: double.infinity,
        child: Stack(
          fit: StackFit.expand,
          children: [
            Image.asset(
              highlight.imageAsset,
              fit: BoxFit.cover,
              errorBuilder:
                  (_, __, ___) {
                return Container(
                  color:
                  AppColors
                      .surfaceContainer,
                  child: const Icon(
                    Icons
                        .image_not_supported_outlined,
                    color:
                    AppColors.secondary,
                    size: 32,
                  ),
                );
              },
            ),

            Container(
              decoration:
              BoxDecoration(
                gradient:
                LinearGradient(
                  begin:
                  Alignment.topCenter,
                  end:
                  Alignment.bottomCenter,
                  colors: [
                    Colors.transparent,
                    Colors.black
                        .withOpacity(0.78),
                  ],
                ),
              ),
            ),

            Positioned(
              left: 14,
              right: 14,
              bottom: 12,
              child: Row(
                crossAxisAlignment:
                CrossAxisAlignment.end,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment:
                      CrossAxisAlignment
                          .start,
                      children: [
                        Text(
                          'STAY HIGHLIGHT',
                          style:
                          AppTextStyles
                              .labelSm
                              .copyWith(
                            color:
                            Colors.white70,
                            fontWeight:
                            FontWeight.w800,
                            letterSpacing:
                            0.7,
                          ),
                        ),

                        const SizedBox(
                          height: 2,
                        ),

                        Text(
                          highlight.title,
                          style:
                          AppTextStyles
                              .headlineSm
                              .copyWith(
                            color:
                            Colors.white,
                            fontWeight:
                            FontWeight.w900,
                          ),
                        ),

                        Text(
                          highlight.subtitle,
                          style:
                          AppTextStyles
                              .bodySm
                              .copyWith(
                            color:
                            Colors.white70,
                          ),
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(width: 8),

                  Container(
                    padding:
                    const EdgeInsets
                        .symmetric(
                      horizontal: 10,
                      vertical: 7,
                    ),
                    decoration:
                    BoxDecoration(
                      color: Colors.black
                          .withOpacity(0.55),
                      borderRadius:
                      BorderRadius.circular(
                        16,
                      ),
                    ),
                    child: Text(
                      '4 Nights • Est.\n\$680',
                      style:
                      AppTextStyles
                          .labelSm
                          .copyWith(
                        color:
                        Colors.white,
                        fontWeight:
                        FontWeight.w700,
                        height: 1.25,
                      ),
                    ),
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