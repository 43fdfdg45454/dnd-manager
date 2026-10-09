import 'package:dnd_companion/core/realtime/realtime_events.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/characters/ui/character_tabs.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'helpers/app_pump.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fake_realtime_hub.dart';
import 'helpers/fakes.dart';

const _invalidRoute = '/characters/ch1/invalid-choices';
const _prepareRoute = '/characters/ch1/prepare-spells';
const _rollsRoute = '/characters/ch1/rest-rolls';
const _playerRoute = '/campaigns/c1/player';

Map<String, dynamic> _invalidFeat() => {
  'replaceKey': 'replace.grappler',
  'classIndex': 'fighter',
  'key': 'asi',
  'level': 4,
  'setId': 'feats',
  'item': {'index': 'grappler', 'name': 'Grappler'},
  'reason': 'Requiere Fuerza 13.',
};

Map<String, dynamic> _invalidStyle() => {
  'replaceKey': 'replace.dueling',
  'classIndex': 'fighter',
  'key': 'fighting-style',
  'level': 1,
  'setId': 'fighting-styles',
  'item': {'index': 'dueling', 'name': 'Dueling'},
  'reason': 'Necesitas un arma cuerpo a cuerpo.',
};

Map<String, dynamic> _option(String index, String name, {Map<String, dynamic>? increase}) => {
  'index': index,
  'name': name,
  'description': ['$name description.'],
  'eligible': true,
  'abilityIncrease': increase,
};

Map<String, dynamic> _replaceChoice(
  String key,
  String name,
  String kind,
  List<Map<String, dynamic>> options,
) => {
  'key': key,
  'name': name,
  'kind': kind,
  'choose': 1,
  'required': 1,
  'replaces': true,
  'cumulative': false,
  'note': 'Ya no cumples sus requisitos.',
  'freeText': false,
  'options': options,
  'known': <Object>[],
};

Map<String, dynamic> _invalidPlan() => {
  'characterId': 'ch1',
  'invalid': [_invalidFeat(), _invalidStyle()],
  'choices': [
    _replaceChoice('replace.grappler', 'Sustituir «Grappler»', 'AsiOrFeat', [
      _option(
        'sturdy',
        'Sturdy',
        increase: {
          'amount': 1,
          'from': ['str', 'con'],
        },
      ),
    ]),
    _replaceChoice('replace.dueling', 'Sustituir «Dueling»', 'OptionSet', [
      _option('defense', 'Defense'),
      _option('archery', 'Archery'),
    ]),
  ],
};

Map<String, dynamic> _portent({bool pending = true, List<int> rolls = const []}) => {
  'id': 'r-portent',
  'key': 'portent',
  'name': 'Portent',
  'max': 2,
  'used': 0,
  'recharge': 'LongRest',
  'isAuto': true,
  'rollOnRest': {'dice': 'd20', 'count': 2, 'rest': 'long'},
  'rolls': rolls,
  'rollsPending': pending,
};

/// Plan with only the fighting style to replace.
Map<String, dynamic> _stylePlan() => {
  ..._invalidPlan(),
  'invalid': [_invalidStyle()],
  'choices': [(_invalidPlan()['choices'] as List)[1]],
};

Map<String, dynamic> _character({
  List<Map<String, dynamic>> invalid = const [],
  bool rollsPending = false,
  bool preparation = false,
  int? pendingLevelUpTo,
  List<Map<String, dynamic>>? resources,
}) {
  final portent = resources ?? [if (rollsPending) _portent()];
  return makeCharacterJson(
    status: 'Active',
    combat: makeCombatJson(resources: portent),
    resources: portent,
    classes: [
      {'classIndex': 'wizard', 'className': 'Wizard', 'level': 4},
    ],
    invalidChoices: invalid,
    restRollsPending: rollsPending,
    spellPreparationPending: preparation,
    spellPreparationReason: preparation ? 'LongRest' : null,
    pendingLevelUpTo: pendingLevelUpTo,
  );
}

Future<({FakeCharactersRepository characters, FakeRealtimeHub hub, GoRouter router})> _pump(
  WidgetTester tester,
  Map<String, dynamic> character, {
  String location = _playerRoute,
  Map<String, dynamic>? plan,
}) async {
  final characters = FakeCharactersRepository(characters: [character]);
  characters.invalidPlans['ch1'] = plan ?? _invalidPlan();
  characters.preparations['ch1'] = makePreparationJson();
  final hub = FakeRealtimeHub();
  final router = await pumpRealApp(
    tester,
    location: location,
    realtime: hub,
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
      characters: characters,
    ),
  );
  return (characters: characters, hub: hub, router: router);
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _tapKey(WidgetTester tester, String key) => _tap(tester, find.byKey(Key(key)));

