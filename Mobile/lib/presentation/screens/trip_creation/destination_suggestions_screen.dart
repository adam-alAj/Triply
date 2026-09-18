import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../providers/trip_creation_provider.dart';
import '../../widgets/error_state.dart';
import '../../widgets/primary_button.dart';

class DestinationSuggestionsScreen extends StatelessWidget {
  const DestinationSuggestionsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();

    final suggestions = provider.suggestions;

    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: Column(
          children: [
            const _Header(),

            Expanded(
              child: provider.status == TripCreationStatus.failure
                  ? ErrorState(
                      title: "Couldn't load suggestions",
                      description: provider.errorMessage ??
                          'Something went wrong. Please try again.',
                      onAction: provider.loadSuggestions,
                    )
                  : suggestions.isEmpty
                  ? const _EmptySuggestions()
                  : ListView(
                padding: const EdgeInsets.fromLTRB(
                  20,
                  8,
                  20,
                  24,
                ),
                children: [
                  const _SmartCurationBadge(),

                  const SizedBox(height: 18),

                  Text(
                    'Destinations picked\nfor you',
                    style: AppTextStyles.headlineLg.copyWith(
                      fontSize: 27,
                      height: 1.15,
                    ),
                  ),

                  const SizedBox(height: 10),

                  Text(
                    'Based on your planning preferences, '
                        'here are a few destinations worth exploring.',
                    style: AppTextStyles.bodyMd.copyWith(
                      color: AppColors.secondary,
                      height: 1.45,
                    ),
                  ),

                  const SizedBox(height: 24),

                  ...suggestions.map(
                        (suggestion) => Padding(
                      padding: const EdgeInsets.only(
                        bottom: 16,
                      ),
                      child: _SuggestionCard(
                        suggestion: suggestion,
                      ),
                    ),
                  ),
                ],
              ),
            ),

            _ContinueButton(),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// HEADER
// -----------------------------------------------------------------------------

class _Header extends StatelessWidget {
  const _Header();

  @override
  Widget build(BuildContext context) {
    final provider = context.read<TripCreationProvider>();

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Row(
        children: [
          IconButton(
            onPressed: provider.back,
            icon: const Icon(Icons.arrow_back),
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints(
              minWidth: 40,
              minHeight: 40,
            ),
          ),
          const Spacer(),
          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: 12,
              vertical: 6,
            ),
            decoration: BoxDecoration(
              color: AppColors.surfaceContainerLow,
              borderRadius: BorderRadius.circular(20),
            ),
            child: Text(
              'Step 3 of 5',
              style: AppTextStyles.labelSm.copyWith(
                color: AppColors.secondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// BADGE
// -----------------------------------------------------------------------------

class _SmartCurationBadge extends StatelessWidget {
  const _SmartCurationBadge();

  @override
  Widget build(BuildContext context) {
    return Container(
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
          const Icon(
            Icons.auto_awesome,
            size: 14,
            color: AppColors.primary,
          ),
          const SizedBox(width: 6),
          Text(
            'AI Recommendations',
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.secondary,
            ),
          ),
        ],
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// SUGGESTION CARD
// -----------------------------------------------------------------------------

class _SuggestionCard extends StatelessWidget {
  const _SuggestionCard({
    required this.suggestion,
  });

  final Map<String, dynamic> suggestion;

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();

    final destinationId = suggestion['destinationId'] as int?;
    final name = suggestion['name'] as String;
    final country = suggestion['country'] as String;
    final description = suggestion['description'] as String;
    final image = suggestion['image'] as String;
    final estimatedCost = suggestion['estimatedCost'] as String;

    final selected = provider.data.destination == name;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: () {
          provider.selectDestination(
            name: name,
            country: country,
            destinationId: destinationId,
          );
        },
        borderRadius: BorderRadius.circular(24),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          clipBehavior: Clip.antiAlias,
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(24),
            border: Border.all(
              color: selected
                  ? AppColors.primary
                  : AppColors.surfaceContainer,
              width: selected ? 1.5 : 1,
            ),
            boxShadow: const [
              BoxShadow(
                blurRadius: 10,
                offset: Offset(0, 4),
                color: Color(0x10000000),
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(
                height: 170,
                width: double.infinity,
                child: Stack(
                  fit: StackFit.expand,
                  children: [
                    Image.asset(
                      image,
                      fit: BoxFit.cover,
                      errorBuilder: (_, __, ___) {
                        return Container(
                          color: AppColors.surfaceContainer,
                          child: const Icon(
                            Icons.image_not_supported_outlined,
                            size: 40,
                            color: AppColors.secondary,
                          ),
                        );
                      },
                    ),

                    Positioned(
                      top: 12,
                      right: 12,
                      child: Container(
                        width: 28,
                        height: 28,
                        decoration: BoxDecoration(
                          color: Colors.white.withOpacity(0.92),
                          shape: BoxShape.circle,
                        ),
                        child: selected
                            ? const Icon(
                          Icons.check,
                          size: 17,
                          color: AppColors.primary,
                        )
                            : const Icon(
                          Icons.favorite_border,
                          size: 17,
                          color: AppColors.secondary,
                        ),
                      ),
                    ),

                    Positioned(
                      left: 12,
                      bottom: 12,
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 9,
                          vertical: 5,
                        ),
                        decoration: BoxDecoration(
                          color: Colors.black.withOpacity(0.58),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Text(
                          estimatedCost,
                          style: AppTextStyles.labelSm.copyWith(
                            color: Colors.white,
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),

              Padding(
                padding: const EdgeInsets.fromLTRB(
                  15,
                  14,
                  15,
                  16,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      name,
                      style: AppTextStyles.headlineSm.copyWith(
                        fontSize: 18,
                      ),
                    ),

                    const SizedBox(height: 3),

                    Text(
                      country,
                      style: AppTextStyles.labelSm.copyWith(
                        color: AppColors.secondary,
                      ),
                    ),

                    const SizedBox(height: 8),

                    Text(
                      description,
                      style: AppTextStyles.bodySm.copyWith(
                        color: AppColors.secondary,
                        height: 1.35,
                      ),
                    ),

                    const SizedBox(height: 12),

                    Row(
                      children: [
                        const Icon(
                          Icons.auto_awesome,
                          size: 14,
                          color: AppColors.primary,
                        ),
                        const SizedBox(width: 5),
                        Expanded(
                          child: Text(
                            'Curated for your journey',
                            style: AppTextStyles.labelSm.copyWith(
                              color: AppColors.primary,
                            ),
                          ),
                        ),
                        Icon(
                          selected
                              ? Icons.check_circle
                              : Icons.arrow_forward,
                          size: 19,
                          color: selected
                              ? AppColors.primary
                              : AppColors.secondary,
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// EMPTY
// -----------------------------------------------------------------------------

class _EmptySuggestions extends StatelessWidget {
  const _EmptySuggestions();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(30),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(
              Icons.travel_explore_outlined,
              size: 48,
              color: AppColors.secondary,
            ),
            const SizedBox(height: 16),
            Text(
              'No suggestions available',
              style: AppTextStyles.headlineSm,
            ),
            const SizedBox(height: 8),
            Text(
              'We could not find destinations for these preferences.',
              textAlign: TextAlign.center,
              style: AppTextStyles.bodyMd.copyWith(
                color: AppColors.secondary,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// CONTINUE
// -----------------------------------------------------------------------------

class _ContinueButton extends StatelessWidget {
  const _ContinueButton();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();

    final enabled = provider.data.destination != null;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: PrimaryButton(
        label: 'Continue',
        icon: Icons.arrow_forward,
        onPressed: enabled
            ? () async {
          await provider.next();
        }
            : () {},
        fullWidth: true,
      ),
    );
  }
}