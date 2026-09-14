import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Reusable wrapper so every bottom sheet in the app (Place Detail,
/// Regenerate Options, Edit Item...) gets the same rounded-xl
/// (top corners only, per 09_DESIGN.md Shapes) + drag handle, without
/// re-implementing it per screen. Call `showAppBottomSheet(...)`.
Future<T?> showAppBottomSheet<T>({
  required BuildContext context,
  required String title,
  required Widget child,
}) {
  return showModalBottomSheet<T>(
    context: context,
    backgroundColor: AppColors.surface,
    isScrollControlled: true,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(48)),
    ),
    builder: (context) {
      return Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 36,
                height: 4,
                margin: const EdgeInsets.symmetric(vertical: 8),
                decoration: BoxDecoration(
                  color: AppColors.borderSubtle,
                  borderRadius: BorderRadius.circular(9999),
                ),
              ),
            ),
            Text(title, style: AppTextStyles.headlineSm),
            const SizedBox(height: 16),
            child,
          ],
        ),
      );
    },
  );
}
