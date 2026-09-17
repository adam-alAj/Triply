import 'package:flutter/material.dart';

import 'onboarding_screen.dart';

class OnboardingFlow extends StatefulWidget {
  const OnboardingFlow({super.key});

  @override
  State<OnboardingFlow> createState() => _OnboardingFlowState();
}

class _OnboardingFlowState extends State<OnboardingFlow> {
  final PageController _pageController = PageController();

  int _currentPage = 0;

  final List<OnboardingData> _pages = const [
    OnboardingData(
      title: 'Discover places\nworth going',
      description:
      'Explore destinations that match\nyour interests and travel style.',
      imagePath: 'assets/images/onboarding1.png',
      bottomText: 'New places.\nGreater stories.',
      buttonLabel: 'Continue',
    ),
    OnboardingData(
      title: 'A trip made for you',
      description:
      'Triply shapes your journey\naround what you love.',
      imagePath: 'assets/images/onboarding2.png',
      bottomText: 'Your interests.\nMeaningful journeys.',
      buttonLabel: 'Continue',
    ),
    OnboardingData(
      title: 'Plan with confidence',
      description:
      'Build a thoughtful itinerary with a\nclear view of your estimated costs.',
      imagePath: 'assets/images/onboarding3.png',
      bottomText: 'Better plans.\nBrighter trips.',
      buttonLabel: 'Get Started',
    ),
  ];

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  void _nextPage() {
    if (_currentPage < _pages.length - 1) {
      _pageController.nextPage(
        duration: const Duration(milliseconds: 350),
        curve: Curves.easeInOut,
      );
    } else {
      Navigator.pushReplacementNamed(context, '/login');
    }
  }

  void _skip() {
    Navigator.pushReplacementNamed(context, '/login');
  }

  @override
  Widget build(BuildContext context) {
    return PageView.builder(
      controller: _pageController,
      itemCount: _pages.length,
      onPageChanged: (index) {
        setState(() {
          _currentPage = index;
        });
      },
      itemBuilder: (context, index) {
        final page = _pages[index];

        return OnboardingScreen(
          title: page.title,
          description: page.description,
          imagePath: page.imagePath,
          bottomText: page.bottomText,
          currentPage: _currentPage,
          buttonLabel: page.buttonLabel,
          onButtonPressed: _nextPage,
          onSkipPressed: _skip,
        );
      },
    );
  }
}

class OnboardingData {
  const OnboardingData({
    required this.title,
    required this.description,
    required this.imagePath,
    required this.bottomText,
    required this.buttonLabel,
  });

  final String title;
  final String description;
  final String imagePath;
  final String bottomText;
  final String buttonLabel;
}