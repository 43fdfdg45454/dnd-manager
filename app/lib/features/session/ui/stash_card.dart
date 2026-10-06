import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_icon.dart';
import '../../../core/theme/components.dart';
import '../../../core/theme/icons.dart';
import '../../../core/theme/tokens.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../catalog/domain/catalog_format.dart';
import '../../items/domain/items_format.dart';
import '../../items/ui/quantity_dialog.dart';
import '../data/models.dart';
import '../data/session_controllers.dart';
import 'dm/add_stash_item_page.dart';
import 'session_feedback.dart';

/// "Botín del grupo": the shared gold and the loot nobody owns yet.
///
/// The DM adds loot and gold, splits the gold, edits, removes and gives items
/// to [characters]. A player takes items for [takerCharacterId] when the
/// campaign allows it.
class PartyStashCard extends ConsumerWidget {
  const PartyStashCard({
    super.key,
    required this.campaign,
    this.characters = const [],
    this.takerCharacterId,
  });

  final CampaignDetail campaign;

  /// Active characters the DM can give items or gold to.
  final List<TableCharacter> characters;

  /// The player's character that takes the items.
  final String? takerCharacterId;

  StashController _controller(WidgetRef ref) =>
      ref.read(stashControllerProvider(campaign.id).notifier);

  Future<void> _adjustGold(BuildContext context, WidgetRef ref) async {
    final deltaCp = await showDialog<int>(context: context, builder: (_) => const _GoldDialog());
    if (deltaCp == null || !context.mounted) return;
    await runTableAction(
      context,
      () => _controller(ref).adjustGold(deltaCp),
      success: deltaCp > 0 ? 'Oro añadido al botín.' : 'Oro retirado del botín.',
    );
  }

  Future<void> _splitGold(BuildContext context, WidgetRef ref) async {
    final chosen = await pickCharacters(
      context,
      title: 'Repartir oro',
      characters: characters,
      confirmLabel: 'Repartir',
    );
    if (chosen == null || !context.mounted) return;
    final everyone = chosen.length == characters.length;
    await runTableAction(
      context,
      () => _controller(ref).splitGold(characterIds: everyone ? null : chosen.toList()),
      success: 'Oro repartido.',
    );
  }

  Future<void> _setPlayersCanTake(BuildContext context, WidgetRef ref, bool value) async {
    await runTableAction(
      context,
      () async {
        await ref
            .read(campaignDetailControllerProvider(campaign.id).notifier)
            .updateSettings(playersCanTakeFromStash: value);
        ref.invalidate(stashControllerProvider(campaign.id));
      },
      success: value
          ? 'Los jugadores ya pueden tomar objetos del botín.'
          : 'Solo el DM reparte el botín.',
    );
  }

  Future<void> _add(BuildContext context) => Navigator.of(
    context,
    rootNavigator: true,
  ).push<bool>(MaterialPageRoute(builder: (_) => AddStashItemPage(campaignId: campaign.id)));

  Future<void> _take(BuildContext context, WidgetRef ref, StashItem item) async {
    final characterId = takerCharacterId;
    if (characterId == null) return;
    final quantity = await showQuantityDialog(
      context,
      title: 'Tomar del botín',
      message: '¿Pasar "${item.item.name}" a tu inventario?',
      max: item.quantity,
      initial: 1,
      confirmLabel: 'Tomar',
    );
    if (quantity == null || !context.mounted) return;
    await runTableAction(
      context,
      () => _controller(ref).take(item.id, characterId: characterId, quantity: quantity),
      success: 'Añadido a tu inventario.',
    );
  }

  Future<void> _giveTo(BuildContext context, WidgetRef ref, StashItem item) async {
    final chosen = await pickCharacters(
      context,
      title: 'Dar a…',
      characters: characters,
      confirmLabel: 'Elegir',
      single: true,
    );
    if (chosen == null || chosen.isEmpty || !context.mounted) return;
    final quantity = await showQuantityDialog(
      context,
      title: 'Dar a…',
      message: '¿Cuántas unidades de "${item.item.name}"?',
      max: item.quantity,
      confirmLabel: 'Dar',
    );
    if (quantity == null || !context.mounted) return;
    final name = characters.firstWhere((c) => c.id == chosen.first).name;
    await runTableAction(
      context,
      () => _controller(ref).take(item.id, characterId: chosen.first, quantity: quantity),
      success: 'Entregado a $name.',
    );
  }

