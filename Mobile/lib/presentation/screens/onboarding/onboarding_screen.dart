import 'package:flutter/material.dart';

class OnboardingScreen extends StatelessWidget {
  const OnboardingScreen({
    super.key,
    required this.title,
    required this.description,
    required this.imagePath,
    required this.bottomText,
    required this.currentPage,
    required this.buttonLabel,
    required this.onButtonPressed,
    required this.onSkipPressed,
  });

  final String title;
  final String description;
  final String imagePath;
  final String bottomText;
  final int currentPage;
  final String buttonLabel;
  final VoidCallback onButtonPressed;
  final VoidCallback onSkipPressed;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFAF8FF),
      body: SafeArea(
        child: Column(
          children: [
            // Skip
            Align(
              alignment: Alignment.topRight,
              child: Padding(
                padding: const EdgeInsets.only(
                  top: 14,
                  right: 18,
                ),
                child: GestureDetector(
                  onTap: onSkipPressed,
                  child: const Text(
                    'Skip',
                    style: TextStyle(
                      color: Color(0xFF7D86A3),
                      fontSize: 12,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ),
              ),
            ),

            // Title + description
            Padding(
              padding: const EdgeInsets.fromLTRB(
                18,
                22,
                18,
                0,
              ),
              child: SizedBox(
                width: double.infinity,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      textAlign: TextAlign.left,
                      style: const TextStyle(
                        color: Color(0xFF10213F),
                        fontSize: 22,
                        height: 1.12,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      description,
                      textAlign: TextAlign.left,
                      style: const TextStyle(
                        color: Color(0xFF7D86A3),
                        fontSize: 12,
                        height: 1.4,
                      ),
                    ),
                  ],
                ),
              ),
            ),

            const SizedBox(height: 10),

            // Illustration
            Expanded(
              child: Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 8,
                ),
                child: Image.asset(
                  imagePath,
                  fit: BoxFit.contain,
                ),
              ),
            ),

            // Bottom description
            Padding(
              padding: const EdgeInsets.symmetric(
                horizontal: 18,
              ),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      bottomText,
                      style: const TextStyle(
                        color: Color(0xFF7D86A3),
                        fontSize: 10,
                        height: 1.3,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Container(
                      width: 14,
                      height: 2,
                      color: const Color(0xFFB94735),
                    ),
                  ],
                ),
              ),
            ),

            const SizedBox(height: 12),

            // Page indicators
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(
                3,
                    (index) {
                  final isActive = index == currentPage;

                  return Container(
                    width: isActive ? 7 : 6,
                    height: isActive ? 7 : 6,
                    margin: const EdgeInsets.symmetric(
                      horizontal: 4,
                    ),
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: isActive
                          ? const Color(0xFFB94735)
                          : const Color(0xFFD9DCE8),
                    ),
                  );
                },
              ),
            ),

            const SizedBox(height: 18),

            // Continue button
            Padding(
              padding: const EdgeInsets.symmetric(
                horizontal: 16,
              ),
              child: SizedBox(
                width: double.infinity,
                height: 48,
                child: ElevatedButton(
                  onPressed: onButtonPressed,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFFB94735),
                    foregroundColor: Colors.white,
                    elevation: 0,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(24),
                    ),
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(
                        buttonLabel,
                        style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                      const SizedBox(width: 7),
                      const Icon(
                        Icons.arrow_forward,
                        size: 17,
                      ),
                    ],
                  ),
                ),
              ),
            ),

            const SizedBox(height: 18),
          ],
        ),
      ),
    );
  }
}