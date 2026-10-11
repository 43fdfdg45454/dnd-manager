import 'package:flutter/material.dart' hide Page;
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/theme/app_theme.dart';
import 'package:opentrpg_core/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg_dnd5e/catalog/data/models.dart';
import 'package:opentrpg_dnd5e/characters/data/character_wizard_controller.dart';
import 'package:opentrpg_dnd5e/characters/models.dart';
import 'package:opentrpg_dnd5e/characters/ui/level_up/character_choices_section.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

// Level-1 choices of the first class (fighting style, expertise...), asked in
// the origin step of the creation wizard with "class." keys. Fictitious data.

const _fighter = ClassDetail(
  index: 'fighter',
  name: 'Fighter',
  hitDie: 10,
  savingThrows: ['str', 'con'],
  skillChoices: SkillChoices(choose: 2, from: ['athletics', 'perception', 'survival']),
  levels: [ClassLevel(level: 1)],
  hasLevelOneChoices: true,
);

FakeCatalogRepository _catalog({bool classAsks = true}) => FakeCatalogRepository(
  classList: const [ClassSummary(index: 'fighter', name: 'Fighter', hitDie: 10)],
  classDetails: {
    'fighter': classAsks
        ? _fighter
        : const ClassDetail(
            index: 'fighter',
            name: 'Fighter',
            hitDie: 10,
            savingThrows: ['str', 'con'],
            skillChoices: SkillChoices(choose: 2, from: ['athletics', 'perception', 'survival']),
            levels: [ClassLevel(level: 1)],
          ),
  },
  raceList: const [
    RaceSummary(index: 'human', name: 'Human', speed: 30),
    RaceSummary(index: 'half-elf', name: 'Half-Elf', speed: 30),
  ],
  raceDetails: {
    'human': const RaceDetail(index: 'human', name: 'Human', speed: 30, languages: ['Common']),
    'half-elf': const RaceDetail(
      index: 'half-elf',
      name: 'Half-Elf',
      speed: 30,
      languages: ['Common', 'Elvish'],
      choices: OriginChoiceSpec(kinds: {'skills'}),
    ),
  },
  backgroundList: const [Background(index: 'acolyte', name: 'Acolyte')],
);

/// A style choice whose "Estilo de ejemplo con arma" option asks for a weapon
/// (options[].choices), and a class tool of any kind.
List<Map<String, dynamic>> _classChoices() => [
  makeOriginChoiceJson(
    key: 'class.fighting-style',
    name: 'Fighting Style',
    kind: 'OptionSet',
    source: 'class',
    classIndex: 'fighter',
    level: 1,
    setId: 'fighting-styles',
    note: 'Un estilo de combate.',
    options: [
      {
        'index': 'estilo-defensa',
        'name': 'Estilo de ejemplo defensivo',
        'eligible': true,
        'description': ['+1 a la CA con armadura.'],
      },
      {
        'index': 'estilo-arma',
        'name': 'Estilo de ejemplo con arma',
        'eligible': true,
        'description': ['Elige un arma.'],
      },
    ],
  ),
  makeOriginChoiceJson(
    key: 'class.estilo-arma-weapon',
    name: 'Arma del estilo',
    kind: 'Weapon',
    source: 'class',
    classIndex: 'fighter',
    level: 1,
    requires: {'choiceKey': 'class.fighting-style', 'index': 'estilo-arma'},
    options: [
      {'index': 'longsword', 'name': 'Longsword', 'eligible': true, 'description': <String>[]},
    ],
  ),
  makeOriginChoiceJson(
    key: 'class.gaming-set',
    name: 'Juego de ejemplo',
    kind: 'Tool',
    source: 'class',
    classIndex: 'fighter',
    level: 1,
    freeText: true,
    options: const [],
  ),
];

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

/// Opens the wizard and goes up to the background step with a fighter.
Future<FakeCharactersRepository> _toBackground(
  WidgetTester tester, {
  required List<Map<String, dynamic>> choices,
  String race = 'human',
  bool classAsks = true,
}) async {
  final characters = FakeCharactersRepository()..originPlan = makeOriginChoicesJson(choices);
  await pumpRealApp(
    tester,
    location: '/campaigns/c1/characters/new',
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
      characters: characters,
      catalog: _catalog(classAsks: classAsks),
    ),
  );
  await tester.enterText(find.byKey(const Key('wizard-name')), 'Brunhild');
  await tester.pumpAndSettle();
  await _next(tester);
  await _tap(tester, find.byKey(Key('race-$race')));
  await _next(tester);
  await _tap(tester, find.byKey(const Key('class-fighter')));
  await _next(tester);
  await _next(tester);
  await _tap(tester, find.byKey(const Key('wizard-background')));
  await _tap(tester, find.text('Acolyte').last);
  await _tap(tester, find.byKey(const Key('skill-athletics')));
  await _tap(tester, find.byKey(const Key('skill-survival')));
  return characters;
}

