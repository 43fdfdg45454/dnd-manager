import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../catalog/domain/catalog_format.dart';
import '../../characters/data/models.dart' show CharacterDetail;
import '../../session/data/session_controllers.dart';
import '../data/items_controllers.dart';
import '../data/models.dart';
import '../domain/items_format.dart';
import 'add_item_page.dart';
import 'attunement_dialog.dart';
import 'effective_item_page.dart';
import 'item_feedback.dart';
import 'quantity_dialog.dart';
import 'sell_dialog.dart';
import '../../../systems/dnd5e/items/dnd5e_item.dart';

/// "Inventario" tab of a character: money, weight, attunement and the items
/// grouped as equipped, backpack and consumables.
class InventoryTab extends ConsumerWidget {
  const InventoryTab({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final inventory = ref.watch(inventoryControllerProvider(character.id));
    return inventory.when(
      skipLoadingOnReload: true,
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => ItemsErrorView(
        error: error,
        onRetry: () => ref.invalidate(inventoryControllerProvider(character.id)),
      ),
      data: (data) => _InventoryView(character: character, inventory: data),
    );
  }
}

class _InventoryView extends ConsumerWidget {
  const _InventoryView({required this.character, required this.inventory});

  final CharacterDetail character;
  final Inventory inventory;

  InventoryController _controller(WidgetRef ref) =>
      ref.read(inventoryControllerProvider(character.id).notifier);

  Future<void> _editMoney(BuildContext context, WidgetRef ref) async {
    final change = await showDialog<({int deltaCp, String reason})>(
      context: context,
      builder: (_) => _MoneyDialog(copperPieces: inventory.copperPieces),
    );
    if (change == null || !context.mounted) return;
    await runWrite(
      context,
      () => _controller(ref).adjustMoney(deltaCp: change.deltaCp, reason: change.reason),
      applied: 'Dinero actualizado.',
    );
  }

  Future<void> _add(BuildContext context) => Navigator.of(context).push<bool>(
    MaterialPageRoute(
      builder: (_) => AddItemPage(characterId: character.id, campaignId: character.campaignId),
    ),
  );

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final grouped = <InventoryGroup, List<CharacterItem>>{};
    for (final item in inventory.items) {
      grouped.putIfAbsent(groupOf(item), () => []).add(item);
    }
    final overweight =
        inventory.carryCapacityLb > 0 && inventory.totalWeightLb > inventory.carryCapacityLb;

    return ListView(
      padding: EdgeInsets.fromLTRB(16, 8, 16, 88 + MediaQuery.paddingOf(context).bottom),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Icon(Icons.savings_outlined),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        formatMoney(inventory.copperPieces),
                        key: const Key('inventory-money'),
                        style: theme.textTheme.titleMedium,
                      ),
                    ),
                    OfflineAware(
                      builder: (context, canWrite) => IconButton(
                        key: const Key('inventory-money-edit'),
                        tooltip: 'Editar dinero',
                        onPressed: !canWrite ? null : () => _editMoney(context, ref),
                        icon: const Icon(Icons.edit_outlined),
                      ),
                    ),
                  ],
                ),
                Text(
                  'Peso ${formatPlainNumber(inventory.totalWeightLb)} / '
                  '${formatPlainNumber(inventory.carryCapacityLb)} lb',
                  key: const Key('inventory-weight'),
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: overweight ? theme.colorScheme.error : null,
                  ),
                ),
                Text(
                  'Sintonizados ${inventory.attunedCount}/$maxAttunedItems',
                  key: const Key('inventory-attuned'),
                  style: theme.textTheme.bodyMedium,
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 8),
        Align(
          alignment: Alignment.centerLeft,
          child: OfflineAware(
            builder: (context, canWrite) => FilledButton.tonalIcon(
              key: const Key('inventory-add'),
              onPressed: !canWrite ? null : () => _add(context),
              icon: const Icon(Icons.add),
              label: const Text('Añadir objeto'),
            ),
          ),
        ),
        if (inventory.items.isEmpty) ...[
          const SizedBox(height: 24),
          const Center(child: Text('Este personaje no tiene objetos.')),
        ],
        for (final group in InventoryGroup.values)
          if (grouped[group]?.isNotEmpty ?? false) ...[
            Padding(
              padding: const EdgeInsets.only(top: 16, bottom: 4),
              child: Text(
                '${group.label} (${grouped[group]!.length})',
                key: Key('inventory-group-${group.name}'),
                style: theme.textTheme.titleMedium,
              ),
            ),
            for (final item in grouped[group]!) _ItemTile(character: character, item: item),
          ],
      ],
    );
  }
}

enum _ItemAction { equip, attune, use, notes, detail, sell, giveBack, remove }

class _ItemTile extends ConsumerWidget {
  const _ItemTile({required this.character, required this.item});

  final CharacterDetail character;
  final CharacterItem item;

  InventoryController _controller(WidgetRef ref) =>
      ref.read(inventoryControllerProvider(character.id).notifier);

