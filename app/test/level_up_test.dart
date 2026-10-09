import 'package:dio/dio.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:dnd_companion/features/characters/data/level_up_controller.dart';
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/characters/ui/level_up/hit_points_step.dart';
import 'package:dnd_companion/features/dice/data/dice_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'dice_test.dart' show SequenceRandom;
import 'helpers/app_pump.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

// Fictional option names stand in for content outside the SRD.

Map<String, dynamic> _option(
  String index,
  String name, {
  bool eligible = true,
  String? reason,
  String? prerequisites,
  List<Map<String, dynamic>> effects = const [],
  Map<String, dynamic>? abilityIncrease,
  int? spellLevel,
}) => {
  'index': index,
  'name': name,
  'description': ['$name description.'],
  'prerequisitesText': prerequisites,
  'eligible': eligible,
  'reason': reason,
  'spellLevel': spellLevel,
  'effectsPreview': effects,
  'abilityIncrease': abilityIncrease,
};

Map<String, dynamic> _choice(
  String key,
  String name,
  String kind, {
  int choose = 1,
  int? required,
  String? subclassIndex,
  bool replaces = false,
  List<Map<String, dynamic>> options = const [],
  List<Map<String, dynamic>> known = const [],
}) => {
  'key': key,
  'name': name,
  'kind': kind,
  'choose': choose,
  'required': required ?? choose,
  'replaces': replaces,
  'cumulative': false,
  'note': '',
  'subclassIndex': subclassIndex,
  'setId': null,
  'freeText': false,
  'options': options,
  'known': known,
};

Map<String, dynamic> _plan({
  int targetLevel = 3,
  int classLevel = 3,
  List<Map<String, dynamic>> features = const [],
  List<Map<String, dynamic>> choices = const [],
}) => {
  'characterId': 'ch1',
  'currentLevel': targetLevel - 1,
  'targetLevel': targetLevel,
  'classIndex': 'fighter',
  'classLevel': classLevel,
  'hitDie': 10,
  'conModifier': 2,
  'classes': [
    {
      'classIndex': 'fighter',
      'name': 'Fighter',
      'allowed': true,
      'reason': null,
      'hitDie': 10,
      'isNew': false,
      'currentLevel': classLevel - 1,
      'subclassIndex': null,
    },
    {
      'classIndex': 'wizard',
      'name': 'Wizard',
      'allowed': false,
      'reason': 'Necesitas Inteligencia 13 (tienes 11).',
      'hitDie': 6,
      'isNew': true,
      'currentLevel': 0,
      'subclassIndex': null,
    },
  ],
  'automaticFeatures': features,
  'choices': choices,
  'spellcasting': null,
};

Map<String, dynamic> _feature(String index, String name, {String? subclassIndex}) => {
  'classIndex': 'fighter',
  'subclassIndex': subclassIndex,
  'level': 3,
  'feature': {
    'index': index,
    'name': name,
    'description': ['$name description.'],
  },
};

/// Fighter 2 → 3: archetype (two fictional ones) and, for the second, three
/// manoeuvres; a base feature and a feature of each archetype.
Map<String, dynamic> _archetypePlan() => _plan(
  features: [
    _feature('martial-archetype', 'Martial Archetype'),
    _feature('keen-edge', 'Keen Edge', subclassIndex: 'champion'),
    _feature('drill-master', 'Drill Master', subclassIndex: 'tactician'),
  ],
  choices: [
    _choice(
      'subclass',
      'Martial Archetype',
      'Subclass',
      options: [_option('champion', 'Champion'), _option('tactician', 'Tactician')],
    ),
    _choice(
      'maneuvers',
      'Maneuvers',
      'OptionSet',
      choose: 3,
      subclassIndex: 'tactician',
      options: [
        _option('feint', 'Feint'),
        _option('parry', 'Parry'),
        _option('sweep', 'Sweep'),
        _option('rally', 'Rally'),
      ],
    ),
  ],
);

/// Fighter 3 → 4: Ability Score Improvement or a feat.
Map<String, dynamic> _improvementPlan() => _plan(
  targetLevel: 4,
  classLevel: 4,
  choices: [
    _choice(
      'asi',
      'Ability Score Improvement',
      'AsiOrFeat',
      options: [
        _option(
          'grappler',
          'Grappler',
          eligible: false,
          prerequisites: 'Fuerza 13',
          reason: 'Requiere Fuerza 13.',
        ),
        _option(
          'sturdy',
          'Sturdy',
          abilityIncrease: {
            'amount': 1,
            'from': ['str', 'con'],
          },
          effects: [
            {
              'source': 'feature',
              'label': 'PG máx',
              'value': 4,
              'field': 'hitPointsMax',
              'before': 28,
              'after': 32,
              'condition': null,
            },
          ],
        ),
      ],
    ),
  ],
);

