import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/features/characters/data/character_wizard_controller.dart';
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/characters/ui/wizard/character_wizard_page.dart';
import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';

const _skillsOfClass = SkillChoices(
  choose: 2,
  from: ['arcana', 'history', 'insight', 'investigation', 'medicine', 'religion'],
);

FakeCatalogRepository _catalog() => FakeCatalogRepository(
  classList: const [
    ClassSummary(index: 'cleric', name: 'Cleric', hitDie: 8, isSpellcaster: true),
    ClassSummary(index: 'fighter', name: 'Fighter', hitDie: 10),
    ClassSummary(
      index: 'wizard',
      name: 'Wizard',
      hitDie: 6,
      isSpellcaster: true,
      spellcastingAbility: 'int',
    ),
  ],
  classDetails: {
    'cleric': const ClassDetail(
      index: 'cleric',
      name: 'Cleric',
      hitDie: 8,
      isSpellcaster: true,
      spellcastingAbility: 'wis',
      savingThrows: ['wis', 'cha'],
      skillChoices: SkillChoices(choose: 2, from: ['history', 'insight', 'medicine']),
      subclasses: [Subclass(index: 'life', name: 'Life Domain')],
      levels: [
        ClassLevel(level: 1, cantripsKnown: 3, spellSlots: [2, 0, 0, 0, 0, 0, 0, 0, 0]),
      ],
    ),
    'fighter': const ClassDetail(
      index: 'fighter',
      name: 'Fighter',
      hitDie: 10,
      savingThrows: ['str', 'con'],
      skillChoices: SkillChoices(choose: 2, from: ['athletics', 'perception', 'survival']),
      startingEquipmentText: 'Chain mail, a martial weapon and a shield.',
      levels: [ClassLevel(level: 1)],
    ),
    'wizard': const ClassDetail(
      index: 'wizard',
      name: 'Wizard',
      hitDie: 6,
      isSpellcaster: true,
      spellcastingAbility: 'int',
      savingThrows: ['int', 'wis'],
      skillChoices: _skillsOfClass,
      startingEquipmentText: 'A quarterstaff and a spellbook.',
      levels: [
        ClassLevel(level: 1, cantripsKnown: 3, spellSlots: [2, 0, 0, 0, 0, 0, 0, 0, 0]),
      ],
    ),
  },
  raceList: const [
    RaceSummary(
      index: 'elf',
      name: 'Elf',
      speed: 30,
      abilityBonuses: [AbilityBonus(ability: 'dex', bonus: 2)],
      subraceIndexes: ['high-elf'],
    ),
    RaceSummary(
      index: 'human',
      name: 'Human',
      speed: 30,
      abilityBonuses: [AbilityBonus(ability: 'str', bonus: 1)],
    ),
  ],
  raceDetails: {
    'elf': const RaceDetail(
      index: 'elf',
      name: 'Elf',
      speed: 30,
      abilityBonuses: [AbilityBonus(ability: 'dex', bonus: 2)],
      languages: ['Common', 'Elvish'],
      subraces: [
        Subrace(
          index: 'high-elf',
          name: 'High Elf',
          abilityBonuses: [AbilityBonus(ability: 'int', bonus: 1)],
        ),
      ],
    ),
    'human': const RaceDetail(
      index: 'human',
      name: 'Human',
      speed: 30,
      abilityBonuses: [AbilityBonus(ability: 'str', bonus: 1)],
      languages: ['Common'],
    ),
  },
  backgroundList: const [
    Background(
      index: 'acolyte',
      name: 'Acolyte',
      skillProficiencies: ['Insight', 'Religion'],
      startingEquipmentText: 'A holy symbol and a prayer book.',
    ),
  ],
  spellList: [
    makeSpell(index: 'fire-bolt', name: 'Fire Bolt', level: 0),
    makeSpell(index: 'light', name: 'Light', level: 0),
    makeSpell(index: 'mage-hand', name: 'Mage Hand', level: 0),
    makeSpell(index: 'ray-of-frost', name: 'Ray of Frost', level: 0),
    makeSpell(index: 'magic-missile', name: 'Magic Missile', level: 1),
    makeSpell(index: 'shield', name: 'Shield', level: 1),
  ],
);

