import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';

class AuthHeader extends StatelessWidget {
  const AuthHeader({
    super.key,
    required this.onBack,
  });

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 56,
      child: Row(
        children: [
          IconButton(
            onPressed: onBack,
            icon: const Icon(
              Icons.arrow_back,
              color: AppColors.onSurface,
            ),
          ),
          const SizedBox(width: 4),
          Image.asset(
            'assets/images/Triply Logo.png',
            width: 28,
            height: 28,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              'Trip Detail Itinerary',
              style: AppTextStyles.headlineSm,
            ),
          ),
          Container(
            width: 36,
            height: 36,
            decoration: const BoxDecoration(
              color: AppColors.primary,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.person_outline,
              size: 20,
              color: AppColors.onPrimary,
            ),
          ),
          const SizedBox(width: 12),
        ],
      ),
    );
  }
}