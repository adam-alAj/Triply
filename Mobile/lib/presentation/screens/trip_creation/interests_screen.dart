import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../providers/trip_creation_provider.dart';
import '../../widgets/app_scaffold.dart';
import '../../widgets/primary_button.dart';

class InterestsScreen extends StatelessWidget {
  const InterestsScreen({super.key});

  static const List<_InterestOption> _interests = [
    _InterestOption(
      title: 'Culture',
      subtitle: 'Local traditions & arts',
      icon: Icons.museum_outlined,
    ),
    _InterestOption(
      title: 'Food',
      subtitle: 'Local cuisine & cafés',
      icon: Icons.restaurant_outlined,
    ),
    _InterestOption(
      title: 'Nature',
      subtitle: 'Scenery & outdoors',
      icon: Icons.park_outlined,
    ),
    _InterestOption(
      title: 'Adventure',
      subtitle: 'Activities & exploration',
      icon: Icons.landscape_outlined,
    ),
    _InterestOption(
      title: 'History',
      subtitle: 'Landmarks & heritage',
      icon: Icons.account_balance_outlined,
    ),
    _InterestOption(
      title: 'Shopping',
      subtitle: 'Markets & local finds',
      icon: Icons.shopping_bag_outlined,
    ),
    _InterestOption(
      title: 'Relaxation',
      subtitle: 'Slow days & wellness',
      icon: Icons.spa_outlined,
    ),
    _InterestOption(
      title: 'Other',
      subtitle: 'Anything else you enjoy',
      icon: Icons.more_horiz_rounded,
    ),
  ];

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();
    final selectedInterests = provider.data.interests;

