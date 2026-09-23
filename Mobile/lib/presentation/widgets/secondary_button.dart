import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Source: 09_DESIGN.md "Secondary Action" — pill radius, tertiary
/// background, secondary-colored label, zero border.
/// Used for: alternative actions, Edit, Back to Review.
class SecondaryButton extends StatelessWidget {
  const SecondaryButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.icon,
    this.fullWidth = false,
  });

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool fullWidth;

  @override
  Widget build(BuildContext context) {
    final button = ElevatedButton(
      onPressed: onPressed,
      style: ElevatedButton.styleFrom(
        backgroundColor: AppColors.surfaceContainerLow,
        foregroundColor: AppColors.secondary,
        elevation: 0,
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
        shape: const StadiumBorder(),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Flexible(
            child: FittedBox(
              fit: BoxFit.scaleDown,
              child: Text(label, maxLines: 1, style: AppTextStyles.labelLg.copyWith(color: AppColors.secondary)),
            ),
          ),
          if (icon != null) ...[
            const SizedBox(width: 8),
            Icon(icon, size: 16, color: AppColors.secondary),
          ],
        ],
      ),
    );

    return fullWidth ? SizedBox(width: double.infinity, child: button) : button;
  }
}
