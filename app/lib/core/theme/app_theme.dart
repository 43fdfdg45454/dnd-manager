import 'package:flutter/cupertino.dart' show CupertinoPageTransitionsBuilder;
import 'package:flutter/material.dart';

import '../motion/page_transitions.dart';
import 'components.dart';
import 'tokens.dart';
import 'typography.dart';

export 'app_icon.dart';
export 'components.dart' show ParchmentCard, RuneDivider, SectionHeader, StoneCard;
export 'icons.dart';
export 'textures.dart' show GrainBackground, GrainPainter, RuneBorderPainter, RuneCard;
export 'tokens.dart';
export 'typography.dart' show AppFonts, AppTypography;

/// Dark (default) and light themes of the app ("carved stone" identity).
abstract final class AppTheme {
  static ThemeData light() => _build(Brightness.light);

  static ThemeData dark() => _build(Brightness.dark);

  static ColorScheme _scheme(Brightness brightness) {
    final t = AppTokens.of(brightness);
    if (brightness == Brightness.light) {
      return ColorScheme(
        brightness: brightness,
        primary: t.ember,
        onPrimary: const Color(0xFFFFFFFF),
        primaryContainer: const Color(0xFFF2CDB0),
        onPrimaryContainer: const Color(0xFF3D1A04),
        secondary: t.oldGold,
        onSecondary: const Color(0xFF14110F),
        secondaryContainer: const Color(0xFFE8D49F),
        onSecondaryContainer: const Color(0xFF2E2205),
        tertiary: t.arcane,
        onTertiary: const Color(0xFFFFFFFF),
        tertiaryContainer: const Color(0xFFDCD2F2),
        onTertiaryContainer: const Color(0xFF231747),
        error: t.blood,
        onError: const Color(0xFFFFFFFF),
        errorContainer: const Color(0xFFF2D0CC),
        onErrorContainer: const Color(0xFF3E0A0C),
        surface: t.obsidian,
        onSurface: t.bone,
        onSurfaceVariant: t.boneMuted,
        surfaceContainerLowest: const Color(0xFFF1E9D8),
        surfaceContainerLow: t.stone,
        surfaceContainer: const Color(0xFFDDCFB5),
        surfaceContainerHigh: t.stoneRaised,
        surfaceContainerHighest: const Color(0xFFCDBC9E),
        outline: t.rune,
        outlineVariant: const Color(0xFFC4B497),
        shadow: const Color(0xFF000000),
        scrim: const Color(0xFF000000),
        inverseSurface: t.bone,
        onInverseSurface: t.obsidian,
        inversePrimary: const Color(0xFFD9671E),
        surfaceTint: Colors.transparent,
      );
    }
    return ColorScheme(
      brightness: brightness,
      primary: t.ember,
      // White on ember is below 4.5:1; obsidian reads well.
      onPrimary: t.obsidian,
      primaryContainer: const Color(0xFF5A2A0C),
      onPrimaryContainer: const Color(0xFFFFD9BF),
      secondary: t.oldGold,
      onSecondary: t.obsidian,
      secondaryContainer: const Color(0xFF4D3C14),
      onSecondaryContainer: const Color(0xFFF0DDA8),
      tertiary: t.arcane,
      onTertiary: t.obsidian,
      tertiaryContainer: const Color(0xFF35295A),
      onTertiaryContainer: const Color(0xFFE6DDFF),
      // Blood is too dark to read as error text on obsidian.
      error: const Color(0xFFE5735F),
      onError: const Color(0xFF2B0606),
      errorContainer: const Color(0xFF5C1618),
      onErrorContainer: const Color(0xFFFFDAD6),
      surface: t.obsidian,
      onSurface: t.bone,
      onSurfaceVariant: t.boneMuted,
      surfaceContainerLowest: const Color(0xFF0E0C0A),
      surfaceContainerLow: t.stone,
      surfaceContainer: const Color(0xFF28211D),
      surfaceContainerHigh: t.stoneRaised,
      surfaceContainerHighest: const Color(0xFF372E29),
      outline: t.rune,
      outlineVariant: const Color(0xFF3A312B),
      shadow: const Color(0xFF000000),
      scrim: const Color(0xFF000000),
      inverseSurface: t.bone,
      onInverseSurface: t.obsidian,
      inversePrimary: const Color(0xFFB8581A),
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
      scaffoldBackgroundColor: tokens.obsidian,
      canvasColor: tokens.obsidian,
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
      segmentedButtonTheme: components.segmentedButton,
      bottomSheetTheme: components.bottomSheet,
      popupMenuTheme: components.popupMenu,
      pageTransitionsTheme: const PageTransitionsTheme(
        builders: {
          TargetPlatform.android: FadeSlidePageTransitionsBuilder(),
          // Keeps the iOS back swipe.
          TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.linux: FadeSlidePageTransitionsBuilder(),
          TargetPlatform.macOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.windows: FadeSlidePageTransitionsBuilder(),
          TargetPlatform.fuchsia: FadeSlidePageTransitionsBuilder(),
        },
      ),
    );
  }
}