/// Fighter with Str 19 without items (the 20 cap applies at +1).
Map<String, dynamic> _character({int? pendingLevelUpTo = 3, List<Object>? choices}) =>
    makeCharacterJson(
      status: 'Active',
      pendingLevelUpTo: pendingLevelUpTo,
      combat: makeCombatJson(),
      classes: [
        {'classIndex': 'fighter', 'className': 'Fighter', 'level': (pendingLevelUpTo ?? 3) - 1},
      ],
      breakdowns: {
        'ability.str': makeBreakdownJson([('base', 'Base', 17), ('race', 'Humano', 2)]),
      },
    )..['choices'] = choices ?? const <Object>[];

DioException _problem(int status, String detail) {
  final options = RequestOptions(path: '/api/v1/characters/ch1/level-up');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response<dynamic>(
      requestOptions: options,
      statusCode: status,
      data: {'title': 'Conflicto.', 'status': status, 'detail': detail},
    ),
  );
}

Future<({FakeCharactersRepository characters, GoRouter router})> _pump(
  WidgetTester tester, {
  required Map<String, dynamic> plan,
  Map<String, dynamic>? character,
  String location = '/characters/ch1/level-up',
  int face = 6,
}) async {
  final characters = FakeCharactersRepository(characters: [character ?? _character()]);
  characters.levelUpPlans[''] = plan;
  final fakes = AppFakes(
    campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
    characters: characters,
  );
  final router = await pumpRealApp(
    tester,
    location: location,
    fakes: fakes,
    overrides: [diceRandomProvider.overrideWithValue(SequenceRandom.always(face))],
  );
  return (characters: characters, router: router);
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _tapKey(WidgetTester tester, String key) => _tap(tester, find.byKey(Key(key)));

Future<void> _next(WidgetTester tester) => _tapKey(tester, 'levelup-next');

Future<void> _writeHp(WidgetTester tester, String value) async {
  await tester.enterText(find.byKey(const Key('levelup-hp-field')), value);
  await tester.pumpAndSettle();
}

/// The error under the bottom bar, with [text].
Finder _error(String text) => find.byWidgetPredicate(
  (w) => w is Text && w.key == const Key('levelup-error') && w.data == text,
);

void main() {
  group('Modelos', () {
    test('LevelUpPlan lee clases, rasgos, elecciones y efectos', () {
      final plan = LevelUpPlan.fromJson(_improvementPlan());
      expect(plan.targetLevel, 4);
      expect(plan.hitDie, 10);
      expect(plan.classes.last.allowed, isFalse);
      final choice = plan.choices.single;
      expect(choice.kind, LevelChoiceKind.asiOrFeat);
      expect(choice.option('grappler')!.reason, 'Requiere Fuerza 13.');
      final sturdy = choice.option('sturdy')!;
      expect(sturdy.abilityIncrease!.needsPick, isTrue);
      expect(sturdy.effectsPreview.single.text, 'PG máx 28 → 32');
      expect(plan.newFeatures, isEmpty);
      expect(choice.warning, isNull);
    });

    test('LevelUpPlan lee newFeatures, warning y preparesSpells', () {
      final plan = LevelUpPlan.fromJson(
        _plan(
            choices: [
              _choice('expertise', 'Expertise', 'Expertise', choose: 2, required: 0)
                ..['warning'] = 'Aviso.',
            ],
          )
          ..['newFeatures'] = [
            {
              'name': 'Base',
              'description': <String>['x'],
              'subclassIndex': null,
            },
            {'name': 'Sub', 'description': <String>[], 'subclassIndex': 'champion'},
          ]
          ..['spellcasting'] = {'classIndex': 'fighter', 'preparesSpells': true},
      );
      expect(plan.choices.single.warning, 'Aviso.');
      expect(plan.newFeaturesFor(null).map((f) => f.name), ['Base']);
      expect(plan.newFeaturesFor('champion').map((f) => f.name), ['Base', 'Sub']);
      expect(plan.spellcasting!.preparesSpells, isTrue);
    });

    test('CharacterDetail lee choices[] con selección, mejora y dote', () {
      final detail = CharacterDetail.fromJson(
        _character(
          choices: [
            {
              'level': 1,
              'classIndex': 'fighter',
              'key': 'fighting-style',
              'name': 'Fighting Style',
              'kind': 'OptionSet',
              'selected': [
                {'index': 'defense', 'name': 'Defense'},
              ],
              'replaced': <Object>[],
            },
            {
              'level': 4,
              'classIndex': 'fighter',
              'key': 'asi',
              'name': 'Ability Score Improvement',
              'kind': 'AsiOrFeat',
              'selected': <Object>[],
              'replaced': <Object>[],
              'asi': {'str': 1, 'dex': 1},
            },
          ],
        ),
      );
      expect(detail.choices, hasLength(2));
      expect(detail.choices.first.selected.single.name, 'Defense');
      expect(detail.choices.last.asi, {'str': 1, 'dex': 1});
    });
  });

  group('LevelUpController', () {
    Future<(ProviderContainer, FakeCharactersRepository)> load(Map<String, dynamic> plan) async {
      final characters = FakeCharactersRepository(characters: [_character()]);
      characters.levelUpPlans[''] = plan;
      final container = ProviderContainer(
        overrides: [charactersRepositoryProvider.overrideWithValue(characters)],
      );
      addTearDown(container.dispose);
      container.listen(levelUpControllerProvider('ch1'), (_, _) {});
      await pumpEventQueue();
      return (container, characters);
    }

    test('Confirmar se habilita solo con todas las elecciones completas', () async {
      final (container, _) = await load(_archetypePlan());
      final provider = levelUpControllerProvider('ch1');
      final controller = container.read(provider.notifier);
      LevelUpState state() => container.read(provider);

      expect(state().plan, isNotNull);
      expect(controller.canConfirm, isFalse);
      controller.setHitPoints('8');
      expect(controller.canConfirm, isFalse, reason: 'falta la subclase');

      final subclass = state().choiceOf('subclass')!;
      controller.toggleOption(subclass, 'tactician');
      expect(state().steps.map((s) => s.id), [
        'class',
        'hp',
        'choice-subclass',
        'choice-maneuvers',
        'review',
      ]);
      final maneuvers = state().choiceOf('maneuvers')!;
      controller.toggleOption(maneuvers, 'feint');
      controller.toggleOption(maneuvers, 'parry');
      expect(controller.canConfirm, isFalse);
      expect(state().validateChoice(maneuvers), 'Elige 3 opciones');

      controller.toggleOption(maneuvers, 'sweep');
      expect(controller.canConfirm, isTrue);
      expect(state().request.toJson()['choices'], [
        {
          'key': 'subclass',
          'selected': ['tactician'],
        },
        {
          'key': 'maneuvers',
          'selected': ['feint', 'parry', 'sweep'],
        },
      ]);

      // Another archetype drops the manoeuvres and their answer.
      controller.toggleOption(subclass, 'champion');
      expect(state().choiceOf('maneuvers'), isNull);
      expect(state().selections.containsKey('maneuvers'), isFalse);
      expect(controller.canConfirm, isTrue);
    });

    test('una sustitución pide una opción más y viaja en replaced', () async {
      final (container, _) = await load(
        _plan(
          choices: [
            _choice(
              'cantrips',
              'Cantrips',
              'CantripsKnown',
              choose: 0,
              replaces: true,
              options: [_option('mage-hand', 'Mage Hand', spellLevel: 0)],
              known: [
                {'index': 'light', 'name': 'Light'},
              ],
            ),
          ],
        ),
      );
      final provider = levelUpControllerProvider('ch1');
      final controller = container.read(provider.notifier);
      LevelUpState state() => container.read(provider);
      controller.setHitPoints('3');
      final cantrips = state().choiceOf('cantrips')!;
      expect(state().neededPicks(cantrips), 0);
      expect(controller.canConfirm, isTrue, reason: 'sustituir es opcional');
      expect(state().request.choices, isEmpty);

      controller.setReplaced(cantrips, 'light');
      expect(state().neededPicks(cantrips), 1);
      expect(controller.canConfirm, isFalse);
      controller.toggleOption(cantrips, 'mage-hand');
      expect(controller.canConfirm, isTrue);
      expect(state().request.toJson()['choices'], [
        {
          'key': 'cantrips',
          'selected': ['mage-hand'],
          'replaced': ['light'],
        },
      ]);

      controller.setReplaced(cantrips, null);
      expect(state().selectionOf('cantrips').selected, isEmpty);
    });

    test('cambiar de clase vuelve a pedir el plan', () async {
      final (container, characters) = await load(_archetypePlan());
      characters.levelUpPlans['rogue'] = {..._archetypePlan(), 'classIndex': 'rogue'};
      final controller = container.read(levelUpControllerProvider('ch1').notifier);
      await controller.selectClass('rogue');
      expect(characters.levelUpPlanRequests, [null, 'rogue']);
      expect(container.read(levelUpControllerProvider('ch1')).plan!.classIndex, 'rogue');
    });
  });

  group('Asistente de subida de nivel', () {
    testWidgets('carga el plan con la clase preseleccionada y los rasgos automáticos', (
      tester,
    ) async {
      final (:characters, router: _) = await _pump(tester, plan: _archetypePlan());

      expect(find.byKey(const Key('levelup-class')), findsOneWidget);
      expect(find.text('Subes a nivel 3'), findsOneWidget);
      expect(characters.levelUpPlanRequests, [null]);
      expect(find.byKey(const Key('levelup-class-fighter')), findsOneWidget);
      expect(
        find.byKey(const Key('levelup-class-reason-wizard')),
        findsOneWidget,
        reason: 'la clase no permitida muestra el motivo',
      );
      expect(find.text('Necesitas Inteligencia 13 (tienes 11).'), findsOneWidget);
      expect(find.byKey(const Key('levelup-feature-martial-archetype')), findsOneWidget);
      // The archetype features wait until an archetype is chosen.
      expect(find.byKey(const Key('levelup-feature-keen-edge')), findsNothing);

      // A class that is not allowed cannot be selected.
      await _tapKey(tester, 'levelup-class-wizard');
      expect(characters.levelUpPlanRequests, [null]);

      await _tap(tester, find.text('Martial Archetype').first);
      expect(find.text('Martial Archetype description.'), findsOneWidget);
    });

    testWidgets('PG fuera de rango bloquea el avance', (tester) async {
      await _pump(tester, plan: _archetypePlan());
      await _next(tester);
      expect(find.byKey(const Key('levelup-hp')), findsOneWidget);
      expect(find.text('Tira 1d10 y escribe el resultado'), findsOneWidget);

      await _next(tester);
      expect(_error('Escribe el resultado del dado (1-10)'), findsOneWidget);

      await _writeHp(tester, '11');
      await _next(tester);
      expect(_error('Escribe el resultado del dado (1-10)'), findsOneWidget);
      expect(find.byKey(const Key('levelup-hp')), findsOneWidget);

      await _writeHp(tester, '0');
      await _next(tester);
      expect(find.byKey(const Key('levelup-hp')), findsOneWidget);

      await _writeHp(tester, '7');
      expect(find.text('+ Con 2 = +9 PG'), findsOneWidget);
      await _next(tester);
      expect(find.byKey(const Key('levelup-choice-subclass')), findsOneWidget);
    });

    test('el valor fijo de PG es la mitad del dado más uno', () {
      expect([6, 8, 10, 12].map(fixedHitPoints), [4, 5, 6, 7]);
    });

    testWidgets('PG: "Tirar" usa el dado virtual y "Usar el valor fijo" rellena el campo', (
      tester,
    ) async {
      await _pump(tester, plan: _archetypePlan(), face: 9);
      await _next(tester);
      await _tapKey(tester, 'levelup-hp-roll');
      expect(find.text('9'), findsWidgets);
      expect(find.text('+ Con 2 = +11 PG'), findsOneWidget);

      await _tapKey(tester, 'levelup-hp-fixed');
      expect(find.text('Usar el valor fijo (6)'), findsOneWidget);
      expect(find.text('+ Con 2 = +8 PG'), findsOneWidget);
      expect(find.byKey(const Key('levelup-hp-fixed-hint')), findsOneWidget);
      await _next(tester);
      expect(find.byKey(const Key('levelup-choice-subclass')), findsOneWidget);
    });

    testWidgets('la elección de subclase añade las suyas y exige el número exacto', (tester) async {
      await _pump(tester, plan: _archetypePlan());
      await _next(tester);
      await _writeHp(tester, '6');
      await _next(tester);

      expect(find.byKey(const Key('levelup-dot-choice-maneuvers')), findsNothing);
      await _next(tester);
      expect(_error('Elige 1 opción'), findsOneWidget);

      await _tapKey(tester, 'levelup-option-subclass-champion');
      expect(find.byKey(const Key('levelup-dot-choice-maneuvers')), findsNothing);

      await _tapKey(tester, 'levelup-option-subclass-tactician');
      expect(find.byKey(const Key('levelup-dot-choice-maneuvers')), findsOneWidget);
      expect(find.text('1 de 1'), findsOneWidget);

      await _next(tester);
      expect(find.byKey(const Key('levelup-choice-maneuvers')), findsOneWidget);
      await _tapKey(tester, 'levelup-option-maneuvers-feint');
      await _tapKey(tester, 'levelup-option-maneuvers-parry');
      expect(find.text('2 de 3'), findsOneWidget);
      await _next(tester);
      expect(_error('Elige 3 opciones'), findsOneWidget);
      expect(find.byKey(const Key('levelup-choice-maneuvers')), findsOneWidget);

      await _tapKey(tester, 'levelup-option-maneuvers-rally');
      expect(find.text('3 de 3'), findsOneWidget);
      // A fourth pick is ignored.
      await _tapKey(tester, 'levelup-option-maneuvers-sweep');
      expect(find.text('3 de 3'), findsOneWidget);

      await _next(tester);
      expect(find.byKey(const Key('levelup-review')), findsOneWidget);
      expect(find.text('Feint, Parry, Rally'), findsOneWidget);
      final confirm = tester.widget<ButtonStyleButton>(find.byKey(const Key('levelup-confirm')));
      expect(confirm.onPressed, isNotNull);
    });

    testWidgets('el resumen lista los rasgos nuevos de la subclase elegida y avisa de preparar', (
      tester,
    ) async {
      final plan = _archetypePlan()
        ..['newFeatures'] = [
          {
            'name': 'Martial Archetype',
            'description': <String>['Archetype description.'],
            'subclassIndex': null,
          },
          {
            'name': 'Keen Edge',
            'description': <String>['Keen Edge description.'],
            'subclassIndex': 'champion',
          },
          {
            'name': 'Drill Master',
            'description': <String>['Drill Master description.'],
            'subclassIndex': 'tactician',
          },
        ]
        ..['spellcasting'] = {
          'classIndex': 'fighter',
          'ability': 'int',
          'isPactCaster': false,
          'cantripsKnown': null,
          'spellsKnown': null,
          'maxSpellLevel': 1,
          'currentCantrips': 0,
          'currentSpells': 0,
          'spellSlots': [2, 0, 0, 0, 0, 0, 0, 0, 0],
          'preparesSpells': true,
        };
      await _pump(tester, plan: plan);
      await _next(tester);
      await _writeHp(tester, '6');
      await _next(tester);
      await _tapKey(tester, 'levelup-option-subclass-champion');
      await _next(tester);

      expect(find.byKey(const Key('levelup-review')), findsOneWidget);
      final section = find.byKey(const Key('levelup-new-features'));
      await tester.ensureVisible(section);
      await tester.pumpAndSettle();
      expect(find.text('Rasgos nuevos'), findsOneWidget);
      expect(find.byKey(const Key('levelup-new-feature-Martial Archetype')), findsOneWidget);
      expect(find.byKey(const Key('levelup-new-feature-Keen Edge')), findsOneWidget);
      expect(find.byKey(const Key('levelup-new-feature-Drill Master')), findsNothing);
      final note = find.byKey(const Key('levelup-prepare-note'));
      await tester.ensureVisible(note);
      await tester.pumpAndSettle();
      expect(note, findsOneWidget);
    });

    testWidgets('una elección sin opciones elegibles muestra el aviso del servidor', (
      tester,
    ) async {
      const warning = 'Ninguna opción cumple los requisitos ahora mismo.';
      final plan = _plan(
        choices: [
          _choice('expertise', 'Expertise', 'Expertise', choose: 2, required: 0)
            ..['warning'] = warning,
        ],
      );
      await _pump(tester, plan: plan);
      await _next(tester);
      await _writeHp(tester, '6');
      await _next(tester);

      expect(find.byKey(const Key('levelup-choice-expertise')), findsOneWidget);
      expect(find.byKey(const Key('levelup-choice-warning')), findsOneWidget);
      expect(find.text(warning), findsOneWidget);
      // Nothing is required, so the review is reachable and "Confirmar" is enabled.
      await _next(tester);
      expect(find.byKey(const Key('levelup-review')), findsOneWidget);
      expect(find.text('Nada (opcional)'), findsOneWidget);
    });

    testWidgets('Mejora: reparte 2 puntos con el tope de 20 en vivo', (tester) async {
      await _pump(tester, plan: _improvementPlan(), character: _character(pendingLevelUpTo: 4));
      await _next(tester);
      await _writeHp(tester, '5');
      await _next(tester);
      expect(find.byKey(const Key('levelup-choice-asi')), findsOneWidget);
      expect(find.text('Puntos repartidos: 0 de 2'), findsOneWidget);

      await _tapKey(tester, 'levelup-asi-str-plus');
      expect(find.text('19 → 20'), findsOneWidget);
      expect(find.byKey(const Key('levelup-asi-cap-str')), findsOneWidget);
      // Str is at 20: a second point there is not possible.
      await _tapKey(tester, 'levelup-asi-str-plus');
      expect(find.text('Puntos repartidos: 1 de 2'), findsOneWidget);

      await _next(tester);
      expect(_error('Reparte 2 puntos (ninguna puede pasar de 20)'), findsOneWidget);

      await _tapKey(tester, 'levelup-asi-dex-plus');
      expect(find.text('Puntos repartidos: 2 de 2'), findsOneWidget);
      expect(find.text('15 → 16'), findsOneWidget);
      // Every point is spent.
      await _tapKey(tester, 'levelup-asi-con-plus');
      expect(find.text('Puntos repartidos: 2 de 2'), findsOneWidget);

      await _next(tester);
      expect(find.byKey(const Key('levelup-review')), findsOneWidget);
      expect(find.text('+1 Fue, +1 Des'), findsOneWidget);
    });

    testWidgets('Conjuros con filtro de escuela: muestra el motivo de los no elegibles', (
      tester,
    ) async {
      const reason = 'Solo abjuración o evocación salvo en los niveles 3, 8, 14 y 20';
      await _pump(
        tester,
        plan: _plan(
          targetLevel: 4,
          classLevel: 4,
          choices: [
            _choice(
              'conjuros',
              'Conjuros rúnicos',
              'SpellsKnown',
              options: [
                _option('runas-ejemplo-proyectil', 'Proyectil rúnico', spellLevel: 1),
                _option(
                  'runas-ejemplo-encanto',
                  'Encanto rúnico',
                  spellLevel: 1,
                  eligible: false,
                  reason: reason,
                ),
              ],
            ),
          ],
        ),
        character: _character(pendingLevelUpTo: 4),
      );
      await _next(tester);
      await _writeHp(tester, '5');
      await _next(tester);

      expect(find.byKey(const Key('levelup-reason-runas-ejemplo-encanto')), findsOneWidget);
      expect(find.text(reason), findsOneWidget);
      expect(find.byKey(const Key('levelup-reason-runas-ejemplo-proyectil')), findsNothing);
    });

    testWidgets('Dote: muestra el motivo de las no elegibles y pide la característica', (
      tester,
    ) async {
      final (:characters, router: _) = await _pump(
        tester,
        plan: _improvementPlan(),
        character: _character(pendingLevelUpTo: 4),
      );
      await _next(tester);
      await _writeHp(tester, '5');
      await _next(tester);

      await _tapKey(tester, 'levelup-tab-feat');
      expect(find.byKey(const Key('levelup-reason-grappler')), findsOneWidget);
      expect(find.text('Requiere Fuerza 13.'), findsOneWidget);
      expect(find.text('PG máx 28 → 32'), findsOneWidget);

      await _tapKey(tester, 'levelup-option-asi-grappler');
      await _next(tester);
      expect(_error('Elige una dote'), findsOneWidget);

      await _tapKey(tester, 'levelup-option-asi-sturdy');
      expect(find.byKey(const Key('levelup-feat-ability-str')), findsOneWidget);
      await _next(tester);
      expect(_error('Elige la característica que sube Sturdy'), findsOneWidget);

      await _tapKey(tester, 'levelup-feat-ability-con');
      await _next(tester);
      expect(find.text('Sturdy (+1 Con)'), findsOneWidget);

      await _tapKey(tester, 'levelup-confirm');
      expect(characters.levelUpBodies.single, {
        'classIndex': 'fighter',
        'hitPointsRolled': 5,
        'choices': [
          {
            'key': 'asi',
            'selected': {'feat': 'sturdy', 'ability': 'con'},
          },
        ],
      });
    });

    testWidgets('envía el cuerpo esperado, celebra y vuelve a Mi sesión', (tester) async {
      final (:characters, :router) = await _pump(
        tester,
        plan: _archetypePlan(),
        location: '/campaigns/c1/player',
      );
      // The granted level opens the wizard by itself and cannot be skipped.
      expect(locationOf(router), '/characters/ch1/level-up');

      await _next(tester);
      await _writeHp(tester, '7');
      await _next(tester);
      await _tapKey(tester, 'levelup-option-subclass-tactician');
      expect(find.byKey(const Key('levelup-dot-choice-maneuvers')), findsOneWidget);
      await _tapKey(tester, 'levelup-option-subclass-champion');
      await _next(tester);
      expect(find.byKey(const Key('levelup-review')), findsOneWidget);

      await _tapKey(tester, 'levelup-confirm');
      expect(characters.levelUpBodies.single, {
        'classIndex': 'fighter',
        'hitPointsRolled': 7,
        'choices': [
          {
            'key': 'subclass',
            'selected': ['champion'],
          },
        ],
      });
      expect(find.byKey(const Key('level-up-celebration')), findsOneWidget);
      expect(find.byKey(const Key('level-up-number')), findsOneWidget);

      await _tapKey(tester, 'level-up-dismiss');
      expect(find.byKey(const Key('level-up-celebration')), findsNothing);
      expect(locationOf(router), '/campaigns/c1/player');
      // The refreshed character no longer has a pending level.
      expect(find.byKey(const Key('level-up-card')), findsNothing);
    });

    testWidgets('un 409 muestra el mensaje del servidor y se queda en el resumen', (tester) async {
      final (:characters, router: _) = await _pump(tester, plan: _archetypePlan());
      characters.levelUpError = _problem(409, 'El DM no te ha concedido ningún nivel.');
      await _next(tester);
      await _writeHp(tester, '4');
      await _next(tester);
      await _tapKey(tester, 'levelup-option-subclass-champion');
      await _next(tester);

      await _tapKey(tester, 'levelup-confirm');
      expect(characters.levelUpBodies, hasLength(1));
      expect(find.byKey(const Key('level-up-celebration')), findsNothing);
      expect(find.byKey(const Key('levelup-review')), findsOneWidget);
      expect(
        find.descendant(
          of: find.byKey(const Key('levelup-submit-error')),
          matching: find.text('El DM no te ha concedido ningún nivel.'),
          matchRoot: true,
        ),
        findsOneWidget,
      );
    });

    testWidgets('sin nivel concedido el plan muestra el error', (tester) async {
      final characters = FakeCharactersRepository(characters: [_character()]);
      characters.levelUpPlanError = _problem(409, 'No tienes ningún nivel pendiente.');
      await pumpRealApp(
        tester,
        location: '/characters/ch1/level-up',
        fakes: AppFakes(characters: characters),
      );
      expect(find.byKey(const Key('levelup-load-error')), findsOneWidget);
      expect(find.text('No tienes ningún nivel pendiente.'), findsOneWidget);
    });
  });

  group('Hoja · Elecciones', () {
    testWidgets('Rasgos lista las dotes con su texto, nivel y característica', (tester) async {
      final characters = FakeCharactersRepository(
        characters: [
          _character(pendingLevelUpTo: null)
            ..['feats'] = [
              {
                'index': 'grappler',
                'name': 'Grappler',
                'description': ['You have developed the skills necessary to hold your own.'],
                'prerequisitesText': 'Strength 13 or higher',
                'ability': null,
                'level': 4,
                'classIndex': 'fighter',
              },
              {
                'index': 'pack-feat-athlete',
                'name': 'Athlete',
                'description': <Object>[],
                'ability': 'str',
                'level': 0,
                'classIndex': null,
              },
            ],
        ],
      );
      await pumpRealApp(
        tester,
        location: '/characters/ch1',
        fakes: AppFakes(characters: characters),
      );
      await _tapKey(tester, 'tab-traits');

      final section = find.byKey(const Key('sheet-feats'));
      await tester.scrollUntilVisible(
        section,
        200,
        scrollable: find.descendant(
          of: find.byKey(const Key('sheet-tab-list')),
          matching: find.byType(Scrollable),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.descendant(of: section, matching: find.text('Dotes')), findsOneWidget);
      expect(find.descendant(of: section, matching: find.text('Grappler')), findsOneWidget);
      expect(find.descendant(of: section, matching: find.text('Nivel 4')), findsOneWidget);
      expect(
        find.descendant(of: section, matching: find.text('Raza o trasfondo · +1 Fue')),
        findsOneWidget,
      );

      await _tap(tester, find.text('Grappler'));
      expect(find.text('Requisito: Strength 13 or higher'), findsOneWidget);
      expect(
        find.text('You have developed the skills necessary to hold your own.'),
        findsOneWidget,
      );

      await _tap(tester, find.text('Athlete'));
      expect(find.text('Esta dote ya no está en el catálogo.'), findsOneWidget);
    });

    testWidgets('lista las elecciones por nivel con los nombres elegidos', (tester) async {
      final characters = FakeCharactersRepository(
        characters: [
          _character(
            pendingLevelUpTo: null,
            choices: [
              {
                'level': 4,
                'classIndex': 'fighter',
                'key': 'asi',
                'name': 'Ability Score Improvement',
                'kind': 'AsiOrFeat',
                'selected': <Object>[],
                'replaced': <Object>[],
                'asi': {'str': 1, 'dex': 1},
              },
              {
                'level': 1,
                'classIndex': 'fighter',
                'key': 'fighting-style',
                'name': 'Fighting Style',
                'kind': 'OptionSet',
                'selected': [
                  {'index': 'defense', 'name': 'Defense'},
                ],
                'replaced': <Object>[],
              },
            ],
          ),
        ],
      );
      await pumpRealApp(
        tester,
        location: '/characters/ch1',
        fakes: AppFakes(characters: characters),
      );
      await _tapKey(tester, 'tab-traits');

      final section = find.byKey(const Key('sheet-choices'));
      expect(section, findsOneWidget);
      expect(find.descendant(of: section, matching: find.text('Nivel 1')), findsOneWidget);
      expect(find.descendant(of: section, matching: find.text('Defense')), findsOneWidget);
      expect(find.descendant(of: section, matching: find.text('Nivel 4')), findsOneWidget);
      expect(find.descendant(of: section, matching: find.text('+1 Fue, +1 Des')), findsOneWidget);
    });
  });

  group('Fase 19: habilidad de multiclase y sustituciones', () {
    Map<String, dynamic> multiclassPlan() => _plan(
      choices: [
        _choice(
          'multiclass-skill',
          'Habilidad de multiclase',
          'Skill',
          options: [
            _option('stealth', 'Sigilo'),
            _option('acrobatics', 'Acrobacias'),
            _option('perception', 'Percepción', eligible: false, reason: 'Ya la tienes.'),
          ],
        )..['note'] = 'Al entrar en Pícaro como multiclase ganas una habilidad de su lista.',
      ],
    );

    testWidgets('la elección multiclass-skill sale sola con tarjetas y se envía', (tester) async {
      final (:characters, router: _) = await _pump(tester, plan: multiclassPlan());
      await _next(tester);
      await _writeHp(tester, '6');
      await _next(tester);

      expect(find.byKey(const Key('levelup-choice-multiclass-skill')), findsOneWidget);
      expect(find.text('Habilidad de multiclase'), findsWidgets);
      expect(
        find.text('Al entrar en Pícaro como multiclase ganas una habilidad de su lista.'),
        findsOneWidget,
      );
      // The cards are the same as every other choice; an ineligible skill is inert.
      await _tapKey(tester, 'levelup-option-multiclass-skill-perception');
      expect(find.text('0 de 1'), findsOneWidget);
      expect(find.text('Ya la tienes.'), findsOneWidget);

      await _next(tester);
      expect(_error('Elige 1 opción'), findsOneWidget);
      await _tapKey(tester, 'levelup-option-multiclass-skill-stealth');
      await _next(tester);
      await _tapKey(tester, 'levelup-confirm');
      await tester.pump(const Duration(seconds: 3));
      await tester.pumpAndSettle();

      expect(characters.levelUpBodies.single['choices'], [
        {
          'key': 'multiclass-skill',
          'selected': ['stealth'],
        },
      ]);
    });

    testWidgets('una sustitución de dote inválida solo ofrece dotes y se responde con la dote', (
      tester,
    ) async {
      final plan = _plan(
        choices: [
          _choice(
            'replace.grappler',
            'Sustituir «Grappler»',
            'AsiOrFeat',
            replaces: true,
            options: [
              _option(
                'sturdy',
                'Sturdy',
                abilityIncrease: {
                  'amount': 1,
                  'from': ['str', 'con'],
                },
              ),
            ],
            known: [
              {'index': 'grappler', 'name': 'Grappler'},
            ],
          )..['note'] = 'Ya no cumples sus requisitos: Requiere Fuerza 13. Elige otra opción.',
        ],
      );
      final (:characters, router: _) = await _pump(tester, plan: plan);
      await _next(tester);
      await _writeHp(tester, '6');
      await _next(tester);

      expect(find.byKey(const Key('levelup-choice-replace.grappler')), findsOneWidget);
      // No ability score improvement tab: only the replacement feat.
      expect(find.byKey(const Key('levelup-tab-asi')), findsNothing);
      expect(find.byKey(const Key('levelup-asi-total')), findsNothing);
      expect(find.byKey(const Key('levelup-replace-replace.grappler-grappler')), findsNothing);

      await _next(tester);
      expect(_error('Elige una dote'), findsOneWidget);
      await _tapKey(tester, 'levelup-option-replace.grappler-sturdy');
      await _tapKey(tester, 'levelup-feat-ability-con');
      await _next(tester);
      await _tapKey(tester, 'levelup-confirm');
      await tester.pump(const Duration(seconds: 3));
      await tester.pumpAndSettle();

      expect(characters.levelUpBodies.single['choices'], [
        {
          'key': 'replace.grappler',
          'selected': {'feat': 'sturdy', 'ability': 'con'},
        },
      ]);
    });

    testWidgets('una sustitución de opción no ofrece "Sustituir uno conocido"', (tester) async {
      final plan = _plan(
        choices: [
          _choice(
            'replace.dueling',
            'Sustituir «Dueling»',
            'OptionSet',
            replaces: true,
            options: [_option('defense', 'Defense')],
            known: [
              {'index': 'dueling', 'name': 'Dueling'},
            ],
          ),
        ],
      );
      final (:characters, router: _) = await _pump(tester, plan: plan);
      await _next(tester);
      await _writeHp(tester, '6');
      await _next(tester);

      expect(find.text('Sustituir uno conocido'), findsNothing);
      await _tapKey(tester, 'levelup-option-replace.dueling-defense');
      await _next(tester);
      await _tapKey(tester, 'levelup-confirm');
      await tester.pump(const Duration(seconds: 3));
      await tester.pumpAndSettle();
      expect(characters.levelUpBodies.single['choices'], [
        {
          'key': 'replace.dueling',
          'selected': ['defense'],
        },
      ]);
    });
  });
}
