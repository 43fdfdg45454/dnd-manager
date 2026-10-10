import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_dnd5e/characters/domain/change_details.dart';
import 'package:opentrpg_dnd5e/characters/models.dart';

import 'helpers/app_pump.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';

// Item enhancements and grafts (phase 35): the enhanced entries of the
// summary with their breakdowns, the options dialog and the graft slots.

Map<String, dynamic> _option(
  String index,
  String name, {
  List<String> appliesTo = const ['weapon'],
  int? minLevel,
  List<String> requires = const [],
  bool available = true,
}) => {
  'index': index,
  'name': name,
  'description': ['Texto de ejemplo.'],
  'minLevel': ?minLevel,
  'requiresOptions': requires,
  'available': available,
  'effects': {
    'appliesTo': appliesTo,
    'attackBonus': 1,
    'damageBonus': 0,
    'armorClassBonus': 0,
    'addProperties': <String>[],
    'damageStep': 0,
  },
};

Map<String, dynamic> _feature() => {
  'featureIndex': 'forja-ejemplo-mano',
  'featureName': 'Mano experta',
  'classIndex': 'fighter',
  'classLevel': 3,
  'maxPerItem': 1,
  'options': [
    _option('forja-ejemplo-afilado', 'Afilado'),
    _option(
      'forja-ejemplo-ardiente',
      'Ardiente',
      minLevel: 5,
      requires: ['forja-ejemplo-afilado'],
      available: false,
    ),
    _option('forja-ejemplo-reforzado', 'Reforzado', appliesTo: ['armor', 'shield']),
  ],
};

FakeCharactersRepository _repo({bool feature = true}) =>
    FakeCharactersRepository(characters: [_json(feature: feature)]);

Map<String, dynamic> _json({bool feature = true}) => {
  ...makeCharacterJson(status: 'Active'),
  'itemEnhancementFeature': feature ? _feature() : null,
  'itemEnhancements': [
    {
      'itemId': 'it9',
      'itemName': 'Hacha de ejemplo',
      'options': [
        {'index': 'forja-ejemplo-afilado', 'name': 'Afilado'},
      ],
      'damageDice': '1d10',
      'damageType': 'fire',
      'properties': ['finesse'],
      'breakdowns': {
        'attack': makeBreakdownJson([('item', 'Afilado (Hacha de ejemplo)', 1)]),
      },
    },
  ],
  'grafts': [
    {'itemId': 'g1', 'name': 'Brazo de hierro', 'slot': 'brazos', 'equipped': true},
    {'itemId': 'g2', 'name': 'Brazo de cobre', 'slot': 'brazos', 'equipped': false},
  ],
};

Future<void> _open(WidgetTester tester, FakeCharactersRepository characters) async {
  await pumpRealApp(
    tester,
    location: '/characters/ch1',
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign()]),
      characters: characters,
      inventory: FakeInventoryRepository(
        items: {
          'ch1': [
            makeCharacterItem(id: 'it1'),
            makeCharacterItem(
              id: 'it2',
              templateId: 't-rope',
              effective: makeEffective(
                name: 'Cuerda',
                category: 'AdventuringGear',
                damageDice: null,
              ),
            ),
          ],
        },
      ),
    ),
  );
  await tester.tap(find.byKey(const Key('tab-detail')));
  await tester.pumpAndSettle();
  await tester.ensureVisible(find.byKey(const Key('item-enhancements')));
  await tester.pumpAndSettle();
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

