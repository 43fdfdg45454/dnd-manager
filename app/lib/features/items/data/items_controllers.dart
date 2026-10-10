import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../catalog/data/catalog_controllers.dart' show itemDetailProvider;
import '../../catalog/data/models.dart' show ItemDetail, ItemSummary, Page;
import '../../characters/data/characters_controller.dart';
import 'campaign_items_repository.dart';
import 'inventory_repository.dart';
import 'models.dart';
import 'shops_repository.dart';
import '../../../systems/dnd5e/items/dnd5e_item.dart';

const itemsPageSize = 30;

/// Errors are shown with a retry button instead of being retried silently.
Duration? _noRetry(int retryCount, Object error) => null;

// ---------------------------------------------------------------------------
// Inventory
// ---------------------------------------------------------------------------

/// Inventory of one character. Every mutation rethrows errors for the UI and
/// refreshes the character sheet (armor class, money, pending requests).
class InventoryController extends AsyncNotifier<Inventory> {
  InventoryController(this.characterId);

  final String characterId;

  InventoryRepository get _repository => ref.read(inventoryRepositoryProvider);

  @override
  Future<Inventory> build() => _repository.get(characterId);

  void _refreshSheet() {
    ref.invalidate(characterControllerProvider(characterId));
    ref.invalidate(changeRequestsControllerProvider);
  }

  Future<void> reload() async {
    state = AsyncData(await _repository.get(characterId));
    _refreshSheet();
  }

  /// Adds an item: applied at once or sent to the DM as a change request.
  Future<InventoryWriteResult> add({
    String? templateId,
    int quantity = 1,
    ItemOverrides overrides = const ItemOverrides(),
  }) async {
    final result = await _repository.add(
      characterId,
      templateId: templateId,
      quantity: quantity,
      overrides: overrides,
    );
    await reload();
    return result;
  }

  Future<void> patch(String itemId, InventoryPatch patch) async {
    await _repository.patch(characterId, itemId, patch);
    await reload();
  }

  Future<void> use(String itemId, {int amount = 1}) async {
    await _repository.use(characterId, itemId, amount: amount);
    await reload();
  }

  Future<InventoryWriteResult> remove(String itemId, {int? quantity}) async {
    final result = await _repository.remove(characterId, itemId, quantity: quantity);
    await reload();
    return result;
  }

  Future<InventoryWriteResult> adjustMoney({required int deltaCp, required String reason}) async {
    final result = await _repository.adjustMoney(characterId, deltaCp: deltaCp, reason: reason);
    await reload();
    return result;
  }
}

final inventoryControllerProvider = AsyncNotifierProvider.autoDispose
    .family<InventoryController, Inventory, String>(InventoryController.new, retry: _noRetry);

// ---------------------------------------------------------------------------
// Campaign item catalog and homebrew
// ---------------------------------------------------------------------------

/// Selector of the campaign item list: campaign, source and (debounced) search.
typedef CampaignItemsKey = ({String campaignId, ItemSource source, String search});

/// Campaign items for one search, loaded page by page.
class CampaignItemsController extends AsyncNotifier<Page<ItemSummary>> {
  CampaignItemsController(this.key);

  final CampaignItemsKey key;

  CampaignItemsRepository get _repository => ref.read(campaignItemsRepositoryProvider);

  @override
  Future<Page<ItemSummary>> build() => _repository.list(
    key.campaignId,
    search: key.search,
    source: key.source,
    pageSize: itemsPageSize,
  );

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || state.isLoading) return;
    final next = await _repository.list(
      key.campaignId,
      search: key.search,
      source: key.source,
      page: current.page + 1,
      pageSize: itemsPageSize,
    );
    if (!identical(state.value, current)) return;
    state = AsyncData(
      next.copyWith(items: [...current.items, ...next.items], page: current.page + 1),
    );
  }
}

final campaignItemsControllerProvider = AsyncNotifierProvider.autoDispose
    .family<CampaignItemsController, Page<ItemSummary>, CampaignItemsKey>(
      CampaignItemsController.new,
      retry: _noRetry,
    );

/// Homebrew mutations of a campaign. Every list of the catalog is refreshed
/// afterwards, as it may now contain, miss or rename the template.
class HomebrewActions {
  HomebrewActions(this._ref);

  final Ref _ref;

  CampaignItemsRepository get _repository => _ref.read(campaignItemsRepositoryProvider);

  Future<ItemDetail> create(String campaignId, ItemTemplateInput input) async {
    final created = await _repository.create(campaignId, input);
    _ref.invalidate(campaignItemsControllerProvider);
    return created;
  }

  Future<ItemDetail> update(String campaignId, String templateId, ItemTemplateInput input) async {
    final updated = await _repository.update(campaignId, templateId, input);
    _ref.invalidate(campaignItemsControllerProvider);
    _ref.invalidate(itemDetailProvider(templateId));
    return updated;
  }

