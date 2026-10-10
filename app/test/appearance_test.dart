import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg/app.dart';
import 'package:opentrpg_core/core/auth/auth_controller.dart';
import 'package:opentrpg_core/core/auth/auth_state.dart';
import 'package:opentrpg_core/core/motion/motion_settings.dart';
import 'package:opentrpg_core/core/router/app_router.dart';
import 'package:opentrpg_core/core/storage/local_preferences.dart';
import 'package:opentrpg_core/core/theme/app_theme.dart';
import 'package:opentrpg_core/features/settings/data/appearance_controller.dart';
import 'package:opentrpg_core/features/settings/ui/appearance_page.dart';
import 'package:opentrpg_dnd5e/characters/domain/class_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'helpers/app_pump.dart';
import 'helpers/fakes.dart';
import 'helpers/session_fakes.dart';

Future<SharedPreferences> _prefs([Map<String, Object> values = const {}]) async {
  SharedPreferences.setMockInitialValues(values);
  return SharedPreferences.getInstance();
}

Future<void> _pumpPage(WidgetTester tester, SharedPreferences? prefs) async {
  tester.view.physicalSize = const Size(800, 1600);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [dnd5eSystemsOverride(), localPreferencesProvider.overrideWithValue(prefs)],
      child: MaterialApp(theme: AppTheme.dark(), home: const AppearancePage()),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _tap(WidgetTester tester, String key) async {
  await tester.ensureVisible(find.byKey(Key(key)));
  await tester.pumpAndSettle();
  await tester.tap(find.byKey(Key(key)));
  await tester.pumpAndSettle();
}

Set<T> _selected<T>(WidgetTester tester, String key) => tester
    .widget<SegmentedButton<T>>(
      find.descendant(of: find.byKey(Key(key)), matching: find.byType(SegmentedButton<T>)),
    )
    .selected;

/// Fill of the preview card (its [RuneBorderPainter]).
RuneBorderPainter _previewPainter(WidgetTester tester) => tester
    .widgetList<CustomPaint>(
      find.descendant(
        of: find.byKey(const Key('appearance-preview')),
        matching: find.byType(CustomPaint),
      ),
    )
    .map((paint) => paint.painter)
    .whereType<RuneBorderPainter>()
    .first;

TextStyle _previewTitleStyle(WidgetTester tester) =>
    tester.widget<Text>(find.byKey(const Key('appearance-preview-title'))).style!;

void main() {
  // Building a theme makes its fonts the active ones; start every test clean.
  setUp(() => AppFonts.active = AppFontSet.standard);

  group('ajustes de apariencia', () {
    test('por defecto: tema oscuro, texto normal y todas las animaciones', () async {
      final container = ProviderContainer(
        overrides: [
          dnd5eSystemsOverride(),
          localPreferencesProvider.overrideWithValue(await _prefs()),
        ],
      );
      addTearDown(container.dispose);

      expect(container.read(appearanceProvider), const AppearanceSettings());
      expect(container.read(appearanceProvider).themeMode, ThemeMode.dark);
      expect(container.read(appearanceProvider).textSize, TextSizePreference.normal);
      expect(container.read(motionSettingsProvider), MotionPreference.all);
    });

    test('lee lo guardado e ignora valores desconocidos', () async {
      final prefs = await _prefs({
        themeModeKey: 'system',
        textSizeKey: 'enorme',
        motionPreferenceKey: 'reduced',
      });
      final container = ProviderContainer(
        overrides: [dnd5eSystemsOverride(), localPreferencesProvider.overrideWithValue(prefs)],
      );
      addTearDown(container.dispose);

      expect(container.read(appearanceProvider).themeMode, ThemeMode.system);
      expect(container.read(appearanceProvider).textSize, TextSizePreference.normal);
      expect(container.read(motionSettingsProvider), MotionPreference.reduced);
    });

    test('sin almacenamiento funciona en memoria', () {
      final container = ProviderContainer();
      addTearDown(container.dispose);

      container.read(appearanceProvider.notifier).selectThemeMode(ThemeMode.light);
      container.read(motionSettingsProvider.notifier).select(MotionPreference.reduced);

      expect(container.read(appearanceProvider).themeMode, ThemeMode.light);
      expect(container.read(motionSettingsProvider), MotionPreference.reduced);
    });
  });

  group('pantalla Personalización', () {
    testWidgets('muestra las tres opciones con los valores por defecto', (tester) async {
      await _pumpPage(tester, await _prefs());

      expect(find.byKey(const Key('settings-appearance')), findsOneWidget);
      expect(find.text('Personalización'), findsOneWidget);
      expect(find.text('Oscuro'), findsOneWidget);
      expect(find.text('Claro'), findsOneWidget);
      expect(find.text('Sistema'), findsOneWidget);
      expect(find.text('Normal'), findsOneWidget);
      expect(find.text('Grande'), findsOneWidget);
      expect(find.text('Muy grande'), findsOneWidget);
      for (final palette in AppPalette.values) {
        expect(find.byKey(Key('palette-${palette.name}')), findsOneWidget);
      }
      for (final font in TitleFont.values) {
        expect(find.byKey(Key('title-font-${font.name}')), findsOneWidget);
      }
      for (final font in BodyFont.values) {
        expect(find.byKey(Key('body-font-${font.name}')), findsOneWidget);
      }
      expect(find.text('Grafito'), findsOneWidget);
      expect(find.text('Igual que el texto'), findsOneWidget);
      expect(find.text('Fuente del sistema'), findsOneWidget);
      expect(find.byKey(const Key('appearance-reset')), findsOneWidget);
      expect(find.text('Todas'), findsOneWidget);
      expect(find.text('Reducidas'), findsOneWidget);
      expect(find.byKey(const Key('appearance-preview')), findsOneWidget);
      expect(_selected<ThemeMode>(tester, 'appearance-theme'), {ThemeMode.dark});
      expect(_selected<TextSizePreference>(tester, 'appearance-text-size'), {
        TextSizePreference.normal,
      });
      expect(_selected<MotionPreference>(tester, 'appearance-motion'), {MotionPreference.all});
    });

    testWidgets('cambiar las opciones las guarda y sobreviven a un reinicio', (tester) async {
      final prefs = await _prefs();
      await _pumpPage(tester, prefs);

      await _tap(tester, 'appearance-theme-light');
      await _tap(tester, 'appearance-text-large');
      await _tap(tester, 'appearance-motion-reduced');

      expect(_selected<ThemeMode>(tester, 'appearance-theme'), {ThemeMode.light});
      expect(_selected<TextSizePreference>(tester, 'appearance-text-size'), {
        TextSizePreference.large,
      });
      expect(_selected<MotionPreference>(tester, 'appearance-motion'), {MotionPreference.reduced});
      expect(prefs.getString(themeModeKey), 'light');
      expect(prefs.getString(textSizeKey), 'large');
      expect(prefs.getString(motionPreferenceKey), 'reduced');

      // A fresh start reads the stored choices.
      final container = ProviderContainer(
        overrides: [dnd5eSystemsOverride(), localPreferencesProvider.overrideWithValue(prefs)],
      );
      addTearDown(container.dispose);
      expect(
        container.read(appearanceProvider),
        const AppearanceSettings(themeMode: ThemeMode.light, textSize: TextSizePreference.large),
      );
      expect(container.read(motionSettingsProvider), MotionPreference.reduced);

      await _tap(tester, 'appearance-theme-system');
      expect(prefs.getString(themeModeKey), 'system');
    });

    testWidgets('el menú de usuario abre Personalización', (tester) async {
      final router = await pumpRealApp(tester, location: AppRoutes.home);

      await tester.tap(find.byKey(const Key('nav-profile')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('home-appearance')), findsOneWidget);
      await tester.tap(find.byKey(const Key('home-appearance')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('settings-appearance')), findsOneWidget);
      expect(locationOf(router), AppRoutes.appearance);
    });
  });

  group('paletas, fuentes y superficies', () {
    test('los valores por defecto son la paleta actual, Almendra y Source Sans 3', () {
      const settings = AppearanceSettings();
      expect(settings.palette, AppPalette.ember);
      expect(settings.titleFont, TitleFont.almendra);
      expect(settings.bodyFont, BodyFont.sourceSans3);
      expect(settings.classColors, isTrue);
      expect(settings.textures, isTrue);
      expect(settings.fonts, AppFontSet.standard);
      expect(settings.style, AppStyle.standard);
      expect(TextSizePreference.extraLarge.scale, 1.4);
    });

    test('lee lo guardado e ignora claves desconocidas o de otro tipo', () async {
      final known = ProviderContainer(
        overrides: [
          dnd5eSystemsOverride(),
          localPreferencesProvider.overrideWithValue(
            await _prefs({
              paletteKey: 'graphite',
              titleFontKey: 'imFellEnglish',
              bodyFontKey: 'system',
              classColorsKey: false,
              texturesKey: false,
              textSizeKey: 'extraLarge',
            }),
          ),
        ],
      );
      addTearDown(known.dispose);
      expect(
        known.read(appearanceProvider),
        const AppearanceSettings(
          palette: AppPalette.graphite,
          titleFont: TitleFont.imFellEnglish,
          bodyFont: BodyFont.system,
          classColors: false,
          textures: false,
          textSize: TextSizePreference.extraLarge,
        ),
      );

      final unknown = ProviderContainer(
        overrides: [
          dnd5eSystemsOverride(),
          localPreferencesProvider.overrideWithValue(
            await _prefs({
              paletteKey: 'neon',
              titleFontKey: 'Comic',
              bodyFontKey: '',
              classColorsKey: 'no',
              texturesKey: 'off',
            }),
          ),
        ],
      );
      addTearDown(unknown.dispose);
      expect(unknown.read(appearanceProvider), const AppearanceSettings());
    });

    testWidgets('cada opción se guarda, sobrevive a un reinicio y «Restablecer» la deshace', (
      tester,
    ) async {
      final prefs = await _prefs();
      await _pumpPage(tester, prefs);

      await _tap(tester, 'palette-graphite');
      await _tap(tester, 'title-font-cinzel');
      await _tap(tester, 'body-font-atkinsonHyperlegibleNext');
      await _tap(tester, 'appearance-text-xlarge');
      await _tap(tester, 'appearance-class-colors');
      await _tap(tester, 'appearance-textures');
      await _tap(tester, 'appearance-theme-light');
      await _tap(tester, 'appearance-motion-reduced');

      expect(prefs.getString(paletteKey), 'graphite');
      expect(prefs.getString(titleFontKey), 'cinzel');
      expect(prefs.getString(bodyFontKey), 'atkinsonHyperlegibleNext');
      expect(prefs.getString(textSizeKey), 'extraLarge');
      expect(prefs.getBool(classColorsKey), isFalse);
      expect(prefs.getBool(texturesKey), isFalse);
      expect(_selected<TextSizePreference>(tester, 'appearance-text-size'), {
        TextSizePreference.extraLarge,
      });
      expect(
        tester.widget<SwitchListTile>(find.byKey(const Key('appearance-class-colors'))).value,
        isFalse,
      );

      // A fresh start reads every stored choice.
      final restarted = ProviderContainer(
        overrides: [dnd5eSystemsOverride(), localPreferencesProvider.overrideWithValue(prefs)],
      );
      addTearDown(restarted.dispose);
      expect(
        restarted.read(appearanceProvider),
        const AppearanceSettings(
          themeMode: ThemeMode.light,
          palette: AppPalette.graphite,
          titleFont: TitleFont.cinzel,
          bodyFont: BodyFont.atkinsonHyperlegibleNext,
          textSize: TextSizePreference.extraLarge,
          classColors: false,
          textures: false,
        ),
      );
      expect(restarted.read(motionSettingsProvider), MotionPreference.reduced);

      await tester.tap(find.byKey(const Key('appearance-reset')));
      await tester.pumpAndSettle();
      expect(find.text('Personalización restablecida.'), findsOneWidget);
      for (final key in [...appearanceKeys, motionPreferenceKey]) {
        expect(prefs.containsKey(key), isFalse, reason: key);
      }
      final afterReset = ProviderContainer(
        overrides: [dnd5eSystemsOverride(), localPreferencesProvider.overrideWithValue(prefs)],
      );
      addTearDown(afterReset.dispose);
      expect(afterReset.read(appearanceProvider), const AppearanceSettings());
      expect(afterReset.read(motionSettingsProvider), MotionPreference.all);
      expect(_selected<ThemeMode>(tester, 'appearance-theme'), {ThemeMode.dark});
    });

    testWidgets('la vista previa cambia al elegir paleta y fuente', (tester) async {
      await _pumpPage(tester, await _prefs());

      expect(_previewPainter(tester).fill, AppPalette.ember.dark.stone);
      expect(_previewTitleStyle(tester).fontFamily, 'Almendra');
      expect(
        tester.widget<AppIcon>(find.byKey(const Key('appearance-preview-class-icon'))).color,
        classThemeOf('wizard').accentDark,
      );

      await _tap(tester, 'palette-graphite');
      expect(_previewPainter(tester).fill, AppPalette.graphite.dark.stone);
      expect(_previewPainter(tester).border.withValues(alpha: 1), AppPalette.graphite.dark.oldGold);

      await _tap(tester, 'title-font-cinzel');
      expect(_previewTitleStyle(tester).fontFamily, 'Cinzel');

      await _tap(tester, 'title-font-sameAsBody');
      await _tap(tester, 'body-font-lora');
      expect(_previewTitleStyle(tester).fontFamily, 'Lora');

      await _tap(tester, 'appearance-theme-light');
      expect(_previewPainter(tester).fill, AppPalette.graphite.light.stone);

      // Class colours off: the class icon takes the palette accent.
      await _tap(tester, 'appearance-class-colors');
      expect(
        tester.widget<AppIcon>(find.byKey(const Key('appearance-preview-class-icon'))).color,
        AppPalette.graphite.light.oldGold,
      );

      // Textures off: straight borders and no grain.
      final grain = find.descendant(
        of: find.byKey(const Key('appearance-preview-area')),
        matching: find.byWidgetPredicate((w) => w is CustomPaint && w.painter is GrainPainter),
      );
      expect(_previewPainter(tester).brush, isTrue);
      expect(grain, findsOneWidget);
      await _tap(tester, 'appearance-textures');
      expect(_previewPainter(tester).brush, isFalse);
      expect(grain, findsNothing);
    });

    testWidgets('Grafito recomienda desactivar los colores de clase', (tester) async {
      await _pumpPage(tester, await _prefs());
      expect(find.textContaining('se recomienda desactivarlo'), findsNothing);
      await _tap(tester, 'palette-graphite');
      expect(find.textContaining('Con Grafito se recomienda desactivarlo'), findsOneWidget);
    });

    testWidgets('el StatValue de la vista previa abre su desglose', (tester) async {
      await _pumpPage(tester, await _prefs());
      await tester.tap(find.byKey(const Key('stat-preview.ac')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('breakdown-sheet')), findsOneWidget);
      expect(find.text('Anillo de protección'), findsOneWidget);
    });
  });

  group('la app aplica la apariencia', () {
    Future<void> pumpApp(WidgetTester tester, SharedPreferences prefs) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            dnd5eSystemsOverride(),
            localPreferencesProvider.overrideWithValue(prefs),
            fakeServerConfigOverride(),
            authControllerProvider.overrideWith(
              () => FixedAuthController(AuthSignedIn(makeUser())),
            ),
            fakeCampaignsOverride,
            fakeServerInfoOverride,
            sessionsOverride(FakeSessionsRepository()),
          ],
          child: const OpenTrpgApp(),
        ),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('por defecto usa el tema oscuro sin escalar el texto', (tester) async {
      await pumpApp(tester, await _prefs());

      final app = tester.widget<MaterialApp>(find.byType(MaterialApp));
      expect(app.themeMode, ThemeMode.dark);
      final context = tester.element(find.byKey(const Key('home-page')));
      expect(Theme.of(context).brightness, Brightness.dark);
      expect(MediaQuery.textScalerOf(context).scale(10), 10);
      expect(MotionScope.reducedOf(context), isFalse);
    });

    testWidgets('respeta tema claro, texto grande y animaciones reducidas', (tester) async {
      await pumpApp(
        tester,
        await _prefs({themeModeKey: 'light', textSizeKey: 'large', motionPreferenceKey: 'reduced'}),
      );

      final app = tester.widget<MaterialApp>(find.byType(MaterialApp));
      expect(app.themeMode, ThemeMode.light);
      final context = tester.element(find.byKey(const Key('home-page')));
      expect(Theme.of(context).brightness, Brightness.light);
      expect(MediaQuery.textScalerOf(context).scale(10), closeTo(12, 0.001));
      expect(MotionScope.reducedOf(context), isTrue);
    });

    testWidgets('MaterialApp usa la paleta, las fuentes y las superficies elegidas', (
      tester,
    ) async {
      await pumpApp(
        tester,
        await _prefs({
          paletteKey: 'graphite',
          titleFontKey: 'cinzel',
          bodyFontKey: 'atkinsonHyperlegibleNext',
          classColorsKey: false,
          texturesKey: false,
          textSizeKey: 'extraLarge',
        }),
      );

      final app = tester.widget<MaterialApp>(find.byType(MaterialApp));
      expect(app.theme!.extension<AppTokens>(), same(AppPalette.graphite.light));
      expect(app.darkTheme!.extension<AppTokens>(), same(AppPalette.graphite.dark));
      expect(app.darkTheme!.colorScheme.primary, AppPalette.graphite.dark.ember);
      expect(app.darkTheme!.textTheme.titleLarge!.fontFamily, 'Cinzel');
      expect(app.darkTheme!.textTheme.bodyMedium!.fontFamily, 'AtkinsonHyperlegibleNext');
      expect(app.theme!.textTheme.labelLarge!.fontFamily, 'AtkinsonHyperlegibleNext');
      expect(
        app.darkTheme!.extension<AppStyle>(),
        const AppStyle(classColors: false, textures: false),
      );
      expect(AppTypography.numeric.fontFamily, 'AtkinsonHyperlegibleNext');

      final context = tester.element(find.byKey(const Key('home-page')));
      expect(context.tokens, same(AppPalette.graphite.dark));
      expect(Theme.of(context).scaffoldBackgroundColor, AppPalette.graphite.dark.obsidian);
      expect(context.appStyle.classColors, isFalse);
      expect(MediaQuery.textScalerOf(context).scale(10), closeTo(14, 0.001));
    });

    testWidgets('cambiar la paleta en Personalización repinta la app al instante', (tester) async {
      final prefs = await _prefs();
      await pumpApp(tester, prefs);
      final container = ProviderScope.containerOf(
        tester.element(find.byKey(const Key('home-page'))),
      );

      container.read(appearanceProvider.notifier).selectPalette(AppPalette.forest);
      container.read(appearanceProvider.notifier).selectTitleFont(TitleFont.imFellEnglish);
      await tester.pumpAndSettle();

      final context = tester.element(find.byKey(const Key('home-page')));
      expect(context.tokens, same(AppPalette.forest.dark));
      expect(Theme.of(context).textTheme.titleLarge!.fontFamily, 'IMFellEnglish');
      // IM Fell English has no bold: titles stay regular.
      expect(Theme.of(context).textTheme.titleLarge!.fontWeight, FontWeight.w400);
      expect(prefs.getString(paletteKey), 'forest');
    });
  });
}