  Future<void> _edit(BuildContext context, WidgetRef ref, StashItem item) async {
    final result = await showDialog<({int quantity, String notes})>(
      context: context,
      builder: (_) => _EditStashItemDialog(item: item),
    );
    if (result == null || !context.mounted) return;
    await runTableAction(
      context,
      () => _controller(ref).updateItem(item.id, quantity: result.quantity, notes: result.notes),
      success: 'Botín actualizado.',
    );
  }

  Future<void> _remove(BuildContext context, WidgetRef ref, StashItem item) async {
    final confirmed = await confirmAction(
      context,
      title: 'Quitar del botín',
      message: '¿Quitar "${item.item.name}" del botín del grupo?',
      confirmLabel: 'Quitar',
    );
    if (!confirmed || !context.mounted) return;
    await runTableAction(
      context,
      () => _controller(ref).removeItem(item.id),
      success: 'Quitado del botín.',
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final isDm = campaign.myRole.isAtLeastDm;
    final stash = ref.watch(stashControllerProvider(campaign.id));

    return ParchmentCard(
      key: const Key('stash-card'),
      margin: const EdgeInsets.symmetric(vertical: 6),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              AppIcon(AppIcons.treasure, color: tokens.gold),
              const SizedBox(width: 8),
              Expanded(child: Text('Botín del grupo', style: theme.textTheme.titleMedium)),
              if (isDm)
                OfflineAware(
                  builder: (context, canWrite) => IconButton(
                    key: const Key('stash-add'),
                    tooltip: 'Añadir botín',
                    onPressed: canWrite ? () => _add(context) : null,
                    icon: const Icon(Icons.add_box_outlined),
                  ),
                ),
            ],
          ),
          stash.when(
            skipLoadingOnReload: true,
            loading: () => const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(describeTableError(error)),
                TextButton(
                  onPressed: () => ref.invalidate(stashControllerProvider(campaign.id)),
                  child: const Text('Reintentar'),
                ),
              ],
            ),
            data: (data) => _StashContent(
              stash: data,
              isDm: isDm,
              canTake: !isDm && data.playersCanTakeFromStash && takerCharacterId != null,
              onAdjustGold: () => _adjustGold(context, ref),
              onSplitGold: () => _splitGold(context, ref),
              onPlayersCanTake: (value) => _setPlayersCanTake(context, ref, value),
              onTake: (item) => _take(context, ref, item),
              onGiveTo: (item) => _giveTo(context, ref, item),
              onEdit: (item) => _edit(context, ref, item),
              onRemove: (item) => _remove(context, ref, item),
            ),
          ),
        ],
      ),
    );
  }
}

enum _StashAction { edit, giveTo, remove }

class _StashContent extends StatelessWidget {
  const _StashContent({
    required this.stash,
    required this.isDm,
    required this.canTake,
    required this.onAdjustGold,
    required this.onSplitGold,
    required this.onPlayersCanTake,
    required this.onTake,
    required this.onGiveTo,
    required this.onEdit,
    required this.onRemove,
  });

