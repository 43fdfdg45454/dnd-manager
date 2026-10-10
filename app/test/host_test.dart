import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:opentrpg/main.dart';
import 'package:opentrpg_core/core/auth/auth_controller.dart';
import 'package:opentrpg_core/core/auth/auth_state.dart';
import 'package:opentrpg_core/core/router/app_router.dart';
import 'package:opentrpg_core/core/systems/system_registry.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_dnd5e/dnd5e_ui.dart';

import 'helpers/fakes.dart';

/// Full paths of [routes] and their children, under [parent].
Iterable<String> _paths(List<RouteBase> routes, [String parent = '']) sync* {
  for (final route in routes) {
    var path = parent;
    if (route is GoRoute) {
      path = route.path.startsWith('/')
          ? route.path
          : '${parent.endsWith('/') ? parent : '$parent/'}${route.path}';
      yield path;
    }
    yield* _paths(route.routes, path);
  }
}

/// The host (`opentrpg`): registers the systems and keeps the assets and fonts
/// the packages load with the same keys.
void main() {
  test('el anfitrión registra D&D 5e', () {
    final container = ProviderContainer(
      overrides: [gameSystemsProvider.overrideWithValue(hostGameSystems)],
    );
    addTearDown(container.dispose);
    expect(container.read(gameSystemUiProvider('dnd5e')), isA<Dnd5eUi>());
  });

  test('el router incluye las rutas del módulo 5e', () {
    final container = ProviderContainer(
      overrides: [
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
        fakeServerConfigOverride(),
        gameSystemsProvider.overrideWithValue(hostGameSystems),
      ],
    );
    addTearDown(container.dispose);
    final paths = _paths(container.read(routerProvider).configuration.routes).toSet();
    expect(paths, containsAll(['/characters/:id/level-up', '/compendium/spells/:index']));
  });

  testWidgets('los paquetes cargan un icono SVG del anfitrión', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(body: AppIcon(AppIcons.d20, key: Key('host-icon'))),
      ),
    );
    await tester.runAsync(() => rootBundle.load(AppIcons.d20.assetPath));
    await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 200)));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    // The picture is drawn (not the placeholder) and the fallback Material
    // Icon is not.
    expect(
      find.descendant(
        of: find.byKey(const Key('host-icon')),
        matching: find.byWidgetPredicate((w) => '${w.runtimeType}'.contains('RawPicture')),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(of: find.byKey(const Key('host-icon')), matching: find.byType(Icon)),
      findsNothing,
    );
  });

  test('las fuentes empaquetadas están declaradas en el anfitrión', () async {
    final manifest = jsonDecode(await rootBundle.loadString('FontManifest.json')) as List;
    final families = {for (final f in manifest) (f as Map)['family'] as String};
    expect(
      families,
      containsAll([
        'Almendra',
        'Cinzel',
        'IMFellEnglish',
        'SourceSans3',
        'AtkinsonHyperlegibleNext',
        'Lora',
      ]),
    );
    final loader = FontLoader('Almendra')
      ..addFont(rootBundle.load('assets/fonts/Almendra-Regular.ttf'));
    await loader.load();
  });
}