void main() {
  group('modelo', () {
    test('una elección de clase lleva su clase, nivel, conjunto y requisito', () {
      final choice = OriginChoice.fromJson(
        makeOriginChoiceJson(
          key: 'class.expertise',
          name: 'Expertise',
          kind: 'Expertise',
          source: 'class',
          classIndex: 'rogue',
          level: 1,
          setId: 'conjunto',
          requires: {'choiceKey': 'class.padre', 'index': 'opcion'},
          warning: 'Solo 1 de 2.',
        ),
      );
      expect(choice.isClassChoice, isTrue);
      expect(choice.kind, OriginChoiceKind.traitOption, reason: 'una lista de opciones');
      expect(choice.kindName, 'Expertise');
      expect((choice.classIndex, choice.level, choice.setId), ('rogue', 1, 'conjunto'));
      expect(choice.requires, (choiceKey: 'class.padre', index: 'opcion'));
      expect(choice.warning, 'Solo 1 de 2.');

      final tool = OriginChoice.fromJson(makeOriginChoiceJson(kind: 'Tool', source: 'class'));
      expect(tool.kind, OriginChoiceKind.tool);
      final race = OriginChoice.fromJson(makeOriginChoiceJson());
      expect((race.isClassChoice, race.classIndex, race.requires), (false, null, null));
    });

    test('la clase dice si pide elecciones a nivel 1', () {
      expect(
        ClassDetail.fromJson({
          'index': 'fighter',
          'name': 'Fighter',
          'levels': [
            {'level': 1},
          ],
          'hasLevelOneChoices': true,
        }).hasLevelOneChoices,
        isTrue,
      );
      expect(
        ClassDetail.fromJson({
          'index': 'wizard',
          'name': 'Wizard',
          'levels': [
            {'level': 1},
          ],
        }).hasLevelOneChoices,
        isFalse,
      );
    });

    test('el paso de origen aparece por la clase y cambia de título', () {
      const plain = WizardState();
      expect(plain.needsOriginStep, isFalse);
      final fighter = plain.copyWith(classDetail: _fighter);
      expect(fighter.needsOriginStep, isTrue);
      expect(fighter.steps, contains(WizardStep.origin));
      expect(fighter.originStepTitle, 'Tu clase');
      expect(fighter.originStepLabel, 'Elecciones de clase');

      final both = fighter.copyWith(
        race: const RaceDetail(
          index: 'half-elf',
          name: 'Half-Elf',
          speed: 30,
          choices: OriginChoiceSpec(kinds: {'skills'}),
        ),
      );
      expect(both.originStepTitle, 'Tu origen y tu clase');
      expect(both.originStepLabel, 'Elecciones de origen y clase');
    });

    test('una elección de clase de idioma se pide en el paso y la de una opción espera', () {
      final state = WizardState(
        originPlan: OriginChoices.fromJson(
          makeOriginChoicesJson([
            ..._classChoices(),
            makeOriginChoiceJson(
              key: 'class.idioma',
              name: 'Idioma de ejemplo',
              kind: 'Language',
              source: 'class',
              freeText: true,
              options: const [],
            ),
            makeOriginChoiceJson(key: 'race.languages', kind: 'Language', required: 0),
          ]),
        ),
      );
      expect(state.originChoices.map((c) => c.key), [
        'class.fighting-style',
        'class.estilo-arma-weapon',
        'class.gaming-set',
        'class.idioma',
      ]);
      expect(
        state.visibleOriginChoices.map((c) => c.key),
        isNot(contains('class.estilo-arma-weapon')),
      );

      final picked = state.copyWith(
        originAnswers: {
          'class.fighting-style': const OriginAnswer(picks: ['estilo-arma']),
          'class.estilo-arma-weapon': const OriginAnswer(picks: ['longsword']),
        },
      );
      expect(picked.visibleOriginChoices.map((c) => c.key), contains('class.estilo-arma-weapon'));

      // Back to the other style: the weapon is no longer asked and is cleared when saving.
      final changed = picked.copyWith(
        originAnswers: {
          ...picked.originAnswers,
          'class.fighting-style': const OriginAnswer(picks: ['estilo-defensa']),
        },
      );
      final answers = {for (final a in changed.toOriginAnswers()) a.key: a.selected};
      expect(answers['class.fighting-style'], ['estilo-defensa']);
      expect(answers['class.estilo-arma-weapon'], isEmpty);
    });
  });

  group('asistente de creación', () {
    testWidgets('un guerrero humano elige su estilo en el paso «Tu clase»', (tester) async {
      final characters = await _toBackground(tester, choices: _classChoices());
      await _next(tester);

      expect(find.byKey(const Key('step-origin')), findsOneWidget);
      expect(find.text('Paso 7 de 9 · Elecciones de clase'), findsOneWidget);
      expect(find.text('Tu clase'), findsOneWidget);
      expect(find.text('Fighting Style'), findsOneWidget);
      expect(find.text('Clase · nivel 1'), findsWidgets);
      // The weapon of the style waits for that style.
      expect(find.byKey(const Key('origin-choice-class.estilo-arma-weapon')), findsNothing);

      await _next(tester);
      expect(find.text('Fighting Style: falta 1 elección'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('origin-option-class.fighting-style-estilo-arma')));
      expect(find.byKey(const Key('origin-choice-class.estilo-arma-weapon')), findsOneWidget);
      await _next(tester);
      expect(find.text('Arma del estilo: falta 1 elección'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('origin-option-class.estilo-arma-weapon-longsword')));
      await tester.enterText(find.byKey(const Key('origin-text-class.gaming-set-0')), 'Dados');
      await tester.pumpAndSettle();
      expect(find.text('Valor 1'), findsNothing, reason: 'una herramienta se rotula como tal');
      await _next(tester);

      expect(find.byKey(const Key('step-equipment')), findsOneWidget);
      expect(characters.originSaves.single, [
        {
          'key': 'class.fighting-style',
          'selected': ['estilo-arma'],
        },
        {
          'key': 'class.estilo-arma-weapon',
          'selected': ['longsword'],
        },
        {
          'key': 'class.gaming-set',
          'selected': ['Dados'],
        },
      ]);
    });

    testWidgets('con raza y clase el paso se titula «Tu origen y tu clase»', (tester) async {
      await _toBackground(
        tester,
        race: 'half-elf',
        choices: [makeOriginChoiceJson(), ..._classChoices().take(1)],
      );
      await _next(tester);
      expect(find.text('Paso 7 de 9 · Elecciones de origen y clase'), findsOneWidget);
      expect(find.text('Tu origen y tu clase'), findsOneWidget);
      expect(find.text('Habilidades (raza)'), findsOneWidget);
      expect(find.text('Fighting Style'), findsOneWidget);
    });

    testWidgets('una clase sin elecciones de nivel 1 no añade el paso', (tester) async {
      final characters = await _toBackground(tester, choices: const [], classAsks: false);
      await _next(tester);
      expect(find.byKey(const Key('step-origin')), findsNothing);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);
      expect(characters.created, isEmpty);
    });
  });

  group('hoja', () {
    testWidgets('las elecciones de nivel 1 de la clase salen con las de nivel 1', (tester) async {
      final json = makeCharacterJson(status: 'Active')
        ..['choices'] = [
          {
            'level': 0,
            'classIndex': '',
            'key': 'race.skills',
            'name': 'Habilidades (raza)',
            'kind': 'Skill',
            'selected': [
              {'index': 'insight', 'name': 'Perspicacia'},
            ],
          },
          {
            'level': 1,
            'classIndex': 'fighter',
            'key': 'fighting-style',
            'name': 'Fighting Style',
            'kind': 'OptionSet',
            'selected': [
              {'index': 'estilo-defensa', 'name': 'Estilo de ejemplo defensivo'},
            ],
          },
        ];
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light(),
          home: Scaffold(body: CharacterChoicesSection(character: CharacterDetail.fromJson(json))),
        ),
      );
      expect(find.text('Raza y trasfondo'), findsOneWidget);
      expect(find.text('Nivel 1'), findsOneWidget);
      expect(find.textContaining('Estilo de ejemplo defensivo'), findsOneWidget);
    });
  });
}
