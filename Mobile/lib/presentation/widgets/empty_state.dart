import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';
import 'primary_button.dart';
import 'secondary_button.dart';

/// Per 08_SYSTEM_DESIGN.md Principle 06 ("No Dead Ends"): every empty
/// state must answer what happened, why it matters, what to do next.
/// Example: "No saved trips yet" -> "Plan Your First Trip".
///
/// An optional [secondaryActionLabel]/[onSecondaryAction] pair supports
/// empty states with two useful next steps (e.g. "No destinations match your
/// budget" -> Adjust budget / Change interests, 08 §35).
class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.title,
    required this.description,
    this.icon,
    this.actionLabel,
    this.onAction,
    this.secondaryActionLabel,
    this.onSecondaryAction,
  });

  final String title;
  final String description;
  final IconData? icon;
  final String? actionLabel;
  final VoidCallback? onAction;
  final String? secondaryActionLabel;
  final VoidCallback? onSecondaryAction;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) Icon(icon, size: 48, color: AppColors.textMuted),
          const SizedBox(height: 16),
          Text(title, style: AppTextStyles.headlineMd, textAlign: TextAlign.center),
          const SizedBox(height: 8),
          Text(description, style: AppTextStyles.bodyMd, textAlign: TextAlign.center),
          if (actionLabel != null && onAction != null) ...[
            const SizedBox(height: 24),
            PrimaryButton(label: actionLabel!, onPressed: onAction, fullWidth: false),
          ],
          if (secondaryActionLabel != null && onSecondaryAction != null) ...[
            const SizedBox(height: 8),
            SecondaryButton(label: secondaryActionLabel!, onPressed: onSecondaryAction),
          ],
        ],
      ),
    );
  }
}
