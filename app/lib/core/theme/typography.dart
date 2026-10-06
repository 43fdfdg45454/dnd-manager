import 'package:flutter/material.dart';

import 'tokens.dart';

/// Bundled fonts (SIL OFL, see `assets/licenses`).
abstract final class AppFonts {
  /// Display, headline and title styles.
  static const display = 'Cinzel';

  /// Body and label styles.
  static const body = 'Alegreya';
}

/// Text theme of the app: Cinzel for display / headline / title, Alegreya for
/// body / label. Sizes are Material's defaults, only family, weight and colour
/// change, so existing layouts keep their metrics.
abstract final class AppTypography {
  static TextTheme textTheme(AppTokens t) {
    // The bundled files are variable fonts: the weight axis must be set explicitly
    // (fontWeight alone only picks a file, and both weights point at the same one).
    TextStyle title(Color color, [FontWeight weight = FontWeight.w700]) => TextStyle(
      fontFamily: AppFonts.display,
      fontWeight: weight,
      fontVariations: [FontVariation.weight(weight.value.toDouble())],
      color: color,
    );
    TextStyle body(Color color, [FontWeight weight = FontWeight.w400]) => TextStyle(
      fontFamily: AppFonts.body,
      fontWeight: weight,
      fontVariations: [FontVariation.weight(weight.value.toDouble())],
      color: color,
    );

    return TextTheme(
      displayLarge: title(t.ink),
      displayMedium: title(t.ink),
      displaySmall: title(t.ink),
      headlineLarge: title(t.ink),
      headlineMedium: title(t.ink),
      headlineSmall: title(t.ink),
      titleLarge: title(t.ink),
      titleMedium: title(t.ink),
      titleSmall: title(t.ink),
      bodyLarge: body(t.ink),
      bodyMedium: body(t.ink),
      bodySmall: body(t.inkMuted),
      labelLarge: body(t.ink, FontWeight.w700),
      labelMedium: body(t.ink, FontWeight.w700),
      labelSmall: body(t.ink, FontWeight.w700),
    );
  }
}
