import 'package:flutter/material.dart';

/// Centralized color tokens.
/// Source of truth: 09_DESIGN.md front-matter + Figma file
/// (afPpgAPyJsEgYp83QbdMrl) — Login & Trip Overview screens.
/// Do NOT hardcode hex values in widgets — always reference AppColors.
class AppColors {
  AppColors._();

  // Surfaces
  static const Color surface = Color(0xFFFAF8FF);
  static const Color surfaceContainerLowest = Color(0xFFFFFFFF);
  static const Color surfaceContainerLow = Color(0xFFF2F3FF);
  static const Color surfaceContainer = Color(0xFFEAEDFF);
  static const Color surfaceContainerHigh = Color(0xFFE2E7FF);
  static const Color surfaceContainerHighest = Color(0xFFDAE2FD);

  // Text
  static const Color onSurface = Color(0xFF131B2E);
  static const Color onSurfaceVariant = Color(0xFF58413D);
  static const Color textMuted = Color(0xFF64748B);
  static const Color secondary = Color(0xFF49607C);

  // Primary / brand
  static const Color primary = Color(0xFFA83223);
  static const Color onPrimary = Color(0xFFFFFFFF);
  static const Color primaryContainerLight = Color(0xFFFFDAD4); // pill bg
  static const Color onPrimaryContainerLight = Color(0xFF410100); // pill text

  // Tertiary / AI badge
  static const Color tertiaryFixed = Color(0xFFDBE4EB); // AI-generated badge bg

  // Borders
  static const Color outlineVariant = Color(0xFFE0BFBA);
  static const Color borderSubtle = Color(0xFFE2E8F0);

  // Status (semantic — never used decoratively)
  static const Color success = Color(0xFF10B981);
  static const Color successBg = Color(0x2610B981); // ~15% alpha
  static const Color warning = Color(0xFFF59E0B);
  static const Color warningBg = Color(0x26F59E0B); // ~15% alpha
  static const Color error = Color(0xFFEF4444);
  static const Color errorBg = Color(0x26EF4444);
  static const Color formError = Color(0xFFBA1A1A);

  // Shadows (use with BoxShadow, not as fill colors)
  static const Color shadowAmbient = Color(0x0D0F2942); // rgba(15,41,66,0.05)
}
