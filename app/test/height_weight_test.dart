import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg_core/features/dice/data/dice_controller.dart';
import 'package:opentrpg_dnd5e/catalog/data/models.dart';
import 'package:opentrpg_dnd5e/characters/data/character_wizard_controller.dart';
import 'package:opentrpg_dnd5e/characters/domain/height_weight.dart';
import 'package:opentrpg_dnd5e/characters/ui/wizard/character_wizard_page.dart';

import 'dice_test.dart' show SequenceRandom;
import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

/// Phase 29, block 3: height and weight of a character. Fictitious tables.
const _folkTable = HeightWeightTable(
  baseHeightInches: 50,
  heightModifier: '2d8',
  baseWeightPounds: 100,
  weightModifier: '2d4',
);

const _tallTable = HeightWeightTable(
  baseHeightInches: 60,
  heightModifier: '2d6',
  baseWeightPounds: 120,
  weightModifier: '1',
);

FakeCatalogRepository _catalog() => FakeCatalogRepository(
  classList: [ClassSummary(index: 'fighter', name: 'Fighter', hitDie: 10)],
  raceList: const [
    RaceSummary(index: 'folk', name: 'Folk', speed: 30, subraceIndexes: ['tall-folk']),
    RaceSummary(index: 'human', name: 'Human', speed: 30),
  ],
  raceDetails: {
    'folk': const RaceDetail(
      index: 'folk',
      name: 'Folk',
      speed: 30,
      heightWeight: _folkTable,
      subraces: [
        Subrace(index: 'short-folk', name: 'Short Folk'),
        Subrace(index: 'tall-folk', name: 'Tall Folk', heightWeight: _tallTable),
      ],
    ),
    'human': const RaceDetail(index: 'human', name: 'Human', speed: 30),
  },
);

const _args = (campaignId: 'c1', ownerUserId: null);

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<FakeCharactersRepository> _pump(
  WidgetTester tester, {
  required String location,
  List<Map<String, dynamic>> characters = const [],
  int face = 4,
}) async {
  final repository = FakeCharactersRepository(characters: characters);
  await pumpRealApp(
    tester,
    location: location,
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
      characters: repository,
      catalog: _catalog(),
    ),
    overrides: [diceRandomProvider.overrideWithValue(SequenceRandom.always(face))],
  );
  return repository;
}

String _fieldText(WidgetTester tester, String key) => tester
    .widget<TextField>(find.descendant(of: find.byKey(Key(key)), matching: find.byType(TextField)))
    .controller!
    .text;

