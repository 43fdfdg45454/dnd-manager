import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/core/theme/app_theme.dart';
import 'package:dnd_companion/features/characters/data/character_wizard_controller.dart';
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/characters/ui/character_tabs.dart';
import 'package:dnd_companion/features/characters/ui/level_up/character_choices_section.dart';
import 'package:dnd_companion/features/characters/ui/wizard/character_wizard_page.dart';
import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

FakeCatalogRepository _catalog() => FakeCatalogRepository(
  classList: const [ClassSummary(index: 'fighter', name: 'Fighter', hitDie: 10)],
  classDetails: {
    'fighter': const ClassDetail(
      index: 'fighter',
      name: 'Fighter',
      hitDie: 10,
      savingThrows: ['str', 'con'],
      skillChoices: SkillChoices(choose: 2, from: ['athletics', 'perception', 'survival']),
      levels: [ClassLevel(level: 1)],
    ),
  },
  raceList: const [
    RaceSummary(index: 'half-elf', name: 'Half-Elf', speed: 30),
    RaceSummary(index: 'human', name: 'Human', speed: 30),
  ],
  raceDetails: {
    'half-elf': const RaceDetail(
      index: 'half-elf',
      name: 'Half-Elf',
      speed: 30,
      languages: ['Common', 'Elvish'],
      choices: OriginChoiceSpec(kinds: {'abilityBonuses', 'skills', 'languages'}),
    ),
    'human': const RaceDetail(index: 'human', name: 'Human', speed: 30, languages: ['Common']),
  },
  backgroundList: const [
    Background(
      index: 'acolyte',
      name: 'Acolyte',
      choices: OriginChoiceSpec(kinds: {'languages'}),
    ),
  ],
);

List<Map<String, dynamic>> _halfElfChoices({bool withLanguages = true}) => [
  makeOriginChoiceJson(
    key: 'race.abilityBonuses',
    name: 'Mejora de característica (raza)',
    kind: 'AbilityBonus',
    choose: 2,
    amount: 1,
    note: '+1 a 2 características distintas.',
    options: [
      for (final a in const [('str', 'Fuerza'), ('dex', 'Destreza'), ('wis', 'Sabiduría')])
        {'index': a.$1, 'name': a.$2, 'eligible': true, 'description': <String>[]},
    ],
  ),
  makeOriginChoiceJson(),
  if (withLanguages)
    makeOriginChoiceJson(
      key: 'race.languages',
      name: 'Idiomas (raza)',
      kind: 'Language',
      required: 0,
      note: 'Opcional: el asistente de creación ya pide los idiomas en su propio paso.',
      options: [
        {'index': 'Dwarvish', 'name': 'Dwarvish', 'eligible': true, 'description': <String>[]},
      ],
    ),
];

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _next(WidgetTester tester) => _tap(tester, find.byKey(const Key('wizard-next')));

const _args = (campaignId: 'c1', ownerUserId: null);

