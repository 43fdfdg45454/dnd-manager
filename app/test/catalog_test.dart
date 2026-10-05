import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/features/catalog/data/catalog_repository.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/features/catalog/domain/catalog_format.dart';
import 'package:dnd_companion/features/catalog/ui/class_detail_page.dart';
import 'package:dnd_companion/features/catalog/ui/compendium_page.dart';
import 'package:dnd_companion/features/catalog/ui/item_detail_page.dart';
import 'package:dnd_companion/features/catalog/ui/spell_detail_page.dart';
import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'helpers/catalog_fakes.dart';
import 'helpers/fakes.dart';

final _wizard = ClassSummary(
  index: 'wizard',
  name: 'Wizard',
  hitDie: 6,
  isSpellcaster: true,
  spellcastingAbility: 'int',
);

SpellDetail _fireballDetail() => const SpellDetail(
  index: 'fireball',
  name: 'Fireball',
  level: 3,
  school: 'Evocation',
  castingTime: '1 action',
  range: '150 feet',
  components: ['V', 'S', 'M'],
  material: 'a tiny ball of bat guano and sulfur',
  duration: 'Instantaneous',
  classes: ['Sorcerer', 'Wizard'],
  description: [
    'A bright streak flashes from your pointing finger.',
    'The fire spreads around corners.',
  ],
  higherLevel: ['The damage increases by 1d6 for each slot level above 3rd.'],
);

ClassDetail _wizardDetail() => ClassDetail(
  index: 'wizard',
  name: 'Wizard',
  hitDie: 6,
  isSpellcaster: true,
  spellcastingAbility: 'int',
  savingThrows: const ['int', 'wis'],
  levels: [
    const ClassLevel(
      level: 1,
      profBonus: 2,
      cantripsKnown: 3,
      spellSlots: [2, 0, 0, 0, 0, 0, 0, 0, 0],
      features: [
        Feature(
          index: 'arcane-recovery',
          name: 'Arcane Recovery',
          level: 1,
          description: ['You can regain some spell slots.'],
        ),
      ],
    ),
    const ClassLevel(
      level: 2,
      profBonus: 2,
      cantripsKnown: 3,
      spellSlots: [3, 0, 0, 0, 0, 0, 0, 0, 0],
    ),
  ],
);

Widget _scope(FakeCatalogRepository repository, Widget child) => ProviderScope(
  overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
  child: child,
);

FakeCatalogRepository _repository({List<SpellSummary>? spells}) => FakeCatalogRepository(
  spellList:
      spells ??
      [
        makeSpell(),
        makeSpell(index: 'magic-missile', name: 'Magic Missile', level: 1),
        makeSpell(index: 'light', name: 'Light', level: 0, school: 'Evocation'),
      ],
  itemList: [
    makeItem(),
    makeItem(id: 'i2', name: 'Potion of Healing', category: 'Consumable'),
  ],
  classList: [
    _wizard,
    const ClassSummary(index: 'barbarian', name: 'Barbarian', hitDie: 12),
  ],
  classDetails: {'wizard': _wizardDetail()},
  spellDetails: {'fireball': _fireballDetail()},
  raceList: const [RaceSummary(index: 'elf', name: 'Elf', speed: 30, size: 'Medium')],
  conditionList: const [
    Condition(index: 'blinded', name: 'Blinded', description: ['A blinded creature cannot see.']),
  ],
);

Future<void> _pumpCompendium(WidgetTester tester, FakeCatalogRepository repository) async {
  await tester.pumpWidget(_scope(repository, const MaterialApp(home: CompendiumPage())));
  await tester.pumpAndSettle();
}

