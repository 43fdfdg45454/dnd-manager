import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:opentrpg/core/auth/auth_controller.dart';
import 'package:opentrpg/core/auth/auth_state.dart';
import 'package:opentrpg/core/network/api_client.dart';
import 'package:opentrpg/core/router/app_router.dart';
import 'package:opentrpg/core/storage/local_preferences.dart';
import 'package:opentrpg/features/campaigns/data/campaigns_repository.dart';
import 'package:opentrpg/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg/features/catalog/data/catalog_repository.dart';
import 'package:opentrpg/features/catalog/data/models.dart';
import 'package:opentrpg/features/catalog/ui/spell_detail_page.dart';
import 'package:opentrpg/features/characters/data/models.dart';
import 'package:opentrpg/features/characters/domain/spell_combat.dart';
import 'package:opentrpg/features/characters/ui/character_page.dart';
import 'package:opentrpg/features/characters/ui/combat/class_panels.dart';
import 'package:opentrpg/features/characters/ui/combat/combat_support.dart' show Pip;
import 'package:opentrpg/features/characters/ui/combat/panels/critical_damage_roll.dart';
import 'package:opentrpg/features/characters/ui/combat/resources_section.dart'
    show canRestoreResource;
import 'package:opentrpg/features/characters/ui/combat/vitals_section.dart'
    show ConditionsCard, StatsCard;
import 'package:opentrpg/features/characters/ui/combat/wild_magic_surge.dart'
    show isWildMagicSurgeKey;
import 'package:opentrpg/features/dice/data/dice_controller.dart';
import 'package:opentrpg/systems/dnd5e/session/party_models.dart' show PartyAdjustment;
import 'package:opentrpg/features/session/data/party_repository.dart';
import 'package:opentrpg/features/items/data/inventory_repository.dart';
import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'dice_test.dart' show SequenceRandom;
import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';
import 'helpers/motion.dart';

import 'package:opentrpg/systems/dnd5e/characters/dnd5e_characters_repository.dart';

/// Opens `/characters/ch1` (or [location]) with the fakes. [face] is what every
/// die shows. The view is tall so the whole combat screen is built.
Future<void> _pump(
  WidgetTester tester, {
  required FakeCharactersRepository characters,
  CampaignRole role = CampaignRole.player,
  FakeCatalogRepository? catalog,
  FakeInventoryRepository? inventory,
  int face = 14,
  bool startInCombat = true,
  SharedPreferences? prefs,
  String location = '/characters/ch1',
}) async {
  if (prefs == null) {
    SharedPreferences.setMockInitialValues({if (startInCombat) 'character.ch1.tab': 'combat'});
    prefs = await SharedPreferences.getInstance();
  }
  final router = GoRouter(
    initialLocation: location,
    routes: [
      GoRoute(
        path: '/',
        builder: (_, _) => const Scaffold(body: Text('inicio')),
      ),
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
        inventoryRepositoryProvider.overrideWithValue(inventory ?? FakeInventoryRepository()),
        catalogRepositoryProvider.overrideWithValue(catalog ?? FakeCatalogRepository()),
        diceRandomProvider.overrideWithValue(SequenceRandom.always(face)),
        localPreferencesProvider.overrideWithValue(prefs),
      ],
      child: MaterialApp.router(routerConfig: router, builder: reducedMotionBuilder),
    ),
  );
  await tester.pumpAndSettle();
}

FakeCharactersRepository _repo({
  Map<String, dynamic>? combat,
  int hp = 20,
  int temp = 3,
  List<Map<String, dynamic>>? classes,
  String? ownerUserId = 'u1',
}) => FakeCharactersRepository(
  characters: [
    makeCharacterJson(
      status: 'Active',
      ownerUserId: ownerUserId,
      hitPointsCurrent: hp,
      temporaryHitPoints: temp,
      classes: classes,
      combat: combat ?? makeCombatJson(),
    ),
  ],
);

