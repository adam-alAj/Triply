import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../primary_button.dart';

/// Adjust Budget sheet (Costs tab). Collects a new total budget in USD; the
/// caller saves it via `TripOverviewProvider.updateBudget`. Returns null
/// when dismissed.
Future<double?> showAdjustBudgetSheet(
  BuildContext context, {
  required double currentBudgetUsd,
  required double estimatedTotalUsd,
}) {
  return showModalBottomSheet<double>(
    context: context,
    isScrollControlled: true,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    builder: (context) => _AdjustBudgetSheet(
      currentBudgetUsd: currentBudgetUsd,
      estimatedTotalUsd: estimatedTotalUsd,
    ),
  );
}

class _AdjustBudgetSheet extends StatefulWidget {
  const _AdjustBudgetSheet({
    required this.currentBudgetUsd,
    required this.estimatedTotalUsd,
  });

  final double currentBudgetUsd;
  final double estimatedTotalUsd;

  @override
  State<_AdjustBudgetSheet> createState() => _AdjustBudgetSheetState();
}

class _AdjustBudgetSheetState extends State<_AdjustBudgetSheet> {
  late final TextEditingController _controller = TextEditingController(
    text: widget.currentBudgetUsd.round().toString(),
  );
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _submit() {
    final value = double.tryParse(_controller.text.trim());
    if (value == null || value <= 0) {
      setState(() => _error = 'Enter a budget greater than 0.');
      return;
    }
    Navigator.of(context).pop(value);
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(
                child: Container(
                  width: 36,
                  height: 4,
                  decoration: BoxDecoration(
                    color: AppColors.surfaceContainerHigh,
                    borderRadius: BorderRadius.circular(999),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              Text('Adjust Budget', style: AppTextStyles.headlineSm),
              const SizedBox(height: 4),
              Text(
                'Current estimate: \$${widget.estimatedTotalUsd.round()} for the whole trip.',
                style: AppTextStyles.bodySm,
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _controller,
                autofocus: true,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: [
                  FilteringTextInputFormatter.allow(RegExp(r'^\d*\.?\d{0,2}')),
                ],
                onSubmitted: (_) => _submit(),
                decoration: InputDecoration(
                  labelText: 'Total budget (USD)',
                  prefixText: '\$ ',
                  errorText: _error,
                  filled: true,
                  fillColor: AppColors.surfaceContainerLow,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(16),
                    borderSide: BorderSide.none,
                  ),
                ),
              ),
              const SizedBox(height: 16),
              PrimaryButton(label: 'Save Budget', onPressed: _submit),
            ],
          ),
        ),
      ),
    );
  }
}
