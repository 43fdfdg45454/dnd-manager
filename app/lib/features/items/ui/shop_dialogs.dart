import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/characters/models.dart' show CharacterSummary;
import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../data/items_controllers.dart';
import '../data/models.dart';

/// Values of the shop form.
class ShopFormData {
  const ShopFormData({required this.name, this.description, required this.buybackPercent});

  final String name;
  final String? description;
  final int buybackPercent;
}

/// Creates or edits a shop: name, description and buyback percentage.
class ShopFormDialog extends StatefulWidget {
  const ShopFormDialog({super.key, required this.title, this.initial});

  final String title;
  final ShopSummary? initial;

  @override
  State<ShopFormDialog> createState() => _ShopFormDialogState();
}

class _ShopFormDialogState extends State<ShopFormDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _name = TextEditingController(text: widget.initial?.name ?? '');
  late final TextEditingController _description = TextEditingController(
    text: widget.initial?.description ?? '',
  );
  late final TextEditingController _buyback = TextEditingController(
    text: '${widget.initial?.buybackPercent ?? 50}',
  );

  @override
  void dispose() {
    _name.dispose();
    _description.dispose();
    _buyback.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    final description = _description.text.trim();
    Navigator.of(context).pop(
      ShopFormData(
        name: _name.text.trim(),
        description: description.isEmpty ? null : description,
        buybackPercent: int.parse(_buyback.text.trim()),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: SingleChildScrollView(
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                key: const Key('shop-name'),
                controller: _name,
                autofocus: true,
                validator: (v) => (v ?? '').trim().isEmpty ? 'Escribe un nombre' : null,
                decoration: const InputDecoration(
                  labelText: 'Nombre',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 12),
              TextFormField(
                key: const Key('shop-description'),
                controller: _description,
                minLines: 2,
                maxLines: 4,
                decoration: const InputDecoration(
                  labelText: 'Descripción (opcional)',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 12),
              TextFormField(
                key: const Key('shop-buyback'),
                controller: _buyback,
                keyboardType: TextInputType.number,
                validator: (v) {
                  final n = int.tryParse(v?.trim() ?? '');
                  return n == null || n < 0 || n > 100 ? 'Entre 0 y 100' : null;
                },
                decoration: const InputDecoration(
                  labelText: 'Recompra (%)',
                  helperText: 'Porcentaje que paga al comprar objetos a los jugadores',
                  border: OutlineInputBorder(),
                ),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('shop-form-submit'),
          onPressed: _submit,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}

/// Edits the price (in gp) and the stock of a shop item of [campaignId].
class ShopItemDialog extends ConsumerStatefulWidget {
  const ShopItemDialog({super.key, required this.campaignId, required this.item});

  final String campaignId;
  final ShopItem item;

  @override
  ConsumerState<ShopItemDialog> createState() => _ShopItemDialogState();
}

class _ShopItemDialogState extends ConsumerState<ShopItemDialog> {
  final _formKey = GlobalKey<FormState>();

  GameSystemUi get _system => ref.read(campaignSystemUiProvider(widget.campaignId));

  late final TextEditingController _price = TextEditingController(
    text: _system.moneyInputText(widget.item.priceCp),
  );
  late final TextEditingController _stock = TextEditingController(
    text: widget.item.stock?.toString() ?? '',
  );

  @override
  void dispose() {
    _price.dispose();
    _stock.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    final stock = _stock.text.trim();
    Navigator.of(context).pop(
      ShopItemPatch(
        priceCp: _system.parseMoney(_price.text),
        stock: stock.isEmpty ? null : int.parse(stock),
        unlimitedStock: stock.isEmpty,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text('Editar ${widget.item.effective.name}'),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextFormField(
              key: const Key('shop-item-price'),
              controller: _price,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              validator: (v) => _system.parseMoney(v ?? '') == null ? 'Precio no válido' : null,
              decoration: const InputDecoration(
                labelText: 'Precio (gp)',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            TextFormField(
              key: const Key('shop-item-stock'),
              controller: _stock,
              keyboardType: TextInputType.number,
              validator: (v) {
                final text = v?.trim() ?? '';
                if (text.isEmpty) return null;
                final n = int.tryParse(text);
                return n == null || n < 0 ? 'Stock no válido' : null;
              },
              decoration: const InputDecoration(
                labelText: 'Stock',
                helperText: 'Vacío = ilimitado',
                border: OutlineInputBorder(),
              ),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('shop-item-save'),
          onPressed: _submit,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}

/// Result of [BuyDialog].
typedef BuyChoice = ({String characterId, int quantity});

/// Buys a shop item: picks one of the player's characters and the quantity and
/// shows the total next to the character's money.
class BuyDialog extends ConsumerStatefulWidget {
  const BuyDialog({
    super.key,
    required this.item,
    required this.characters,
    this.initialCharacterId,
  });

  final ShopItem item;

  /// Characters of the signed-in user in the campaign (never empty).
  final List<CharacterSummary> characters;
  final String? initialCharacterId;

  @override
  ConsumerState<BuyDialog> createState() => _BuyDialogState();
}

class _BuyDialogState extends ConsumerState<BuyDialog> {
  late String _characterId = widget.characters.any((c) => c.id == widget.initialCharacterId)
      ? widget.initialCharacterId!
      : widget.characters.first.id;
  int _quantity = 1;

  @override
  Widget build(BuildContext context) {
    final item = widget.item;
    final theme = Theme.of(context);
    final money = ref.watch(inventoryControllerProvider(_characterId)).value?.copperPieces;
    final system = ref.watch(campaignSystemUiProvider(widget.characters.first.campaignId));
    final total = item.priceCp * _quantity;
    final maxQuantity = item.stock;
    return AlertDialog(
      title: Text('Comprar ${item.effective.name}'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            DropdownButtonFormField<String>(
              key: const Key('buy-character'),
              initialValue: _characterId,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'Personaje',
                border: OutlineInputBorder(),
              ),
              items: [
                for (final c in widget.characters)
                  DropdownMenuItem(value: c.id, child: Text(c.name)),
              ],
              onChanged: (id) => setState(() => _characterId = id ?? _characterId),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                const Text('Cantidad'),
                const Spacer(),
                IconButton(
                  key: const Key('buy-minus'),
                  onPressed: _quantity > 1 ? () => setState(() => _quantity--) : null,
                  icon: const Icon(Icons.remove_circle_outline),
                ),
                Text('$_quantity', key: const Key('buy-quantity')),
                IconButton(
                  key: const Key('buy-plus'),
                  onPressed: maxQuantity == null || _quantity < maxQuantity
                      ? () => setState(() => _quantity++)
                      : null,
                  icon: const Icon(Icons.add_circle_outline),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              'Total: ${system.formatPrice(total)}',
              key: const Key('buy-total'),
              style: theme.textTheme.titleSmall,
            ),
            if (money != null) ...[
              Text('Dinero disponible: ${system.formatMoney(money)}', key: const Key('buy-money')),
              if (money < total)
                Text(
                  'No tienes suficiente dinero.',
                  key: const Key('buy-insufficient'),
                  style: TextStyle(color: theme.colorScheme.error),
                ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('buy-confirm'),
          onPressed: () =>
              Navigator.of(context)
                  .pop<BuyChoice>((characterId: _characterId, quantity: _quantity)),
          child: const Text('Comprar'),
        ),
      ],
    );
  }
}
