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
import 'package:opentrpg_core/features/dice/data/dice_controller.dart';
import 'package:opentrpg_core/features/items/data/inventory_repository.dart';
import 'package:opentrpg_dnd5e/catalog/data/catalog_repository.dart';
import 'package:opentrpg_dnd5e/catalog/data/models.dart';
import 'package:opentrpg_dnd5e/catalog/ui/feature_detail_page.dart';
import 'package:opentrpg_dnd5e/catalog/ui/race_detail_page.dart';
import 'package:opentrpg_dnd5e/catalog/ui/spell_detail_page.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'dice_test.dart' show SequenceRandom;
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/combat_tabs.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';
import 'helpers/motion.dart';

/// Opens the combat view of `ch1` with the fakes and [prefs] (fresh ones in
/// the combat view when null).
Future<SharedPreferences> _pump(
  WidgetTester tester, {
  required FakeCharactersRepository characters,
  FakeCatalogRepository? catalog,
  SharedPreferences? prefs,
  CampaignRole role = CampaignRole.player,
}) async {
  if (prefs == null) {
    SharedPreferences.setMockInitialValues({'character.ch1.tab': 'combat'});
    prefs = await SharedPreferences.getInstance();
  }
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
        dnd5eSystemsOverride(),
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
        campaignsRepositoryProvider.overrideWithValue(
          FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
        ),
        ...fakeCharactersOverrides(characters),
        inventoryRepositoryProvider.overrideWithValue(FakeInventoryRepository()),
        catalogRepositoryProvider.overrideWithValue(catalog ?? FakeCatalogRepository()),
        diceRandomProvider.overrideWithValue(SequenceRandom.always(10)),
        localPreferencesProvider.overrideWithValue(prefs),
      ],
      child: MaterialApp.router(routerConfig: router, builder: reducedMotionBuilder),
    ),
  );
  await tester.pumpAndSettle();
  return prefs;
}

Future<void> _tap(WidgetTester tester, String key) async {
  await tester.tap(find.byKey(Key(key)));
  await tester.pumpAndSettle();
}

FakeCharactersRepository _repo({
  Map<String, dynamic>? combat,
  List<Map<String, dynamic>> spells = const [],
  List<Map<String, dynamic>> spellcasting = const [],
}) {
  final json = makeCharacterJson(
    status: 'Active',
    spells: spells,
    combat: combat ?? makeCombatJson(),
  );
  if (spellcasting.isNotEmpty) {
    (json['sheet'] as Map<String, dynamic>)['spellcasting'] = spellcasting;
  }
  return FakeCharactersRepository(characters: [json]);
}

Map<String, dynamic> _resource(
  String id,
  String name, {
  String? key,
  int max = 1,
  int used = 0,
  String recharge = 'LongRest',
  String? sourceKind,
  String? sourceIndex,
  String? sourceDescription,
}) => {
  'id': id,
  'key': ?key,
  'name': name,
  'max': max,
  'used': used,
  'recharge': recharge,
  'isAuto': true,
  'sourceKind': ?sourceKind,
  'sourceIndex': ?sourceIndex,
  'sourceDescription': ?sourceDescription,
};

ChoiceChip _chip(WidgetTester tester, String section) =>
    tester.widget<ChoiceChip>(find.byKey(Key('combat-tab-$section')));

