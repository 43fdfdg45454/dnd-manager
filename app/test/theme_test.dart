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
      expect(AppTokens.light.parchment, const Color(0xFFF3E9D2));
      expect(AppTokens.light.crimson, const Color(0xFF8B1E1E));
      expect(AppTokens.dark.parchment, const Color(0xFF1C1814));
      expect(AppTokens.dark.gold, const Color(0xFFD4A83A));
    });

    test('are attached to both themes and map to the colour scheme', () {
      for (final theme in [AppTheme.light(), AppTheme.dark()]) {
        final tokens = theme.extension<AppTokens>()!;
        expect(theme.colorScheme.primary, tokens.crimson);
        expect(theme.colorScheme.secondary, tokens.gold);
        expect(theme.colorScheme.tertiary, tokens.arcane);
        expect(theme.colorScheme.surface, tokens.parchment);
        expect(theme.colorScheme.onSurface, tokens.ink);
        expect(theme.colorScheme.outline, tokens.rune);
        expect(theme.useMaterial3, isTrue);
      }
    });

    test('lerp and copyWith', () {
      final mid = AppTokens.light.lerp(AppTokens.dark, 0.5);
      expect(mid.parchment, Color.lerp(AppTokens.light.parchment, AppTokens.dark.parchment, 0.5));
      expect(AppTokens.light.copyWith(gold: Colors.red).gold, Colors.red);
      expect(AppTokens.light.copyWith(gold: Colors.red).ink, AppTokens.light.ink);
    });

    test('ink is readable (WCAG >= 4.5) on parchment and parchmentDeep', () {
      for (final tokens in [AppTokens.light, AppTokens.dark]) {
        expect(_contrast(tokens.ink, tokens.parchment), greaterThanOrEqualTo(4.5));
        expect(_contrast(tokens.ink, tokens.parchmentDeep), greaterThanOrEqualTo(4.5));
      }
    });

    test('inkMuted is readable on parchment and parchmentDeep', () {
      for (final tokens in [AppTokens.light, AppTokens.dark]) {
        expect(_contrast(tokens.inkMuted, tokens.parchment), greaterThanOrEqualTo(4.5));
        expect(_contrast(tokens.inkMuted, tokens.parchmentDeep), greaterThanOrEqualTo(4.5));
      }
    });
  });

  group('themes', () {
    for (final entry in {'claro': AppTheme.light(), 'oscuro': AppTheme.dark()}.entries) {
      testWidgets('el tema ${entry.key} dibuja Card y FilledButton', (tester) async {
        await tester.pumpWidget(
          _themed(
            entry.value,
            Column(
              children: [
                const Card(child: Text('Tarjeta')),
                FilledButton(onPressed: () {}, child: const Text('Acción')),
                const ParchmentCard(child: Text('Pergamino')),
                const StoneCard(child: Text('Piedra')),
                const SectionHeader('Sección'),
                const RuneDivider(),
              ],
            ),
          ),
        );
        await tester.pump();

        expect(tester.takeException(), isNull);
        expect(find.byType(Card), findsNWidgets(3));
        expect(find.byType(FilledButton), findsOneWidget);
        expect(find.text('Sección'), findsOneWidget);
        expect(find.byType(RuneDivider), findsOneWidget);
      });
    }

    testWidgets('la Card usa el relleno pergamino del brillo activo', (tester) async {
      await tester.pumpWidget(_themed(AppTheme.dark(), const Card(child: Text('x'))));
      final material = tester.widget<Material>(
        find.descendant(of: find.byType(Card), matching: find.byType(Material)).first,
      );
      expect(material.color, AppTokens.dark.parchmentDeep);
    });

    testWidgets('los títulos usan Cinzel y el cuerpo Alegreya', (tester) async {
      final theme = AppTheme.light();
      expect(theme.textTheme.titleLarge?.fontFamily, 'Cinzel');
      expect(theme.textTheme.headlineMedium?.fontFamily, 'Cinzel');
      expect(theme.textTheme.bodyMedium?.fontFamily, 'Alegreya');
      expect(theme.textTheme.labelLarge?.fontFamily, 'Alegreya');
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

    testWidgets('every AppIcons value has its bundled asset', (tester) async {
      for (final icon in AppIcons.values) {
        final data = await rootBundle.load(icon.assetPath);
        expect(data.lengthInBytes, greaterThan(100), reason: icon.assetPath);
      }
      expect(AppIcons.values.length, greaterThanOrEqualTo(38));
    });
  });

  testWidgets('la pantalla de atribuciones lista SRD, iconos y tipografías', (tester) async {
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
    expect(find.byKey(const Key('attributions-icons')), findsOneWidget);
    expect(find.byKey(const Key('attributions-fonts')), findsOneWidget);

    await tester.ensureVisible(find.byKey(const Key('attributions-license-cinzel')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('attributions-license-cinzel')));
    await tester.pumpAndSettle();
    expect(find.textContaining('SIL Open Font License', findRichText: true), findsWidgets);
  });
}
