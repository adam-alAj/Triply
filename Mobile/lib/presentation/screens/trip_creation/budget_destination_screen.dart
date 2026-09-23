import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../../core/validation/trip_validators.dart';
import '../../../data/models/trip_creation_data.dart';
import '../../providers/trip_creation_provider.dart';
import '../../widgets/error_state.dart';
import '../../widgets/loading_skeleton.dart';
import '../../widgets/primary_button.dart';

class BudgetDestinationScreen extends StatelessWidget {
  const BudgetDestinationScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();
    final mode = provider.data.planningMode;

    final isBudgetFirst = mode == PlanningMode.budgetFirst;

    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: Column(
          children: [
            const _WizardHeader(),

            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
                child: isBudgetFirst
                    ? const _BudgetFirstContent()
                    : const _DestinationFirstContent(),
              ),
            ),

            if (isBudgetFirst) ...[
              _ContinueButton(
                enabled: provider.data.budget != null,
                onPressed: () async {
                  await provider.next();
                },
              ),
              const SizedBox(height: 16),
            ] else
              _DestinationSelectionBar(
                selectedName: provider.data.destination,
                onContinue: provider.next,
              ),
          ],
        ),
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// HEADER
// -----------------------------------------------------------------------------

class _WizardHeader extends StatelessWidget {
  const _WizardHeader();

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
              'Step 2 of 5',
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
// DESTINATION-FIRST
// -----------------------------------------------------------------------------

/// Search + country chips filter the real `GET /api/destinations` list
/// client-side (it's a short, curated list). A search with no match is the
/// SRS journey-7 "destination not supported" inline notice — no new screen.
class _DestinationFirstContent extends StatefulWidget {
  const _DestinationFirstContent();

  @override
  State<_DestinationFirstContent> createState() =>
      _DestinationFirstContentState();
}

class _DestinationFirstContentState extends State<_DestinationFirstContent> {
  final _searchController = TextEditingController();
  String _query = '';
  String? _country; // null = All

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  void _clearFilters() {
    _searchController.clear();
    setState(() {
      _query = '';
      _country = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();
    final selectedDestination = provider.data.destination;

    final destinations = provider.destinations.map((destination) {
      final name = destination['name'] as String;
      return _DestinationOption(
        name: name,
        country: destination['country'] as String,
        description: destination['description'] as String? ?? '',
        destinationId: destination['destinationId'] as int?,
        imageUrl: provider.imageUrlFor(name),
      );
    }).toList();

    final countries = {for (final d in destinations) d.country}.toList()
      ..sort();

    final query = _query.trim().toLowerCase();
    final visible = destinations.where((d) {
      if (_country != null && d.country != _country) return false;
      if (query.isEmpty) return true;
      return d.name.toLowerCase().contains(query) ||
          d.country.toLowerCase().contains(query) ||
          d.description.toLowerCase().contains(query);
    }).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const _SmartCurationBadge(label: 'Curated Destinations'),

        const SizedBox(height: 16),

        Text(
          'Where do you want\nto explore?',
          style: AppTextStyles.headlineLg.copyWith(
            fontSize: 28,
            height: 1.15,
          ),
        ),

        const SizedBox(height: 10),

        Text(
          'Select from our curated destinations or search below.',
          style: AppTextStyles.bodyMd.copyWith(
            color: AppColors.secondary,
            height: 1.45,
          ),
        ),

        const SizedBox(height: 20),

        _DestinationSearchField(
          controller: _searchController,
          onChanged: (value) => setState(() => _query = value),
          onClear: () {
            _searchController.clear();
            setState(() => _query = '');
          },
        ),

        if (countries.length > 1) ...[
          const SizedBox(height: 14),
          _CountryChips(
            countries: countries,
            selected: _country,
            onSelected: (country) => setState(() => _country = country),
          ),
        ],

        const SizedBox(height: 18),

        if (provider.destinationsLoading && destinations.isEmpty)
          const Column(
            children: [
              LoadingSkeleton(height: 250, borderRadius: 24),
              SizedBox(height: 16),
              LoadingSkeleton(height: 250, borderRadius: 24),
            ],
          )
        else if (provider.destinationsError != null)
          ErrorState(
            title: "Couldn't load destinations",
            description: provider.destinationsError!,
            onAction: provider.loadDestinations,
          )
        else if (destinations.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 24),
            child: Text(
              'No destinations available right now.',
              style: AppTextStyles.bodyMd.copyWith(color: AppColors.secondary),
            ),
          )
        else if (visible.isEmpty)
          _NotSupportedNotice(
            query: _query.trim(),
            onShowAll: _clearFilters,
          )
        else
          ...visible.map(
            (destination) => Padding(
              padding: const EdgeInsets.only(bottom: 16),
              child: _DestinationCard(
                option: destination,
                selected: selectedDestination == destination.name,
                onTap: () {
                  provider.selectDestination(
                    name: destination.name,
                    country: destination.country,
                    destinationId: destination.destinationId,
                  );
                },
              ),
            ),
          ),
      ],
    );
  }
}

