import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';
import 'primary_button.dart';

/// Per 08_SYSTEM_DESIGN.md Principle 06 ("No Dead Ends"): every empty
/// state must answer what happened, why it matters, what to do next.
/// Example: "No saved trips yet" -> "Plan Your First Trip".
class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.title,
    required this.description,
    this.icon,
    this.actionLabel,
    this.onAction,
  });

  final String title;
  final String description;
  final IconData? icon;
  final String? actionLabel;
  final VoidCallback? onAction;

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
        ],
      ),
    );
  }
}
