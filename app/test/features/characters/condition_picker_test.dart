import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_dnd5e/catalog/data/catalog_repository.dart';
import 'package:opentrpg_dnd5e/catalog/data/models.dart' show Condition;
import 'package:opentrpg_dnd5e/characters/domain/character_format.dart';
import 'package:opentrpg_dnd5e/characters/ui/combat/vitals_section.dart';

import '../../helpers/catalog_fakes.dart';

/// The fifteen conditions of the SRD, in its order, and the concentration
/// the catalog ships as one more condition.
const _srdConditions = [
  'blinded',
  'charmed',
  'deafened',
  'exhaustion',
  'frightened',
  'grappled',
  'incapacitated',
  'invisible',
  'paralyzed',
  'petrified',
  'poisoned',
  'prone',
  'restrained',
  'stunned',
  'unconscious',
  'concentration',
];

String _title(String index) => index[0].toUpperCase() + index.substring(1);

/// Pumps a phone-sized screen with a button that opens the picker; the picked
/// condition lands in [picked].
Future<void> _pump(WidgetTester tester, List<Condition?> picked) async {
  tester.view.physicalSize = const Size(360, 640);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final catalog = FakeCatalogRepository(
    conditionList: [
      for (final index in _srdConditions)
        Condition(index: index, name: _title(index), description: ['Rules of ${_title(index)}.']),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: [catalogRepositoryProvider.overrideWithValue(catalog)],
      child: MaterialApp(
        home: Scaffold(
          body: Builder(
            builder: (context) => TextButton(
              key: const Key('open-picker'),
              onPressed: () async => picked.add(await showConditionPicker(context, taken: {})),
              child: const Text('Abrir'),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.byKey(const Key('open-picker')));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('en un móvil lista todas las condiciones del catálogo', (tester) async {
    final picked = <Condition?>[];
    await _pump(tester, picked);

    final list = find.descendant(
      of: find.byKey(const Key('condition-picker-list')),
      matching: find.byType(Scrollable),
    );
    // One row per condition, reachable by scrolling down in the order of the
    // list (by Spanish name), with its info button; the concentration is one
    // more, under its Spanish name.
    final ordered = [..._srdConditions]
      ..sort((a, b) => foldForSort(conditionTitle(a)).compareTo(foldForSort(conditionTitle(b))));
    for (final index in ordered) {
      final row = find.byKey(Key('pick-condition-$index'));
      await tester.scrollUntilVisible(row, 100, scrollable: list);
      expect(row, findsOneWidget, reason: index);
      expect(find.byKey(Key('condition-info-$index')), findsOneWidget, reason: index);
      if (index == 'concentration') expect(find.text('Concentración'), findsOneWidget);
    }
    expect(find.byKey(const Key('condition-picker')), findsOneWidget);
    expect(picked, isEmpty);
  });

  testWidgets('en un móvil se llega a la última condición del SRD y se elige', (tester) async {
    final picked = <Condition?>[];
    await _pump(tester, picked);

    final last = find.byKey(const Key('pick-condition-unconscious'));
    await tester.scrollUntilVisible(
      last,
      100,
      scrollable: find.descendant(
        of: find.byKey(const Key('condition-picker-list')),
        matching: find.byType(Scrollable),
      ),
    );
    await tester.tap(last);
    await tester.pumpAndSettle();

    expect(picked.single?.index, 'unconscious');
    expect(find.byKey(const Key('condition-picker')), findsNothing);
  });

  testWidgets('el botón de información muestra la condición sin cerrar el selector', (
    tester,
  ) async {
    final picked = <Condition?>[];
    await _pump(tester, picked);

    await tester.tap(find.byKey(const Key('condition-info-blinded')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('condition-sheet')), findsOneWidget);
    expect(find.text('Rules of Blinded.'), findsOneWidget);

    // Closing the rules goes back to the picker, still open.
    Navigator.of(tester.element(find.byKey(const Key('condition-sheet')))).pop();
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('condition-picker')), findsOneWidget);
    expect(picked, isEmpty);
  });
}
