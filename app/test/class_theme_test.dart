import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/theme/app_theme.dart';
import 'package:opentrpg_dnd5e/characters/domain/class_theme.dart';
import 'package:opentrpg_dnd5e/characters/models.dart';

import 'helpers/character_fakes.dart';

void main() {
  group('classThemeOf', () {
    const expected = {
      'barbarian': ('Bárbaro', Color(0xFFA6341B), AppIcons.barbarian),
      'bard': ('Bardo', Color(0xFFB0579A), AppIcons.bard),
      'cleric': ('Clérigo', Color(0xFFD9A441), AppIcons.cleric),
      'druid': ('Druida', Color(0xFF4F7F3A), AppIcons.druid),
      'fighter': ('Guerrero', Color(0xFF7E5A3C), AppIcons.fighter),
      'monk': ('Monje', Color(0xFF3F8FA8), AppIcons.monk),
      'paladin': ('Paladín', Color(0xFFC7B46A), AppIcons.paladin),
      'ranger': ('Explorador', Color(0xFF2F6B4F), AppIcons.ranger),
      'rogue': ('Pícaro', Color(0xFF4A4A4A), AppIcons.rogue),
      'sorcerer': ('Hechicero', Color(0xFFC2453F), AppIcons.sorcerer),
      'warlock': ('Brujo', Color(0xFF5E3A8A), AppIcons.warlock),
      'wizard': ('Mago', Color(0xFF3A5CA8), AppIcons.wizard),
    };

    test('las 12 clases del SRD tienen nombre, acento e icono', () {
      expect(classThemes.keys.toSet(), expected.keys.toSet());
      for (final MapEntry(key: index, value: (label, accent, icon)) in expected.entries) {
        final theme = classThemeOf(index);
        expect(theme.index, index);
        expect(theme.labelEs, label, reason: index);
        expect(theme.accentLight, accent, reason: index);
        expect(theme.icon, icon, reason: index);
        expect(theme.accent(Brightness.light), accent);
      }
    });

    test('la variante oscura es el acento aclarado un 20 %', () {
      for (final theme in classThemes.values) {
        expect(theme.accentDark, lightenAccent(theme.accentLight), reason: theme.index);
        expect(theme.accent(Brightness.dark), theme.accentDark);
        expect(
          theme.accentDark.computeLuminance(),
          greaterThan(theme.accentLight.computeLuminance()),
          reason: theme.index,
        );
      }
    });

    test('una clase desconocida o ninguna usa "aventurero" con d20 y oro', () {
      for (final index in ['artificer', '', null]) {
        final theme = classThemeOf(index);
        expect(theme.index, 'aventurero');
        expect(theme.labelEs, 'Aventurero');
        expect(theme.icon, AppIcons.d20);
        expect(theme.accentLight, AppTokens.light.gold);
        expect(theme.accentDark, AppTokens.dark.gold);
      }
    });

    test('la clase principal es la de más nivel (la primera si empatan)', () {
      CharacterDetail withClasses(List<Map<String, dynamic>> classes) =>
          CharacterDetail.fromJson(makeCharacterJson(classes: classes));
      expect(
        mainClassIndex(
          withClasses([
            {'classIndex': 'fighter', 'className': 'Fighter', 'level': 2},
            {'classIndex': 'wizard', 'className': 'Wizard', 'level': 5},
          ]),
        ),
        'wizard',
      );
      expect(
        mainClassIndex(
          withClasses([
            {'classIndex': 'rogue', 'className': 'Rogue', 'level': 3},
            {'classIndex': 'bard', 'className': 'Bard', 'level': 3},
          ]),
        ),
        'rogue',
      );
      expect(mainClassIndex(withClasses(const [])), isNull);
    });
  });

  group('ClassAccent', () {
    Future<ThemeData> themeInside(WidgetTester tester, Brightness brightness, String? index) async {
      late ThemeData inner;
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(brightness: brightness),
          home: ClassAccent(
            classIndex: index,
            child: Builder(
              builder: (context) {
                inner = Theme.of(context);
                return const SizedBox();
              },
            ),
          ),
        ),
      );
      // The theme change from the previous pump animates.
      await tester.pumpAndSettle();
      return inner;
    }

    testWidgets('cambia el primary por el acento de la clase en claro y oscuro', (tester) async {
      final light = await themeInside(tester, Brightness.light, 'druid');
      expect(light.colorScheme.primary, classThemeOf('druid').accentLight);
      expect(light.colorScheme.onPrimary, Colors.white);

      final dark = await themeInside(tester, Brightness.dark, 'druid');
      expect(dark.colorScheme.primary, classThemeOf('druid').accentDark);

      final paladin = await themeInside(tester, Brightness.light, 'paladin');
      // Light gold accent: dark text on top.
      expect(paladin.colorScheme.onPrimary, Colors.black);

      final none = await themeInside(tester, Brightness.light, null);
      expect(none.colorScheme.primary, AppTokens.light.gold);
    });

    testWidgets('sin colores de clase usa el acento de la paleta', (tester) async {
      late ThemeData inner;
      late Color accent;
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.dark(
            palette: AppPalette.graphite,
            style: const AppStyle(classColors: false),
          ),
          home: ClassAccent(
            classIndex: 'barbarian',
            child: Builder(
              builder: (context) {
                inner = Theme.of(context);
                accent = classAccentOf(context, 'barbarian');
                return const SizedBox();
              },
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(inner.colorScheme.primary, AppPalette.graphite.dark.oldGold);
      expect(accent, AppPalette.graphite.dark.oldGold);
    });

    testWidgets('no toca el resto del esquema', (tester) async {
      final base = ThemeData(brightness: Brightness.light).colorScheme;
      final inner = await themeInside(tester, Brightness.light, 'wizard');
      expect(inner.colorScheme.secondary, base.secondary);
      expect(inner.colorScheme.surface, base.surface);
      expect(inner.colorScheme.error, base.error);
    });
  });
}