/// Opens the wizard and goes up to the background step with a half-elf fighter.
Future<FakeCharactersRepository> _toBackground(
  WidgetTester tester, {
  List<Map<String, dynamic>>? choices,
  String race = 'half-elf',
  bool pushed = false,
}) async {
  final characters = FakeCharactersRepository()
    ..originPlan = makeOriginChoicesJson(choices ?? _halfElfChoices());
  final router = await pumpRealApp(
    tester,
    location: pushed ? '/campaigns/c1' : '/campaigns/c1/characters/new',
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
      characters: characters,
      catalog: _catalog(),
    ),
  );
  if (pushed) {
    router.push('/campaigns/c1/characters/new');
    await tester.pumpAndSettle();
  }
  await tester.enterText(find.byKey(const Key('wizard-name')), 'Merlina');
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
  group('elecciones de raza y trasfondo en la creación', () {
    testWidgets('aparece tras el trasfondo, crea el borrador y pide el plan al servidor', (
      tester,
    ) async {
      final characters = await _toBackground(tester);
      expect(characters.created, isEmpty);
      await _next(tester);

      expect(find.byKey(const Key('step-origin')), findsOneWidget);
      expect(find.text('Paso 6 de 8 · Elecciones de raza y trasfondo'), findsOneWidget);
      // The draft carries the identity needed to plan the choices.
      expect(characters.created.single.name, 'Merlina');
      final early = characters.patches.single;
      expect(early.raceIndex, 'half-elf');
      expect(early.classes!.single.classIndex, 'fighter');
      expect(early.proficiencies, isNull);

      expect(find.text('Mejora de característica (raza)'), findsOneWidget);
      expect(find.text('+1 a 2 características distintas.'), findsOneWidget);
      expect(find.byKey(const Key('origin-option-race.abilityBonuses-str')), findsOneWidget);
      expect(find.byKey(const Key('origin-count-race.skills')), findsOneWidget);
    });

    testWidgets('no se avanza sin las obligatorias y se guardan con el PUT', (tester) async {
      final characters = await _toBackground(tester);
      await _next(tester);

      await _next(tester);
      expect(find.byKey(const Key('step-origin')), findsOneWidget);
      expect(
        find.text('Mejora de característica (raza): faltan 2 elecciones'),
        findsOneWidget,
        reason: 'el primer error pendiente se muestra bajo el paso',
      );
      expect(characters.originSaves, isEmpty);

      await _tap(tester, find.byKey(const Key('origin-option-race.abilityBonuses-str')));
      await _tap(tester, find.byKey(const Key('origin-option-race.abilityBonuses-wis')));
      // A third pick is ignored: the choice allows two.
      await _tap(tester, find.byKey(const Key('origin-option-race.abilityBonuses-dex')));
      expect(find.text('2 de 2'), findsOneWidget);
      await _next(tester);
      expect(find.text('Habilidades (raza): falta 1 elección'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('origin-option-race.skills-perception')));
      await _next(tester);

      // Leaving the step saved the answers (and the blank optional ones are not asked).
      expect(characters.originSaves.single, [
        {
          'key': 'race.abilityBonuses',
          'selected': ['str', 'wis'],
        },
        {
          'key': 'race.skills',
          'selected': ['perception'],
        },
      ]);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);
    });

    testWidgets('los idiomas opcionales no se piden aquí (tienen su propio paso)', (tester) async {
      await _toBackground(tester);
      await _next(tester);
      expect(find.byKey(const Key('origin-choice-race.languages')), findsNothing);
      expect(find.text('Idiomas (raza)'), findsNothing);
    });

    testWidgets('una elección opcional se muestra y no bloquea', (tester) async {
      await _toBackground(
        tester,
        choices: [
          makeOriginChoiceJson(
            key: 'background.tools',
            name: 'Herramientas (trasfondo)',
            kind: 'Tool',
            source: 'background',
            choose: 1,
            required: 0,
            freeText: true,
            options: const [],
          ),
        ],
      );
      await _next(tester);

      expect(find.text('Herramientas (trasfondo)'), findsOneWidget);
      expect(find.text('Trasfondo · opcional'), findsOneWidget);
      await _next(tester);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);
    });

    testWidgets('una dote pide la característica que sube cuando hay varias', (tester) async {
      final characters = await _toBackground(
        tester,
        choices: [
          makeOriginChoiceJson(
            key: 'race.feat',
            name: 'Dote (raza)',
            kind: 'Feat',
            options: [
              {
                'index': 'grappler',
                'name': 'Grappler',
                'eligible': true,
                'description': <String>[],
                'abilityIncrease': {
                  'amount': 1,
                  'from': ['str', 'dex'],
                },
              },
              {
                'index': 'tavern-brawler',
                'name': 'Tavern Brawler',
                'eligible': false,
                'reason': 'Requiere Fuerza 13.',
                'description': <String>[],
              },
            ],
          ),
        ],
      );
      await _next(tester);

      expect(find.text('Requiere Fuerza 13.'), findsOneWidget);
      // An ineligible feat cannot be picked.
      await _tap(tester, find.byKey(const Key('origin-option-race.feat-tavern-brawler')));
      expect(find.text('0 de 1'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('origin-option-race.feat-grappler')));
      await _next(tester);
      expect(find.text('Dote (raza): elige la característica que sube Grappler'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('origin-feat-ability-dex')));
      await _next(tester);
      expect(characters.originSaves.single.single, {
        'key': 'race.feat',
        'selected': {'feat': 'grappler', 'ability': 'dex'},
      });
    });

    testWidgets('la ascendencia dracónica muestra la resistencia que concede', (tester) async {
      await _toBackground(
        tester,
        choices: [
          makeOriginChoiceJson(
            key: 'race.trait.draconic-ancestry',
            name: 'Linaje dracónico',
            kind: 'TraitOption',
            options: [
              {
                'index': 'red',
                'name': 'Rojo',
                'eligible': true,
                'description': ['Dragón rojo.'],
                'damageType': 'fire',
              },
            ],
          ),
        ],
      );
      await _next(tester);
      expect(find.textContaining('Resistencia al daño fuego.'), findsOneWidget);
    });

    testWidgets('al enviar se vuelven a guardar las elecciones tras la hoja completa', (
      tester,
    ) async {
      final characters = await _toBackground(
        tester,
        choices: _halfElfChoices(withLanguages: false),
      );
      await _next(tester);
      await _tap(tester, find.byKey(const Key('origin-option-race.abilityBonuses-str')));
      await _tap(tester, find.byKey(const Key('origin-option-race.abilityBonuses-dex')));
      await _tap(tester, find.byKey(const Key('origin-option-race.skills-insight')));
      await _next(tester);
      expect(characters.originSaves, hasLength(1));

      // Equipment, review and submit.
      await _next(tester);
      await _tap(tester, find.byKey(const Key('wizard-submit')));

      expect(characters.created, hasLength(1), reason: 'se reutiliza el borrador');
      expect(characters.patches, hasLength(2));
      expect(characters.patches.last.proficiencies, isNotNull);
      expect(characters.originSaves, hasLength(2));
      expect(characters.originSaves.last.first['selected'], ['str', 'dex']);
    });

    testWidgets('un fallo al guardar mantiene el paso y muestra el motivo', (tester) async {
      final characters = await _toBackground(
        tester,
        choices: _halfElfChoices(withLanguages: false),
      );
      characters.originSaveError = dioError(
        400,
        data: {'detail': 'La habilidad ya la tienes.', 'code': 'x'},
      );
      await _next(tester);
      await _tap(tester, find.byKey(const Key('origin-option-race.abilityBonuses-str')));
      await _tap(tester, find.byKey(const Key('origin-option-race.abilityBonuses-dex')));
      await _tap(tester, find.byKey(const Key('origin-option-race.skills-insight')));
      await _next(tester);

      expect(find.byKey(const Key('step-origin')), findsOneWidget);
      expect(find.text('La habilidad ya la tienes.'), findsOneWidget);
    });

    testWidgets('descartar el asistente borra el borrador creado', (tester) async {
      final characters = await _toBackground(tester, pushed: true);
      await _next(tester);
      expect(characters.created, hasLength(1));

      final state = ProviderScope.containerOf(tester.element(find.byType(CharacterWizardPage)))
          .read(characterWizardControllerProvider(_args));
      expect(state.originChoices.map((c) => c.key), ['race.abilityBonuses', 'race.skills']);

      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('wizard-discard')));
      await tester.pumpAndSettle();
      expect(characters.deleted, ['new1']);
    });

    testWidgets('una raza sin elecciones no añade el paso ni crea borrador', (tester) async {
      final characters = await _toBackground(tester, race: 'human');
      await _next(tester);
      expect(find.byKey(const Key('step-origin')), findsNothing);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);
      expect(characters.created, isEmpty);
    });
  });

  group('hoja: resistencias, arma de aliento y elecciones de origen', () {
    CharacterDetail dragonborn() => CharacterDetail.fromJson(
      makeCharacterJson(
        status: 'Active',
        resistances: [
          {'damageType': 'fire', 'source': 'race', 'label': 'Linaje dracónico (rojo)'},
          {'damageType': 'poison', 'source': 'race', 'label': 'Resistencia enana'},
        ],
        breathWeapon: {
          'name': 'Aliento',
          'source': 'race',
          'damageType': 'fire',
          'dice': '2d6',
          'saveAbility': 'dex',
          'area': 'cono de 15 pies',
          'dc': 13,
        },
        breakdowns: {
          'breathWeapon.dc': makeBreakdownJson([
            ('base', 'Base', 8),
            ('proficiency', 'Competencia', 2),
            ('ability', 'Constitución', 3),
          ]),
        },
      ),
    );

    testWidgets('las resistencias y el arma de aliento son visibles y la CD se explica', (
      tester,
    ) async {
      final c = dragonborn();
      expect(c.sheet.resistances.map((r) => r.damageType), ['fire', 'poison']);
      expect(c.sheet.breathWeapon!.dc, 13);

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light(),
          home: Scaffold(body: SummaryTab(character: c)),
        ),
      );
      await tester.scrollUntilVisible(
        find.byKey(const Key('breath-weapon')),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.byKey(const Key('resistance-fire')), findsOneWidget);
      expect(find.text('fuego · Linaje dracónico (rojo)'), findsOneWidget);
      expect(find.text('veneno · Resistencia enana'), findsOneWidget);
      expect(find.text('Aliento'), findsOneWidget);
      expect(
        find.text('2d6 de daño fuego · cono de 15 pies · salvación de Destreza'),
        findsOneWidget,
      );
      expect(find.text('CD 13'), findsOneWidget);

      await tester.tap(find.byKey(const Key('stat-breath-weapon-dc')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('breakdown-sheet')), findsOneWidget);
      final sheet = find.byKey(const Key('breakdown-sheet'));
      expect(find.descendant(of: sheet, matching: find.text('Competencia')), findsOneWidget);
      expect(find.descendant(of: sheet, matching: find.text('Constitución')), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '13');
    });

    testWidgets('sin resistencias ni aliento no hay secciones', (tester) async {
      final c = CharacterDetail.fromJson(makeCharacterJson(status: 'Active'));
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light(),
          home: Scaffold(body: SummaryTab(character: c)),
        ),
      );
      expect(find.byKey(const Key('resistances')), findsNothing);
      expect(find.byKey(const Key('breath-weapon')), findsNothing);
    });

    testWidgets('las elecciones de origen (nivel 0) salen bajo "Raza y trasfondo"', (tester) async {
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
        ];
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light(),
          home: Scaffold(body: CharacterChoicesSection(character: CharacterDetail.fromJson(json))),
        ),
      );
      expect(find.text('Raza y trasfondo'), findsOneWidget);
      expect(find.text('Perspicacia'), findsOneWidget);
      expect(find.text('Nivel 0'), findsNothing);
    });
  });
}
