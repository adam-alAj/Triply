import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../data/models/trip_creation_data.dart';
import '../../providers/trip_creation_provider.dart';

import '../../widgets/primary_button.dart';

class PlanningModeScreen extends StatelessWidget {
  const PlanningModeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();
    final selectedMode = provider.data.planningMode;

    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: Column(
          children: [
            _TopBar(),

            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(20, 8, 20, 20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const _ProgressHeader(),

                    const SizedBox(height: 20),

                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 7,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.surfaceContainerLow,
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            Icons.auto_awesome,
                            size: 14,
                            color: AppColors.primary,
                          ),
                          const SizedBox(width: 6),
                          Text(
                            'Smart Curation',
                            style: AppTextStyles.labelSm.copyWith(
                              color: AppColors.secondary,
                            ),
                          ),
                        ],
                      ),
                    ),

                    const SizedBox(height: 12),

                    Text(
                      'How would you like to plan\nyour trip?',
                      style: AppTextStyles.headlineLg.copyWith(
                        fontSize: 25,
                        height: 1.15,
                      ),
                    ),

                    const SizedBox(height: 10),

                    Text(
                      'Choose a planning strategy. Triply adapts its '
                          'AI curation engine based on your starting point.',
                      style: AppTextStyles.bodyMd.copyWith(
                        color: AppColors.secondary,
                        height: 1.45,
                      ),
                    ),

                    const SizedBox(height: 24),

                    _PlanningOptionCard(
                      selected:
                      selectedMode == PlanningMode.destinationFirst,
                      icon: Icons.explore,
                      iconBackground: AppColors.primaryContainerLight,
                      badge: 'Popular choice',
                      title: 'I know where I want to go',
                      description:
                      'Pick a verified destination and we’ll craft an '
                          'itinerary tailored to your dates, rhythm, and passions.',
                      footer: 'Includes smart transit routing & local gems',
                      onTap: () {
                        provider.selectPlanningMode(
                          PlanningMode.destinationFirst,
                        );
                      },
                    ),

                    const SizedBox(height: 14),

                    _PlanningOptionCard(
                      selected:
                      selectedMode == PlanningMode.budgetFirst,
                      icon: Icons.account_balance_wallet_outlined,
                      iconBackground: const Color(0xFFD9E7FF),
                      badge: 'Smart discovery',
                      title: 'Help me discover where to go',
                      description:
                      'Set your spending limit and travel vibe, and '
                          'Triply will suggest optimal destinations matching your wallet.',
                      footer: 'Flight + stay cost dynamic estimates',
                      onTap: () {
                        provider.selectPlanningMode(
                          PlanningMode.budgetFirst,
                        );
                      },
                    ),

                    const SizedBox(height: 14),

                    _FlexibleDesignCard(),

                    const SizedBox(height: 18),
                  ],
                ),
              ),
            ),

            _BottomAction(
              enabled: selectedMode != null,
              onPressed: selectedMode == null
                  ? null
                  : () async {
                await provider.next();
              },
            ),

            const SizedBox(height: 8),

            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 28),
              child: Text(
                'Planning mode cannot be changed once generation starts.',
                textAlign: TextAlign.center,
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.textMuted,
                ),
              ),
            ),

            const SizedBox(height: 10),

            _WizardBottomNavigation(),
          ],
        ),
      ),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16),
        child: Row(
          children: [
            Container(
              width: 22,
              height: 22,
              decoration: BoxDecoration(
                color: AppColors.surfaceContainerLow,
                borderRadius: BorderRadius.circular(6),
              ),
              child: const Icon(
                Icons.flight_takeoff,
                size: 14,
                color: AppColors.primary,
              ),
            ),
            const SizedBox(width: 7),
            Text(
              'Triply',
              style: AppTextStyles.labelLg.copyWith(
                fontWeight: FontWeight.w700,
                color: AppColors.onSurface,
              ),
            ),
            const Spacer(),
            IconButton(
              onPressed: () {},
              icon: const Icon(
                Icons.notifications_none_rounded,
                size: 22,
              ),
              color: AppColors.secondary,
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints(
                minWidth: 38,
                minHeight: 38,
              ),
            ),
            const SizedBox(width: 4),
            Container(
              width: 30,
              height: 30,
              decoration: const BoxDecoration(
                color: AppColors.primary,
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.person_outline,
                color: Colors.white,
                size: 17,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ProgressHeader extends StatelessWidget {
  const _ProgressHeader();

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        IconButton(
          onPressed: () {
            Navigator.pop(context);
          },
          icon: const Icon(Icons.arrow_back),
          padding: EdgeInsets.zero,
          constraints: const BoxConstraints(
            minWidth: 36,
            minHeight: 36,
          ),
        ),
        const Spacer(),
        Container(
          padding: const EdgeInsets.symmetric(
            horizontal: 12,
            vertical: 5,
          ),
          decoration: BoxDecoration(
            color: AppColors.surfaceContainerLow,
            borderRadius: BorderRadius.circular(20),
          ),
          child: Text(
            'Step 1 of 5',
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.secondary,
            ),
          ),
        ),
      ],
    );
  }
}

