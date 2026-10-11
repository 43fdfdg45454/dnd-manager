import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/ui/detail_widgets.dart';
import 'package:opentrpg_core/core/ui/stat_value.dart';
import 'package:opentrpg_core/features/items/data/inventory_repository.dart';
import 'package:opentrpg_core/features/items/data/items_controllers.dart';
import 'package:opentrpg_core/features/items/data/models.dart' show CharacterItem;

import '../dnd5e_characters_controller.dart';
import '../domain/character_format.dart' show damageTypeLabel, formatModifier;
import '../models.dart';
import 'combat/combat_support.dart';

/// "weapon", "armor" or "shield" for an inventory entry that can be
/// enhanced; null for anything else.
String? enhanceableKind(CharacterItem item) => switch (item.effective.category.toLowerCase()) {
  'weapon' => 'weapon',
  'armor' => 'armor',
  'shield' => 'shield',
  _ => null,
};

/// "Mejoras de objetos" and "Injertos" of the summary: the enhanced inventory
/// entries with their options, damage and properties after them and the
/// breakdowns of what they add; with the feature, "Mejorar objeto" and
/// "Cambiar mejoras" (applied directly by the owner within the feature's
/// limits, by the DM always). Grafts list their body slot and whether they
/// are equipped (one equipped per slot).
class ItemEnhancementsSection extends ConsumerWidget {
  const ItemEnhancementsSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  /// Whether the summary shows this section.
  static bool shows(CharacterDetail c) =>
      c.itemEnhancements.isNotEmpty || c.itemEnhancementFeature != null || c.grafts.isNotEmpty;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final feature = c.itemEnhancementFeature;
    final theme = Theme.of(context);
    return Column(
      key: const Key('item-enhancements'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (feature != null || c.itemEnhancements.isNotEmpty) ...[
          const SectionTitle('Mejoras de objetos'),
          if (feature != null)
            Text(
              '${feature.featureName}: hasta ${feature.maxPerItem} '
              '${feature.maxPerItem == 1 ? 'mejora' : 'mejoras'} por objeto.',
              key: const Key('item-enhancement-feature'),
              style: theme.textTheme.bodySmall,
            ),
          for (final e in c.itemEnhancements)
            _EnhancedItemTile(
              character: c,
              enhancement: e,
              onEdit: feature != null && canEdit
                  ? () => _edit(context, ref, e.itemId, e.itemName, null, {
                      for (final o in e.options) o.index,
                    })
                  : null,
            ),
          if (feature != null && canEdit)
            Align(
              alignment: Alignment.centerLeft,
              child: OutlinedButton.icon(
                key: const Key('item-enhance'),
                onPressed: () => _pickItem(context, ref),
                icon: const Icon(Icons.auto_fix_high_outlined),
                label: const Text('Mejorar objeto'),
              ),
            ),
        ],
        if (c.grafts.isNotEmpty) ...[
          const SectionTitle('Injertos'),
          Wrap(
            key: const Key('grafts'),
            spacing: 8,
            runSpacing: 4,
            children: [
              for (final g in c.grafts)
                Chip(
                  key: Key('graft-${g.itemId}'),
                  avatar: Icon(g.equipped ? Icons.check_circle : Icons.circle_outlined, size: 18),
                  label: Text('${g.name} · ${g.slot}${g.equipped ? ' · equipado' : ''}'),
                ),
            ],
          ),
          Text('Solo un injerto equipado por zona.', style: theme.textTheme.bodySmall),
        ],
      ],
    );
  }

  /// Chooses an inventory weapon, armor or shield and opens its options.
  Future<void> _pickItem(BuildContext context, WidgetRef ref) async {
    final List<CharacterItem> items;
    try {
      items = [
        for (final i in (await ref.read(inventoryRepositoryProvider).get(character.id)).items)
          if (enhanceableKind(i) != null) i,
      ];
    } catch (error) {
      if (context.mounted) showCombatMessage(context, describeCombatError(error));
      return;
    }
    if (!context.mounted) return;
    if (items.isEmpty) {
      showCombatMessage(context, 'No tienes armas, armaduras ni escudos que mejorar.');
      return;
    }
    final item = await showDialog<CharacterItem>(
      context: context,
      builder: (dialogContext) => SimpleDialog(
        title: const Text('¿Qué objeto mejoras?'),
        children: [
          for (final i in items)
            SimpleDialogOption(
              key: Key('enhance-item-${i.id}'),
              onPressed: () => Navigator.of(dialogContext).pop(i),
              child: Text(i.effective.name),
            ),
        ],
      ),
    );
    if (item == null || !context.mounted) return;
    final current = character.itemEnhancements.where((e) => e.itemId == item.id).firstOrNull;
    await _edit(context, ref, item.id, item.effective.name, enhanceableKind(item), {
      for (final o in current?.options ?? const <AppliedEnhancement>[]) o.index,
    });
  }

  /// The options dialog for [itemId], then the request.
  Future<void> _edit(
    BuildContext context,
    WidgetRef ref,
    String itemId,
    String itemName,
    String? kind,
    Set<String> current,
  ) async {
    final feature = character.itemEnhancementFeature;
    if (feature == null) return;
    final options = await showDialog<List<String>>(
      context: context,
      builder: (_) =>
          ItemEnhancementDialog(feature: feature, itemName: itemName, kind: kind, initial: current),
    );
    if (options == null || !context.mounted) return;
    SheetSaveResult? result;
    final done = await runCombat(context, () async {
      result = await ref
          .read(dnd5eCharacterControllerProvider(character.id).notifier)
          .setItemEnhancements(itemId, options);
    });
    if (!done || !context.mounted) return;
    ref.invalidate(inventoryControllerProvider(character.id));
    showCombatMessage(context, switch (result) {
      PendingApproval() => 'Enviado al DM para aprobación',
      _ => 'Mejoras de $itemName guardadas.',
    });
  }
}