void main() {
  group('modelos', () {
    test('el detalle lee invalidChoices, restRollsPending y las tiradas del recurso', () {
      final c = CharacterDetail.fromJson(_character(invalid: [_invalidFeat()], rollsPending: true));
      expect(c.invalidChoices.single.item.name, 'Grappler');
      expect(c.invalidChoices.single.replaceKey, 'replace.grappler');
      expect(c.invalidChoices.single.classIndex, 'fighter');
      expect(c.restRollsPending, isTrue);
      final portent = c.combat.resources.single;
      expect(portent.rollsPending, isTrue);
      expect(portent.rollOnRest!.count, 2);
      expect(portent.rollOnRest!.sides, 20);
    });

    test('InvalidChoices lee las sustituciones con el mismo formato que la subida', () {
      final plan = InvalidChoices.fromJson(_invalidPlan());
      expect(plan.invalid, hasLength(2));
      expect(plan.choices.first.isReplacement, isTrue);
      expect(plan.choices.first.kind, LevelChoiceKind.asiOrFeat);
      expect(plan.choices.first.option('sturdy')!.abilityIncrease!.needsPick, isTrue);
    });
  });

  group('sustituciones inválidas forzadas', () {
    testWidgets('se abren solas, no se pueden cerrar y piden una opción por elección', (
      tester,
    ) async {
      final (:characters, hub: _, :router) = await _pump(
        tester,
        _character(invalid: [_invalidFeat(), _invalidStyle()]),
      );
      expect(locationOf(router), _invalidRoute);
      expect(find.byKey(const Key('invalid-choices')), findsOneWidget);
      expect(find.byType(BackButton), findsNothing);
      expect(find.text('Requiere Fuerza 13.'), findsOneWidget);

      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(locationOf(router), _invalidRoute);

      FilledButton confirm() =>
          tester.widget<FilledButton>(find.byKey(const Key('invalid-choices-confirm')));
      expect(confirm().onPressed, isNull);

      await _tapKey(tester, 'replace-option-replace.dueling-defense');
      expect(confirm().onPressed, isNull, reason: 'falta la dote');
      await _tapKey(tester, 'replace-option-replace.grappler-sturdy');
      expect(confirm().onPressed, isNull, reason: 'la dote pide la característica');
      await _tapKey(tester, 'replace-ability-replace.grappler-con');
      expect(confirm().onPressed, isNotNull);

      await _tapKey(tester, 'invalid-choices-confirm');

      expect(characters.invalidReplacements.single, [
        {
          'key': 'replace.grappler',
          'selected': {'feat': 'sturdy', 'ability': 'con'},
        },
        {
          'key': 'replace.dueling',
          'selected': ['defense'],
        },
      ]);
      expect(locationOf(router), _playerRoute);
    });

    testWidgets('un error del servidor sale en línea y la pantalla sigue', (tester) async {
      final (:characters, hub: _, :router) = await _pump(
        tester,
        _character(invalid: [_invalidStyle()]),
        plan: _stylePlan(),
      );
      expect(locationOf(router), _invalidRoute);
      await _tapKey(tester, 'replace-option-replace.dueling-archery');
      characters.error = dioError(400, data: {'detail': 'Ya tienes esa opción.'});
      await _tapKey(tester, 'invalid-choices-confirm');
      expect(find.text('Ya tienes esa opción.'), findsOneWidget);
      expect(locationOf(router), _invalidRoute);
    });

    testWidgets('la hoja avisa mientras haya elecciones inválidas', (tester) async {
      final json = _character(invalid: [_invalidFeat()]);
      final c = CharacterDetail.fromJson(json);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(body: SummaryTab(character: c)),
        ),
      );
      expect(find.byKey(const Key('invalid-choices-notice')), findsOneWidget);
      expect(find.text('Grappler: Requiere Fuerza 13.'), findsOneWidget);

      final clean = CharacterDetail.fromJson(_character());
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(body: SummaryTab(character: clean)),
        ),
      );
      expect(find.byKey(const Key('invalid-choices-notice')), findsNothing);
    });
  });

  group('tiradas de descanso forzadas', () {
    testWidgets('un campo por dado, valida 1..dado y guarda los valores', (tester) async {
      final (:characters, hub: _, :router) = await _pump(tester, _character(rollsPending: true));
      expect(locationOf(router), _rollsRoute);
      expect(find.byKey(const Key('rest-rolls')), findsOneWidget);
      expect(find.text('Tira 2 d20.'), findsOneWidget);
      expect(find.byType(BackButton), findsNothing);

      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(locationOf(router), _rollsRoute);

      FilledButton save() => tester.widget<FilledButton>(find.byKey(const Key('rest-rolls-save')));
      expect(save().onPressed, isNull);

      await tester.enterText(find.byKey(const Key('rest-roll-r-portent-0')), '14');
      await tester.enterText(find.byKey(const Key('rest-roll-r-portent-1')), '21');
      await tester.pumpAndSettle();
      expect(find.text('Escribe un número de 1 a 20'), findsOneWidget);
      expect(save().onPressed, isNull);

      await tester.enterText(find.byKey(const Key('rest-roll-r-portent-1')), '3');
      await tester.pumpAndSettle();
      expect(save().onPressed, isNotNull);
      await _tapKey(tester, 'rest-rolls-save');

      expect(characters.rollSaves.single.resourceId, 'r-portent');
      expect(characters.rollSaves.single.values, [14, 3]);
      expect(locationOf(router), _playerRoute);
      // The values stay in the panel of the resource.
      expect(find.byKey(const Key('resource-r-portent-rolls')), findsOneWidget);
      expect(find.text('Tiradas: 14 · 3'), findsOneWidget);
    });

    testWidgets('"Tirar" rellena cada tirada con el dado virtual', (tester) async {
      final (:characters, hub: _, router: _) = await _pump(tester, _character(rollsPending: true));
      await _tapKey(tester, 'rest-roll-r-portent-0-dice');
      await _tapKey(tester, 'rest-roll-r-portent-1-dice');
      final values = [
        for (var i = 0; i < 2; i++)
          int.parse(
            tester.widget<TextField>(find.byKey(Key('rest-roll-r-portent-$i'))).controller!.text,
          ),
      ];
      expect(values, everyElement(inInclusiveRange(1, 20)));
      await _tapKey(tester, 'rest-rolls-save');
      expect(characters.rollSaves.single.values, values);
    });

    testWidgets('sin tiradas pendientes no se abre y el recurso muestra las guardadas', (
      tester,
    ) async {
      final (characters: _, hub: _, :router) = await _pump(
        tester,
        _character(
          resources: [
            _portent(pending: false, rolls: [7, 18]),
          ],
        ),
      );
      expect(locationOf(router), _playerRoute);
      expect(find.text('Tiradas: 7 · 18'), findsOneWidget);
    });
  });

  group('orden de la secuencia forzada', () {
    testWidgets('sustituciones, luego conjuros y por último las tiradas', (tester) async {
      final (characters: _, :hub, :router) = await _pump(
        tester,
        _character(invalid: [_invalidStyle()], preparation: true, rollsPending: true),
        plan: _stylePlan(),
      );
      expect(locationOf(router), _invalidRoute);

      await _tapKey(tester, 'replace-option-replace.dueling-defense');
      await _tapKey(tester, 'invalid-choices-confirm');
      expect(locationOf(router), _prepareRoute);

      await _tapKey(tester, 'prepare-spell-cure-wounds');
      await _tapKey(tester, 'prepare-confirm');
      expect(locationOf(router), _rollsRoute);

      await tester.enterText(find.byKey(const Key('rest-roll-r-portent-0')), '5');
      await tester.enterText(find.byKey(const Key('rest-roll-r-portent-1')), '6');
      await tester.pumpAndSettle();
      await _tapKey(tester, 'rest-rolls-save');
      expect(locationOf(router), _playerRoute);
      hub.emit(const CharacterUpdated(campaignId: 'c1', characterId: 'ch1'));
      await tester.pumpAndSettle();
      expect(locationOf(router), _playerRoute);
    });

    testWidgets('el nivel concedido va antes que todo lo demás', (tester) async {
      final (characters: _, hub: _, :router) = await _pump(
        tester,
        _character(
          invalid: [_invalidFeat()],
          preparation: true,
          rollsPending: true,
          pendingLevelUpTo: 5,
        ),
      );
      expect(locationOf(router), '/characters/ch1/level-up');
    });
  });
}
