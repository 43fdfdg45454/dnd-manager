import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/theme/app_theme.dart';
import 'package:opentrpg_core/features/items/data/inventory_repository.dart';
import 'package:opentrpg_dnd5e/catalog/data/catalog_repository.dart';
import 'package:opentrpg_dnd5e/catalog/data/models.dart' show ReferenceEntry, ReferenceKinds;
import 'package:opentrpg_dnd5e/characters/models.dart';
import 'package:opentrpg_dnd5e/characters/ui/character_tabs.dart';

import '../../helpers/catalog_fakes.dart';
import '../../helpers/character_fakes.dart';
import '../../helpers/item_fakes.dart';

CharacterDetail _character(List<Map<String, dynamic>> proficiencies) =>
    CharacterDetail.fromJson(makeCharacterJson(status: 'Active', proficiencies: proficiencies));

final _catalog = FakeCatalogRepository(
  referenceEntries: const [
    ReferenceEntry(
      kind: ReferenceKinds.tools,
      index: 'thieves-tools',
      name: "Thieves' Tools",
      description: ['A set of picks and small files.'],
    ),
    ReferenceEntry(
      kind: ReferenceKinds.tools,
      index: 'brewers-supplies',
      name: "Brewer's Supplies",
    ),
    ReferenceEntry(kind: ReferenceKinds.tools, index: 'lute', name: 'Lute'),
    ReferenceEntry(
      kind: ReferenceKinds.languages,
      index: 'dwarvish',
      name: 'Dwarvish',
      description: ['Script: Dwarvish.'],
    ),
  ],
);

Future<void> _pump(
  WidgetTester tester,
  CharacterDetail character,
  FakeInventoryRepository inv,
) async {
  tester.view.physicalSize = const Size(800, 4000);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        catalogRepositoryProvider.overrideWithValue(_catalog),
        inventoryRepositoryProvider.overrideWithValue(inv),
      ],
      child: MaterialApp(
        theme: AppTheme.light(),
        home: Scaffold(body: SkillsTab(character: character)),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

String _detail(WidgetTester tester, String id) =>
    tester.widget<Text>(find.byKey(Key('proficiency-detail-$id'))).data!;

void main() {
  final character = _character([
    {'type': 'Armor', 'key': 'light-armor', 'source': 'Class'},
    {'type': 'Armor', 'key': 'shields', 'source': 'Class'},
    {'type': 'Weapon', 'key': 'simple-weapons', 'source': 'Class'},
    {'type': 'Weapon', 'key': 'hand-crossbows', 'source': 'Race'},
    {'type': 'Tool', 'key': 'thieves-tools', 'source': 'Class', 'expertise': true},
    {'type': 'Tool', 'key': 'thieves-tools', 'source': 'Background'},
    {'type': 'Tool', 'key': 'brewers-supplies', 'source': 'Background'},
    {'type': 'Tool', 'key': 'lute', 'source': 'Manual'},
    {'type': 'Language', 'key': 'dwarvish', 'source': 'Race'},
    {'type': 'Language', 'key': 'secret-cant', 'source': 'Manual'},
  ]);

  testWidgets('las competencias van en filas con nombre, origen e inventario', (tester) async {
    final inventory = FakeInventoryRepository(
      items: {
        'ch1': [
          // Cruce por índice del catálogo.
          makeCharacterItem(
            id: 'i1',
            templateId: 't-thieves',
            templateIndex: 'thieves-tools',
            effective: makeEffective(name: "Thieves' Tools", category: 'Tool', damageDice: null),
          ),
          // Sin índice (hecho a mano): cruce por nombre.
          makeCharacterItem(
            id: 'i2',
            templateId: null,
            effective: makeEffective(name: 'Lute', category: 'Tool', damageDice: null),
          ),
        ],
      },
    );
    await _pump(tester, character, inventory);

    expect(find.byType(Chip), findsNothing);
    expect(find.text('Armaduras ligeras'), findsOneWidget);
    expect(find.text('Escudos'), findsOneWidget);
    expect(find.text('Armas sencillas'), findsOneWidget);
    expect(find.text('Hand crossbows'), findsOneWidget);
    expect(find.text('thieves-tools'), findsNothing);

    // Herramientas: nombre del catálogo, orígenes juntos, pericia e inventario.
    expect(find.text("Thieves' Tools"), findsOneWidget);
    expect(
      _detail(tester, 'tool-thieves-tools'),
      'Pericia · Origen: clase, trasfondo · En el inventario',
    );
    expect(_detail(tester, 'tool-brewers-supplies'), 'Origen: trasfondo · No la tienes');
    expect(_detail(tester, 'tool-lute'), 'Origen: añadida a mano · En el inventario');
    expect(
      find.descendant(
        of: find.byKey(const Key('proficiency-tool-thieves-tools')),
        matching: find.byIcon(Icons.stars),
      ),
      findsOneWidget,
    );

    // Idiomas y armas también dicen de dónde vienen.
    expect(find.text('Dwarvish'), findsOneWidget);
    expect(_detail(tester, 'language-dwarvish'), 'Origen: raza');
    expect(find.text('Secret cant'), findsOneWidget);
    expect(_detail(tester, 'weapon-hand-crossbows'), 'Origen: raza');
    expect(find.byKey(const Key('proficiency-info-weapon-simple-weapons')), findsNothing);

    // Orden alfabético dentro del grupo.
    final brewer = tester.getTopLeft(find.text("Brewer's Supplies")).dy;
    final lute = tester.getTopLeft(find.text('Lute')).dy;
    final thieves = tester.getTopLeft(find.text("Thieves' Tools")).dy;
    expect(brewer < lute && lute < thieves, isTrue);
  });

  testWidgets('el botón de información muestra la herramienta o el idioma del catálogo', (
    tester,
  ) async {
    await _pump(tester, character, FakeInventoryRepository());

    await tester.tap(find.byKey(const Key('proficiency-info-tool-thieves-tools')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('reference-sheet')), findsOneWidget);
    expect(find.byKey(const Key('reference-sheet-title')), findsOneWidget);
    expect(find.text('A set of picks and small files.'), findsOneWidget);
    Navigator.of(tester.element(find.byKey(const Key('reference-sheet')))).pop();
    await tester.pumpAndSettle();

    // Sin descripción en el catálogo, se dice.
    await tester.tap(find.byKey(const Key('proficiency-info-tool-brewers-supplies')));
    await tester.pumpAndSettle();
    expect(find.text('Sin descripción en el catálogo.'), findsOneWidget);
    Navigator.of(tester.element(find.byKey(const Key('reference-sheet')))).pop();
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('proficiency-info-language-dwarvish')));
    await tester.pumpAndSettle();
    expect(find.text('Script: Dwarvish.'), findsOneWidget);
  });
}
