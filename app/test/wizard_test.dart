import 'package:dnd_companion/core/ui/selection_grid.dart';
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

const _fighterEquipment = StartingEquipment(
  fixed: [
    StartingItem(
      item: 'explorers-pack',
      templateId: 'explorers-pack',
      name: "Explorer's Pack",
      contents: [StartingItem(item: 'rope', templateId: 'rope', name: 'Rope', quantity: 1)],
    ),
    StartingItem(item: 'dagger', templateId: 'dagger', name: 'Dagger', quantity: 2),
  ],
  choices: [
    StartingEquipmentChoice(
      description: '(a) chain mail or (b) leather armor',
      options: [
        StartingEquipmentOption(
          label: 'Chain mail',
          items: [StartingItem(item: 'chain-mail', templateId: 'chain-mail', name: 'Chain Mail')],
        ),
        StartingEquipmentOption(
          label: 'Leather armor',
          items: [StartingItem(item: 'leather', templateId: 'leather', name: 'Leather Armor')],
        ),
      ],
    ),
    StartingEquipmentChoice(
      description: '(a) a martial weapon and a shield or (b) two martial weapons',
      options: [
        StartingEquipmentOption(
          label: 'A martial weapon and a shield',
          items: [StartingItem(item: 'shield', templateId: 'shield', name: 'Shield')],
          categories: [StartingCategoryPick(category: 'martial-weapons', name: 'Martial Weapons')],
        ),
        StartingEquipmentOption(
          label: 'Two martial weapons',
          categories: [
            StartingCategoryPick(category: 'martial-weapons', name: 'Martial Weapons', choose: 2),
          ],
        ),
      ],
    ),
  ],
  gold: StartingGold(dice: '5d4', multiplier: 10),
);

const _acolyteEquipment = StartingEquipment(
  fixed: [StartingItem(item: 'holy-symbol', templateId: 'holy-symbol', name: 'Holy Symbol')],
  fixedGoldCp: 1500,
);

