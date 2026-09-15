import 'package:flutter/material.dart';

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

    Future.delayed(const Duration(milliseconds: 4500), () {
      if (!mounted) return;

      Navigator.pushReplacementNamed(
        context,
        '/onboarding',
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