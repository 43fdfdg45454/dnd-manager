import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/network/api_error.dart';
import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/features/campaigns/data/campaigns_repository.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/campaigns/ui/campaign_detail_page.dart';
import 'package:dnd_companion/features/catalog/data/catalog_repository.dart';
import 'package:dnd_companion/features/catalog/data/models.dart' hide Page;
import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:dnd_companion/features/characters/ui/character_page.dart';
import 'package:dnd_companion/features/items/data/campaign_items_repository.dart';
import 'package:dnd_companion/features/items/data/inventory_repository.dart';
import 'package:dnd_companion/features/items/data/models.dart';
import 'package:dnd_companion/features/items/data/shops_repository.dart';
import 'package:dnd_companion/features/items/domain/item_form_data.dart';
import 'package:dnd_companion/features/items/domain/items_format.dart';
import 'package:dnd_companion/features/items/ui/shop_page.dart';
import 'package:dnd_companion/features/items/ui/transactions_page.dart';
import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';

const _sword = ItemSummary(id: 't-sword', name: 'Longsword', category: 'Weapon', costCp: 1500);
const _rope = ItemSummary(id: 't-rope', name: 'Rope', category: 'AdventuringGear', costCp: 100);

FakeCatalogRepository _catalog() => FakeCatalogRepository(
  itemDetails: {
    't-sword': ItemDetail(
      id: 't-sword',
      name: 'Longsword',
      category: 'Weapon',
      costCp: 1500,
      weightLb: 3,
      damage: ItemDamage(dice: '1d8', type: 'Slashing'),
      properties: ['Versatile'],
    ),
  },
);

Shop _shop({
  String id = 's1',
  String name = 'Armería',
  bool isOpen = true,
  int buyback = 50,
  List<ShopItem> items = const [],
}) => Shop(id: id, name: name, isOpen: isOpen, buybackPercent: buyback, items: items);

ShopItem _shopItem({String id = 'si1', int priceCp = 500, int? stock = 3, String name = 'Rope'}) =>
    ShopItem(
      id: id,
      templateId: 't-rope',
      priceCp: priceCp,
      stock: stock,
      effective: makeEffective(name: name, category: 'AdventuringGear', damageDice: null),
    );

