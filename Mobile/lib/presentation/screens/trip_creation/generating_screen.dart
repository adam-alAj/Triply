import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../providers/trip_creation_provider.dart';
import '../../widgets/generation_wait_notice.dart';
import '../../widgets/over_budget_notice.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/secondary_button.dart';

/// MOB-TRIP-08 — full-screen wait state while the trip is created and AI
/// generation is kicked off.
///
/// This screen (not Review) owns triggering the actual network calls, so
/// a slow or failed request never leaves Review's button stuck mid-press.
/// Every request already carries a bounded timeout (see ApiClient — 15s by
/// default, 120s for the generate call itself since it runs bounded-retry
/// AI attempts server-side), so "never spins forever" is satisfied by the
/// network layer itself.
///
/// The local elapsed timer below is not a timeout — it drives the 30s+
/// "taking longer than usual" reassurance in [GenerationWaitNotice] so a slow
/// but healthy generation never reads as a frozen app.
class GeneratingScreen extends StatefulWidget {
  const GeneratingScreen({super.key});

  @override
  State<GeneratingScreen> createState() => _GeneratingScreenState();
}

class _GeneratingScreenState extends State<GeneratingScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController _spinController;
  Timer? _messageTimer;
  Timer? _elapsedTimer;
  int _messageIndex = 0;
  Duration _elapsed = Duration.zero;

  static const _messages = [
    'Balancing your dates, budget, and travelers…',
    'Matching activities to your interests…',
    'Checking real places against our dataset…',
    'Almost there — putting your itinerary together…',
  ];

  @override
  void initState() {
    super.initState();

    _spinController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();

    _messageTimer = Timer.periodic(const Duration(seconds: 3), (_) {
      if (!mounted) return;
      setState(() => _messageIndex = (_messageIndex + 1) % _messages.length);
    });

    _elapsedTimer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      setState(() => _elapsed += const Duration(seconds: 1));
    });

    // Kick off the real work once, after the first frame — never in build(),
    // so rebuilds from the provider's own notifyListeners() don't re-fire it.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<TripCreationProvider>().submitTrip();
    });
  }

  @override
  void dispose() {
    _spinController.dispose();
    _messageTimer?.cancel();
    _elapsedTimer?.cancel();
    super.dispose();
  }

  void _retry() {
    // Restart the wait clock so the reassurance reflects the new attempt,
    // not the time already spent on the failed one.
    setState(() => _elapsed = Duration.zero);
    context.read<TripCreationProvider>().submitTrip();
  }

  void _backToReview() =>
      context.read<TripCreationProvider>().jumpToStep(5);

  Widget _buildSpinner() {
    return RotationTransition(
      turns: _spinController,
      child: Container(
        width: 72,
        height: 72,
        decoration: const BoxDecoration(
          color: AppColors.primaryContainerLight,
          shape: BoxShape.circle,
        ),
        child: const Icon(
          Icons.auto_awesome_rounded,
          color: AppColors.primary,
          size: 32,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();

    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: switch (provider.status) {
              TripCreationStatus.failure => _FailureView(
                  message: provider.errorMessage ??
                      'Something went wrong while creating your trip.',
                  onRetry: _retry,
                  onBackToReview: _backToReview,
                ),
              TripCreationStatus.success => _SuccessView(
                  tripId: provider.createdTripId,
                  isOverBudget: provider.isOverBudget,
                ),
              _ => GenerationWaitNotice(
                  spinner: _buildSpinner(),
                  message: _messages[_messageIndex],
                  elapsed: _elapsed,
                  onCancel: _backToReview,
                ),
            },
          ),
        ),
      ),
    );
  }
}

class _FailureView extends StatelessWidget {
  const _FailureView({
    required this.message,
    required this.onRetry,
    required this.onBackToReview,
  });

  final String message;
  final VoidCallback onRetry;
  final VoidCallback onBackToReview;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.error_outline, size: 48, color: AppColors.error),
        const SizedBox(height: 16),
        Text(
          "We couldn't create your trip",
          style: AppTextStyles.headlineMd,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 8),
        Text(message, style: AppTextStyles.bodyMd, textAlign: TextAlign.center),
        const SizedBox(height: 24),
        PrimaryButton(label: 'Retry', onPressed: onRetry, fullWidth: false),
        const SizedBox(height: 12),
        SecondaryButton(label: 'Back to Review', onPressed: onBackToReview),
      ],
    );
  }
}

/// `POST /generate` now runs real AI generation (Gemini) and returns the
/// finished itinerary synchronously — by the time this view shows, Trip
/// Overview has real content to display, per UI Pages §8 ("auto-advance to
/// Trip Overview").
class _SuccessView extends StatelessWidget {
  const _SuccessView({required this.tripId, this.isOverBudget = false});

  final String? tripId;

  /// Backend over-budget signal for DESTINATION_FIRST plans (V-002 §5.3).
  final bool isOverBudget;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(
          Icons.check_circle_rounded,
          size: 48,
          color: AppColors.success,
        ),
        const SizedBox(height: 16),
        Text(
          'Your trip is ready!',
          style: AppTextStyles.headlineMd,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 8),
        Text(
          'Your personalized itinerary has been generated.',
          style: AppTextStyles.bodyMd.copyWith(color: AppColors.secondary),
          textAlign: TextAlign.center,
        ),
        if (isOverBudget) ...[
          const SizedBox(height: 20),
          const OverBudgetNotice(),
        ],
        const SizedBox(height: 24),
        PrimaryButton(
          label: 'View My Trip',
          fullWidth: false,
          onPressed: () {
            Navigator.of(context).pushNamedAndRemoveUntil(
              '/trip-overview',
              (route) => false,
              arguments: tripId,
            );
          },
        ),
      ],
    );
  }
}
