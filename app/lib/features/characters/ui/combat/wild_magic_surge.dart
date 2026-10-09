import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/data/models.dart' show RollTable;
import '../../../catalog/ui/roll_table_widgets.dart';
import '../../../dice/ui/dice_sheet.dart';
import '../../data/models.dart';
import 'panels/panel_support.dart' show restoreClassResource;
import 'resources_section.dart' show resourcesOf, tidesOfChaosSuffix;

/// Key (or key suffix, for prefixed pack keys) of the Wild Magic Surge table.
const wildMagicSurgeKey = 'wild-magic-surge';

/// Whether [key] names a Wild Magic Surge table: `wild-magic-surge` or a
/// prefixed one (`<pack>-wild-magic-surge`).
bool isWildMagicSurgeKey(String key) =>
    key == wildMagicSurgeKey || key.endsWith('-$wildMagicSurgeKey');

/// The Wild Magic Surge table of one of the character's subclasses (content
/// packs), or null.
RollTable? wildMagicSurgeTable(WidgetRef ref, CharacterDetail character) {
  final tables = [
    for (final subclass in {
      for (final k in character.classes)
        if (k.subclassIndex case final String index) index,
    })
      ...?ref.watch(rollTablesProvider(subclass)).value,
  ];
  return tables.where((t) => isWildMagicSurgeKey(t.key)).firstOrNull;
}

/// The character's Tides of Chaos: an automatic resource whose key ends in
/// `tides-of-chaos` (feature resource of a content pack), or null.
CharacterResource? tidesOfChaosOf(CharacterDetail character) =>
    resourcesOf(character)
        .where((r) => r.isAuto && (r.key?.endsWith(tidesOfChaosSuffix) ?? false))
        .firstOrNull;

/// One-line prompt shown after spending a slot of level 1 or higher with a
/// Wild Magic Surge subclass: "Tirar d20" rolls the virtual d20 and, on a 1,
/// opens the surge table; "Tirar oleada" opens the table directly. With Tides
/// of Chaos, "Recuperar Mareas del caos" gives back its use (no DM approval:
/// a surge restores it by the rules).
class WildMagicSurgePrompt extends ConsumerWidget {
  const WildMagicSurgePrompt({
    super.key,
    required this.character,
    required this.table,
    required this.canEdit,
    this.onDismiss,
  });

  final CharacterDetail character;
  final RollTable table;
  final bool canEdit;
  final VoidCallback? onDismiss;

  Future<void> _rollD20(BuildContext context) async {
    final result = await rollAndShow(context, '1d20', label: 'Oleada de magia salvaje');
    if (result == null || result.total != 1 || !context.mounted) return;
    await showRollTableSheet(context, table);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final tides = tidesOfChaosOf(character);
    return Padding(
      key: const Key('wild-magic-surge-prompt'),
      padding: const EdgeInsets.only(top: 8),
      child: Wrap(
        spacing: 8,
        runSpacing: 4,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text('Oleada de magia salvaje: tira 1d20', style: theme.textTheme.bodyMedium),
          FilledButton.tonal(
            key: const Key('wild-magic-surge-d20'),
            onPressed: () => _rollD20(context),
            child: const Text('Tirar d20'),
          ),
          OutlinedButton(
            key: const Key('wild-magic-surge-table'),
            onPressed: () => showRollTableSheet(context, table),
            child: const Text('Tirar oleada'),
          ),
          if (tides != null)
            TextButton(
              key: const Key('wild-magic-surge-tides'),
              onPressed: canEdit && tides.used > 0
                  ? () => restoreClassResource(
                      context,
                      ref,
                      character,
                      tides,
                      success: '${tides.name}: uso recuperado.',
                    )
                  : null,
              child: const Text('Recuperar Mareas del caos'),
            ),
          if (onDismiss != null)
            IconButton(
              key: const Key('wild-magic-surge-dismiss'),
              tooltip: 'Cerrar',
              onPressed: onDismiss,
              icon: const Icon(Icons.close, size: 18),
            ),
        ],
      ),
    );
  }
}
