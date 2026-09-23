import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_text_styles.dart';

/// Base scaffold every Triply screen should build on top of, so
/// background color, app bar style, and safe-area handling stay
/// consistent app-wide (Design Principle 08 — Consistency Beats Novelty).
class AppScaffold extends StatelessWidget {
  const AppScaffold({
    super.key,
    required this.body,
    this.title,
    this.showBackButton = false,
    this.actions,
    this.bottomBar,
    this.floatingActionButton,
  });

  final Widget body;
  final String? title;
  final bool showBackButton;
  final List<Widget>? actions;
  final Widget? bottomBar;
  final Widget? floatingActionButton;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: title == null
          ? null
          : AppBar(
        backgroundColor: AppColors.surface.withValues(alpha: 0.8),
        elevation: 0,
        automaticallyImplyLeading: showBackButton,
        title: Text(title!, style: AppTextStyles.headlineSm),
        actions: actions,
      ),
      body: SafeArea(child: body),
      bottomNavigationBar: bottomBar,
      floatingActionButton: floatingActionButton,
    );
  }
}
