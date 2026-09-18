import 'package:flutter/material.dart';

import '../../core/storage/token_storage.dart';

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _logoScale;
  late final Animation<double> _logoFade;

  @override
  void initState() {
    super.initState();

    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 2500),
    );

    _logoScale = Tween<double>(
      begin: 0.25,
      end: 1.0,
    ).animate(
      CurvedAnimation(
        parent: _controller,
        curve: Curves.easeOutCubic,
      ),
    );

    _logoFade = Tween<double>(
      begin: 0.0,
      end: 1.0,
    ).animate(
      CurvedAnimation(
        parent: _controller,
        curve: const Interval(
          0.0,
          0.45,
          curve: Curves.easeIn,
        ),
      ),
    );

    _controller.forward();

    Future.delayed(const Duration(milliseconds: 4500), () async {
      if (!mounted) return;

      // Session restore (UI Pages §8): a saved token means the user already
      // logged in on this device — skip Onboarding/Login and go straight to
      // Home. There's no `GET /api/users/me` yet (flagged separately), so
      // this only restores the *session*, not the profile fields; Profile
      // still shows a generic name until that endpoint exists.
      final hasSession = await TokenStorage().readToken() != null;
      if (!mounted) return;

      Navigator.pushReplacementNamed(
        context,
        hasSession ? '/home' : '/onboarding',
      );
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final screenHeight = MediaQuery.sizeOf(context).height;

    return Scaffold(
      body: Stack(
        fit: StackFit.expand,
        children: [
          // Splash background
          Image.asset(
            'assets/images/splash.png',
            fit: BoxFit.cover,
          ),

          // Triply logo
          Positioned(
            top: screenHeight * 0.30,
            left: 0,
            right: 0,
            child: FadeTransition(
              opacity: _logoFade,
              child: ScaleTransition(
                scale: _logoScale,
                child: Center(
                  child: Image.asset(
                    'assets/images/Triply Logo.png',
                    width: 185,
                  ),
                ),
              ),
            ),
          ),

          // Tagline
          Positioned(
            top: screenHeight * 0.50,
            left: 0,
            right: 0,
            child: RichText(
              textAlign: TextAlign.center,
              text: const TextSpan(
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w400,
                ),
                children: [
                  TextSpan(
                    text: 'Plan less. ',
                    style: TextStyle(
                      color: Color(0xFF7D86A3),
                    ),
                  ),
                  TextSpan(
                    text: 'Travel better.',
                    style: TextStyle(
                      color: Color(0xFFB94735),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}