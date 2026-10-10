import 'package:opentrpg/core/auth/auth_controller.dart';
import 'package:opentrpg/core/auth/auth_state.dart';
import 'package:opentrpg/core/router/app_router.dart';
import 'package:opentrpg/core/storage/local_preferences.dart';
import 'package:opentrpg/features/campaigns/data/campaigns_repository.dart';
import 'package:opentrpg/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg/features/catalog/data/beast_models.dart';
import 'package:opentrpg/features/catalog/data/catalog_repository.dart';
import 'package:opentrpg/features/catalog/ui/beast_page.dart';
import 'package:opentrpg/features/characters/data/models.dart';
import 'package:opentrpg/features/characters/domain/change_details.dart';
import 'package:opentrpg/features/characters/ui/character_page.dart';
import 'package:opentrpg/features/dice/data/dice_controller.dart';
import 'package:opentrpg/features/items/data/inventory_repository.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'dice_test.dart' show SequenceRandom;
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';
import 'helpers/motion.dart';

// Animal companion (phase 25, block 6): the picker filtered by the feature and
// the companion's block in the Combat tab with its breakdowns.

const _wolf = Beast(
  index: 'wolf',
  name: 'Wolf',
  size: 'Medium',
  challengeRating: 0.25,
  challengeRatingText: '1/4',
  armorClass: 13,
  hitPoints: 11,
  speeds: {'walk': 40},
);

const _cat = Beast(
  index: 'cat',
  name: 'Cat',
  size: 'Tiny',
  challengeRating: 0,
  challengeRatingText: '0',
  armorClass: 12,
  hitPoints: 2,
  speeds: {'walk': 40, 'climb': 30},
);

const _boar = Beast(
  index: 'boar',
  name: 'Boar',
  size: 'Medium',
  challengeRating: 0.25,
  challengeRatingText: '1/4',
  armorClass: 11,
  hitPoints: 11,
  speeds: {'walk': 40},
);

const _bear = Beast(
  index: 'brown-bear',
  name: 'Brown Bear',
  size: 'Large',
  challengeRating: 1,
  challengeRatingText: '1',
  armorClass: 11,
  hitPoints: 34,
  speeds: {'walk': 40, 'climb': 30},
);

FakeCharactersRepository _repo({Map<String, dynamic>? companion, bool isDm = false}) =>
    FakeCharactersRepository(
      isDm: isDm,
      characters: [
        {
          ...makeCharacterJson(
            status: 'Active',
            classes: [
              {
                'classIndex': 'ranger',
                'className': 'Ranger',
                'subclassIndex': 'compas-ejemplo-guardian',
                'subclassName': 'Guardián de ejemplo',
                'level': 3,
                'order': 0,
              },
            ],
            combat: makeCombatJson(),
          ),
          'companionFeature': makeCompanionFeatureJson(),
          'companionPending': companion == null,
          'companion': ?companion,
        },
      ],
    );

