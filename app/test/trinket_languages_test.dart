import 'package:dnd_companion/core/ui/selection_grid.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:flutter/material.dart' hide Page;
import 'package:flutter_test/flutter_test.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';

// Phase 21: trinket of the equipment step and the limit of the languages step.
// Every name of the trinket table is fictitious.

FakeCatalogRepository _catalog({List<Trinket> trinkets = const []}) => FakeCatalogRepository(
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
    RaceSummary(index: 'dwarf', name: 'Dwarf', speed: 25),
    RaceSummary(index: 'elf', name: 'Elf', speed: 30, subraceIndexes: ['high-elf']),
    RaceSummary(index: 'human', name: 'Human', speed: 30),
  ],
  raceDetails: {
    'dwarf': const RaceDetail(
      index: 'dwarf',
      name: 'Dwarf',
      speed: 25,
      languages: ['Common', 'Dwarvish'],
    ),
    'elf': const RaceDetail(
      index: 'elf',
      name: 'Elf',
      speed: 30,
      languages: ['Common', 'Elvish'],
      subraces: [
        Subrace(
          index: 'high-elf',
          name: 'High Elf',
          choices: OriginChoiceSpec(kinds: {'languages'}, languages: LanguagePick(choose: 1)),
        ),
      ],
    ),
    'human': const RaceDetail(
      index: 'human',
      name: 'Human',
      speed: 30,
      languages: ['Common'],
      choices: OriginChoiceSpec(kinds: {'languages'}, languages: LanguagePick(choose: 1)),
    ),
  },
  backgroundList: const [
    Background(
      index: 'acolyte',
      name: 'Acolyte',
      choices: OriginChoiceSpec(kinds: {'languages'}, languages: LanguagePick(choose: 2)),
    ),
    Background(
      index: 'guardia-ejemplo',
      name: 'Guardia de ejemplo',
      choices: OriginChoiceSpec(
        kinds: {'languages'},
        languages: LanguagePick(choose: 1, from: ['Giant', 'Orc']),
      ),
    ),
  ],
  trinketList: trinkets,
);

class _Setup {
  _Setup(this.characters, this.inventory);

  final FakeCharactersRepository characters;
  final FakeInventoryRepository inventory;
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

/// Taps "Siguiente"; the optional personality step (phase 22, tested in
/// personality_roll_tables_test.dart) is passed through.
Future<void> _next(WidgetTester tester) async {
  await _tap(tester, find.byKey(const Key('wizard-next')));
  if (find.byKey(const Key('step-personality')).evaluate().isNotEmpty) {
    await _tap(tester, find.byKey(const Key('wizard-next')));
  }
}

/// Name, race, fighter and abilities; stops on the background step.
Future<_Setup> _toBackground(
  WidgetTester tester, {
  required String race,
  String? subrace,
  String? background,
  List<Trinket> trinkets = const [],
}) async {
  final characters = FakeCharactersRepository();
  final inventory = FakeInventoryRepository();
  await pumpRealApp(
    tester,
    location: '/campaigns/c1/characters/new',
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
      characters: characters,
      inventory: inventory,
      catalog: _catalog(trinkets: trinkets),
    ),
  );
  await tester.enterText(find.byKey(const Key('wizard-name')), 'Merlina');
  await tester.pumpAndSettle();
  await _next(tester);
  await _tap(tester, find.byKey(Key('race-$race')));
  if (subrace != null) await _tap(tester, find.byKey(Key('subrace-$subrace')));
  await _next(tester);
  await _tap(tester, find.byKey(const Key('class-fighter')));
  await _next(tester);
  await _next(tester);
  if (background != null) await _background(tester, background);
  await _tap(tester, find.byKey(const Key('skill-athletics')));
  await _tap(tester, find.byKey(const Key('skill-survival')));
  return _Setup(characters, inventory);
}

Future<void> _background(WidgetTester tester, String name) async {
  await _tap(tester, find.byKey(const Key('wizard-background')));
  await _tap(tester, find.text(name).last);
}

