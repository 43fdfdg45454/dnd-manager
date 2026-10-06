import 'package:flutter/material.dart';

import 'components.dart';
import 'tokens.dart';
import 'typography.dart';

export 'app_icon.dart';
export 'components.dart' show ParchmentCard, RuneDivider, SectionHeader, StoneCard;
export 'icons.dart';
export 'tokens.dart';

/// Light and dark themes of the app ("mystic" parchment identity).
abstract final class AppTheme {
  static ThemeData light() => _build(Brightness.light);

  static ThemeData dark() => _build(Brightness.dark);

  static ColorScheme _scheme(Brightness brightness) {
    final t = AppTokens.of(brightness);
    if (brightness == Brightness.light) {
      return ColorScheme(
        brightness: brightness,
        primary: t.crimson,
        onPrimary: const Color(0xFFFFF8EE),
        primaryContainer: const Color(0xFFF1D6D0),
        onPrimaryContainer: const Color(0xFF4A0E0E),
        secondary: t.gold,
        onSecondary: const Color(0xFF1C1814),
        secondaryContainer: const Color(0xFFEBD9A4),
        onSecondaryContainer: const Color(0xFF3A2A00),
        tertiary: t.arcane,
        onTertiary: const Color(0xFFFFF8EE),
        tertiaryContainer: const Color(0xFFDAD4F2),
        onTertiaryContainer: const Color(0xFF1F1647),
        error: const Color(0xFFB3261E),
        onError: const Color(0xFFFFFFFF),
        errorContainer: const Color(0xFFF9DAD6),
        onErrorContainer: const Color(0xFF410E0B),
        surface: t.parchment,
        onSurface: t.ink,
        onSurfaceVariant: t.inkMuted,
        surfaceContainerLowest: const Color(0xFFFAF4E4),
        surfaceContainerLow: t.parchmentDeep,
        surfaceContainer: const Color(0xFFDFD3BA),
        surfaceContainerHigh: t.stone,
        surfaceContainerHighest: const Color(0xFFCBC3B4),
        outline: t.rune,
        outlineVariant: const Color(0xFFCDBFA3),
        shadow: const Color(0xFF000000),
        scrim: const Color(0xFF000000),
        inverseSurface: t.ink,
        onInverseSurface: t.parchment,
        inversePrimary: const Color(0xFFE08A7F),
        surfaceTint: Colors.transparent,
      );
    }
    return ColorScheme(
      brightness: brightness,
      primary: t.crimson,
      onPrimary: const Color(0xFFFFF8EE),
      primaryContainer: const Color(0xFF5E1B14),
      onPrimaryContainer: const Color(0xFFFFDAD3),
      secondary: t.gold,
      onSecondary: const Color(0xFF1C1814),
      secondaryContainer: const Color(0xFF54420F),
      onSecondaryContainer: const Color(0xFFF3DFA0),
      tertiary: t.arcane,
      onTertiary: const Color(0xFF1C1814),
      tertiaryContainer: const Color(0xFF352C6B),
      onTertiaryContainer: const Color(0xFFE4DEFF),
      error: const Color(0xFFFF8A80),
      onError: const Color(0xFF3A0A06),
      errorContainer: const Color(0xFF5C1A15),
      onErrorContainer: const Color(0xFFFFDAD6),
      surface: t.parchment,
      onSurface: t.ink,
      onSurfaceVariant: t.inkMuted,
      surfaceContainerLowest: const Color(0xFF14110E),
      surfaceContainerLow: t.parchmentDeep,
      surfaceContainer: const Color(0xFF2D2822),
      surfaceContainerHigh: t.stone,
      surfaceContainerHighest: const Color(0xFF3D372F),
      outline: t.rune,
      outlineVariant: const Color(0xFF443B2E),
      shadow: const Color(0xFF000000),
      scrim: const Color(0xFF000000),
      inverseSurface: t.ink,
      onInverseSurface: t.parchment,
      inversePrimary: const Color(0xFF8B1E1E),
      surfaceTint: Colors.transparent,
    );
  }

  static ThemeData _build(Brightness brightness) {
    final tokens = AppTokens.of(brightness);
    final scheme = _scheme(brightness);
    final text = AppTypography.textTheme(tokens);
    final components = AppComponentThemes(tokens, scheme, text);
    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: tokens.parchment,
      canvasColor: tokens.parchment,
      textTheme: text,
      extensions: [tokens],
      appBarTheme: components.appBar,
      cardTheme: components.card,
      filledButtonTheme: components.filledButton,
      outlinedButtonTheme: components.outlinedButton,
      textButtonTheme: components.textButton,
      floatingActionButtonTheme: components.floatingActionButton,
      tabBarTheme: components.tabBar,
      chipTheme: components.chip,
      navigationBarTheme: components.navigationBar,
      progressIndicatorTheme: components.progress,
      dividerTheme: components.divider,
      snackBarTheme: components.snackBar,
      dialogTheme: components.dialog,
    );
  }
}
