import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_client.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/repositories/api_home_repository.dart';
import '../../../data/repositories/widgets/home_active_trip_card.dart';
import '../../../data/repositories/widgets/home_region_card.dart';
import '../../providers/auth_provider.dart';
import '../../providers/home_provider.dart';
import '../../widgets/app_bottom_navigation.dart';
import '../../widgets/empty_state.dart';
import '../../widgets/loading_skeleton.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => HomeProvider(
        repository: ApiHomeRepository(apiClient: context.read<ApiClient>()),
      )..loadHome(),
      child: const _HomeView(),
    );
  }
}

class _HomeView extends StatefulWidget {
  const _HomeView();

  @override
  State<_HomeView> createState() => _HomeViewState();
}

class _HomeViewState extends State<_HomeView>
    with SingleTickerProviderStateMixin {
  late final AnimationController _animationController;
  late final Animation<double> _fadeAnimation;
  late final Animation<Offset> _slideAnimation;

  @override
  void initState() {
    super.initState();

    _animationController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 450),
    );

    _fadeAnimation = CurvedAnimation(
      parent: _animationController,
      curve: Curves.easeOut,
    );

    _slideAnimation = Tween<Offset>(
      begin: const Offset(0, 0.04),
      end: Offset.zero,
    ).animate(
      CurvedAnimation(
        parent: _animationController,
        curve: Curves.easeOutCubic,
      ),
    );

    _animationController.forward();
  }

  @override
  void dispose() {
    _animationController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: Consumer<HomeProvider>(
          builder: (context, provider, _) {
            if (provider.status == HomeStatus.loading) {
              return const _HomeLoadingSkeleton();
            }

            if (provider.status == HomeStatus.failure) {
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
                        onPressed: provider.loadHome,
                        child: const Text('Try again'),
                      ),
                    ],
                  ),
                ),
              );
            }

            return FadeTransition(
              opacity: _fadeAnimation,
              child: SlideTransition(
                position: _slideAnimation,
                child: RefreshIndicator(
                  onRefresh: provider.loadHome,
                  child: CustomScrollView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    slivers: [
                      const SliverToBoxAdapter(
                        child: _HomeTopBar(),
                      ),

                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(
                            16,
                            8,
                            16,
                            0,
                          ),
                          child: const _GreetingSection(),
                        ),
                      ),

                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(
                            16,
                            16,
                            16,
                            0,
                          ),
                          child: _PlanTripSection(
                            onPressed: () {
                              Navigator.pushNamed(
                                context,
                                '/create-trip',
                              );
                            },
                          ),
                        ),
                      ),

                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(
                            16,
                            18,
                            16,
                            0,
                          ),
                          child: _SectionHeader(
                            title: 'Active Trip',
                            trailing: provider.hasRecentTrip
                                ? 'View timeline'
                                : null,
                          ),
                        ),
                      ),

                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(
                            16,
                            8,
                            16,
                            0,
                          ),
                          child: provider.hasRecentTrip
                              ? HomeActiveTripCard(
                            trip: provider.recentTrips.first,
                            onResume: () {
                              // "Resume" opens the trip itself (Trip
                              // Overview), not the creation wizard — it was
                              // wired to /create-trip before Trip Overview
                              // existed.
                              Navigator.pushNamed(
                                context,
                                '/trip-overview',
                                arguments: provider.recentTrips.first.id,
                              );
                            },
                          )
                              : EmptyState(
                            title: 'No trips yet',
                            description:
                            'Plan your first trip and start building your journey.',
                            icon: Icons.luggage_outlined,
                            actionLabel: 'Plan Your First Trip',
                            onAction: () {
                              Navigator.pushNamed(
                                context,
                                '/create-trip',
                              );
                            },
                          ),
                        ),
                      ),

                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(
                            16,
                            18,
                            16,
                            0,
                          ),
                          child: _SectionHeader(
                            title: 'Curated Regions',
                            subtitle:
                            'Verified destination guides ready for immediate AI tailoring.',
                          ),
                        ),
                      ),

                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(
                            16,
                            10,
                            16,
                            0,
                          ),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              for (final region in provider.regions) ...[
                                Expanded(
                                  child: HomeRegionCard(
                                    name: region.name,
                                    imageAsset: region.imageAsset,
                                    badge: region.country,
                                    description: region.description,
                                  ),
                                ),
                                if (region != provider.regions.last)
                                  const SizedBox(width: 10),
                              ],
                            ],
                          ),
                        ),
                      ),

                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(
                            16,
                            18,
                            16,
                            28,
                          ),
                          child: const _UnderstoodBanner(),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            );
          },
        ),
      ),
      bottomNavigationBar: const AppBottomNavigation(selected: AppNavTab.home),
    );
  }
}

/// Mirrors Home's actual layout (08_SYSTEM_DESIGN.md §34: "skeletons when
/// content structure is known" — avoid a bare spinner when we do).
class _HomeLoadingSkeleton extends StatelessWidget {
  const _HomeLoadingSkeleton();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.fromLTRB(16, 10, 16, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          LoadingSkeleton(height: 24, width: 220, borderRadius: 6),
          SizedBox(height: 24),
          LoadingSkeleton(height: 160, borderRadius: 24),
          SizedBox(height: 20),
          LoadingSkeleton(height: 18, width: 140, borderRadius: 6),
          SizedBox(height: 10),
          LoadingSkeleton(height: 130, borderRadius: 20),
        ],
      ),
    );
  }
}

