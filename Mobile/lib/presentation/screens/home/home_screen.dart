import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/repositories/mock_home_repository.dart';
import '../../../data/repositories/widgets/home_active_trip_card.dart';
import '../../../data/repositories/widgets/home_region_card.dart';
import '../../providers/home_provider.dart';
import '../../widgets/empty_state.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => HomeProvider(
        repository: MockHomeRepository(),
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
              return const Center(
                child: CircularProgressIndicator(),
              );
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
                      SliverToBoxAdapter(
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
                          child: _GreetingSection(),
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
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text(
                                    'Trip planning will be connected next.',
                                  ),
                                ),
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
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text(
                                    'Trip overview will be connected next.',
                                  ),
                                ),
                              );
                            },
                          )
                              : const EmptyState(
                            title: 'No trips yet',
                            description:
                            'Plan your first trip and start building your journey.',
                            icon: Icons.luggage_outlined,
                            actionLabel: 'Plan Your First Trip',
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
                            children: const [
                              HomeRegionCard(
                                name: 'Japan',
                                imageAsset:
                                'assets/images/home_japan.jpg',
                                badge: 'Top Pick',
                                guides: '14 Guides',
                                description:
                                'Culture, Gastronomy, Rail',
                              ),
                              SizedBox(width: 10),
                              HomeRegionCard(
                                name: 'Italy',
                                imageAsset:
                                'assets/images/home_italy.jpg',
                                badge: 'Scenic',
                                guides: '18 Guides',
                                description:
                                'Coastal, History, Wine',
                              ),
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
                          child: _UnderstoodBanner(),
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

      bottomNavigationBar: const _HomeBottomNavigation(),
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
          Container(
            width: 30,
            height: 30,
            decoration: const BoxDecoration(
              color: AppColors.primary,
              shape: BoxShape.circle,
            ),
            alignment: Alignment.center,
            child: const Text(
              'A',
              style: TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w700,
                fontSize: 13,
              ),
            ),
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
            children: const [
              TextSpan(
                text: 'Where to next, ',
              ),
              TextSpan(
                text: 'Alex?',
                style: TextStyle(
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

class _HomeBottomNavigation extends StatelessWidget {
  const _HomeBottomNavigation();

  @override
  Widget build(BuildContext context) {
    return BottomAppBar(
      color: Colors.white,
      elevation: 10,
      child: SizedBox(
        height: 62,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceAround,
          children: [
            _NavItem(
              icon: Icons.home_outlined,
              label: 'Home',
              selected: true,
              onTap: () {},
            ),

            _NavItem(
              icon: Icons.luggage_outlined,
              label: 'My Trips',
              onTap: () {},
            ),

            _CreateNavButton(
              onTap: () {},
            ),

            _NavItem(
              icon: Icons.person_outline,
              label: 'Profile',
              onTap: () {},
            ),
          ],
        ),
      ),
    );
  }
}

class _CreateNavButton extends StatefulWidget {
  const _CreateNavButton({
    required this.onTap,
  });

  final VoidCallback onTap;

  @override
  State<_CreateNavButton> createState() => _CreateNavButtonState();
}

class _CreateNavButtonState extends State<_CreateNavButton> {
  bool _pressed = false;

  void _handleTap() {
    setState(() {
      _pressed = true;
    });

    Future.delayed(
      const Duration(milliseconds: 110),
          () {
        if (!mounted) return;

        setState(() {
          _pressed = false;
        });
      },
    );

    widget.onTap();
  }

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: _handleTap,
      borderRadius: BorderRadius.circular(16),
      child: SizedBox(
        width: 58,
        height: 62,
        child: Center(
          child: AnimatedScale(
            scale: _pressed ? 0.90 : 1.0,
            duration: const Duration(milliseconds: 110),
            curve: Curves.easeOut,
            child: Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: AppColors.primary,
                borderRadius: BorderRadius.circular(14),
                boxShadow: const [
                  BoxShadow(
                    blurRadius: 8,
                    offset: Offset(0, 3),
                    color: Color(0x22000000),
                  ),
                ],
              ),
              child: const Icon(
                Icons.add,
                color: Colors.white,
                size: 25,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.icon,
    required this.label,
    this.selected = false,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = selected
        ? AppColors.primary
        : AppColors.secondary;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: 12,
          vertical: 6,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              icon,
              size: 20,
              color: color,
            ),
            const SizedBox(height: 2),
            Text(
              label,
              style: AppTextStyles.labelSm.copyWith(
                color: color,
                fontWeight: selected
                    ? FontWeight.w700
                    : FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }
}