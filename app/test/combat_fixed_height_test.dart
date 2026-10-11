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

/// A phone in portrait.
const _phone = Size(400, 850);

FakeCharactersRepository _repo() => FakeCharactersRepository(
  characters: [makeCharacterJson(status: 'Active', combat: makeCombatJson())],
);

/// The character page of `ch1` on its "Combate" tab, on a phone.
Future<void> _pumpCharacterPage(WidgetTester tester) async {
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
  tester.view.physicalSize = _phone;
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

/// Top of the scrollable content of the combat view ("Acciones" open).
double _combatTop(WidgetTester tester) =>
    tester.getTopLeft(find.byKey(const Key('combat-section-actions'))).dy;

void main() {
  group('alto fijo del combate en un móvil', () {
    testWidgets('en "Personaje › Combate" lo desplazable empieza antes de 260 px', (tester) async {
      await _pumpCharacterPage(tester);
      expect(find.byKey(const Key('combat-view')), findsOneWidget);
      // Nothing of the sheet header above the combat: only the app bar with
      // the name, the tabs, the two status rows and the row of sections.
      expect(find.byKey(const Key('character-status')), findsNothing);
      expect(find.byKey(const Key('character-edit')), findsNothing);
      expect(find.byKey(const Key('character-title')), findsOneWidget);
      expect(find.byKey(const Key('combat-tab-actions')), findsOneWidget);
      expect(_combatTop(tester), lessThanOrEqualTo(260));
      expect(tester.takeException(), isNull);

      // "Detalle" brings the header back, over the sub-tabs.
      await tester.tap(find.byKey(const Key('tab-detail')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('character-status')), findsOneWidget);
      expect(find.byKey(const Key('character-edit')), findsOneWidget);
      expect(find.byKey(const Key('character-detail-tabs')), findsOneWidget);
    });

    testWidgets('en "Mi sesión" lo desplazable empieza antes de 310 px', (tester) async {
      final fakes = AppFakes(
        campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
        characters: _repo(),
      );
      await pumpRealApp(tester, location: '/campaigns/c1/player', fakes: fakes, size: _phone);
      expect(find.byKey(const Key('player-combat')), findsOneWidget);
      // The name and the subview switch share one row.
      final name = tester.getRect(find.text('Thorin'));
      final switcher = tester.getRect(find.byKey(const Key('player-subview')));
      expect(name.right <= switcher.left, isTrue);
      expect((name.center.dy - switcher.center.dy).abs(), lessThan(12));
      expect(find.byKey(const Key('player-character-name')), findsNothing);
      expect(_combatTop(tester), lessThanOrEqualTo(310));
      expect(tester.takeException(), isNull);
    });
  });
}
