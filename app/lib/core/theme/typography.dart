import 'package:flutter/material.dart';

import 'tokens.dart';

/// Bundled fonts (SIL OFL, see `assets/licenses`).
abstract final class AppFonts {
  /// Display, headline and title styles (static Regular and Bold files).
  static const display = 'Almendra';

  /// Body, label and numeric styles (variable font, weight axis 200–900).
  static const body = 'SourceSans3';
}

/// Text theme of the app: Almendra for display / headline / title, Source Sans
/// 3 for body / label. Sizes are Material's defaults, only family, weight and
/// colour change, so existing layouts keep their metrics.
abstract final class AppTypography {
  /// A Source Sans 3 style of the given [weight]. The bundled file is a
  /// variable font: the weight axis must be set explicitly (fontWeight alone
  /// only picks a file, and every weight points at the same one).
  static TextStyle sans({FontWeight weight = FontWeight.w400, Color? color, double? fontSize}) =>
      TextStyle(
        fontFamily: AppFonts.body,
        fontWeight: weight,
        fontVariations: [FontVariation.weight(weight.value.toDouble())],
        color: color,
        fontSize: fontSize,
      );

  /// Numbers that change in place (HP, slots, gold, modifiers): tabular
  /// figures so digits keep their width, semibold Source Sans 3. Colour and
  /// size come from the style it is merged into.
  static const numeric = TextStyle(
    fontFamily: AppFonts.body,
    fontWeight: FontWeight.w600,
    fontVariations: [FontVariation.weight(600)],
    fontFeatures: [FontFeature.tabularFigures()],
  );

  static TextTheme textTheme(AppTokens t) {
    TextStyle title(Color color, [FontWeight weight = FontWeight.w700]) =>
        TextStyle(fontFamily: AppFonts.display, fontWeight: weight, color: color);

    return TextTheme(
      displayLarge: title(t.bone, FontWeight.w400),
      displayMedium: title(t.bone, FontWeight.w400),
      displaySmall: title(t.bone, FontWeight.w400),
      headlineLarge: title(t.bone),
      headlineMedium: title(t.bone),
      headlineSmall: title(t.bone),
      titleLarge: title(t.bone),
      titleMedium: title(t.bone),
      titleSmall: title(t.bone),
      bodyLarge: sans(color: t.bone),
      bodyMedium: sans(color: t.bone),
      bodySmall: sans(color: t.boneMuted),
      labelLarge: sans(color: t.bone, weight: FontWeight.w600),
      labelMedium: sans(color: t.bone, weight: FontWeight.w600),
      labelSmall: sans(color: t.bone, weight: FontWeight.w600),
    );
  }
}