class _PlanningOptionCard extends StatelessWidget {
  const _PlanningOptionCard({
    required this.selected,
    required this.icon,
    required this.iconBackground,
    required this.badge,
    required this.title,
    required this.description,
    required this.footer,
    required this.onTap,
  });

  final bool selected;
  final IconData icon;
  final Color iconBackground;
  final String badge;
  final String title;
  final String description;
  final String footer;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(24),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          padding: const EdgeInsets.fromLTRB(14, 14, 14, 15),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(24),
            border: Border.all(
              color: selected
                  ? AppColors.primary
                  : AppColors.surfaceContainer,
              width: selected ? 1.4 : 1,
            ),
            boxShadow: selected
                ? const [
              BoxShadow(
                blurRadius: 5,
                offset: Offset(0, 2),
                color: Color(0x14000000),
              ),
            ]
                : null,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    width: 34,
                    height: 34,
                    decoration: BoxDecoration(
                      color: iconBackground,
                      shape: BoxShape.circle,
                    ),
                    child: Icon(
                      icon,
                      size: 17,
                      color: AppColors.primary,
                    ),
                  ),
                  const Spacer(),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.surfaceContainerLow,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Text(
                      badge,
                      style: AppTextStyles.labelSm.copyWith(
                        fontSize: 9,
                        color: AppColors.secondary,
                      ),
                    ),
                  ),
                  const SizedBox(width: 7),
                  AnimatedContainer(
                    duration: const Duration(milliseconds: 180),
                    width: 20,
                    height: 20,
                    decoration: BoxDecoration(
                      color: selected
                          ? AppColors.primary
                          : AppColors.surfaceContainer,
                      shape: BoxShape.circle,
                    ),
                    child: selected
                        ? const Icon(
                      Icons.check,
                      color: Colors.white,
                      size: 13,
                    )
                        : null,
                  ),
                ],
              ),

              const SizedBox(height: 18),

              Text(
                title,
                style: AppTextStyles.headlineSm.copyWith(
                  fontSize: 16,
                  color: AppColors.onSurface,
                ),
              ),

              const SizedBox(height: 6),

              Text(
                description,
                style: AppTextStyles.bodySm.copyWith(
                  color: AppColors.secondary,
                  height: 1.35,
                ),
              ),

              const SizedBox(height: 14),

              Row(
                children: [
                  Icon(
                    Icons.near_me_outlined,
                    size: 13,
                    color: AppColors.secondary,
                  ),
                  const SizedBox(width: 5),
                  Expanded(
                    child: Text(
                      footer,
                      style: AppTextStyles.labelSm.copyWith(
                        color: AppColors.secondary,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FlexibleDesignCard extends StatelessWidget {
  const _FlexibleDesignCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(4),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 30,
            height: 30,
            decoration: const BoxDecoration(
              color: Colors.white,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.location_on_outlined,
              color: AppColors.primary,
              size: 17,
            ),
          ),
          const SizedBox(width: 9),
          Expanded(
            child: RichText(
              text: TextSpan(
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.secondary,
                  height: 1.35,
                ),
                children: [
                  TextSpan(
                    text: 'Flexible by design\n',
                    style: AppTextStyles.labelMd.copyWith(
                      color: AppColors.onSurface,
                    ),
                  ),
                  const TextSpan(
                    text:
                    'Already saved spots on Instagram or Maps? '
                        'You can import them effortlessly in the next step.',
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

class _BottomAction extends StatelessWidget {
  const _BottomAction({
    required this.enabled,
    required this.onPressed,
  });

  final bool enabled;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: PrimaryButton(
        label: 'Continue',
        icon: Icons.arrow_forward,
        onPressed: onPressed ?? () {},
        fullWidth: true,
      ),
    );
  }
}

class _WizardBottomNavigation extends StatelessWidget {
  const _WizardBottomNavigation();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 62,
      child: Row(
        children: [
          Expanded(
            child: _NavItem(
              icon: Icons.explore_outlined,
              label: 'Home',
              selected: true,
            ),
          ),
          Expanded(
            child: _NavItem(
              icon: Icons.luggage_outlined,
              label: 'My Trips',
            ),
          ),
          Expanded(
            child: Center(
              child: Container(
                width: 48,
                height: 48,
                decoration: const BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.add,
                  color: Colors.white,
                  size: 25,
                ),
              ),
            ),
          ),
          Expanded(
            child: _NavItem(
              icon: Icons.person_outline,
              label: 'Profile',
            ),
          ),
        ],
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.icon,
    required this.label,
    this.selected = false,
  });

  final IconData icon;
  final String label;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Icon(
          icon,
          size: 19,
          color: selected
              ? AppColors.primary
              : AppColors.secondary,
        ),
        const SizedBox(height: 3),
        Text(
          label,
          style: AppTextStyles.labelSm.copyWith(
            fontSize: 9,
            color: selected
                ? AppColors.primary
                : AppColors.secondary,
          ),
        ),
      ],
    );
  }
}