import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';
import 'secondary_button.dart';

/// Body of the "Generating" full-screen wait state (MOB-TRIP-08).
///
/// AI generation latency is explicitly unbounded (NFR-PERF-001), so the wait
/// must stay trustworthy past 30 seconds without ever looking broken or
/// fabricating progress (07_UI_PAGES.md §8, 08_SYSTEM_DESIGN.md §33 + §44
/// Risk 05: "Long generation time can feel like a broken app" → "purposeful
/// generating state, no fake progress").
///
/// Past [longWaitThreshold] the notice escalates from a plain spinner to an
/// explicit "this is taking longer than usual" reassurance, reports honestly
/// how long it has actually been running, and keeps the Cancel escape hatch.
class GenerationWaitNotice extends StatelessWidget {
  const GenerationWaitNotice({
    super.key,
    required this.spinner,
    required this.message,
    required this.elapsed,
    required this.onCancel,
  });

  /// Duration after which the notice reassures the user that a long wait is
  /// expected rather than a failure. Chosen so a normal generation (which can
  /// take ~30s against the live Gemini API) still feels intentional.
  static const Duration longWaitThreshold = Duration(seconds: 30);

  /// The animated indicator owned by the screen (kept as a slot so the
  /// animation controller's lifecycle stays with the stateful host).
  final Widget spinner;

  /// The rotating status line describing what the AI is currently doing.
  final String message;

  /// How long the current attempt has been running.
  final Duration elapsed;

  final VoidCallback onCancel;

  bool get isLongWait => elapsed >= longWaitThreshold;

  String get elapsedLabel {
    final seconds = elapsed.inSeconds;
    if (seconds < 60) return '$seconds seconds';
    final minutes = seconds ~/ 60;
    final remainder = seconds % 60;
    return remainder == 0 ? '$minutes min' : '$minutes min $remainder sec';
  }

  @override
  Widget build(BuildContext context) {
    return Semantics(
      liveRegion: true,
      label: 'Creating your trip. $message',
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          spinner,
          const SizedBox(height: 28),
          Text(
            'Creating your trip',
            style: AppTextStyles.headlineMd,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 10),
          AnimatedSwitcher(
            duration: const Duration(milliseconds: 250),
            child: Text(
              message,
              key: ValueKey(message),
              style: AppTextStyles.bodyMd.copyWith(color: AppColors.secondary),
              textAlign: TextAlign.center,
            ),
          ),
          if (isLongWait) ...[
            const SizedBox(height: 24),
            _LongWaitCard(elapsedLabel: elapsedLabel),
          ],
          const SizedBox(height: 36),
          SecondaryButton(label: 'Cancel', onPressed: onCancel),
        ],
      ),
    );
  }
}

/// Reassurance shown once generation passes [GenerationWaitNotice.longWaitThreshold].
/// Explains *why* the wait is long and what the user can do — never a fake
/// percentage bar (08_SYSTEM_DESIGN.md §44 Risk 05).
class _LongWaitCard extends StatelessWidget {
  const _LongWaitCard({required this.elapsedLabel});

  final String elapsedLabel;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.borderSubtle),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.hourglass_bottom_rounded,
            size: 18,
            color: AppColors.secondary,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'This is taking a little longer than usual',
                  style: AppTextStyles.labelMd.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  'AI is still matching real places to your preferences. '
                  'This can take up to a minute — you can keep waiting or '
                  'cancel and try again.',
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.textMuted,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  'Working for $elapsedLabel',
                  style: AppTextStyles.labelSm.copyWith(
                    color: AppColors.secondary,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
