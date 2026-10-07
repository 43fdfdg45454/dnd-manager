import 'package:dnd_companion/core/ui/selection_grid.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/features/catalog/ui/roll_table_widgets.dart';
import 'package:dnd_companion/features/characters/data/character_wizard_controller.dart';
import 'package:flutter/material.dart' hide Page;
import 'package:flutter_test/flutter_test.dart';

import 'dice_test.dart' show SequenceRandom;
import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';

// Phase 22: personality step of the wizard and roll tables. Every text of the
// tables is fictitious.

const _traits = ['Rasgo ficticio A.', 'Rasgo ficticio B.', 'Rasgo ficticio C.'];
const _ideals = [
  BackgroundIdeal(text: 'Ideal ficticio uno.', alignment: 'Lawful'),
  BackgroundIdeal(text: 'Ideal ficticio dos.', alignment: 'Any'),
];
const _bonds = ['Vínculo ficticio uno.', 'Vínculo ficticio dos.'];
const _flaws = ['Defecto ficticio uno.', 'Defecto ficticio dos.'];

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
  raceList: const [RaceSummary(index: 'dwarf', name: 'Dwarf', speed: 25)],
  raceDetails: {
    'dwarf': const RaceDetail(
      index: 'dwarf',
      name: 'Dwarf',
      speed: 25,
      languages: ['Common', 'Dwarvish'],
    ),
  },
  backgroundList: const [
    Background(
      index: 'pack-cartografo',
      name: 'Cartógrafo de ejemplo',
      personality: BackgroundPersonality(
        traits: _traits,
        ideals: _ideals,
        bonds: _bonds,
        flaws: _flaws,
      ),
      optionalTables: [
        BackgroundTable(key: 'specialty', name: 'Especialidad', entries: ['Costas', 'Cuevas']),
      ],
    ),
    Background(index: 'pack-vacio', name: 'Trasfondo sin tablas'),
  ],
);

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _tapKey(WidgetTester tester, String key) => _tap(tester, find.byKey(Key(key)));

Future<void> _next(WidgetTester tester) => _tapKey(tester, 'wizard-next');

/// Name, dwarf, fighter, abilities and [background]; stops on the personality step.
Future<FakeCharactersRepository> _toPersonality(WidgetTester tester, String background) async {
  final characters = FakeCharactersRepository();
  await pumpRealApp(
    tester,
    location: '/campaigns/c1/characters/new',
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
      characters: characters,
      inventory: FakeInventoryRepository(),
      catalog: _catalog(),
    ),
    // Every roll takes the first entry still free.
    overrides: [wizardRandomProvider.overrideWithValue(SequenceRandom.always(1))],
  );
  await tester.enterText(find.byKey(const Key('wizard-name')), 'Merlina');
  await tester.pumpAndSettle();
  await _next(tester);
  await _tapKey(tester, 'race-dwarf');
  await _next(tester);
  await _tapKey(tester, 'class-fighter');
  await _next(tester);
  await _next(tester);
  await _tapKey(tester, 'wizard-background');
  await _tap(tester, find.text(background).last);
  await _tapKey(tester, 'skill-athletics');
  await _tapKey(tester, 'skill-survival');
  await _next(tester);
  expect(find.byKey(const Key('step-personality')), findsOneWidget);
  return characters;
}

Future<void> _submit(WidgetTester tester) async {
  while (find.byKey(const Key('wizard-submit')).evaluate().isEmpty) {
    await _next(tester);
  }
  await _tapKey(tester, 'wizard-submit');
}

SelectionState _tile(WidgetTester tester, String key) =>
    tester.widget<SelectionTile>(find.byKey(Key(key))).state;

String _field(WidgetTester tester, String key) =>
    tester.widget<TextField>(find.byKey(Key(key))).controller!.text;

