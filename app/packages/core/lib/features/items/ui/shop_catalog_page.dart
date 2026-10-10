import 'dart:async';

import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/catalog/catalog_models.dart' show Page;
import '../../../core/network/api_error.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/ui/infinite_scroll_list.dart';
import '../../../core/ui/source_chip.dart';
import '../data/campaign_items_repository.dart';
import '../data/items_controllers.dart';
import '../data/models.dart';
import '../domain/items_format.dart';
import 'item_feedback.dart';

/// A category filter of the catalog as the shop page offers it, mapped to the
/// campaign item search (categories separated by commas, subcategory prefix).
enum ShopCatalogFilter {
  simpleWeapons('Armas sencillas', category: 'Weapon', subcategory: 'Simple'),
  martialWeapons('Armas marciales', category: 'Weapon', subcategory: 'Martial'),
  armor('Armaduras', category: 'Armor,Shield'),
  adventuringGear('Equipo de aventurero', category: 'AdventuringGear,Consumable'),
  tools('Herramientas', category: 'Tool'),
  mounts('Monturas y vehículos', category: 'Mount'),
  potions('Pociones', subcategory: 'Potion');

  const ShopCatalogFilter(this.label, {this.category, this.subcategory});

  final String label;
  final String? category;
  final String? subcategory;
}

/// Ready-made shops: SRD items by dataset index, some with a price of their
/// own (the SRD potions have no list price).
enum ShopPreset {
  smithy('Herrería', {
    'hammer': null,
    'hammer-sledge': null,
    'crowbar': null,
    'piton': null,
    'spike-iron': null,
    'chain-10-feet': null,
    'manacles': null,
    'lock': null,
    'grappling-hook': null,
    'pot-iron': null,
    'shovel': null,
    'pick-miners': null,
    'smiths-tools': null,
    'dagger': null,
    'handaxe': null,
    'mace': null,
    'sickle': null,
    'spear': null,
    'light-hammer': null,
    'warhammer': null,
    'shield': null,
  }),
  armory('Armería', {
    'club': null,
    'dagger': null,
    'quarterstaff': null,
    'spear': null,
    'crossbow-light': null,
    'shortbow': null,
    'sling': null,
    'longsword': null,
    'shortsword': null,
    'rapier': null,
    'scimitar': null,
    'battleaxe': null,
    'greataxe': null,
    'greatsword': null,
    'halberd': null,
    'longbow': null,
    'crossbow-heavy': null,
    'crossbow-hand': null,
    'padded-armor': null,
    'leather-armor': null,
    'studded-leather-armor': null,
    'hide-armor': null,
    'chain-shirt': null,
    'scale-mail': null,
    'breastplate': null,
    'chain-mail': null,
    'splint-armor': null,
    'plate-armor': null,
    'shield': null,
    'arrow': null,
    'crossbow-bolt': null,
    'sling-bullet': null,
    'quiver': null,
  }),
  generalStore('Tienda general', {
    'backpack': null,
    'bedroll': null,
    'blanket': null,
    'candle': null,
    'chalk-1-piece': null,
    'clothes-common': null,
    'clothes-travelers': null,
    'flask-or-tankard': null,
    'rope-hempen-50-feet': null,
    'lantern-hooded': null,
    'oil-flask': null,
    'rations-1-day': null,
    'sack': null,
    'tinderbox': null,
    'torch': null,
    'waterskin': null,
    'mess-kit': null,
    'pouch': null,
    'soap': null,
    'whetstone': null,
    'explorers-pack': null,
    'dungeoneers-pack': null,
    'fishing-tackle': null,
    'tent-two-person': null,
  }),
  alchemist('Alquimista', {
    'acid-vial': null,
    'alchemists-fire-flask': null,
    'antitoxin-vial': null,
    'oil-flask': null,
    'perfume-vial': null,
    'vial': null,
    'bottle-glass': null,
    'alchemists-supplies': null,
    'herbalism-kit': null,
    'healers-kit': null,
    'poison-basic-vial': null,
    // 50 gp, the usual price of a potion of healing.
    'potion-of-healing-common': 5000,
  });

  const ShopPreset(this.label, this.items);

  final String label;

  /// Dataset index → price in copper (null: the template's list price).
  final Map<String, int?> items;
}

/// One selected catalog item and its price (null: list price).
class _Selection {
  _Selection(this.item, this.priceCp);

  final ItemSummary item;
  final int? priceCp;

  int? get shownPriceCp => priceCp ?? item.costCp;
}

/// DM page to fill a shop from the campaign catalog: several items at once,
/// filtered by category, or a ready-made batch (Herrería, Armería...). Each
/// item goes in at its list price (presets may set their own); everything is
/// added in one request.
class ShopCatalogAddPage extends ConsumerStatefulWidget {
  const ShopCatalogAddPage({super.key, required this.campaignId, required this.shopId});

  final String campaignId;
  final String shopId;

  @override
  ConsumerState<ShopCatalogAddPage> createState() => _ShopCatalogAddPageState();
}

class _ShopCatalogAddPageState extends ConsumerState<ShopCatalogAddPage> {
  static const _debounceTime = Duration(milliseconds: 350);

  final _selected = <String, _Selection>{};
  Timer? _debounce;
  String _search = '';
  ShopCatalogFilter? _filter;

  List<ItemSummary> _items = [];
  int _page = 0;
  bool _hasMore = false;
  bool _loading = true;
  Object? _error;
  int _generation = 0;
  bool _busy = false;

  CampaignItemsRepository get _repository => ref.read(campaignItemsRepositoryProvider);

