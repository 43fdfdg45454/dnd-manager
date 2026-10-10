import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/cache/stale_data.dart';
import '../../../core/characters/models.dart' show CharacterSummary;
import '../../../core/router/app_router.dart';
import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../characters/data/characters_controller.dart';
import '../data/items_controllers.dart';
import '../data/models.dart';
import '../data/shops_repository.dart';
import 'effective_item_page.dart';
import 'item_composer.dart';
import 'item_feedback.dart';
import 'shop_catalog_page.dart';
import 'shop_dialogs.dart';

const _shopMissing = 'La tienda no está disponible.';
const _shopClosed = 'La tienda está cerrada';

/// One shop: its items with prices and stock, the buy flow for players and the
/// management tools (open/closed, edit, items) for DMs.
class ShopPage extends ConsumerStatefulWidget {
  const ShopPage({super.key, required this.campaignId, required this.shopId});

  final String campaignId;
  final String shopId;

  @override
  ConsumerState<ShopPage> createState() => _ShopPageState();
}

class _ShopPageState extends ConsumerState<ShopPage> {
  /// Character used in the last purchase (its money is shown in the header).
  String? _characterId;

  ShopController get _shop => ref.read(shopControllerProvider(widget.shopId).notifier);

  Future<void> _buy(Shop shop, ShopItem item, List<CharacterSummary> mine) async {
    if (mine.isEmpty) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(content: Text('No tienes personajes en esta campaña para comprar.')),
        );
      return;
    }
    final choice = await showDialog<BuyChoice>(
      context: context,
      builder: (_) => BuyDialog(item: item, characters: mine, initialCharacterId: _characterId),
    );
    if (choice == null || !mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    TradeResult? result;
    final done = await runItemAction(
      context,
      () async {
        result = await _shop.buy(
          characterId: choice.characterId,
          shopItemId: item.id,
          quantity: choice.quantity,
        );
      },
      errors: const {
        400: 'No tienes suficiente dinero o no hay stock suficiente.',
        403: 'Ese personaje no es tuyo.',
        404: _shopMissing,
        409: _shopClosed,
      },
    );
    if (!done || !mounted) return;
    setState(() => _characterId = choice.characterId);
    final total = result?.transaction.totalCp ?? item.priceCp * choice.quantity;
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(
            'Compra realizada: ${item.effective.name} ×${choice.quantity} por '
            '${ref.read(campaignSystemUiProvider(widget.campaignId)).formatPrice(total)}.',
          ),
        ),
      );
  }

  Future<void> _setOpen(bool open) => runItemAction(
    context,
    () => _shop.edit(ShopPatch(isOpen: open)),
    success: open ? 'Tienda abierta.' : 'Tienda cerrada.',
  );

  Future<void> _editShop(Shop shop) async {
    final data = await showDialog<ShopFormData>(
      context: context,
      builder: (_) => ShopFormDialog(title: 'Editar tienda', initial: shop),
    );
    if (data == null || !mounted) return;
    await runItemAction(
      context,
      () => _shop.edit(
        ShopPatch(
          name: data.name,
          description: data.description ?? '',
          buybackPercent: data.buybackPercent,
        ),
      ),
      success: 'Tienda actualizada.',
    );
  }

  Future<void> _deleteShop(Shop shop) async {
    final confirmed = await confirmAction(
      context,
      title: 'Eliminar tienda',
      message: '¿Seguro que quieres eliminar "${shop.name}"? No se puede deshacer.',
      confirmLabel: 'Eliminar',
    );
    if (!confirmed || !mounted) return;
    final router = GoRouter.of(context);
    final done = await runItemAction(context, () => _shop.delete(), success: 'Tienda eliminada.');
    if (done && router.canPop()) router.pop();
  }

  Future<void> _editItem(ShopItem item) async {
    final patch = await showDialog<ShopItemPatch>(
      context: context,
      builder: (_) => ShopItemDialog(campaignId: widget.campaignId, item: item),
    );
    if (patch == null || !mounted) return;
    await runItemAction(
      context,
      () => _shop.updateItem(item.id, patch),
      success: 'Objeto actualizado.',
    );
  }

  Future<void> _removeItem(ShopItem item) async {
    final confirmed = await confirmAction(
      context,
      title: 'Quitar objeto',
      message: '¿Quitar "${item.effective.name}" de la tienda?',
      confirmLabel: 'Quitar',
    );
    if (!confirmed || !mounted) return;
    await runItemAction(
      context,
      () => _shop.removeItem(item.id),
      success: 'Objeto quitado de la tienda.',
    );
  }

  Future<void> _addItem() => Navigator.of(context).push<void>(
    MaterialPageRoute(
      builder: (_) => ShopItemAddPage(campaignId: widget.campaignId, shopId: widget.shopId),
    ),
  );

  Future<void> _addFromCatalog() => Navigator.of(context).push<void>(
    MaterialPageRoute(
      builder: (_) => ShopCatalogAddPage(campaignId: widget.campaignId, shopId: widget.shopId),
    ),
  );

  void _openDetail(ShopItem item) => Navigator.of(context).push<void>(
    MaterialPageRoute(
      builder: (_) => EffectiveItemPage(campaignId: widget.campaignId, effective: item.effective),
    ),
  );

  @override
  Widget build(BuildContext context) {
    final shop = ref.watch(shopControllerProvider(widget.shopId));
    final campaign = ref.watch(campaignDetailControllerProvider(widget.campaignId)).value;
    final isDm = campaign?.myRole.isAtLeastDm ?? false;
    final system = ref.watch(campaignSystemUiProvider(widget.campaignId));
    final auth = ref.watch(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : '';
    final mine = [
      for (final c
          in ref.watch(campaignCharactersControllerProvider(widget.campaignId)).value ??
              const <CharacterSummary>[])
        if (c.ownerUserId == myUserId) c,
    ];

    return Scaffold(
      appBar: AppBar(
        title: Text(shop.value?.name ?? 'Tienda'),
        actions: [
          IconButton(
            key: const Key('shop-transactions'),
            tooltip: 'Transacciones',
            onPressed: () => context.push(AppRoutes.transactions(widget.campaignId)),
            icon: const Icon(Icons.receipt_long_outlined),
          ),
          if (isDm && shop.hasValue)
            OfflineAware(
              builder: (context, canWrite) => IconButton(
                key: const Key('shop-add-catalog'),
                tooltip: 'Añadir del catálogo',
                onPressed: canWrite ? _addFromCatalog : null,
                icon: const Icon(Icons.playlist_add),
              ),
            ),
          if (isDm && shop.hasValue)
            PopupMenuButton<String>(
              key: const Key('shop-menu'),
              onSelected: (value) =>
                  value == 'edit' ? _editShop(shop.requireValue) : _deleteShop(shop.requireValue),
              itemBuilder: (_) => const [
                PopupMenuItem(value: 'edit', child: Text('Editar tienda')),
                PopupMenuItem(value: 'delete', child: Text('Eliminar tienda')),
              ],
            ),
        ],
      ),
      floatingActionButton: isDm && shop.hasValue
          ? OfflineAwareFab(
              fabKey: const Key('shop-add-item'),
              onPressed: _addItem,
              icon: const Icon(Icons.add),
              label: const Text('Añadir objeto'),
            )
          : null,
      body: OfflineBannerLayout(
        scopes: [staleTree(ShopsRepository.shopPath(widget.shopId))],
        child: shop.when(
          skipLoadingOnReload: true,
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ItemsErrorView(
            error: error,
            byStatus: const {404: _shopMissing},
            onRetry: () => ref.invalidate(shopControllerProvider(widget.shopId)),
          ),
          data: (data) => _ShopBody(
            system: system,
            shop: data,
            isDm: isDm,
            mine: mine,
            characterId: _characterId,
            onBuy: (item) => _buy(data, item, mine),
            onSetOpen: _setOpen,
            onEditItem: _editItem,
            onRemoveItem: _removeItem,
            onOpenDetail: _openDetail,
          ),
        ),
      ),
    );
  }
}