  /// 409 when an item or a shop still uses the template.
  Future<void> delete(String campaignId, String templateId) async {
    await _repository.delete(campaignId, templateId);
    _ref.invalidate(campaignItemsControllerProvider);
  }
}

final homebrewActionsProvider = Provider<HomebrewActions>(HomebrewActions.new);

// ---------------------------------------------------------------------------
// Shops
// ---------------------------------------------------------------------------

/// Shops of a campaign (players only get the open ones).
class ShopsController extends AsyncNotifier<List<ShopSummary>> {
  ShopsController(this.campaignId);

  final String campaignId;

  ShopsRepository get _repository => ref.read(shopsRepositoryProvider);

  @override
  Future<List<ShopSummary>> build() => _repository.list(campaignId);

  Future<void> reload() async {
    state = await AsyncValue.guard(() => _repository.list(campaignId));
  }

  Future<void> create({required String name, String? description, int? buybackPercent}) async {
    await _repository.create(
      campaignId,
      name: name,
      description: description,
      buybackPercent: buybackPercent,
    );
    await reload();
  }

  Future<void> setOpen(String shopId, bool isOpen) async {
    await _repository.update(shopId, ShopPatch(isOpen: isOpen));
    ref.invalidate(shopControllerProvider(shopId));
    await reload();
  }
}

final shopsControllerProvider = AsyncNotifierProvider.autoDispose
    .family<ShopsController, List<ShopSummary>, String>(ShopsController.new, retry: _noRetry);

/// One shop with its items. Trades refresh the stock, the inventory of the
/// character and the transaction history.
class ShopController extends AsyncNotifier<Shop> {
  ShopController(this.shopId);

  final String shopId;

  ShopsRepository get _repository => ref.read(shopsRepositoryProvider);

  @override
  Future<Shop> build() => _repository.get(shopId);

  Future<void> reload() async {
    state = AsyncData(await _repository.get(shopId));
  }

  void _refreshLists() {
    ref.invalidate(shopsControllerProvider);
    ref.invalidate(transactionsControllerProvider);
  }

  Future<void> edit(ShopPatch patch) async {
    await _repository.update(shopId, patch);
    await reload();
    _refreshLists();
  }

  Future<void> delete() async {
    await _repository.delete(shopId);
    _refreshLists();
  }

  Future<void> addItem(ShopItemInput input) async {
    await _repository.addItem(shopId, input);
    await reload();
  }

  Future<void> addItemsBulk(List<BulkShopItem> items) async {
    state = AsyncData(await _repository.addItemsBulk(shopId, items));
  }

  Future<void> updateItem(String shopItemId, ShopItemPatch patch) async {
    await _repository.updateItem(shopId, shopItemId, patch);
    await reload();
  }

  Future<void> removeItem(String shopItemId) async {
    await _repository.removeItem(shopId, shopItemId);
    await reload();
  }

  /// A failed refresh after a successful trade keeps the previous state.
  Future<void> _afterTrade(String characterId) async {
    ref.invalidate(inventoryControllerProvider(characterId));
    ref.invalidate(characterControllerProvider(characterId));
    ref.invalidate(transactionsControllerProvider);
    try {
      await reload();
    } catch (_) {}
  }

  Future<TradeResult> buy({
    required String characterId,
    required String shopItemId,
    int quantity = 1,
  }) async {
    final result = await _repository.buy(
      shopId,
      characterId: characterId,
      shopItemId: shopItemId,
      quantity: quantity,
    );
    await _afterTrade(characterId);
    return result;
  }

  Future<TradeResult> sell({
    required String characterId,
    required String itemId,
    int quantity = 1,
  }) async {
    final result = await _repository.sell(
      shopId,
      characterId: characterId,
      itemId: itemId,
      quantity: quantity,
    );
    await _afterTrade(characterId);
    return result;
  }
}

final shopControllerProvider = AsyncNotifierProvider.autoDispose
    .family<ShopController, Shop, String>(ShopController.new, retry: _noRetry);

// ---------------------------------------------------------------------------
// Transactions
// ---------------------------------------------------------------------------

/// Transaction history of a campaign, loaded page by page.
class TransactionsController extends AsyncNotifier<Page<Transaction>> {
  TransactionsController(this.campaignId);

  final String campaignId;

  ShopsRepository get _repository => ref.read(shopsRepositoryProvider);

  @override
  Future<Page<Transaction>> build() =>
      _repository.transactions(campaignId, pageSize: itemsPageSize);

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || state.isLoading) return;
    final next = await _repository.transactions(
      campaignId,
      page: current.page + 1,
      pageSize: itemsPageSize,
    );
    if (!identical(state.value, current)) return;
    state = AsyncData(
      next.copyWith(items: [...current.items, ...next.items], page: current.page + 1),
    );
  }
}

final transactionsControllerProvider = AsyncNotifierProvider.autoDispose
    .family<TransactionsController, Page<Transaction>, String>(
      TransactionsController.new,
      retry: _noRetry,
    );