// -----------------------------------------------------------------------------
// BUDGET-FIRST
// -----------------------------------------------------------------------------

class _BudgetFirstContent extends StatelessWidget {
  const _BudgetFirstContent();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();
    final selectedBudget = provider.data.budget;

    const budgets = [
      1000.0,
      1500.0,
      2000.0,
      2500.0,
      3000.0,
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const _SmartCurationBadge(),

        const SizedBox(height: 18),

        Text(
          'What is your\ntotal budget?',
          style: AppTextStyles.headlineLg.copyWith(
            fontSize: 27,
            height: 1.15,
          ),
        ),

        const SizedBox(height: 10),

        Text(
          'Tell us what you would like to spend and '
              'Triply will discover destinations that fit your budget.',
          style: AppTextStyles.bodyMd.copyWith(
            color: AppColors.secondary,
            height: 1.45,
          ),
        ),

        const SizedBox(height: 26),

        Text(
          'Choose your budget',
          style: AppTextStyles.headlineSm,
        ),

        const SizedBox(height: 12),

        ...budgets.map(
              (budget) => Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: _BudgetCard(
              amount: budget,
              selected: selectedBudget == budget,
              onTap: () {
                provider.setBudget(budget);
              },
            ),
          ),
        ),

        const SizedBox(height: 8),

        _CustomBudgetCard(
          selected:
          selectedBudget != null &&
              !budgets.contains(selectedBudget),
          onTap: () {
            _showCustomBudgetDialog(context);
          },
        ),
      ],
    );
  }
}

// -----------------------------------------------------------------------------
// COMMON
// -----------------------------------------------------------------------------

class _SmartCurationBadge extends StatelessWidget {
  const _SmartCurationBadge({this.label = 'Smart Curation'});

  final String label;

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
            label,
            style: AppTextStyles.labelSm.copyWith(
              color: AppColors.secondary,
            ),
          ),
        ],
      ),
    );
  }
}

class _DestinationOption {
  const _DestinationOption({
    required this.name,
    required this.country,
    required this.description,
    this.destinationId,
    this.imageUrl,
  });

  final String name;
  final String country;
  final String description;
  final int? destinationId;
  final String? imageUrl;
}

class _DestinationSearchField extends StatelessWidget {
  const _DestinationSearchField({
    required this.controller,
    required this.onChanged,
    required this.onClear,
  });

  final TextEditingController controller;
  final ValueChanged<String> onChanged;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(999),
        boxShadow: const [
          BoxShadow(
            blurRadius: 16,
            offset: Offset(0, 4),
            color: AppColors.shadowAmbient,
          ),
        ],
      ),
      child: TextField(
        controller: controller,
        onChanged: onChanged,
        textInputAction: TextInputAction.search,
        style: AppTextStyles.bodyMd.copyWith(color: AppColors.onSurface),
        decoration: InputDecoration(
          hintText: 'Search destinations or countries...',
          hintStyle: AppTextStyles.bodyMd.copyWith(color: AppColors.textMuted),
          prefixIcon: const Icon(Icons.travel_explore, color: AppColors.secondary),
          suffixIcon: ValueListenableBuilder<TextEditingValue>(
            valueListenable: controller,
            builder: (_, value, _) => value.text.isEmpty
                ? const SizedBox.shrink()
                : IconButton(
                    tooltip: 'Clear search',
                    icon: const Icon(Icons.close, size: 18),
                    color: AppColors.textMuted,
                    onPressed: onClear,
                  ),
          ),
          border: InputBorder.none,
          contentPadding: const EdgeInsets.symmetric(vertical: 16),
        ),
      ),
    );
  }
}

