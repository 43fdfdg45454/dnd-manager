import 'package:dnd_companion/features/catalog/data/catalog_repository.dart';
import 'package:dnd_companion/features/catalog/data/models.dart' show Condition;
import 'package:dnd_companion/features/characters/ui/combat/vitals_section.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/catalog_fakes.dart';

/// The fifteen conditions of the SRD, in its order.
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