void main() {
  test('los modelos leen mejoras, rasgo e injertos', () {
    final c = CharacterDetail.fromJson(_json());
    expect(c.itemEnhancementFeature!.maxPerItem, 1);
    expect(c.itemEnhancementFeature!.options[1].requiresOptions, ['forja-ejemplo-afilado']);
    expect(c.itemEnhancements.single.breakdowns['attack']!.total, 1);
    expect(c.grafts.map((g) => (g.slot, g.equipped)), [('brazos', true), ('brazos', false)]);
  });

  testWidgets('el objeto mejorado muestra sus mejoras y el origen del bono', (tester) async {
    await _open(tester, _repo());

    expect(find.byKey(const Key('enhanced-item-it9')), findsOneWidget);
    expect(find.text('Hacha de ejemplo'), findsOneWidget);
    expect(find.textContaining('Daño 1d10'), findsOneWidget);
    await _tap(tester, find.byKey(const Key('stat-enhancement.it9.attack')));
    expect(find.text('Afilado (Hacha de ejemplo)'), findsOneWidget);
  });

  testWidgets('"Mejorar objeto" ofrece solo armas y armaduras y respeta los requisitos', (
    tester,
  ) async {
    final characters = _repo();
    await _open(tester, characters);

    await _tap(tester, find.byKey(const Key('item-enhance')));
    expect(find.byKey(const Key('enhance-item-it1')), findsOneWidget);
    expect(find.byKey(const Key('enhance-item-it2')), findsNothing);
    await _tap(tester, find.byKey(const Key('enhance-item-it1')));

    // A weapon: the armor option is not offered; the level 5 one is disabled.
    expect(find.byKey(const Key('enhancement-option-forja-ejemplo-reforzado')), findsNothing);
    final locked = tester.widget<CheckboxListTile>(
      find.byKey(const Key('enhancement-option-forja-ejemplo-ardiente')),
    );
    expect(locked.onChanged, isNull);
    await _tap(tester, find.byKey(const Key('enhancement-option-forja-ejemplo-afilado')));
    expect(find.text('1 de 1'), findsOneWidget);
    await _tap(tester, find.byKey(const Key('enhancement-save')));

    expect(characters.enhancementCalls.single.itemId, 'it1');
    expect(characters.enhancementCalls.single.options, ['forja-ejemplo-afilado']);
    expect(find.text('Mejoras de Longsword guardadas.'), findsOneWidget);
    expect(find.byKey(const Key('enhanced-item-it1')), findsOneWidget);
  });

  testWidgets('quitar todas las mejoras de un objeto', (tester) async {
    final characters = _repo();
    await _open(tester, characters);

    await _tap(tester, find.byKey(const Key('enhanced-item-it9-edit')));
    await _tap(tester, find.byKey(const Key('enhancement-option-forja-ejemplo-afilado')));
    await _tap(tester, find.byKey(const Key('enhancement-save')));

    expect(characters.enhancementCalls.single.itemId, 'it9');
    expect(characters.enhancementCalls.single.options, isEmpty);
    expect(find.byKey(const Key('enhanced-item-it9')), findsNothing);
  });

  testWidgets('sin el rasgo no se puede mejorar; los injertos muestran su zona', (tester) async {
    await _open(tester, _repo(feature: false));

    expect(find.byKey(const Key('item-enhance')), findsNothing);
    expect(find.byKey(const Key('enhanced-item-it9-edit')), findsNothing);
    expect(find.byKey(const Key('graft-g1')), findsOneWidget);
    expect(find.text('Brazo de hierro · brazos · equipado'), findsOneWidget);
    expect(find.text('Brazo de cobre · brazos'), findsOneWidget);
  });

  test('la solicitud de mejora describe el objeto y sus mejoras', () {
    final request = makeChangeRequest(
      type: 'ItemEnhancement',
      payload: {
        'itemId': 'it1',
        'itemName': 'Hacha de ejemplo',
        'options': ['forja-ejemplo-afilado'],
      },
      before: {'itemId': 'it1', 'itemName': 'Hacha de ejemplo', 'options': <String>[]},
    );

    expect(request.type, ChangeRequestType.itemEnhancement);
    expect(request.type.label, 'Mejora de objeto');
    final detail = describeChange(request) as SheetChangeDetail;
    expect(detail.fields.single.label, 'Mejoras de Hacha de ejemplo');
    expect(detail.fields.single.before, '—');
    expect(detail.fields.single.after, 'Forja ejemplo afilado');
  });
}