  Future<void> _onAction(BuildContext context, WidgetRef ref, _ItemAction action) async {
    switch (action) {
      case _ItemAction.equip:
        await runItemAction(
          context,
          () => _controller(ref).patch(item.id, InventoryPatch(equipped: !item.equipped)),
          success: item.equipped ? 'Objeto desequipado.' : 'Objeto equipado.',
          errors: const {400: 'Este objeto no se puede equipar.'},
        );
      case _ItemAction.attune:
        if (item.attuned) {
          await runItemAction(
            context,
            () => _controller(ref).patch(item.id, const InventoryPatch(attuned: false)),
            success: 'Sintonía eliminada.',
          );
        } else {
          // At the limit of three, the player chooses which item to drop.
          await attuneWithReplacement(
            context,
            item: item,
            attunedItems: [...?ref.read(inventoryControllerProvider(character.id)).value?.items],
            write: (patch) => _controller(ref).patch(item.id, patch),
          );
        }
      case _ItemAction.use:
        await runItemAction(
          context,
          () => _controller(ref).use(item.id),
          success: 'Has usado ${item.effective.name}.',
          errors: const {400: 'No se puede usar este objeto.'},
        );
      case _ItemAction.notes:
        final notes = await showDialog<String>(
          context: context,
          builder: (_) => _NotesDialog(initial: item.notes ?? ''),
        );
        if (notes == null || !context.mounted) return;
        await runItemAction(
          context,
          () => _controller(ref).patch(item.id, InventoryPatch(notes: notes)),
          success: 'Notas guardadas.',
        );
      case _ItemAction.detail:
        await openInventoryItemDetail(context, item);
      case _ItemAction.sell:
        await showDialog<void>(
          context: context,
          builder: (_) => SellDialog(character: character, item: item),
        );
      case _ItemAction.giveBack:
        final quantity = await showQuantityDialog(
          context,
          title: 'Devolver al grupo',
          message: '¿Devolver "${item.effective.name}" al botín del grupo?',
          max: item.quantity,
          confirmLabel: 'Devolver',
        );
        if (quantity == null || !context.mounted) return;
        // The stash controller is auto-disposed: kept alive while the request
        // runs, since the stash card may not be on screen (e.g. the inventory
        // sub-tab of "Detalle").
        final stash = stashControllerProvider(character.campaignId);
        final keepAlive = ProviderScope.containerOf(context).listen(stash, (_, _) {});
        try {
          await runItemAction(
            context,
            () => ref
                .read(stash.notifier)
                .giveBack(characterId: character.id, characterItemId: item.id, quantity: quantity),
            success: 'Devuelto al botín del grupo.',
          );
        } finally {
          keepAlive.close();
        }
      case _ItemAction.remove:
        final quantity = await showDialog<int>(
          context: context,
          builder: (_) => _RemoveDialog(item: item),
        );
        if (quantity == null || !context.mounted) return;
        await runWrite(
          context,
          () =>
              _controller(ref)
                  .remove(item.id, quantity: quantity == item.quantity ? null : quantity),
          applied: 'Objeto quitado del inventario.',
        );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final e = item.effective;
    // Giving back to the party stash: DMs always, players when the campaign
    // allows it; never an attuned item.
    final campaign = ref.watch(campaignDetailControllerProvider(character.campaignId)).value;
    final canGiveBack =
        campaign != null &&
        !item.attuned &&
        (campaign.myRole.isAtLeastDm || campaign.playersCanTakeFromStash);
    final chips = <Widget>[
      if (item.equipped) const _SmallChip('Equipado'),
      if (item.attuned) const _SmallChip('Sintonizado'),
      if (item.charges != null)
        _SmallChip('Cargas ${item.charges}/${item.chargesMax ?? item.charges}'),
    ];
    return ListTile(
      key: Key('inv-item-${item.id}'),
      contentPadding: EdgeInsets.zero,
      onTap: () => _onAction(context, ref, _ItemAction.detail),
      title: Row(
        children: [
          Flexible(child: Text(e.name, overflow: TextOverflow.ellipsis)),
          if (item.isMarked) ...[
            const SizedBox(width: 6),
            KeyedSubtree(
              key: Key('inv-mark-${item.id}'),
              child: OverrideBadge(
                tooltip: item.hasOverrides
                    ? 'Con modificaciones respecto a la plantilla'
                    : 'Objeto personalizado',
              ),
            ),
          ],
        ],
      ),
      subtitle: Wrap(
        spacing: 6,
        runSpacing: 2,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text(itemCategoryLabel(e.category), style: theme.textTheme.bodySmall),
          ...chips,
        ],
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            '×${item.quantity}',
            key: Key('inv-qty-${item.id}'),
            style: theme.textTheme.titleSmall,
          ),
          PopupMenuButton<_ItemAction>(
            key: Key('inv-menu-${item.id}'),
            onSelected: (action) => _onAction(context, ref, action),
            itemBuilder: (_) => [
              if (canEquip(e))
                PopupMenuItem(
                  value: _ItemAction.equip,
                  child: Text(item.equipped ? 'Desequipar' : 'Equipar'),
                ),
              if (e.requiresAttunement || item.attuned)
                PopupMenuItem(
                  value: _ItemAction.attune,
                  child: Text(item.attuned ? 'Quitar sintonía' : 'Sintonizar'),
                ),
              if (canUse(item)) const PopupMenuItem(value: _ItemAction.use, child: Text('Usar')),
              const PopupMenuItem(value: _ItemAction.notes, child: Text('Notas')),
              const PopupMenuItem(value: _ItemAction.detail, child: Text('Ver detalle')),
              const PopupMenuItem(value: _ItemAction.sell, child: Text('Vender')),
              if (canGiveBack)
                const PopupMenuItem(
                  key: Key('inv-give-back'),
                  value: _ItemAction.giveBack,
                  child: Text('Devolver al grupo'),
                ),
              const PopupMenuItem(value: _ItemAction.remove, child: Text('Quitar')),
            ],
          ),
        ],
      ),
    );
  }
}

