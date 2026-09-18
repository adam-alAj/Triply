import 'package:flutter/material.dart';

import '../../../core/theme/app_text_styles.dart';

/// Archive/Delete Trip (UI Pages §6): "soft delete only (DB §16)" — there is
/// deliberately no hard-delete option. What the task calls "Archive/Delete"
/// is a single confirmation for archiving; restoring is handled elsewhere
/// (My Trips' Archived tab).
Future<bool> showArchiveTripDialog(BuildContext context) async {
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (_) => AlertDialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      title: Text('Archive this trip?', style: AppTextStyles.headlineSm),
      content: Text(
        "It'll move out of your active trips, but you can restore it "
        'anytime from Archived Trips.',
        style: AppTextStyles.bodySm,
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(false),
          child: const Text('Cancel'),
        ),
        ElevatedButton(
          onPressed: () => Navigator.of(context).pop(true),
          child: const Text('Archive'),
        ),
      ],
    ),
  );

  return confirmed ?? false;
}
