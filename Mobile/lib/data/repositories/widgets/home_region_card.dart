import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';


class HomeRegionCard extends StatelessWidget {
  const HomeRegionCard({
    super.key,
    required this.name,
    required this.imageAsset,
    required this.badge,
    this.guides,
    required this.description,
    this.imageUrl,
  });

  final String name;
  final String imageAsset;
  final String badge;
  final String? guides;
  final String description;
  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    return Container(
        decoration: BoxDecoration(
          color: AppColors.surfaceContainerLowest,
          borderRadius: BorderRadius.circular(20),
          boxShadow: const [
            BoxShadow(
              color: AppColors.shadowAmbient,
              blurRadius: 5,
              offset: Offset(0, 2),
            ),
          ],
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Stack(
              children: [
                AspectRatio(
                  aspectRatio: 1.25,
                  child: imageUrl != null
                      ? Image.network(
                          imageUrl!,
                          fit: BoxFit.cover,
                          errorBuilder: (_, __, ___) =>
                              Image.asset(imageAsset, fit: BoxFit.cover),
                        )
                      : Image.asset(
                          imageAsset,
                          fit: BoxFit.cover,
                        ),
                ),
                Positioned(
                  top: 8,
                  left: 8,
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 7,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.92),
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Text(
                      badge,
                      style: AppTextStyles.labelSm.copyWith(
                        color: AppColors.onSurface,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ),
                Positioned(
                  left: 10,
                  right: 10,
                  bottom: 9,
                  child: Text(
                    name,
                    style: AppTextStyles.headlineSm.copyWith(
                      color: Colors.white,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(10, 8, 10, 10),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (guides != null) ...[
                    Text(
                      guides!,
                      style: AppTextStyles.labelSm,
                    ),
                    const SizedBox(height: 3),
                  ],
                  Text(
                    description,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTextStyles.labelSm,
                  ),
                ],
              ),
            ),
          ],
        ),
    );
  }
}