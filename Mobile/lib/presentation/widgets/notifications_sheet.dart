import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import 'empty_state.dart';

/// Notifications bell (Home, My Trips, Profile, Planning Mode). There's no
/// notifications backend yet, so the sheet always shows the empty state
/// instead of the bell doing nothing.
Future<void> showNotificationsSheet(BuildContext context) {
  return showModalBottomSheet<void>(
    context: context,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    builder: (context) => SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(height: 12),
          Container(
            width: 36,
            height: 4,
            decoration: BoxDecoration(
              color: AppColors.surfaceContainerHigh,
              borderRadius: BorderRadius.circular(999),
            ),
          ),
          const EmptyState(
            icon: Icons.notifications_none_outlined,
            title: "You're all caught up",
            description:
                'Trip updates and reminders will show up here when there is something new.',
          ),
        ],
      ),
    ),
  );
}
