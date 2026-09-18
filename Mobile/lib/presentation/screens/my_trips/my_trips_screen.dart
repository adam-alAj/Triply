import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/models/trip_summary.dart';
import '../../../data/repositories/api_my_trips_repository.dart';
import '../../providers/my_trips_provider.dart';
import '../../widgets/app_bottom_navigation.dart';
import '../../widgets/empty_state.dart';

class MyTripsScreen extends StatelessWidget {
  const MyTripsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => MyTripsProvider(
        repository: ApiMyTripsRepository(apiClient: context.read<ApiClient>()),
      )..loadTrips(),
      child: const _MyTripsView(),
    );
  }
}

class _MyTripsView extends StatelessWidget {
  const _MyTripsView();

  // One local image per trip, cycled by position — the backend doesn't
  // serve trip imagery yet (same gap as Trip Creation's suggestion cards).
  static const _images = [
    'assets/images/trip_creation/kyoto_tokyo.jpg',
    'assets/images/trip_creation/amalfi_rome.jpg',
    'assets/images/trip_creation/lucerne_interlaken.jpg',
    'assets/images/trip_creation/sintra_cascais.jpg',
    'assets/images/trip_creation/oaxaca.jpg',
    'assets/images/trip_creation/tokyo.jpg',
    'assets/images/trip_creation/kyoto.jpg',
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: Consumer<MyTripsProvider>(
          builder: (context, provider, _) {
            return Column(
              children: [
                const _TopBar(),
                const SizedBox(height: 12),
                const _SearchBar(),
                const SizedBox(height: 12),
                _FilterTabs(provider: provider),
                const SizedBox(height: 8),
                Expanded(child: _Body(provider: provider, images: _images)),
              ],
            );
          },
        ),
      ),
      bottomNavigationBar: const AppBottomNavigation(selected: AppNavTab.myTrips),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body({required this.provider, required this.images});

  final MyTripsProvider provider;
  final List<String> images;

  @override
  Widget build(BuildContext context) {
    if (provider.status == MyTripsStatus.loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (provider.status == MyTripsStatus.failure) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                provider.errorMessage ?? 'Something went wrong.',
                textAlign: TextAlign.center,
                style: AppTextStyles.bodyMd,
              ),
              const SizedBox(height: 16),
              TextButton(
                onPressed: provider.loadTrips,
                child: const Text('Try again'),
              ),
            ],
          ),
        ),
      );
    }

    final trips = provider.visibleTrips;

    if (trips.isEmpty && provider.filter == MyTripsFilter.active) {
      return SingleChildScrollView(
        child: EmptyState(
          title: 'No more trips planned',
          description:
              "Ready for your next journey? Let Triply curate your perfect "
              'pacing, hidden gems, and budget.',
          icon: Icons.explore_outlined,
          actionLabel: 'Plan a New Trip',
          onAction: () => Navigator.pushNamed(context, '/create-trip'),
        ),
      );
    }

    if (trips.isEmpty) {
      return const Center(
        child: EmptyState(
          title: 'No archived trips',
          description: 'Trips you archive will show up here.',
          icon: Icons.inventory_2_outlined,
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: provider.loadTrips,
      child: ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        itemCount: trips.length,
        itemBuilder: (context, index) {
          final trip = trips[index];
          return Padding(
            padding: const EdgeInsets.only(bottom: 16),
            child: _TripCard(
              trip: trip,
              image: images[index % images.length],
            ),
          );
        },
      ),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
      child: Row(
        children: [
          Image.asset('assets/images/Triply Logo.png', width: 24, height: 24),
          const SizedBox(width: 8),
          Text(
            'Triply',
            style: AppTextStyles.labelLg.copyWith(fontWeight: FontWeight.w700),
          ),
          const Spacer(),
          IconButton(
            tooltip: 'Notifications',
            onPressed: () {},
            icon: const Icon(Icons.notifications_none_outlined, size: 21),
          ),
          Container(
            width: 30,
            height: 30,
            decoration: const BoxDecoration(
              color: AppColors.primary,
              shape: BoxShape.circle,
            ),
            alignment: Alignment.center,
            child: const Icon(Icons.person, color: Colors.white, size: 16),
          ),
        ],
      ),
    );
  }
}

