import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';
import 'primary_button.dart';

/// Per 08_SYSTEM_DESIGN.md §36: error messages must be human-readable,
/// short, non-technical, actionable, non-blaming. Never pass a raw
/// exception/stack trace as [description] — the caller must map it to
/// a friendly message first.
class ErrorState extends StatelessWidget {
  const ErrorState({
    super.key,
    required this.title,
    required this.description,
    this.actionLabel = 'Retry',
    this.onAction,
  });

  final String title;
  final String description;
  final String actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.error_outline, size: 48, color: AppColors.error),
          const SizedBox(height: 16),
          Text(title, style: AppTextStyles.headlineMd, textAlign: TextAlign.center),
          const SizedBox(height: 8),
          Text(description, style: AppTextStyles.bodyMd, textAlign: TextAlign.center),
          if (onAction != null) ...[
            const SizedBox(height: 24),
            PrimaryButton(label: actionLabel, onPressed: onAction, fullWidth: false),
          ],
        ],
      ),
    );
  }
}
