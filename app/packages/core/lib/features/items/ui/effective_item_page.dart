import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/ui/detail_widgets.dart';
import '../data/models.dart';
import '../domain/items_format.dart';

/// Icon flagging a field (or the whole item) as different from its template.
class OverrideBadge extends StatelessWidget {
  const OverrideBadge({super.key, this.tooltip = 'Modificado respecto a la plantilla'});

  final String tooltip;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      triggerMode: TooltipTriggerMode.tap,
      child: Icon(Icons.edit_note, size: 18, color: Theme.of(context).colorScheme.primary),
    );
  }
}

/// Opens the [EffectiveItemPage] of an inventory entry with its quantity,
/// charges and notes. [campaignId] picks the game system (the default one
/// when null).
Future<void> openInventoryItemDetail(
  BuildContext context,
  CharacterItem item, {
  String? campaignId,
}) => Navigator.of(context).push<void>(
  MaterialPageRoute(
    builder: (_) => EffectiveItemPage(
      campaignId: campaignId,
      effective: item.effective,
      overrides: item.overrides,
      isCustom: item.isCustom,
      extraFacts: [
        ('Cantidad', '${item.quantity}'),
        (
          'Cargas',
          item.charges == null ? null : '${item.charges}/${item.chargesMax ?? item.charges}',
        ),
        ('Notas', item.notes),
      ],
    ),
  ),
);

/// Detail of an item as the character sees it (template plus overrides), with
/// the overridden fields marked. The rows and effects of the game system of
/// [campaignId] ([GameSystemUi.itemFacts], [GameSystemUi.itemModifiers]) go
/// between the facts of the entry and the description.
class EffectiveItemPage extends ConsumerWidget {
  const EffectiveItemPage({
    super.key,
    this.campaignId,
    required this.effective,
    this.overrides = const ItemOverrides(),
    this.isCustom = false,
    this.extraFacts = const [],
  });

  /// The campaign of the item; null uses the default game system.
  final String? campaignId;
  final EffectiveItem effective;

  /// The fields that differ from the template.
  final ItemOverrides overrides;
  final bool isCustom;

  /// "label: value" rows shown first (quantity, charges, notes, ...).
  final List<(String, String?)> extraFacts;

  bool _marked(List<String> fields) => fields.any(overrides.definedFields.contains);

  Widget _row(String label, String? value, List<String> fields) {
    if (value == null || value.isEmpty) return const SizedBox.shrink();
    final marked = _marked(fields);
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(child: FactRow(label, value)),
        if (marked)
          Padding(
            padding: const EdgeInsets.only(top: 3),
            child: KeyedSubtree(
              key: Key('override-mark-${fields.first}'),
              child: const OverrideBadge(),
            ),
          ),
      ],
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final id = campaignId;
    final system = id == null
        ? ref.watch(defaultGameSystemUiProvider)
        : ref.watch(campaignSystemUiProvider(id));
    final i = effective;
    final modifiers = system.itemModifiers(i);
    final modifiersView = modifiers?.view;
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(i.name, key: const Key('effective-title'))),
      body: DetailList(
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  [
                    itemCategoryLabel(i.category),
                    ?i.subcategory,
                  ].where((e) => e.isNotEmpty).join(' · '),
                  style: theme.textTheme.titleSmall,
                ),
              ),
              if (_marked(const ['category', 'name']))
                KeyedSubtree(key: const Key('override-mark-name'), child: const OverrideBadge()),
            ],
          ),
          if (isCustom || !overrides.isEmpty) ...[
            const SizedBox(height: 4),
            Text(
              overrides.isEmpty
                  ? 'Objeto personalizado, sin plantilla.'
                  : 'Los campos marcados con el icono se han modificado respecto a la plantilla.',
              key: const Key('effective-note'),
              style: theme.textTheme.bodySmall,
            ),
          ],
          const SizedBox(height: 8),
          for (final (label, value) in extraFacts) FactRow(label, value),
          for (final fact in system.itemFacts(i)) _row(fact.label, fact.value, fact.fields),
          if (modifiersView != null || i.effects.isNotEmpty) ...[
            Row(
              children: [
                const Expanded(child: SectionTitle('Efectos')),
                if (modifiers != null && _marked(modifiers.fields))
                  KeyedSubtree(
                    key: Key('override-mark-${modifiers.fields.first}'),
                    child: const OverrideBadge(),
                  ),
                if (_marked(const ['effects']))
                  KeyedSubtree(
                    key: const Key('override-mark-effects'),
                    child: const OverrideBadge(),
                  ),
              ],
            ),
            ?modifiersView,
            Paragraphs(i.effects),
          ],
          if (i.description.isNotEmpty) ...[
            Row(
              children: [
                const Expanded(child: SectionTitle('Descripción')),
                if (_marked(const ['description']))
                  KeyedSubtree(
                    key: const Key('override-mark-description'),
                    child: const OverrideBadge(),
                  ),
              ],
            ),
            Paragraphs(i.description),
          ],
        ],
      ),
    );
  }
}
