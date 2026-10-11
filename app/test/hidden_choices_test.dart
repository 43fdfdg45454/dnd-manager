import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/network/api_error.dart';
import 'package:opentrpg_core/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg_dnd5e/catalog/data/models.dart';
import 'package:opentrpg_dnd5e/characters/data/character_wizard_controller.dart';
import 'package:opentrpg_dnd5e/characters/data/level_up_controller.dart';
import 'package:opentrpg_dnd5e/characters/models.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

// Nothing the player cannot see is ever sent: a pick of an option that is no
// longer offered or whose requirement is not met, or the answer of a hidden
// choice, leaves the state as soon as it becomes invisible and never reaches
// the server. Fictitious data.

const _args = (campaignId: 'c1', ownerUserId: null);

Map<String, dynamic> _opt(String index, String name, {Map<String, String>? requires}) => {
  'index': index,
  'name': name,
  'eligible': true,
  'description': <String>[],
  'requires': ?requires,
};

/// A skill choice of the class and an expertise whose options are the skills
/// the character has: [expertise] lists the offered ones; the option of each
/// skill still to be picked at level 1 requires that pick.
List<Map<String, dynamic>> _classPlan({
  List<String> expertise = const ['athletics', 'arcana'],
  List<String> gated = const ['stealth'],
}) => [
  makeOriginChoiceJson(
    key: 'class.skills',
    name: 'Habilidades (clase)',
    kind: 'Skill',
    source: 'class',
    classIndex: 'rogue',
    level: 1,
    options: [_opt('stealth', 'Stealth'), _opt('deception', 'Deception')],
  ),
  makeOriginChoiceJson(
    key: 'class.expertise',
    name: 'Pericia',
    kind: 'Expertise',
    source: 'class',
    classIndex: 'rogue',
    level: 1,
    options: [
      for (final s in expertise) _opt(s, s),
      for (final s in gated) _opt(s, s, requires: {'choiceKey': 'class.skills', 'index': s}),
    ],
  ),
  makeOriginChoiceJson(
    key: 'class.style',
    name: 'Estilo',
    kind: 'OptionSet',
    source: 'class',
    classIndex: 'rogue',
    level: 1,
    options: [_opt('estilo-arma', 'Con arma'), _opt('estilo-defensa', 'Defensivo')],
  ),
  makeOriginChoiceJson(
    key: 'class.style-weapon',
    name: 'Arma del estilo',
    kind: 'Weapon',
    source: 'class',
    classIndex: 'rogue',
    level: 1,
    requires: {'choiceKey': 'class.style', 'index': 'estilo-arma'},
    options: [_opt('longsword', 'Longsword')],
  ),
];

const _rogue = ClassDetail(
  index: 'rogue',
  name: 'Rogue',
  hitDie: 8,
  savingThrows: ['dex', 'int'],
  skillChoices: SkillChoices(choose: 1, from: ['stealth', 'deception']),
  levels: [ClassLevel(level: 1)],
  hasLevelOneChoices: true,
);

Map<String, List<String>> _picks(WizardState state) => {
  for (final a in state.toOriginAnswers()) a.key: a.selected,
};

// ---------------------------------------------------------------------------
// Level-up helpers
// ---------------------------------------------------------------------------

Map<String, dynamic> _luChoice(
  String key,
  String name,
  String kind, {
  int choose = 1,
  String? subclassIndex,
  Map<String, String>? requires,
  List<Map<String, dynamic>> options = const [],
  String note = '',
}) => {
  'key': key,
  'name': name,
  'kind': kind,
  'choose': choose,
  'required': choose,
  'replaces': false,
  'cumulative': false,
  'note': note,
  'subclassIndex': subclassIndex,
  'setId': null,
  'freeText': false,
  'options': options,
  'known': const <Map<String, dynamic>>[],
  'requires': ?requires,
};

Map<String, dynamic> _luPlan(List<Map<String, dynamic>> choices) => {
  'characterId': 'ch1',
  'currentLevel': 2,
  'targetLevel': 3,
  'classIndex': 'rogue',
  'classLevel': 3,
  'hitDie': 8,
  'conModifier': 1,
  'classes': [
    {
      'classIndex': 'rogue',
      'name': 'Rogue',
      'allowed': true,
      'reason': null,
      'hitDie': 8,
      'isNew': false,
      'currentLevel': 2,
      'subclassIndex': null,
    },
  ],
  'automaticFeatures': const <Map<String, dynamic>>[],
  'choices': choices,
  'spellcasting': null,
};