class _Setup {
  _Setup(this.tester, this.router, this.characters, this.inventory);

  final WidgetTester tester;
  final GoRouter router;
  final FakeCharactersRepository characters;
  final FakeInventoryRepository inventory;

  ProviderContainer get container =>
      ProviderScope.containerOf(tester.element(find.byType(CharacterWizardPage)));
}

Future<_Setup> _pump(
  WidgetTester tester, {
  CampaignRole role = CampaignRole.player,
  String location = '/campaigns/c1/characters/new',
}) async {
  final characters = FakeCharactersRepository(isDm: role.isAtLeastDm);
  final inventory = FakeInventoryRepository();
  final router = await pumpRealApp(
    tester,
    location: location,
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
      characters: characters,
      inventory: inventory,
      catalog: _catalog(),
      campaignItems: FakeCampaignItemsRepository(
        srd: [
          makeItem(id: 'dagger', name: 'Dagger', costCp: 200),
          makeItem(id: 'rope', name: 'Rope', costCp: 100),
        ],
      ),
    ),
  );
  return _Setup(tester, router, characters, inventory);
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _next(WidgetTester tester) => _tap(tester, find.byKey(const Key('wizard-next')));

Future<void> _name(WidgetTester tester, [String name = 'Merlina']) async {
  await tester.enterText(find.byKey(const Key('wizard-name')), name);
  await tester.pumpAndSettle();
}

Future<void> _plus(WidgetTester tester, String ability, int times) async {
  for (var i = 0; i < times; i++) {
    await _tap(tester, find.byKey(Key('wizard-pb-plus-$ability')));
  }
}

/// Goes through name, race (elf, high elf), class and abilities with 27 points.
Future<void> _toBackground(WidgetTester tester, {String classIndex = 'wizard'}) async {
  await _name(tester);
  await _next(tester);
  await _tap(tester, find.byKey(const Key('race-elf')));
  await _tap(tester, find.byKey(const Key('subrace-high-elf')));
  await _next(tester);
  await _tap(tester, find.byKey(Key('class-$classIndex')));
  await _next(tester);
  await _plus(tester, 'int', 7); // 15 -> 9 points
  await _plus(tester, 'dex', 6); // 14 -> 7 points
  await _plus(tester, 'con', 5); // 13 -> 5 points
  await _next(tester);
}

String _text(WidgetTester tester, String key) => tester.widget<Text>(find.byKey(Key(key))).data!;