class _CountryChips extends StatelessWidget {
  const _CountryChips({
    required this.countries,
    required this.selected,
    required this.onSelected,
  });

  final List<String> countries;
  final String? selected;
  final ValueChanged<String?> onSelected;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 40,
      child: ListView(
        scrollDirection: Axis.horizontal,
        clipBehavior: Clip.none,
        children: [
          _chip('All', selected == null, () => onSelected(null)),
          for (final country in countries)
            _chip(country, selected == country, () => onSelected(country)),
        ],
      ),
    );
  }

  Widget _chip(String label, bool isSelected, VoidCallback onTap) {
    return Padding(
      padding: const EdgeInsets.only(right: 8),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(999),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          padding: const EdgeInsets.symmetric(horizontal: 18),
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: isSelected ? AppColors.secondary : Colors.white,
            borderRadius: BorderRadius.circular(999),
            border: Border.all(
              color: isSelected ? AppColors.secondary : AppColors.borderSubtle,
            ),
          ),
          child: Text(
            label,
            style: AppTextStyles.labelMd.copyWith(
              color: isSelected ? Colors.white : AppColors.onSurface,
              fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
            ),
          ),
        ),
      ),
    );
  }
}

class _DestinationCard extends StatelessWidget {
  const _DestinationCard({
    required this.option,
    required this.selected,
    required this.onTap,
  });

  final _DestinationOption option;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      selected: selected,
      label: '${option.name}, ${option.country}',
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(24),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 200),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(24),
              border: Border.all(
                color: selected ? AppColors.primary : Colors.transparent,
                width: 2,
              ),
              boxShadow: const [
                BoxShadow(
                  blurRadius: 18,
                  offset: Offset(0, 6),
                  color: AppColors.shadowAmbient,
                ),
              ],
            ),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(22),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  SizedBox(
                    height: 180,
                    child: Stack(
                      fit: StackFit.expand,
                      children: [
                        _CardImage(imageUrl: option.imageUrl),
                        const DecoratedBox(
                          decoration: BoxDecoration(
                            gradient: LinearGradient(
                              begin: Alignment.topCenter,
                              end: Alignment.bottomCenter,
                              colors: [Color(0x00000000), Color(0xB3000000)],
                              stops: [0.35, 1],
                            ),
                          ),
                        ),
                        const Positioned(
                          top: 12,
                          left: 12,
                          child: _CuratedPill(),
                        ),
                        Positioned(
                          top: 12,
                          right: 12,
                          child: _SelectionDot(selected: selected),
                        ),
                        Positioned(
                          left: 16,
                          right: 16,
                          bottom: 14,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  const Icon(
                                    Icons.near_me_outlined,
                                    size: 13,
                                    color: Colors.white70,
                                  ),
                                  const SizedBox(width: 4),
                                  Flexible(
                                    child: Text(
                                      option.country,
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: AppTextStyles.labelSm.copyWith(
                                        color: Colors.white70,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 2),
                              Text(
                                option.name,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: AppTextStyles.headlineLg.copyWith(
                                  color: Colors.white,
                                  fontSize: 24,
                                  height: 1.15,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 12, 14),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            option.description.isEmpty
                                ? 'Plan a personalized trip to ${option.name}.'
                                : option.description,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: AppTextStyles.bodySm.copyWith(
                              color: AppColors.onSurface,
                              height: 1.4,
                            ),
                          ),
                        ),
                        const SizedBox(width: 10),
                        AnimatedContainer(
                          duration: const Duration(milliseconds: 200),
                          width: 36,
                          height: 36,
                          decoration: BoxDecoration(
                            shape: BoxShape.circle,
                            color: selected
                                ? AppColors.primary
                                : AppColors.surfaceContainer,
                          ),
                          child: Icon(
                            selected ? Icons.check : Icons.arrow_forward,
                            size: 18,
                            color: selected ? Colors.white : AppColors.secondary,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _CardImage extends StatelessWidget {
  const _CardImage({required this.imageUrl});

  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    const fallback = DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [AppColors.secondary, AppColors.primary],
        ),
      ),
      child: Center(
        child: Icon(Icons.landscape_outlined, size: 48, color: Colors.white54),
      ),
    );

    if (imageUrl == null) return fallback;

    return Image.network(
      imageUrl!,
      fit: BoxFit.cover,
      errorBuilder: (_, _, _) => fallback,
      loadingBuilder: (context, child, progress) => progress == null
          ? child
          : const ColoredBox(color: AppColors.surfaceContainer),
    );
  }
}

class _CuratedPill extends StatelessWidget {
  const _CuratedPill();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(6, 5, 10, 5),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.92),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 16,
            height: 16,
            decoration: const BoxDecoration(
              color: AppColors.primary,
              shape: BoxShape.circle,
            ),
            child: const Icon(Icons.star, size: 10, color: Colors.white),
          ),
          const SizedBox(width: 6),
          Text(
            'Curated by Triply',
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

class _SelectionDot extends StatelessWidget {
  const _SelectionDot({required this.selected});

  final bool selected;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 200),
      width: 30,
      height: 30,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: selected
            ? AppColors.primary
            : Colors.white.withValues(alpha: 0.35),
        border: Border.all(color: Colors.white, width: 1.5),
      ),
      child: selected
          ? const Icon(Icons.check, color: Colors.white, size: 17)
          : null,
    );
  }
}