    return AppScaffold(
      body: Column(
        children: [
          _buildHeader(context, provider),

          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _buildDestinationSummary(provider),

                  const SizedBox(height: 14),

                  Text(
                    'What do you love experiencing?',
                    style: AppTextStyles.headlineSm.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),

                  const SizedBox(height: 5),

                  Text(
                    'Choose the experiences you enjoy most. '
                        'Triply will use them to personalize your itinerary.',
                    style: AppTextStyles.bodySm.copyWith(
                      color: AppColors.textMuted,
                    ),
                  ),

                  const SizedBox(height: 18),

                  _buildSectionTitle(
                    'Your interests',
                    selectedInterests.length,
                  ),

                  const SizedBox(height: 10),

                  _buildInterestGrid(
                    context,
                    provider,
                    selectedInterests,
                  ),

                  const SizedBox(height: 16),

                  _buildAiInfoCard(),

                  const SizedBox(height: 20),

                  PrimaryButton(
                    label: 'Continue to Review',
                    fullWidth: true,
                    onPressed: selectedInterests.isEmpty
                        ? null
                        : () {
                      provider.next();
                    },
                  ),

                  const SizedBox(height: 8),

                  Center(
                    child: Text(
                      'You can change your interests later',
                      style: AppTextStyles.labelSm.copyWith(
                        color: AppColors.textMuted,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildHeader(
      BuildContext context,
      TripCreationProvider provider,
      ) {
    return Column(
      children: [
        Container(
          height: 52,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          decoration: BoxDecoration(
            color: AppColors.surface,
            border: Border(
              bottom: BorderSide(
                color: AppColors.surfaceContainer,
              ),
            ),
          ),
          child: Row(
            children: [
              IconButton(
                onPressed: provider.back,
                icon: const Icon(
                  Icons.arrow_back_ios_new_rounded,
                ),
                iconSize: 18,
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(
                  minWidth: 32,
                  minHeight: 32,
                ),
              ),

              const SizedBox(width: 4),

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
        ),

        Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
          child: Row(
            children: [
              Text(
                'Step 4 of 5',
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.secondary,
                  fontWeight: FontWeight.w600,
                ),
              ),

              const Spacer(),

              Text(
                '${provider.data.interests.length} selected',
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.textMuted,
                ),
              ),
            ],
          ),
        ),

        Padding(
          padding: const EdgeInsets.fromLTRB(16, 6, 16, 4),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(10),
            child: const LinearProgressIndicator(
              value: 0.80,
              minHeight: 4,
              backgroundColor: AppColors.surfaceContainer,
              valueColor: AlwaysStoppedAnimation<Color>(
                AppColors.primary,
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildDestinationSummary(
      TripCreationProvider provider,
      ) {
    final destination = provider.data.destination;
    final country = provider.data.destinationCountry;

    final text = destination == null
        ? 'Your personalized trip'
        : country == null
        ? destination
        : '$destination, $country';

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: 8,
      ),
      decoration: BoxDecoration(
        color: AppColors.primaryContainerLight.withOpacity(0.45),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        children: [
          const Icon(
            Icons.auto_awesome_rounded,
            color: AppColors.primary,
            size: 16,
          ),

          const SizedBox(width: 7),

          Expanded(
            child: Text(
              text,
              style: AppTextStyles.labelSm.copyWith(
                color: AppColors.primary,
                fontWeight: FontWeight.w600,
              ),
              overflow: TextOverflow.ellipsis,
            ),
          ),

          Text(
            'Personalized for you',
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.secondary,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSectionTitle(
      String title,
      int selectedCount,
      ) {
    return Row(
      children: [
        Text(
          title,
          style: AppTextStyles.labelLg.copyWith(
            fontWeight: FontWeight.w700,
          ),
        ),

        const Spacer(),

        if (selectedCount > 0)
          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: 8,
              vertical: 4,
            ),
            decoration: BoxDecoration(
              color: AppColors.primaryContainerLight,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Text(
              '$selectedCount selected',
              style: AppTextStyles.labelSm.copyWith(
                color: AppColors.primary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
      ],
    );
  }

  Widget _buildInterestGrid(
      BuildContext context,
      TripCreationProvider provider,
      List<String> selectedInterests,
      ) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final isWide = constraints.maxWidth >= 600;

        final crossAxisCount = isWide ? 3 : 2;

        return GridView.builder(
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          itemCount: _interests.length,
          gridDelegate:
          SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: crossAxisCount,
            crossAxisSpacing: 10,
            mainAxisSpacing: 10,
            childAspectRatio: isWide ? 1.8 : 1.35,
          ),
          itemBuilder: (context, index) {
            final interest = _interests[index];

            final isSelected =
            selectedInterests.contains(interest.title);

            return _InterestCard(
              option: interest,
              selected: isSelected,
              onTap: () {
                provider.toggleInterest(interest.title);
              },
            );
          },
        );
      },
    );
  }

  Widget _buildAiInfoCard() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(13),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(18),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 34,
            height: 34,
            decoration: BoxDecoration(
              color: AppColors.primaryContainerLight,
              borderRadius: BorderRadius.circular(10),
            ),
            child: const Icon(
              Icons.auto_awesome_rounded,
              color: AppColors.primary,
              size: 18,
            ),
          ),

          const SizedBox(width: 10),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Smart personalization',
                  style: AppTextStyles.labelMd.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),

                const SizedBox(height: 3),

                Text(
                  'Your interests help Triply balance activities, '
                      'pacing, and recommendations in your itinerary.',
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.textMuted,
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

class _InterestOption {
  const _InterestOption({
    required this.title,
    required this.subtitle,
    required this.icon,
  });

  final String title;
  final String subtitle;
  final IconData icon;
}

class _InterestCard extends StatelessWidget {
  const _InterestCard({
    required this.option,
    required this.selected,
    required this.onTap,
  });

  final _InterestOption option;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          padding: const EdgeInsets.all(11),
          decoration: BoxDecoration(
            color: selected
                ? AppColors.primaryContainerLight
                : AppColors.surfaceContainerLowest,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: selected
                  ? AppColors.primary
                  : AppColors.surfaceContainer,
              width: selected ? 1.5 : 1,
            ),
            boxShadow: selected
                ? null
                : [
              BoxShadow(
                color: AppColors.shadowAmbient,
                blurRadius: 8,
                offset: const Offset(0, 2),
              ),
            ],
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
                      color: selected
                          ? AppColors.primary
                          : AppColors.surfaceContainerLow,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Icon(
                      option.icon,
                      size: 18,
                      color: selected
                          ? AppColors.onPrimary
                          : AppColors.secondary,
                    ),
                  ),

                  const Spacer(),

                  AnimatedSwitcher(
                    duration: const Duration(milliseconds: 150),
                    child: selected
                        ? const Icon(
                      Icons.check_circle_rounded,
                      key: ValueKey('selected'),
                      color: AppColors.primary,
                      size: 20,
                    )
                        : const Icon(
                      Icons.add_circle_outline_rounded,
                      key: ValueKey('unselected'),
                      color: AppColors.textMuted,
                      size: 20,
                    ),
                  ),
                ],
              ),

              const Spacer(),

              Text(
                option.title,
                style: AppTextStyles.labelMd.copyWith(
                  fontWeight: FontWeight.w700,
                  color: selected
                      ? AppColors.primary
                      : AppColors.onSurface,
                ),
              ),

              const SizedBox(height: 2),

              Text(
                option.subtitle,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.textMuted,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}