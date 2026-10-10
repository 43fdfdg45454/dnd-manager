import 'package:flutter/material.dart';

import 'package:opentrpg_core/core/theme/app_style.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/theme/tokens.dart';

import '../models.dart';

/// Visual identity of an SRD class: Spanish name, accent colour (light and
/// dark variants) and icon.
@immutable
class ClassTheme {
  const ClassTheme({
    required this.index,
    required this.labelEs,
    required this.accentLight,
    required this.accentDark,
    required this.icon,
  });

  /// Lighter variant of [accent] for dark backgrounds (~20 % towards white).
  factory ClassTheme.of({
    required String index,
    required String labelEs,
    required Color accent,
    required AppIcons icon,
  }) => ClassTheme(
    index: index,
    labelEs: labelEs,
    accentLight: accent,
    accentDark: lightenAccent(accent),
    icon: icon,
  );

  final String index;
  final String labelEs;
  final Color accentLight;
  final Color accentDark;
  final AppIcons icon;

  /// The accent for [brightness].
  Color accent(Brightness brightness) => brightness == Brightness.dark ? accentDark : accentLight;
}

/// [color] moved 20 % towards white: the accent variant used in dark mode.
Color lightenAccent(Color color) => Color.lerp(color, Colors.white, 0.2)!;

/// Index of the fallback theme (no class, or a class outside the SRD).
const adventurerThemeIndex = 'aventurero';

/// Fallback: d20 icon and the gold token as accent.
final ClassTheme adventurerTheme = ClassTheme(
  index: adventurerThemeIndex,
  labelEs: 'Aventurero',
  accentLight: AppTokens.light.gold,
  accentDark: AppTokens.dark.gold,
  icon: AppIcons.d20,
);

/// The 12 SRD classes by index.
final Map<String, ClassTheme> classThemes = {
  for (final theme in [
    ClassTheme.of(
      index: 'barbarian',
      labelEs: 'Bárbaro',
      accent: const Color(0xFFA6341B),
      icon: AppIcons.barbarian,
    ),
    ClassTheme.of(
      index: 'bard',
      labelEs: 'Bardo',
      accent: const Color(0xFFB0579A),
      icon: AppIcons.bard,
    ),
    ClassTheme.of(
      index: 'cleric',
      labelEs: 'Clérigo',
      accent: const Color(0xFFD9A441),
      icon: AppIcons.cleric,
    ),
    ClassTheme.of(
      index: 'druid',
      labelEs: 'Druida',
      accent: const Color(0xFF4F7F3A),
      icon: AppIcons.druid,
    ),
    ClassTheme.of(
      index: 'fighter',
      labelEs: 'Guerrero',
      accent: const Color(0xFF7E5A3C),
      icon: AppIcons.fighter,
    ),
    ClassTheme.of(
      index: 'monk',
      labelEs: 'Monje',
      accent: const Color(0xFF3F8FA8),
      icon: AppIcons.monk,
    ),
    ClassTheme.of(
      index: 'paladin',
      labelEs: 'Paladín',
      accent: const Color(0xFFC7B46A),
      icon: AppIcons.paladin,
    ),
    ClassTheme.of(
      index: 'ranger',
      labelEs: 'Explorador',
      accent: const Color(0xFF2F6B4F),
      icon: AppIcons.ranger,
    ),
    ClassTheme.of(
      index: 'rogue',
      labelEs: 'Pícaro',
      accent: const Color(0xFF4A4A4A),
      icon: AppIcons.rogue,
    ),
    ClassTheme.of(
      index: 'sorcerer',
      labelEs: 'Hechicero',
      accent: const Color(0xFFC2453F),
      icon: AppIcons.sorcerer,
    ),
    ClassTheme.of(
      index: 'warlock',
      labelEs: 'Brujo',
      accent: const Color(0xFF5E3A8A),
      icon: AppIcons.warlock,
    ),
    ClassTheme.of(
      index: 'wizard',
      labelEs: 'Mago',
      accent: const Color(0xFF3A5CA8),
      icon: AppIcons.wizard,
    ),
  ])
    theme.index: theme,
};

/// The theme of [index]; [adventurerTheme] for null or unknown classes.
ClassTheme classThemeOf(String? index) => classThemes[index] ?? adventurerTheme;

/// The accent [classIndex] shows at [context]: the class colour, or the
/// palette accent ([AppTokens.oldGold]) when class colours are off
/// ([AppStyle.classColors]) or there is no SRD class.
Color classAccentOf(BuildContext context, String? classIndex) {
  final theme = classThemes[classIndex];
  if (theme == null || !context.appStyle.classColors) return context.tokens.oldGold;
  return theme.accent(Theme.of(context).brightness);
}

/// The class that gives a character its colour: the highest level one (the
/// first on a tie); null without classes.
String? mainClassIndex(CharacterDetail character) => mainClassIndexOf(character.classes);

/// [mainClassIndex] of a list of classes (a summary or a party member).
String? mainClassIndexOf(List<CharacterClass> classes) {
  CharacterClass? best;
  for (final c in classes) {
    if (best == null || c.level > best.level) best = c;
  }
  return best?.classIndex;
}

/// Gives [child] the accent of [classIndex] as `colorScheme.primary` (with a
/// readable `onPrimary`). Used only on the character header, the hit point
/// card and the class panels. Follows [classAccentOf], so with class colours
/// off it is the palette accent.
class ClassAccent extends StatelessWidget {
  const ClassAccent({super.key, required this.classIndex, required this.child});

  final String? classIndex;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final accent = classAccentOf(context, classIndex);
    final onAccent = ThemeData.estimateBrightnessForColor(accent) == Brightness.dark
        ? Colors.white
        : Colors.black;
    return Theme(
      data: theme.copyWith(
        colorScheme: theme.colorScheme.copyWith(primary: accent, onPrimary: onAccent),
      ),
      child: child,
    );
  }
}