List<Map<String, dynamic>> _luChoices() => [
  _luChoice('skills', 'Habilidad', 'Skill', options: [_opt('arcana', 'Arcana')]),
  _luChoice(
    'expertise',
    'Pericia',
    'Expertise',
    options: [
      _opt('athletics', 'Athletics'),
      _opt('arcana', 'Arcana', requires: {'choiceKey': 'skills', 'index': 'arcana'}),
    ],
  ),
  _luChoice(
    'style',
    'Estilo',
    'OptionSet',
    options: [_opt('estilo-arma', 'Con arma'), _opt('estilo-defensa', 'Defensivo')],
  ),
  _luChoice(
    'style-weapon',
    'Arma del estilo',
    'Weapon',
    requires: {'choiceKey': 'style', 'index': 'estilo-arma'},
    options: [_opt('longsword', 'Longsword')],
  ),
];

// ---------------------------------------------------------------------------
// Proficient kits
// ---------------------------------------------------------------------------

const _toolsCategory = EquipmentCategory(
  index: 'artisans-tools',
  name: "Artisan's Tools",
  items: [
    EquipmentCategoryItem(templateId: 't-brewer', index: 'brewers-supplies', name: "Brewer's"),
    EquipmentCategoryItem(templateId: 't-smith', index: 'smiths-tools', name: "Smith's"),
  ],
);

const _kitPick = StartingCategoryPick(
  category: 'artisans-tools',
  name: "Artisan's tools",
  proficientOnly: true,
);

const _kitEquipment = StartingEquipment(
  choices: [
    StartingEquipmentChoice(
      description: 'Un kit de artesano',
      options: [
        StartingEquipmentOption(label: 'Un kit de artesano', categories: [_kitPick]),
      ],
    ),
  ],
);

ClassDetail _fighter({List<String> tools = const []}) => ClassDetail(
  index: 'fighter',
  name: 'Fighter',
  hitDie: 10,
  savingThrows: const ['str', 'con'],
  skillChoices: const SkillChoices(choose: 1, from: ['athletics', 'perception']),
  startingEquipment: _kitEquipment,
  levels: const [ClassLevel(level: 1)],
  toolProficiencies: tools,
);

FakeCatalogRepository _catalog({List<String> tools = const []}) => FakeCatalogRepository(
  classList: const [
    ClassSummary(index: 'fighter', name: 'Fighter', hitDie: 10),
    ClassSummary(index: 'rogue', name: 'Rogue', hitDie: 8),
  ],
  classDetails: {
    'fighter': _fighter(tools: tools),
    'rogue': _rogue,
  },
  raceList: const [RaceSummary(index: 'human', name: 'Human', speed: 30)],
  raceDetails: {
    'human': const RaceDetail(index: 'human', name: 'Human', speed: 30, languages: ['Common']),
  },
  backgroundList: const [Background(index: 'acolyte', name: 'Acolyte')],
  equipmentCategories: const {'artisans-tools': _toolsCategory},
);

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _next(WidgetTester tester) async {
  await _tap(tester, find.byKey(const Key('wizard-next')));
  if (find.byKey(const Key('step-personality')).evaluate().isNotEmpty) {
    await _tap(tester, find.byKey(const Key('wizard-next')));
  }
}

/// Opens the wizard with a human fighter and goes up to the equipment step.
Future<void> _toEquipment(WidgetTester tester, {List<String> tools = const []}) async {
  await pumpRealApp(
    tester,
    location: '/campaigns/c1/characters/new',
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
      characters: FakeCharactersRepository(),
      catalog: _catalog(tools: tools),
    ),
  );
  await tester.enterText(find.byKey(const Key('wizard-name')), 'Brunhild');
  await tester.pumpAndSettle();
  await _next(tester);
  await _tap(tester, find.byKey(const Key('race-human')));
  await _next(tester);
  await _tap(tester, find.byKey(const Key('class-fighter')));
  await _next(tester);
  await _next(tester);
  await _tap(tester, find.byKey(const Key('wizard-background')));
  await _tap(tester, find.text('Acolyte').last);
  await _tap(tester, find.byKey(const Key('skill-athletics')));
  await _next(tester);
  expect(find.byKey(const Key('step-equipment')), findsOneWidget);
}