/// SRS journey 7: the search matched nothing in the curated dataset.
class _NotSupportedNotice extends StatelessWidget {
  const _NotSupportedNotice({
    required this.query,
    required this.onShowAll,
  });

  final String query;
  final VoidCallback onShowAll;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.borderSubtle),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.explore_off_outlined, color: AppColors.secondary),
          const SizedBox(height: 10),
          Text(
            query.isEmpty
                ? 'No destinations match this filter'
                : '"$query" isn\'t supported yet',
            style: AppTextStyles.labelLg,
          ),
          const SizedBox(height: 6),
          Text(
            'Triply only plans trips to destinations in its curated dataset, '
            'so every place in your itinerary is real. Try one of the '
            'supported destinations instead.',
            style: AppTextStyles.bodySm.copyWith(
              color: AppColors.secondary,
              height: 1.4,
            ),
          ),
          const SizedBox(height: 12),
          TextButton.icon(
            onPressed: onShowAll,
            icon: const Icon(Icons.public, size: 18),
            label: const Text('Show all destinations'),
            style: TextButton.styleFrom(
              foregroundColor: AppColors.primary,
              padding: EdgeInsets.zero,
            ),
          ),
        ],
      ),
    );
  }
}

class _DestinationSelectionBar extends StatelessWidget {
  const _DestinationSelectionBar({
    required this.selectedName,
    required this.onContinue,
  });

  final String? selectedName;
  final Future<void> Function() onContinue;