class _HomeTopBar extends StatelessWidget {
  const _HomeTopBar();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
      child: Row(
        children: [
          Image.asset(
            'assets/images/Triply Logo.png',
            width: 24,
            height: 24,
          ),
          const SizedBox(width: 8),
          Text(
            'Triply',
            style: AppTextStyles.labelLg.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
          const Spacer(),
          IconButton(
            tooltip: 'Notifications',
            onPressed: () {},
            icon: const Icon(
              Icons.notifications_none_outlined,
              size: 21,
            ),
          ),
          Builder(
            builder: (context) {
              final name = context.watch<AuthProvider>().user?.name ?? '';
              final initial = name.isNotEmpty ? name[0].toUpperCase() : '?';
              return Container(
                width: 30,
                height: 30,
                decoration: const BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
                alignment: Alignment.center,
                child: Text(
                  initial,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: 13,
                  ),
                ),
              );
            },
          ),
        ],
      ),
    );
  }
}

class _GreetingSection extends StatelessWidget {
  const _GreetingSection();

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final firstName = (user?.name ?? '').split(' ').first;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          padding: const EdgeInsets.symmetric(
            horizontal: 9,
            vertical: 5,
          ),
          decoration: BoxDecoration(
            color: AppColors.surfaceContainer,
            borderRadius: BorderRadius.circular(999),
          ),
          child: Text(
            '✈ AI TRAVEL COMPANION',
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.secondary,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
        const SizedBox(height: 8),
        RichText(
          text: TextSpan(
            style: AppTextStyles.headlineLg.copyWith(
              fontSize: 26,
            ),
            children: [
              const TextSpan(
                text: 'Where to next, ',
              ),
              TextSpan(
                text: firstName.isEmpty ? 'there?' : '$firstName?',
                style: const TextStyle(
                  color: AppColors.primary,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 4),
        Text(
          'Plan your perfect itinerary in seconds with mindful AI curation.',
          style: AppTextStyles.bodySm,
        ),
      ],
    );
  }
}

class _PlanTripSection extends StatelessWidget {
  const _PlanTripSection({
    required this.onPressed,
  });

  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(24),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: 9,
              vertical: 5,
            ),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text(
              '● Intelligent Multi-stop Ready',
              style: AppTextStyles.labelSm.copyWith(
                color: AppColors.secondary,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          const SizedBox(height: 9),
          Text(
            'Design a Journey',
            style: AppTextStyles.headlineMd,
          ),
          const SizedBox(height: 4),
          Text(
            'Start effortlessly by selecting your dream destination or establishing a budget first.',
            style: AppTextStyles.bodySm,
          ),
          const SizedBox(height: 12),
          Row(
            children: const [
              Expanded(
                child: _PlanningOption(
                  icon: Icons.near_me_outlined,
                  title: 'Destination',
                  subtitle: 'Pick places',
                ),
              ),
              SizedBox(width: 8),
              Expanded(
                child: _PlanningOption(
                  icon: Icons.account_balance_wallet_outlined,
                  title: 'Budget-first',
                  subtitle: 'Optimized spend',
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          ElevatedButton(
            onPressed: onPressed,
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.primary,
              foregroundColor: Colors.white,
              elevation: 2,
              minimumSize: const Size.fromHeight(46),
              shape: const StadiumBorder(),
            ),
            child: const Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text(
                  'Start AI Generator',
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                SizedBox(width: 8),
                Icon(
                  Icons.arrow_forward,
                  size: 17,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _PlanningOption extends StatelessWidget {
  const _PlanningOption({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  final IconData icon;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 9,
        vertical: 8,
      ),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          Icon(
            icon,
            size: 17,
            color: AppColors.secondary,
          ),
          const SizedBox(width: 7),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.onSurface,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                Text(
                  subtitle,
                  style: AppTextStyles.labelSm,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({
    required this.title,
    this.subtitle,
    this.trailing,
  });

  final String title;
  final String? subtitle;
  final String? trailing;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: AppTextStyles.labelLg,
              ),
              if (subtitle != null) ...[
                const SizedBox(height: 2),
                Text(
                  subtitle!,
                  style: AppTextStyles.labelSm,
                ),
              ],
            ],
          ),
        ),
        if (trailing != null)
          Text(
            trailing!,
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.primary,
              fontWeight: FontWeight.w700,
            ),
          ),
      ],
    );
  }
}

class _UnderstoodBanner extends StatelessWidget {
  const _UnderstoodBanner();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        children: [
          Container(
            width: 34,
            height: 34,
            decoration: const BoxDecoration(
              color: AppColors.primaryContainerLight,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.auto_awesome,
              color: AppColors.primary,
              size: 18,
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: RichText(
              text: TextSpan(
                style: AppTextStyles.bodySm,
                children: const [
                  TextSpan(
                    text: 'Uncluttered Wanderlust\n',
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      color: AppColors.onSurface,
                    ),
                  ),
                  TextSpan(
                    text:
                    'Zero algorithmic ads or sponsored stays. Only tailored routes on your rhythm.',
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

