import 'package:dio/dio.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/features/items/data/campaign_items_repository.dart';
import 'package:dnd_companion/features/items/data/inventory_repository.dart';
import 'package:dnd_companion/features/items/data/models.dart';
import 'package:dnd_companion/features/items/data/shops_repository.dart';
import 'package:dnd_companion/features/items/domain/items_format.dart';

import 'character_fakes.dart';
import 'fakes.dart';

/// A 400 response with a ProblemDetails body.
DioException dioProblem(int status, String detail) {
  final options = RequestOptions(path: '/test');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response<dynamic>(
      requestOptions: options,
      statusCode: status,
      data: {'title': 'Error', 'status': status, 'detail': detail},
    ),
  );
}

EffectiveItem makeEffective({
  String name = 'Longsword',
  String category = 'Weapon',
  String? damageDice = '1d8',
  bool requiresAttunement = false,
  List<ItemModifier> modifiers = const [],
}) => EffectiveItem(
  name: name,
  category: category,
  requiresAttunement: requiresAttunement,
  modifiers: modifiers,
  damage: damageDice == null ? null : ItemDamage(dice: damageDice, type: 'Slashing'),
);

CharacterItem makeCharacterItem({
  String id = 'it1',
  String? templateId = 't-sword',
  int quantity = 1,
  bool equipped = false,
  bool attuned = false,
  int? charges,
  ItemOverrides overrides = const ItemOverrides(),
  EffectiveItem? effective,
}) => CharacterItem(
  id: id,
  templateId: templateId,
  quantity: quantity,
  equipped: equipped,
  attuned: attuned,
  charges: charges,
  chargesMax: charges,
  overrides: overrides,
  effective: effective ?? makeEffective(),
  isCustom: templateId == null || !overrides.isEmpty,
);

/// In-memory inventories. With [requiresApproval] the writes that need the DM
/// (add, remove, money) answer 202 and change nothing, like an Active player.
class FakeInventoryRepository implements InventoryRepository {
  FakeInventoryRepository({
    Map<String, List<CharacterItem>> items = const {},
    Map<String, int> money = const {},
    this.requiresApproval = false,
    this.templates = const {},
  }) : items = {
         for (final e in items.entries) e.key: [...e.value],
       },
       money = {...money};

  final Map<String, List<CharacterItem>> items;
  final Map<String, int> money;
  final bool requiresApproval;

  /// Effective item by template id, used when an item is added.
  final Map<String, EffectiveItem> templates;
  Object? error;

  final List<({String? templateId, int quantity, ItemOverrides overrides})> added = [];
  final List<({String itemId, InventoryPatch patch})> patches = [];
  final List<String> used = [];
  final List<({String itemId, int? quantity})> removed = [];
  final List<({int deltaCp, String reason})> moneyChanges = [];

  void _fail() {
    if (error != null) throw error!;
  }

  List<CharacterItem> _of(String id) => items.putIfAbsent(id, () => []);

  Inventory snapshot(String characterId) {
    final list = _of(characterId);
    return Inventory(
      items: [...list],
      copperPieces: money[characterId] ?? 0,
      totalWeightLb: 12.5,
      carryCapacityLb: 240,
      attunedCount: list.where((i) => i.attuned).length,
    );
  }

  /// Puts an item in an inventory (used by the shop fake when buying).
  void give(String characterId, CharacterItem item) => _of(characterId).add(item);

  @override
  Future<Inventory> get(String characterId) async {
    _fail();
    return snapshot(characterId);
  }

  @override
  Future<InventoryWriteResult> add(
    String characterId, {
    String? templateId,
    int quantity = 1,
    ItemOverrides overrides = const ItemOverrides(),
  }) async {
    _fail();
    added.add((templateId: templateId, quantity: quantity, overrides: overrides));
    if (requiresApproval) {
      return InventoryPending(makeChangeRequest(type: 'AddItem', payload: const {}));
    }
    final base = templateId == null ? null : templates[templateId];
    _of(characterId).add(
      makeCharacterItem(
        id: 'new${added.length}',
        templateId: templateId,
        quantity: quantity,
        overrides: overrides,
        effective: base ?? makeEffective(name: overrides.name ?? 'Objeto'),
      ),
    );
    return const InventoryApplied();
  }

  @override
  Future<CharacterItem> patch(String characterId, String itemId, InventoryPatch patch) async {
    _fail();
    patches.add((itemId: itemId, patch: patch));
    final list = _of(characterId);
    final index = list.indexWhere((i) => i.id == itemId);
    if (index < 0) throw dioError(404);
    if (patch.attuned == true && list.where((i) => i.attuned).length >= maxAttunedItems) {
      throw dioError(400);
    }
    list[index] = list[index].copyWith(
      equipped: patch.equipped,
      attuned: patch.attuned,
      notes: patch.notes,
    );
    return list[index];
  }

  @override
  Future<void> use(String characterId, String itemId, {int amount = 1}) async {
    _fail();
    used.add(itemId);
    final list = _of(characterId);
    final index = list.indexWhere((i) => i.id == itemId);
    if (index < 0) throw dioError(404);
    final left = list[index].quantity - amount;
    if (left <= 0) {
      list.removeAt(index);
    } else {
      list[index] = list[index].copyWith(quantity: left);
    }
  }

