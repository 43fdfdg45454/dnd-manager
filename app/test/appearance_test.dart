import 'package:dnd_companion/app.dart';
import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/motion/motion_settings.dart';
import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/core/storage/local_preferences.dart';
import 'package:dnd_companion/core/theme/app_theme.dart';
import 'package:dnd_companion/features/settings/data/appearance_controller.dart';
import 'package:dnd_companion/features/settings/ui/appearance_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
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
      overrides: [localPreferencesProvider.overrideWithValue(prefs)],
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

void main() {
  group('ajustes de apariencia', () {
    test('por defecto: tema oscuro, texto normal y todas las animaciones', () async {
      final container = ProviderContainer(
        overrides: [localPreferencesProvider.overrideWithValue(await _prefs())],
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
        overrides: [localPreferencesProvider.overrideWithValue(prefs)],
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
        overrides: [localPreferencesProvider.overrideWithValue(prefs)],
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

      await tester.tap(find.byKey(const Key('home-user-menu')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('home-appearance')), findsOneWidget);
      await tester.tap(find.byKey(const Key('home-appearance')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('settings-appearance')), findsOneWidget);
      expect(locationOf(router), AppRoutes.appearance);
    });
  });

  group('la app aplica la apariencia', () {
    Future<void> pumpApp(WidgetTester tester, SharedPreferences prefs) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            localPreferencesProvider.overrideWithValue(prefs),
            fakeServerConfigOverride(),
            authControllerProvider.overrideWith(
              () => FixedAuthController(AuthSignedIn(makeUser())),
            ),
            fakeCampaignsOverride,
            fakeServerInfoOverride,
            sessionsOverride(FakeSessionsRepository()),
          ],
          child: const DndCompanionApp(),
        ),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('por defecto usa el tema oscuro sin escalar el texto', (tester) async {
      await pumpApp(tester, await _prefs());

      final app = tester.widget<MaterialApp>(find.byType(MaterialApp));
      expect(app.themeMode, ThemeMode.dark);
      final context = tester.element(find.byKey(const Key('home-user-menu')));
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
      final context = tester.element(find.byKey(const Key('home-user-menu')));
      expect(Theme.of(context).brightness, Brightness.light);
      expect(MediaQuery.textScalerOf(context).scale(10), closeTo(12, 0.001));
      expect(MotionScope.reducedOf(context), isTrue);
    });
  });
}
