import 'dart:convert';
import 'dart:math' as math;

import 'package:dnd_companion/core/theme/app_theme.dart';
import 'package:dnd_companion/features/catalog/data/catalog_controllers.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/features/home/ui/attribution_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';

/// WCAG 2.x relative luminance.
double _luminance(Color color) {
  double channel(double c) =>
      c <= 0.03928 ? c / 12.92 : math.pow((c + 0.055) / 1.055, 2.4).toDouble();
  return 0.2126 * channel(color.r) + 0.7152 * channel(color.g) + 0.0722 * channel(color.b);
}

double _contrast(Color a, Color b) {
  final la = _luminance(a);
  final lb = _luminance(b);
  return (math.max(la, lb) + 0.05) / (math.min(la, lb) + 0.05);
}

Widget _themed(ThemeData theme, Widget child) => MaterialApp(
  theme: theme,
  home: Scaffold(body: child),
);

void main() {
  group('tokens', () {
    test('exist in both brightnesses with the contract values', () {
      expect(AppTokens.of(Brightness.light), same(AppTokens.light));
      expect(AppTokens.of(Brightness.dark), same(AppTokens.dark));
      const dark = AppTokens.dark;
      expect(dark.obsidian, const Color(0xFF14110F));
      expect(dark.stone, const Color(0xFF231D19));
      expect(dark.stoneRaised, const Color(0xFF2D2622));
      expect(dark.bone, const Color(0xFFE7DCC6));
      expect(dark.boneMuted, const Color(0xFFA89C87));
      expect(dark.ember, const Color(0xFFD9671E));
      expect(dark.arcane, const Color(0xFF8A6BD1));
      expect(dark.moss, const Color(0xFF5E7A4A));
      expect(dark.blood, const Color(0xFF9B2226));
      expect(dark.oldGold, const Color(0xFFB8923A));
      expect(dark.rune, const Color(0xFF4A3F36));
      // Light: bone background, clay cards, obsidian text.
      expect(AppTokens.light.obsidian, dark.bone);
      expect(AppTokens.light.stone, const Color(0xFFD8C9AE));
      expect(AppTokens.light.bone, dark.obsidian);
    });

    test('the light accents are the dark ones darkened by 15 %', () {
      Color darken(Color c) =>
          Color.from(alpha: 1, red: c.r * 0.85, green: c.g * 0.85, blue: c.b * 0.85);
      for (final (name, d, l) in [
        ('ember', AppTokens.dark.ember, AppTokens.light.ember),
        ('arcane', AppTokens.dark.arcane, AppTokens.light.arcane),
        ('moss', AppTokens.dark.moss, AppTokens.light.moss),
        ('blood', AppTokens.dark.blood, AppTokens.light.blood),
        ('oldGold', AppTokens.dark.oldGold, AppTokens.light.oldGold),
      ]) {
        final expected = darken(d);
        expect((l.r - expected.r).abs(), lessThan(0.003), reason: name);
        expect((l.g - expected.g).abs(), lessThan(0.003), reason: name);
        expect((l.b - expected.b).abs(), lessThan(0.003), reason: name);
      }
    });

    test('the previous names are aliases of the new roles', () {
      for (final t in [AppTokens.light, AppTokens.dark]) {
        expect(t.parchment, t.obsidian);
        expect(t.parchmentDeep, t.stone);
        expect(t.ink, t.bone);
        expect(t.inkMuted, t.boneMuted);
        expect(t.gold, t.oldGold);
        expect(t.crimson, t.ember);
        expect(t.emerald, t.moss);
      }
    });

    test('are attached to both themes and map to the colour scheme', () {
      for (final theme in [AppTheme.light(), AppTheme.dark()]) {
        final tokens = theme.extension<AppTokens>()!;
        expect(theme.colorScheme.primary, tokens.ember);
        expect(theme.colorScheme.secondary, tokens.oldGold);
        expect(theme.colorScheme.tertiary, tokens.arcane);
        expect(theme.colorScheme.surface, tokens.obsidian);
        expect(theme.colorScheme.onSurface, tokens.bone);
        expect(theme.colorScheme.outline, tokens.rune);
        expect(theme.scaffoldBackgroundColor, tokens.obsidian);
        expect(theme.useMaterial3, isTrue);
      }
    });

    test('lerp and copyWith', () {
      final mid = AppTokens.light.lerp(AppTokens.dark, 0.5);
      expect(mid.obsidian, Color.lerp(AppTokens.light.obsidian, AppTokens.dark.obsidian, 0.5));
      expect(AppTokens.light.copyWith(oldGold: Colors.red).oldGold, Colors.red);
      expect(AppTokens.light.copyWith(oldGold: Colors.red).gold, Colors.red);
      expect(AppTokens.light.copyWith(oldGold: Colors.red).bone, AppTokens.light.bone);
    });

    test('bone is readable (WCAG >= 4.5) on obsidian, stone and stoneRaised', () {
      for (final tokens in [AppTokens.light, AppTokens.dark]) {
        expect(_contrast(tokens.bone, tokens.obsidian), greaterThanOrEqualTo(4.5));
        expect(_contrast(tokens.bone, tokens.stone), greaterThanOrEqualTo(4.5));
        expect(_contrast(tokens.bone, tokens.stoneRaised), greaterThanOrEqualTo(4.5));
      }
    });

    test('boneMuted is readable on obsidian, stone and stoneRaised', () {
      for (final tokens in [AppTokens.light, AppTokens.dark]) {
        expect(_contrast(tokens.boneMuted, tokens.obsidian), greaterThanOrEqualTo(4.5));
        expect(_contrast(tokens.boneMuted, tokens.stone), greaterThanOrEqualTo(4.5));
        expect(_contrast(tokens.boneMuted, tokens.stoneRaised), greaterThanOrEqualTo(4.5));
      }
    });

    test('the main buttons are readable', () {
      for (final theme in [AppTheme.light(), AppTheme.dark()]) {
        final scheme = theme.colorScheme;
        expect(_contrast(scheme.onPrimary, scheme.primary), greaterThanOrEqualTo(4.5));
        expect(_contrast(scheme.error, scheme.surface), greaterThanOrEqualTo(4.5));
      }
    });
  });

  group('themes', () {
    for (final entry in {'claro': AppTheme.light(), 'oscuro': AppTheme.dark()}.entries) {
      testWidgets('el tema ${entry.key} dibuja Card, RuneCard y FilledButton', (tester) async {
        var tapped = 0;
        await tester.pumpWidget(
          _themed(
            entry.value,
            Column(
              children: [
                const Card(child: Text('Tarjeta')),
                FilledButton(onPressed: () {}, child: const Text('Acción')),
                ParchmentCard(onTap: () => tapped++, child: const Text('Pergamino')),
                const StoneCard(child: Text('Piedra')),
                const RuneCard(child: Text('Runa')),
                const SectionHeader('Sección'),
                const RuneDivider(),
              ],
            ),
          ),
        );
        await tester.pump();

        expect(tester.takeException(), isNull);
        expect(find.byType(Card), findsOneWidget);
        expect(find.byType(RuneCard), findsNWidgets(3));
        expect(find.byType(FilledButton), findsOneWidget);
        expect(find.text('Sección'), findsOneWidget);
        expect(find.byType(RuneDivider), findsOneWidget);

        await tester.tap(find.text('Pergamino'));
        expect(tapped, 1);
      });
    }

    testWidgets('la Card usa el relleno piedra del brillo activo', (tester) async {
      await tester.pumpWidget(_themed(AppTheme.dark(), const Card(child: Text('x'))));
      final material = tester.widget<Material>(
        find.descendant(of: find.byType(Card), matching: find.byType(Material)).first,
      );
      expect(material.color, AppTokens.dark.stone);
    });

    testWidgets('ParchmentCard y StoneCard pintan piedra y piedra elevada', (tester) async {
      await tester.pumpWidget(
        _themed(
          AppTheme.dark(),
          const Column(
            children: [
              ParchmentCard(child: Text('a')),
              StoneCard(child: Text('b')),
            ],
          ),
        ),
      );
      final painters = tester
          .widgetList<CustomPaint>(find.byType(CustomPaint))
          .map((paint) => paint.painter)
          .whereType<RuneBorderPainter>()
          .toList();
      expect(painters.map((p) => p.fill), [AppTokens.dark.stone, AppTokens.dark.stoneRaised]);
    });

    test('los títulos usan Almendra y el cuerpo Source Sans 3', () {
      final theme = AppTheme.light();
      expect(theme.textTheme.titleLarge?.fontFamily, 'Almendra');
      expect(theme.textTheme.headlineMedium?.fontFamily, 'Almendra');
      expect(theme.textTheme.displayLarge?.fontFamily, 'Almendra');
      expect(theme.textTheme.bodyMedium?.fontFamily, 'SourceSans3');
      expect(theme.textTheme.labelLarge?.fontFamily, 'SourceSans3');
      // Variable font: the weight travels as a variation.
      expect(theme.textTheme.bodyMedium?.fontVariations, [const FontVariation.weight(400)]);
      expect(theme.textTheme.labelLarge?.fontVariations, [const FontVariation.weight(600)]);
    });

    test('el estilo numérico usa cifras tabulares', () {
      expect(AppTypography.numeric.fontFamily, 'SourceSans3');
      expect(AppTypography.numeric.fontFeatures, contains(const FontFeature.tabularFigures()));
      final merged = AppTheme.dark().textTheme.titleMedium!.merge(AppTypography.numeric);
      expect(merged.fontFeatures, contains(const FontFeature.tabularFigures()));
      expect(merged.color, AppTokens.dark.bone);
    });

    testWidgets('las fuentes están declaradas y empaquetadas', (tester) async {
      final manifest = jsonDecode(await rootBundle.loadString('FontManifest.json')) as List;
      final families = {
        for (final f in manifest) (f as Map)['family'] as String: f['fonts'] as List,
      };
      expect(
        families.keys,
        containsAll([
          'Almendra',
          'SourceSans3',
          'Cinzel',
          'IMFellEnglish',
          'AtkinsonHyperlegibleNext',
          'Lora',
        ]),
      );
      expect(families.keys, isNot(contains('Alegreya')));
      // Every selectable bundled family is declared.
      for (final family in [
        ...TitleFont.values.map((f) => f.family),
        ...BodyFont.values.map((f) => f.family),
      ].nonNulls) {
        expect(families.keys, contains(family));
      }
      final assets = {
        for (final fonts in families.values)
          for (final font in fonts) (font as Map)['asset'] as String,
      };
      expect(
        assets,
        containsAll([
          'assets/fonts/Almendra-Regular.ttf',
          'assets/fonts/Almendra-Bold.ttf',
          'assets/fonts/SourceSans3.ttf',
          'assets/fonts/SourceSans3-Italic.ttf',
          'assets/fonts/Cinzel.ttf',
          'assets/fonts/IMFellEnglish-Regular.ttf',
          'assets/fonts/AtkinsonHyperlegibleNext.ttf',
          'assets/fonts/AtkinsonHyperlegibleNext-Italic.ttf',
          'assets/fonts/Lora.ttf',
          'assets/fonts/Lora-Italic.ttf',
        ]),
      );
      for (final asset in assets.where((a) => a.startsWith('assets/fonts/'))) {
        final data = await rootBundle.load(asset);
        // TrueType signature.
        expect(data.getUint32(0), 0x00010000, reason: asset);
      }
      for (final license in [
        'OFL-Almendra.txt',
        'OFL-SourceSans3.txt',
        'OFL-Cinzel.txt',
        'OFL-IMFellEnglish.txt',
        'OFL-AtkinsonHyperlegibleNext.txt',
        'OFL-Lora.txt',
      ]) {
        final text = await rootBundle.loadString('assets/licenses/$license');
        expect(text, contains('SIL Open Font License'), reason: license);
      }
    });

    testWidgets('context.tokens cae al brillo cuando el tema no lleva la extensión', (
      tester,
    ) async {
      late AppTokens tokens;
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(brightness: Brightness.dark),
          home: Builder(
            builder: (context) {
              tokens = context.tokens;
              return const SizedBox();
            },
          ),
        ),
      );
      expect(tokens, same(AppTokens.dark));
    });
  });

  group('texturas', () {
    test('el grano es determinista y se cachea', () {
      final a = GrainPainter(color: Colors.white, seed: 3);
      final b = GrainPainter(color: Colors.black, seed: 3);
      final c = GrainPainter(color: Colors.white, seed: 4);
      expect(identical(a.points, b.points), isTrue);
      expect(a.points, isNot(equals(c.points)));
      expect(a.points.length, (128 * 128 * 0.012).round() * 2);
      expect(a.shouldRepaint(GrainPainter(color: Colors.white, seed: 3)), isFalse);
      expect(a.shouldRepaint(c), isTrue);
    });

    test('el borde a pincel es estable para una semilla', () {
      const size = Size(200, 120);
      final a = RuneBorderPainter(fill: Colors.black, border: Colors.white, seed: 5);
      final b = RuneBorderPainter(fill: Colors.black, border: Colors.white, seed: 5);
      expect(a.outline(size).getBounds(), b.outline(size).getBounds());
      final bounds = a.outline(size).getBounds();
      expect(bounds.left, greaterThan(-1));
      expect(bounds.right, lessThan(size.width + 1));
    });

    for (final entry in {'claro': AppTheme.light(), 'oscuro': AppTheme.dark()}.entries) {
      testWidgets('GrainBackground pinta en el tema ${entry.key}', (tester) async {
        await tester.pumpWidget(
          _themed(entry.value, const GrainBackground(child: SizedBox.expand())),
        );
        expect(tester.takeException(), isNull);
        final box = tester.widget<ColoredBox>(
          find.descendant(of: find.byType(GrainBackground), matching: find.byType(ColoredBox)),
        );
        expect(box.color, entry.value.extension<AppTokens>()!.obsidian);
      });
    }
  });

  group('AppIcon', () {
    testWidgets('renders the bundled SVG', (tester) async {
      await tester.pumpWidget(_themed(AppTheme.light(), const AppIcon(AppIcons.d20, size: 32)));
      await tester.pumpAndSettle();

      expect(find.byType(SvgPicture), findsOneWidget);
      expect(find.byType(Icon), findsNothing);
      expect(tester.getSize(find.byType(AppIcon)), const Size(32, 32));
    });

    testWidgets('tints with the colour or the IconTheme', (tester) async {
      await tester.pumpWidget(
        _themed(
          AppTheme.light(),
          const IconTheme(
            data: IconThemeData(color: Colors.green),
            child: Column(
              children: [
                AppIcon(AppIcons.wizard),
                AppIcon(AppIcons.sword, color: Colors.blue),
              ],
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final pictures = tester.widgetList<SvgPicture>(find.byType(SvgPicture)).toList();
      expect(pictures[0].colorFilter, const ColorFilter.mode(Colors.green, BlendMode.srcIn));
      expect(pictures[1].colorFilter, const ColorFilter.mode(Colors.blue, BlendMode.srcIn));
    });

    test('the set has every glyph of the contract', () {
      const required = [
        'barbarian', 'bard', 'cleric', 'druid', 'fighter', 'monk', 'paladin', 'ranger', //
        'rogue', 'sorcerer', 'warlock', 'wizard', 'd20', 'heart', 'shield', 'sword', 'bow',
        'staff', 'flame', 'bolt', 'moon', 'campfire', 'sun', 'scroll', 'book', 'quill', 'map',
        'compass', 'crown', 'hood', 'envelope', 'treasure', 'backpack', 'coins', 'calendar',
        'anvil', 'potion', 'skull', 'eye', 'chains', 'sparkles', 'seal', 'rune', 'levelUp',
        'drop', 'splash', 'gear', 'users', 'anchor', 'clock',
        // Kept from the previous set.
        'castle', 'spellbook',
      ];
      expect(AppIcons.values.map((icon) => icon.name), containsAll(required));
      for (final icon in AppIcons.values) {
        expect(icon.file, icon.name);
      }
    });

    testWidgets('every AppIcons value has its own stroke-only 24x24 SVG', (tester) async {
      for (final icon in AppIcons.values) {
        final svg = await rootBundle.loadString(icon.assetPath);
        expect(svg, contains('viewBox="0 0 24 24"'), reason: icon.assetPath);
        expect(svg, contains('fill="none"'), reason: icon.assetPath);
        expect(svg, contains('stroke="currentColor"'), reason: icon.assetPath);
        expect(svg, contains('stroke-width="2"'), reason: icon.assetPath);
        expect(svg, isNot(contains('game-icons')), reason: icon.assetPath);
        expect(svg.length, lessThan(1024), reason: icon.assetPath);
      }
      expect(AppIcons.values.length, greaterThanOrEqualTo(52));
    });

    testWidgets('every icon renders at 20 px without errors', (tester) async {
      await tester.pumpWidget(
        _themed(
          AppTheme.dark(),
          Wrap(children: [for (final icon in AppIcons.values) AppIcon(icon, size: 20)]),
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.byType(SvgPicture), findsNWidgets(AppIcons.values.length));
      expect(find.byType(Icon), findsNothing);
    });

    testWidgets('the attribution file declares the icons as original work', (tester) async {
      final text = await rootBundle.loadString('assets/icons/ATTRIBUTION.md');
      expect(text, contains('obra original'));
      expect(text, isNot(contains('game-icons')));
    });
  });

  testWidgets('la pantalla de atribuciones cita solo el SRD y las tipografías', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          attributionProvider.overrideWith(
            (ref) async => const Attribution(
              ruleset: 'SRD 5.1',
              license: 'CC-BY-4.0',
              text: 'Texto SRD de prueba.',
            ),
          ),
        ],
        child: MaterialApp(theme: AppTheme.light(), home: const AttributionPage()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Atribuciones'), findsOneWidget);
    expect(find.text('Texto SRD de prueba.'), findsOneWidget);
    expect(find.byKey(const Key('attributions-icons')), findsNothing);
    expect(find.textContaining('game-icons'), findsNothing);
    expect(find.byKey(const Key('attributions-fonts')), findsOneWidget);
    expect(find.textContaining('Almendra, Cinzel, IM Fell English, Source Sans 3'), findsOneWidget);

    for (final key in [
      'attributions-license-almendra',
      'attributions-license-source-sans',
      'attributions-license-cinzel',
      'attributions-license-im-fell-english',
      'attributions-license-atkinson',
      'attributions-license-lora',
    ]) {
      await tester.ensureVisible(find.byKey(Key(key)));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(Key(key)));
      await tester.pumpAndSettle();
    }
    expect(find.textContaining('SIL Open Font License', findRichText: true), findsWidgets);
  });
}
