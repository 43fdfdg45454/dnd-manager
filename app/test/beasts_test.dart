import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:opentrpg_core/core/storage/local_preferences.dart';
import 'package:opentrpg_core/features/compendium/ui/compendium_page.dart';
import 'package:opentrpg_core/features/dice/data/dice_controller.dart';
import 'package:opentrpg_dnd5e/catalog/data/beast_models.dart';
import 'package:opentrpg_dnd5e/catalog/data/catalog_repository.dart';
import 'package:opentrpg_dnd5e/catalog/ui/beast_page.dart';
import 'package:opentrpg_dnd5e/characters/ui/combat/panels/druid.dart';
import 'package:opentrpg_dnd5e/dnd5e_routes.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'dice_test.dart' show SequenceRandom;
import 'helpers/catalog_fakes.dart';
import 'helpers/fakes.dart' show dnd5eSystemsOverride;
import 'helpers/motion.dart';

const _wolf = Beast(
  index: 'wolf',
  name: 'Wolf',
  size: 'Medium',
  challengeRating: 0.25,
  challengeRatingText: '1/4',
  armorClass: 13,
  hitPoints: 11,
  speeds: {'walk': 40},
  armorClassType: 'natural',
  hitDice: '2d8',
  hitPointsRoll: '2d8+2',
  abilities: {'str': 12, 'dex': 15, 'con': 12, 'int': 3, 'wis': 12, 'cha': 6},
  skills: {'perception': 3, 'stealth': 4},
  passivePerception: 13,
  traits: [BeastTrait(name: 'Pack Tactics', description: 'Advantage with allies nearby.')],
  actions: [
    BeastAction(
      name: 'Bite',
      description: 'Melee Weapon Attack: +4 to hit.',
      attackBonus: 4,
      damage: [BeastDamage(dice: '2d4+2', type: 'Piercing')],
      save: BeastSave(dc: 11, ability: 'str'),
    ),
  ],
);

const _crocodile = Beast(
  index: 'crocodile',
  name: 'Crocodile',
  challengeRating: 0.5,
  challengeRatingText: '1/2',
  speeds: {'walk': 20, 'swim': 20},
);

const _eagle = Beast(
  index: 'giant-eagle',
  name: 'Giant Eagle',
  challengeRating: 1,
  challengeRatingText: '1',
  speeds: {'walk': 10, 'fly': 80},
);

const _bear = Beast(
  index: 'brown-bear',
  name: 'Brown Bear',
  challengeRating: 1,
  challengeRatingText: '1',
  speeds: {'walk': 40, 'climb': 30},
);

FakeCatalogRepository _catalog() =>
    FakeCatalogRepository(beastList: const [_wolf, _crocodile, _bear, _eagle]);