FakeCatalogRepository _catalog({bool structured = false}) => FakeCatalogRepository(
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
    'fighter': ClassDetail(
      index: 'fighter',
      name: 'Fighter',
      hitDie: 10,
      savingThrows: const ['str', 'con'],
      skillChoices: const SkillChoices(choose: 2, from: ['athletics', 'perception', 'survival']),
      startingEquipmentText: 'Chain mail, a martial weapon and a shield.',
      startingEquipment: structured ? _fighterEquipment : null,
      levels: const [ClassLevel(level: 1)],
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
  backgroundList: [
    Background(
      index: 'acolyte',
      name: 'Acolyte',
      skillProficiencies: const ['Insight', 'Religion'],
      startingEquipmentText: 'A holy symbol and a prayer book.',
      startingEquipment: structured ? _acolyteEquipment : null,
    ),
  ],
  equipmentCategories: const {
    'martial-weapons': EquipmentCategory(
      index: 'martial-weapons',
      name: 'Martial Weapons',
      items: [
        EquipmentCategoryItem(templateId: 'battleaxe', index: 'battleaxe', name: 'Battleaxe'),
        EquipmentCategoryItem(templateId: 'halberd', index: 'halberd', name: 'Halberd'),
        EquipmentCategoryItem(templateId: 'longsword', index: 'longsword', name: 'Longsword'),
      ],
    ),
  },
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
  bool structured = false,
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
      catalog: _catalog(structured: structured),
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

const _args = (campaignId: 'c1', ownerUserId: null);

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
      expect(tester.widget<SelectionTile>(find.byKey(const Key('skill-insight'))).onTap, isNull);
      expect(tester.widget<SelectionTile>(find.byKey(const Key('skill-religion'))).onTap, isNull);
      expect(tester.widget<SelectionTile>(find.byKey(const Key('skill-arcana'))).onTap, isNotNull);
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

  group('tirada de características', () {
    Future<void> toAbilities(WidgetTester tester) async {
      await _name(tester);
      await _next(tester);
      await _tap(tester, find.byKey(const Key('race-elf')));
      await _tap(tester, find.byKey(const Key('subrace-high-elf')));
      await _next(tester);
      await _tap(tester, find.byKey(const Key('class-wizard')));
      await _next(tester);
      await _tap(tester, find.text('Tirada (4d6, descarta el menor)'));
    }

    Future<void> typeRolls(WidgetTester tester, List<String> values) async {
      for (var i = 0; i < values.length; i++) {
        await tester.enterText(find.byKey(Key('roll-score-$i')), values[i]);
      }
      await tester.pumpAndSettle();
    }

    Future<void> assign(WidgetTester tester, String ability, int slot) async {
      await _tap(tester, find.byKey(Key('wizard-roll-$ability')));
      await _tap(tester, find.byKey(Key('wizard-roll-$ability-slot-$slot')).last);
    }

    testWidgets('valida el rango, ordena y exige asignar los seis valores', (tester) async {
      await _pump(tester);
      await toAbilities(tester);
      expect(
        find.text(
          'Tira 4d6 seis veces, descarta el dado menor de cada tirada y escribe los totales.',
        ),
        findsOneWidget,
      );
      expect(find.byKey(const Key('roll-score-5')), findsOneWidget);

      await typeRolls(tester, ['19', '2', '8', '14', '10', '13']);
      expect(find.text('3 a 18'), findsNWidgets(2));
      expect(find.byKey(const Key('roll-sorted')), findsNothing);
      await _next(tester);
      expect(find.byKey(const Key('step-abilities')), findsOneWidget);
      expect(find.text('Escribe las seis tiradas (3 a 18)'), findsOneWidget);

      await typeRolls(tester, ['12', '15', '8', '14', '10', '13']);
      expect(find.text('3 a 18'), findsNothing);
      expect(
        find.text('Valores ordenados: 15, 14, 13, 12, 10, 8. Asigna cada uno una sola vez.'),
        findsOneWidget,
      );

      // Sin asignar no avanza.
      await _next(tester);
      expect(find.byKey(const Key('step-abilities')), findsOneWidget);
      expect(find.text('Asigna las seis puntuaciones'), findsOneWidget);

      await assign(tester, 'int', 0); // 15 (+1 alto elfo)
      expect(_text(tester, 'wizard-final-int'), '16 (+3) · raza +1');
      // El valor ya usado queda deshabilitado para las demás características.
      await _tap(tester, find.byKey(const Key('wizard-roll-dex')));
      final taken = tester.widget<DropdownMenuItem<int?>>(
        find.byKey(const Key('wizard-roll-dex-slot-0')).last,
      );
      expect(taken.enabled, isFalse);
      await _tap(tester, find.byKey(const Key('wizard-roll-dex-slot-1')).last); // 14 (+2 raza)
      expect(_text(tester, 'wizard-final-dex'), '16 (+3) · raza +2');
      await assign(tester, 'con', 2);
      await assign(tester, 'wis', 3);
      await assign(tester, 'cha', 4);
      await _next(tester);
      expect(find.byKey(const Key('step-abilities')), findsOneWidget);
      await assign(tester, 'str', 5);

      final state = ProviderScope.containerOf(
        tester.element(find.byKey(const Key('step-abilities'))),
      ).read(characterWizardControllerProvider(_args));
      expect(state.abilities, {'int': 15, 'dex': 14, 'con': 13, 'wis': 12, 'cha': 10, 'str': 8});

      await _next(tester);
      expect(find.byKey(const Key('step-background')), findsOneWidget);
    });

    test('valores repetidos se asignan por separado y cambiar una tirada reinicia', () {
      final container = ProviderContainer();
      addTearDown(container.dispose);
      final controller = container.read(characterWizardControllerProvider(_args).notifier);
      controller.setMethod(AbilityMethod.rolled);
      for (final (i, v) in ['14', '14', '9', '9', '3', '18'].indexed) {
        controller.setRollInput(i, v);
      }
      var state = container.read(characterWizardControllerProvider(_args));
      expect(state.rollValues, [18, 14, 14, 9, 9, 3]);
      controller.assignRoll('str', 1);
      controller.assignRoll('dex', 2);
      controller.assignRoll('con', 1);
      state = container.read(characterWizardControllerProvider(_args));
      expect(state.abilities, {'dex': 14, 'con': 14});
      controller.setRollInput(0, '15');
      state = container.read(characterWizardControllerProvider(_args));
      expect(state.rollAssignment, isEmpty);
      expect(parseRollScore('2'), isNull);
      expect(parseRollScore('abc'), isNull);
      expect(parseRollScore('18'), 18);
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

  group('equipo inicial estructurado', () {
    Future<_Setup> toEquipment(WidgetTester tester) async {
      final setup = await _pump(tester, structured: true);
      await _toBackground(tester, classIndex: 'fighter');
      await _tap(tester, find.byKey(const Key('wizard-background')));
      await _tap(tester, find.text('Acolyte').last);
      await _tap(tester, find.byKey(const Key('skill-athletics')));
      await _tap(tester, find.byKey(const Key('skill-perception')));
      await _next(tester);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);
      return setup;
    }

    Future<void> pickTwoWeapons(WidgetTester tester) async {
      await _tap(tester, find.byKey(const Key('equipment-option-1-1')));
      expect(find.byKey(const Key('equipment-category-picker')), findsOneWidget);
      await _tap(tester, find.byKey(const Key('equipment-category-item-longsword')));
      await _tap(tester, find.byKey(const Key('equipment-category-item-battleaxe')));
      await _tap(tester, find.byKey(const Key('equipment-category-done')));
    }

    Future<void> toReview(WidgetTester tester) async {
      await _next(tester);
      expect(find.byKey(const Key('step-review')), findsOneWidget);
    }

    testWidgets('el modo equipo bloquea hasta completar todas las elecciones', (tester) async {
      await toEquipment(tester);
      // Default items share the look of the player's own items, under their
      // own heading and marked "Por defecto", without edit buttons.
      expect(find.text('Equipo inicial'), findsOneWidget);
      final dagger = find.byKey(const Key('equipment-default-dagger'));
      expect(dagger, findsOneWidget);
      expect(find.descendant(of: dagger, matching: find.text('Cantidad: 2')), findsOneWidget);
      expect(find.descendant(of: dagger, matching: find.text('Por defecto')), findsOneWidget);
      expect(find.descendant(of: dagger, matching: find.byType(IconButton)), findsNothing);
      expect(find.text('Holy Symbol'), findsOneWidget);
      expect(find.text('Elecciones 0 de 2'), findsOneWidget);

      await _next(tester);
      expect(find.text('Completa todas las elecciones de equipo'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('equipment-option-0-0')));
      expect(find.text('Elecciones 1 de 2'), findsOneWidget);
      await _next(tester);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);

      await pickTwoWeapons(tester);
      expect(find.text('Elecciones 2 de 2'), findsOneWidget);
      await toReview(tester);
    });

    testWidgets('el selector de categoría exige exactamente 2 armas marciales', (tester) async {
      final setup = await toEquipment(tester);
      await _tap(tester, find.byKey(const Key('equipment-option-1-1')));
      await _tap(tester, find.byKey(const Key('equipment-category-item-longsword')));
      expect(find.text('Elige 2: 1 de 2'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('equipment-category-item-battleaxe')));
      // A third one is ignored.
      await _tap(tester, find.byKey(const Key('equipment-category-item-halberd')));
      expect(find.text('Elige 2: 2 de 2'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('equipment-category-done')));

      final state = setup.container.read(characterWizardControllerProvider(_args));
      expect(state.categoryPicks['1-1-0']!.map((e) => e.templateId), ['longsword', 'battleaxe']);
      expect(state.isChoiceComplete(1), isTrue);
    });

    testWidgets('el contenido de un paquete se despliega', (tester) async {
      await toEquipment(tester);
      expect(find.text('Rope'), findsNothing);
      await _tap(tester, find.byKey(const Key('equipment-default-explorers-pack')));
      expect(find.text('Rope'), findsOneWidget);
    });

    testWidgets('el oro inicial valida el rango y muestra la vista previa', (tester) async {
      await toEquipment(tester);
      await _tap(tester, find.byKey(const Key('equipment-mode-gold')));
      expect(find.text('Tira 5d4 y escribe el resultado'), findsOneWidget);
      expect(find.byKey(const Key('equipment-option-0-0')), findsNothing);

      await _next(tester);
      expect(find.text('Escribe el resultado de la tirada de oro'), findsOneWidget);

      await tester.enterText(find.byKey(const Key('equipment-gold-roll')), '4');
      await tester.pumpAndSettle();
      expect(find.text('La tirada va de 5 a 20'), findsWidgets);
      expect(find.byKey(const Key('equipment-gold-preview')), findsNothing);
      await _next(tester);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('equipment-gold-roll')), '21');
      await tester.pumpAndSettle();
      expect(find.text('La tirada va de 5 a 20'), findsWidgets);

      await tester.enterText(find.byKey(const Key('equipment-gold-roll')), '12');
      await tester.pumpAndSettle();
      expect(_text(tester, 'equipment-gold-preview'), '× 10 = 120 po');
      expect(
        tester.widget<CheckboxListTile>(find.byKey(const Key('equipment-keep-background'))).value,
        isFalse,
      );
      await toReview(tester);
    });

    testWidgets('envío en modo equipo: objetos y oro fijo del trasfondo', (tester) async {
      final setup = await toEquipment(tester);
      await _tap(tester, find.byKey(const Key('equipment-option-0-0')));
      await pickTwoWeapons(tester);
      await toReview(tester);
      expect(find.text('Oro inicial: 15 po'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('wizard-submit')));

      expect(setup.inventory.added.map((a) => (a.templateId, a.quantity)).toList(), [
        ('explorers-pack', 1),
        ('dagger', 2),
        ('holy-symbol', 1),
        ('chain-mail', 1),
        ('longsword', 1),
        ('battleaxe', 1),
      ]);
      expect(setup.characters.patches.single.copperPieces, 1500);
    });

    testWidgets('envío en modo oro sin conservar el trasfondo', (tester) async {
      final setup = await toEquipment(tester);
      await _tap(tester, find.byKey(const Key('equipment-mode-gold')));
      await tester.enterText(find.byKey(const Key('equipment-gold-roll')), '12');
      await tester.pumpAndSettle();
      await toReview(tester);
      await _tap(tester, find.byKey(const Key('wizard-submit')));

      expect(setup.inventory.added, isEmpty);
      expect(setup.characters.patches.single.copperPieces, 12000);
    });

    testWidgets('envío en modo oro conservando el equipo del trasfondo', (tester) async {
      final setup = await toEquipment(tester);
      await _tap(tester, find.byKey(const Key('equipment-mode-gold')));
      await tester.enterText(find.byKey(const Key('equipment-gold-roll')), '12');
      await tester.pumpAndSettle();
      await _tap(tester, find.byKey(const Key('equipment-keep-background')));
      await toReview(tester);
      await _tap(tester, find.byKey(const Key('wizard-submit')));

      expect(setup.inventory.added.map((a) => (a.templateId, a.quantity)).toList(), [
        ('holy-symbol', 1),
      ]);
      expect(setup.characters.patches.single.copperPieces, 13500);
    });

    testWidgets('cambiar de clase reinicia las elecciones de equipo', (tester) async {
      final setup = await toEquipment(tester);
      await _tap(tester, find.byKey(const Key('equipment-option-0-0')));
      final controller = setup.container.read(characterWizardControllerProvider(_args).notifier);
      expect(controller.state.equipmentOptions, isNotEmpty);

      await controller.selectClass('wizard');
      expect(controller.state.equipmentOptions, isEmpty);
      expect(controller.state.categoryPicks, isEmpty);
      expect(controller.state.goldRoll, isNull);
    });

    testWidgets('sin equipo estructurado se muestra el texto de siempre', (tester) async {
      await _pump(tester);
      await _toBackground(tester, classIndex: 'fighter');
      await _tap(tester, find.byKey(const Key('skill-athletics')));
      await _tap(tester, find.byKey(const Key('skill-perception')));
      await _next(tester);

      expect(find.byKey(const Key('wizard-class-equipment')), findsOneWidget);
      expect(find.byKey(const Key('equipment-mode-kit')), findsNothing);
      await _next(tester);
      expect(find.byKey(const Key('step-review')), findsOneWidget);
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
