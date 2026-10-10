import 'package:flutter/material.dart';

import 'tokens.dart';

/// Colour palettes of the "Personalización" screen. Each one fills every
/// [AppTokens] role in a dark and a light variant; the theme mode picks the
/// variant. Semantic roles keep their meaning everywhere: [AppTokens.moss] is
/// healing, [AppTokens.blood] damage and [AppTokens.arcane] magic.
///
/// Every variant is checked by a test: [AppTokens.bone] and
/// [AppTokens.boneMuted] reach WCAG AA on the three surfaces and the button
/// text reaches it on [AppTokens.ember].
enum AppPalette {
  /// The original identity: obsidian, ember and old gold.
  ember(
    'Obsidiana y brasa',
    'Piedra negra, brasa naranja y oro viejo.',
    dark: AppTokens.dark,
    light: AppTokens.light,
  ),

  /// Black and greys (dark) / grey and black (light), without orange or gold.
  /// Semantic accents are desaturated but keep their hue.
  graphite(
    'Grafito',
    'Negro y grises, sin naranja ni dorado. Sobrio y neutro.',
    dark: AppTokens(
      obsidian: Color(0xFF0E0E10),
      stone: Color(0xFF18181B),
      stoneRaised: Color(0xFF222226),
      bone: Color(0xFFECECEE),
      boneMuted: Color(0xFFA6A6AD),
      ember: Color(0xFFD6D6DB),
      arcane: Color(0xFF9D98C6),
      moss: Color(0xFF86A68B),
      blood: Color(0xFFC46A6A),
      oldGold: Color(0xFF8C8C94),
      rune: Color(0xFF3A3A40),
    ),
    light: AppTokens(
      obsidian: Color(0xFFD6D6D9),
      stone: Color(0xFFE6E6E8),
      stoneRaised: Color(0xFFEDEDEF),
      bone: Color(0xFF111113),
      boneMuted: Color(0xFF46464C),
      ember: Color(0xFF1C1C1F),
      arcane: Color(0xFF4F4A7E),
      moss: Color(0xFF3B5E43),
      blood: Color(0xFF8A3A3A),
      oldGold: Color(0xFF55555C),
      rune: Color(0xFF9C9CA3),
    ),
  ),

  /// Warm black with a golden primary and an orange secondary.
  gold(
    'Oro y fuego',
    'Negro cálido, oro y llamas naranjas.',
    dark: AppTokens(
      obsidian: Color(0xFF15110A),
      stone: Color(0xFF221C12),
      stoneRaised: Color(0xFF2C2418),
      bone: Color(0xFFF0E3C4),
      boneMuted: Color(0xFFB3A27E),
      ember: Color(0xFFD9AE38),
      arcane: Color(0xFF9A86DE),
      moss: Color(0xFF7FA05C),
      blood: Color(0xFFD0463E),
      oldGold: Color(0xFFE07A2E),
      rune: Color(0xFF4D3F28),
    ),
    light: AppTokens(
      obsidian: Color(0xFFF6F0E1),
      stone: Color(0xFFECE2CA),
      stoneRaised: Color(0xFFF2EAD6),
      bone: Color(0xFF1E170C),
      boneMuted: Color(0xFF5E4F33),
      ember: Color(0xFF7E5F0C),
      arcane: Color(0xFF5E4BA6),
      moss: Color(0xFF466630),
      blood: Color(0xFF9A2420),
      oldGold: Color(0xFFA8521A),
      rune: Color(0xFFB9A780),
    ),
  ),