void main() {
  group('asistente de creación: lo oculto no se envía', () {
    test('una pericia en una habilidad que ya no se ofrece se descarta', () {
      final state = WizardState(
        originPlan: OriginChoices.fromJson(makeOriginChoicesJson(_classPlan())),
        originAnswers: {
          'class.expertise': const OriginAnswer(picks: ['arcana']),
          'class.style': const OriginAnswer(picks: ['estilo-defensa']),
        },
      );
      expect(_picks(state)['class.expertise'], ['arcana']);

      // The skills changed: the plan now offers other skills for the expertise.
      final replanned = state.copyWith(
        originPlan: OriginChoices.fromJson(
          makeOriginChoicesJson(_classPlan(expertise: ['athletics', 'history'])),
        ),
      );
      expect(replanned.prunedOriginAnswers['class.expertise']?.picks, isEmpty);
      expect(_picks(replanned)['class.expertise'], isEmpty);
      expect(_picks(replanned)['class.style'], ['estilo-defensa'], reason: 'lo visible sigue');
    });

    test('una opción con requisito y una elección con requisito se descartan juntas', () {
      final state = WizardState(
        originPlan: OriginChoices.fromJson(makeOriginChoicesJson(_classPlan())),
        originAnswers: {
          'class.skills': const OriginAnswer(picks: ['stealth']),
          'class.expertise': const OriginAnswer(picks: ['stealth']),
          'class.style': const OriginAnswer(picks: ['estilo-arma']),
          'class.style-weapon': const OriginAnswer(picks: ['longsword']),
        },
      );
      expect(_picks(state)['class.expertise'], ['stealth']);
      expect(_picks(state)['class.style-weapon'], ['longsword']);

      final changed = state.copyWith(
        originAnswers: {
          ...state.originAnswers,
          'class.skills': const OriginAnswer(picks: ['deception']),
          'class.style': const OriginAnswer(picks: ['estilo-defensa']),
        },
      );
      final pruned = changed.prunedOriginAnswers;
      expect(pruned['class.expertise']?.picks, isEmpty);
      expect(pruned.containsKey('class.style-weapon'), isFalse);
      expect(_picks(changed)['class.expertise'], isEmpty);
      expect(_picks(changed)['class.style-weapon'], isEmpty);
    });

    test('el controlador limpia el estado en cuanto algo deja de verse', () async {
      final characters = FakeCharactersRepository()
        ..originPlan = makeOriginChoicesJson(_classPlan());
      final fakes = AppFakes(
        campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
        characters: characters,
        catalog: _catalog(),
      );
      final container = ProviderContainer(overrides: fakes.overrides);
      addTearDown(container.dispose);
      final provider = characterWizardControllerProvider(_args);
      container.listen(provider, (_, _) {});
      final controller = container.read(provider.notifier);
      WizardState state() => container.read(provider);
      OriginChoice choice(String key) => state().originChoices.firstWhere((c) => c.key == key);

      controller.setName('Brunhild');
      await controller.selectClass('rogue');
      await controller.loadOrigin();
      expect(state().originPlan, isNotNull);

      // The expertise in the skill picked now, and the weapon of the style.
      controller.toggleOriginOption(choice('class.skills'), 'stealth');
      controller.toggleOriginOption(choice('class.expertise'), 'stealth');
      controller.toggleOriginOption(choice('class.style'), 'estilo-arma');
      controller.toggleOriginOption(choice('class.style-weapon'), 'longsword');
      expect(state().originAnswers['class.expertise']?.picks, ['stealth']);
      expect(state().originAnswers['class.style-weapon']?.picks, ['longsword']);

      controller.toggleOriginOption(choice('class.skills'), 'stealth');
      expect(state().originAnswers['class.expertise']?.picks, isEmpty);
      controller.toggleOriginOption(choice('class.style'), 'estilo-defensa');
      expect(state().originAnswers.containsKey('class.style-weapon'), isFalse);
      expect(_picks(state())['class.style-weapon'], isEmpty);

      // The plan is loaded again with other expertise options: the stale pick goes.
      controller.toggleOriginOption(choice('class.expertise'), 'arcana');
      characters.originPlan = makeOriginChoicesJson(_classPlan(expertise: ['athletics']));
      await controller.loadOrigin();
      expect(state().originAnswers['class.expertise']?.picks, isEmpty);
      expect(_picks(state())['class.expertise'], isEmpty);
    });
  });

  group('subida de nivel: lo oculto no se envía', () {
    Future<ProviderContainer> load() async {
      final characters = FakeCharactersRepository(
        characters: [makeCharacterJson(id: 'ch1', status: 'Active', pendingLevelUpTo: 3)],
      );
      characters.levelUpPlans[''] = _luPlan(_luChoices());
      final container = ProviderContainer(overrides: [...fakeCharactersOverrides(characters)]);
      addTearDown(container.dispose);
      container.listen(levelUpControllerProvider('ch1'), (_, _) {});
      await pumpEventQueue();
      return container;
    }

    test('la pericia en una habilidad que ya no se elige se descarta', () async {
      final container = await load();
      final provider = levelUpControllerProvider('ch1');
      final controller = container.read(provider.notifier);
      LevelUpState state() => container.read(provider);
      controller.setHitPoints('5');
      controller.toggleOption(state().choiceOf('skills')!, 'arcana');
      controller.toggleOption(state().choiceOf('expertise')!, 'arcana');
      expect(state().selectionOf('expertise').selected, ['arcana']);

      controller.toggleOption(state().choiceOf('skills')!, 'arcana');
      expect(state().selectionOf('expertise').selected, isEmpty);
      final sent = {for (final a in state().request.choices) a.key: a.selected};
      expect(sent['expertise'], isEmpty, reason: 'una elección obligatoria viaja vacía');
    });

    test('la elección de una opción que ya no está elegida se descarta', () async {
      final container = await load();
      final provider = levelUpControllerProvider('ch1');
      final controller = container.read(provider.notifier);
      LevelUpState state() => container.read(provider);
      controller.setHitPoints('5');
      controller.toggleOption(state().choiceOf('style')!, 'estilo-arma');
      controller.toggleOption(state().choiceOf('style-weapon')!, 'longsword');
      expect(state().selectionOf('style-weapon').selected, ['longsword']);

      controller.toggleOption(state().choiceOf('style')!, 'estilo-defensa');
      expect(state().choiceOf('style-weapon'), isNull);
      expect(state().selections.containsKey('style-weapon'), isFalse);
      final sent = {for (final a in state().request.choices) a.key: a.selected};
      expect(sent['style'], ['estilo-defensa']);
      expect(sent.containsKey('style-weapon'), isFalse);
    });
  });

  group('errores del servidor', () {
    test('problemErrors y problemMessage leen el detalle y los errores por campo', () {
      final error = dioError(
        400,
        data: {
          'title': 'Datos no válidos.',
          'status': 400,
          'detail': '<arcana> no es una opción de <Pericia>.',
          'errors': {
            'Choices': ['Faltan 2 elecciones.', '<arcana> no es una opción de <Pericia>.'],
            'Name': 'Demasiado largo.',
          },
        },
      );
      expect(problemErrors(error), {
        'Choices': ['Faltan 2 elecciones.', '<arcana> no es una opción de <Pericia>.'],
        'Name': ['Demasiado largo.'],
      });
      expect(
        problemMessage(error),
        '<arcana> no es una opción de <Pericia>.\nFaltan 2 elecciones.\nDemasiado largo.',
      );
      expect(problemMessage(dioError(400, data: {'title': 'x'})), isNull);
      expect(problemMessage(dioError(500)), isNull);
      expect(problemErrors(dioError(null)), isEmpty);

      expect(
        describeLevelUpError(
          dioError(
            400,
            data: {
              'errors': {
                'HitPointsRolled': ['Entre 1 y 8.'],
              },
            },
          ),
        ),
        'Entre 1 y 8.',
      );
      expect(
        describeLevelUpError(dioError(400)),
        'Los datos enviados no son válidos para este personaje.',
      );
    });
  });

  group('kits con competencia', () {
    test('proficientOnly se lee y por defecto es falso', () {
      final pick = StartingCategoryPick.fromJson({
        'category': 'artisans-tools',
        'name': "Artisan's tools",
        'choose': 1,
        'proficientOnly': true,
      });
      expect(pick.proficientOnly, isTrue);
      expect(
        StartingCategoryPick.fromJson({'category': 'martial-weapons', 'name': 'x'}).proficientOnly,
        isFalse,
      );
      final detail = ClassDetail.fromJson({
        'index': 'rogue',
        'name': 'Rogue',
        'levels': [
          {'level': 1},
        ],
        'toolProficiencies': ['thieves-tools'],
      });
      expect(detail.toolProficiencies, ['thieves-tools']);
      expect(
        Background.fromJson({
          'index': 'x',
          'name': 'X',
          'toolProficiencies': ['disguise-kit'],
        }).toolProficiencies,
        ['disguise-kit'],
      );
    });

    test(
      'solo se ofrecen los kits con los que hay competencia (clase, trasfondo y elecciones)',
      () {
        final plain = WizardState(classDetail: _fighter());
        expect(plain.toolProficiencyIndexes, isEmpty);
        expect(plain.offeredCategoryItems(_kitPick, _toolsCategory.items), isEmpty);
        expect(
          plain.offeredCategoryItems(
            const StartingCategoryPick(category: 'artisans-tools', name: 'x'),
            _toolsCategory.items,
          ),
          _toolsCategory.items,
          reason: 'sin proficientOnly se ofrece todo',
        );

        final withClass = plain.copyWith(classDetail: _fighter(tools: ['smiths-tools']));
        expect(withClass.offeredCategoryItems(_kitPick, _toolsCategory.items).map((i) => i.index), [
          'smiths-tools',
        ]);

        final withBackground = plain.copyWith(
          background: const Background(
            index: 'guild',
            name: 'Guild',
            toolProficiencies: ['brewers-supplies'],
          ),
        );
        expect(withBackground.toolProficiencyIndexes, {'brewers-supplies'});

        final withPick = plain.copyWith(
          originPlan: OriginChoices.fromJson(
            makeOriginChoicesJson([
              makeOriginChoiceJson(
                key: 'background.tools',
                kind: 'Tool',
                source: 'background',
                options: [_opt('smiths-tools', "Smith's"), _opt('brewers-supplies', "Brewer's")],
              ),
              makeOriginChoiceJson(
                key: 'race.tools',
                kind: 'Tool',
                freeText: true,
                options: const [],
              ),
            ]),
          ),
          originAnswers: {
            'background.tools': const OriginAnswer(picks: ['smiths-tools']),
            'race.tools': const OriginAnswer(picks: ['brewers-supplies']),
          },
        );
        expect(withPick.toolProficiencyIndexes, {
          'smiths-tools',
        }, reason: 'un texto libre no es índice');
      },
    );

    test('un kit elegido que deja de tener competencia se descarta', () {
      final state = WizardState(
        classDetail: _fighter(),
        background: const Background(
          index: 'guild',
          name: 'Guild',
          toolProficiencies: ['smiths-tools'],
        ),
        equipmentOptions: const {
          0: {0},
        },
        categoryPicks: {
          '0-0-0': [_toolsCategory.items[1]],
        },
      );
      expect(state.isChoiceComplete(0), isTrue);
      expect(state.startingLines.map((l) => l.templateId), ['t-smith']);

      final changed = state.copyWith(
        background: const Background(index: 'acolyte', name: 'Acolyte'),
      );
      expect(changed.prunedCategoryPicks, isEmpty);
      expect(changed.isChoiceComplete(0), isFalse);
      expect(changed.startingLines, isEmpty);
    });

    testWidgets('sin competencia el selector lo dice en lugar de listar', (tester) async {
      await _toEquipment(tester);
      await _tap(tester, find.byKey(const Key('equipment-option-0-0')));
      expect(find.byKey(const Key('equipment-category-picker')), findsOneWidget);
      expect(find.text('No tienes competencia con ningún kit todavía.'), findsOneWidget);
      expect(find.byKey(const Key('equipment-category-item-smiths-tools')), findsNothing);
    });

    testWidgets('con competencia solo se listan esos kits', (tester) async {
      await _toEquipment(tester, tools: ['smiths-tools']);
      await _tap(tester, find.byKey(const Key('equipment-option-0-0')));
      expect(find.byKey(const Key('equipment-category-item-smiths-tools')), findsOneWidget);
      expect(find.byKey(const Key('equipment-category-item-brewers-supplies')), findsNothing);
      expect(find.text('No tienes competencia con ningún kit todavía.'), findsNothing);
    });
  });
}