SelectionState _lang(WidgetTester tester, String language) {
  final grid = find.ancestor(
    of: find.byKey(Key('lang-$language')),
    matching: find.byType(SelectionGrid),
  );
  return tester.widget<SelectionGrid>(grid.first).items.singleWhere((i) => i.id == language).state;
}

String _counter(WidgetTester tester) =>
    tester.widget<Text>(find.byKey(const Key('wizard-languages-counter'))).data!;

Future<void> _submit(WidgetTester tester) async {
  while (find.byKey(const Key('wizard-submit')).evaluate().isEmpty) {
    await _next(tester);
  }
  await _tap(tester, find.byKey(const Key('wizard-submit')));
}

void main() {
  group('idiomas', () {
    testWidgets('humano: los fijos van con candado y el segundo extra queda bloqueado', (
      tester,
    ) async {
      await _toBackground(tester, race: 'human');

      expect(_counter(tester), 'Idiomas a elegir 0/1');
      expect(_lang(tester, 'Common'), SelectionState.locked);
      expect(
        find.descendant(
          of: find.byKey(const Key('lang-Common')),
          matching: find.text('De la raza'),
        ),
        findsOneWidget,
      );
      expect(find.text('Te queda 1 idioma por elegir'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('lang-Dwarvish')));
      expect(_counter(tester), 'Idiomas a elegir 1/1');
      expect(_lang(tester, 'Dwarvish'), SelectionState.selected);
      expect(_lang(tester, 'Elvish'), SelectionState.blocked);
      expect(find.byKey(const Key('wizard-languages-remaining')), findsNothing);

      // A blocked tile does nothing; a fixed one cannot be removed.
      await _tap(tester, find.byKey(const Key('lang-Elvish')));
      await _tap(tester, find.byKey(const Key('lang-Common')));
      expect(_lang(tester, 'Elvish'), SelectionState.blocked);
      expect(_lang(tester, 'Common'), SelectionState.locked);
      expect(_counter(tester), 'Idiomas a elegir 1/1');
    });

    testWidgets('alto elfo acólito: 1 + 2 extra, repartidos en el PUT y los fijos en la hoja', (
      tester,
    ) async {
      final setup = await _toBackground(
        tester,
        race: 'elf',
        subrace: 'high-elf',
        background: 'Acolyte',
      );
      expect(_counter(tester), 'Idiomas a elegir 0/3');
      expect(_lang(tester, 'Elvish'), SelectionState.locked);

      for (final l in ['Dwarvish', 'Giant', 'Orc']) {
        await _tap(tester, find.byKey(Key('lang-$l')));
      }
      expect(_counter(tester), 'Idiomas a elegir 3/3');
      expect(_lang(tester, 'Goblin'), SelectionState.blocked);

      await _submit(tester);

      final patch = setup.characters.patches.single;
      expect(
        {
          for (final p in patch.proficiencies!)
            if (p.type == ProficiencyType.language) p.key,
        },
        {'Common', 'Elvish'},
      );
      final saved = {for (final a in setup.characters.originSaves.last) a['key']: a['selected']};
      expect(saved['race.subrace.languages'], ['Dwarvish']);
      expect(saved['background.languages'], ['Giant', 'Orc']);
    });

    testWidgets('menos de los ofrecidos avisa pero deja avanzar', (tester) async {
      await _toBackground(tester, race: 'elf', subrace: 'high-elf', background: 'Acolyte');
      await _tap(tester, find.byKey(const Key('lang-Giant')));
      expect(find.text('Te quedan 2 idiomas por elegir'), findsOneWidget);

      await _next(tester);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);
    });

    testWidgets('cambiar de trasfondo recorta la selección sobrante', (tester) async {
      await _toBackground(tester, race: 'elf', subrace: 'high-elf', background: 'Acolyte');
      for (final l in ['Dwarvish', 'Giant', 'Orc']) {
        await _tap(tester, find.byKey(Key('lang-$l')));
      }

      await _background(tester, 'Sin trasfondo');
      expect(_counter(tester), 'Idiomas a elegir 1/1');
      expect(_lang(tester, 'Dwarvish'), SelectionState.selected);
      expect(_lang(tester, 'Giant'), SelectionState.blocked);
      expect(_lang(tester, 'Orc'), SelectionState.blocked);
    });

    testWidgets('una opción con lista restringida solo admite esos idiomas', (tester) async {
      await _toBackground(tester, race: 'dwarf', background: 'Guardia de ejemplo');
      expect(_counter(tester), 'Idiomas a elegir 0/1');
      expect(_lang(tester, 'Dwarvish'), SelectionState.locked);
      expect(_lang(tester, 'Elvish'), SelectionState.blocked);
      expect(_lang(tester, 'Orc'), SelectionState.available);

      await _tap(tester, find.byKey(const Key('lang-Elvish')));
      await _tap(tester, find.byKey(const Key('lang-Orc')));
      expect(_lang(tester, 'Orc'), SelectionState.selected);
    });

    testWidgets('sin elecciones lo explica', (tester) async {
      await _toBackground(tester, race: 'dwarf');
      expect(_counter(tester), 'Tu raza y trasfondo no te dan idiomas adicionales');
      expect(_lang(tester, 'Elvish'), SelectionState.blocked);
    });
  });

  group('baratija', () {
    const table = [
      Trinket(
        roll: 7,
        templateId: 't-canica',
        index: 'pack-ejemplo-canica',
        name: 'Canica de ejemplo',
        description: 'Texto ficticio.',
      ),
    ];

    Future<_Setup> toEquipment(WidgetTester tester, {List<Trinket> trinkets = table}) async {
      final setup = await _toBackground(tester, race: 'dwarf', trinkets: trinkets);
      await _next(tester);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);
      expect(find.byKey(const Key('equipment-trinket')), findsOneWidget);
      return setup;
    }

    testWidgets('con tabla muestra el objeto y lo añade al terminar', (tester) async {
      final setup = await toEquipment(tester);
      await tester.enterText(find.byKey(const Key('trinket-roll')), '7');
      await tester.pumpAndSettle();

      final line = find.byKey(const Key('trinket-item'));
      expect(find.descendant(of: line, matching: find.text('Canica de ejemplo')), findsOneWidget);
      expect(find.byKey(const Key('trinket-text')), findsNothing);

      await _submit(tester);
      expect(setup.inventory.added.map((a) => (a.templateId, a.quantity)).toList(), [
        ('t-canica', 1),
      ]);
    });

    testWidgets('sin entrada para el número pide el texto y añade un objeto personalizado', (
      tester,
    ) async {
      final setup = await toEquipment(tester);
      await tester.enterText(find.byKey(const Key('trinket-roll')), '42');
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('trinket-item')), findsNothing);
      await tester.enterText(find.byKey(const Key('trinket-text')), 'Un botón de latón');
      await tester.pumpAndSettle();

      await _submit(tester);
      final added = setup.inventory.added.single;
      expect(added.templateId, isNull);
      expect(added.overrides.name, 'Baratija');
      expect(added.overrides.description, ['Un botón de latón']);
    });

    testWidgets('sin tabla (solo SRD) también pide el texto', (tester) async {
      await toEquipment(tester, trinkets: const []);
      await tester.enterText(find.byKey(const Key('trinket-roll')), '7');
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('trinket-text')), findsOneWidget);
    });

    testWidgets('fuera de rango bloquea; "Sin baratija" no añade nada', (tester) async {
      final setup = await toEquipment(tester);
      await tester.enterText(find.byKey(const Key('trinket-roll')), '101');
      await tester.pumpAndSettle();
      await _next(tester);
      expect(find.text('La tirada de baratija va de 1 a 100'), findsWidgets);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('trinket-none')));
      await _submit(tester);
      expect(setup.inventory.added, isEmpty);
    });
  });
}