Future<void> _tap(WidgetTester tester, Object key) async {
  final finder = key is Finder ? key : find.byKey(Key(key as String));
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Map<String, dynamic> _paladinPanel({int loh = 25, int lohUsed = 0}) => {
  'classIndex': 'paladin',
  'level': 5,
  'data': {
    'layOnHands': {'pool': loh, 'used': lohUsed},
    'divineSmite': {
      'slotsByLevel': [
        {'level': 1, 'available': 2, 'extraDice': 2},
        {'level': 2, 'available': 1, 'extraDice': 3},
      ],
    },
    'channelDivinity': {'max': 1, 'used': 0},
    'auraRange': 10,
  },
};

const _paladinClasses = [
  {'classIndex': 'paladin', 'className': 'Paladin', 'level': 5},
];

void main() {
  group('modelo y repositorio', () {
    test('CombatSummary se lee del DTO y tolera su ausencia', () {
      final detail = CharacterDetail.fromJson(
        makeCharacterJson(
          combat: makeCombatJson(
            pactSlots: {'level': 2, 'max': 2, 'used': 1},
            quickConsumables: [
              {'itemId': 'p1', 'name': 'Potion of Healing', 'quantity': 2, 'charges': null},
            ],
            classPanels: [
              {
                'classIndex': 'barbarian',
                'level': 3,
                'data': {'rageDamageBonus': 2},
              },
            ],
            onceSinceLongRest: [
              {'key': 'second-wind', 'name': 'Second Wind', 'used': true},
            ],
          ),
        ),
      );
      final combat = detail.combat;
      final attack = combat.attacks.single;
      expect(attack.name, 'Longsword');
      expect(attack.attackBonus, 5);
      expect(attack.damage, '1d8+3');
      expect(attack.versatileDamage, '1d10+3');
      expect(attack.isRanged, isFalse);
      expect(combat.spellSlots.single.max, 2);
      expect(combat.pactSlots?.level, 2);
      expect(combat.resources.single.name, 'Second Wind');
      expect(combat.quickConsumables.single.quantity, 2);
      expect(combat.panelOf('barbarian')?.data['rageDamageBonus'], 2);
      expect(combat.panelOf('wizard'), isNull);
      expect(combat.onceSinceLongRest.single.used, isTrue);

      final bare = CharacterDetail.fromJson(makeCharacterJson()).combat;
      expect(bare.attacks, isEmpty);
      expect(bare.pactSlots, isNull);
      expect(bare.classPanels, isEmpty);
    });

    test('un ataque con munición o alcance no lanzado es a distancia', () {
      const bow = CombatAttack(name: 'Longbow', properties: ['Ammunition'], range: '150/600');
      const dagger = CombatAttack(name: 'Dagger', properties: ['Thrown'], range: '20/60');
      const sword = CombatAttack(name: 'Longsword');
      expect(bow.isRanged, isTrue);
      expect(dagger.isRanged, isFalse);
      expect(sword.isRanged, isFalse);
    });

    test('classAction y divineSmite usan los endpoints del contrato', () async {
      final adapter = _Adapter();
      final repository = Dnd5eCharactersRepository(
        ApiClient(
          baseUrl: 'http://localhost',
          dio: Dio(BaseOptions(baseUrl: 'http://localhost'))..httpClientAdapter = adapter,
        ),
      );
      adapter.body = makeCharacterJson();
      await repository.classAction('ch1', 'lay-on-hands', {'amount': 5, 'targetSelf': false});
      expect(adapter.requests.last.method, 'POST');
      expect(
        adapter.requests.last.path,
        '/api/v1/systems/dnd5e/characters/ch1/class-actions/lay-on-hands',
      );
      expect(adapter.requests.last.data, {'amount': 5, 'targetSelf': false});
      await repository.classAction('ch1', 'rage');
      expect(adapter.requests.last.path, '/api/v1/systems/dnd5e/characters/ch1/class-actions/rage');
      expect(adapter.requests.last.data, <String, dynamic>{});

      adapter.body = {'character': makeCharacterJson(name: 'Tras el castigo'), 'damageDice': '3d8'};
      final smite = await repository.divineSmite('ch1', 2);
      expect(
        adapter.requests.last.path,
        '/api/v1/systems/dnd5e/characters/ch1/class-actions/divine-smite',
      );
      expect(adapter.requests.last.data, {'slotLevel': 2});
      expect(smite.damageDice, '3d8');
      expect(smite.character.name, 'Tras el castigo');
    });
  });

  group('endpoints de la fase 19', () {
    Future<(Dnd5eCharactersRepository, _Adapter)> repo() async {
      final adapter = _Adapter();
      final repository = Dnd5eCharactersRepository(
        ApiClient(
          baseUrl: 'http://localhost',
          dio: Dio(BaseOptions(baseUrl: 'http://localhost'))..httpClientAdapter = adapter,
        ),
      );
      return (repository, adapter);
    }

    test('el daño usa POST /damage y lee el desenlace de la concentración', () async {
      final (repository, adapter) = await repo();
      adapter.body = {
        'character': makeCharacterJson(hitPointsCurrent: 12),
        'outcome': {
          'characterId': 'ch1',
          'damage': 8,
          'hitPointsCurrent': 12,
          'concentratingOn': 'bless',
          'concentrationCheckDc': 10,
          'concentrationEnded': false,
        },
      };
      final result = await repository.applyDamage('ch1', 8);
      expect(adapter.requests.last.method, 'POST');
      expect(adapter.requests.last.path, '/api/v1/systems/dnd5e/characters/ch1/damage');
      expect(adapter.requests.last.data, {'amount': 8});
      expect(result.character.hitPointsCurrent, 12);
      expect(result.outcome.concentratingOn, 'bless');
      expect(result.outcome.concentrationCheckDc, 10);
      expect(result.outcome.concentrationEnded, isFalse);
      expect(result.outcome.affectsConcentration, isTrue);
    });

    test('elecciones de origen, sustituciones y tiradas usan sus rutas', () async {
      final (repository, adapter) = await repo();
      adapter.body = makeOriginChoicesJson([makeOriginChoiceJson()]);
      final plan = await repository.originChoices('ch1');
      expect(adapter.requests.last.method, 'GET');
      expect(adapter.requests.last.path, '/api/v1/systems/dnd5e/characters/ch1/origin-choices');
      expect(plan.choices.single.kind, OriginChoiceKind.skill);
      expect(plan.choices.single.required, 1);

      await repository.saveOriginChoices('ch1', [
        const LevelUpChoiceAnswer.picks('race.skills', ['insight']),
        const LevelUpChoiceAnswer.feat('race.feat', 'grappler', ability: 'str'),
      ]);
      expect(adapter.requests.last.method, 'PUT');
      expect(adapter.requests.last.path, '/api/v1/systems/dnd5e/characters/ch1/origin-choices');
      expect(adapter.requests.last.data, {
        'choices': [
          {
            'key': 'race.skills',
            'selected': ['insight'],
          },
          {
            'key': 'race.feat',
            'selected': {'feat': 'grappler', 'ability': 'str'},
          },
        ],
      });

      adapter.body = makeCharacterJson();
      await repository.replaceInvalidChoices('ch1', [
        const LevelUpChoiceAnswer.picks('replace.dueling', ['defense']),
      ]);
      expect(adapter.requests.last.method, 'POST');
      expect(adapter.requests.last.path, '/api/v1/systems/dnd5e/characters/ch1/invalid-choices');

      await repository.saveResourceRolls('ch1', 'r1', [14, 3]);
      expect(adapter.requests.last.path, '/api/v1/systems/dnd5e/characters/ch1/resources/r1/rolls');
      expect(adapter.requests.last.data, {
        'values': [14, 3],
      });

      await repository.classAction('ch1', 'natural-recovery', {
        'slotLevels': [2],
      });
      expect(
        adapter.requests.last.path,
        '/api/v1/systems/dnd5e/characters/ch1/class-actions/natural-recovery',
      );
    });

    test('party/adjust devuelve los desenlaces de daño', () async {
      final adapter = _Adapter();
      final repository = PartyRepository(
        ApiClient(
          baseUrl: 'http://localhost',
          dio: Dio(BaseOptions(baseUrl: 'http://localhost'))..httpClientAdapter = adapter,
        ),
      );
      adapter.body = {
        'characters': <Object>[],
        'damage': [
          {
            'characterId': 'ch1',
            'damage': 9,
            'hitPointsCurrent': 0,
            'concentratingOn': 'bless',
            'concentrationCheckDc': null,
            'concentrationEnded': true,
          },
        ],
      };
      final result = await repository.adjust('c1', [
        const PartyAdjustment(characterId: 'ch1', hitPointsDelta: -9),
      ]);
      expect(adapter.requests.last.path, '/api/v1/systems/dnd5e/campaigns/c1/party/adjust');
      expect(result.damage.single.concentrationEnded, isTrue);
      expect(result.damage.single.concentratingOn, 'bless');
    });
  });

  group('concentración', () {
    FakeCharactersRepository concentrating({
      int? dc,
      bool ended = false,
      List<Map<String, dynamic>>? classes,
      Map<String, dynamic>? combat,
    }) =>
        FakeCharactersRepository(
            characters: [
              makeCharacterJson(
                status: 'Active',
                temporaryHitPoints: 0,
                combat: combat ?? makeCombatJson(),
                classes: classes,
                concentratingOnSpellIndex: 'bless',
              ),
            ],
          )
          ..nextConcentrationDc = dc
          ..nextConcentrationEnded = ended;

    testWidgets('el daño a un concentrado pregunta por la salvación con la CD del servidor', (
      tester,
    ) async {
      final repo = concentrating(dc: 12);
      await _pump(tester, characters: repo);
      await tester.enterText(find.byKey(const Key('hp-amount')), '24');
      await _tap(tester, 'hp-minus');

      expect(find.byKey(const Key('concentration-save-dialog')), findsOneWidget);
      expect(
        find.textContaining('¿Superaste la salvación de Constitución (CD 12)?'),
        findsOneWidget,
      );
      await _tap(tester, 'concentration-save-yes');
      expect(repo.concentrationCalls, isEmpty);
      expect(find.byKey(const Key('concentration-chip')), findsOneWidget);
    });

    testWidgets('"No" termina la concentración', (tester) async {
      final repo = concentrating(dc: 10);
      await _pump(tester, characters: repo);
      await _tap(tester, 'hp-minus');
      await _tap(tester, 'concentration-save-no');

      expect(repo.concentrationCalls, [null]);
      expect(find.byKey(const Key('concentration-chip')), findsNothing);
      expect(find.text('Pierdes la concentración en Bless.'), findsOneWidget);
    });

    testWidgets('si la concentración termina sola (0 PG) solo avisa', (tester) async {
      final repo = concentrating(ended: true);
      await _pump(tester, characters: repo);
      await _tap(tester, 'hp-minus');

      expect(find.byKey(const Key('concentration-save-dialog')), findsNothing);
      expect(find.text('Pierdes la concentración en Bless.'), findsOneWidget);
      expect(repo.concentrationCalls, isEmpty);
    });

    testWidgets('sin concentración no se pregunta nada', (tester) async {
      final repo = _repo(temp: 0);
      repo.nextConcentrationDc = 10;
      await _pump(tester, characters: repo);
      await _tap(tester, 'hp-minus');
      expect(find.byKey(const Key('concentration-save-dialog')), findsNothing);
      expect(repo.damageCalls, [1]);
    });

    testWidgets('lanzar Marca del cazador concentrado en otro conjuro pide confirmación', (
      tester,
    ) async {
      final repo = concentrating(
        classes: [
          {'classIndex': 'ranger', 'className': 'Ranger', 'level': 6},
        ],
        combat: makeCombatJson(
          classPanels: [
            {'classIndex': 'ranger', 'level': 6, 'data': <String, dynamic>{}},
          ],
        ),
      );
      await _pump(tester, characters: repo);

      await _tap(tester, 'ranger-hunters-mark');
      expect(find.text('Dejarás de concentrarte en Bless.'), findsOneWidget);
      await _tap(tester, 'concentration-replace-cancel');
      expect(repo.concentrationCalls, isEmpty);

      await _tap(tester, 'ranger-hunters-mark');
      await _tap(tester, 'concentration-replace-confirm');
      expect(repo.concentrationCalls, ['hunters-mark']);
    });
  });

  group('pestaña Combate', () {
    testWidgets('Combate es la primera pestaña y la última elegida se recuerda por personaje', (
      tester,
    ) async {
      final repo = _repo();
      SharedPreferences.setMockInitialValues({'character.otro.tab': 'combat'});
      final prefs = await SharedPreferences.getInstance();
      await _pump(tester, characters: repo, prefs: prefs);
      // Solo cuenta la clave del propio personaje: abre en Resumen.
      final tabs = tester.widget<TabBar>(find.byKey(const Key('character-tabs')));
      expect((tabs.tabs.first as Tab).text, 'Combate');
      expect(tabs.tabs, hasLength(2));
      expect(find.byKey(const Key('combat-view')), findsNothing);
      expect(find.byKey(const Key('view-mode')), findsNothing);

      await _tap(tester, 'tab-combat');
      expect(find.byKey(const Key('combat-view')), findsOneWidget);
      // La cabecera con sus acciones y las pestañas siguen a la vista.
      expect(find.byKey(const Key('character-status')), findsOneWidget);
      expect(find.byKey(const Key('tab-detail')), findsOneWidget);
      expect(prefs.getString('character.ch1.mainView'), 'combat');

      // Una pantalla nueva con las mismas preferencias abre directamente en Combate.
      await tester.pumpWidget(const SizedBox());
      await _pump(tester, characters: repo, prefs: prefs);
      expect(find.byKey(const Key('combat-view')), findsOneWidget);

      await openDetailTab(tester, 'tab-spells');
      expect(find.byKey(const Key('combat-view')), findsNothing);
      expect(prefs.getString('character.ch1.tab'), 'spells');
      expect(prefs.getString('character.ch1.mainView'), 'detail');
    });

    testWidgets('un personaje que quedó en el antiguo modo Combate abre en esa pestaña', (
      tester,
    ) async {
      SharedPreferences.setMockInitialValues({'character.ch1.view': 'combat'});
      final prefs = await SharedPreferences.getInstance();
      await _pump(tester, characters: _repo(), prefs: prefs);
      expect(find.byKey(const Key('combat-view')), findsOneWidget);
    });
  });

  group('puntos de golpe', () {
    testWidgets('PG - aplica el daño con el endpoint de daño y reduce el valor mostrado', (
      tester,
    ) async {
      final repo = _repo(temp: 0);
      await _pump(tester, characters: repo);
      expect(find.text('20 / 28'), findsOneWidget);

      await _tap(tester, 'hp-minus');
      expect(repo.damageCalls, [1]);
      expect(repo.combatPatches, isEmpty);
      expect(find.text('19 / 28'), findsOneWidget);
      expect(find.text('20 / 28'), findsNothing);
    });

    testWidgets('el daño consume antes los PG temporales; curar no pasa del máximo', (
      tester,
    ) async {
      final repo = _repo(temp: 3);
      await _pump(tester, characters: repo);
      await tester.enterText(find.byKey(const Key('hp-amount')), '5');
      await _tap(tester, 'hp-minus');
      // The server takes the temporary hit points first.
      expect(repo.damageCalls, [5]);
      expect(find.text('18 / 28'), findsOneWidget);
      expect(find.text('PG temp.: 0'), findsOneWidget);

      await tester.enterText(find.byKey(const Key('hp-amount')), '50');
      await _tap(tester, 'hp-plus');
      expect(repo.combatPatches.last.hitPointsCurrent, 28);
      expect(find.text('28 / 28'), findsOneWidget);
    });

    testWidgets('un fallo de red muestra "Sin conexión" y no cambia nada', (tester) async {
      final repo = _repo(temp: 0);
      await _pump(tester, characters: repo);
      repo.error = dioError(null);
      await _tap(tester, 'hp-minus');
      expect(find.textContaining('Sin conexión'), findsOneWidget);
      expect(find.text('20 / 28'), findsOneWidget);
    });

    testWidgets('PG temporales, inspiración y concentración', (tester) async {
      final json = makeCharacterJson(status: 'Active', combat: makeCombatJson())
        ..['concentratingOnSpellIndex'] = 'bless';
      final withConcentration = FakeCharactersRepository(characters: [json]);
      await _pump(tester, characters: withConcentration);

      await _tap(tester, 'temp-hp');
      await tester.enterText(find.byKey(const Key('number-field')), '7');
      await _tap(tester, 'number-confirm');
      expect(withConcentration.combatPatches.last.temporaryHitPoints, 7);

      // La fixture arranca con inspiración: se quita.
      await _tap(tester, 'inspiration');
      expect(withConcentration.combatPatches.last.inspiration, isFalse);

      expect(find.text('Concentración: Bless'), findsOneWidget);
      // The chip lives with the other states, not under the stats grid.
      expect(
        find.descendant(
          of: find.byType(ConditionsCard),
          matching: find.byKey(const Key('concentration-chip')),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byType(StatsCard),
          matching: find.byKey(const Key('concentration-chip')),
        ),
        findsNothing,
      );
      await _tap(tester, 'concentration-lose');
      expect(withConcentration.concentrationCalls, [null]);
      expect(find.byKey(const Key('concentration-chip')), findsNothing);
    });

    FakeCharactersRepository hpMaxRepo({bool overridden = false, bool isDm = true}) =>
        FakeCharactersRepository(
          isDm: isDm,
          characters: [
            makeCharacterJson(
              status: 'Active',
              combat: makeCombatJson(),
              overrides: [
                {'field': 'armorClass', 'value': 18, 'note': 'Escudo de la familia'},
                if (overridden) {'field': 'hitPointsMax', 'value': 40, 'note': 'Bendición'},
              ],
              overriddenFields: ['armorClass', if (overridden) 'hitPointsMax'],
            ),
          ],
        );

    testWidgets('tocar "/ máx" guarda el override hitPointsMax con los demás', (tester) async {
      final repo = hpMaxRepo();
      await _pump(tester, characters: repo, role: CampaignRole.dm);
      expect(find.byKey(const Key('override-hitPointsMax')), findsNothing);

      await _tap(tester, 'hp-max-edit');
      expect(find.text('PG máximos'), findsWidgets);
      expect(find.byKey(const Key('hp-max-reset')), findsNothing);
      await tester.enterText(find.byKey(const Key('hp-max-field')), '35');
      await _tap(tester, 'hp-max-save');

      final overrides = repo.patches.single.overrides!;
      expect(
        [for (final o in overrides) (o.field, o.value)],
        [('armorClass', 18), ('hitPointsMax', 35)],
      );
      expect(overrides.first.note, 'Escudo de la familia');
      expect(repo.patches.single.toJson().keys, ['overrides']);
      expect(find.text('PG máximos actualizados.'), findsOneWidget);
    });

    testWidgets('"Volver al cálculo" quita el override y la marca se ve', (tester) async {
      final repo = hpMaxRepo(overridden: true);
      await _pump(tester, characters: repo, role: CampaignRole.dm);
      expect(find.byKey(const Key('override-hitPointsMax')), findsOneWidget);

      await _tap(tester, 'hp-max-edit');
      await _tap(tester, 'hp-max-reset');
      final overrides = repo.patches.single.overrides!;
      expect([for (final o in overrides) o.field], ['armorClass']);
      expect(find.text('PG máximos: vuelven al cálculo.'), findsOneWidget);
    });

    testWidgets('el jugador de un personaje activo lo envía al DM; valores inválidos no', (
      tester,
    ) async {
      final repo = hpMaxRepo(isDm: false);
      await _pump(tester, characters: repo, role: CampaignRole.player);
      await _tap(tester, 'hp-max-edit');
      await tester.enterText(find.byKey(const Key('hp-max-field')), '0');
      await _tap(tester, 'hp-max-save');
      expect(find.text('Introduce un número entre 1 y 999.'), findsOneWidget);
      expect(repo.patches, isEmpty);

      await tester.enterText(find.byKey(const Key('hp-max-field')), '30');
      await _tap(tester, 'hp-max-save');
      expect(repo.patches.single.overrides!.last.value, 30);
      expect(find.text('Enviado al DM para aprobación'), findsOneWidget);
    });

    testWidgets('sin permiso de escritura "/ máx" no abre nada', (tester) async {
      final repo = _repo(ownerUserId: 'p2');
      await _pump(tester, characters: repo, role: CampaignRole.player);
      await _tap(tester, 'hp-max-edit');
      expect(find.byKey(const Key('hp-max-field')), findsNothing);
    });

    testWidgets('a 0 PG un banner pide anotar la salvación y desaparece al estabilizarse o curar', (
      tester,
    ) async {
      final alive = _repo();
      await _pump(tester, characters: alive);
      expect(find.byKey(const Key('death-save-banner')), findsNothing);

      await tester.pumpWidget(const SizedBox());
      final down = _repo(hp: 0, temp: 0);
      await _pump(tester, characters: down);
      expect(find.byKey(const Key('death-save-banner')), findsOneWidget);
      expect(find.text('Anota tu salvación contra muerte'), findsOneWidget);
      // El banner queda encima de la tarjeta de salvaciones.
      expect(
        tester.getBottomLeft(find.byKey(const Key('death-save-banner'))).dy,
        lessThanOrEqualTo(tester.getTopLeft(find.byKey(const Key('death-saves'))).dy),
      );
      await _tap(tester, 'death-success-0');
      await _tap(tester, 'death-success-1');
      await _tap(tester, 'death-success-2');
      expect(find.byKey(const Key('death-save-banner')), findsNothing);
      expect(find.byKey(const Key('death-saves')), findsOneWidget);
    });

    testWidgets('las salvaciones de muerte solo aparecen con 0 PG y se marcan al tocar', (
      tester,
    ) async {
      final alive = _repo();
      await _pump(tester, characters: alive);
      expect(find.byKey(const Key('death-saves')), findsNothing);

      await tester.pumpWidget(const SizedBox());
      final down = _repo(hp: 0, temp: 0);
      await _pump(tester, characters: down);
      expect(find.byKey(const Key('death-saves')), findsOneWidget);
      await _tap(tester, 'death-failure-0');
      expect(down.combatPatches.last.deathSaveFailures, 1);
      await _tap(tester, 'death-success-1');
      expect(down.combatPatches.last.deathSaveSuccesses, 2);

      // Curar desde 0 reinicia las salvaciones.
      await _tap(tester, 'hp-plus');
      expect(down.combatPatches.last.hitPointsCurrent, 1);
      expect(down.combatPatches.last.deathSaveSuccesses, 0);
      expect(down.combatPatches.last.deathSaveFailures, 0);
      expect(find.byKey(const Key('death-saves')), findsNothing);
    });
  });

  group('condiciones', () {
    final catalog = FakeCatalogRepository(
      conditionList: const [
        Condition(index: 'poisoned', name: 'Poisoned'),
        Condition(index: 'prone', name: 'Prone'),
        Condition(index: 'exhaustion', name: 'Exhaustion'),
      ],
    );

    testWidgets('se añade desde el catálogo y se quita', (tester) async {
      final repo = _repo();
      await _pump(tester, characters: repo, catalog: catalog);
      expect(find.text('Sin condiciones.'), findsOneWidget);

      await _tap(tester, 'condition-add');
      await _tap(tester, 'pick-condition-poisoned');
      expect(repo.combatPatches.last.conditions?.map((c) => c.index), ['poisoned']);
      expect(find.byKey(const Key('condition-poisoned')), findsOneWidget);
      expect(find.text('Poisoned'), findsOneWidget);

      // Las ya puestas no se ofrecen otra vez.
      await _tap(tester, 'condition-add');
      expect(find.byKey(const Key('pick-condition-poisoned')), findsNothing);
      expect(find.byKey(const Key('pick-condition-prone')), findsOneWidget);
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();

      await tester.tap(
        find.descendant(
          of: find.byKey(const Key('condition-poisoned')),
          matching: find.byTooltip('Quitar'),
        ),
      );
      await tester.pumpAndSettle();
      expect(repo.combatPatches.last.conditions, isEmpty);
      expect(find.byKey(const Key('condition-poisoned')), findsNothing);
    });

    testWidgets(
      'con concentración y sin condiciones la tarjeta muestra el chip y "Perder" lo quita',
      (tester) async {
        final repo = FakeCharactersRepository(
          characters: [
            makeCharacterJson(
              status: 'Active',
              combat: makeCombatJson(),
              concentratingOnSpellIndex: 'bless',
            ),
          ],
        );
        await _pump(tester, characters: repo, catalog: catalog);

        Finder inConditions(String key) =>
            find.descendant(of: find.byType(ConditionsCard), matching: find.byKey(Key(key)));
        expect(inConditions('concentration-chip'), findsOneWidget);
        expect(inConditions('concentration-lose'), findsOneWidget);
        expect(find.text('Sin condiciones.'), findsNothing);

        await tester.ensureVisible(inConditions('concentration-lose'));
        await _tap(tester, inConditions('concentration-lose'));
        expect(repo.concentrationCalls, [null]);
        expect(find.byKey(const Key('concentration-chip')), findsNothing);
        expect(find.text('Sin condiciones.'), findsOneWidget);
      },
    );

    testWidgets('el agotamiento pide el nivel', (tester) async {
      final repo = _repo();
      await _pump(tester, characters: repo, catalog: catalog);
      await _tap(tester, 'condition-add');
      await _tap(tester, 'pick-condition-exhaustion');
      await _tap(tester, 'exhaustion-level-2');
      expect(repo.combatPatches.last.exhaustionLevel, 2);
      expect(repo.combatPatches.last.conditions, isNull);
      expect(find.text('Exhaustion 2'), findsOneWidget);

      await tester.tap(
        find.descendant(
          of: find.byKey(const Key('condition-exhaustion')),
          matching: find.byTooltip('Quitar'),
        ),
      );
      await tester.pumpAndSettle();
      expect(repo.combatPatches.last.exhaustionLevel, 0);
      expect(find.byKey(const Key('condition-exhaustion')), findsNothing);
    });
  });

  group('ataques y dados', () {
    test('CombatAttack.fromJson lee attackBreakdown y damageBreakdown', () {
      final attack = CombatAttack.fromJson({
        'name': 'Longsword',
        'attackBonus': 6,
        'damage': '1d8+4',
        'attackBreakdown': makeBreakdownJson([
          ('ability', 'Fuerza', 3),
          ('proficiency', 'Competencia', 2),
          ('item', 'Espada +1', 1),
        ]),
        'damageBreakdown': makeBreakdownJson([('ability', 'Fuerza', 3), ('item', 'Espada +1', 1)]),
      });
      expect(attack.attackBreakdown!.total, 6);
      expect(attack.attackBreakdown!.parts.last.source, 'item');
      expect(attack.damageBreakdown!.total, 4);
      expect(attack.damageBreakdown!.parts, hasLength(2));
      expect(CombatAttack.fromJson({'name': 'Dagger'}).attackBreakdown, isNull);
    });

    testWidgets('tocar el bono o el daño de un ataque abre su desglose', (tester) async {
      await _pump(
        tester,
        characters: _repo(
          combat: makeCombatJson(
            attacks: [
              {
                'name': 'Longsword',
                'attackBonus': 6,
                'damage': '1d8+4',
                'damageType': 'Slashing',
                'properties': <String>[],
                'attackBreakdown': makeBreakdownJson([
                  ('ability', 'Fuerza', 3),
                  ('proficiency', 'Competencia', 2),
                  ('item', 'Espada +1', 1),
                ]),
                'damageBreakdown': makeBreakdownJson([
                  ('ability', 'Fuerza', 3),
                  ('item', 'Espada +1', 1),
                ]),
              },
            ],
          ),
        ),
      );

      // The attack is affected by an item: gold dot on both values.
      expect(find.byKey(const Key('stat-mark-attack.0.bonus')), findsOneWidget);
      expect(find.byKey(const Key('stat-mark-attack.0.damage')), findsOneWidget);

      await _tap(tester, 'stat-attack.0.bonus');
      expect(find.byKey(const Key('breakdown-sheet')), findsOneWidget);
      final sheet = find.byKey(const Key('breakdown-sheet'));
      expect(find.descendant(of: sheet, matching: find.text('Competencia')), findsOneWidget);
      expect(find.descendant(of: sheet, matching: find.text('Espada +1')), findsOneWidget);
      expect(find.text('Ataque: Longsword'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '+6');
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      await _tap(tester, 'stat-attack.0.damage');
      expect(find.text('Daño: Longsword'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '+4');
    });

    testWidgets('muestra los ataques del DTO con su bono', (tester) async {
      await _pump(
        tester,
        characters: _repo(
          combat: makeCombatJson(
            attacks: [
              {
                'name': 'Longsword',
                'attackBonus': 5,
                'damage': '1d8+3',
                'damageType': 'Slashing',
                'versatileDamage': '1d10+3',
                'properties': ['Versatile'],
              },
              {
                'name': 'Unarmed Strike',
                'attackBonus': 3,
                'damage': '1+1',
                'damageType': 'Bludgeoning',
                'properties': <String>[],
              },
            ],
          ),
        ),
      );
      expect(find.text('Longsword'), findsOneWidget);
      expect(find.text('+5'), findsOneWidget);
      expect(find.text('1d8+3 Slashing'), findsOneWidget);
      expect(find.text('Dos manos (1d10+3)'), findsOneWidget);
      expect(find.text('Unarmed Strike'), findsOneWidget);
      expect(find.text('+3'), findsOneWidget);
      expect(find.text('Tirar ataque'), findsNWidgets(2));
    });

    testWidgets('"Tirar ataque" abre la hoja con el resultado y el desglose', (tester) async {
      await _pump(tester, characters: _repo(), face: 14);
      await _tap(tester, 'attack-roll-0');

      expect(find.byKey(const Key('dice-result')), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '19');
      expect(tester.widget<Text>(find.byKey(const Key('dice-breakdown'))).data, '[14] + 5');
      expect(find.text('Ataque: Longsword'), findsOneWidget);
      expect(find.byKey(const Key('dice-critical')), findsNothing);
    });

    testWidgets('un 20 natural resalta el crítico y duplica los dados del daño', (tester) async {
      await _pump(tester, characters: _repo(), face: 20);
      await _tap(tester, 'attack-roll-0');
      expect(find.byKey(const Key('dice-critical')), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '25');

      // Cerrar la hoja: la tarjeta queda marcada como crítico.
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();
      expect(tester.widget<FilterChip>(find.byKey(const Key('attack-crit-0'))).selected, isTrue);

      await _tap(tester, 'attack-damage-0');
      expect(find.text('2d8+3'), findsOneWidget);
      expect(find.text('Daño: Longsword (crítico)'), findsOneWidget);
      // 2 dados de 20 -> se acotan a la cara máxima (8) del d8: 8 + 8 + 3.
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '19');
    });

    testWidgets('pulsación larga elige ventaja o desventaja', (tester) async {
      await _pump(tester, characters: _repo(), face: 14);
      await tester.longPress(find.byKey(const Key('attack-roll-0')));
      await tester.pumpAndSettle();
      await _tap(tester, 'mode-advantage');
      expect(find.text('adv+5'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('dice-breakdown'))).data, '[14 (14)] + 5');
    });

    testWidgets('"Dos manos" usa el daño versátil', (tester) async {
      await _pump(tester, characters: _repo(), face: 4);
      await _tap(tester, 'attack-versatile-0');
      await _tap(tester, 'attack-damage-0');
      expect(find.text('1d10+3'), findsWidgets);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '7');
    });

    testWidgets('la iniciativa se tira con el modificador de la hoja', (tester) async {
      await _pump(tester, characters: _repo(), face: 10);
      await _tap(tester, 'roll-initiative');
      expect(find.text('Iniciativa'), findsWidgets);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '12');
    });

    testWidgets('un daño sin dados ("1+1") se tira como cantidad fija', (tester) async {
      await _pump(
        tester,
        characters: _repo(
          combat: makeCombatJson(
            attacks: [
              {
                'name': 'Unarmed Strike',
                'attackBonus': 3,
                'damage': '1+1',
                'damageType': 'Bludgeoning',
              },
            ],
          ),
        ),
      );
      await _tap(tester, 'attack-damage-0');
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '2');
    });
  });

  group('tiradas desde la hoja detallada', () {
    testWidgets('salvaciones, habilidades y pruebas de característica llevan su modificador', (
      tester,
    ) async {
      await _pump(tester, characters: _repo(), startInCombat: false, face: 14);

      await _tap(tester, 'save-str');
      expect(find.text('Salvación de Fuerza'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '19');
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      await _tap(tester, 'ability-dex');
      expect(find.text('Prueba de Destreza'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '16');
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      await openDetailTab(tester, 'tab-skills');
      await _tap(tester, 'skill-athletics');
      expect(find.text('Atletismo'), findsWidgets);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '19');
    });
  });

  group('hoja de dados', () {
    testWidgets('el botón flotante abre la hoja: dados, expresión, historial y favoritos', (
      tester,
    ) async {
      await _pump(tester, characters: _repo(), face: 3);
      await _tap(tester, 'dice-fab');
      for (final d in ['d4', 'd6', 'd8', 'd10', 'd12', 'd20', 'd100']) {
        expect(find.byKey(Key('die-$d')), findsOneWidget);
      }
      expect(find.text('Aún no has tirado ningún dado.'), findsOneWidget);

      await _tap(tester, 'die-d6');
      await _tap(tester, 'die-d6');
      await _tap(tester, 'die-d4');
      expect(
        tester.widget<TextField>(find.byKey(const Key('dice-expression'))).controller?.text,
        '2d6+1d4',
      );
      await tester.enterText(find.byKey(const Key('dice-expression')), '2d6+3');
      await _tap(tester, 'dice-roll');
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '9');
      expect(find.byKey(const Key('history-0')), findsOneWidget);

      await _tap(tester, 'dice-favorite-add');
      expect(find.byKey(const Key('favorite-2d6+3')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('dice-expression')), '2d');
      await _tap(tester, 'dice-roll');
      expect(find.textContaining('no es una parte válida'), findsOneWidget);

      await _tap(tester, 'dice-clear-history');
      expect(find.text('Aún no has tirado ningún dado.'), findsOneWidget);
    });

    testWidgets('la ventaja de la hoja convierte el 1d20 en adv', (tester) async {
      await _pump(tester, characters: _repo(), face: 3);
      await _tap(tester, 'dice-fab');
      await tester.enterText(find.byKey(const Key('dice-expression')), '1d20+2');
      await _tap(tester, find.text('Ventaja'));
      await _tap(tester, 'dice-roll');
      expect(find.text('adv+2'), findsWidgets);
    });
  });

  group('fichas de combate (fase 28)', () {
    const tiles = [
      'combat-ac',
      'combat-initiative',
      'combat-speed',
      'combat-perception',
      'combat-proficiency',
      'combat-inspiration',
    ];

    testWidgets('las seis fichas miden lo mismo a 400 y a 800 px; sin habilidades', (tester) async {
      await _pump(tester, characters: _repo());
      final grid = find.byKey(const Key('combat-stats-grid'));
      for (final key in tiles) {
        expect(
          find.descendant(of: grid, matching: find.byKey(Key(key))),
          findsOneWidget,
          reason: key,
        );
      }
      Rect rect(String key) => tester.getRect(find.byKey(Key(key)));
      void expectEqualTiles() {
        expect({for (final k in tiles) rect(k).width.round()}.length, 1);
        expect({for (final k in tiles) rect(k).height.round()}.length, 1);
      }

      // 800 px: one row of six.
      expectEqualTiles();
      expect({for (final k in tiles) rect(k).top}.length, 1);

      // 400 px: two rows of three, still equal.
      tester.view.physicalSize = const Size(400, 7000);
      await tester.pumpAndSettle();
      expectEqualTiles();
      expect({for (final k in tiles) rect(k).top}.length, 2);

      // The dice corner lives inside the initiative tile.
      expect(
        find.descendant(
          of: find.byKey(const Key('combat-initiative')),
          matching: find.byKey(const Key('roll-initiative')),
        ),
        findsOneWidget,
      );
      // Skills are no longer in Combate.
      expect(find.byKey(const Key('quick-skills')), findsNothing);
      expect(find.byKey(const Key('all-skills')), findsNothing);
      expect(tester.takeException(), isNull);
    });

    testWidgets('la inspiración se alterna tocando su ficha', (tester) async {
      final repo = _repo();
      await _pump(tester, characters: repo);
      Finder inTile(String text) => find.descendant(
        of: find.byKey(const Key('combat-inspiration')),
        matching: find.text(text),
      );
      expect(inTile('Sí'), findsOneWidget);
      await _tap(tester, 'inspiration');
      expect(repo.combatPatches.last.inspiration, isFalse);
    });

    testWidgets('la etiqueta "Pacto (niv. 5)" cabe sin desbordar', (tester) async {
      final repo = _repo(
        combat: makeCombatJson(spellSlots: const [], pactSlots: {'level': 5, 'max': 2, 'used': 0}),
      );
      await _pump(tester, characters: repo);
      tester.view.physicalSize = const Size(400, 7000);
      await tester.pumpAndSettle();
      expect(
        find.descendant(
          of: find.byKey(const Key('combat-pact-slots')),
          matching: find.text('Pacto (niv. 5)'),
        ),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);
    });

    testWidgets('el área de pulsación de los espacios abraza los puntos y los centra', (
      tester,
    ) async {
      final repo = _repo(
        combat: makeCombatJson(spellSlots: const [], pactSlots: {'level': 5, 'max': 2, 'used': 0}),
      );
      await _pump(tester, characters: repo);
      final pips = find.byKey(const Key('combat-pact-slots-pips'));
      final ink = find.descendant(of: pips, matching: find.byType(InkWell));
      final inkRect = tester.getRect(ink);
      final dots = tester.getRect(find.descendant(of: pips, matching: find.byType(Wrap)));
      // Two 24 px pips with 8 px between them, 8 px of padding on every side.
      expect(dots.width, 56);
      expect(inkRect.width, 72);
      expect(inkRect.height, 48);
      expect(dots.center, inkRect.center);
      // The pip row itself is the ink area: it does not stretch to the row.
      expect(tester.getRect(pips), inkRect);
      expect(
        inkRect.width,
        lessThan(tester.getRect(find.byKey(const Key('combat-pact-slots'))).width),
      );
    });
  });

  group('conjuros en combate', () {
    SpellDetail spell(Map<String, dynamic> json) => SpellDetail.fromJson(json);

    final fireBolt = spell({
      'index': 'fire-bolt',
      'name': 'Fire Bolt',
      'level': 0,
      'attackType': 'ranged',
      'damage': {
        'dice': '1d10',
        'type': 'Fire',
        'atCharacterLevel': {'1': '1d10', '5': '2d10', '11': '3d10', '17': '4d10'},
      },
    });
    final fireball = spell({
      'index': 'fireball',
      'name': 'Fireball',
      'level': 3,
      'dcAbility': 'dex',
      'damage': {
        'dice': '8d6',
        'type': 'Fire',
        'atSlotLevel': {for (var l = 3; l <= 9; l++) '$l': '${l + 5}d6'},
      },
    });
    final cureWounds = spell({
      'index': 'cure-wounds',
      'name': 'Cure Wounds',
      'level': 1,
      'healAtSlotLevel': {for (var l = 1; l <= 9; l++) '$l': '${l}d8 + MOD'},
    });
    final shield = spell({'index': 'shield', 'name': 'Shield', 'level': 1});
    final burningHands = spell({
      'index': 'burning-hands',
      'name': 'Burning Hands',
      'level': 1,
      'dcAbility': 'dex',
      'damage': {
        'atSlotLevel': {'1': '3d6', '2': '4d6'},
      },
    });

    FakeCharactersRepository caster() {
      final json = makeCharacterJson(
        status: 'Active',
        classes: [
          {'classIndex': 'wizard', 'className': 'Wizard', 'level': 4},
          {'classIndex': 'cleric', 'className': 'Cleric', 'level': 1},
        ],
        spells: [
          {'spellIndex': 'fire-bolt', 'classIndex': 'wizard'},
          {'spellIndex': 'fireball', 'classIndex': 'wizard', 'isPrepared': true},
          {'spellIndex': 'shield', 'classIndex': 'wizard', 'isPrepared': true},
          {'spellIndex': 'burning-hands', 'classIndex': 'wizard'},
          {'spellIndex': 'cure-wounds', 'classIndex': 'cleric', 'alwaysPrepared': true},
        ],
        combat: makeCombatJson(
          spellSlots: [
            {'level': 1, 'max': 4, 'used': 0},
            {'level': 3, 'max': 2, 'used': 0},
            {'level': 4, 'max': 1, 'used': 0},
          ],
        ),
      );
      (json['sheet'] as Map<String, dynamic>)['spellcasting'] = [
        {
          'classIndex': 'wizard',
          'ability': 'int',
          'saveDc': 15,
          'attackBonus': 7,
          'preparedMax': 6,
        },
        {
          'classIndex': 'cleric',
          'ability': 'wis',
          'saveDc': 11,
          'attackBonus': 3,
          'preparedMax': 2,
        },
      ];
      return FakeCharactersRepository(characters: [json]);
    }

    FakeCatalogRepository catalog() => FakeCatalogRepository(
      spellDetails: {
        for (final s in [fireBolt, fireball, cureWounds, shield, burningHands]) s.index: s,
      },
    );

    test('SpellDetail conserva el escalado de daño y de curación', () {
      expect(fireBolt.damageAtCharacterLevel[5], '2d10');
      expect(fireBolt.damageType, 'Fire');
      expect(fireball.damageAtSlotLevel[9], '14d6');
      expect(cureWounds.healAtSlotLevel[2], '2d8 + MOD');
      expect(shield.damageAtSlotLevel, isEmpty);
    });

    test('escalado por nivel de lanzamiento y de personaje', () {
      expect(spellDamageExpression(fireBolt, castLevel: 0, characterLevel: 4), '1d10');
      expect(spellDamageExpression(fireBolt, castLevel: 0, characterLevel: 11), '3d10');
      expect(spellDamageExpression(fireball, castLevel: 5, characterLevel: 5), '10d6');
      expect(
        spellDamageExpression(fireball, castLevel: 3, characterLevel: 5, critical: true),
        '16d6',
      );
      expect(spellHealExpression(cureWounds, castLevel: 3, modifier: 2), '3d8+2');
      expect(spellHealExpression(cureWounds, castLevel: 1, modifier: -1), '1d8-1');
      expect(spellHealExpression(cureWounds, castLevel: 1, modifier: 0), '1d8');
      expect(withModifier('5', 3), '5');
      expect(isCombatSpell(shield), isFalse);
      expect(
        castLevels(3, const [
          SpellSlot(level: 1, max: 2, used: 0),
          SpellSlot(level: 4, max: 1, used: 0),
        ], null),
        [4],
      );
      expect(castLevels(2, const [], const SpellSlot(level: 3, max: 2, used: 0)), [3]);
      expect(castLevels(2, const [], null), [2]);
    });

    testWidgets('lista trucos y conjuros preparados con ataque, salvación o curación', (
      tester,
    ) async {
      await _pump(tester, characters: caster(), catalog: catalog());
      expect(find.byKey(const Key('combat-spells')), findsOneWidget);
      expect(find.byKey(const Key('combat-spell-fire-bolt')), findsOneWidget);
      expect(find.byKey(const Key('combat-spell-fireball')), findsOneWidget);
      expect(find.byKey(const Key('combat-spell-cure-wounds')), findsOneWidget);
      // Fase 27: también los preparados sin números; nunca los no preparados
      // de una clase que prepara.
      expect(find.byKey(const Key('combat-spell-shield')), findsOneWidget);
      expect(find.byKey(const Key('combat-spell-burning-hands')), findsNothing);

      // The facts are now values with their breakdown: label and value texts.
      String facts(String index) => tester
          .widgetList<Text>(
            find.descendant(of: find.byKey(Key('spell-facts-$index')), matching: find.byType(Text)),
          )
          .map((t) => t.data)
          .join(' ');
      expect(facts('fire-bolt'), 'Ataque a distancia +7 Daño 2d10 fuego');
      expect(facts('fireball'), 'Salvación CD 15 (Destreza) Daño 8d6 fuego');
      expect(facts('cure-wounds'), 'Cura 1d8+1');
    });

    testWidgets('un 20 natural en el ataque marca crítico y duplica los dados', (tester) async {
      await _pump(tester, characters: caster(), catalog: catalog(), face: 20);
      await _tap(tester, 'spell-attack-fire-bolt');
      expect(find.text('1d20+7'), findsOneWidget);
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();
      expect(
        tester.widget<FilterChip>(find.byKey(const Key('spell-crit-fire-bolt'))).selected,
        isTrue,
      );
      await _tap(tester, 'spell-damage-fire-bolt');
      expect(find.text('4d10'), findsOneWidget);
      expect(find.text('Daño: Fire Bolt (crítico)'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '40');
    });

    testWidgets('el nivel de lanzamiento escala el daño y gasta ese espacio', (tester) async {
      final repo = caster();
      await _pump(tester, characters: repo, catalog: catalog(), face: 1);
      await _tap(tester, 'spell-level-fireball');
      await _tap(tester, find.text('4').last);
      await _tap(tester, 'spell-damage-fireball');
      expect(find.text('9d6'), findsOneWidget);
      expect(find.text('Daño: Fireball (nivel 4)'), findsOneWidget);
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      await _tap(tester, 'spell-spend-fireball');
      expect(repo.slotSpends, [(level: 4, amount: 1)]);
    });

    testWidgets('la curación suma el modificador de la característica de su clase', (tester) async {
      await _pump(tester, characters: caster(), catalog: catalog(), face: 6);
      await _tap(tester, 'spell-heal-cure-wounds');
      expect(find.text('Curación: Cure Wounds (nivel 1)'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '7');
      expect(find.byKey(const Key('spell-crit-cure-wounds')), findsNothing);
    });

    group('detalle, tipo de acción y desglose (fase 27)', () {
      final mageArmor = spell({
        'index': 'mage-armor',
        'name': 'Mage Armor',
        'level': 1,
        'castingTime': '1 action',
        'range': 'Touch',
        'duration': '8 hours',
        'description': [
          'You touch a willing creature who is not wearing armor.',
          'The spell ends if the target dons armor.',
        ],
      });
      final bless = spell({
        'index': 'bless',
        'name': 'Bless',
        'level': 1,
        'castingTime': '1 action',
        'concentration': true,
        'description': ['You bless up to three creatures of your choice within range.'],
      });
      final trueStrike = spell({
        'index': 'true-strike',
        'name': 'True Strike',
        'level': 0,
        'castingTime': '1 action',
        'concentration': true,
        'description': ['You point a finger at a target in range.'],
      });

      FakeCharactersRepository wizard() {
        final json = makeCharacterJson(
          status: 'Active',
          classes: [
            {'classIndex': 'wizard', 'className': 'Wizard', 'level': 5},
          ],
          spells: [
            {'spellIndex': 'fire-bolt', 'classIndex': 'wizard'},
            {'spellIndex': 'true-strike', 'classIndex': 'wizard'},
            {'spellIndex': 'fireball', 'classIndex': 'wizard', 'isPrepared': true},
            {'spellIndex': 'mage-armor', 'classIndex': 'wizard', 'isPrepared': true},
            {'spellIndex': 'bless', 'classIndex': 'wizard', 'isPrepared': true},
          ],
          combat: makeCombatJson(
            spellSlots: [
              {'level': 1, 'max': 4, 'used': 0},
              {'level': 3, 'max': 2, 'used': 0},
            ],
          ),
        );
        (json['sheet'] as Map<String, dynamic>)['spellcasting'] = [
          {
            'classIndex': 'wizard',
            'ability': 'int',
            'saveDc': 15,
            'attackBonus': 7,
            'preparedMax': 6,
          },
        ];
        return FakeCharactersRepository(characters: [json]);
      }

      FakeCatalogRepository wizardCatalog() => FakeCatalogRepository(
        spellDetails: {
          for (final s in [fireBolt, fireball, mageArmor, bless, trueStrike]) s.index: s,
        },
      );

      testWidgets('un conjuro preparado sin números se muestra con su acción y su texto', (
        tester,
      ) async {
        await _pump(tester, characters: wizard(), catalog: wizardCatalog());
        final card = find.byKey(const Key('combat-spell-mage-armor'));
        expect(card, findsOneWidget);
        expect(
          find.descendant(of: card, matching: find.byKey(const Key('action-kind-action'))),
          findsOneWidget,
        );
        expect(find.descendant(of: card, matching: find.text('Acción')), findsOneWidget);
        expect(
          find.descendant(
            of: card,
            matching: find.text('You touch a willing creature who is not wearing armor.'),
          ),
          findsOneWidget,
        );
        expect(find.textContaining('The spell ends if'), findsNothing);
        expect(find.byKey(const Key('spell-facts-mage-armor')), findsNothing);
        expect(find.byKey(const Key('spell-damage-mage-armor')), findsNothing);
        final cast = find.byKey(const Key('spell-spend-mage-armor'));
        expect(find.descendant(of: cast, matching: find.text('Lanzar')), findsOneWidget);
        // A cantrip without concentration has nothing to cast.
        expect(find.byKey(const Key('spell-spend-fire-bolt')), findsNothing);
        expect(find.byKey(const Key('spell-concentrate-fire-bolt')), findsNothing);
      });

      testWidgets('Lanzar un conjuro de concentración gasta el espacio y se concentra', (
        tester,
      ) async {
        final repo = wizard();
        await _pump(tester, characters: repo, catalog: wizardCatalog());
        await _tap(tester, 'spell-spend-bless');
        expect(repo.slotSpends, [(level: 1, amount: 1)]);
        expect(repo.concentrationCalls, ['bless']);
        expect((await repo.get('ch1')).concentratingOnSpellIndex, 'bless');
        expect(find.textContaining('Concentrándote en Bless'), findsOneWidget);
      });

      testWidgets('un truco de concentración solo se concentra', (tester) async {
        final repo = wizard();
        await _pump(tester, characters: repo, catalog: wizardCatalog());
        expect(find.byKey(const Key('spell-spend-true-strike')), findsNothing);
        await _tap(tester, 'spell-concentrate-true-strike');
        expect(repo.slotSpends, isEmpty);
        expect(repo.concentrationCalls, ['true-strike']);
      });

      testWidgets('el botón de detalle abre la ficha del conjuro', (tester) async {
        await _pump(tester, characters: wizard(), catalog: wizardCatalog());
        await _tap(tester, 'detail-spell-mage-armor');
        expect(find.byType(SpellDetailPage), findsOneWidget);
        expect(find.text('The spell ends if the target dons armor.'), findsOneWidget);
      });

      testWidgets('el desglose del daño explica la tabla y el crítico', (tester) async {
        await _pump(tester, characters: wizard(), catalog: wizardCatalog());
        await _tap(tester, 'spell-crit-fireball');
        await _tap(tester, 'stat-spell.fireball.damage');
        expect(find.byKey(const Key('breakdown-sheet')), findsOneWidget);
        expect(
          tester.widget<Text>(find.byKey(const Key('breakdown-line-0'))).data,
          'Tabla del conjuro a nivel 3: 8d6',
        );
        expect(
          tester.widget<Text>(find.byKey(const Key('breakdown-line-1'))).data,
          'Crítico: dados doblados (16d6)',
        );
        expect(find.text('Sin modificador de característica'), findsOneWidget);
        expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '16d6 fuego');
        await tester.tapAt(const Offset(10, 10));
        await tester.pumpAndSettle();

        // Cantrips go by character level.
        await _tap(tester, 'stat-spell.fire-bolt.damage');
        expect(
          tester.widget<Text>(find.byKey(const Key('breakdown-line-0'))).data,
          'Tabla del conjuro a nivel de personaje 5: 2d10',
        );
      });

      testWidgets('la curación desglosa el modificador de la característica', (tester) async {
        await _pump(tester, characters: caster(), catalog: catalog());
        await _tap(tester, 'stat-spell.cure-wounds.heal');
        expect(
          tester.widget<Text>(find.byKey(const Key('breakdown-line-0'))).data,
          'Tabla del conjuro a nivel 1: 1d8 + MOD',
        );
        expect(find.text('Modificador de Sabiduría'), findsOneWidget);
        expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '1d8+1');
      });

      testWidgets('el ataque muestra que es una acción', (tester) async {
        await _pump(tester, characters: _repo());
        expect(
          find.descendant(
            of: find.byKey(const Key('attack-0')),
            matching: find.byKey(const Key('action-kind-action')),
          ),
          findsOneWidget,
        );
      });
    });

    group('oleada de magia salvaje (fase 25, bloque 7)', () {
      const surge = RollTable(
        key: 'pack-caos-wild-magic-surge',
        name: 'Oleada de ejemplo',
        dice: 'd100',
        classIndex: 'sorcerer',
        subclassIndex: 'pack-caos',
        entries: [
          RollTableEntry(from: 1, to: 50, text: 'Efecto ficticio bajo.'),
          RollTableEntry(from: 51, to: 100, text: 'Efecto ficticio alto.'),
        ],
      );

      FakeCharactersRepository sorcerer({String subclass = 'pack-caos', int tidesUsed = 1}) {
        final json = makeCharacterJson(
          status: 'Active',
          classes: [
            {
              'classIndex': 'sorcerer',
              'className': 'Sorcerer',
              'subclassIndex': subclass,
              'level': 3,
            },
          ],
          spells: [
            {'spellIndex': 'fire-bolt', 'classIndex': 'sorcerer'},
            {'spellIndex': 'burning-hands', 'classIndex': 'sorcerer'},
          ],
          combat: makeCombatJson(
            spellSlots: [
              {'level': 1, 'max': 4, 'used': 0},
            ],
            resources: [
              {
                'id': 'tides',
                'key': 'pack-caos-tides-of-chaos',
                'name': 'Mareas de ejemplo',
                'max': 1,
                'used': tidesUsed,
                'recharge': 'LongRest',
                'isAuto': true,
              },
            ],
          ),
        );
        (json['sheet'] as Map<String, dynamic>)['spellcasting'] = [
          {'classIndex': 'sorcerer', 'ability': 'cha', 'saveDc': 13, 'attackBonus': 5},
        ];
        return FakeCharactersRepository(characters: [json]);
      }

      FakeCatalogRepository surgeCatalog() => FakeCatalogRepository(
        spellDetails: {
          for (final s in [fireBolt, burningHands]) s.index: s,
        },
        rollTableList: const [surge],
      );

      testWidgets('gastar un espacio ofrece tirar 1d20 y un 1 abre la tabla', (tester) async {
        final repo = sorcerer();
        await _pump(tester, characters: repo, catalog: surgeCatalog(), face: 1);
        expect(find.byKey(const Key('wild-magic-surge-prompt')), findsNothing);

        await _tap(tester, 'spell-spend-burning-hands');
        expect(repo.slotSpends, [(level: 1, amount: 1)]);
        expect(find.text('Oleada de magia salvaje: tira 1d20'), findsOneWidget);

        await _tap(tester, 'wild-magic-surge-d20');
        expect(find.text('Oleada de magia salvaje'), findsOneWidget);
        expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '1');
        await tester.tapAt(const Offset(10, 10));
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('roll-table-sheet')), findsOneWidget);
        expect(
          find.descendant(
            of: find.byKey(const Key('roll-table-sheet')),
            matching: find.text('Oleada de ejemplo'),
          ),
          findsOneWidget,
        );
      });

      testWidgets('un d20 distinto de 1 no abre la tabla; "Tirar oleada" sí', (tester) async {
        await _pump(tester, characters: sorcerer(), catalog: surgeCatalog(), face: 14);
        await _tap(tester, 'spell-spend-burning-hands');

        await _tap(tester, 'wild-magic-surge-d20');
        await tester.tapAt(const Offset(10, 10));
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('roll-table-sheet')), findsNothing);

        await _tap(tester, 'wild-magic-surge-table');
        expect(find.byKey(const Key('roll-table-sheet')), findsOneWidget);
      });

      testWidgets('el jugador recupera Mareas del caos junto al aviso', (tester) async {
        final repo = sorcerer();
        await _pump(tester, characters: repo, catalog: surgeCatalog());
        await _tap(tester, 'spell-spend-burning-hands');

        await _tap(tester, 'wild-magic-surge-tides');
        expect(repo.resourceRestores, [(id: 'tides', amount: 1)]);
        expect(
          tester.widget<TextButton>(find.byKey(const Key('wild-magic-surge-tides'))).onPressed,
          isNull,
        );

        await _tap(tester, 'wild-magic-surge-dismiss');
        expect(find.byKey(const Key('wild-magic-surge-prompt')), findsNothing);
      });

      testWidgets('sin la tabla de oleada no aparece el aviso', (tester) async {
        final repo = sorcerer(subclass: 'otra-subclase');
        await _pump(tester, characters: repo, catalog: surgeCatalog());
        await _tap(tester, 'spell-spend-burning-hands');
        expect(repo.slotSpends, [(level: 1, amount: 1)]);
        expect(find.byKey(const Key('wild-magic-surge-prompt')), findsNothing);
      });

      test('claves de la oleada y de Mareas del caos', () {
        expect(isWildMagicSurgeKey('wild-magic-surge'), isTrue);
        expect(isWildMagicSurgeKey('pack-wild-magic-surge'), isTrue);
        expect(isWildMagicSurgeKey('pack-surge'), isFalse);
        CharacterResource auto(String key) => CharacterResource(
          id: 'x',
          key: key,
          name: 'X',
          max: 1,
          used: 1,
          recharge: Recharge.longRest,
          isAuto: true,
        );
        expect(canRestoreResource(auto('pack-tides-of-chaos'), isDm: false), isTrue);
        expect(canRestoreResource(auto('ki'), isDm: false), isFalse);
      });
    });
  });

  group('espacios de conjuro', () {
    testWidgets('tocar un nivel gasta un espacio y mantener pulsado lo repone', (tester) async {
      final repo = _repo();
      await _pump(tester, characters: repo);
      Finder pips({required bool filled}) => find.descendant(
        of: find.byKey(const Key('combat-slot-1')),
        matching: find.byWidgetPredicate((w) => w is Pip && w.filled == filled),
      );
      expect(pips(filled: true), findsNWidgets(2));

      await _tap(tester, 'combat-slot-1-pips');
      expect(repo.slotSpends, [(level: 1, amount: 1)]);
      expect(pips(filled: true), findsNWidgets(1));
      expect(pips(filled: false), findsNWidgets(1));

      await tester.longPress(find.byKey(const Key('combat-slot-1-pips')));
      await tester.pumpAndSettle();
      expect(repo.slotRestores, [(level: 1, amount: 1)]);
      expect(pips(filled: true), findsNWidgets(2));
    });

    testWidgets('sin espacios disponibles avisa y no llama al servidor', (tester) async {
      final repo = _repo(
        combat: makeCombatJson(
          spellSlots: [
            {'level': 2, 'max': 1, 'used': 1},
          ],
        ),
      );
      await _pump(tester, characters: repo);
      await _tap(tester, 'combat-slot-2-pips');
      expect(repo.slotSpends, isEmpty);
      expect(find.text('No quedan espacios de nivel 2.'), findsOneWidget);
    });

    testWidgets('la magia de pacto va aparte y gasta el nivel 0', (tester) async {
      final repo = _repo(
        combat: makeCombatJson(spellSlots: const [], pactSlots: {'level': 3, 'max': 2, 'used': 0}),
      );
      await _pump(tester, characters: repo);
      expect(find.text('Pacto (niv. 3)'), findsOneWidget);
      await _tap(tester, 'combat-pact-slots-pips');
      expect(repo.slotSpends, [(level: 0, amount: 1)]);
    });
  });

  group('recursos y consumibles', () {
    testWidgets('puntos para pocos usos y barra de reserva para muchos', (tester) async {
      final repo = _repo(
        combat: makeCombatJson(
          resources: [
            {'id': 'r1', 'name': 'Second Wind', 'max': 1, 'used': 0, 'recharge': 'ShortRest'},
            {'id': 'r2', 'name': 'Lay on Hands', 'max': 25, 'used': 5, 'recharge': 'LongRest'},
          ],
          onceSinceLongRest: [
            {'key': 'relentless', 'name': 'Relentless Endurance', 'used': false},
          ],
        ),
      );
      await _pump(tester, characters: repo);
      expect(find.byKey(const Key('resource-r1-pips')), findsOneWidget);
      expect(find.byKey(const Key('resource-r2-pool')), findsOneWidget);
      expect(find.text('20 / 25'), findsOneWidget);
      expect(find.byKey(const Key('once-relentless')), findsOneWidget);

      await _tap(tester, 'resource-r1-pips');
      expect(repo.resourceSpends, [(id: 'r1', amount: 1)]);
      await _tap(tester, 'resource-r2-minus');
      expect(repo.resourceSpends.last, (id: 'r2', amount: 1));
      expect(find.text('19 / 25'), findsOneWidget);
      await _tap(tester, 'resource-r2-plus');
      expect(repo.resourceRestores.last, (id: 'r2', amount: 1));

      // Escribir los puntos que quedan gasta la diferencia.
      await _tap(tester, 'resource-r2-pool');
      await tester.enterText(find.byKey(const Key('number-field')), '10');
      await _tap(tester, 'number-confirm');
      expect(repo.resourceSpends.last, (id: 'r2', amount: 10));
    });

    testWidgets('las opciones con coste salen junto a su recurso con "Usar"', (tester) async {
      final repo = _repo(
        combat: makeCombatJson(
          resources: [
            {
              'id': 'r1',
              'key': 'ki',
              'name': 'Ki',
              'max': 4,
              'used': 1,
              'recharge': 'ShortRest',
              'isAuto': true,
              'options': [
                {
                  'index': 'golpe-sereno',
                  'name': 'Golpe sereno',
                  'resource': 'ki',
                  'resourceName': 'Ki',
                  'amount': 2,
                },
                {
                  'index': 'rafaga-ejemplo',
                  'name': 'Ráfaga de ejemplo',
                  'resource': 'ki',
                  'resourceName': 'Ki',
                  'amount': 4,
                },
              ],
            },
          ],
        ),
      );
      await _pump(tester, characters: repo);
      expect(find.byKey(const Key('resource-r1-option-golpe-sereno')), findsOneWidget);
      expect(find.text('Golpe sereno'), findsOneWidget);
      expect(find.text('2 Ki'), findsOneWidget);

      // 3 uses left: 4 are not enough.
      final tooDear = tester.widget<ButtonStyleButton>(
        find.byKey(const Key('resource-r1-use-rafaga-ejemplo')),
      );
      expect(tooDear.onPressed, isNull);

      await _tap(tester, 'resource-r1-use-golpe-sereno');
      expect(repo.resourceSpends, [(id: 'r1', amount: 2)]);
      final now = tester.widget<ButtonStyleButton>(
        find.byKey(const Key('resource-r1-use-golpe-sereno')),
      );
      expect(now.onPressed, isNull);
    });

    testWidgets('el jugador no recupera a mano recursos automáticos; el DM sí', (tester) async {
      final resources = [
        {
          'id': 'r1',
          'key': 'ki',
          'name': 'Ki',
          'max': 4,
          'used': 2,
          'recharge': 'ShortRest',
          'isAuto': true,
        },
        {
          'id': 'r2',
          'key': 'lay-on-hands',
          'name': 'Lay on Hands',
          'max': 25,
          'used': 5,
          'recharge': 'LongRest',
          'isAuto': true,
        },
        {
          'id': 'r3',
          'key': 'sorcery-points',
          'name': 'Sorcery Points',
          'max': 4,
          'used': 2,
          'recharge': 'LongRest',
          'isAuto': true,
        },
        {'id': 'r4', 'name': 'Varita', 'max': 7, 'used': 3, 'recharge': 'Dawn'},
      ];
      final repo = _repo(combat: makeCombatJson(resources: resources));
      await _pump(tester, characters: repo);

      await tester.longPress(find.byKey(const Key('resource-r1-pips')));
      await tester.pumpAndSettle();
      final plus = tester.widget<IconButton>(find.byKey(const Key('resource-r2-plus')));
      expect(plus.onPressed, isNull);
      expect(repo.resourceRestores, isEmpty);
      // Los puntos de hechicería y los recursos manuales sí se recuperan.
      await tester.longPress(find.byKey(const Key('resource-r3-pips')));
      await tester.pumpAndSettle();
      await tester.longPress(find.byKey(const Key('resource-r4-pips')));
      await tester.pumpAndSettle();
      expect(repo.resourceRestores, [(id: 'r3', amount: 1), (id: 'r4', amount: 1)]);
    });

    testWidgets('el DM recupera recursos automáticos', (tester) async {
      final repo = _repo(
        combat: makeCombatJson(
          resources: [
            {
              'id': 'r1',
              'key': 'ki',
              'name': 'Ki',
              'max': 4,
              'used': 2,
              'recharge': 'ShortRest',
              'isAuto': true,
            },
          ],
        ),
      );
      await _pump(tester, characters: repo, role: CampaignRole.dm);
      await tester.longPress(find.byKey(const Key('resource-r1-pips')));
      await tester.pumpAndSettle();
      expect(repo.resourceRestores, [(id: 'r1', amount: 1)]);
    });

    testWidgets('un recurso con dado ofrece "Tirar", que gasta un uso y tira', (tester) async {
      final repo = _repo(
        combat: makeCombatJson(
          resources: [
            {
              'id': 'r1',
              'key': 'tacticos-ejemplo-dados',
              'name': 'Dados de táctica',
              'max': 4,
              'used': 1,
              'recharge': 'ShortRest',
              'isAuto': true,
              'dice': 'd8',
              'source': 'Ventaja táctica (nivel 3)',
              'breakdown': {
                'total': 4,
                'parts': [
                  {
                    'source': 'feature',
                    'label': 'Ventaja táctica (nivel 3), tabla desde el nivel 3',
                    'value': 4,
                  },
                ],
              },
            },
            {
              'id': 'r2',
              'name': 'Agotado',
              'max': 2,
              'used': 2,
              'recharge': 'LongRest',
              'dice': 'd6',
            },
            {'id': 'r3', 'name': 'Sin dado', 'max': 2, 'used': 0, 'recharge': 'LongRest'},
          ],
        ),
      );
      await _pump(tester, characters: repo, face: 5);

      expect(find.text('Tirar 1d8'), findsOneWidget);
      expect(find.byKey(const Key('resource-r3-roll')), findsNothing);
      final exhausted = tester.widget<ButtonStyleButton>(find.byKey(const Key('resource-r2-roll')));
      expect(exhausted.onPressed, isNull);
      expect(find.text(' · Ventaja táctica (nivel 3)'), findsOneWidget);

      await _tap(tester, 'resource-r1-roll');
      expect(repo.resourceSpends, [(id: 'r1', amount: 1)]);
      expect(find.byKey(const Key('dice-result')), findsOneWidget);
      expect(find.text('Dados de táctica'), findsWidgets);
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '5');
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      // El máximo se explica con un toque.
      await _tap(tester, 'stat-resource-r1-max');
      expect(find.text('Ventaja táctica (nivel 3), tabla desde el nivel 3'), findsOneWidget);
    });

    testWidgets('"Usar" gasta el consumible del inventario', (tester) async {
      final inventory = FakeInventoryRepository(
        items: {
          'ch1': [makeCharacterItem(id: 'p1', quantity: 2)],
        },
      );
      await _pump(
        tester,
        characters: _repo(
          combat: makeCombatJson(
            quickConsumables: [
              {'itemId': 'p1', 'name': 'Potion of Healing', 'quantity': 2},
            ],
          ),
        ),
        inventory: inventory,
      );
      expect(find.text('Potion of Healing'), findsOneWidget);
      expect(find.text('Cantidad: 2'), findsOneWidget);
      await _tap(tester, 'consumable-use-p1');
      expect(inventory.used, ['p1']);
      expect(find.text('Usado: Potion of Healing.'), findsOneWidget);
    });
  });

  group('descansos', () {
    testWidgets('el DM descansa directo; el jugador pide el descanso al DM', (tester) async {
      await _pump(tester, characters: _repo(), role: CampaignRole.dm);
      expect(find.byKey(const Key('rest-short')), findsOneWidget);
      expect(find.byKey(const Key('rest-request-short')), findsNothing);
    });

    testWidgets('el jugador no tiene botones de descanso directo', (tester) async {
      await _pump(tester, characters: _repo(), role: CampaignRole.player);
      expect(find.byKey(const Key('rest-short')), findsNothing);
      expect(find.byKey(const Key('rest-long')), findsNothing);
      expect(find.byKey(const Key('rest-request-short')), findsOneWidget);
      expect(find.byKey(const Key('rest-request-long')), findsOneWidget);
    });

    testWidgets('el corto pide dados de golpe por clase', (tester) async {
      final repo = _repo();
      await _pump(tester, characters: repo, role: CampaignRole.dm);
      await _tap(tester, 'rest-short');
      expect(find.text('Fighter (d10)\nQuedan 3 de 3'), findsOneWidget);
      await _tap(tester, 'hit-dice-fighter-plus');
      await _tap(tester, 'hit-dice-fighter-plus');
      await _tap(tester, 'rest-short-confirm');
      expect(repo.shortRests, [
        {'fighter': 2},
      ]);
      expect(find.text('Descanso corto realizado.'), findsOneWidget);
    });

    testWidgets('el largo pide confirmación', (tester) async {
      final repo = _repo();
      await _pump(tester, characters: repo, role: CampaignRole.dm);
      await _tap(tester, 'rest-long');
      expect(repo.longRests, 0);
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
      expect(repo.longRests, 0);

      await _tap(tester, 'rest-long');
      await _tap(tester, 'confirm-action');
      expect(repo.longRests, 1);
    });
  });

  group('paneles de clase', () {
    testWidgets('el panel de paladín muestra los dados del slot elegido', (tester) async {
      await _pump(
        tester,
        characters: _repo(
          classes: _paladinClasses,
          combat: makeCombatJson(classPanels: [_paladinPanel()]),
        ),
      );
      expect(find.byKey(const Key('class-panel-paladin')), findsOneWidget);
      expect(find.text('Dados extra: 2d8'), findsOneWidget);
      await _tap(tester, 'smite-level-2');
      expect(find.text('Dados extra: 3d8'), findsOneWidget);
      expect(find.text('Dados extra: 2d8'), findsNothing);
      expect(find.text('Aura de protección: 10 pies'), findsOneWidget);
    });

    testWidgets('Castigo divino llama a la API y muestra el resultado', (tester) async {
      final repo = _repo(
        classes: _paladinClasses,
        combat: makeCombatJson(classPanels: [_paladinPanel()]),
      );
      repo.smiteDice = '3d8';
      await _pump(tester, characters: repo, face: 5);
      await _tap(tester, 'smite-level-2');
      await _tap(tester, 'smite-confirm');
      expect(repo.smites, [2]);
      expect(find.text('Daño radiante adicional: 3d8'), findsOneWidget);

      await _tap(tester, 'smite-roll');
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '15');
      expect(find.text('Castigo divino'), findsWidgets);
    });

    test('Castigo divino: +1d8 contra no muertos hasta 6d8 y el crítico duplica los dados', () {
      expect(smiteDamage('2d8'), '2d8');
      expect(smiteDamage('2d8', againstUndead: true), '3d8');
      expect(smiteDamage('5d8', againstUndead: true), '6d8');
      expect(smiteDamage('6d8', againstUndead: true), '6d8');
      expect(smiteDamage('3d8', critical: true), '6d8');
      expect(smiteDamage('5d8', againstUndead: true, critical: true), '12d8');
      expect(criticalDamage('1d6+3', critical: true), '2d6+3');
      expect(criticalDamage('1d6+3', critical: false), '1d6+3');
    });

    testWidgets('Castigo divino: las casillas Crítico y no muerto cambian la tirada', (
      tester,
    ) async {
      final repo = _repo(
        classes: _paladinClasses,
        combat: makeCombatJson(classPanels: [_paladinPanel()]),
      );
      repo.smiteDice = '3d8';
      await _pump(tester, characters: repo, face: 5);
      await _tap(tester, 'smite-level-2');
      await _tap(tester, 'smite-confirm');
      await _tap(tester, 'smite-undead');
      expect(find.text('Daño radiante adicional: 4d8'), findsOneWidget);
      await _tap(tester, 'smite-critical');
      expect(find.text('Daño radiante adicional: 8d8'), findsOneWidget);

      await _tap(tester, 'smite-roll');
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '40');
      expect(find.text('Castigo divino (crítico)'), findsOneWidget);
    });

    testWidgets('Imposición de manos: "Curarme" usa el deslizador', (tester) async {
      final repo = _repo(
        classes: _paladinClasses,
        combat: makeCombatJson(classPanels: [_paladinPanel(loh: 25, lohUsed: 5)]),
      );
      await _pump(tester, characters: repo);
      expect(find.text('20 / 25'), findsWidgets);
      expect(find.byKey(const Key('loh-self')), findsNothing);
      expect(find.byKey(const Key('loh-apply')), findsNothing);

      final slider = find.byKey(const Key('loh-slider'));
      await tester.drag(slider, const Offset(100, 0));
      await tester.pumpAndSettle();
      final amount = int.parse(tester.widget<Text>(find.byKey(const Key('loh-amount'))).data!);
      expect(amount, greaterThan(1));

      expect(find.text('Curarme'), findsOneWidget);
      expect(find.text('Curar a otro'), findsOneWidget);
      await _tap(tester, 'loh-heal-self');
      expect(repo.classActions.last.action, 'lay-on-hands');
      expect(repo.classActions.last.body, {'amount': amount, 'targetSelf': true});
    });

    testWidgets('Imposición de manos: "Curar a otro" elige un personaje de la campaña', (
      tester,
    ) async {
      final repo = FakeCharactersRepository(
        characters: [
          makeCharacterJson(
            status: 'Active',
            classes: _paladinClasses,
            combat: makeCombatJson(classPanels: [_paladinPanel(loh: 25)]),
          ),
          makeCharacterJson(id: 'ch2', name: 'Elara', status: 'Active', ownerUserId: 'u2'),
        ],
      );
      await _pump(tester, characters: repo);

      await _tap(tester, 'loh-heal-other');
      expect(find.byKey(const Key('loh-other-sheet')), findsOneWidget);
      expect(find.byKey(const Key('loh-target-ch2')), findsOneWidget);
      // The paladin does not list itself.
      expect(find.byKey(const Key('loh-target-ch1')), findsNothing);
      expect(find.byKey(const Key('loh-target-free')), findsOneWidget);

      await _tap(tester, 'loh-target-ch2');
      expect(find.byKey(const Key('loh-other-sheet')), findsNothing);
      expect(repo.classActions.last.action, 'lay-on-hands');
      expect(repo.classActions.last.body, {
        'amount': 1,
        'targetSelf': false,
        'note': 'Curar a Elara',
      });
      expect(find.text('Has curado 1 PG a Elara.'), findsOneWidget);
    });

    testWidgets('Imposición de manos: "Otra criatura" acepta un nombre libre', (tester) async {
      final repo = _repo(
        classes: _paladinClasses,
        combat: makeCombatJson(classPanels: [_paladinPanel(loh: 25)]),
      );
      await _pump(tester, characters: repo);

      await _tap(tester, 'loh-heal-other');
      final confirm = find.byKey(const Key('loh-target-free-confirm'));
      expect(tester.widget<FilledButton>(confirm).onPressed, isNull);
      await tester.enterText(find.byKey(const Key('loh-target-free')), 'Caballo de guerra');
      await tester.pumpAndSettle();
      await _tap(tester, confirm);

      expect(repo.classActions.last.body['targetSelf'], isFalse);
      expect(repo.classActions.last.body['note'], 'Curar a Caballo de guerra');
      expect(find.text('Has curado 1 PG a Caballo de guerra.'), findsOneWidget);
    });

    testWidgets('Imposición de manos: sin reserva los dos botones se desactivan', (tester) async {
      final repo = _repo(
        classes: _paladinClasses,
        combat: makeCombatJson(classPanels: [_paladinPanel(loh: 5, lohUsed: 5)]),
      );
      await _pump(tester, characters: repo);
      for (final key in ['loh-heal-self', 'loh-heal-other']) {
        expect(tester.widget<ButtonStyleButton>(find.byKey(Key(key))).onPressed, isNull);
      }
    });

    testWidgets('Canalizar divinidad gasta el recurso', (tester) async {
      final repo = _repo(
        classes: _paladinClasses,
        combat: makeCombatJson(
          resources: [
            {
              'id': 'cd1',
              'key': 'channel-divinity',
              'name': 'Channel Divinity',
              'max': 1,
              'used': 0,
              'recharge': 'ShortRest',
            },
          ],
          classPanels: [_paladinPanel()],
        ),
      );
      await _pump(tester, characters: repo);
      await _tap(tester, 'channel-divinity');
      expect(repo.resourceSpends, [(id: 'cd1', amount: 1)]);
    });

    testWidgets('Furia gasta un uso, activa el contador y suma el bono a los cuerpo a cuerpo', (
      tester,
    ) async {
      final repo = _repo(
        classes: const [
          {'classIndex': 'barbarian', 'className': 'Barbarian', 'level': 3},
        ],
        combat: makeCombatJson(
          attacks: [
            {'name': 'Greataxe', 'attackBonus': 5, 'damage': '1d12+3', 'damageType': 'Slashing'},
            {
              'name': 'Longbow',
              'attackBonus': 3,
              'damage': '1d8+1',
              'damageType': 'Piercing',
              'range': '150/600',
              'properties': ['Ammunition'],
            },
          ],
          classPanels: [
            {
              'classIndex': 'barbarian',
              'level': 3,
              'data': {
                'rageDamageBonus': 2,
                'rageUses': {'max': 3, 'used': 0},
                'recklessAttack': true,
                'brutalCriticalDice': 0,
              },
            },
          ],
        ),
      );
      await _pump(tester, characters: repo, face: 4);
      expect(find.byKey(const Key('rage-active')), findsNothing);
      expect(find.text('Usos: 3 / 3'), findsOneWidget);
      expect(find.byKey(const Key('reckless-attack')), findsOneWidget);

      await _tap(tester, 'rage-start');
      expect(repo.classActions.single.action, 'rage');
      expect(find.byKey(const Key('rage-active')), findsOneWidget);
      expect(find.text('Asaltos restantes: 10'), findsOneWidget);
      expect(find.byKey(const Key('attack-rage-0')), findsOneWidget);
      expect(find.byKey(const Key('attack-rage-1')), findsNothing);

      await _tap(tester, 'attack-damage-0');
      expect(find.text('1d12+3+2'), findsOneWidget);
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      await _tap(tester, 'rage-next-round');
      expect(find.text('Asaltos restantes: 9'), findsOneWidget);
      await _tap(tester, 'rage-end');
      expect(find.byKey(const Key('rage-active')), findsNothing);
      expect(find.byKey(const Key('attack-rage-0')), findsNothing);
    });

    testWidgets('el mago ve su libro de hechizos y usa Recuperación arcana', (tester) async {
      final repo = _repo(
        classes: const [
          {'classIndex': 'wizard', 'className': 'Wizard', 'level': 4},
        ],
        combat: makeCombatJson(
          spellSlots: [
            {'level': 1, 'max': 4, 'used': 3},
            {'level': 2, 'max': 3, 'used': 1},
          ],
          classPanels: [
            {
              'classIndex': 'wizard',
              'level': 4,
              'data': {
                'spellbook': ['magic-missile', 'shield'],
                'prepared': ['magic-missile'],
                'preparedMax': 5,
                'arcaneRecovery': {'used': false, 'slotLevelsRecoverable': 2},
              },
            },
          ],
        ),
      );
      await _pump(
        tester,
        characters: repo,
        catalog: FakeCatalogRepository(
          spellDetails: {
            'magic-missile': const SpellDetail(
              index: 'magic-missile',
              name: 'Magic Missile',
              level: 1,
            ),
          },
        ),
      );
      expect(find.text('Preparados: 1 / 5'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('wizard-spellbook')));
      expect(find.text('Magic Missile'), findsOneWidget);
      expect(find.text('Shield'), findsOneWidget);

      await _tap(tester, 'arcane-recovery');
      expect(find.text('Niveles seleccionados: 0 / 2'), findsOneWidget);
      expect(
        tester.widget<FilledButton>(find.byKey(const Key('arcane-confirm'))).onPressed,
        isNull,
      );
      await _tap(tester, 'arcane-level-2-plus');
      expect(find.text('Niveles seleccionados: 2 / 2'), findsOneWidget);
      // Sin presupuesto: no se puede añadir un espacio más.
      expect(
        tester.widget<IconButton>(find.byKey(const Key('arcane-level-1-plus'))).onPressed,
        isNull,
      );
      await _tap(tester, 'arcane-confirm');
      expect(repo.classActions.single.action, 'arcane-recovery');
      expect(repo.classActions.single.body, {
        'slotLevels': [2],
      });
    });

    testWidgets('Recuperación arcana usada queda deshabilitada', (tester) async {
      await _pump(
        tester,
        characters: _repo(
          classes: const [
            {'classIndex': 'wizard', 'className': 'Wizard', 'level': 4},
          ],
          combat: makeCombatJson(
            classPanels: [
              {
                'classIndex': 'wizard',
                'level': 4,
                'data': {
                  'spellbook': <String>[],
                  'arcaneRecovery': {'used': true, 'slotLevelsRecoverable': 2},
                },
              },
            ],
          ),
        ),
      );
      expect(find.text('Recuperación arcana (usada)'), findsOneWidget);
      expect(
        tester.widget<ButtonStyleButton>(find.byKey(const Key('arcane-recovery'))).onPressed,
        isNull,
      );
    });

    testWidgets('una clase sin panel propio usa el genérico y se puede registrar otro', (
      tester,
    ) async {
      final rogue = classPanelBuilders['rogue'];
      classPanelBuilders['rogue'] = (context, panel) =>
          Text('Panel de pícaro ${panel.panel.level}');
      addTearDown(() => classPanelBuilders['rogue'] = rogue!);
      await _pump(
        tester,
        characters: _repo(
          classes: const [
            {'classIndex': 'artificer', 'className': 'Artificer', 'level': 3},
          ],
          combat: makeCombatJson(
            classPanels: [
              {'classIndex': 'rogue', 'level': 3, 'data': <String, dynamic>{}},
              {
                'classIndex': 'artificer',
                'level': 3,
                'data': {'infusions': 2},
              },
            ],
          ),
        ),
      );
      expect(find.text('Panel de pícaro 3'), findsOneWidget);
      expect(find.text('Artificer (nivel 3)'), findsOneWidget);
      expect(find.text('infusions: 2'), findsOneWidget);
    });
  });

  group('permisos', () {
    testWidgets('quien no es dueño ni DM no puede escribir pero sí tirar dados', (tester) async {
      final repo = _repo(ownerUserId: 'p2');
      await _pump(tester, characters: repo, role: CampaignRole.player);
      expect(tester.widget<IconButton>(find.byKey(const Key('hp-minus'))).onPressed, isNull);
      expect(
        tester.widget<ButtonStyleButton>(find.byKey(const Key('rest-request-long'))).onPressed,
        isNull,
      );
      await _tap(tester, 'combat-slot-1-pips');
      expect(repo.slotSpends, isEmpty);
      await _tap(tester, 'attack-roll-0');
      expect(find.byKey(const Key('dice-result')), findsOneWidget);
    });

    testWidgets('el DM puede escribir en el personaje de otro', (tester) async {
      final repo = _repo(ownerUserId: 'p2', temp: 0);
      await _pump(tester, characters: repo, role: CampaignRole.dm);
      await _tap(tester, 'hp-minus');
      expect(repo.damageCalls, hasLength(1));
    });
  });

  group('tipo de tirada (RollKind)', () {
    testWidgets('un 1 en la iniciativa no es pifia y muestra el d20 natural', (tester) async {
      await _pump(tester, characters: _repo(), face: 1);
      await _tap(tester, 'roll-initiative');
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '3');
      expect(find.text('d20 natural: 1'), findsOneWidget);
      expect(find.byKey(const Key('dice-fumble')), findsNothing);
      expect(find.byKey(const Key('dice-critical')), findsNothing);
    });

    testWidgets('un 1 en un ataque sí es pifia', (tester) async {
      await _pump(tester, characters: _repo(), face: 1);
      await _tap(tester, 'attack-roll-0');
      expect(find.byKey(const Key('dice-fumble')), findsOneWidget);
      expect(find.text('d20 natural: 1'), findsOneWidget);
    });

    testWidgets('"Tirar" en las salvaciones de muerte aplica un éxito con 10 o más', (
      tester,
    ) async {
      final down = _repo(hp: 0, temp: 0);
      await _pump(tester, characters: down, face: 12);
      await _tap(tester, 'roll-death-save');
      expect(down.combatPatches.last.deathSaveSuccesses, 1);
      expect(down.combatPatches.last.deathSaveFailures, 0);
      expect(down.combatPatches.last.hitPointsCurrent, isNull);
      expect(find.byKey(const Key('dice-death-save-revive')), findsNothing);
    });

    testWidgets('un 1 natural en la salvación de muerte son dos fallos', (tester) async {
      final down = _repo(hp: 0, temp: 0);
      await _pump(tester, characters: down, face: 1);
      await _tap(tester, 'roll-death-save');
      expect(find.byKey(const Key('dice-death-save-double-failure')), findsOneWidget);
      expect(find.byKey(const Key('dice-fumble')), findsNothing);
      expect(down.combatPatches.last.deathSaveFailures, 2);
    });

    testWidgets('un 20 natural en la salvación de muerte recupera 1 PG', (tester) async {
      final down = _repo(hp: 0, temp: 0);
      await _pump(tester, characters: down, face: 20);
      await _tap(tester, 'roll-death-save');
      expect(find.byKey(const Key('dice-death-save-revive')), findsOneWidget);
      expect(down.combatPatches.last.hitPointsCurrent, 1);
      expect(down.combatPatches.last.deathSaveSuccesses, 0);
      expect(down.combatPatches.last.deathSaveFailures, 0);
    });

    FakeCharactersRepository concentrating(int dc) => FakeCharactersRepository(
      characters: [
        makeCharacterJson(
          status: 'Active',
          temporaryHitPoints: 0,
          combat: makeCombatJson(),
          concentratingOnSpellIndex: 'bless',
        ),
      ],
    )..nextConcentrationDc = dc;

    testWidgets('"Tirar salvación" de concentración la supera con d20 + CON contra la CD', (
      tester,
    ) async {
      final repo = concentrating(12);
      await _pump(tester, characters: repo, face: 8);
      await _tap(tester, 'hp-minus');
      await _tap(tester, 'concentration-save-roll');
      // 8 + 4 (salvación de CON) = 12 contra CD 12.
      expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '12');
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();
      expect(repo.concentrationCalls, isEmpty);
      expect(find.text('12 contra CD 12: mantienes la concentración en Bless.'), findsOneWidget);
    });

    testWidgets('"Tirar salvación" de concentración la pierde por debajo de la CD', (tester) async {
      final repo = concentrating(12);
      await _pump(tester, characters: repo, face: 7);
      await _tap(tester, 'hp-minus');
      await _tap(tester, 'concentration-save-roll');
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();
      expect(repo.concentrationCalls, [null]);
      expect(find.byKey(const Key('concentration-chip')), findsNothing);
    });
  });
}

/// Answers every request with [body], recording the requests.
class _Adapter implements HttpClientAdapter {
  Object? body;
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    return ResponseBody.fromString(
      jsonEncode(body),
      200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
