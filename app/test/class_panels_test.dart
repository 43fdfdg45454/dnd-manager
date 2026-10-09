import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/core/storage/local_preferences.dart';
import 'package:dnd_companion/features/campaigns/data/campaigns_repository.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/catalog/data/catalog_repository.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:dnd_companion/features/characters/domain/class_theme.dart';
import 'package:dnd_companion/features/characters/ui/character_page.dart';
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
import 'helpers/motion.dart';

/// Opens the combat view of `ch1` with the fakes; every die shows [face].
Future<void> _pump(
  WidgetTester tester, {
  required FakeCharactersRepository characters,
  CampaignRole role = CampaignRole.player,
  FakeCatalogRepository? catalog,
  int face = 5,
}) async {
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
        inventoryRepositoryProvider.overrideWithValue(FakeInventoryRepository()),
        catalogRepositoryProvider.overrideWithValue(catalog ?? FakeCatalogRepository()),
        diceRandomProvider.overrideWithValue(SequenceRandom.always(face)),
        localPreferencesProvider.overrideWithValue(prefs),
      ],
      child: MaterialApp.router(routerConfig: router, builder: reducedMotionBuilder),
    ),
  );
  await tester.pumpAndSettle();
}

Map<String, dynamic> _resource(
  String id,
  String key,
  String name,
  int max, {
  int used = 0,
  String recharge = 'ShortRest',
}) => {
  'id': id,
  'key': key,
  'name': name,
  'max': max,
  'used': used,
  'recharge': recharge,
  'isAuto': true,
};

/// An active character of [classIndex] at [level] owned by the signed-in user.
/// The server sends no class panel for it: the panel works from the level
/// and the resources.
FakeCharactersRepository _repo(
  String classIndex,
  int level, {
  List<Map<String, dynamic>> resources = const [],
  List<Map<String, dynamic>> spellSlots = const [],
  Map<String, dynamic>? pactSlots,
  int hp = 20,
  String? ownerUserId = 'u1',
}) => FakeCharactersRepository(
  characters: [
    makeCharacterJson(
      status: 'Active',
      ownerUserId: ownerUserId,
      hitPointsCurrent: hp,
      classes: [
        {'classIndex': classIndex, 'className': classIndex, 'level': level},
      ],
      combat: makeCombatJson(resources: resources, spellSlots: spellSlots, pactSlots: pactSlots),
    ),
  ],
);

Future<void> _tap(WidgetTester tester, String key) async {
  await tester.tap(find.byKey(Key(key)));
  await tester.pumpAndSettle();
}

String _text(WidgetTester tester, String key) =>
    tester.widget<Text>(find.byKey(Key(key))).data ?? '';

bool _enabled(WidgetTester tester, String key) =>
    tester.widget<ButtonStyleButton>(find.byKey(Key(key))).onPressed != null;

