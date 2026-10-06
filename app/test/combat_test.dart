import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/network/api_client.dart';
import 'package:dnd_companion/core/storage/local_preferences.dart';
import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/features/campaigns/data/campaigns_repository.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/catalog/data/catalog_repository.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/characters/data/view_mode_controller.dart';
import 'package:dnd_companion/features/characters/ui/character_page.dart';
import 'package:dnd_companion/features/characters/ui/combat/class_panels.dart';
import 'package:dnd_companion/features/characters/ui/combat/combat_support.dart' show Pip;
import 'package:dnd_companion/features/dice/data/dice_controller.dart';
import 'package:dnd_companion/features/items/data/inventory_repository.dart';
import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'dice_test.dart' show SequenceRandom;
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';

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
    SharedPreferences.setMockInitialValues({if (startInCombat) 'character.ch1.view': 'combat'});
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
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
        campaignsRepositoryProvider.overrideWithValue(
          FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
        ),
        charactersRepositoryProvider.overrideWithValue(characters),
        inventoryRepositoryProvider.overrideWithValue(inventory ?? FakeInventoryRepository()),
        catalogRepositoryProvider.overrideWithValue(catalog ?? FakeCatalogRepository()),
        diceRandomProvider.overrideWithValue(SequenceRandom.always(face)),
        localPreferencesProvider.overrideWithValue(prefs),
      ],
      child: MaterialApp.router(routerConfig: router),
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
      final repository = CharactersRepository(
        ApiClient(
          baseUrl: 'http://localhost',
          dio: Dio(BaseOptions(baseUrl: 'http://localhost'))..httpClientAdapter = adapter,
        ),
      );
      adapter.body = makeCharacterJson();
      await repository.classAction('ch1', 'lay-on-hands', {'amount': 5, 'targetSelf': false});
      expect(adapter.requests.last.method, 'POST');
      expect(adapter.requests.last.path, '/api/v1/characters/ch1/class-actions/lay-on-hands');
      expect(adapter.requests.last.data, {'amount': 5, 'targetSelf': false});
      await repository.classAction('ch1', 'rage');
      expect(adapter.requests.last.path, '/api/v1/characters/ch1/class-actions/rage');
      expect(adapter.requests.last.data, <String, dynamic>{});

      adapter.body = {'character': makeCharacterJson(name: 'Tras el castigo'), 'damageDice': '3d8'};
      final smite = await repository.divineSmite('ch1', 2);
      expect(adapter.requests.last.path, '/api/v1/characters/ch1/class-actions/divine-smite');
      expect(adapter.requests.last.data, {'slotLevel': 2});
      expect(smite.damageDice, '3d8');
      expect(smite.character.name, 'Tras el castigo');
    });
  });

  group('conmutador Detallado / Combate', () {
    testWidgets('empieza en Detallado, cambia a Combate y lo recuerda por personaje', (
      tester,
    ) async {
      final repo = _repo();
      SharedPreferences.setMockInitialValues({'character.otro.view': 'combat'});
      final prefs = await SharedPreferences.getInstance();
      await _pump(tester, characters: repo, prefs: prefs);
      // Solo cuenta la clave del propio personaje.
      expect(find.byKey(const Key('tab-summary')), findsOneWidget);
      expect(find.byKey(const Key('combat-view')), findsNothing);
      final segmented = tester.widget<SegmentedButton<CharacterViewMode>>(
        find.byKey(const Key('view-mode')),
      );
      expect(segmented.segments.every((s) => s.enabled), isTrue);

      Finder segment(String label) =>
          find.descendant(of: find.byKey(const Key('view-mode')), matching: find.text(label));
      await tester.tap(segment('Combate'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('combat-view')), findsOneWidget);
      expect(find.byKey(const Key('tab-summary')), findsNothing);
      expect(prefs.getString('character.ch1.view'), 'combat');

      // Una pantalla nueva con las mismas preferencias abre directamente en Combate.
      await tester.pumpWidget(const SizedBox());
      await _pump(tester, characters: repo, prefs: prefs);
      expect(find.byKey(const Key('combat-view')), findsOneWidget);

      await tester.tap(segment('Detallado'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('tab-summary')), findsOneWidget);
      expect(prefs.getString('character.ch1.view'), 'detailed');
    });
  });

  group('puntos de golpe', () {
    testWidgets('PG - reduce el valor mostrado tras el PATCH', (tester) async {
      final repo = _repo(temp: 0);
      await _pump(tester, characters: repo);
      expect(find.text('20 / 28'), findsOneWidget);

      await _tap(tester, 'hp-minus');
      expect(repo.combatPatches.single.hitPointsCurrent, 19);
      expect(repo.combatPatches.single.temporaryHitPoints, isNull);
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
      expect(repo.combatPatches.last.temporaryHitPoints, 0);
      expect(repo.combatPatches.last.hitPointsCurrent, 18);
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

      await _tap(tester, 'tab-skills');
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
    testWidgets('el corto pide dados de golpe por clase', (tester) async {
      final repo = _repo();
      await _pump(tester, characters: repo);
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
      await _pump(tester, characters: repo);
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
        tester.widget<ButtonStyleButton>(find.byKey(const Key('rest-long'))).onPressed,
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
      expect(repo.combatPatches, hasLength(1));
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