void main() {
  group('formato', () {
    test('altura en pies y pulgadas con centímetros; peso en libras con kilos', () {
      expect(formatHeight(67), '5\' 7" (170 cm)');
      expect(formatHeight(48), '4\' 0" (122 cm)');
      expect(formatWeight(165), '165 lb (75 kg)');
      expect(formatWeight(35), '35 lb (16 kg)');
      expect(formatHeightAndWeight(67, 165), '5\' 7" (170 cm) · 165 lb (75 kg)');
      expect(formatHeightAndWeight(67, null), '5\' 7" (170 cm)');
      expect(formatHeightAndWeight(null, 165), '165 lb (75 kg)');
      expect(formatHeightAndWeight(null, null), isNull);
    });

    test('la tabla se describe como en el detalle de raza', () {
      expect(describeHeightWeightTable(_folkTable), 'base 4\' 2" + 2d8; 100 lb × 2d4');
    });

    test('la tirada suma la altura y multiplica la misma tirada por la de peso', () {
      final roll = rollHeightWeight(_folkTable, SequenceRandom.always(4))!;
      expect((roll.heightRoll, roll.weightRoll), (8, 8));
      expect(roll.heightInches, 58);
      expect(roll.weightPounds, 100 + 8 * 8);
      expect(roll.summary, 'Altura 2d8 = 8 → 4\' 10" · Peso 2d4 = 8 → 164 lb');

      final constant = rollHeightWeight(_tallTable, SequenceRandom.always(3))!;
      expect((constant.heightInches, constant.weightPounds), (66, 126));
    });

    test('valida lo escrito', () {
      expect(parseMeasure('67', min: 1, max: 200), 67);
      expect(parseMeasure('', min: 1, max: 200), isNull);
      expect(parseMeasure('0', min: 1, max: 200), isNull);
      expect(measureError('', min: 1, max: 200, label: 'La altura', unit: 'pulgadas'), isNull);
      expect(
        measureError('201', min: 1, max: 200, label: 'La altura', unit: 'pulgadas'),
        'La altura debe ser un número entero entre 1 y 200 pulgadas',
      );
    });
  });

  group('asistente', () {
    testWidgets('"Tirar" rellena altura y peso con la tabla de la subraza', (tester) async {
      await _pump(tester, location: '/campaigns/c1/characters/new');
      await tester.enterText(find.byKey(const Key('wizard-name')), 'Merlina');
      await _tap(tester, find.byKey(const Key('wizard-next')));

      await _tap(tester, find.byKey(const Key('race-folk')));
      await _tap(tester, find.byKey(const Key('subrace-short-folk')));
      // The race's table applies to a subrace without one.
      await _tap(tester, find.byKey(const Key('basics-roll-height-weight')));
      expect(_fieldText(tester, 'basics-height'), '58');
      expect(_fieldText(tester, 'basics-weight'), '164');
      expect(find.text('Altura 2d8 = 8 → 4\' 10" · Peso 2d4 = 8 → 164 lb'), findsOneWidget);
      expect(find.text('4\' 10" (147 cm) · 164 lb (74 kg)'), findsOneWidget);

      // The subrace's table replaces the race's.
      await _tap(tester, find.byKey(const Key('subrace-tall-folk')));
      await _tap(tester, find.byKey(const Key('basics-roll-height-weight')));
      expect(_fieldText(tester, 'basics-height'), '68');
      expect(_fieldText(tester, 'basics-weight'), '128');

      final container = ProviderScope.containerOf(tester.element(find.byType(CharacterWizardPage)));
      final state = container.read(characterWizardControllerProvider(_args));
      expect((state.heightInches, state.weightPounds), (68, 128));
    });

    testWidgets('sin tabla solo hay campos y un valor fuera de rango bloquea el paso', (
      tester,
    ) async {
      await _pump(tester, location: '/campaigns/c1/characters/new');
      await tester.enterText(find.byKey(const Key('wizard-name')), 'Merlina');
      await _tap(tester, find.byKey(const Key('wizard-next')));
      await _tap(tester, find.byKey(const Key('race-human')));

      expect(find.byKey(const Key('basics-height')), findsOneWidget);
      expect(find.byKey(const Key('basics-roll-height-weight')), findsNothing);

      await tester.enterText(find.byKey(const Key('basics-height')), '250');
      await _tap(tester, find.byKey(const Key('wizard-next')));
      expect(find.text('La altura debe ser un número entero entre 1 y 200 pulgadas'), findsWidgets);
      expect(find.byKey(const Key('step-race')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('basics-height')), '70');
      await _tap(tester, find.byKey(const Key('wizard-next')));
      expect(find.byKey(const Key('step-race')), findsNothing);
    });
  });

  group('hoja', () {
    testWidgets('Resumen muestra la línea de altura y peso', (tester) async {
      await _pump(
        tester,
        location: '/characters/ch1',
        characters: [
          {...makeCharacterJson(status: 'Active'), 'heightInches': 67, 'weightPounds': 165},
        ],
      );
      await _tap(tester, find.byKey(const Key('tab-summary')));

      expect(find.byKey(const Key('summary-height-weight')), findsOneWidget);
      expect(
        find.textContaining('5\' 7" (170 cm) · 165 lb (75 kg)', findRichText: true),
        findsOneWidget,
      );
    });

    testWidgets('el editor tira con la tabla de la raza y el jugador guarda sin aprobación', (
      tester,
    ) async {
      final repository = await _pump(
        tester,
        location: '/characters/ch1/edit',
        characters: [
          {
            ...makeCharacterJson(status: 'Active'),
            'raceIndex': 'folk',
            'subraceIndex': 'tall-folk',
          },
        ],
      );

      await _tap(tester, find.byKey(const Key('editor-roll-height-weight')));
      expect(_fieldText(tester, 'editor-height'), '68');
      expect(_fieldText(tester, 'editor-weight'), '128');

      await _tap(tester, find.byKey(const Key('editor-save')));
      expect(repository.patches.single.toJson(), {'heightInches': 68, 'weightPounds': 128});
      expect(find.text('Hoja guardada.'), findsOneWidget);
    });

    testWidgets('vaciar un campo lo borra', (tester) async {
      final repository = await _pump(
        tester,
        location: '/characters/ch1/edit',
        characters: [
          {...makeCharacterJson(), 'heightInches': 67, 'weightPounds': 165},
        ],
      );

      expect(find.byKey(const Key('editor-roll-height-weight')), findsNothing);
      await tester.enterText(find.byKey(const Key('editor-weight')), '');
      await _tap(tester, find.byKey(const Key('editor-save')));
      expect(repository.patches.single.toJson(), {'weightPounds': null});
    });
  });

  testWidgets('el detalle de raza muestra la tabla', (tester) async {
    await _pump(tester, location: '/compendium/races/folk');

    expect(find.byKey(const Key('race-height-weight')), findsOneWidget);
    expect(
      find.textContaining('base 4\' 2" + 2d8; 100 lb × 2d4', findRichText: true),
      findsOneWidget,
    );
    expect(find.byKey(const Key('subrace-height-weight-tall-folk')), findsOneWidget);
    expect(find.byKey(const Key('subrace-height-weight-short-folk')), findsNothing);
  });
}