class _SearchBar extends StatelessWidget {
  const _SearchBar();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Row(
        children: [
          Expanded(
            child: Container(
              height: 44,
              padding: const EdgeInsets.symmetric(horizontal: 14),
              decoration: BoxDecoration(
                color: AppColors.surfaceContainerLow,
                borderRadius: BorderRadius.circular(999),
              ),
              child: Row(
                children: [
                  const Icon(Icons.search, size: 18, color: AppColors.textMuted),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Search saved destinations or dates...',
                      style: AppTextStyles.bodySm,
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(width: 10),
          InkWell(
            onTap: () => Navigator.pushNamed(context, '/create-trip'),
            borderRadius: BorderRadius.circular(999),
            child: Container(
              width: 44,
              height: 44,
              decoration: const BoxDecoration(
                color: AppColors.primary,
                shape: BoxShape.circle,
              ),
              child: const Icon(Icons.add, color: Colors.white, size: 22),
            ),
          ),
        ],
      ),
    );
  }
}

class _FilterTabs extends StatelessWidget {
  const _FilterTabs({required this.provider});

  final MyTripsProvider provider;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Container(
        padding: const EdgeInsets.all(4),
        decoration: BoxDecoration(
          color: AppColors.surfaceContainerLow,
          borderRadius: BorderRadius.circular(999),
        ),
        child: Row(
          children: [
            Expanded(
              child: _FilterTab(
                label: 'Active Trips',
                count: provider.activeCount,
                selected: provider.filter == MyTripsFilter.active,
                onTap: () => provider.setFilter(MyTripsFilter.active),
              ),
            ),
            Expanded(
              child: _FilterTab(
                label: 'Archived Trips',
                count: provider.archivedCount,
                selected: provider.filter == MyTripsFilter.archived,
                onTap: () => provider.setFilter(MyTripsFilter.archived),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _FilterTab extends StatelessWidget {
  const _FilterTab({
    required this.label,
    required this.count,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final int count;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 10),
        decoration: BoxDecoration(
          color: selected ? AppColors.secondary : Colors.transparent,
          borderRadius: BorderRadius.circular(999),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              label,
              style: AppTextStyles.labelMd.copyWith(
                color: selected ? Colors.white : AppColors.onSurface,
              ),
            ),
            const SizedBox(width: 6),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 1),
              decoration: BoxDecoration(
                color: selected ? Colors.white.withOpacity(0.25) : AppColors.surfaceContainer,
                borderRadius: BorderRadius.circular(999),
              ),
              child: Text(
                '$count',
                style: AppTextStyles.labelSm.copyWith(
                  color: selected ? Colors.white : AppColors.secondary,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TripCard extends StatelessWidget {
  const _TripCard({required this.trip, required this.image});

  final TripSummary trip;
  final String image;

  ({Color bg, Color fg, String label}) get _statusBadge {
    switch (trip.status) {
      case 'SAVED':
        return (bg: AppColors.successBg, fg: AppColors.success, label: 'SAVED • SYNCED');
      case 'GENERATED':
      case 'MODIFIED':
        return (bg: AppColors.warningBg, fg: AppColors.warning, label: 'DRAFT • AI MATCHED');
      case 'GENERATING':
        return (bg: AppColors.surfaceContainerHigh, fg: AppColors.secondary, label: 'GENERATING...');
      case 'ARCHIVED':
        return (bg: AppColors.surfaceContainerHigh, fg: AppColors.secondary, label: 'ARCHIVED');
      default:
        return (bg: AppColors.surfaceContainerHigh, fg: AppColors.secondary, label: 'DRAFT');
    }
  }

  @override
  Widget build(BuildContext context) {
    final badge = _statusBadge;
    final actionLabel = trip.isArchived
        ? 'View Itinerary'
        : (trip.status == 'SAVED' ? 'Open Itinerary' : 'Continue Edit');

    return InkWell(
      borderRadius: BorderRadius.circular(24),
      onTap: () => Navigator.pushNamed(context, '/trip-overview', arguments: trip.id),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          boxShadow: const [
            BoxShadow(blurRadius: 12, offset: Offset(0, 4), color: AppColors.shadowAmbient),
          ],
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              height: 160,
              width: double.infinity,
              child: Stack(
                fit: StackFit.expand,
                children: [
                  Image.asset(
                    image,
                    fit: BoxFit.cover,
                    errorBuilder: (_, __, ___) => Container(
                      color: AppColors.surfaceContainer,
                      child: const Icon(Icons.image_outlined, size: 32, color: AppColors.textMuted),
                    ),
                  ),
                  Positioned(
                    top: 10,
                    left: 10,
                    child: _Pill(bg: badge.bg, fg: badge.fg, label: badge.label),
                  ),
                  if (trip.formattedBudget != null)
                    Positioned(
                      top: 10,
                      right: 10,
                      child: _Pill(
                        bg: Colors.black.withOpacity(0.45),
                        fg: Colors.white,
                        label: 'EST. ${trip.formattedBudget}',
                      ),
                    ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(14, 12, 14, 14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(trip.title, style: AppTextStyles.headlineSm),
                  const SizedBox(height: 4),
                  Wrap(
                    spacing: 6,
                    children: [
                      if (trip.formattedDateRange != null)
                        Icon(Icons.calendar_today_outlined, size: 13, color: AppColors.textMuted),
                      if (trip.formattedDateRange != null)
                        Text(trip.formattedDateRange!, style: AppTextStyles.bodySm),
                      Text('•', style: AppTextStyles.bodySm),
                      Icon(Icons.people_outline, size: 13, color: AppColors.textMuted),
                      Text('${trip.travelerCount} Adults', style: AppTextStyles.bodySm),
                    ],
                  ),
                  const SizedBox(height: 12),
                  ElevatedButton(
                    onPressed: () => Navigator.pushNamed(
                      context,
                      '/trip-overview',
                      arguments: trip.id,
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      foregroundColor: Colors.white,
                      minimumSize: const Size.fromHeight(40),
                      shape: const StadiumBorder(),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(actionLabel, style: AppTextStyles.labelMd.copyWith(color: Colors.white)),
                        const SizedBox(width: 6),
                        const Icon(Icons.arrow_forward, size: 15, color: Colors.white),
                      ],
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

class _Pill extends StatelessWidget {
  const _Pill({required this.bg, required this.fg, required this.label});

  final Color bg;
  final Color fg;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(999)),
      child: Text(
        label,
        style: AppTextStyles.labelSm.copyWith(color: fg, fontWeight: FontWeight.w700),
      ),
    );
  }
}