void main() {
  group('compendio', () {
    testWidgets('la lista de hechizos muestra los resultados', (tester) async {
      await _pumpCompendium(tester, _repository());

      expect(find.text('Fireball'), findsOneWidget);
      expect(find.text('Magic Missile'), findsOneWidget);
      expect(find.text('Nivel 3 · Evocation'), findsOneWidget);
      expect(find.text('Truco · Evocation'), findsOneWidget);
    });

    testWidgets('el pie muestra la atribución', (tester) async {
      await _pumpCompendium(tester, _repository());

      expect(find.byKey(const Key('compendium-attribution')), findsOneWidget);
      expect(find.text(fakeAttributionText), findsOneWidget);
    });

    testWidgets('el filtro por nivel consulta el repositorio con level=3', (tester) async {
      final repository = _repository();
      await _pumpCompendium(tester, repository);
      expect(repository.spellCalls.last.level, isNull);

      await tester.tap(find.byKey(const Key('filter-spell-level')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Nivel 3').last);
      await tester.pumpAndSettle();

      expect(repository.spellCalls.last.level, 3);
      expect(repository.spellCalls.last.page, 1);
      expect(find.text('Fireball'), findsOneWidget);
      expect(find.text('Magic Missile'), findsNothing);
    });

    testWidgets('el filtro por clase consulta con la clase elegida', (tester) async {
      final repository = _repository();
      await _pumpCompendium(tester, repository);

      await tester.tap(find.byKey(const Key('filter-spell-class')));
      await tester.pumpAndSettle();
      // Barbarian is not a caster, so it is not offered.
      expect(find.text('Barbarian'), findsNothing);
      await tester.tap(find.text('Wizard').last);
      await tester.pumpAndSettle();

      expect(repository.spellCalls.last.classIndex, 'wizard');
    });

    testWidgets('la búsqueda espera 300 ms antes de consultar', (tester) async {
      final repository = _repository();
      await _pumpCompendium(tester, repository);
      final calls = repository.spellCalls.length;

      await tester.enterText(find.byKey(const Key('compendium-search')), 'magic');
      await tester.pump(const Duration(milliseconds: 250));
      expect(repository.spellCalls.length, calls);

      await tester.pump(const Duration(milliseconds: 100));
      await tester.pumpAndSettle();
      expect(repository.spellCalls.last.search, 'magic');
      expect(find.text('Magic Missile'), findsOneWidget);
      expect(find.text('Fireball'), findsNothing);
    });

    testWidgets('el scroll infinito pide la página siguiente', (tester) async {
      final repository = _repository(
        spells: [for (var i = 0; i < 120; i++) makeSpell(index: 's$i', name: 'Spell $i')],
      );
      await _pumpCompendium(tester, repository);
      expect(repository.spellCalls.map((c) => c.page), [1]);
      expect(repository.spellCalls.first.pageSize, 50);

      await tester.scrollUntilVisible(
        find.text('Spell 49'),
        300,
        scrollable: find.descendant(
          of: find.byKey(const Key('spell-list')),
          matching: find.byType(Scrollable),
        ),
      );
      await tester.pumpAndSettle();

      expect(repository.spellCalls.map((c) => c.page), [1, 2]);
    });

    testWidgets('la pestaña de objetos filtra por categoría', (tester) async {
      final repository = _repository();
      await _pumpCompendium(tester, repository);

      await tester.tap(find.byKey(const Key('tab-items')));
      await tester.pumpAndSettle();
      expect(find.text('Longsword'), findsOneWidget);
      expect(find.text('Arma · 15 gp'), findsOneWidget);

      await tester.tap(find.byKey(const Key('filter-item-category')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Consumibles').last);
      await tester.pumpAndSettle();

      expect(repository.itemCalls.last.category, 'Consumable');
      expect(find.text('Potion of Healing'), findsOneWidget);
      expect(find.text('Longsword'), findsNothing);
    });

    testWidgets('clases, razas y condiciones se listan y las condiciones abren una hoja', (
      tester,
    ) async {
      await _pumpCompendium(tester, _repository());

      await tester.tap(find.byKey(const Key('tab-classes')));
      await tester.pumpAndSettle();
      expect(find.text('Wizard'), findsOneWidget);
      expect(find.text('Barbarian'), findsOneWidget);

      await tester.tap(find.byKey(const Key('tab-races')));
      await tester.pumpAndSettle();
      expect(find.text('Elf'), findsOneWidget);

      await tester.tap(find.byKey(const Key('tab-conditions')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Blinded'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('condition-sheet')), findsOneWidget);
      expect(find.text('A blinded creature cannot see.'), findsOneWidget);
    });

    testWidgets('un error muestra el mensaje y permite reintentar', (tester) async {
      final repository = _repository()..error = dioError(null);
      await _pumpCompendium(tester, repository);

      expect(find.text(networkMessage), findsWidgets);
      expect(find.text('Reintentar'), findsWidgets);
    });

    testWidgets('tocar un hechizo abre su detalle', (tester) async {
      final router = GoRouter(
        initialLocation: AppRoutes.compendium,
        routes: [
          GoRoute(path: AppRoutes.compendium, builder: (_, _) => const CompendiumPage()),
          GoRoute(
            path: AppRoutes.spellDetail,
            builder: (_, state) => SpellDetailPage(index: state.pathParameters['index']!),
          ),
        ],
      );
      addTearDown(router.dispose);
      await tester.pumpWidget(_scope(_repository(), MaterialApp.router(routerConfig: router)));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('spell-fireball')));
      await tester.pumpAndSettle();

      expect(find.text('The fire spreads around corners.'), findsOneWidget);
    });
  });

  group('detalle', () {
    testWidgets('el hechizo muestra descripción por párrafos y niveles superiores', (tester) async {
      await tester.pumpWidget(
        _scope(_repository(), const MaterialApp(home: SpellDetailPage(index: 'fireball'))),
      );
      await tester.pumpAndSettle();

      expect(find.text('A bright streak flashes from your pointing finger.'), findsOneWidget);
      expect(find.text('The fire spreads around corners.'), findsOneWidget);
      expect(find.text('A niveles superiores'), findsOneWidget);
      expect(find.text('Nivel 3 · Evocation'), findsOneWidget);
      expect(
        find.textContaining('V, S, M (a tiny ball of bat guano and sulfur)', findRichText: true),
        findsOneWidget,
      );
      expect(find.textContaining('Sorcerer, Wizard', findRichText: true), findsOneWidget);
    });

    testWidgets('un hechizo inexistente muestra el error de no encontrado', (tester) async {
      await tester.pumpWidget(
        _scope(_repository(), const MaterialApp(home: SpellDetailPage(index: 'nope'))),
      );
      await tester.pumpAndSettle();

      expect(find.text('No se encontró este elemento del compendio.'), findsOneWidget);
    });

    testWidgets('la tabla de la clase muestra los slots del nivel 1', (tester) async {
      await tester.pumpWidget(
        _scope(_repository(), const MaterialApp(home: ClassDetailPage(index: 'wizard'))),
      );
      await tester.pumpAndSettle();

      expect(find.text('Dado de golpe: d6', findRichText: true), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('slot-1-1'))).data, '2');
      expect(tester.widget<Text>(find.byKey(const Key('slot-2-1'))).data, '3');
      expect(find.text('Inteligencia, Sabiduría', findRichText: true), findsNothing);
      expect(find.textContaining('Inteligencia, Sabiduría', findRichText: true), findsOneWidget);
    });

    testWidgets('los rasgos de la clase se expanden con su descripción', (tester) async {
      await tester.pumpWidget(
        _scope(_repository(), const MaterialApp(home: ClassDetailPage(index: 'wizard'))),
      );
      await tester.pumpAndSettle();

      final feature = find.byKey(const Key('feature-1-arcane-recovery'));
      await tester.scrollUntilVisible(feature, 200, scrollable: find.byType(Scrollable).first);
      await tester.tap(feature);
      await tester.pumpAndSettle();

      expect(find.text('You can regain some spell slots.'), findsOneWidget);
    });

    testWidgets('el objeto muestra daño, propiedades, coste y peso', (tester) async {
      final repository = FakeCatalogRepository(
        itemDetails: {
          'i1': const ItemDetail(
            id: 'i1',
            name: 'Longsword',
            category: 'Weapon',
            subcategory: 'Martial Melee',
            costCp: 1500,
            weightLb: 3,
            damage: ItemDamage(dice: '1d8', type: 'slashing', versatileDice: '1d10'),
            properties: ['Versatile'],
            description: ['A classic blade.'],
          ),
        },
      );
      await tester.pumpWidget(
        _scope(repository, const MaterialApp(home: ItemDetailPage(id: 'i1'))),
      );
      await tester.pumpAndSettle();

      expect(find.textContaining('1d8 slashing', findRichText: true), findsOneWidget);
      expect(find.textContaining('Versatile', findRichText: true), findsOneWidget);
      expect(find.textContaining('15 gp', findRichText: true), findsOneWidget);
      expect(find.textContaining('3 lb', findRichText: true), findsOneWidget);
      expect(find.text('A classic blade.'), findsOneWidget);
    });
  });

  group('formato', () {
    test('formatCostCp usa gp, sp y cp', () {
      expect(formatCostCp(100), '1 gp');
      expect(formatCostCp(50), '5 sp');
      expect(formatCostCp(250), '2 gp 5 sp');
      expect(formatCostCp(1), '1 cp');
      expect(formatCostCp(1500 * 100), '1500 gp');
      expect(formatCostCp(0), '0 cp');
      expect(formatCostCp(null), '—');
    });

    test('formatCostCp puede usar pp y ep', () {
      expect(formatCostCp(1000, allCoins: true), '1 pp');
      expect(formatCostCp(150, allCoins: true), '1 gp 1 ep');
    });
  });

  group('modelos', () {
    test('toleran campos ausentes o nulos', () {
      final spell = SpellDetail.fromJson({'index': 'x', 'name': null, 'level': null});
      expect(spell.level, 0);
      expect(spell.description, isEmpty);
      expect(spell.classes, isEmpty);

      final item = ItemDetail.fromJson({'id': 'i', 'name': 'Rope'});
      expect(item.damage, isNull);
      expect(item.armor, isNull);
      expect(item.costCp, isNull);
    });

    test('la clase resuelve rasgos por índice y rellena slots', () {
      final detail = ClassDetail.fromJson({
        'index': 'wizard',
        'name': 'Wizard',
        'hitDie': 6,
        'savingThrows': ['int', 'wis'],
        'features': [
          {
            'index': 'arcane-recovery',
            'name': 'Arcane Recovery',
            'level': 1,
            'description': ['x'],
          },
        ],
        'levels': [
          {
            'level': 1,
            'profBonus': 2,
            'featureIndexes': ['arcane-recovery'],
            'spellSlots': [2],
          },
        ],
      });
      expect(detail.levels.single.spellSlots, [2, 0, 0, 0, 0, 0, 0, 0, 0]);
      expect(detail.levels.single.features.single.description, ['x']);
      expect(detail.savingThrows, ['int', 'wis']);
    });

    test('el objeto lee daño, armadura y alcance anidados', () {
      final item = ItemDetail.fromJson({
        'id': 'i',
        'name': 'Chain Mail',
        'damage': {'dice': '1d4', 'type': 'piercing'},
        'armor': {'base': 16, 'addDexModifier': false},
        'range': {'normal': 20, 'long': 60},
        'properties': [
          {'index': 'light', 'name': 'Light'},
          'Finesse',
        ],
      });
      expect(item.damage?.type, 'piercing');
      expect(item.armor?.baseAc, 16);
      expect(item.rangeLong, 60);
      expect(item.properties, ['Light', 'Finesse']);
    });

    test('Page reconoce si quedan más elementos', () {
      final page = Page<int>.fromJson({
        'items': [1, 2].map((i) => {'v': i}).toList(),
        'total': 5,
        'page': 1,
        'pageSize': 2,
      }, (json) => json['v'] as int);
      expect(page.items, [1, 2]);
      expect(page.hasMore, isTrue);
    });
  });
}

const networkMessage = 'No se pudo conectar con el servidor. Revisa tu conexión.';