  @override
  Future<InventoryWriteResult> remove(String characterId, String itemId, {int? quantity}) async {
    _fail();
    removed.add((itemId: itemId, quantity: quantity));
    if (requiresApproval) {
      return InventoryPending(makeChangeRequest(type: 'RemoveItem', payload: const {}));
    }
    final list = _of(characterId);
    final index = list.indexWhere((i) => i.id == itemId);
    if (index < 0) throw dioError(404);
    final left = quantity == null ? 0 : list[index].quantity - quantity;
    if (left <= 0) {
      list.removeAt(index);
    } else {
      list[index] = list[index].copyWith(quantity: left);
    }
    return const InventoryApplied();
  }

  @override
  Future<InventoryWriteResult> adjustMoney(
    String characterId, {
    required int deltaCp,
    required String reason,
  }) async {
    _fail();
    moneyChanges.add((deltaCp: deltaCp, reason: reason));
    if (requiresApproval) {
      return InventoryPending(makeChangeRequest(type: 'AdjustMoney', payload: const {}));
    }
    money[characterId] = (money[characterId] ?? 0) + deltaCp;
    return const InventoryApplied();
  }
}

/// In-memory item catalog of a campaign, SRD and homebrew apart.
class FakeCampaignItemsRepository implements CampaignItemsRepository {
  FakeCampaignItemsRepository({this.srd = const [], List<ItemSummary> homebrew = const []})
    : homebrew = [...homebrew];

  final List<ItemSummary> srd;
  final List<ItemSummary> homebrew;
  Object? deleteError;
  final List<ItemSource> sources = [];
  final List<ItemTemplateInput> created = [];
  final List<({String id, ItemTemplateInput input})> updated = [];
  final List<String> deleted = [];

  @override
  Future<Page<ItemSummary>> list(
    String campaignId, {
    String? search,
    String? category,
    String? rarity,
    ItemSource source = ItemSource.all,
    int page = 1,
    int pageSize = 30,
  }) async {
    sources.add(source);
    final q = (search ?? '').toLowerCase();
    final all = [
      if (source != ItemSource.homebrew) ...srd,
      if (source != ItemSource.srd) ...homebrew,
    ].where((i) => i.name.toLowerCase().contains(q)).toList();
    return Page(items: all, total: all.length, page: page, pageSize: pageSize);
  }

  @override
  Future<ItemDetail> create(String campaignId, ItemTemplateInput input) async {
    created.add(input);
    final id = 'hb${created.length}';
    homebrew.add(ItemSummary(id: id, name: input.name, category: input.category));
    return ItemDetail(id: id, name: input.name, category: input.category);
  }

  @override
  Future<ItemDetail> update(String campaignId, String templateId, ItemTemplateInput input) async {
    updated.add((id: templateId, input: input));
    final index = homebrew.indexWhere((i) => i.id == templateId);
    homebrew[index] = ItemSummary(id: templateId, name: input.name, category: input.category);
    return ItemDetail(id: templateId, name: input.name, category: input.category);
  }

  @override
  Future<void> delete(String campaignId, String templateId) async {
    if (deleteError != null) throw deleteError!;
    deleted.add(templateId);
    homebrew.removeWhere((i) => i.id == templateId);
  }
}

/// In-memory shops. Trades move money and items in [inventory] like the server.
class FakeShopsRepository implements ShopsRepository {
  FakeShopsRepository({
    required this.inventory,
    List<Shop> shops = const [],
    this.isDm = false,
    this.costByTemplate = const {},
  }) : shops = [...shops];

  final FakeInventoryRepository inventory;
  final List<Shop> shops;
  final bool isDm;

  /// Template cost in copper, the base of a sale.
  final Map<String, int> costByTemplate;
  Object? buyError;
  final List<Transaction> history = [];
  final List<({String shopId, bool isOpen})> toggles = [];
  final List<({String shopId, String shopItemId, int quantity})> buys = [];

  Shop _shop(String id) => shops.firstWhere((s) => s.id == id, orElse: () => throw dioError(404));

  void _replace(Shop shop) => shops[shops.indexWhere((s) => s.id == shop.id)] = shop;

  @override
  Future<List<ShopSummary>> list(String campaignId) async => [
    for (final s in shops)
      if (isDm || s.isOpen)
        ShopSummary(
          id: s.id,
          name: s.name,
          description: s.description,
          isOpen: s.isOpen,
          buybackPercent: s.buybackPercent,
          itemCount: s.items.length,
        ),
  ];

  @override
  Future<ShopSummary> create(
    String campaignId, {
    required String name,
    String? description,
    int? buybackPercent,
  }) async {
    final shop = Shop(
      id: 's${shops.length + 1}',
      name: name,
      description: description,
      buybackPercent: buybackPercent ?? 50,
    );
    shops.add(shop);
    return shop;
  }

  @override
  Future<Shop> get(String shopId) async {
    final shop = _shop(shopId);
    if (!isDm && !shop.isOpen) throw dioError(404);
    return shop;
  }

