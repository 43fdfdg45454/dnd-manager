import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:opentrpg_core/core/auth/auth_controller.dart';
import 'package:opentrpg_core/core/auth/auth_state.dart';
import 'package:opentrpg_core/core/router/app_router.dart';
import 'package:opentrpg_core/core/storage/local_preferences.dart';
import 'package:opentrpg_core/features/campaigns/data/campaigns_repository.dart';
import 'package:opentrpg_core/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg_core/features/characters/ui/character_page.dart';
import 'package:opentrpg_core/features/items/data/inventory_repository.dart';
import 'package:opentrpg_dnd5e/catalog/data/catalog_repository.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';
import 'helpers/motion.dart';

/// A phone in landscape and in portrait.
const _landscape = Size(850, 400);
const _portrait = Size(400, 850);

FakeCharactersRepository _repo() => FakeCharactersRepository(
  characters: [makeCharacterJson(status: 'Active', combat: makeCombatJson())],
);

/// The character page of `ch1` on its "Combate" tab, on a phone of [size].
Future<void> _pumpCharacterPage(WidgetTester tester, Size size) async {
  SharedPreferences.setMockInitialValues({'character.ch1.tab': 'combat'});
  final prefs = await SharedPreferences.getInstance();
  final router = GoRouter(
    initialLocation: '/characters/ch1',
    routes: [
      GoRoute(
        path: AppRoutes.characterDetail,
        builder: (_, state) => CharacterPage(characterId: state.pathParameters['id']!),
      ),
    ],
  );
  addTearDown(router.dispose);
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        dnd5eSystemsOverride(),
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
        campaignsRepositoryProvider.overrideWithValue(
          FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
        ),
        ...fakeCharactersOverrides(_repo()),
        inventoryRepositoryProvider.overrideWithValue(FakeInventoryRepository()),
        catalogRepositoryProvider.overrideWithValue(FakeCatalogRepository()),
        localPreferencesProvider.overrideWithValue(prefs),
      ],
      child: MaterialApp.router(routerConfig: router, builder: reducedMotionBuilder),
    ),
  );
  await tester.pumpAndSettle();
}

/// Whether the widget with [key] is built and some of it is on the screen.
bool _onScreen(WidgetTester tester, String key) {
  final finder = find.byKey(Key(key));
  if (finder.evaluate().isEmpty) return false;
  final rect = tester.getRect(finder);
  final screen = Offset.zero & tester.view.physicalSize;
  return rect.overlaps(screen) && rect.bottom > 0;
}

/// Scrolls the combat content by [dy] (negative scrolls down).
Future<void> _scroll(WidgetTester tester, double dy) async {
  await tester.drag(find.byKey(const Key('combat-tabs')), Offset(0, dy), warnIfMissed: false);
  await tester.pumpAndSettle();
}

void main() {
  group('combate en apaisado', () {
    testWidgets('solo las pestañas de sección quedan fijas; el resto vuelve al subir', (
      tester,
    ) async {
      await _pumpCharacterPage(tester, _landscape);
      expect(_onScreen(tester, 'character-title'), isTrue);
      expect(_onScreen(tester, 'character-tabs'), isTrue);
      expect(_onScreen(tester, 'combat-status'), isTrue);
      expect(_onScreen(tester, 'combat-tabs'), isTrue);
      final tabsTop = tester.getTopLeft(find.byKey(const Key('combat-tabs'))).dy;

      await _scroll(tester, -300);
      expect(_onScreen(tester, 'character-title'), isFalse);
      expect(_onScreen(tester, 'character-tabs'), isFalse);
      expect(_onScreen(tester, 'combat-status'), isFalse);
      expect(_onScreen(tester, 'combat-tabs'), isTrue);
      // Pinned at the very top once the rest has scrolled away.
      expect(tester.getTopLeft(find.byKey(const Key('combat-tabs'))).dy, lessThan(tabsTop));
      expect(tester.getTopLeft(find.byKey(const Key('combat-tabs'))).dy, 0);
      expect(tester.takeException(), isNull);

      // A short scroll up brings the header and the app bar back.
      await _scroll(tester, 50);
      expect(_onScreen(tester, 'combat-status'), isTrue);
      expect(_onScreen(tester, 'character-tabs'), isTrue);
      expect(_onScreen(tester, 'combat-tabs'), isTrue);
      expect(tester.takeException(), isNull);
    });

    testWidgets('en "Mi sesión" la cabecera de estado también se va al bajar', (tester) async {
      final fakes = AppFakes(
        campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
        characters: _repo(),
      );
      await pumpRealApp(tester, location: '/campaigns/c1/player', fakes: fakes, size: _landscape);
      expect(find.byKey(const Key('player-combat')), findsOneWidget);
      expect(_onScreen(tester, 'combat-status'), isTrue);

      await _scroll(tester, -300);
      expect(_onScreen(tester, 'combat-status'), isFalse);
      expect(_onScreen(tester, 'combat-tabs'), isTrue);
      expect(_onScreen(tester, 'player-subview'), isTrue);

      await _scroll(tester, 50);
      expect(_onScreen(tester, 'combat-status'), isTrue);
      expect(tester.takeException(), isNull);
    });
  });

  group('combate en vertical', () {
    testWidgets('la cabecera de estado y las pestañas no se mueven al desplazar', (tester) async {
      await _pumpCharacterPage(tester, _portrait);
      final status = tester.getRect(find.byKey(const Key('combat-status')));
      final tabs = tester.getRect(find.byKey(const Key('combat-tabs')));
      final title = tester.getRect(find.byKey(const Key('character-title')));

      await tester.drag(
        find.byKey(const Key('combat-section-actions')),
        const Offset(0, -300),
        warnIfMissed: false,
      );
      await tester.pumpAndSettle();
      expect(tester.getRect(find.byKey(const Key('combat-status'))), status);
      expect(tester.getRect(find.byKey(const Key('combat-tabs'))), tabs);
      expect(tester.getRect(find.byKey(const Key('character-title'))), title);
      expect(tester.takeException(), isNull);
    });
  });
}