void main() {
  group('paso de personalidad', () {
    testWidgets('tirar, elegir y escribir viajan en el parche de la hoja', (tester) async {
      final characters = await _toPersonality(tester, 'Cartógrafo de ejemplo');
      expect(find.text('Tirar d3'), findsOneWidget);
      expect(find.text('Tirar d2'), findsNWidgets(4));
      // The ideal shows its alignment.
      expect(find.text('Legal'), findsOneWidget);
      expect(find.text('Cualquiera'), findsOneWidget);

      // Rolling the traits fills both slots with different entries.
      await _tapKey(tester, 'personality-roll-traits');
      expect(_field(tester, 'personality-text-traits-0'), _traits[0]);
      expect(_field(tester, 'personality-text-traits-1'), _traits[1]);
      expect(_tile(tester, 'personality-traits-0'), SelectionState.selected);
      expect(_tile(tester, 'personality-traits-2'), SelectionState.blocked);

      // Unpicking one frees its slot for another entry.
      await _tapKey(tester, 'personality-traits-0');
      await _tapKey(tester, 'personality-traits-2');
      expect(_field(tester, 'personality-text-traits-0'), _traits[2]);

      // Tap an ideal, roll the bond, write the flaw and pick the specialty.
      await _tapKey(tester, 'personality-ideal-1');
      expect(_field(tester, 'personality-text-ideal-0'), _ideals[1].text);
      await _tapKey(tester, 'personality-roll-bond');
      expect(_field(tester, 'personality-text-bond-0'), _bonds[0]);
      await tester.enterText(
        find.byKey(const Key('personality-text-flaw-0')),
        'Mi defecto escrito a mano.',
      );
      await tester.pumpAndSettle();
      expect(_tile(tester, 'personality-flaw-0'), SelectionState.available);
      await _tapKey(tester, 'personality-table-specialty-1');
      expect(_field(tester, 'personality-text-detail'), 'Especialidad: Cuevas');
      expect(find.byKey(const Key('personality-warning')), findsNothing);

      await _submit(tester);
      final patch = characters.patches.last;
      expect(patch.personalityTraits, '${_traits[2]}\n${_traits[1]}');
      expect(patch.ideals, _ideals[1].text);
      expect(patch.bonds, _bonds[0]);
      expect(patch.flaws, 'Mi defecto escrito a mano.');
      expect(patch.backgroundDetail, 'Especialidad: Cuevas');
      expect(patch.toJson()['personalityTraits'], '${_traits[2]}\n${_traits[1]}');
    });

    testWidgets('sin tablas solo pide texto y "Siguiente" avisa sin bloquear', (tester) async {
      final characters = await _toPersonality(tester, 'Trasfondo sin tablas');
      expect(find.textContaining('Tirar d'), findsNothing);
      expect(find.byType(SelectionTile), findsNothing);
      expect(find.byKey(const Key('personality-text-detail')), findsNothing);

      await tester.enterText(find.byKey(const Key('personality-text-traits-0')), 'Rasgo propio.');
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Personalidad incompleta: falta un rasgo, el ideal, el vínculo y el defecto. '
          'Puedes completarla más tarde en la hoja.',
        ),
        findsOneWidget,
      );

      await _next(tester);
      expect(find.byKey(const Key('personality-next-warning')), findsOneWidget);
      expect(find.byKey(const Key('step-equipment')), findsOneWidget);

      await _submit(tester);
      final patch = characters.patches.last;
      expect(patch.personalityTraits, 'Rasgo propio.');
      expect(patch.toJson().containsKey('ideals'), isFalse);
      expect(patch.toJson().containsKey('backgroundDetail'), isFalse);
    });
  });

  group('tablas de tirada', () {
    const table = RollTable(
      key: 'surge-example',
      name: 'Oleada de ejemplo',
      dice: 'd100',
      subclassIndex: 'pack-chispa',
      entries: [
        RollTableEntry(from: 1, to: 1, text: 'Efecto del uno.'),
        RollTableEntry(from: 2, to: 49, text: 'Efecto bajo.'),
        RollTableEntry(from: 50, to: 51, text: 'Efecto del medio.'),
        RollTableEntry(from: 52, to: 99, text: 'Efecto alto.'),
        RollTableEntry(from: 100, to: 100, text: 'Efecto del cien.'),
      ],
    );

    test('lee la tirada: 00 es 100 y fuera de rango no vale', () {
      expect(table.faces, 100);
      expect(table.parseRoll('00'), 100);
      expect(table.parseRoll('0'), 100);
      expect(table.parseRoll('101'), isNull);
      expect(table.parseRoll('abc'), isNull);
      expect(table.entryFor(50)!.text, 'Efecto del medio.');
      const d20 = RollTable(key: 'k', name: 'n', dice: 'd20');
      expect(d20.parseRoll('0'), isNull);
      expect(d20.parseRoll('20'), 20);
    });

    testWidgets('muestra el resultado de 1, 50, 100 y 00', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: RollTableLookup(table: table)),
        ),
      );
      expect(find.text('Tira 1d100'), findsOneWidget);
      for (final (input, text) in [
        ('1', 'Efecto del uno.'),
        ('50', 'Efecto del medio.'),
        ('100', 'Efecto del cien.'),
        ('00', 'Efecto del cien.'),
      ]) {
        await tester.enterText(find.byKey(const Key('roll-table-input')), input);
        await tester.pump();
        expect(
          find.descendant(
            of: find.byKey(const Key('roll-table-result')),
            matching: find.text(text),
          ),
          findsOneWidget,
          reason: input,
        );
      }

      await tester.enterText(find.byKey(const Key('roll-table-input')), '101');
      await tester.pump();
      expect(find.byKey(const Key('roll-table-result')), findsNothing);
      expect(find.byKey(const Key('roll-table-error')), findsOneWidget);
    });

    testWidgets('el compendio lista las tablas y abre la tabla completa con buscador', (
      tester,
    ) async {
      await pumpRealApp(
        tester,
        location: '/compendium',
        fakes: AppFakes(catalog: FakeCatalogRepository(rollTableList: const [table])),
      );
      await _tapKey(tester, 'tab-tables');
      await _tapKey(tester, 'roll-table-surge-example');
      expect(find.byKey(const Key('roll-table-page')), findsOneWidget);
      expect(find.byKey(const Key('roll-table-entry-52')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('roll-table-search')), 'medio');
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('roll-table-entry-50')), findsOneWidget);
      expect(find.byKey(const Key('roll-table-entry-52')), findsNothing);

      await tester.enterText(find.byKey(const Key('roll-table-input')), '00');
      await tester.pumpAndSettle();
      expect(find.text('Efecto del cien.'), findsWidgets);
    });
  });
}