class _SmallChip extends StatelessWidget {
  const _SmallChip(this.label);

  final String label;

  @override
  Widget build(BuildContext context) =>
      Chip(label: Text(label), visualDensity: VisualDensity.compact, padding: EdgeInsets.zero);
}

// ---------------------------------------------------------------------------
// Dialogs
// ---------------------------------------------------------------------------

class _MoneyDialog extends StatefulWidget {
  const _MoneyDialog({required this.copperPieces});

  final int copperPieces;

  @override
  State<_MoneyDialog> createState() => _MoneyDialogState();
}

class _MoneyDialogState extends State<_MoneyDialog> {
  final _formKey = GlobalKey<FormState>();
  final _delta = TextEditingController();
  final _reason = TextEditingController();

  @override
  void dispose() {
    _delta.dispose();
    _reason.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(
      context,
    ).pop((deltaCp: parseGoldToCp(_delta.text, allowNegative: true)!, reason: _reason.text.trim()));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Editar dinero'),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Dinero actual: ${formatMoney(widget.copperPieces)}'),
            const SizedBox(height: 12),
            TextFormField(
              key: const Key('money-delta'),
              controller: _delta,
              autofocus: true,
              keyboardType: const TextInputType.numberWithOptions(decimal: true, signed: true),
              validator: (v) {
                final cp = parseGoldToCp(v ?? '', allowNegative: true);
                return cp == null || cp == 0 ? 'Indica una cantidad en gp' : null;
              },
              decoration: const InputDecoration(
                labelText: 'Cantidad (gp)',
                helperText: 'Negativa para restar',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            TextFormField(
              key: const Key('money-reason'),
              controller: _reason,
              validator: (v) => (v ?? '').trim().isEmpty ? 'Escribe el motivo' : null,
              decoration: const InputDecoration(labelText: 'Motivo', border: OutlineInputBorder()),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('money-submit'),
          onPressed: _submit,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}

class _NotesDialog extends StatefulWidget {
  const _NotesDialog({required this.initial});

  final String initial;

  @override
  State<_NotesDialog> createState() => _NotesDialogState();
}

class _NotesDialogState extends State<_NotesDialog> {
  late final TextEditingController _controller = TextEditingController(text: widget.initial);

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Notas del objeto'),
      content: TextField(
        key: const Key('item-notes'),
        controller: _controller,
        autofocus: true,
        minLines: 2,
        maxLines: 5,
        decoration: const InputDecoration(border: OutlineInputBorder()),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('item-notes-save'),
          onPressed: () => Navigator.of(context).pop(_controller.text.trim()),
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}

class _RemoveDialog extends StatefulWidget {
  const _RemoveDialog({required this.item});

  final CharacterItem item;

  @override
  State<_RemoveDialog> createState() => _RemoveDialogState();
}

class _RemoveDialogState extends State<_RemoveDialog> {
  late int _quantity = widget.item.quantity;

  @override
  Widget build(BuildContext context) {
    final item = widget.item;
    return AlertDialog(
      title: const Text('Quitar objeto'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('¿Quitar "${item.effective.name}" del inventario?'),
          if (item.quantity > 1) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                const Text('Cantidad'),
                const Spacer(),
                IconButton(
                  key: const Key('remove-minus'),
                  onPressed: _quantity > 1 ? () => setState(() => _quantity--) : null,
                  icon: const Icon(Icons.remove_circle_outline),
                ),
                Text('$_quantity', key: const Key('remove-quantity')),
                IconButton(
                  key: const Key('remove-plus'),
                  onPressed: _quantity < item.quantity ? () => setState(() => _quantity++) : null,
                  icon: const Icon(Icons.add_circle_outline),
                ),
              ],
            ),
          ],
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('confirm-action'),
          onPressed: () => Navigator.of(context).pop(_quantity),
          child: const Text('Quitar'),
        ),
      ],
    );
  }
}