void main() {
  group('validación de cada paso', () {
    testWidgets('el nombre vacío bloquea "Siguiente" y muestra el mensaje', (tester) async {
      await _pump(tester);

      expect(find.byKey(const Key('step-name')), findsOneWidget);
      await _next(tester);

      expect(find.text('Introduce un nombre'), findsOneWidget);
      expect(find.byKey(const Key('step-name')), findsOneWidget);
      expect(find.byKey(const Key('step-race')), findsNothing);

      await _name(tester);
      await _next(tester);
      expect(find.byKey(const Key('step-race')), findsOneWidget);
      expect(find.text('Introduce un nombre'), findsNothing);
    });

    testWidgets('raza y subraza obligatorias', (tester) async {
      await _pump(tester);
      await _name(tester);
      await _next(tester);

      await _next(tester);
      expect(find.text('Elige una raza'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('race-elf')));
      await _next(tester);
      expect(find.text('Elige una subraza'), findsOneWidget);
      expect(find.byKey(const Key('step-race')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('subrace-high-elf')));
      await _next(tester);
      expect(find.byKey(const Key('step-class')), findsOneWidget);
    });

    testWidgets('clase y subclase obligatorias (clérigo)', (tester) async {
      await _pump(tester);
      await _name(tester);
      await _next(tester);
      await _tap(tester, find.byKey(const Key('race-human')));
      await _next(tester);

      await _next(tester);
      expect(find.text('Elige una clase'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('class-cleric')));
      expect(find.byKey(const Key('subclass-life')), findsOneWidget);
      await _next(tester);
      expect(find.text('Elige una subclase'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('subclass-life')));
      await _next(tester);
      expect(find.byKey(const Key('step-abilities')), findsOneWidget);
    });

    testWidgets('la matriz estándar exige asignar las seis puntuaciones', (tester) async {
      await _pump(tester);
      await _name(tester);
      await _next(tester);
      await _tap(tester, find.byKey(const Key('race-human')));
      await _next(tester);
      await _tap(tester, find.byKey(const Key('class-fighter')));
      await _next(tester);

      await _tap(tester, find.text('Matriz estándar'));
      await _tap(tester, find.byKey(const Key('wizard-array-str')));
      await _tap(tester, find.text('15').last);
      await _next(tester);

      expect(find.text('Asigna las seis puntuaciones'), findsOneWidget);
      expect(find.byKey(const Key('step-abilities')), findsOneWidget);
    });

    testWidgets('las habilidades de clase deben ser exactamente las que se eligen', (tester) async {
      await _pump(tester);
      await _toBackground(tester);

      await _next(tester);
      expect(find.text('Elige 2 habilidades'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('skill-arcana')));
      await _next(tester);
      expect(find.text('Elige 2 habilidades'), findsOneWidget);
      expect(find.text('Habilidades de clase 1/2'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('skill-history')));
      // A third one is ignored: the limit is "choose".
      await _tap(tester, find.byKey(const Key('skill-medicine')));
      expect(find.text('Habilidades de clase 2/2'), findsOneWidget);
      await _next(tester);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);
    });

    testWidgets('las habilidades del trasfondo se deshabilitan en la lista de clase', (
      tester,
    ) async {
      await _pump(tester);
      await _toBackground(tester);

      await _tap(tester, find.byKey(const Key('wizard-background')));
      await _tap(tester, find.text('Acolyte').last);

      expect(find.byKey(const Key('wizard-background-skills')), findsOneWidget);
      expect(tester.widget<FilterChip>(find.byKey(const Key('skill-insight'))).onSelected, isNull);
      expect(tester.widget<FilterChip>(find.byKey(const Key('skill-religion'))).onSelected, isNull);
      expect(
        tester.widget<FilterChip>(find.byKey(const Key('skill-arcana'))).onSelected,
        isNotNull,
      );
    });

    test('mensajes de trucos, hechizos y puntuaciones del estado', () {
      const wizard = ClassDetail(
        index: 'wizard',
        name: 'Wizard',
        isSpellcaster: true,
        spellcastingAbility: 'int',
        levels: [
          ClassLevel(level: 1, cantripsKnown: 3, spellSlots: [2, 0, 0, 0, 0, 0, 0, 0, 0]),
        ],
      );
      CharacterSpell spell(String index, int level) =>
          CharacterSpell(spellIndex: index, classIndex: 'wizard', level: level);
      var state = const WizardState(classDetail: wizard);
      final spellsStep = state.steps.indexOf(WizardStep.spells);
      expect(spellsStep, isNonNegative);

      state = state.copyWith(cantrips: [for (var i = 0; i < 4; i++) spell('c$i', 0)]);
      expect(state.validate(spellsStep), 'Como máximo 3 trucos');

      // Int 8 -> modifier -1 -> max(1, 0) = 1 prepared spell.
      state = state.copyWith(
        cantrips: [spell('c1', 0)],
        leveledSpells: [spell('s1', 1), spell('s2', 1)],
      );
      expect(state.maxSpells, 1);
      expect(state.validate(spellsStep), 'Como máximo 1 hechizo');

      state = state.copyWith(
        manualScores: {...state.manualScores, 'int': 21},
        method: AbilityMethod.manual,
      );
      expect(state.validate(state.steps.indexOf(WizardStep.abilities)), isNotNull);
    });
  });

  group('puntuaciones', () {
    test('compra por puntos: 27 exactos valen, 28 no', () {
      const exact = {'str': 15, 'dex': 15, 'con': 15, 'int': 8, 'wis': 8, 'cha': 8};
      const over = {'str': 15, 'dex': 15, 'con': 15, 'int': 9, 'wis': 8, 'cha': 8};
      final abilities = const WizardState().steps.indexOf(WizardStep.abilities);

      expect(const WizardState(pointBuyScores: exact).pointBuySpent, 27);
      expect(const WizardState(pointBuyScores: exact).validate(abilities), isNull);
      expect(const WizardState(pointBuyScores: over).pointBuySpent, 28);
      expect(
        const WizardState(pointBuyScores: over).validate(abilities),
        'Te has pasado de 27 puntos',
      );
    });

    testWidgets('compra por puntos: los botones se detienen en 27 puntos', (tester) async {
      await _pump(tester);
      await _name(tester);
      await _next(tester);
      await _tap(tester, find.byKey(const Key('race-human')));
      await _next(tester);
      await _tap(tester, find.byKey(const Key('class-fighter')));
      await _next(tester);

      expect(find.text('Puntos restantes: 27 / 27'), findsOneWidget);
      await _plus(tester, 'str', 7);
      await _plus(tester, 'dex', 7);
      await _plus(tester, 'con', 7);
      expect(find.text('Puntos restantes: 0 / 27'), findsOneWidget);
      expect(_text(tester, 'wizard-pb-score-str'), '15');
      expect(
        tester.widget<IconButton>(find.byKey(const Key('wizard-pb-plus-int'))).onPressed,
        isNull,
      );

      await _next(tester);
      expect(find.byKey(const Key('step-background')), findsOneWidget);
    });

    testWidgets('la vista previa suma el bono racial y muestra el modificador', (tester) async {
      await _pump(tester);
      await _name(tester);
      await _next(tester);
      await _tap(tester, find.byKey(const Key('race-elf')));
      await _tap(tester, find.byKey(const Key('subrace-high-elf')));
      await _next(tester);
      await _tap(tester, find.byKey(const Key('class-wizard')));
      await _next(tester);
      await _plus(tester, 'int', 7);

      expect(_text(tester, 'wizard-final-int'), '16 (+3) · raza +1');
      expect(_text(tester, 'wizard-final-dex'), '10 (+0) · raza +2');
      expect(_text(tester, 'wizard-final-str'), '8 (-1)');
    });

    test('matriz estándar: un valor no se repite', () {
      final container = ProviderContainer();
      addTearDown(container.dispose);
      const args = (campaignId: 'c1', ownerUserId: null);
      final controller = container.read(characterWizardControllerProvider(args).notifier);

      controller.assignArray('str', 15);
      controller.assignArray('dex', 14);
      controller.assignArray('con', 15);

      final scores = container.read(characterWizardControllerProvider(args)).arrayScores;
      expect(scores, {'dex': 14, 'con': 15});
    });
  });

  group('hechizos', () {
    testWidgets('el paso se omite para un guerrero', (tester) async {
      await _pump(tester);
      await _toBackground(tester, classIndex: 'fighter');

      expect(find.byKey(const Key('wizard-dot-spells')), findsNothing);
      expect(find.text('Paso 5 de 7 · Trasfondo'), findsOneWidget);
    });

    testWidgets('aparece para un mago y limita trucos y hechizos al nivel 1', (tester) async {
      await _pump(tester);
      await _toBackground(tester);
      expect(find.byKey(const Key('wizard-dot-spells')), findsOneWidget);
      expect(find.text('Paso 5 de 8 · Trasfondo'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('skill-arcana')));
      await _tap(tester, find.byKey(const Key('skill-history')));
      await _next(tester);
      await _next(tester);

      expect(find.byKey(const Key('step-spells')), findsOneWidget);
      expect(find.text('Trucos 0/3'), findsOneWidget);
      // Int 15 + 1 (high elf) = 16 -> modifier +3 -> 4 spells.
      expect(find.text('Hechizos 0/4'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('wizard-pick-cantrips')));
      for (final spell in ['fire-bolt', 'light', 'mage-hand', 'ray-of-frost']) {
        await _tap(tester, find.byKey(Key('picker-spell-$spell')));
      }
      await _tap(tester, find.byKey(const Key('spell-picker-done')));
      expect(find.text('Trucos 3/3'), findsOneWidget);
      expect(find.byKey(const Key('spell-ray-of-frost')), findsNothing);
      // Every cantrip is already chosen: the button is disabled.
      expect(
        tester.widget<TextButton>(find.byKey(const Key('wizard-pick-cantrips'))).onPressed,
        isNull,
      );

      await _tap(tester, find.byKey(const Key('wizard-pick-spells')));
      expect(find.byKey(const Key('picker-spell-fire-bolt')), findsNothing);
      await _tap(tester, find.byKey(const Key('picker-spell-magic-missile')));
      await _tap(tester, find.byKey(const Key('spell-picker-done')));
      expect(find.text('Hechizos 1/4'), findsOneWidget);

      await _tap(
        tester,
        find.descendant(
          of: find.byKey(const Key('spell-magic-missile')),
          matching: find.byIcon(Icons.clear),
        ),
      );
      expect(find.text('Hechizos 0/4'), findsOneWidget);
    });
  });

  group('envío', () {
    Future<void> toReview(WidgetTester tester) async {
      await _toBackground(tester);
      await _tap(tester, find.byKey(const Key('wizard-background')));
      await _tap(tester, find.text('Acolyte').last);
      await _tap(tester, find.byKey(const Key('skill-arcana')));
      await _tap(tester, find.byKey(const Key('skill-history')));
      await _next(tester);

      await _tap(tester, find.byKey(const Key('wizard-add-item')));
      await _tap(tester, find.byKey(const Key('item-dagger')));
      await _tap(tester, find.byKey(const Key('item-dagger')));
      await _tap(tester, find.byKey(const Key('item-rope')));
      await _tap(tester, find.byKey(const Key('wizard-item-done')));
      expect(find.text('Cantidad: 2'), findsOneWidget);
      await _next(tester);

      await _tap(tester, find.byKey(const Key('wizard-pick-cantrips')));
      await _tap(tester, find.byKey(const Key('picker-spell-fire-bolt')));
      await _tap(tester, find.byKey(const Key('picker-spell-light')));
      await _tap(tester, find.byKey(const Key('spell-picker-done')));
      await _tap(tester, find.byKey(const Key('wizard-pick-spells')));
      await _tap(tester, find.byKey(const Key('picker-spell-magic-missile')));
      await _tap(tester, find.byKey(const Key('spell-picker-done')));
      await _next(tester);
      expect(find.byKey(const Key('step-review')), findsOneWidget);
    }

    testWidgets('crea el personaje, guarda la hoja, añade el equipo y abre la ficha', (
      tester,
    ) async {
      final setup = await _pump(tester);
      await toReview(tester);
      expect(find.text('Crear personaje'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('wizard-submit')));

      final created = setup.characters.created.single;
      expect(created.name, 'Merlina');
      expect(created.owner, isNull);

      final patch = setup.characters.patches.single;
      expect(patch.name, 'Merlina');
      expect(patch.raceIndex, 'elf');
      expect(patch.subraceIndex, 'high-elf');
      expect(patch.backgroundIndex, 'acolyte');
      expect(patch.applyRacialBonuses, isTrue);
      expect(patch.baseAbilities, {'str': 8, 'dex': 14, 'con': 13, 'int': 15, 'wis': 8, 'cha': 8});
      expect(patch.classes!.single.classIndex, 'wizard');
      expect(patch.classes!.single.level, 1);
      expect(patch.overrides, isEmpty);

      final skills = {
        for (final p in patch.proficiencies!)
          if (p.type == ProficiencyType.skill) p.key: p.source,
      };
      expect(skills, {
        'arcana': ProficiencySource.classSource,
        'history': ProficiencySource.classSource,
        'insight': ProficiencySource.background,
        'religion': ProficiencySource.background,
      });
      expect(
        {
          for (final p in patch.proficiencies!)
            if (p.type == ProficiencyType.language) p.key,
        },
        {'Common', 'Elvish'},
      );
      expect(
        {
          for (final p in patch.proficiencies!)
            if (p.type == ProficiencyType.savingThrow) p.key: p.source,
        },
        {'int': ProficiencySource.classSource, 'wis': ProficiencySource.classSource},
      );
      expect(
        {for (final s in patch.spells!) s.spellIndex: s.isPrepared},
        {'fire-bolt': true, 'light': true, 'magic-missile': true},
      );
      expect(patch.spells!.every((s) => s.classIndex == 'wizard'), isTrue);

      expect(setup.inventory.added.map((a) => (a.templateId, a.quantity)).toList(), [
        ('dagger', 2),
        ('rope', 1),
      ]);
      expect(locationOf(setup.router), AppRoutes.character('new1'));
    });

    testWidgets('el resumen permite saltar a un paso con "Editar"', (tester) async {
      await _pump(tester);
      await toReview(tester);

      await _tap(tester, find.byKey(const Key('review-edit-race')));
      expect(find.byKey(const Key('step-race')), findsOneWidget);
      await _tap(tester, find.byKey(const Key('wizard-dot-review')));
      // Forward jumps are not allowed from the rail: only "Siguiente" moves on.
      expect(find.byKey(const Key('step-race')), findsOneWidget);
    });

    testWidgets('un error del servidor se muestra y el asistente permanece', (tester) async {
      final setup = await _pump(tester);
      await toReview(tester);
      setup.characters.error = dioError(500);

      await _tap(tester, find.byKey(const Key('wizard-submit')));

      expect(find.byKey(const Key('wizard-submit-error')), findsOneWidget);
      expect(find.byKey(const Key('step-review')), findsOneWidget);
      expect(setup.characters.created, isEmpty);

      setup.characters.error = null;
      await _tap(tester, find.byKey(const Key('wizard-submit')));
      expect(setup.characters.created, hasLength(1));
      expect(locationOf(setup.router), AppRoutes.character('new1'));
    });
  });

  group('dueño', () {
    testWidgets('un jugador no elige dueño', (tester) async {
      await _pump(tester);
      expect(find.byKey(const Key('wizard-owner')), findsNothing);
    });

    testWidgets('un DM elige el dueño y puede llegar con ownerUserId', (tester) async {
      final setup = await _pump(
        tester,
        role: CampaignRole.dm,
        location: '/campaigns/c1/characters/new?ownerUserId=p2',
      );

      expect(find.byKey(const Key('wizard-owner')), findsOneWidget);
      expect(find.text('Beto'), findsOneWidget);
      const args = (campaignId: 'c1', ownerUserId: 'p2');
      expect(setup.container.read(characterWizardControllerProvider(args)).owner, (userId: 'p2'));

      await _tap(tester, find.byKey(const Key('wizard-owner')));
      await _tap(tester, find.text('Sin dueño (PNJ)').last);
      final state = setup.container.read(characterWizardControllerProvider(args));
      expect(state.owner, (userId: null));
    });
  });
}