void main() {
  testWidgets('bardo: dado por nivel e Inspirar gasta un uso', (tester) async {
    final repo = _repo(
      'bard',
      5,
      resources: [_resource('b1', 'bardic-inspiration', 'Bardic Inspiration', 3)],
    );
    await _pump(tester, characters: repo);
    expect(find.byKey(const Key('class-panel-bard')), findsOneWidget);
    expect(find.text('Bardo (nivel 5)'), findsOneWidget);
    expect(_text(tester, 'bard-inspiration-die'), 'Dado: d8');
    expect(_text(tester, 'bard-inspire-uses'), '3 / 3');

    await _tap(tester, 'bard-inspire');
    expect(repo.resourceSpends, [(id: 'b1', amount: 1)]);
    expect(_text(tester, 'bard-inspire-uses'), '2 / 3');
    expect(find.text('Inspiración bárdica concedida: 1d8.'), findsOneWidget);
  });

  testWidgets('clérigo: Canalizar divinidad y Destruir muertos vivientes por nivel', (
    tester,
  ) async {
    final repo = _repo(
      'cleric',
      8,
      resources: [_resource('c1', 'channel-divinity', 'Channel Divinity', 2)],
    );
    await _pump(tester, characters: repo);
    expect(find.byKey(const Key('class-panel-cleric')), findsOneWidget);
    expect(find.text('Destruir muertos vivientes: VD 1 o inferior'), findsOneWidget);

    await _tap(tester, 'cleric-channel-divinity');
    expect(repo.resourceSpends, [(id: 'c1', amount: 1)]);
    expect(_text(tester, 'cleric-channel-divinity-uses'), '1 / 2');
  });

  testWidgets('druida: VD máximo, gastar Forma salvaje activa el interruptor local', (
    tester,
  ) async {
    final repo = _repo('druid', 4, resources: [_resource('d1', 'wild-shape', 'Wild Shape', 2)]);
    await _pump(tester, characters: repo);
    expect(find.text('VD máximo: 1/2'), findsOneWidget);
    SwitchListTile toggle() =>
        tester.widget<SwitchListTile>(find.byKey(const Key('druid-wild-shape-active')));
    expect(toggle().value, isFalse);

    await _tap(tester, 'druid-wild-shape');
    expect(repo.resourceSpends, [(id: 'd1', amount: 1)]);
    expect(toggle().value, isTrue);

    await _tap(tester, 'druid-wild-shape-active');
    expect(toggle().value, isFalse);
    expect(repo.resourceSpends, hasLength(1));
  });

  group('druida: Recuperación natural', () {
    FakeCharactersRepository druid({Map<String, dynamic>? natural, bool used = false}) =>
        FakeCharactersRepository(
          characters: [
            makeCharacterJson(
              status: 'Active',
              classes: [
                {'classIndex': 'druid', 'className': 'Druid', 'level': 4},
              ],
              combat: makeCombatJson(
                spellSlots: [
                  {'level': 1, 'max': 4, 'used': 3},
                  {'level': 2, 'max': 3, 'used': 1},
                ],
                resources: [_resource('d1', 'wild-shape', 'Wild Shape', 2)],
                classPanels: [
                  {
                    'classIndex': 'druid',
                    'level': 4,
                    'data': {
                      'naturalRecovery': natural ?? {'used': used, 'slotLevelsRecoverable': 2},
                    },
                  },
                ],
              ),
            ),
          ],
        );

    testWidgets('el panel usa el endpoint nuevo con los niveles elegidos', (tester) async {
      final repo = druid();
      await _pump(tester, characters: repo);
      expect(find.byKey(const Key('druid-natural-recovery-card')), findsOneWidget);
      expect(_enabled(tester, 'natural-recovery'), isTrue);

      await _tap(tester, 'natural-recovery');
      expect(find.text('Recuperación natural'), findsWidgets);
      expect(find.text('Niveles seleccionados: 0 / 2'), findsOneWidget);
      await _tap(tester, 'arcane-level-1-plus');
      await _tap(tester, 'arcane-level-1-plus');
      await _tap(tester, 'arcane-confirm');

      expect(repo.classActions.single.action, 'natural-recovery');
      expect(repo.classActions.single.body, {
        'slotLevels': [1, 1],
      });
    });

    testWidgets('usada queda deshabilitada', (tester) async {
      await _pump(tester, characters: druid(used: true));
      expect(_enabled(tester, 'natural-recovery'), isFalse);
      expect(find.text('Recuperación natural (usada)'), findsOneWidget);
    });

    testWidgets('un druida sin el rasgo (el servidor no manda el dato) no ve la tarjeta', (
      tester,
    ) async {
      final repo = _repo('druid', 4, resources: [_resource('d1', 'wild-shape', 'Wild Shape', 2)]);
      await _pump(tester, characters: repo);
      expect(find.byKey(const Key('druid-natural-recovery-card')), findsNothing);
      expect(find.byKey(const Key('natural-recovery')), findsNothing);
    });
  });

  testWidgets('guerrero: Segundo aliento gasta el recurso y cura 1d10 + nivel', (tester) async {
    final repo = _repo(
      'fighter',
      3,
      hp: 10,
      resources: [
        _resource('f1', 'second-wind', 'Second Wind', 1),
        _resource('f2', 'action-surge', 'Action Surge', 1),
      ],
    );
    await _pump(tester, characters: repo, face: 5);
    expect(find.byKey(const Key('class-panel-fighter')), findsOneWidget);
    expect(find.byKey(const Key('fighter-indomitable-locked')), findsOneWidget);

    await _tap(tester, 'fighter-second-wind');
    expect(repo.resourceSpends, [(id: 'f1', amount: 1)]);
    // 1d10 shows 5, + 3 (level): 10 -> 18.
    expect(repo.combatPatches.single.hitPointsCurrent, 18);
    expect(find.text('18 / 28'), findsOneWidget);
    expect(find.textContaining('Recuperas 8 PG'), findsOneWidget);
    expect(_enabled(tester, 'fighter-second-wind'), isFalse);

    await _tap(tester, 'fighter-action-surge');
    expect(repo.resourceSpends.last, (id: 'f2', amount: 1));
  });

  testWidgets('monje: puntos de ki, Ráfaga de golpes gasta 1 y dado de artes marciales', (
    tester,
  ) async {
    final repo = _repo('monk', 5, resources: [_resource('k1', 'ki', 'Ki', 5)]);
    await _pump(tester, characters: repo);
    expect(find.text('Dado de artes marciales: d6'), findsOneWidget);
    expect(_text(tester, 'monk-ki-dc'), 'CD 11');

    await _tap(tester, 'monk-flurry-of-blows');
    expect(repo.resourceSpends, [(id: 'k1', amount: 1)]);
    expect(_text(tester, 'monk-ki-uses'), '4 / 5');
    expect(find.byKey(const Key('monk-martial-arts-critical')), findsOneWidget);
    await _tap(tester, 'monk-patient-defense');
    await _tap(tester, 'monk-step-of-the-wind');
    expect(repo.resourceSpends, hasLength(3));
  });

  testWidgets('clérigo: Intervención divina se tira con 1d100 y responde según el nivel', (
    tester,
  ) async {
    final repo = _repo('cleric', 10);
    await _pump(tester, characters: repo, face: 7);
    await _tap(tester, 'cleric-divine-intervention-roll');
    expect(find.text('Intervención divina'), findsWidgets);
    await tester.tapAt(const Offset(10, 10));
    await tester.pumpAndSettle();
    expect(
      find.text('7: tu deidad interviene. No podrás pedirlo de nuevo en 7 días.'),
      findsOneWidget,
    );
  });

  testWidgets('pícaro: Ataque furtivo por nivel se tira y hay recordatorios', (tester) async {
    final repo = _repo('rogue', 5);
    await _pump(tester, characters: repo, face: 4);
    expect(_text(tester, 'rogue-sneak-attack'), '3d6');
    expect(find.byKey(const Key('rogue-cunning-action')), findsOneWidget);
    expect(find.byKey(const Key('rogue-uncanny-dodge')), findsOneWidget);
    expect(find.byKey(const Key('rogue-evasion')), findsNothing);

    await _tap(tester, 'rogue-sneak-attack-roll');
    expect(find.byKey(const Key('dice-result')), findsOneWidget);
    expect(find.text('Ataque furtivo'), findsWidgets);
    expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '12');
    await tester.tapAt(const Offset(10, 10));
    await tester.pumpAndSettle();

    // "Crítico" duplica los dados: 6d6 a 4 = 24.
    await _tap(tester, 'rogue-sneak-attack-critical');
    expect(find.text('Tirar 6d6'), findsOneWidget);
    await _tap(tester, 'rogue-sneak-attack-roll');
    expect(tester.widget<Text>(find.byKey(const Key('dice-result-total'))).data, '24');
    expect(find.text('Ataque furtivo (crítico)'), findsOneWidget);
  });

  testWidgets('hechicero: convierte espacio en puntos y puntos en espacio', (tester) async {
    final repo = _repo(
      'sorcerer',
      3,
      resources: [_resource('s1', 'sorcery-points', 'Sorcery Points', 3, used: 1)],
      spellSlots: [
        {'level': 1, 'max': 4, 'used': 0},
        {'level': 2, 'max': 2, 'used': 0},
      ],
    );
    await _pump(tester, characters: repo);
    expect(_text(tester, 'sorcerer-points-uses'), '2 / 3');
    // Two points would go over the maximum.
    expect(
      tester.widget<ActionChip>(find.byKey(const Key('sorcerer-slot-to-points-2'))).onPressed,
      isNull,
    );

    await _tap(tester, 'sorcerer-slot-to-points-1');
    expect(repo.slotSpends, [(level: 1, amount: 1)]);
    expect(repo.resourceRestores, [(id: 's1', amount: 1)]);
    expect(_text(tester, 'sorcerer-points-uses'), '3 / 3');

    await _tap(tester, 'sorcerer-points-to-slot-1');
    expect(repo.resourceSpends, [(id: 's1', amount: 2)]);
    expect(repo.slotRestores, [(level: 1, amount: 1)]);

    await _tap(tester, 'sorcerer-points-pips');
    expect(repo.resourceSpends.last, (id: 's1', amount: 1));
  });

  testWidgets('hechicero: la tabla de su subclase se abre y busca el resultado (fase 22)', (
    tester,
  ) async {
    final repo = FakeCharactersRepository(
      characters: [
        makeCharacterJson(
          status: 'Active',
          classes: [
            {
              'classIndex': 'sorcerer',
              'className': 'sorcerer',
              'subclassIndex': 'pack-chispa',
              'level': 1,
            },
          ],
          combat: makeCombatJson(),
        ),
      ],
    );
    final catalog = FakeCatalogRepository(
      rollTableList: const [
        RollTable(
          key: 'surge-example',
          name: 'Oleada de magia salvaje',
          dice: 'd100',
          classIndex: 'sorcerer',
          subclassIndex: 'pack-chispa',
          entries: [
            RollTableEntry(from: 1, to: 50, text: 'Efecto ficticio bajo.'),
            RollTableEntry(from: 51, to: 100, text: 'Efecto ficticio alto.'),
          ],
        ),
        RollTable(key: 'otra', name: 'Otra subclase', dice: 'd4', subclassIndex: 'otra'),
      ],
    );
    await _pump(tester, characters: repo, catalog: catalog);
    expect(find.byKey(const Key('roll-table-open-otra')), findsNothing);

    await _tap(tester, 'roll-table-open-surge-example');
    await tester.enterText(find.byKey(const Key('roll-table-input')), '00');
    await tester.pumpAndSettle();
    expect(
      find.descendant(
        of: find.byKey(const Key('roll-table-result')),
        matching: find.text('Efecto ficticio alto.'),
      ),
      findsOneWidget,
    );

    // "Tirar" usa el dado virtual (todas las caras a 5) y rellena el resultado.
    await _tap(tester, 'roll-table-dice');
    expect(
      tester.widget<TextField>(find.byKey(const Key('roll-table-input'))).controller!.text,
      '5',
    );
    expect(
      find.descendant(
        of: find.byKey(const Key('roll-table-result')),
        matching: find.text('Efecto ficticio bajo.'),
      ),
      findsOneWidget,
    );
  });

  testWidgets('brujo: los espacios de pacto se gastan y lista invocaciones de los rasgos', (
    tester,
  ) async {
    final repo = _repo('warlock', 5, pactSlots: {'level': 3, 'max': 2, 'used': 0});
    final catalog = FakeCatalogRepository(
      classDetails: {
        'warlock': const ClassDetail(
          index: 'warlock',
          name: 'Warlock',
          levels: [
            ClassLevel(
              level: 2,
              features: [
                Feature(
                  index: 'eldritch-invocations',
                  name: 'Eldritch Invocations',
                  description: ['You learn two eldritch invocations.'],
                ),
              ],
            ),
          ],
        ),
      },
    );
    await _pump(tester, characters: repo, catalog: catalog);
    expect(_text(tester, 'warlock-pact-level'), 'Nivel 3');
    expect(_text(tester, 'warlock-pact-slots-uses'), '2 / 2');
    expect(_text(tester, 'warlock-invocations-known'), 'Conocidas: 3');
    expect(find.byKey(const Key('warlock-invocation-eldritch-invocations')), findsOneWidget);

    await _tap(tester, 'warlock-pact-slots');
    expect(repo.slotSpends, [(level: 0, amount: 1)]);
  });

  testWidgets('explorador: Marca del cazador fija la concentración', (tester) async {
    final repo = _repo('ranger', 6);
    await _pump(tester, characters: repo);
    expect(find.text('Enemigo predilecto (2)'), findsOneWidget);
    expect(find.text('Explorador natural (2)'), findsOneWidget);

    await _tap(tester, 'ranger-hunters-mark');
    expect(repo.concentrationCalls, ['hunters-mark']);
    expect(find.byKey(const Key('concentration-chip')), findsOneWidget);
    expect(find.text('Marca del cazador (activa)'), findsOneWidget);
    await _tap(tester, 'ranger-hunters-mark-critical');
    expect(find.text('Tirar 2d6'), findsOneWidget);
    expect(_enabled(tester, 'ranger-hunters-mark'), isFalse);
  });

  group('degradación', () {
    testWidgets('sin el recurso muestra "Recurso no disponible" y no deja gastar', (tester) async {
      final repo = _repo('bard', 3);
      await _pump(tester, characters: repo);
      expect(find.byKey(const Key('class-panel-bard')), findsOneWidget);
      expect(find.byKey(const Key('bard-inspire-unavailable')), findsOneWidget);
      expect(find.text('Usos: Recurso no disponible'), findsOneWidget);
      expect(_enabled(tester, 'bard-inspire'), isFalse);
    });

    testWidgets('por debajo del nivel del rasgo dice cuándo llega', (tester) async {
      await _pump(tester, characters: _repo('monk', 1));
      expect(find.byKey(const Key('monk-ki-locked')), findsOneWidget);
      expect(find.byKey(const Key('monk-flurry-of-blows')), findsNothing);
    });

    testWidgets('el brujo sin espacios de pacto no falla', (tester) async {
      await _pump(tester, characters: _repo('warlock', 1));
      expect(find.byKey(const Key('warlock-pact-slots-unavailable')), findsOneWidget);
    });

    testWidgets('bárbaro, mago y paladín sin datos del servidor no muestran panel', (tester) async {
      await _pump(tester, characters: _repo('barbarian', 3));
      expect(find.byKey(const Key('class-panel-barbarian')), findsNothing);
    });
  });

  group('permisos y acento', () {
    testWidgets('quien no es dueño ni DM ve el panel sin poder gastar', (tester) async {
      final repo = _repo('monk', 5, ownerUserId: 'p2', resources: [_resource('k1', 'ki', 'Ki', 5)]);
      await _pump(tester, characters: repo);
      expect(_enabled(tester, 'monk-flurry-of-blows'), isFalse);
      await _tap(tester, 'monk-ki-pips');
      expect(repo.resourceSpends, isEmpty);
    });

    testWidgets('cabecera, PG y panel llevan el acento de la clase', (tester) async {
      final repo = _repo('druid', 4, resources: [_resource('d1', 'wild-shape', 'Wild Shape', 2)]);
      await _pump(tester, characters: repo);
      final accent = classThemeOf('druid').accentLight;
      Color primaryAt(String key) =>
          Theme.of(tester.element(find.byKey(Key(key)))).colorScheme.primary;
      expect(primaryAt('class-panel-druid'), accent);
      expect(primaryAt('combat-hp'), accent);
      expect(primaryAt('character-title'), accent);
      expect(primaryAt('character-class-icon'), accent);
      // The rest of the combat view keeps the app's colours.
      expect(primaryAt('combat-ac'), isNot(accent));
    });
  });
}