class _ShopBody extends ConsumerWidget {
  const _ShopBody({
    required this.system,
    required this.shop,
    required this.isDm,
    required this.mine,
    required this.characterId,
    required this.onBuy,
    required this.onSetOpen,
    required this.onEditItem,
    required this.onRemoveItem,
    required this.onOpenDetail,
  });

  final GameSystemUi system;
  final Shop shop;
  final bool isDm;
  final List<CharacterSummary> mine;
  final String? characterId;
  final ValueChanged<ShopItem> onBuy;
  final ValueChanged<bool> onSetOpen;
  final ValueChanged<ShopItem> onEditItem;
  final ValueChanged<ShopItem> onRemoveItem;
  final ValueChanged<ShopItem> onOpenDetail;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final buyer = mine.where((c) => c.id == characterId).firstOrNull;
    final money = buyer == null ? null : ref.watch(inventoryControllerProvider(buyer.id)).value;
    final description = shop.description;

    return ListView(
      padding: const EdgeInsets.only(bottom: 96),
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (description != null && description.isNotEmpty) Text(description),
              const SizedBox(height: 4),
              Text(
                'Recompra ${shop.buybackPercent} %',
                key: const Key('shop-buyback-label'),
                style: theme.textTheme.bodySmall,
              ),
              if (!shop.isOpen)
                const Padding(
                  padding: EdgeInsets.only(top: 4),
                  child: Chip(
                    key: Key('shop-closed-chip'),
                    label: Text('Cerrada'),
                    visualDensity: VisualDensity.compact,
                  ),
                ),
              if (buyer != null && money != null)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text(
                    'Dinero de ${buyer.name}: ${system.formatMoney(money.copperPieces)}',
                    key: const Key('shop-money'),
                    style: theme.textTheme.titleSmall,
                  ),
                ),
            ],
          ),
        ),
        if (isDm)
          SwitchListTile(
            key: const Key('shop-open-switch'),
            title: const Text('Tienda abierta'),
            value: shop.isOpen,
            onChanged: onSetOpen,
          ),
        if (shop.items.isEmpty)
          const Padding(
            padding: EdgeInsets.all(32),
            child: Center(child: Text('Esta tienda no tiene objetos.', key: Key('shop-empty'))),
          ),
        for (final item in shop.items)
          ListTile(
            key: Key('shop-item-${item.id}'),
            onTap: () => onOpenDetail(item),
            title: Text(item.effective.name),
            subtitle: Text(
              '${system.formatPrice(item.priceCp)} · ${item.isUnlimited ? 'Stock ∞' : 'Stock ${item.stock}'}',
              key: Key('shop-item-info-${item.id}'),
            ),
            trailing: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (!isDm || mine.isNotEmpty)
                  FilledButton.tonal(
                    key: Key('shop-buy-${item.id}'),
                    onPressed: item.soldOut || !shop.isOpen ? null : () => onBuy(item),
                    child: Text(item.soldOut ? 'Agotado' : 'Comprar'),
                  ),
                if (isDm)
                  PopupMenuButton<String>(
                    key: Key('shop-item-menu-${item.id}'),
                    onSelected: (value) => value == 'edit' ? onEditItem(item) : onRemoveItem(item),
                    itemBuilder: (_) => const [
                      PopupMenuItem(value: 'edit', child: Text('Editar')),
                      PopupMenuItem(value: 'remove', child: Text('Quitar')),
                    ],
                  ),
              ],
            ),
          ),
      ],
    );
  }
}

/// DM page to add an item to a shop: template plus overrides (or from
/// scratch), price in gp and stock.
class ShopItemAddPage extends ConsumerWidget {
  const ShopItemAddPage({super.key, required this.campaignId, required this.shopId});

  final String campaignId;
  final String shopId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(title: const Text('Añadir a la tienda')),
      body: ItemComposer(
        campaignId: campaignId,
        mode: ComposerMode.shop,
        submitLabel: 'Añadir a la tienda',
        onSubmit: (item) async {
          final done = await runItemAction(
            context,
            () => ref
                .read(shopControllerProvider(shopId).notifier)
                .addItem(
                  ShopItemInput(
                    templateId: item.templateId,
                    overrides: item.overrides,
                    priceCp: item.priceCp,
                    stock: item.stock,
                  ),
                ),
            success: 'Objeto añadido a la tienda.',
          );
          if (done && context.mounted) Navigator.of(context).pop();
          return done;
        },
      ),
    );
  }
}
