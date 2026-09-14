import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Figma source: Login (node 1:1319) — Email/Password fields.
/// White pill container, leading icon at left, label above with an
/// optional helper/required tag aligned right, focus ring in primary,
/// error state swaps border to formError.
class AppTextField extends StatefulWidget {
  const AppTextField({
    super.key,
    required this.label,
    this.helperText,
    this.controller,
    this.leadingIcon,
    this.obscureText = false,
    this.errorText,
    this.keyboardType,
    this.onChanged,
    this.enabled = true,
  });

  final String label;
  final String? helperText; // e.g. "Required"
  final TextEditingController? controller;
  final IconData? leadingIcon;
  final bool obscureText;
  final String? errorText;
  final TextInputType? keyboardType;
  final ValueChanged<String>? onChanged;
  final bool enabled;

  @override
  State<AppTextField> createState() => _AppTextFieldState();
}

class _AppTextFieldState extends State<AppTextField> {
  late bool _obscure = widget.obscureText;

  @override
  Widget build(BuildContext context) {
    final hasError = widget.errorText != null && widget.errorText!.isNotEmpty;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(widget.label, style: AppTextStyles.bodySm.copyWith(color: AppColors.onSurface, fontWeight: FontWeight.w500)),
              if (widget.helperText != null)
                Text(widget.helperText!, style: AppTextStyles.labelSm),
            ],
          ),
        ),
        const SizedBox(height: 4),
        Container(
          decoration: BoxDecoration(
            color: AppColors.surfaceContainerLowest,
            borderRadius: BorderRadius.circular(32),
            border: hasError ? Border.all(color: AppColors.formError, width: 1.5) : null,
            boxShadow: const [
              BoxShadow(color: AppColors.shadowAmbient, blurRadius: 1, offset: Offset(0, 1)),
            ],
          ),
          child: TextField(
            controller: widget.controller,
            obscureText: _obscure,
            enabled: widget.enabled,
            keyboardType: widget.keyboardType,
            onChanged: widget.onChanged,
            style: AppTextStyles.bodyMd.copyWith(color: AppColors.onSurface),
            decoration: InputDecoration(
              border: InputBorder.none,
              contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
              prefixIcon: widget.leadingIcon != null
                  ? Icon(widget.leadingIcon, size: 18, color: AppColors.textMuted)
                  : null,
              suffixIcon: widget.obscureText
                  ? IconButton(
                icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined,
                    size: 18, color: AppColors.textMuted),
                onPressed: () => setState(() => _obscure = !_obscure),
              )
                  : null,
            ),
          ),
        ),
        if (hasError) ...[
          const SizedBox(height: 4),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4),
            child: Text(widget.errorText!, style: AppTextStyles.labelSm.copyWith(color: AppColors.formError)),
          ),
        ],
      ],
    );
  }
}