  /// Near-black green with moss and bronze.
  forest(
    'Bosque',
    'Verde casi negro, musgo y bronce.',
    dark: AppTokens(
      obsidian: Color(0xFF0D130F),
      stone: Color(0xFF17201A),
      stoneRaised: Color(0xFF1F2A22),
      bone: Color(0xFFE3E8DA),
      boneMuted: Color(0xFF9FAA98),
      ember: Color(0xFF8FB26C),
      arcane: Color(0xFF9A93DA),
      moss: Color(0xFF76B571),
      blood: Color(0xFFCF5A4E),
      oldGold: Color(0xFFB08D57),
      rune: Color(0xFF34433A),
    ),
    light: AppTokens(
      obsidian: Color(0xFFEEF1E4),
      stone: Color(0xFFE1E7D3),
      stoneRaised: Color(0xFFE8ECDD),
      bone: Color(0xFF142019),
      boneMuted: Color(0xFF4A5A4E),
      ember: Color(0xFF2F5D34),
      arcane: Color(0xFF5A4FA0),
      moss: Color(0xFF2F6E35),
      blood: Color(0xFF9A2B24),
      oldGold: Color(0xFF7E6230),
      rune: Color(0xFFA7B39C),
    ),
  ),

  /// Night blue with violet and silver.
  arcane(
    'Arcano',
    'Azul noche, violeta y plata.',
    dark: AppTokens(
      obsidian: Color(0xFF0D1020),
      stone: Color(0xFF161A2E),
      stoneRaised: Color(0xFF1E2339),
      bone: Color(0xFFE4E6F2),
      boneMuted: Color(0xFFA1A6C2),
      ember: Color(0xFFA98FEA),
      arcane: Color(0xFF74B4E8),
      moss: Color(0xFF72B585),
      blood: Color(0xFFE0606C),
      oldGold: Color(0xFFB8BCD0),
      rune: Color(0xFF343A5C),
    ),
    light: AppTokens(
      obsidian: Color(0xFFEEEBF7),
      stone: Color(0xFFE1DCF0),
      stoneRaised: Color(0xFFE8E4F4),
      bone: Color(0xFF161632),
      boneMuted: Color(0xFF4C4A6E),
      ember: Color(0xFF3F3A99),
      arcane: Color(0xFF2C66A3),
      moss: Color(0xFF357045),
      blood: Color(0xFFA3283A),
      oldGold: Color(0xFF63677F),
      rune: Color(0xFFABA6C8),
    ),
  ),

  /// Reddish black with crimson and bone.
  blood(
    'Sangre y hueso',
    'Negro rojizo, carmesí y hueso.',
    dark: AppTokens(
      obsidian: Color(0xFF140C0C),
      stone: Color(0xFF211414),
      stoneRaised: Color(0xFF2B1A1A),
      bone: Color(0xFFEDE3D3),
      boneMuted: Color(0xFFB3A291),
      ember: Color(0xFFC8343C),
      arcane: Color(0xFFA88AE0),
      moss: Color(0xFF7BA866),
      blood: Color(0xFFE4625A),
      oldGold: Color(0xFFCDBFA6),
      rune: Color(0xFF4A2E2E),
    ),
    light: AppTokens(
      obsidian: Color(0xFFEFE6D6),
      stone: Color(0xFFE3D7C2),
      stoneRaised: Color(0xFFE9DFCC),
      bone: Color(0xFF1E0E0E),
      boneMuted: Color(0xFF5C4540),
      ember: Color(0xFF7A1E2A),
      arcane: Color(0xFF5E45A0),
      moss: Color(0xFF45683A),
      blood: Color(0xFF9E2A22),
      oldGold: Color(0xFF7E6A4C),
      rune: Color(0xFFB3A08A),
    ),
  );

  const AppPalette(this.label, this.description, {required this.dark, required this.light});

  /// Name shown on the Personalización screen.
  final String label;

  /// One-line description shown under the [label].
  final String description;

  final AppTokens dark;
  final AppTokens light;

  /// The variant of this palette for [brightness].
  AppTokens tokens(Brightness brightness) => brightness == Brightness.dark ? dark : light;

  /// Whether class accents are recommended with this palette (they bring back
  /// colours a neutral palette avoids on purpose).
  bool get recommendsClassColors => this != graphite;
}
