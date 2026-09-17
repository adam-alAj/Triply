import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_text_styles.dart';
import '../../providers/trip_creation_provider.dart';
import '../../widgets/primary_button.dart';
import '../../widgets/secondary_button.dart';

/// MOB-TRIP-08 — full-screen wait state while the trip is created and AI
/// generation is kicked off.
///
/// This screen (not Review) owns triggering the actual network calls, so
/// a slow or failed request never leaves Review's button stuck mid-press.
/// Every request already carries a bounded 15s timeout (see ApiClient), so
/// "never spins forever" is satisfied by the network layer itself — no
/// separate manual timeout timer needed here.
class GeneratingScreen extends StatefulWidget {
  const GeneratingScreen({super.key});

  @override
  State<GeneratingScreen> createState() => _GeneratingScreenState();
}

class _GeneratingScreenState extends State<GeneratingScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController _spinController;
  Timer? _messageTimer;
  int _messageIndex = 0;

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
    super.dispose();
  }

  void _retry() => context.read<TripCreationProvider>().submitTrip();

  void _backToReview() =>
      context.read<TripCreationProvider>().jumpToStep(5);

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
              TripCreationStatus.success =>
                _SuccessView(tripId: provider.createdTripId),
              _ => _LoadingView(
                  spinController: _spinController,
                  message: _messages[_messageIndex],
                  onCancel: _backToReview,
                ),
            },
          ),
        ),
      ),
    );
  }
}

class _LoadingView extends StatelessWidget {
  const _LoadingView({
    required this.spinController,
    required this.message,
    required this.onCancel,
  });

  final AnimationController spinController;
  final String message;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        RotationTransition(
          turns: spinController,
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
        ),
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
        const SizedBox(height: 36),
        SecondaryButton(label: 'Cancel', onPressed: onCancel),
      ],
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

/// PENDING BACKEND: real AI itinerary generation isn't wired yet — today
/// `POST /generate` only flips the trip's status. Once it produces real
/// content, success here should navigate straight to Trip Overview
/// (MOB-TRIP-09) instead of back to Home.
class _SuccessView extends StatelessWidget {
  const _SuccessView({required this.tripId});

  final String? tripId;

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
          'Your trip is on its way!',
          style: AppTextStyles.headlineMd,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 8),
        Text(
          'Trip #$tripId was created and generation has started.',
          style: AppTextStyles.bodyMd.copyWith(color: AppColors.secondary),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 24),
        PrimaryButton(
          label: 'Back to Home',
          fullWidth: false,
          onPressed: () {
            Navigator.of(context).pushNamedAndRemoveUntil(
              '/home',
              (route) => false,
            );
          },
        ),
      ],
    );
  }
}