/// An enhanced entry: options, damage after them and the bonuses they add.
class _EnhancedItemTile extends StatelessWidget {
  const _EnhancedItemTile({required this.character, required this.enhancement, this.onEdit});

  final CharacterDetail character;
  final CharacterItemEnhancement enhancement;
  final VoidCallback? onEdit;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final e = enhancement;
    const labels = {'attack': 'Ataque', 'damage': 'Daño', 'armorClass': 'CA'};
    return Card(
      key: Key('enhanced-item-${e.itemId}'),
      margin: const EdgeInsets.symmetric(vertical: 4),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text(e.itemName, style: theme.textTheme.titleSmall)),
                if (onEdit != null)
                  IconButton(
                    key: Key('enhanced-item-${e.itemId}-edit'),
                    tooltip: 'Cambiar mejoras',
                    onPressed: onEdit,
                    icon: const Icon(Icons.edit_outlined),
                  ),
              ],
            ),
            Text(e.options.map((o) => o.name).join(' · '), style: theme.textTheme.bodyMedium),
            if (e.damageDice != null)
              Text(
                'Daño ${e.damageDice}${e.damageType == null ? '' : ' ${damageTypeLabel(e.damageType!)}'}',
                key: Key('enhanced-item-${e.itemId}-damage'),
                style: theme.textTheme.bodySmall,
              ),
            if (e.properties.isNotEmpty)
              Text('Propiedades: ${e.properties.join(', ')}', style: theme.textTheme.bodySmall),
            if (e.breakdowns.isNotEmpty)
              Wrap(
                spacing: 12,
                children: [
                  for (final entry in e.breakdowns.entries)
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          '${labels[entry.key] ?? entry.key} ',
                          style: theme.textTheme.bodySmall,
                        ),
                        StatValue(
                          statKey: 'enhancement.${e.itemId}.${entry.key}',
                          text: formatModifier(entry.value.total),
                          title: '${labels[entry.key] ?? entry.key} de ${e.itemName}',
                          breakdown: entry.value,
                        ),
                      ],
                    ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

/// Checkboxes of the feature's options for one item: only the ones that fit
/// its [kind] and the class level, at most `maxPerItem`, and each one only
/// after the options it requires. Pops the chosen indexes.
class ItemEnhancementDialog extends StatefulWidget {
  const ItemEnhancementDialog({
    super.key,
    required this.feature,
    required this.itemName,
    this.kind,
    this.initial = const {},
  });

  final ItemEnhancementFeature feature;
  final String itemName;

  /// "weapon", "armor" or "shield"; null when unknown (every option shown).
  final String? kind;
  final Set<String> initial;

  @override
  State<ItemEnhancementDialog> createState() => _ItemEnhancementDialogState();
}

class _ItemEnhancementDialogState extends State<ItemEnhancementDialog> {
  late final Set<String> _chosen = {...widget.initial};

  bool _enabled(ItemEnhancementOption o) {
    if (_chosen.contains(o.index)) return true;
    if (!o.available) return false;
    if (_chosen.length >= widget.feature.maxPerItem) return false;
    return o.requiresOptions.every(_chosen.contains);
  }

  void _toggle(ItemEnhancementOption o, bool on) => setState(() {
    if (on) {
      _chosen.add(o.index);
    } else {
      // Options that required this one go with it.
      _chosen.remove(o.index);
      for (final other in widget.feature.options) {
        if (other.requiresOptions.contains(o.index)) _chosen.remove(other.index);
      }
    }
  });

  @override
  Widget build(BuildContext context) {
    final kind = widget.kind;
    final options = [
      for (final o in widget.feature.options)
        if (kind == null || o.effects.appliesToKind(kind)) o,
    ];
    return AlertDialog(
      title: Text('Mejoras de ${widget.itemName}'),
      content: SizedBox(
        width: 420,
        child: ListView(
          shrinkWrap: true,
          children: [
            Text(
              '${_chosen.length} de ${widget.feature.maxPerItem}',
              key: const Key('enhancement-count'),
            ),
            for (final o in options)
              CheckboxListTile(
                key: Key('enhancement-option-${o.index}'),
                value: _chosen.contains(o.index),
                onChanged: _enabled(o) ? (v) => _toggle(o, v ?? false) : null,
                title: Text(o.name),
                subtitle: Text(
                  [
                    if (o.minLevel != null) 'Nivel ${o.minLevel}',
                    if (o.requiresOptions.isNotEmpty)
                      'Requiere ${o.requiresOptions.map((r) => widget.feature.options.where((x) => x.index == r).firstOrNull?.name ?? r).join(', ')}',
                    ...o.description.take(1),
                  ].join(' · '),
                ),
              ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('enhancement-save'),
          onPressed: () => Navigator.of(context).pop([
            for (final o in widget.feature.options)
              if (_chosen.contains(o.index)) o.index,
          ]),
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}
