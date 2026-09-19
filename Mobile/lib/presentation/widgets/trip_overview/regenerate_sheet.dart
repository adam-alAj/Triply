import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';

enum RegenerateScope { item, day }

/// Regenerate Sheet (UI Pages §6). Offers "this item" / "this day" per the
/// task's acceptance criteria. Actually running AI regeneration is a
/// separate, later task ("Implement Partial Regeneration End-to-End") — this
/// sheet only collects the user's choice; the caller decides what to do
/// with it (today, that's a "coming soon" message).
Future<RegenerateScope?> showRegenerateSheet(BuildContext context) {
  return showModalBottomSheet<RegenerateScope>(
    context: context,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    builder: (context) => const _RegenerateSheet(),
  );
}

class _RegenerateSheet extends StatelessWidget {
  const _RegenerateSheet();

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 36,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.surfaceContainerHigh,
                  borderRadius: BorderRadius.circular(999),
                ),
              ),
            ),
            const SizedBox(height: 16),
            Text('Regenerate with AI', style: AppTextStyles.headlineSm),
            const SizedBox(height: 4),
            Text(
              'Choose how much of the itinerary to re-plan.',
              style: AppTextStyles.bodySm,
            ),
            const SizedBox(height: 16),
            _RegenerateOption(
              icon: Icons.replay_outlined,
              title: 'Regenerate this item',
              subtitle: 'Only this activity gets a new AI suggestion.',
              onTap: () =>
                  Navigator.of(context).pop(RegenerateScope.item),
            ),
            const SizedBox(height: 10),
            _RegenerateOption(
              icon: Icons.autorenew,
              title: 'Regenerate this day',
              subtitle: 'The whole day\'s plan gets re-planned by AI.',
              onTap: () => Navigator.of(context).pop(RegenerateScope.day),
            ),
          ],
        ),
      ),
    );
  }
}

class _RegenerateOption extends StatelessWidget {
  const _RegenerateOption({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(18),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AppColors.surfaceContainerLow,
          borderRadius: BorderRadius.circular(18),
        ),
        child: Row(
          children: [
            Container(
              width: 38,
              height: 38,
              decoration: const BoxDecoration(
                color: AppColors.tertiaryFixed,
                shape: BoxShape.circle,
              ),
              child: Icon(icon, size: 18, color: AppColors.secondary),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: AppTextStyles.labelLg),
                  Text(subtitle, style: AppTextStyles.bodySm),
                ],
              ),
            ),
            const Icon(Icons.chevron_right, color: AppColors.textMuted),
          ],
        ),
      ),
    );
  }
}
