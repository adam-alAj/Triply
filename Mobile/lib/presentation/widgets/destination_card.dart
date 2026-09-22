import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// ASSUMPTION FLAGGED: not yet cross-checked against Figma "Destination
/// Suggestions" screen (node 1:1678) — built from 08_SYSTEM_DESIGN.md
/// §11 ("Destination Card should communicate: Image, name, one useful
/// supporting attribute, selection state"). Re-verify against Figma.
class DestinationCard extends StatelessWidget {
  const DestinationCard({
    super.key,
    required this.name,
    required this.supportingAttribute,
    this.imageUrl,
    this.selected = false,
    this.onTap,
  });

  final String name;
  final String supportingAttribute; // e.g. "From $450" or "7-day avg"
  final String? imageUrl;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 200,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(32),
          border: selected
              ? Border.all(color: AppColors.primary, width: 2)
              : null,
          boxShadow: const [
            BoxShadow(
              color: AppColors.shadowAmbient,
              blurRadius: 6,
              offset: Offset(0, 3),
            ),
          ],
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              height: 120,
              width: double.infinity,
              child: imageUrl != null
                  ? Image.network(
                      imageUrl!,
                      fit: BoxFit.cover,
                      errorBuilder: (context, error, stackTrace) {
                        return Container(color: AppColors.surfaceContainer);
                      },
                    )
                  : Container(color: AppColors.surfaceContainer),
            ),
            Container(
              width: double.infinity,
              color: AppColors.surfaceContainerLowest,
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    name,
                    style: AppTextStyles.headlineSm,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(supportingAttribute, style: AppTextStyles.bodySm),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