  final PartyStash stash;
  final bool isDm;
  final bool canTake;
  final VoidCallback onAdjustGold;
  final VoidCallback onSplitGold;
  final ValueChanged<bool> onPlayersCanTake;
  final ValueChanged<StashItem> onTake;
  final ValueChanged<StashItem> onGiveTo;
  final ValueChanged<StashItem> onEdit;
  final ValueChanged<StashItem> onRemove;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const AppIcon(AppIcons.coins, size: 20),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                formatMoney(stash.copperPieces),
                key: const Key('stash-gold'),
                style: theme.textTheme.titleSmall,
              ),
            ),
            if (isDm) ...[
              OfflineAware(
                builder: (context, canWrite) => IconButton(
                  key: const Key('stash-gold-add'),
                  tooltip: 'Añadir oro',
                  onPressed: canWrite ? onAdjustGold : null,
                  icon: const Icon(Icons.savings_outlined),
                ),
              ),
              OfflineAware(
                builder: (context, canWrite) => IconButton(
                  key: const Key('stash-gold-split'),
                  tooltip: 'Repartir oro',
                  onPressed: canWrite && stash.copperPieces > 0 ? onSplitGold : null,
                  icon: const Icon(Icons.call_split),
                ),
              ),
            ],
          ],
        ),
        if (isDm)
          OfflineAware(
            builder: (context, canWrite) => SwitchListTile(
              key: const Key('stash-players-can-take'),
              contentPadding: EdgeInsets.zero,
              dense: true,
              title: const Text('Los jugadores pueden tomar objetos'),
              value: stash.playersCanTakeFromStash,
              onChanged: canWrite ? onPlayersCanTake : null,
            ),
          ),
        if (stash.items.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 8),
            child: Text(
              'El botín está vacío.',
              key: const Key('stash-empty'),
              style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic),
            ),
          ),
        for (final item in stash.items) _stashTile(context, item),
      ],
    );
  }

  Widget _stashTile(BuildContext context, StashItem item) {
    final theme = Theme.of(context);
    final details = [
      itemCategoryLabel(item.item.category),
      if (item.charges != null) 'Cargas ${item.charges}/${item.chargesMax ?? item.charges}',
      ?item.notes,
    ].join(' · ');
    return ListTile(
      key: Key('stash-item-${item.id}'),
      contentPadding: EdgeInsets.zero,
      title: Text(item.item.name),
      subtitle: Text(details),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            '×${item.quantity}',
            key: Key('stash-qty-${item.id}'),
            style: theme.textTheme.titleSmall,
          ),
          if (isDm)
            OfflineAware(
              builder: (context, canWrite) => PopupMenuButton<_StashAction>(
                key: Key('stash-menu-${item.id}'),
                enabled: canWrite,
                onSelected: (action) => switch (action) {
                  _StashAction.edit => onEdit(item),
                  _StashAction.giveTo => onGiveTo(item),
                  _StashAction.remove => onRemove(item),
                },
                itemBuilder: (_) => const [
                  PopupMenuItem(value: _StashAction.edit, child: Text('Editar')),
                  PopupMenuItem(value: _StashAction.giveTo, child: Text('Dar a…')),
                  PopupMenuItem(value: _StashAction.remove, child: Text('Quitar')),
                ],
              ),
            )
          else if (canTake) ...[
            const SizedBox(width: 8),
            OfflineAware(
              builder: (context, canWrite) => FilledButton.tonal(
                key: Key('stash-take-${item.id}'),
                onPressed: canWrite ? () => onTake(item) : null,
                child: const Text('Tomar'),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// Gold added (positive) or withdrawn (negative) from the stash, in gp.
class _GoldDialog extends StatefulWidget {
  const _GoldDialog();

  @override
  State<_GoldDialog> createState() => _GoldDialogState();
}

class _GoldDialogState extends State<_GoldDialog> {
  final _amount = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _amount.dispose();
    super.dispose();
  }

  void _submit() {
    final cp = parseGoldToCp(_amount.text, allowNegative: true);
    if (cp == null || cp == 0) {
      setState(() => _error = 'Indica una cantidad en gp');
      return;
    }
    Navigator.of(context).pop(cp);
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Oro del grupo'),
      content: TextField(
        key: const Key('stash-gold-amount'),
        controller: _amount,
        autofocus: true,
        keyboardType: const TextInputType.numberWithOptions(decimal: true, signed: true),
        onSubmitted: (_) => _submit(),
        decoration: InputDecoration(
          labelText: 'Cantidad (gp)',
          helperText: 'Negativa para retirar',
          errorText: _error,
          border: const OutlineInputBorder(),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('stash-gold-confirm'),
          onPressed: _submit,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}

class _EditStashItemDialog extends StatefulWidget {
  const _EditStashItemDialog({required this.item});

  final StashItem item;

  @override
  State<_EditStashItemDialog> createState() => _EditStashItemDialogState();
}

class _EditStashItemDialogState extends State<_EditStashItemDialog> {
  late final _quantity = TextEditingController(text: '${widget.item.quantity}');
  late final _notes = TextEditingController(text: widget.item.notes ?? '');
  String? _error;

  @override
  void dispose() {
    _quantity.dispose();
    _notes.dispose();
    super.dispose();
  }

  void _submit() {
    final quantity = int.tryParse(_quantity.text.trim());
    if (quantity == null || quantity < 1) {
      setState(() => _error = 'Introduce un número de 1 en adelante.');
      return;
    }
    Navigator.of(context).pop((quantity: quantity, notes: _notes.text));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.item.item.name),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextField(
            key: const Key('stash-edit-quantity'),
            controller: _quantity,
            keyboardType: TextInputType.number,
            decoration: InputDecoration(
              labelText: 'Cantidad',
              errorText: _error,
              border: const OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            key: const Key('stash-edit-notes'),
            controller: _notes,
            minLines: 1,
            maxLines: 4,
            decoration: const InputDecoration(labelText: 'Notas', border: OutlineInputBorder()),
          ),
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('stash-edit-save'),
          onPressed: _submit,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}
