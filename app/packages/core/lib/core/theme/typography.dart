import 'package:flutter/foundation.dart' show defaultTargetPlatform;
import 'package:flutter/material.dart';

import 'tokens.dart';

/// Title fonts of the "Personalización" screen (bundled ones are SIL OFL, see
/// `assets/licenses`).
enum TitleFont {
  /// Static Regular and Bold files. The default.
  almendra('Almendra', family: 'Almendra'),

  /// Variable font, weight axis 400–900.
  cinzel('Cinzel', family: 'Cinzel'),

  /// Regular only: titles stay at 400 instead of a synthesized bold.
  imFellEnglish('IM Fell English', family: 'IMFellEnglish', hasBold: false),

  /// Titles in the body font.
  sameAsBody('Igual que el texto');

  const TitleFont(this.label, {this.family, this.hasBold = true});

  /// Name shown on the Personalización screen.
  final String label;

  /// Font family; null follows the body font.
  final String? family;

  /// Whether the family has a real bold weight.
  final bool hasBold;
}

/// Body fonts of the "Personalización" screen.
enum BodyFont {
  /// Variable font, weight axis 200–900. The default.
  sourceSans3('Source Sans 3', family: 'SourceSans3'),

  /// High legibility, variable font (weight axis 200–800).
  atkinsonHyperlegibleNext('Atkinson Hyperlegible Next', family: 'AtkinsonHyperlegibleNext'),

  /// Serif, variable font (weight axis 400–700).
  lora('Lora', family: 'Lora'),

  /// The platform font (not bundled).
  system('Fuente del sistema');

  const BodyFont(this.label, {this.family});

  /// Name shown on the Personalización screen.
  final String label;

  /// Bundled family; null for the platform font.
  final String? family;
}

/// The pair of fonts the app uses.
@immutable
class AppFontSet {
  const AppFontSet({this.title = TitleFont.almendra, this.body = BodyFont.sourceSans3});

  static const standard = AppFontSet();

  final TitleFont title;
  final BodyFont body;

  /// Family of body, label and numeric styles.
  String get bodyFamily => body.family ?? _systemFamily;

  /// Family of display, headline and title styles.
  String get titleFamily => title.family ?? bodyFamily;

  /// Weight of bold titles: 700, or 400 when the title family has no bold.
  FontWeight get titleBold => title.hasBold ? FontWeight.w700 : FontWeight.w400;

  /// The family Material uses on this platform (Roboto on Android).
  static String get _systemFamily =>
      Typography.material2021(platform: defaultTargetPlatform).black.bodyMedium?.fontFamily ??
      'Roboto';

  @override
  bool operator ==(Object other) =>
      other is AppFontSet && other.title == title && other.body == body;

  @override
  int get hashCode => Object.hash(title, body);
}

/// Bundled fonts (SIL OFL, see `assets/licenses`).
abstract final class AppFonts {
  /// Default display, headline and title family.
  static const display = 'Almendra';

  /// Default body, label and numeric family.
  static const body = 'SourceSans3';

  /// The fonts of the theme built last (`AppTheme` sets it), read by the
  /// helpers of [AppTypography] that have no `BuildContext`. The light and
  /// dark themes of the app always share the same fonts.
  static AppFontSet active = AppFontSet.standard;
}

/// Text theme of the app: the title font for display / headline / title, the
/// body font for body / label. Sizes are Material's defaults, only family,
/// weight and colour change, so existing layouts keep their metrics.
abstract final class AppTypography {
  /// A body-font style of the given [weight]. The bundled body fonts are
  /// variable: the weight axis must be set explicitly (fontWeight alone only
  /// picks a file, and every weight points at the same one).
  static TextStyle sans({
    FontWeight weight = FontWeight.w400,
    Color? color,
    double? fontSize,
    AppFontSet? fonts,
  }) => TextStyle(
    fontFamily: (fonts ?? AppFonts.active).bodyFamily,
    fontWeight: weight,
    fontVariations: [FontVariation.weight(weight.value.toDouble())],
    color: color,
    fontSize: fontSize,
  );

  /// Numbers that change in place (HP, slots, gold, modifiers): tabular
  /// figures so digits keep their width, semibold body font. Colour and size
  /// come from the style it is merged into.
  static TextStyle get numeric => numericOf(AppFonts.active);

  /// [numeric] in the body font of [fonts].
  static TextStyle numericOf(AppFontSet fonts) => TextStyle(
    fontFamily: fonts.bodyFamily,
    fontWeight: FontWeight.w600,
    fontVariations: const [FontVariation.weight(600)],
    fontFeatures: const [FontFeature.tabularFigures()],
  );

  /// A title-font style of the given [weight] (capped by [AppFontSet.titleBold]).
  static TextStyle title({FontWeight weight = FontWeight.w700, Color? color, AppFontSet? fonts}) {
    final set = fonts ?? AppFonts.active;
    final w = weight.value > set.titleBold.value ? set.titleBold : weight;
    return TextStyle(
      fontFamily: set.titleFamily,
      fontWeight: w,
      fontVariations: [FontVariation.weight(w.value.toDouble())],
      color: color,
    );
  }

  static TextTheme textTheme(AppTokens t, [AppFontSet fonts = AppFontSet.standard]) {
    TextStyle heading(Color color, [FontWeight weight = FontWeight.w700]) =>
        title(weight: weight, color: color, fonts: fonts);
    TextStyle body(Color color, [FontWeight weight = FontWeight.w400]) =>
        sans(weight: weight, color: color, fonts: fonts);

    return TextTheme(
      displayLarge: heading(t.bone, FontWeight.w400),
      displayMedium: heading(t.bone, FontWeight.w400),
      displaySmall: heading(t.bone, FontWeight.w400),
      headlineLarge: heading(t.bone),
      headlineMedium: heading(t.bone),
      headlineSmall: heading(t.bone),
      titleLarge: heading(t.bone),
      titleMedium: heading(t.bone),
      titleSmall: heading(t.bone),
      bodyLarge: body(t.bone),
      bodyMedium: body(t.bone),
      bodySmall: body(t.boneMuted),
      labelLarge: body(t.bone, FontWeight.w600),
      labelMedium: body(t.bone, FontWeight.w600),
      labelSmall: body(t.bone, FontWeight.w600),
    );
  }
}