Future<void> _pump(
  WidgetTester tester, {
  required FakeCharactersRepository characters,
  FakeCatalogRepository? catalog,
  CampaignRole role = CampaignRole.player,
  String tab = 'combat',
}) async {
  SharedPreferences.setMockInitialValues({'character.ch1.tab': tab});
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
  tester.view.physicalSize = const Size(800, 7000);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
        campaignsRepositoryProvider.overrideWithValue(
          FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
        ),
        ...fakeCharactersOverrides(characters),
        inventoryRepositoryProvider.overrideWithValue(FakeInventoryRepository()),
        catalogRepositoryProvider.overrideWithValue(
          catalog ?? FakeCatalogRepository(beastList: const [_wolf, _cat, _boar, _bear]),
        ),
        diceRandomProvider.overrideWithValue(SequenceRandom.always(14)),
        localPreferencesProvider.overrideWithValue(prefs),
      ],
      child: MaterialApp.router(routerConfig: router, builder: reducedMotionBuilder),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _tap(WidgetTester tester, String key) async {
  await tester.ensureVisible(find.byKey(Key(key)));
  await tester.tap(find.byKey(Key(key)));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('el selector solo ofrece las bestias del filtro y guarda la elegida', (tester) async {
    final characters = _repo();
    final catalog = FakeCatalogRepository(beastList: const [_wolf, _cat, _boar, _bear]);
    await _pump(tester, characters: characters, catalog: catalog);

    expect(find.byKey(const Key('companion-pending')), findsOneWidget);
    expect(
      find.text('Vínculo de ejemplo: elige una bestia (VD 1/4 o menos, tamaño pequeña o mediana).'),
      findsOneWidget,
    );

    await _tap(tester, 'companion-choose');
    expect(catalog.beastCalls.last, (maxCr: 0.25, fly: null, swim: null));
    // CR and size: the cat is Tiny, the bear CR 1 and Large.
    expect(find.byKey(const Key('companion-beast-wolf')), findsOneWidget);
    expect(find.byKey(const Key('companion-beast-boar')), findsOneWidget);
    expect(find.byKey(const Key('companion-beast-cat')), findsNothing);
    expect(find.byKey(const Key('companion-beast-brown-bear')), findsNothing);

    await tester.tap(find.byKey(const Key('companion-beast-wolf')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('companion-name')), 'Ceniza');
    await tester.tap(find.byKey(const Key('companion-name-save')));
    await tester.pumpAndSettle();

    expect(characters.companionChoices.single, (beastIndex: 'wolf', name: 'Ceniza'));
    expect(find.byKey(const Key('companion-card')), findsOneWidget);
    expect(find.text('Compañero: Ceniza'), findsOneWidget);
    expect(find.byKey(const Key('companion-pending')), findsNothing);
  });

  testWidgets('el bloque del compañero explica cada valor y tira sus ataques', (tester) async {
    final characters = _repo(companion: makeCompanionJson());
    await _pump(tester, characters: characters);

    expect(find.text('Wolf · Mediana · VD 1/4 · 40 pies'), findsOneWidget);
    expect(find.byKey(const Key('companion-hp')), findsOneWidget);

    await _tap(tester, 'stat-companion.armorClass');
    expect(find.text('CA de la bestia'), findsOneWidget);
    expect(find.text('Competencia del personaje'), findsOneWidget);
    await tester.tapAt(const Offset(400, 10));
    await tester.pumpAndSettle();

    await _tap(tester, 'stat-companion.hitPointsMax');
    expect(find.text('4 × Nivel de explorador (3)'), findsOneWidget);
    await tester.tapAt(const Offset(400, 10));
    await tester.pumpAndSettle();

    await _tap(tester, 'stat-companion.attack.1');
    expect(find.text('Ataque de la bestia'), findsOneWidget);
    await tester.tapAt(const Offset(400, 10));
    await tester.pumpAndSettle();

    // The multiattack has nothing to roll; the bite rolls d20 (14) + 6.
    expect(find.byKey(const Key('companion-attack-0')), findsNothing);
    expect(find.text('Daño 2d4+4'), findsOneWidget);
    await _tap(tester, 'companion-attack-1');
    expect(find.text('20'), findsWidgets);
    expect(find.text('Bite: ataque'), findsOneWidget);
  });

  testWidgets('los PG del compañero se ajustan sin aprobación', (tester) async {
    final characters = _repo(companion: makeCompanionJson());
    await _pump(tester, characters: characters);

    await tester.enterText(find.byKey(const Key('companion-hp-amount')), '5');
    await _tap(tester, 'companion-hp-minus');
    expect(characters.companionHpCalls.single, (delta: -5, current: null));
    expect(find.text('7 / '), findsOneWidget);

    await _tap(tester, 'companion-hp-plus');
    expect(characters.companionHpCalls.last, (delta: 5, current: null));
    expect(find.text('12 / '), findsOneWidget);
  });

  testWidgets('cambiar la bestia como jugador se envía al DM', (tester) async {
    final characters = _repo(companion: makeCompanionJson());
    await _pump(tester, characters: characters);

    await _tap(tester, 'companion-menu');
    await tester.tap(find.text('Cambiar bestia'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('companion-beast-boar')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('companion-name-save')));
    await tester.pumpAndSettle();

    expect(characters.companionChoices.single, (beastIndex: 'boar', name: 'Ceniza'));
    expect(characters.requests.single.type, ChangeRequestType.companion);
    expect(find.text('Enviado al DM para aprobación'), findsOneWidget);
    // The DM-only option is not offered to the player.
    await _tap(tester, 'companion-menu');
    expect(find.text('Quitar compañero'), findsNothing);
  });

  testWidgets('la hoja también ofrece elegir compañero', (tester) async {
    await _pump(tester, characters: _repo(), tab: 'summary');

    expect(find.text('Compañero animal'), findsWidgets);
    expect(find.byKey(const Key('companion-choose')), findsOneWidget);
  });

  test('el detalle de una solicitud de compañero compara bestia y nombre', () {
    final request = makeChangeRequest(
      type: 'Companion',
      payload: {'beastIndex': 'boar', 'beastName': 'Boar', 'name': 'Colmillo'},
      before: {'beastIndex': 'wolf', 'beastName': 'Wolf', 'name': 'Ceniza'},
    );

    final detail = describeChange(request) as SheetChangeDetail;
    expect(detail.fields, [
      (label: 'Bestia', before: 'Wolf', after: 'Boar'),
      (label: 'Nombre', before: 'Ceniza', after: 'Colmillo'),
    ]);
    expect(request.type.label, 'Compañero animal');
  });

  testWidgets('el selector abre el detalle de una bestia sin elegirla', (tester) async {
    final characters = _repo();
    await _pump(tester, characters: characters);

    await _tap(tester, 'companion-choose');
    await _tap(tester, 'detail-beast-wolf');
    expect(find.byType(BeastPage), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();
    expect(find.byType(BeastPage), findsNothing);
    expect(find.byKey(const Key('companion-beast-wolf')), findsOneWidget);
    expect(characters.companionChoices, isEmpty);
  });
}