void main() {
  group('pestañas de combate', () {
    testWidgets('la cabecera de estado sigue a la vista en todas las pestañas', (tester) async {
      await _pump(tester, characters: _repo());
      final header = find.byKey(const Key('combat-status'));
      for (final section in ['actions', 'spells', 'resources', 'status', 'more']) {
        await openCombatSection(tester, section);
        expect(header, findsOneWidget, reason: section);
        expect(find.byKey(const Key('hp-minus')), findsOneWidget, reason: section);
        expect(find.byKey(const Key('condition-add')), findsOneWidget, reason: section);
        // The six stats live in their own tab, never in the header.
        expect(
          find.descendant(of: header, matching: find.byKey(const Key('combat-stats-grid'))),
          findsNothing,
          reason: section,
        );
        expect(
          find.byKey(const Key('combat-stats-grid')),
          section == 'status' ? findsOneWidget : findsNothing,
          reason: section,
        );
        expect(_chip(tester, section).selected, isTrue, reason: section);
      }
      // Each tab builds only its own content.
      expect(find.byKey(const Key('rest-request-long')), findsOneWidget);
      expect(find.byKey(const Key('attack-roll-0')), findsNothing);
    });

    testWidgets('la cabecera es una fila de 720 px en adelante y una columna debajo', (
      tester,
    ) async {
      final down = FakeCharactersRepository(
        characters: [
          makeCharacterJson(status: 'Active', hitPointsCurrent: 0, combat: makeCombatJson()),
        ],
      );
      await _pump(tester, characters: down);
      // 900 px: one row with the hit points on the left and the death saves
      // over the conditions on the right.
      tester.view.physicalSize = const Size(900, 7000);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('combat-status-wide')), findsOneWidget);
      expect(find.byKey(const Key('combat-status-narrow')), findsNothing);
      Rect rect(String key) => tester.getRect(find.byKey(Key(key)));
      final hp = rect('hp-bar');
      final saves = rect('death-saves');
      final conditions = rect('conditions-card');
      expect(hp.right <= saves.left, isTrue);
      expect(hp.right <= conditions.left, isTrue);
      expect(saves.bottom <= conditions.top, isTrue);
      // Side by side: the saves start before the hit points end.
      expect(saves.top < hp.bottom, isTrue);

      // 400 px: one column, the hit points on top.
      tester.view.physicalSize = const Size(400, 7000);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('combat-status-wide')), findsNothing);
      expect(find.byKey(const Key('combat-status-narrow')), findsOneWidget);
      expect(rect('hp-bar').bottom <= rect('death-saves').top, isTrue);
      expect(rect('death-saves').bottom <= rect('conditions-card').top, isTrue);
      expect(tester.takeException(), isNull);
    });

    testWidgets('la última pestaña elegida se recuerda por personaje', (tester) async {
      final prefs = await _pump(tester, characters: _repo());
      expect(_chip(tester, 'actions').selected, isTrue);

      await openCombatSection(tester, 'resources');
      expect(prefs.getString('character.ch1.combatSection'), 'resources');

      await tester.pumpWidget(const SizedBox());
      await _pump(tester, characters: _repo(), prefs: prefs);
      expect(_chip(tester, 'resources').selected, isTrue);
      expect(find.byKey(const Key('resource-r1-pips')), findsOneWidget);
    });

    testWidgets('una pestaña sin contenido pierde su chip y se vuelve a Acciones', (tester) async {
      SharedPreferences.setMockInitialValues({
        'character.ch1.tab': 'combat',
        'character.ch1.combatSection': 'spells',
      });
      final prefs = await SharedPreferences.getInstance();
      await _pump(
        tester,
        prefs: prefs,
        characters: _repo(
          combat: makeCombatJson(spellSlots: const [], resources: const []),
        ),
      );
      expect(find.byKey(const Key('combat-tab-spells')), findsNothing);
      expect(find.byKey(const Key('combat-tab-resources')), findsNothing);
      expect(find.byKey(const Key('combat-tab-more')), findsOneWidget);
      expect(_chip(tester, 'actions').selected, isTrue);
      expect(find.byKey(const Key('attack-roll-0')), findsOneWidget);
    });
  });

  group('origen de los recursos', () {
    final catalog = FakeCatalogRepository(
      featureDetails: const {
        'ejemplo-rasgo': Feature(
          index: 'ejemplo-rasgo',
          name: 'Rasgo de ejemplo',
          classIndex: 'fighter',
          level: 1,
          description: ['Texto ficticio del rasgo.'],
        ),
      },
      spellDetails: {
        'shield': SpellDetail.fromJson(const {'index': 'shield', 'name': 'Shield', 'level': 1}),
      },
      raceDetails: const {'human': RaceDetail(index: 'human', name: 'Human')},
    );

    FakeCharactersRepository repo() => _repo(
      combat: makeCombatJson(
        resources: [
          _resource('f1', 'Rasgo de ejemplo', sourceKind: 'feature', sourceIndex: 'ejemplo-rasgo'),
          _resource('s1', 'Shield', key: 'race.shield', sourceKind: 'spell', sourceIndex: 'shield'),
          _resource(
            'o1',
            'Opción de ejemplo',
            sourceKind: 'option',
            sourceIndex: 'ejemplo-opcion',
            sourceDescription: 'Primer párrafo ficticio.\n\nSegundo párrafo ficticio.',
          ),
          _resource(
            't1',
            'Rasgo racial de ejemplo (raza)',
            sourceKind: 'trait',
            sourceIndex: 'ejemplo',
            sourceDescription: 'Texto ficticio del rasgo racial.',
          ),
          _resource('t2', 'Rasgo de subraza (subraza)', sourceKind: 'trait', sourceIndex: 'sub'),
          _resource('m1', 'Recurso manual'),
        ],
      ),
    );

    testWidgets('un rasgo de clase abre su detalle', (tester) async {
      await _pump(tester, characters: repo(), catalog: catalog);
      await openCombatSection(tester, 'resources');
      await _tap(tester, 'resource-f1-info');
      expect(find.byType(FeatureDetailPage), findsOneWidget);
      expect(find.textContaining('Texto ficticio del rasgo.'), findsOneWidget);
    });

    testWidgets('un conjuro con usos abre el conjuro', (tester) async {
      await _pump(tester, characters: repo(), catalog: catalog);
      await openCombatSection(tester, 'resources');
      await _tap(tester, 'resource-s1-info');
      expect(find.byType(SpellDetailPage), findsOneWidget);
    });

    testWidgets('una opción o una dote muestran su descripción en una hoja', (tester) async {
      await _pump(tester, characters: repo(), catalog: catalog);
      await openCombatSection(tester, 'resources');
      await _tap(tester, 'resource-o1-info');
      final sheet = find.byKey(const Key('rules-text-sheet'));
      expect(sheet, findsOneWidget);
      expect(find.descendant(of: sheet, matching: find.text('Opción de ejemplo')), findsOneWidget);
      expect(find.text('Primer párrafo ficticio.'), findsOneWidget);
      expect(find.text('Segundo párrafo ficticio.'), findsOneWidget);
    });

    testWidgets('un rasgo racial abre el propio rasgo, no la raza; sin origen no hay botón', (
      tester,
    ) async {
      await _pump(tester, characters: repo(), catalog: catalog);
      await openCombatSection(tester, 'resources');
      expect(find.byKey(const Key('resource-m1-info')), findsNothing);

      await _tap(tester, 'resource-t1-info');
      expect(find.byType(RaceDetailPage), findsNothing);
      final sheet = find.byKey(const Key('rules-text-sheet'));
      expect(sheet, findsOneWidget);
      // Titled with the trait alone, without the "(raza)" suffix of the resource.
      expect(
        find.descendant(of: sheet, matching: find.text('Rasgo racial de ejemplo')),
        findsOneWidget,
      );
      expect(find.text('Texto ficticio del rasgo racial.'), findsOneWidget);
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();
      expect(sheet, findsNothing);

      // Without a description the sheet says so.
      await _tap(tester, 'resource-t2-info');
      expect(find.descendant(of: sheet, matching: find.text('Rasgo de subraza')), findsOneWidget);
      expect(find.text('Sin descripción.'), findsOneWidget);
    });
  });

  group('conjuros concedidos con usos por descanso', () {
    final shield = SpellDetail.fromJson(const {'index': 'shield', 'name': 'Shield', 'level': 1});
    final holdPerson = SpellDetail.fromJson(const {
      'index': 'hold-person',
      'name': 'Hold Person',
      'level': 2,
      'concentration': true,
    });
    final catalog = FakeCatalogRepository(
      spellDetails: {shield.index: shield, holdPerson.index: holdPerson},
    );

    FakeCharactersRepository granted({int used = 0}) => _repo(
      spells: const [
        {'spellIndex': 'shield', 'classIndex': 'race'},
        {'spellIndex': 'hold-person', 'classIndex': 'race'},
      ],
      combat: makeCombatJson(
        spellSlots: [
          {'level': 1, 'max': 2, 'used': 0},
          {'level': 2, 'max': 1, 'used': 0},
        ],
        resources: [
          _resource('u1', 'Shield', key: 'race.shield', used: used),
          _resource('u2', 'Hold Person', key: 'race.hold-person', max: 2),
        ],
      ),
    );

    String text(WidgetTester tester, String key) =>
        tester.widget<Text>(find.byKey(Key(key))).data ?? '';

    testWidgets('"Lanzar" gasta un uso del conjuro y no un espacio', (tester) async {
      final repo = granted();
      await _pump(tester, characters: repo, catalog: catalog);
      expect(text(tester, 'spell-cost-shield'), '1/descanso largo');
      // No level to choose: the use pays for the spell's own level.
      expect(find.byKey(const Key('spell-level-shield')), findsNothing);

      await _tap(tester, 'spell-spend-shield');
      expect(repo.spellCasts.single, (
        spellIndex: 'shield',
        classIndex: 'race',
        level: 1,
        ritual: false,
      ));
      expect(repo.resourceSpends, [(id: 'u1', amount: 1)]);
      expect(repo.slotSpends, isEmpty);
      expect(find.text('Uso de Shield gastado (quedan 0).'), findsOneWidget);

      // Without uses the button says until when.
      final cast = find.byKey(const Key('spell-spend-shield'));
      expect(tester.widget<ButtonStyleButton>(cast).onPressed, isNull);
      expect(
        find.descendant(of: cast, matching: find.text('Sin usos hasta el descanso largo')),
        findsOneWidget,
      );
    });

    testWidgets('sin usos el botón está desactivado desde el principio', (tester) async {
      final repo = granted(used: 1);
      await _pump(tester, characters: repo, catalog: catalog);
      final cast = find.byKey(const Key('spell-spend-shield'));
      expect(tester.widget<ButtonStyleButton>(cast).onPressed, isNull);
      expect(find.text('Sin usos hasta el descanso largo'), findsOneWidget);
      expect(repo.spellCasts, isEmpty);
    });

    testWidgets('un conjuro de concentración concedido se concentra tras gastar el uso', (
      tester,
    ) async {
      final repo = granted();
      await _pump(tester, characters: repo, catalog: catalog);
      await _tap(tester, 'spell-spend-hold-person');
      expect(repo.resourceSpends, [(id: 'u2', amount: 1)]);
      expect(repo.concentrationCalls, ['hold-person']);
      expect(
        find.text('Uso de Hold Person gastado (quedan 1). Concentrándote en Hold Person.'),
        findsOneWidget,
      );
      expect(find.byKey(const Key('concentration-chip')), findsOneWidget);
    });
  });

  group('pestaña Conjuros', () {
    final detectMagic = SpellDetail.fromJson(const {
      'index': 'detect-magic',
      'name': 'Detect Magic',
      'level': 1,
      'ritual': true,
      'concentration': true,
    });
    final shield = SpellDetail.fromJson(const {'index': 'shield', 'name': 'Shield', 'level': 1});
    final catalog = FakeCatalogRepository(
      spellDetails: {detectMagic.index: detectMagic, shield.index: shield},
    );

    FakeCharactersRepository wizard() => _repo(
      spells: const [
        {'spellIndex': 'detect-magic', 'classIndex': 'wizard'},
        {'spellIndex': 'shield', 'classIndex': 'wizard', 'isPrepared': true},
      ],
      spellcasting: const [
        {
          'classIndex': 'wizard',
          'ability': 'int',
          'saveDc': 13,
          'attackBonus': 5,
          'preparedMax': 3,
        },
      ],
    );

    testWidgets('filtra preparados y todos, y lanza un ritual sin gastar espacio', (tester) async {
      final repo = wizard();
      await _pump(tester, characters: repo, catalog: catalog);
      await openCombatSection(tester, 'spells');
      expect(find.byKey(const Key('combat-slot-1')), findsOneWidget);
      expect(find.byKey(const Key('spells-prepare')), findsOneWidget);
      expect(find.byKey(const Key('combat-spell-shield')), findsOneWidget);
      expect(find.byKey(const Key('combat-spell-detect-magic')), findsNothing);

      await tester.tap(find.byKey(const Key('spells-filter-all')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('combat-spell-detect-magic')), findsOneWidget);
      expect(find.text('No preparado · Concentración · Ritual'), findsOneWidget);
      // Not prepared: no "Lanzar", but a wizard casts its rituals from the book.
      expect(find.byKey(const Key('spell-spend-detect-magic')), findsNothing);

      await _tap(tester, 'spell-ritual-detect-magic');
      expect(repo.spellCasts.single.ritual, isTrue);
      expect(repo.slotSpends, isEmpty);
      expect(repo.concentrationCalls, ['detect-magic']);
      expect(
        find.text(
          'Detect Magic lanzado como ritual (10 minutos más). Concentrándote en Detect Magic.',
        ),
        findsOneWidget,
      );
    });

    testWidgets('"Lanzar" desde la lista gasta un espacio de su nivel', (tester) async {
      final repo = wizard();
      await _pump(tester, characters: repo, catalog: catalog);
      await openCombatSection(tester, 'spells');
      expect(
        tester.widget<Text>(find.byKey(const Key('spell-cost-shield'))).data,
        'Espacio de nivel 1',
      );
      await _tap(tester, 'spell-spend-shield');
      expect(repo.spellCasts.single, (
        spellIndex: 'shield',
        classIndex: 'wizard',
        level: 1,
        ritual: false,
      ));
      expect(repo.slotSpends, [(level: 1, amount: 1)]);
      expect(find.text('Espacio de nivel 1 gastado: Shield.'), findsOneWidget);
    });
  });
}