  @override
  Widget build(BuildContext context) {
    final hasSelection = selectedName != null;

    return Container(
      padding: const EdgeInsets.fromLTRB(20, 14, 20, 16),
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        boxShadow: [
          BoxShadow(
            blurRadius: 24,
            offset: Offset(0, -6),
            color: AppColors.shadowAmbient,
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          AnimatedSize(
            duration: const Duration(milliseconds: 200),
            child: hasSelection
                ? Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: Row(
                      children: [
                        Container(
                          width: 8,
                          height: 8,
                          decoration: const BoxDecoration(
                            color: AppColors.success,
                            shape: BoxShape.circle,
                          ),
                        ),
                        const SizedBox(width: 8),
                        Text(
                          'DESTINATION',
                          style: AppTextStyles.labelSm.copyWith(
                            color: AppColors.secondary,
                            letterSpacing: 0.6,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Align(
                            alignment: Alignment.centerRight,
                            child: Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 12,
                                vertical: 6,
                              ),
                              decoration: BoxDecoration(
                                color: AppColors.primaryContainerLight,
                                borderRadius: BorderRadius.circular(999),
                              ),
                              child: Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  const Icon(
                                    Icons.place_outlined,
                                    size: 14,
                                    color: AppColors.primary,
                                  ),
                                  const SizedBox(width: 4),
                                  Flexible(
                                    child: Text(
                                      'Selected: $selectedName',
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: AppTextStyles.labelSm.copyWith(
                                        color: AppColors.onPrimaryContainerLight,
                                        fontWeight: FontWeight.w700,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                  )
                : const SizedBox(width: double.infinity),
          ),
          PrimaryButton(
            label: hasSelection
                ? 'Continue to Trip Details'
                : 'Select a destination',
            icon: Icons.arrow_forward,
            onPressed: hasSelection ? onContinue : null,
          ),
        ],
      ),
    );
  }
}

class _BudgetCard extends StatelessWidget {
  const _BudgetCard({
    required this.amount,
    required this.selected,
    required this.onTap,
  });

  final double amount;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(18),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          padding: const EdgeInsets.symmetric(
            horizontal: 16,
            vertical: 15,
          ),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(
              color: selected
                  ? AppColors.primary
                  : AppColors.surfaceContainer,
              width: selected ? 1.5 : 1,
            ),
          ),
          child: Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: AppColors.surfaceContainerLow,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.attach_money,
                  color: AppColors.primary,
                  size: 20,
                ),
              ),

              const SizedBox(width: 12),

              Expanded(
                child: Text(
                  '\$${amount.toStringAsFixed(0)}',
                  style: AppTextStyles.labelLg.copyWith(
                    fontSize: 16,
                  ),
                ),
              ),

              if (selected)
                const Icon(
                  Icons.check_circle,
                  color: AppColors.primary,
                  size: 22,
                )
              else
                const Icon(
                  Icons.radio_button_unchecked,
                  color: AppColors.secondary,
                  size: 22,
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CustomBudgetCard extends StatelessWidget {
  const _CustomBudgetCard({
    required this.selected,
    required this.onTap,
  });

  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(18),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(15),
        decoration: BoxDecoration(
          color: AppColors.surfaceContainerLow,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(
            color: selected
                ? AppColors.primary
                : Colors.transparent,
          ),
        ),
        child: Row(
          children: [
            const Icon(
              Icons.edit_outlined,
              color: AppColors.primary,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                'Enter a custom budget',
                style: AppTextStyles.labelMd.copyWith(
                  color: AppColors.onSurface,
                ),
              ),
            ),
            const Icon(
              Icons.chevron_right,
              color: AppColors.secondary,
            ),
          ],
        ),
      ),
    );
  }
}

class _ContinueButton extends StatelessWidget {
  const _ContinueButton({
    required this.enabled,
    required this.onPressed,
  });

  final bool enabled;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: PrimaryButton(
        label: 'Continue',
        icon: Icons.arrow_forward,
        onPressed: enabled ? onPressed : () {},
        fullWidth: true,
      ),
    );
  }
}

// -----------------------------------------------------------------------------
// CUSTOM BUDGET DIALOG
// -----------------------------------------------------------------------------

Future<void> _showCustomBudgetDialog(BuildContext context) async {
  final tripCreationProvider = context.read<TripCreationProvider>();

  final value = await showDialog<double>(
    context: context,
    builder: (_) => const _CustomBudgetDialog(),
  );

  if (value != null) {
    tripCreationProvider.setBudget(value);
  }
}

/// A dedicated StatefulWidget (not an ad-hoc StatefulBuilder + a
/// TextEditingController manually created around showDialog) so Flutter's
/// own widget lifecycle owns the controller — see trip_details_screen.dart's
/// `_BudgetDialog` for the crash this pattern avoids.
class _CustomBudgetDialog extends StatefulWidget {
  const _CustomBudgetDialog();

  @override
  State<_CustomBudgetDialog> createState() => _CustomBudgetDialogState();
}

class _CustomBudgetDialogState extends State<_CustomBudgetDialog> {
  final _controller = TextEditingController();
  String? _errorText;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _save() {
    final error = TripValidators.budgetInput(_controller.text, required: true);

    if (error != null) {
      setState(() => _errorText = error);
      return;
    }

    Navigator.pop(context, double.parse(_controller.text.trim()));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Custom budget'),
      content: TextField(
        controller: _controller,
        keyboardType: TextInputType.number,
        autofocus: true,
        decoration: InputDecoration(
          prefixText: '\$ ',
          hintText: '2500',
          errorText: _errorText,
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _save,
          child: const Text('Save'),
        ),
      ],
    );
  }
}
