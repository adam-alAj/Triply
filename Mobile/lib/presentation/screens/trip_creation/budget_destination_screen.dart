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

            _ContinueButton(
              enabled: isBudgetFirst
                  ? provider.data.budget != null
                  : provider.data.destination != null,
              onPressed: () async {
                await provider.next();
              },
            ),

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

class _DestinationFirstContent extends StatelessWidget {
  const _DestinationFirstContent();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();
    final selectedDestination = provider.data.destination;

    final destinations = provider.destinations.map((destination) {
      return _DestinationOption(
        name: destination['name'] as String,
        country: destination['country'] as String,
        description: destination['description'] as String? ?? '',
        icon: Icons.place_outlined,
        destinationId: destination['destinationId'] as int?,
      );
    }).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const _SmartCurationBadge(),

        const SizedBox(height: 18),

        Text(
          'Where do you want\nto explore?',
          style: AppTextStyles.headlineLg.copyWith(
            fontSize: 27,
            height: 1.15,
          ),
        ),

        const SizedBox(height: 10),

        Text(
          'Choose a destination and Triply will build '
              'a journey around what makes it meaningful to you.',
          style: AppTextStyles.bodyMd.copyWith(
            color: AppColors.secondary,
            height: 1.45,
          ),
        ),

        const SizedBox(height: 26),

        Text(
          'Popular destinations',
          style: AppTextStyles.headlineSm,
        ),

        const SizedBox(height: 12),

        if (provider.destinationsLoading && destinations.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 12),
            child: Column(
              children: [
                LoadingSkeleton(height: 96, borderRadius: 20),
                SizedBox(height: 12),
                LoadingSkeleton(height: 96, borderRadius: 20),
              ],
            ),
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
        else
          ...destinations.map(
            (destination) => Padding(
              padding: const EdgeInsets.only(bottom: 12),
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

        const SizedBox(height: 8),

        _SearchDestinationCard(
          onTap: () {
            _showMockSearchMessage(context);
          },
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
            'Smart Curation',
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
    required this.icon,
    this.destinationId,
  });

  final String name;
  final String country;
  final String description;
  final IconData icon;
  final int? destinationId;
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
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(20),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          padding: const EdgeInsets.all(15),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(20),
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
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: AppColors.surfaceContainerLow,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Icon(
                  option.icon,
                  color: AppColors.primary,
                  size: 23,
                ),
              ),

              const SizedBox(width: 13),

              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      option.name,
                      style: AppTextStyles.labelLg.copyWith(
                        color: AppColors.onSurface,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      option.description,
                      style: AppTextStyles.bodySm.copyWith(
                        color: AppColors.secondary,
                      ),
                    ),
                  ],
                ),
              ),

              AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                width: 22,
                height: 22,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: selected
                      ? AppColors.primary
                      : Colors.transparent,
                  border: Border.all(
                    color: selected
                        ? AppColors.primary
                        : AppColors.secondary,
                    width: 1.3,
                  ),
                ),
                child: selected
                    ? const Icon(
                  Icons.check,
                  color: Colors.white,
                  size: 14,
                )
                    : null,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _SearchDestinationCard extends StatelessWidget {
  const _SearchDestinationCard({
    required this.onTap,
  });

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
        ),
        child: Row(
          children: [
            const Icon(
              Icons.search,
              color: AppColors.primary,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                'Search for another destination',
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
// MOCK DIALOGS
// -----------------------------------------------------------------------------

void _showMockSearchMessage(BuildContext context) {
  ScaffoldMessenger.of(context).showSnackBar(
    const SnackBar(
      content: Text(
        'Destination search will be connected to the backend later.',
      ),
    ),
  );
}

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