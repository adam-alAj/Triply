import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../core/validation/trip_validators.dart';
import '../../providers/trip_creation_provider.dart';
import '../../widgets/app_scaffold.dart';
import '../../widgets/primary_button.dart';

class ReviewScreen extends StatelessWidget {
  const ReviewScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();

    return AppScaffold(
      body: Column(
        children: [
          _buildHeader(provider),

          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _buildSuccessBadge(),

                  const SizedBox(height: 12),

                  Text(
                    'Review your trip',
                    style: AppTextStyles.headlineLg.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),

                  const SizedBox(height: 5),

                  Text(
                    'Everything looks good? Triply will use these '
                        'preferences to create your personalized itinerary.',
                    style: AppTextStyles.bodySm.copyWith(
                      color: AppColors.textMuted,
                    ),
                  ),

                  const SizedBox(height: 18),

                  _buildDestinationSection(context, provider),

                  const SizedBox(height: 12),

                  _buildTravelSection(context, provider),

                  const SizedBox(height: 12),

                  _buildBudgetSection(context, provider),

                  const SizedBox(height: 12),

                  _buildInterestsSection(context, provider),

                  const SizedBox(height: 16),

                  _buildAiCard(),

                  const SizedBox(height: 20),

                  PrimaryButton(
                    label: 'Generate My Trip',
                    fullWidth: true,
                    onPressed: () => _generate(context, provider),
                  ),

                  const SizedBox(height: 8),

                  Center(
                    child: Text(
                      'You can regenerate or edit your itinerary later',
                      style: AppTextStyles.labelSm.copyWith(
                        color: AppColors.textMuted,
                      ),
                      textAlign: TextAlign.center,
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

  /// Final gate before submission (this task's acceptance criteria): catches
  /// anything that slipped through the per-step checks — e.g. a step was
  /// jumped to directly, or state was restored mid-flow — with the same
  /// backend-consistent messaging used everywhere else.
  void _generate(BuildContext context, TripCreationProvider provider) {
    final errors = TripValidators.validateAll(provider.data);

    if (errors.isNotEmpty) {
      showDialog<void>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          title: const Text('Please fix the following'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (final error in errors)
                Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Text('•  $error'),
                ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('OK'),
            ),
          ],
        ),
      );
      return;
    }

    provider.goToGenerating();
  }

  Widget _buildHeader(TripCreationProvider provider) {
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
                'Step 5 of 5',
                style: AppTextStyles.labelSm.copyWith(
                  color: AppColors.secondary,
                  fontWeight: FontWeight.w600,
                ),
              ),

              const Spacer(),

              Text(
                'Ready to plan',
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
              value: 1,
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

  Widget _buildSuccessBadge() {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: 7,
      ),
      decoration: BoxDecoration(
        color: AppColors.primaryContainerLight.withValues(alpha: 0.55),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(
            Icons.auto_awesome_rounded,
            color: AppColors.primary,
            size: 16,
          ),
          const SizedBox(width: 6),
          Text(
            'AI trip planning ready',
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.primary,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDestinationSection(
      BuildContext context,
      TripCreationProvider provider,
      ) {
    final data = provider.data;

    return _ReviewCard(
      icon: Icons.location_on_outlined,
      title: 'Destination',
      onEdit: () {
        provider.jumpToDestinationStep();
      },
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            data.destination ?? 'Not selected',
            style: AppTextStyles.headlineSm.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
          if (data.destinationCountry != null) ...[
            const SizedBox(height: 2),
            Text(
              data.destinationCountry!,
              style: AppTextStyles.bodySm.copyWith(
                color: AppColors.textMuted,
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildTravelSection(
      BuildContext context,
      TripCreationProvider provider,
      ) {
    final data = provider.data;

    return _ReviewCard(
      icon: Icons.calendar_month_outlined,
      title: 'Travel Details',
      onEdit: () {
        provider.jumpToTripDetailsStep();
      },
      child: Column(
        children: [
          _InfoRow(
            icon: Icons.date_range_outlined,
            label: 'Dates',
            value: _dateRange(data.startDate, data.endDate),
          ),

          const SizedBox(height: 10),

          _InfoRow(
            icon: Icons.groups_outlined,
            label: 'Travelers',
            value: '${data.travelers}',
          ),
        ],
      ),
    );
  }

  Widget _buildBudgetSection(
      BuildContext context,
      TripCreationProvider provider,
      ) {
    final data = provider.data;

    return _ReviewCard(
      icon: Icons.account_balance_wallet_outlined,
      title: 'Budget',
      onEdit: () {
        provider.jumpToTripDetailsStep();
      },
      child: _InfoRow(
        icon: Icons.payments_outlined,
        label: 'Target Budget',
        value: data.budget == null
            ? 'Not specified'
            : '\$${data.budget!.toStringAsFixed(0)}',
      ),
    );
  }

  Widget _buildInterestsSection(
      BuildContext context,
      TripCreationProvider provider,
      ) {
    final interests = provider.data.interests;

    return _ReviewCard(
      icon: Icons.favorite_border_rounded,
      title: 'Interests',
      onEdit: () {
        provider.jumpToInterestsStep();
      },
      child: interests.isEmpty
          ? Text(
        'No interests selected',
        style: AppTextStyles.bodySm.copyWith(
          color: AppColors.textMuted,
        ),
      )
          : Wrap(
        spacing: 7,
        runSpacing: 7,
        children: interests.map((interest) {
          return Container(
            padding: const EdgeInsets.symmetric(
              horizontal: 10,
              vertical: 6,
            ),
            decoration: BoxDecoration(
              color: AppColors.surfaceContainerLow,
              borderRadius: BorderRadius.circular(20),
            ),
            child: Text(
              interest,
              style: AppTextStyles.labelSm.copyWith(
                fontWeight: FontWeight.w600,
              ),
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _buildAiCard() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(18),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: AppColors.primaryContainerLight,
              borderRadius: BorderRadius.circular(11),
            ),
            child: const Icon(
              Icons.auto_awesome_rounded,
              color: AppColors.primary,
              size: 19,
            ),
          ),

          const SizedBox(width: 10),

          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'What happens next?',
                  style: AppTextStyles.labelMd.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),

                const SizedBox(height: 3),

                Text(
                  'Triply will balance your dates, budget, '
                      'travelers, and interests to build your itinerary.',
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

  String _dateRange(
      DateTime? start,
      DateTime? end,
      ) {
    if (start == null || end == null) {
      return 'Not selected';
    }

    return '${_shortDate(start)} – ${_shortDate(end)}';
  }

  String _shortDate(DateTime date) {
    const months = [
      'Jan',
      'Feb',
      'Mar',
      'Apr',
      'May',
      'Jun',
      'Jul',
      'Aug',
      'Sep',
      'Oct',
      'Nov',
      'Dec',
    ];

    return '${months[date.month - 1]} ${date.day}, ${date.year}';
  }
}

class _ReviewCard extends StatelessWidget {
  const _ReviewCard({
    required this.icon,
    required this.title,
    required this.onEdit,
    required this.child,
  });

  final IconData icon;
  final String title;
  final VoidCallback onEdit;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(13),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLowest,
        borderRadius: BorderRadius.circular(18),
        boxShadow: [
          BoxShadow(
            color: AppColors.shadowAmbient,
            blurRadius: 10,
            offset: const Offset(0, 3),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 31,
                height: 31,
                decoration: BoxDecoration(
                  color: AppColors.surfaceContainerLow,
                  borderRadius: BorderRadius.circular(9),
                ),
                child: Icon(
                  icon,
                  size: 17,
                  color: AppColors.secondary,
                ),
              ),

              const SizedBox(width: 8),

              Expanded(
                child: Text(
                  title,
                  style: AppTextStyles.labelLg.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),

              TextButton(
                onPressed: onEdit,
                style: TextButton.styleFrom(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 4,
                  ),
                  minimumSize: Size.zero,
                  tapTargetSize:
                  MaterialTapTargetSize.shrinkWrap,
                ),
                child: Text(
                  'Edit',
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: 11),

          child,
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(
          icon,
          size: 16,
          color: AppColors.secondary,
        ),

        const SizedBox(width: 8),

        Text(
          label,
          style: AppTextStyles.bodySm.copyWith(
            color: AppColors.textMuted,
          ),
        ),

        const Spacer(),

        Flexible(
          child: Text(
            value,
            textAlign: TextAlign.end,
            style: AppTextStyles.labelMd.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ],
    );
  }
}
