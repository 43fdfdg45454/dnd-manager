import 'package:flutter/material.dart';

import 'contrast.dart';

/// Design tokens of the "carved stone" identity: an obsidian / stone / bone
/// palette with ember, arcane, moss, blood and old-gold accents.
///
/// Field names follow the dark palette (the default); every field is a role,
/// so in the [light] variant `obsidian` holds the page background (bone),
/// `stone` the cards (clay) and `bone` the text (obsidian).
///
/// Exposed as a [ThemeExtension] so widgets can read them with `context.tokens`
/// while the regular Material widgets keep using `Theme.of(context).colorScheme`.
/// The names of the previous parchment palette ([parchment], [ink], [gold]…)
/// remain as aliases of the new roles.
@immutable
class AppTokens extends ThemeExtension<AppTokens> {
  const AppTokens({
    required this.obsidian,
    required this.stone,
    required this.stoneRaised,
    required this.bone,
    required this.boneMuted,
    required this.ember,
    required this.arcane,
    required this.moss,
    required this.blood,
    required this.oldGold,
    required this.rune,
  });

  /// Page background.
  final Color obsidian;

  /// Cards.
  final Color stone;

  /// Raised surfaces: app bar, secondary cards, sheets.
  final Color stoneRaised;

  /// Primary text.
  final Color bone;

  /// Secondary text.
  final Color boneMuted;

  /// Primary action.
  final Color ember;

  /// Magic, spells.
  final Color arcane;

  /// Healing, success.
  final Color moss;

  /// Damage, danger.
  final Color blood;

  /// Borders, seals, highlights.
  final Color oldGold;

  /// Lines and outlines.
  final Color rune;

  // Aliases of the previous parchment palette, kept so existing widgets map
  // onto the new roles.

  /// Page background (alias of [obsidian]).
  Color get parchment => obsidian;

  /// Cards (alias of [stone]).
  Color get parchmentDeep => stone;

  /// Primary text (alias of [bone]).
  Color get ink => bone;

  /// Secondary text (alias of [boneMuted]).
  Color get inkMuted => boneMuted;

  /// Accents and borders (alias of [oldGold]).
  Color get gold => oldGold;

  /// Primary action (alias of [ember]); damage now uses [blood].
  Color get crimson => ember;

  /// Healing (alias of [moss]).
  Color get emerald => moss;

  // Text variants of the semantic accents: the accent itself when it already
  // reads (WCAG AA, 4.5:1) on the page, card and raised surfaces, otherwise
  // moved towards [bone] just enough. Same meaning and hue family, safe for
  // labels and numbers.

  /// Primary action text.
  Color get emberText => _readable(ember);

  /// Healing / success text.
  Color get mossText => _readable(moss);

  /// Damage / danger text.
  Color get bloodText => _readable(blood);

  /// Magic text.
  Color get arcaneText => _readable(arcane);

  /// Highlight / warning text.
  Color get oldGoldText => _readable(oldGold);

  Color _readable(Color color) => readableOn(color, [obsidian, stone, stoneRaised], toward: bone);

  /// Dark variant of the default palette ("Obsidiana y brasa"); the other
  /// palettes live in `palettes.dart`.
  static const dark = AppTokens(
    obsidian: Color(0xFF14110F),
    stone: Color(0xFF231D19),
    stoneRaised: Color(0xFF2D2622),
    bone: Color(0xFFE7DCC6),
    boneMuted: Color(0xFFA89C87),
    ember: Color(0xFFD9671E),
    arcane: Color(0xFF8A6BD1),
    moss: Color(0xFF5E7A4A),
    blood: Color(0xFF9B2226),
    oldGold: Color(0xFFB8923A),
    rune: Color(0xFF4A3F36),
  );

  /// Light variant: bone background, clay cards, obsidian text and the same
  /// accents darkened by 15 %.
  static const light = AppTokens(
    obsidian: Color(0xFFE7DCC6),
    stone: Color(0xFFD8C9AE),
    stoneRaised: Color(0xFFE0D3BA),
    bone: Color(0xFF14110F),
    boneMuted: Color(0xFF574B40),
    ember: Color(0xFFB8581A),
    arcane: Color(0xFF755BB2),
    moss: Color(0xFF50683F),
    blood: Color(0xFF841D20),
    oldGold: Color(0xFF9C7C31),
    rune: Color(0xFF9A8B74),
  );

  /// Tokens for the given [brightness].
  static AppTokens of(Brightness brightness) => brightness == Brightness.dark ? dark : light;

  @override
  AppTokens copyWith({
    Color? obsidian,
    Color? stone,
    Color? stoneRaised,
    Color? bone,
    Color? boneMuted,
    Color? ember,
    Color? arcane,
    Color? moss,
    Color? blood,
    Color? oldGold,
    Color? rune,
  }) => AppTokens(
    obsidian: obsidian ?? this.obsidian,
    stone: stone ?? this.stone,
    stoneRaised: stoneRaised ?? this.stoneRaised,
    bone: bone ?? this.bone,
    boneMuted: boneMuted ?? this.boneMuted,
    ember: ember ?? this.ember,
    arcane: arcane ?? this.arcane,
    moss: moss ?? this.moss,
    blood: blood ?? this.blood,
    oldGold: oldGold ?? this.oldGold,
    rune: rune ?? this.rune,
  );

  @override
  AppTokens lerp(ThemeExtension<AppTokens>? other, double t) {
    if (other is! AppTokens) return this;
    return AppTokens(
      obsidian: Color.lerp(obsidian, other.obsidian, t)!,
      stone: Color.lerp(stone, other.stone, t)!,
      stoneRaised: Color.lerp(stoneRaised, other.stoneRaised, t)!,
      bone: Color.lerp(bone, other.bone, t)!,
      boneMuted: Color.lerp(boneMuted, other.boneMuted, t)!,
      ember: Color.lerp(ember, other.ember, t)!,
      arcane: Color.lerp(arcane, other.arcane, t)!,
      moss: Color.lerp(moss, other.moss, t)!,
      blood: Color.lerp(blood, other.blood, t)!,
      oldGold: Color.lerp(oldGold, other.oldGold, t)!,
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
