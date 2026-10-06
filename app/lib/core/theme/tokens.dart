import 'package:flutter/material.dart';

/// Design tokens of the "mystic" visual identity: a parchment / stone / ink
/// palette with gold, crimson, arcane and emerald accents.
///
/// Exposed as a [ThemeExtension] so widgets can read them with `context.tokens`
/// while the regular Material widgets keep using `Theme.of(context).colorScheme`.
@immutable
class AppTokens extends ThemeExtension<AppTokens> {
  const AppTokens({
    required this.parchment,
    required this.parchmentDeep,
    required this.stone,
    required this.ink,
    required this.inkMuted,
    required this.gold,
    required this.crimson,
    required this.arcane,
    required this.emerald,
    required this.rune,
  });

  /// Page background.
  final Color parchment;

  /// "Parchment" cards.
  final Color parchmentDeep;

  /// App bar and secondary cards.
  final Color stone;

  /// Primary text.
  final Color ink;

  /// Secondary text.
  final Color inkMuted;

  /// Accents, borders, active tab.
  final Color gold;

  /// Primary action, damage.
  final Color crimson;

  /// Magic, spells.
  final Color arcane;

  /// Healing, success.
  final Color emerald;

  /// Lines and outlines.
  final Color rune;

  static const light = AppTokens(
    parchment: Color(0xFFF3E9D2),
    parchmentDeep: Color(0xFFE6D6B4),
    stone: Color(0xFFD8D1C5),
    ink: Color(0xFF2B2118),
    inkMuted: Color(0xFF6B5B4B),
    gold: Color(0xFFB8860B),
    crimson: Color(0xFF8B1E1E),
    arcane: Color(0xFF4B3F8F),
    emerald: Color(0xFF2E6B3F),
    rune: Color(0xFF7A6A52),
  );

  static const dark = AppTokens(
    parchment: Color(0xFF1C1814),
    parchmentDeep: Color(0xFF26211B),
    stone: Color(0xFF332E28),
    ink: Color(0xFFEDE3CF),
    inkMuted: Color(0xFFA8997F),
    gold: Color(0xFFD4A83A),
    crimson: Color(0xFFC0392B),
    arcane: Color(0xFF8C7BE0),
    emerald: Color(0xFF5BBF7A),
    rune: Color(0xFF5A4E3C),
  );

  /// Tokens for the given [brightness].
  static AppTokens of(Brightness brightness) => brightness == Brightness.dark ? dark : light;

  @override
  AppTokens copyWith({
    Color? parchment,
    Color? parchmentDeep,
    Color? stone,
    Color? ink,
    Color? inkMuted,
    Color? gold,
    Color? crimson,
    Color? arcane,
    Color? emerald,
    Color? rune,
  }) => AppTokens(
    parchment: parchment ?? this.parchment,
    parchmentDeep: parchmentDeep ?? this.parchmentDeep,
    stone: stone ?? this.stone,
    ink: ink ?? this.ink,
    inkMuted: inkMuted ?? this.inkMuted,
    gold: gold ?? this.gold,
    crimson: crimson ?? this.crimson,
    arcane: arcane ?? this.arcane,
    emerald: emerald ?? this.emerald,
    rune: rune ?? this.rune,
  );

  @override
  AppTokens lerp(ThemeExtension<AppTokens>? other, double t) {
    if (other is! AppTokens) return this;
    return AppTokens(
      parchment: Color.lerp(parchment, other.parchment, t)!,
      parchmentDeep: Color.lerp(parchmentDeep, other.parchmentDeep, t)!,
      stone: Color.lerp(stone, other.stone, t)!,
      ink: Color.lerp(ink, other.ink, t)!,
      inkMuted: Color.lerp(inkMuted, other.inkMuted, t)!,
      gold: Color.lerp(gold, other.gold, t)!,
      crimson: Color.lerp(crimson, other.crimson, t)!,
      arcane: Color.lerp(arcane, other.arcane, t)!,
      emerald: Color.lerp(emerald, other.emerald, t)!,
      rune: Color.lerp(rune, other.rune, t)!,
    );
  }
}

extension AppTokensContext on BuildContext {
  /// The design tokens of the closest [Theme]; falls back to the tokens of the
  /// current brightness when the theme does not carry them (e.g. plain
  /// `ThemeData` in a test).
  AppTokens get tokens {
    final theme = Theme.of(this);
    return theme.extension<AppTokens>() ?? AppTokens.of(theme.brightness);
  }
}