  @override
  Future<ShopSummary> update(String shopId, ShopPatch patch) async {
    final shop = _shop(shopId);
    if (patch.isOpen != null) toggles.add((shopId: shopId, isOpen: patch.isOpen!));
    final updated = Shop(
      id: shop.id,
      name: patch.name ?? shop.name,
      description: patch.description ?? shop.description,
      isOpen: patch.isOpen ?? shop.isOpen,
      buybackPercent: patch.buybackPercent ?? shop.buybackPercent,
      items: shop.items,
    );
    _replace(updated);
    return updated;
  }

  @override
  Future<void> delete(String shopId) async => shops.removeWhere((s) => s.id == shopId);

  @override
  Future<ShopItem> addItem(String shopId, ShopItemInput input) async {
    final shop = _shop(shopId);
    final item = ShopItem(
      id: 'si${shop.items.length + 1}',
      templateId: input.templateId,
      priceCp: input.priceCp,
      stock: input.stock,
      effective: makeEffective(name: input.overrides.name ?? 'Objeto'),
    );
    _replace(
      Shop(
        id: shop.id,
        name: shop.name,
        description: shop.description,
        isOpen: shop.isOpen,
        buybackPercent: shop.buybackPercent,
        items: [...shop.items, item],
      ),
    );
    return item;
  }

  @override
  Future<ShopItem> updateItem(String shopId, String shopItemId, ShopItemPatch patch) =>
      throw UnimplementedError();

  @override
  Future<void> removeItem(String shopId, String shopItemId) async {
    final shop = _shop(shopId);
    _replace(
      Shop(
        id: shop.id,
        name: shop.name,
        description: shop.description,
        isOpen: shop.isOpen,
        buybackPercent: shop.buybackPercent,
        items: [
          for (final i in shop.items)
            if (i.id != shopItemId) i,
        ],
      ),
    );
  }

  Transaction _record(
    Shop shop,
    String characterName,
    TransactionType type,
    String item,
    int q,
    int total,
  ) {
    final t = Transaction(
      id: 'tx${history.length + 1}',
      shopName: shop.name,
      characterName: characterName,
      type: type,
      itemName: item,
      quantity: q,
      totalCp: total,
      at: DateTime.utc(2026, 10, 1, 12),
    );
    history.insert(0, t);
    return t;
  }

  @override
  Future<TradeResult> buy(
    String shopId, {
    required String characterId,
    required String shopItemId,
    int quantity = 1,
  }) async {
    if (buyError != null) throw buyError!;
    buys.add((shopId: shopId, shopItemId: shopItemId, quantity: quantity));
    final shop = _shop(shopId);
    if (!shop.isOpen) throw dioError(409);
    final item = shop.items.firstWhere((i) => i.id == shopItemId);
    if (item.stock != null && item.stock! < quantity) {
      throw dioProblem(400, 'No hay stock suficiente');
    }
    final total = item.priceCp * quantity;
    if ((inventory.money[characterId] ?? 0) < total) {
      throw dioProblem(400, 'No tienes suficiente dinero');
    }
    inventory.money[characterId] = inventory.money[characterId]! - total;
    inventory.give(
      characterId,
      makeCharacterItem(
        id: 'bought${history.length + 1}',
        templateId: item.templateId,
        quantity: quantity,
        effective: item.effective,
      ),
    );
    if (item.stock != null) {
      _replace(
        Shop(
          id: shop.id,
          name: shop.name,
          description: shop.description,
          isOpen: shop.isOpen,
          buybackPercent: shop.buybackPercent,
          items: [
            for (final i in shop.items)
              i.id == item.id ? i.copyWith(stock: i.stock! - quantity) : i,
          ],
        ),
      );
    }
    final t = _record(
      shop,
      'Thorin',
      TransactionType.purchase,
      item.effective.name,
      quantity,
      total,
    );
    return TradeResult(inventory: inventory.snapshot(characterId), transaction: t);
  }

  @override
  Future<TradeResult> sell(
    String shopId, {
    required String characterId,
    required String itemId,
    int quantity = 1,
  }) async {
    final shop = _shop(shopId);
    if (!shop.isOpen) throw dioError(409);
    final list = inventory.items[characterId]!;
    final item = list.firstWhere((i) => i.id == itemId);
    if (item.attuned) throw dioProblem(400, 'No puedes vender un objeto sintonizado');
    final unit = costByTemplate[item.templateId] ?? 0;
    final total = sellPayoutCp(
      unitCp: unit,
      quantity: quantity,
      buybackPercent: shop.buybackPercent,
    );
    inventory.money[characterId] = (inventory.money[characterId] ?? 0) + total;
    await inventory.remove(characterId, itemId, quantity: quantity);
    final t = _record(shop, 'Thorin', TransactionType.sale, item.effective.name, quantity, total);
    return TradeResult(inventory: inventory.snapshot(characterId), transaction: t);
  }

  @override
  Future<Page<Transaction>> transactions(
    String campaignId, {
    String? characterId,
    int page = 1,
    int pageSize = 30,
  }) async => Page(items: history, total: history.length, page: page, pageSize: pageSize);
}