  @override
  void initState() {
    super.initState();
    _reload();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  Future<Page<ItemSummary>> _fetch(int page) => _repository.list(
    widget.campaignId,
    search: _search,
    category: _filter?.category,
    subcategory: _filter?.subcategory,
    page: page,
    pageSize: itemsPageSize,
  );

  Future<void> _reload() async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await _fetch(1);
      if (!mounted || generation != _generation) return;
      setState(() {
        _items = result.items;
        _page = 1;
        _hasMore = result.hasMore;
        _loading = false;
      });
    } catch (error) {
      if (!mounted || generation != _generation) return;
      setState(() {
        _error = error;
        _loading = false;
      });
    }
  }

  Future<void> _loadMore() async {
    final generation = _generation;
    final result = await _fetch(_page + 1);
    if (!mounted || generation != _generation) return;
    setState(() {
      _items = [..._items, ...result.items];
      _page++;
      _hasMore = result.hasMore;
    });
  }

  void _onSearch(String value) {
    _debounce?.cancel();
    _debounce = Timer(_debounceTime, () {
      if (!mounted) return;
      _search = value.trim();
      _reload();
    });
  }

  void _setFilter(ShopCatalogFilter? filter) {
    if (_filter == filter) return;
    _filter = filter;
    _reload();
  }

  void _toggle(ItemSummary item) => setState(() {
    if (_selected.remove(item.id) == null) _selected[item.id] = _Selection(item, null);
  });

  Future<void> _applyPreset(ShopPreset preset) async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      final page = await _repository.list(
        widget.campaignId,
        indexes: preset.items.keys.toList(),
        source: ItemSource.srd,
        pageSize: 200,
      );
      if (!mounted) return;
      setState(() {
        for (final item in page.items) {
          _selected[item.id] = _Selection(item, preset.items[item.index]);
        }
      });
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(content: Text('${preset.label}: ${page.items.length} objetos seleccionados.')),
        );
    } catch (error) {
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(describeItemError(error))));
    }
  }

  Future<void> _submit() async {
    setState(() => _busy = true);
    final count = _selected.length;
    final done = await runItemAction(
      context,
      () => ref.read(shopControllerProvider(widget.shopId).notifier).addItemsBulk([
        for (final s in _selected.values) BulkShopItem(templateId: s.item.id, priceCp: s.priceCp),
      ]),
      success: count == 1 ? 'Objeto añadido a la tienda.' : '$count objetos añadidos a la tienda.',
    );
    if (!mounted) return;
    setState(() => _busy = false);
    if (done) Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final count = _selected.length;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Añadir del catálogo'),
        actions: [
          PopupMenuButton<ShopPreset>(
            key: const Key('shop-catalog-presets'),
            tooltip: 'Tiendas predefinidas',
            icon: const Icon(Icons.storefront_outlined),
            onSelected: _applyPreset,
            itemBuilder: (_) => [
              for (final p in ShopPreset.values)
                PopupMenuItem(key: Key('shop-preset-${p.name}'), value: p, child: Text(p.label)),
            ],
          ),
          if (count > 0)
            IconButton(
              key: const Key('shop-catalog-clear'),
              tooltip: 'Quitar la selección',
              icon: const Icon(Icons.deselect),
              onPressed: () => setState(_selected.clear),
            ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
            child: TextField(
              key: const Key('shop-catalog-search'),
              onChanged: _onSearch,
              textInputAction: TextInputAction.search,
              decoration: const InputDecoration(
                prefixIcon: Icon(Icons.search),
                hintText: 'Buscar objeto',
                isDense: true,
                border: OutlineInputBorder(),
              ),
            ),
          ),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: Row(
              children: [
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 4),
                  child: ChoiceChip(
                    key: const Key('shop-catalog-filter-all'),
                    label: const Text('Todo'),
                    selected: _filter == null,
                    onSelected: (_) => _setFilter(null),
                  ),
                ),
                for (final f in ShopCatalogFilter.values)
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4),
                    child: ChoiceChip(
                      key: Key('shop-catalog-filter-${f.name}'),
                      label: Text(f.label),
                      selected: _filter == f,
                      onSelected: (_) => _setFilter(f),
                    ),
                  ),
              ],
            ),
          ),
          Expanded(child: _buildList()),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
          child: FilledButton.icon(
            key: const Key('shop-catalog-submit'),
            onPressed: count == 0 || _busy ? null : _submit,
            icon: const Icon(Icons.add_shopping_cart),
            label: Text(switch (count) {
              0 => 'Elige objetos',
              1 => 'Añadir 1 objeto',
              _ => 'Añadir $count objetos',
            }),
          ),
        ),
      ),
    );
  }

  Widget _buildList() {
    final system = ref.watch(campaignSystemUiProvider(widget.campaignId));
    if (_loading && _items.isEmpty) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return ItemsErrorView(error: _error!, onRetry: _reload);
    }
    if (_items.isEmpty) return const Center(child: Text('No hay objetos que coincidan.'));
    return Column(
      children: [
        if (_loading) const LinearProgressIndicator(),
        Expanded(
          child: InfiniteScrollList(
            listKey: const Key('shop-catalog-list'),
            itemCount: _items.length,
            hasMore: _hasMore && !_loading,
            onLoadMore: _loadMore,
            describeError: describeItemError,
            itemBuilder: (context, i) {
              final item = _items[i];
              final selection = _selected[item.id];
              final price = selection?.shownPriceCp ?? item.costCp;
              return CheckboxListTile(
                key: Key('shop-catalog-item-${item.id}'),
                value: selection != null,
                onChanged: (_) => _toggle(item),
                title: NameWithSource(item.name, item.source),
                subtitle: Text(
                  [
                    itemCategoryLabel(item.category),
                    ...system.itemSummaryFacts(item),
                    price == null ? 'Sin precio de lista (0 po)' : system.formatPrice(price),
                  ].where((e) => e.isNotEmpty).join(' · '),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}
