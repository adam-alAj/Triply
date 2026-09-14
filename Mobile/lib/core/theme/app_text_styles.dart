import 'package:flutter/material.dart';
import 'app_colors.dart';

/// Typography tokens per 09_DESIGN.md.
/// NOTE: 'Epilogue' and 'Work Sans' must be registered in pubspec.yaml
/// under flutter > fonts for these to render correctly. If not yet
/// registered, Flutter will silently fall back to the platform default —
/// this is a pubspec.yaml follow-up outside this task's scope.
class AppTextStyles {
  AppTextStyles._();

  static const String _display = 'Epilogue';
  static const String _body = 'Work Sans';

  static const TextStyle headlineLg = TextStyle(
    fontFamily: _display,
    fontSize: 28,
    fontWeight: FontWeight.w700,
    height: 34 / 28,
    letterSpacing: -0.02 * 28,
    color: AppColors.onSurface,
  );

  static const TextStyle headlineMd = TextStyle(
    fontFamily: _display,
    fontSize: 20,
    fontWeight: FontWeight.w600,
    height: 26 / 20,
    letterSpacing: -0.01 * 20,
    color: AppColors.onSurface,
  );

  static const TextStyle headlineSm = TextStyle(
    fontFamily: _display,
    fontSize: 18,
    fontWeight: FontWeight.w600,
    height: 24 / 18,
    color: AppColors.onSurface,
  );

  static const TextStyle bodyLg = TextStyle(
    fontFamily: _body,
    fontSize: 17,
    fontWeight: FontWeight.w400,
    height: 26 / 17,
    color: AppColors.onSurface,
  );

  static const TextStyle bodyMd = TextStyle(
    fontFamily: _body,
    fontSize: 15,
    fontWeight: FontWeight.w400,
    height: 22 / 15,
    color: AppColors.secondary,
  );

  static const TextStyle bodySm = TextStyle(
    fontFamily: _body,
    fontSize: 13,
    fontWeight: FontWeight.w400,
    height: 18 / 13,
    color: AppColors.secondary,
  );

  static const TextStyle labelLg = TextStyle(
    fontFamily: _body,
    fontSize: 15,
    fontWeight: FontWeight.w600,
    height: 20 / 15,
    letterSpacing: 0.01 * 15,
    color: AppColors.onSurface,
  );

  static const TextStyle labelMd = TextStyle(
    fontFamily: _body,
    fontSize: 13,
    fontWeight: FontWeight.w600,
    height: 18 / 13,
    letterSpacing: 0.02 * 13,
    color: AppColors.onSurface,
  );

  static const TextStyle labelSm = TextStyle(
    fontFamily: _body,
    fontSize: 11,
    fontWeight: FontWeight.w500,
    height: 14 / 11,
    letterSpacing: 0.04 * 11,
    color: AppColors.textMuted,
  );
}
