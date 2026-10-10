import 'package:flutter/cupertino.dart' show CupertinoPageTransitionsBuilder;
import 'package:flutter/material.dart';

import '../motion/page_transitions.dart';
import 'app_style.dart';
import 'components.dart';
import 'contrast.dart';
import 'palettes.dart';
import 'tokens.dart';
import 'typography.dart';

export 'app_icon.dart';
export 'app_style.dart';
export 'components.dart' show ParchmentCard, RuneDivider, SectionHeader, StoneCard;
export 'contrast.dart';
export 'icons.dart';
export 'palettes.dart';
export 'textures.dart' show GrainBackground, GrainPainter, RuneBorderPainter, RuneCard;
export 'tokens.dart';
export 'typography.dart' show AppFontSet, AppFonts, AppTypography, BodyFont, TitleFont;

/// Dark (default) and light themes of the app ("carved stone" identity), in
/// the chosen [AppPalette], fonts and [AppStyle].
abstract final class AppTheme {
  static ThemeData light({
    AppPalette palette = AppPalette.ember,
    AppFontSet fonts = AppFontSet.standard,
    AppStyle style = AppStyle.standard,
  }) => build(Brightness.light, palette: palette, fonts: fonts, style: style);

  static ThemeData dark({
    AppPalette palette = AppPalette.ember,
    AppFontSet fonts = AppFontSet.standard,
    AppStyle style = AppStyle.standard,
  }) => build(Brightness.dark, palette: palette, fonts: fonts, style: style);

  /// The colour scheme of [palette] in [brightness]. The original palette
  /// keeps its hand-tuned containers; the others derive them from the tokens.
  static ColorScheme scheme(AppPalette palette, Brightness brightness) =>
      palette == AppPalette.ember
      ? _emberScheme(brightness)
      : _derivedScheme(palette.tokens(brightness), brightness, palette.tokens(_flip(brightness)));

  static Brightness _flip(Brightness b) =>
      b == Brightness.dark ? Brightness.light : Brightness.dark;

  /// A full [ColorScheme] from the tokens alone: containers are the accent
  /// blended into the cards, "on" colours the most readable of the palette's
  /// text and page colours (or black / white), and the error colour is
  /// [AppTokens.blood] moved towards the text colour until it reads on the
  /// page (WCAG AA).
  static ColorScheme _derivedScheme(AppTokens t, Brightness brightness, AppTokens other) {
    final dark = brightness == Brightness.dark;
    Color on(Color background) =>
        bestOn(background, [t.bone, t.obsidian, const Color(0xFFFFFFFF), const Color(0xFF000000)]);
    Color container(Color accent) => Color.alphaBlend(accent.withValues(alpha: 0.28), t.stone);
    final error = readableOn(t.blood, [t.obsidian], toward: t.bone);
    final primaryContainer = container(t.ember);
    final secondaryContainer = container(t.oldGold);
    final tertiaryContainer = container(t.arcane);
    final errorContainer = container(t.blood);
    return ColorScheme(
      brightness: brightness,
      primary: t.ember,
      onPrimary: on(t.ember),
      primaryContainer: primaryContainer,
      onPrimaryContainer: on(primaryContainer),
      secondary: t.oldGold,
      onSecondary: on(t.oldGold),
      secondaryContainer: secondaryContainer,
      onSecondaryContainer: on(secondaryContainer),
      tertiary: t.arcane,
      onTertiary: on(t.arcane),
      tertiaryContainer: tertiaryContainer,
      onTertiaryContainer: on(tertiaryContainer),
      error: error,
      onError: on(error),
      errorContainer: errorContainer,
      onErrorContainer: on(errorContainer),
      surface: t.obsidian,
      onSurface: t.bone,
      onSurfaceVariant: t.boneMuted,
      surfaceContainerLowest: Color.lerp(t.obsidian, dark ? Colors.black : Colors.white, 0.35)!,
      surfaceContainerLow: t.stone,
      surfaceContainer: Color.lerp(t.stone, t.stoneRaised, 0.5)!,
      surfaceContainerHigh: t.stoneRaised,
      surfaceContainerHighest: Color.lerp(t.stoneRaised, t.bone, 0.08)!,
      outline: t.rune,
      outlineVariant: Color.lerp(t.rune, t.obsidian, 0.35)!,
      shadow: const Color(0xFF000000),
      scrim: const Color(0xFF000000),
      inverseSurface: t.bone,
      onInverseSurface: t.obsidian,
      inversePrimary: other.ember,
      surfaceTint: Colors.transparent,
    );
  }

  static ColorScheme _emberScheme(Brightness brightness) {
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

  /// The theme of [palette] in [brightness] with [fonts] and [style]. Unless
  /// [activateFonts] is false (a preview), also makes [fonts] the
  /// [AppFonts.active] ones.
  static ThemeData build(
    Brightness brightness, {
    AppPalette palette = AppPalette.ember,
    AppFontSet fonts = AppFontSet.standard,
    AppStyle style = AppStyle.standard,
    bool activateFonts = true,
  }) {
    if (activateFonts) AppFonts.active = fonts;
    final tokens = palette.tokens(brightness);
    final scheme = AppTheme.scheme(palette, brightness);
    final text = AppTypography.textTheme(tokens, fonts);
    final components = AppComponentThemes(tokens, scheme, text, fonts);
    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: tokens.obsidian,
      canvasColor: tokens.obsidian,
      textTheme: text,
      extensions: [tokens, style],
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