/// App with the routes the items feature navigates between. The signed-in user
/// is `u1`, owner of the Active character `ch1` (Thorin).
Future<void> _pumpApp(
  WidgetTester tester, {
  required String location,
  required FakeInventoryRepository inventory,
  FakeShopsRepository? shops,
  FakeCampaignItemsRepository? campaignItems,
  FakeCatalogRepository? catalog,
  CampaignRole role = CampaignRole.player,
}) async {
  final router = GoRouter(
    initialLocation: location,
    routes: [
      GoRoute(
        path: '/',
        builder: (_, _) => const Scaffold(body: Text('inicio')),
      ),
      GoRoute(
        path: AppRoutes.campaignDetail,
        builder: (_, state) => CampaignDetailPage(campaignId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: AppRoutes.campaignShop,
        builder: (_, state) => ShopPage(
          campaignId: state.pathParameters['id']!,
          shopId: state.pathParameters['shopId']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignTransactions,
        builder: (_, state) => TransactionsPage(campaignId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: AppRoutes.characterDetail,
        builder: (_, state) => CharacterPage(characterId: state.pathParameters['id']!),
      ),
    ],
  );
  addTearDown(router.dispose);
  tester.view.physicalSize = const Size(800, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
        campaignsRepositoryProvider.overrideWithValue(
          FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
        ),
        charactersRepositoryProvider.overrideWithValue(
          FakeCharactersRepository(
            characters: [makeCharacterJson(status: 'Active')],
            isDm: role.isAtLeastDm,
          ),
        ),
        catalogRepositoryProvider.overrideWithValue(catalog ?? _catalog()),
        inventoryRepositoryProvider.overrideWithValue(inventory),
        shopsRepositoryProvider.overrideWithValue(
          shops ?? FakeShopsRepository(inventory: inventory, isDm: role.isAtLeastDm),
        ),
        campaignItemsRepositoryProvider.overrideWithValue(
          campaignItems ?? FakeCampaignItemsRepository(srd: [_sword, _rope]),
        ),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

/// Scrolls long forms to the widget first. Not for widgets inside a TabBarView:
/// revealing them can page to another tab.
Future<void> _tapVisible(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _openInventory(WidgetTester tester) =>
    _tap(tester, find.byKey(const Key('tab-inventory')));

Future<void> _menu(WidgetTester tester, String itemId, String action) async {
  await _tap(tester, find.byKey(Key('inv-menu-$itemId')));
  await tester.tap(find.text(action).last);
  await tester.pumpAndSettle();
}

void main() {
  group('formato y modelos', () {
    test('formatMoney desglosa pp, gp, sp y cp', () {
      expect(formatMoney(0), '0 cp');
      expect(formatMoney(1550), '1 pp 5 gp 5 sp');
      expect(formatMoney(1234), '1 pp 2 gp 3 sp 4 cp');
      expect(formatMoney(-250), '-2 gp 5 sp');
    });

    test('parseGoldToCp acepta coma y signo solo si se permite', () {
      expect(parseGoldToCp('15,5'), 1550);
      expect(parseGoldToCp('-2'), isNull);
      expect(parseGoldToCp('-2', allowNegative: true), -200);
      expect(parseGoldToCp('abc'), isNull);
    });

    test('sellPayoutCp aplica el porcentaje de recompra', () {
      expect(sellPayoutCp(unitCp: 1500, quantity: 1, buybackPercent: 50), 750);
      expect(sellPayoutCp(unitCp: 1500, quantity: 3, buybackPercent: 25), 1125);
    });

    test('ItemOverrides.toJson solo envía los campos definidos', () {
      expect(const ItemOverrides().toJson(), isEmpty);
      const overrides = ItemOverrides(damageDice: '1d10', stealthDisadvantage: false);
      expect(overrides.toJson(), {'damageDice': '1d10', 'stealthDisadvantage': false});
      expect(overrides.definedFields, {'damageDice', 'stealthDisadvantage'});
      expect(ItemOverrides.fromJson({'name': 'X', 'effects': <String>[]}).toJson(), {
        'name': 'X',
        'effects': <String>[],
      });
    });

    test('CharacterItem.fromJson lee overrides y el ítem efectivo', () {
      final item = CharacterItem.fromJson({
        'id': 'i1',
        'templateId': 't1',
        'quantity': 2,
        'equipped': true,
        'overrides': {'damageDice': '1d10'},
        'effective': {
          'name': 'Longsword',
          'category': 'Weapon',
          'rarity': 'VeryRare',
          'damage': {'dice': '1d10', 'type': 'Slashing', 'versatile': '1d12'},
          'armor': {'base': 16, 'addDex': false, 'stealthDisadvantage': true},
          'range': {'normal': 5},
          'attackBonus': 1,
          'effects': ['+1 a ataque y daño'],
        },
        'isCustom': true,
      });
      expect(item.hasOverrides, isTrue);
      expect(item.isMarked, isTrue);
      expect(item.overrides.damageDice, '1d10');
      expect(item.effective.damage!.versatileDice, '1d12');
      expect(item.effective.armor!.baseAc, 16);
      expect(item.effective.armor!.stealthDisadvantage, isTrue);
      expect(item.effective.rangeNormal, 5);
      expect(item.effective.attackBonus, 1);
    });

    test('ItemFormData.toOverrides compara con la plantilla', () {
      const base = ItemFormData(name: 'Longsword', category: 'Weapon', damageDice: '1d8');
      const edited = ItemFormData(
        name: 'Longsword',
        category: 'Weapon',
        damageDice: '1d10',
        attackBonus: 1,
      );
      final overrides = edited.toOverrides(base: base);
      expect(overrides.toJson(), {'damageDice': '1d10', 'attackBonus': 1});
      expect(base.toOverrides(base: base).isEmpty, isTrue);
    });

    test('ItemFormData.toOverrides sin plantilla envía todo lo que tiene valor', () {
      const scratch = ItemFormData(name: 'Daga rara', category: 'Weapon', damageDice: '1d4');
      expect(scratch.toOverrides().toJson(), {
        'name': 'Daga rara',
        'category': 'Weapon',
        'damageDice': '1d4',
      });
      expect(scratch.toInput().toJson()['requiresAttunement'], false);
    });

    test('describeItemError prefiere el detail de un 400', () {
      expect(
        describeItemError(dioProblem(400, 'No tienes suficiente dinero')),
        'No tienes suficiente dinero',
      );
      expect(describeItemError(dioError(400), byStatus: const {400: 'Sin stock'}), 'Sin stock');
      expect(
        describeItemError(dioError(409), byStatus: const {409: 'La tienda está cerrada'}),
        'La tienda está cerrada',
      );
    });
  });

  group('inventario', () {
    testWidgets('muestra dinero, peso, sintonía y los grupos', (tester) async {
      final inventory = FakeInventoryRepository(
        money: {'ch1': 1550},
        items: {
          'ch1': [
            makeCharacterItem(id: 'a', equipped: true, attuned: true),
            makeCharacterItem(
              id: 'b',
              templateId: 't-rope',
              quantity: 3,
              effective: makeEffective(name: 'Rope', category: 'AdventuringGear', damageDice: null),
            ),
            makeCharacterItem(
              id: 'c',
              templateId: 't-potion',
              quantity: 2,
              effective: makeEffective(name: 'Potion', category: 'Consumable', damageDice: null),
            ),
          ],
        },
      );
      await _pumpApp(tester, location: '/characters/ch1', inventory: inventory);
      await _openInventory(tester);

      expect(find.text('1 pp 5 gp 5 sp'), findsOneWidget);
      expect(find.text('Peso 12.5 / 240 lb'), findsOneWidget);
      expect(find.text('Sintonizados 1/3'), findsOneWidget);
      expect(find.text('Equipado (1)'), findsOneWidget);
      expect(find.text('Mochila (1)'), findsOneWidget);
      expect(find.text('Consumibles (1)'), findsOneWidget);
      expect(find.text('×3'), findsOneWidget);
      expect(find.text('Sintonizado'), findsOneWidget);
    });

    testWidgets('un ítem con override muestra el marcador y el detalle marca el campo', (
      tester,
    ) async {
      final inventory = FakeInventoryRepository(
        items: {
          'ch1': [
            makeCharacterItem(
              id: 'mod',
              overrides: const ItemOverrides(damageDice: '1d10'),
              effective: makeEffective(name: 'Longsword +1', damageDice: '1d10'),
            ),
            makeCharacterItem(id: 'plain', templateId: 't-dagger'),
          ],
        },
      );
      await _pumpApp(tester, location: '/characters/ch1', inventory: inventory);
      await _openInventory(tester);

      expect(find.byKey(const Key('inv-mark-mod')), findsOneWidget);
      expect(find.byKey(const Key('inv-mark-plain')), findsNothing);

      await _menu(tester, 'mod', 'Ver detalle');
      expect(find.byKey(const Key('effective-title')), findsOneWidget);
      expect(find.byKey(const Key('override-mark-damageDice')), findsOneWidget);
      expect(find.byKey(const Key('override-mark-rarity')), findsNothing);
      expect(find.textContaining('1d10 Slashing'), findsOneWidget);
    });

    testWidgets('un ítem sin plantilla se marca como personalizado', (tester) async {
      final inventory = FakeInventoryRepository(
        items: {
          'ch1': [makeCharacterItem(id: 'custom', templateId: null)],
        },
      );
      await _pumpApp(tester, location: '/characters/ch1', inventory: inventory);
      await _openInventory(tester);
      expect(find.byKey(const Key('inv-mark-custom')), findsOneWidget);
    });

    testWidgets('equipar, sintonizar y usar llaman al servidor', (tester) async {
      final inventory = FakeInventoryRepository(
        items: {
          'ch1': [
            makeCharacterItem(
              id: 'ring',
              templateId: 't-ring',
              effective: makeEffective(
                name: 'Ring',
                category: 'MagicItem',
                damageDice: null,
                requiresAttunement: true,
              ),
            ),
            makeCharacterItem(
              id: 'pot',
              templateId: 't-potion',
              quantity: 2,
              effective: makeEffective(name: 'Potion', category: 'Consumable', damageDice: null),
            ),
          ],
        },
      );
      await _pumpApp(tester, location: '/characters/ch1', inventory: inventory);
      await _openInventory(tester);

      await _menu(tester, 'ring', 'Equipar');
      expect(inventory.patches.single.patch.equipped, isTrue);
      expect(find.text('Objeto equipado.'), findsOneWidget);
      expect(find.text('Equipado (1)'), findsOneWidget);

      await _menu(tester, 'ring', 'Sintonizar');
      expect(inventory.patches.last.patch.attuned, isTrue);
      expect(find.text('Sintonizados 1/3'), findsOneWidget);

      await _menu(tester, 'pot', 'Usar');
      expect(inventory.used, ['pot']);
      expect(find.byKey(const Key('inv-qty-pot')), findsOneWidget);
      expect(find.text('×1'), findsWidgets);
    });

    testWidgets('quitar con 202 muestra el mensaje de aprobación', (tester) async {
      final inventory = FakeInventoryRepository(
        requiresApproval: true,
        items: {
          'ch1': [makeCharacterItem(id: 'a')],
        },
      );
      await _pumpApp(tester, location: '/characters/ch1', inventory: inventory);
      await _openInventory(tester);

      await _menu(tester, 'a', 'Quitar');
      await _tap(tester, find.byKey(const Key('confirm-action')));
      expect(inventory.removed.single.itemId, 'a');
      expect(find.text('Enviado al DM para aprobación'), findsOneWidget);
      expect(find.byKey(const Key('inv-item-a')), findsOneWidget);
    });

    testWidgets('editar dinero envía el delta en cobre; un jugador Active ve el aviso', (
      tester,
    ) async {
      final inventory = FakeInventoryRepository(money: {'ch1': 1000}, requiresApproval: true);
      await _pumpApp(tester, location: '/characters/ch1', inventory: inventory);
      await _openInventory(tester);

      await _tap(tester, find.byKey(const Key('inventory-money-edit')));
      await tester.enterText(find.byKey(const Key('money-delta')), '-2,5');
      await tester.enterText(find.byKey(const Key('money-reason')), 'Limosna');
      await _tap(tester, find.byKey(const Key('money-submit')));

      expect(inventory.moneyChanges.single, (deltaCp: -250, reason: 'Limosna'));
      expect(find.textContaining('Enviado al DM'), findsOneWidget);
    });

    testWidgets('el DM aplica el ajuste de dinero directamente', (tester) async {
      final inventory = FakeInventoryRepository(money: {'ch1': 1000});
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        inventory: inventory,
        role: CampaignRole.dm,
      );
      await _openInventory(tester);

      await _tap(tester, find.byKey(const Key('inventory-money-edit')));
      await tester.enterText(find.byKey(const Key('money-delta')), '5');
      await tester.enterText(find.byKey(const Key('money-reason')), 'Recompensa');
      await _tap(tester, find.byKey(const Key('money-submit')));

      expect(find.text('Dinero actualizado.'), findsOneWidget);
      expect(find.text('1 pp 5 gp'), findsOneWidget);
    });
  });

  group('añadir ítem', () {
    testWidgets('un jugador Active añade rápido y ve el mensaje de aprobación', (tester) async {
      final inventory = FakeInventoryRepository(requiresApproval: true);
      await _pumpApp(tester, location: '/characters/ch1', inventory: inventory);
      await _openInventory(tester);

      await _tap(tester, find.byKey(const Key('inventory-add')));
      expect(find.text('Añadir objeto'), findsWidgets);
      await _tap(tester, find.byKey(const Key('item-t-sword')));
      await tester.enterText(find.byKey(const Key('quick-quantity')), '2');
      await tester.pump();
      await _tap(tester, find.byKey(const Key('quick-add')));

      expect(inventory.added.single.templateId, 't-sword');
      expect(inventory.added.single.quantity, 2);
      expect(inventory.added.single.overrides.isEmpty, isTrue);
      expect(find.text('Enviado al DM para aprobación'), findsOneWidget);
      // Back on the inventory tab.
      expect(find.byKey(const Key('inventory-add')), findsOneWidget);
    });

    testWidgets('el DM añade directo: el ítem aparece en el inventario', (tester) async {
      final inventory = FakeInventoryRepository(
        templates: {'t-sword': makeEffective(name: 'Longsword')},
      );
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        inventory: inventory,
        role: CampaignRole.dm,
      );
      await _openInventory(tester);

      await _tap(tester, find.byKey(const Key('inventory-add')));
      await _tap(tester, find.byKey(const Key('item-t-sword')));
      await _tap(tester, find.byKey(const Key('quick-add')));

      expect(find.text('Objeto añadido al inventario.'), findsOneWidget);
      expect(find.text('Longsword'), findsOneWidget);
    });

    testWidgets('el filtro de origen pide SRD o Campaña', (tester) async {
      final inventory = FakeInventoryRepository();
      final items = FakeCampaignItemsRepository(
        srd: [_sword],
        homebrew: [const ItemSummary(id: 'hb', name: 'Espada de la mina', category: 'Weapon')],
      );
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        inventory: inventory,
        campaignItems: items,
      );
      await _openInventory(tester);
      await _tap(tester, find.byKey(const Key('inventory-add')));
      expect(find.text('Espada de la mina'), findsOneWidget);

      await _tap(tester, find.text('SRD'));
      expect(items.sources.last, ItemSource.srd);
      expect(find.text('Espada de la mina'), findsNothing);

      await _tap(tester, find.text('Campaña'));
      expect(items.sources.last, ItemSource.homebrew);
      expect(find.text('Longsword'), findsNothing);
      expect(find.text('Espada de la mina'), findsOneWidget);
    });

    testWidgets('avanzado con plantilla solo envía los campos cambiados', (tester) async {
      final inventory = FakeInventoryRepository();
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        inventory: inventory,
        role: CampaignRole.dm,
      );
      await _openInventory(tester);
      await _tap(tester, find.byKey(const Key('inventory-add')));
      await _tap(tester, find.byKey(const Key('tab-add-advanced')));

      await _tap(tester, find.byKey(const Key('composer-pick')));
      await _tap(tester, find.byKey(const Key('item-t-sword')));

      // The form is preloaded from the template.
      String textOf(String key) =>
          tester.widget<TextFormField>(find.byKey(Key(key))).controller!.text;
      expect(textOf('item-form-name'), 'Longsword');
      expect(textOf('item-form-damage-dice'), '1d8');
      expect(textOf('item-form-weight'), '3');

      await tester.ensureVisible(find.byKey(const Key('item-form-damage-dice')));
      await tester.enterText(find.byKey(const Key('item-form-damage-dice')), '1d10');
      await _tapVisible(tester, find.byKey(const Key('composer-submit')));

      final added = inventory.added.single;
      expect(added.templateId, 't-sword');
      expect(added.overrides.toJson(), {'damageDice': '1d10'});
    });

    testWidgets('avanzado desde cero envía el nombre y la categoría', (tester) async {
      final inventory = FakeInventoryRepository();
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        inventory: inventory,
        role: CampaignRole.dm,
      );
      await _openInventory(tester);
      await _tap(tester, find.byKey(const Key('inventory-add')));
      await _tap(tester, find.byKey(const Key('tab-add-advanced')));

      // Without a name the form does not validate.
      await _tapVisible(tester, find.byKey(const Key('composer-submit')));
      expect(find.text('Escribe un nombre'), findsOneWidget);
      expect(inventory.added, isEmpty);

      await tester.enterText(find.byKey(const Key('item-form-name')), 'Daga rara');
      await _tapVisible(tester, find.byKey(const Key('composer-submit')));

      final added = inventory.added.single;
      expect(added.templateId, isNull);
      expect(added.overrides.name, 'Daga rara');
      expect(added.overrides.category, 'AdventuringGear');
    });
  });

  group('tiendas', () {
    FakeShopsRepository shopsOf(
      FakeInventoryRepository inventory, {
      bool isDm = false,
      List<Shop>? shops,
    }) => FakeShopsRepository(
      inventory: inventory,
      isDm: isDm,
      costByTemplate: const {'t-sword': 1500},
      shops:
          shops ??
          [
            _shop(items: [_shopItem()]),
          ],
    );

    testWidgets('comprar descuenta el dinero mostrado y el stock', (tester) async {
      final inventory = FakeInventoryRepository(money: {'ch1': 1550});
      await _pumpApp(
        tester,
        location: '/campaigns/c1/shops/s1',
        inventory: inventory,
        shops: shopsOf(inventory),
      );

      expect(find.text('Rope'), findsOneWidget);
      expect(find.byKey(const Key('shop-item-info-si1')), findsOneWidget);
      expect(find.text('5 gp · Stock 3'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('shop-buy-si1')));
      expect(find.text('Dinero disponible: 1 pp 5 gp 5 sp'), findsOneWidget);
      expect(find.text('Total: 5 gp'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('buy-plus')));
      expect(find.text('Total: 10 gp'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('buy-minus')));
      await _tap(tester, find.byKey(const Key('buy-confirm')));

      expect(find.textContaining('Compra realizada: Rope ×1 por 5 gp'), findsOneWidget);
      expect(find.text('Dinero de Thorin: 1 pp 5 sp'), findsOneWidget);
      expect(find.text('5 gp · Stock 2'), findsOneWidget);
      expect(inventory.money['ch1'], 1050);
    });

    testWidgets('una tienda ilimitada muestra ∞', (tester) async {
      final inventory = FakeInventoryRepository(money: {'ch1': 1550});
      await _pumpApp(
        tester,
        location: '/campaigns/c1/shops/s1',
        inventory: inventory,
        shops: shopsOf(
          inventory,
          shops: [
            _shop(items: [_shopItem(stock: null)]),
          ],
        ),
      );
      expect(find.text('5 gp · Stock ∞'), findsOneWidget);
    });

    testWidgets('comprar sin dinero muestra el detail del servidor', (tester) async {
      final inventory = FakeInventoryRepository(money: {'ch1': 100});
      await _pumpApp(
        tester,
        location: '/campaigns/c1/shops/s1',
        inventory: inventory,
        shops: shopsOf(inventory),
      );
      await _tap(tester, find.byKey(const Key('shop-buy-si1')));
      expect(find.byKey(const Key('buy-insufficient')), findsOneWidget);
      await _tap(tester, find.byKey(const Key('buy-confirm')));

      expect(find.text('No tienes suficiente dinero'), findsOneWidget);
      expect(inventory.money['ch1'], 100);
    });

    testWidgets('comprar en una tienda cerrada muestra "La tienda está cerrada"', (tester) async {
      final inventory = FakeInventoryRepository(money: {'ch1': 1550});
      final shops = shopsOf(inventory)..buyError = dioError(409);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/shops/s1',
        inventory: inventory,
        shops: shops,
      );
      await _tap(tester, find.byKey(const Key('shop-buy-si1')));
      await _tap(tester, find.byKey(const Key('buy-confirm')));
      expect(find.text('La tienda está cerrada'), findsOneWidget);
    });

    testWidgets('el DM ve el interruptor de la tienda y el jugador no', (tester) async {
      final inventory = FakeInventoryRepository();
      final dmShops = shopsOf(
        inventory,
        isDm: true,
        shops: [
          _shop(),
          _shop(id: 's2', name: 'Botica', isOpen: false),
        ],
      );
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        inventory: inventory,
        shops: dmShops,
        role: CampaignRole.dm,
      );
      await _tap(tester, find.byKey(const Key('tab-shops')));
      expect(find.byKey(const Key('shop-switch-s1')), findsOneWidget);
      expect(find.byKey(const Key('shop-switch-s2')), findsOneWidget);
      expect(find.byKey(const Key('shops-new')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('shop-switch-s2')));
      expect(dmShops.toggles.single, (shopId: 's2', isOpen: true));
      expect(tester.widget<Switch>(find.byKey(const Key('shop-switch-s2'))).value, isTrue);
    });

    testWidgets('el jugador solo ve las tiendas abiertas y sin interruptor', (tester) async {
      final inventory = FakeInventoryRepository();
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        inventory: inventory,
        shops: shopsOf(
          inventory,
          shops: [
            _shop(),
            _shop(id: 's2', name: 'Botica', isOpen: false),
          ],
        ),
      );
      await _tap(tester, find.byKey(const Key('tab-shops')));
      expect(find.text('Armería'), findsOneWidget);
      expect(find.text('Botica'), findsNothing);
      expect(find.byType(Switch), findsNothing);
      expect(find.byKey(const Key('shops-new')), findsNothing);
    });

    testWidgets('el DM crea una tienda', (tester) async {
      final inventory = FakeInventoryRepository();
      final dmShops = shopsOf(inventory, isDm: true, shops: []);
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        inventory: inventory,
        shops: dmShops,
        role: CampaignRole.dm,
      );
      await _tap(tester, find.byKey(const Key('tab-shops')));
      await _tap(tester, find.byKey(const Key('shops-new')));
      await tester.enterText(find.byKey(const Key('shop-name')), 'Herrería');
      await tester.enterText(find.byKey(const Key('shop-buyback')), '40');
      await _tap(tester, find.byKey(const Key('shop-form-submit')));

      expect(find.text('Tienda creada.'), findsOneWidget);
      expect(dmShops.shops.single.name, 'Herrería');
      expect(dmShops.shops.single.buybackPercent, 40);
    });

    testWidgets('el DM añade un ítem a la tienda con precio y stock', (tester) async {
      final inventory = FakeInventoryRepository();
      final dmShops = shopsOf(inventory, isDm: true, shops: [_shop()]);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/shops/s1',
        inventory: inventory,
        shops: dmShops,
        role: CampaignRole.dm,
      );
      expect(find.byKey(const Key('shop-open-switch')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('shop-add-item')));
      await tester.enterText(find.byKey(const Key('item-form-name')), 'Antorcha');
      await tester.enterText(find.byKey(const Key('composer-price')), '0,5');
      await tester.enterText(find.byKey(const Key('composer-stock')), '10');
      await _tapVisible(tester, find.byKey(const Key('composer-submit')));

      final item = dmShops.shops.single.items.single;
      expect(item.priceCp, 50);
      expect(item.stock, 10);
      expect(find.text('Objeto añadido a la tienda.'), findsOneWidget);
      expect(find.text('Antorcha'), findsOneWidget);
    });

    testWidgets('vender muestra el importe calculado según la recompra', (tester) async {
      final inventory = FakeInventoryRepository(
        money: {'ch1': 100},
        items: {
          'ch1': [makeCharacterItem(id: 'sw', quantity: 2)],
        },
      );
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        inventory: inventory,
        shops: shopsOf(inventory),
      );
      await _openInventory(tester);

      await _menu(tester, 'sw', 'Vender');
      expect(find.byKey(const Key('sell-payout')), findsOneWidget);
      expect(find.textContaining('Recibirás 7 gp 5 sp'), findsOneWidget);
      expect(find.textContaining('50 % de 15 gp'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('sell-plus')));
      expect(find.textContaining('Recibirás 15 gp'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('sell-minus')));
      await _tap(tester, find.byKey(const Key('sell-confirm')));

      expect(find.textContaining('Venta realizada: recibes 7 gp 5 sp'), findsOneWidget);
      expect(inventory.money['ch1'], 850);
      expect(find.text('8 gp 5 sp'), findsOneWidget);
      expect(find.text('×1'), findsOneWidget);
    });

    testWidgets('vender sin tiendas abiertas lo indica', (tester) async {
      final inventory = FakeInventoryRepository(
        items: {
          'ch1': [makeCharacterItem(id: 'sw')],
        },
      );
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        inventory: inventory,
        shops: shopsOf(inventory, shops: [_shop(isOpen: false)]),
      );
      await _openInventory(tester);
      await _menu(tester, 'sw', 'Vender');
      expect(find.byKey(const Key('sell-no-shops')), findsOneWidget);
      expect(tester.widget<FilledButton>(find.byKey(const Key('sell-confirm'))).onPressed, isNull);
    });

    testWidgets('un ítem sintonizado no se puede vender', (tester) async {
      final inventory = FakeInventoryRepository(
        items: {
          'ch1': [makeCharacterItem(id: 'sw', attuned: true)],
        },
      );
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        inventory: inventory,
        shops: shopsOf(inventory),
      );
      await _openInventory(tester);
      await _menu(tester, 'sw', 'Vender');
      expect(find.byKey(const Key('sell-attuned')), findsOneWidget);
      expect(tester.widget<FilledButton>(find.byKey(const Key('sell-confirm'))).onPressed, isNull);
    });

    testWidgets('las transacciones se listan con su importe', (tester) async {
      final inventory = FakeInventoryRepository(money: {'ch1': 1550});
      final shops = shopsOf(inventory);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/shops/s1',
        inventory: inventory,
        shops: shops,
      );
      await _tap(tester, find.byKey(const Key('shop-buy-si1')));
      await _tap(tester, find.byKey(const Key('buy-confirm')));

      await _tap(tester, find.byKey(const Key('shop-transactions')));
      expect(find.text('Rope ×1'), findsOneWidget);
      expect(find.text('-5 gp'), findsOneWidget);
      expect(find.textContaining('Thorin · Armería'), findsOneWidget);
    });
  });

  group('objetos de la campaña', () {
    testWidgets('la pestaña lista los objetos SRD y de campaña con el conmutador visible', (
      tester,
    ) async {
      final inventory = FakeInventoryRepository();
      final items = FakeCampaignItemsRepository(
        srd: [const ItemSummary(id: 'srd1', name: 'Dagger', category: 'Weapon', source: 'srd')],
        homebrew: [
          const ItemSummary(id: 'hb1', name: 'Amuleto', category: 'MagicItem', source: 'homebrew'),
        ],
      );
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        inventory: inventory,
        campaignItems: items,
        role: CampaignRole.dm,
      );
      await _tap(tester, find.byKey(const Key('tab-objects')));

      expect(find.byKey(const Key('item-source')), findsOneWidget);
      expect(items.sources.first, ItemSource.all);
      expect(find.text('Dagger'), findsOneWidget);
      expect(find.text('Amuleto'), findsOneWidget);
      // SRD items are read-only: only the campaign's own item has the menu.
      expect(find.byKey(const Key('homebrew-menu-srd1')), findsNothing);
      expect(find.byKey(const Key('homebrew-menu-hb1')), findsOneWidget);

      await _tap(tester, find.text('Campaña'));
      expect(items.sources.last, ItemSource.homebrew);
      expect(find.text('Dagger'), findsNothing);
      expect(find.text('Amuleto'), findsOneWidget);
    });

    testWidgets('el DM crea un objeto con el formulario avanzado', (tester) async {
      final inventory = FakeInventoryRepository();
      final items = FakeCampaignItemsRepository();
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        inventory: inventory,
        campaignItems: items,
        role: CampaignRole.dm,
      );
      await _tap(tester, find.byKey(const Key('tab-objects')));
      expect(find.text('No hay objetos que coincidan.'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('homebrew-new')));
      await tester.enterText(find.byKey(const Key('item-form-name')), 'Espada de la mina');
      await tester.enterText(find.byKey(const Key('item-form-cost')), '25');
      await _tapVisible(tester, find.byKey(const Key('homebrew-save')));

      expect(items.created.single.name, 'Espada de la mina');
      expect(items.created.single.costCp, 2500);
      expect(find.text('Objeto creado.'), findsOneWidget);
      expect(find.text('Espada de la mina'), findsOneWidget);
    });

    testWidgets('borrar un objeto en uso (409) muestra "Está en uso"', (tester) async {
      final inventory = FakeInventoryRepository();
      final items = FakeCampaignItemsRepository(
        homebrew: [const ItemSummary(id: 'hb1', name: 'Amuleto', category: 'MagicItem')],
      )..deleteError = dioError(409);
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        inventory: inventory,
        campaignItems: items,
        role: CampaignRole.dm,
      );
      await _tap(tester, find.byKey(const Key('tab-objects')));
      await _tap(tester, find.byKey(const Key('homebrew-menu-hb1')));
      await tester.tap(find.text('Borrar').last);
      await tester.pumpAndSettle();
      await _tap(tester, find.byKey(const Key('confirm-action')));

      expect(find.textContaining('Está en uso'), findsOneWidget);
      expect(items.deleted, isEmpty);
    });

    testWidgets('un jugador ve la lista pero no crea ni edita', (tester) async {
      final inventory = FakeInventoryRepository();
      final items = FakeCampaignItemsRepository(
        homebrew: [const ItemSummary(id: 'hb1', name: 'Amuleto', category: 'MagicItem')],
      );
      await _pumpApp(tester, location: '/campaigns/c1', inventory: inventory, campaignItems: items);
      await _tap(tester, find.byKey(const Key('tab-objects')));
      expect(find.text('Amuleto'), findsOneWidget);
      expect(find.byKey(const Key('homebrew-new')), findsNothing);
      expect(find.byKey(const Key('homebrew-menu-hb1')), findsNothing);
    });
  });
}
