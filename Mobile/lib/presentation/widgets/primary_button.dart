import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Figma source: Login (node 1:1319) — "Primary Action Button".
/// Full pill radius, filled primary, white label, soft shadow on press.
/// Stateless, no domain dependencies — reusable everywhere.
class PrimaryButton extends StatelessWidget {
  const PrimaryButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.icon,
    this.isLoading = false,
    this.fullWidth = true,
  });

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool isLoading;
  final bool fullWidth;

  @override
  Widget build(BuildContext context) {
    final button = ElevatedButton(
      onPressed: isLoading ? null : onPressed,
      style: ElevatedButton.styleFrom(
        backgroundColor: AppColors.primary,
        disabledBackgroundColor: AppColors.primary.withValues(alpha: 0.5),
        foregroundColor: AppColors.onPrimary,
        elevation: 4,
        shadowColor: AppColors.primary.withValues(alpha: 0.2),
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
        shape: const StadiumBorder(),
      ),
      child: isLoading
          ? const SizedBox(
        height: 20,
        width: 20,
        child: CircularProgressIndicator(
          strokeWidth: 2,
          valueColor: AlwaysStoppedAnimation(AppColors.onPrimary),
        ),
      )
          : Row(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(label, style: AppTextStyles.labelLg.copyWith(color: AppColors.onPrimary)),
          if (icon != null) ...[
            const SizedBox(width: 8),
            Icon(icon, size: 16, color: AppColors.onPrimary),
          ],
        ],
      ),
    );

    return fullWidth ? SizedBox(width: double.infinity, child: button) : button;
  }
}
