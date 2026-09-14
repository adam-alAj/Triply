import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Per 08_SYSTEM_DESIGN.md §28: never use ambiguous labels like "Yes" —
/// always a specific verb ("Archive Trip", "Delete Trip"). Call
/// `showAppConfirmationDialog(...)`.
Future<bool> showAppConfirmationDialog({
  required BuildContext context,
  required String title,
  required String message,
  required String confirmLabel, // e.g. "Delete Trip" — never "Yes"
  bool isDestructive = true,
  String cancelLabel = 'Cancel',
}) async {
  final result = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      backgroundColor: AppColors.surfaceContainerLowest,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
      title: Text(title, style: AppTextStyles.headlineSm),
      content: Text(message, style: AppTextStyles.bodyMd),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(false),
          child: Text(cancelLabel, style: AppTextStyles.labelMd.copyWith(color: AppColors.secondary)),
        ),
        TextButton(
          onPressed: () => Navigator.of(context).pop(true),
          child: Text(
            confirmLabel,
            style: AppTextStyles.labelMd.copyWith(
              color: isDestructive ? AppColors.error : AppColors.primary,
            ),
          ),
        ),
      ],
    ),
  );
  return result ?? false;
}