Future<void> _pump(WidgetTester tester, Widget home, FakeCatalogRepository catalog) async {
  SharedPreferences.setMockInitialValues({});
  final prefs = await SharedPreferences.getInstance();
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (_, _) => home),
      GoRoute(
        path: Dnd5eRoutes.beastDetail,
        builder: (_, state) => BeastPage(index: state.pathParameters['index']!),
      ),
    ],
  );
  addTearDown(router.dispose);
  tester.view.physicalSize = const Size(800, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        catalogRepositoryProvider.overrideWithValue(catalog),
        diceRandomProvider.overrideWithValue(SequenceRandom.always(5)),
        localPreferencesProvider.overrideWithValue(prefs),
        dnd5eSystemsOverride(),
      ],
      child: MaterialApp.router(routerConfig: router, builder: reducedMotionBuilder),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('reglas de forma salvaje', () {
    test('VD máximo y movimiento por nivel', () {
      final l2 = wildShapeRules(2);
      expect((l2.maxCr, l2.crText, l2.fly, l2.swim), (0.25, '1/4', false, false));
      final l4 = wildShapeRules(4);
      expect((l4.maxCr, l4.crText, l4.fly, l4.swim), (0.5, '1/2', false, true));
      final l8 = wildShapeRules(8);
      expect((l8.maxCr, l8.crText, l8.fly, l8.swim), (1.0, '1', true, true));
    });

    test('Círculo de la Luna: VD 1 desde nivel 2 y nivel / 3 desde el 6', () {
      expect(wildShapeRules(2, moon: true).crText, '1');
      expect(wildShapeRules(2, moon: true).fly, isFalse);
      expect(wildShapeRules(5, moon: true).crText, '1');
      expect(wildShapeRules(6, moon: true).crText, '2');
      expect(wildShapeRules(9, moon: true).crText, '3');
      expect(wildShapeRules(20, moon: true).crText, '6');
    });
  });

  group('bestias', () {
    testWidgets('el compendio lista las bestias y abre su hoja', (tester) async {
      await _pump(tester, const CompendiumPage(), _catalog());
      await tester.tap(find.byKey(const Key('tab-beasts')));
      await tester.pumpAndSettle();

      expect(find.text('Wolf'), findsOneWidget);
      expect(find.text('VD 1/4 · CA 13 · 11 PG · 40 pies'), findsOneWidget);
      expect(find.text('Giant Eagle'), findsOneWidget);

      await tester.tap(find.text('Wolf'));
      await tester.pumpAndSettle();
      expect(
        find.text('Clase de armadura: 13 (armadura natural)', findRichText: true),
        findsOneWidget,
      );
      expect(find.text('Puntos de golpe: 11 (2d8+2)', findRichText: true), findsOneWidget);
      expect(
        find.text('Habilidades: Percepción +3, Sigilo +4', findRichText: true),
        findsOneWidget,
      );
      expect(find.text('Pack Tactics'), findsOneWidget);
      expect(find.text('CD 11 de Fuerza'), findsOneWidget);
    });

    testWidgets('las acciones tiran el ataque y el daño', (tester) async {
      await _pump(tester, const BeastPage(index: 'wolf'), _catalog());

      await tester.tap(find.byKey(const Key('beast-attack-0')));
      await tester.pumpAndSettle();
      // d20 showing 5 + 4.
      expect(find.text('9'), findsOneWidget);
      expect(find.text('Bite: ataque'), findsOneWidget);
      await tester.tapAt(const Offset(400, 10));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('beast-damage-0')));
      await tester.pumpAndSettle();
      // 2d4 (each die at most 4) + 2.
      expect(find.text('Bite: daño · Piercing'), findsOneWidget);
    });

    testWidgets('el druida ve solo las formas que permite su nivel', (tester) async {
      final catalog = _catalog();
      await _pump(tester, const Scaffold(body: WildShapeForms(level: 4)), catalog);

      expect(catalog.beastCalls.single, (maxCr: 0.5, fly: false, swim: null));
      expect(find.text('Formas disponibles (2)'), findsOneWidget);
      await tester.tap(find.byKey(const Key('druid-wild-shape-forms')));
      await tester.pumpAndSettle();
      expect(find.text('Wolf'), findsOneWidget);
      expect(find.text('Crocodile'), findsOneWidget);
      expect(find.text('Brown Bear'), findsNothing);

      await tester.tap(find.text('Crocodile'));
      await tester.pumpAndSettle();
      expect(find.byType(BeastPage), findsOneWidget);
    });

    testWidgets('Círculo de la Luna amplía el VD sin permitir volar', (tester) async {
      final catalog = _catalog();
      await _pump(tester, const Scaffold(body: WildShapeForms(level: 4, moon: true)), catalog);

      expect(catalog.beastCalls.single, (maxCr: 1.0, fly: false, swim: null));
      expect(find.text('Formas disponibles (3)'), findsOneWidget);
      expect(find.text('Bestias de VD 1 o menos'), findsOneWidget);
    });
  });
}
