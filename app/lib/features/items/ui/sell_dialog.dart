import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_error.dart';
import '../../catalog/data/catalog_controllers.dart';
import '../../characters/data/models.dart' show CharacterDetail;
import '../data/items_controllers.dart';
import '../data/models.dart';
import '../domain/items_format.dart';
import '../../catalog/domain/catalog_format.dart';
import 'item_feedback.dart';

/// Sells an inventory item to an open shop: picks the shop and the quantity and
/// shows what the shop will pay according to its buyback percentage.
class SellDialog extends ConsumerStatefulWidget {
  const SellDialog({super.key, required this.character, required this.item});

  final CharacterDetail character;
  final CharacterItem item;

  @override
  ConsumerState<SellDialog> createState() => _SellDialogState();
}

class _SellDialogState extends ConsumerState<SellDialog> {
  String? _shopId;
  int _quantity = 1;
  bool _busy = false;

  Future<void> _sell(ShopSummary shop) async {
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);
    TradeResult? result;
    setState(() => _busy = true);
    final done = await runItemAction(context, () async {
      result = await ref
          .read(shopControllerProvider(shop.id).notifier)
          .sell(characterId: widget.character.id, itemId: widget.item.id, quantity: _quantity);
    }, errors: const {400: 'No se puede vender este objeto.', 409: 'La tienda está cerrada'});
    if (!mounted) return;
    setState(() => _busy = false);
    if (!done) return;
    final total = result?.transaction.totalCp;
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(
            total == null ? 'Venta realizada.' : 'Venta realizada: recibes ${formatCostCp(total)}.',
          ),
        ),
      );
    navigator.pop();
  }

  @override
  Widget build(BuildContext context) {
    final item = widget.item;
    final shops = ref.watch(shopsControllerProvider(widget.character.campaignId));

    Widget body;
    ShopSummary? selected;
    if (shops.isLoading && !shops.hasValue) {
      body = const Center(child: CircularProgressIndicator());
    } else if (shops.hasError && !shops.hasValue) {
      body = Text(describeItemError(shops.error!));
    } else {
      final open = [
        for (final s in shops.requireValue)
          if (s.isOpen) s,
      ];
      if (open.isEmpty) {
        body = const Text('No hay tiendas abiertas en esta campaña.', key: Key('sell-no-shops'));
      } else {
        selected = open.firstWhere((s) => s.id == _shopId, orElse: () => open.first);
        body = _SellForm(
          item: item,
          shops: open,
          selected: selected,
          quantity: _quantity,
          onShop: (id) => setState(() => _shopId = id),
          onQuantity: (q) => setState(() => _quantity = q),
        );
      }
    }

    final canSell = selected != null && !item.attuned && !_busy;
    return AlertDialog(
      title: Text('Vender ${item.effective.name}'),
      content: SingleChildScrollView(child: body),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('sell-confirm'),
          onPressed: canSell ? () => _sell(selected!) : null,
          child: const Text('Vender'),
        ),
      ],
    );
  }
}

class _SellForm extends ConsumerWidget {
  const _SellForm({
    required this.item,
    required this.shops,
    required this.selected,
    required this.quantity,
    required this.onShop,
    required this.onQuantity,
  });

  final CharacterItem item;
  final List<ShopSummary> shops;
  final ShopSummary selected;
  final int quantity;
  final ValueChanged<String> onShop;
  final ValueChanged<int> onQuantity;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final templateId = item.templateId;
    final template = templateId == null ? null : ref.watch(itemDetailProvider(templateId)).value;
    final shop = ref.watch(shopControllerProvider(selected.id)).value;
    final shopPrice = shop?.items.where((s) => s.templateId == templateId && templateId != null);
    final unit = sellUnitCp(
      effectiveCostCp: item.effective.costCp,
      templateCostCp: template?.costCp,
      shopPriceCp: shopPrice == null || shopPrice.isEmpty ? null : shopPrice.first.priceCp,
    );
    final theme = Theme.of(context);

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        DropdownButtonFormField<String>(
          key: const Key('sell-shop'),
          initialValue: selected.id,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'Tienda', border: OutlineInputBorder()),
          items: [
            for (final s in shops)
              DropdownMenuItem(
                value: s.id,
                child: Text(s.name, overflow: TextOverflow.ellipsis),
              ),
          ],
          onChanged: (id) {
            if (id != null) onShop(id);
          },
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            const Text('Cantidad'),
            const Spacer(),
            IconButton(
              key: const Key('sell-minus'),
              onPressed: quantity > 1 ? () => onQuantity(quantity - 1) : null,
              icon: const Icon(Icons.remove_circle_outline),
            ),
            Text('$quantity', key: const Key('sell-quantity')),
            IconButton(
              key: const Key('sell-plus'),
              onPressed: quantity < item.quantity ? () => onQuantity(quantity + 1) : null,
              icon: const Icon(Icons.add_circle_outline),
            ),
          ],
        ),
        const SizedBox(height: 8),
        if (item.attuned)
          Text(
            'No puedes vender un objeto sintonizado.',
            key: const Key('sell-attuned'),
            style: TextStyle(color: theme.colorScheme.error),
          )
        else if (unit == null)
          const Text(
            'No se puede calcular el importe: lo fijará la tienda.',
            key: Key('sell-payout'),
          )
        else
          Text(
            'Recibirás ${formatCostCp(sellPayoutCp(unitCp: unit, quantity: quantity, buybackPercent: selected.buybackPercent))}'
            ' (${selected.buybackPercent} % de ${formatCostCp(unit * quantity)})',
            key: const Key('sell-payout'),
            style: theme.textTheme.titleSmall,
          ),
      ],
    );
  }
}
