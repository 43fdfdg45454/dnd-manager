import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../systems/dnd5e/ui/action_type.dart';

import '../../../../catalog/data/catalog_controllers.dart';
import '../../../../catalog/data/models.dart' show RollTable;
import '../../../../catalog/ui/roll_table_widgets.dart';
import '../../../data/models.dart';
import '../combat_support.dart';
import '../resources_section.dart' show regularSlots;
import 'panel_support.dart';

/// Sorcery points it costs to create a slot of each level (SRD Flexible
/// Casting; nothing above level 5).
const sorcerySlotCost = {1: 2, 2: 3, 3: 5, 4: 6, 5: 7};

/// Sorcerer: sorcery points (resource `sorcery-points`, from level 2) and
/// Flexible Casting: a slot becomes points (spend slot + restore points) and
/// points recover a spent slot (spend points + restore slot).
class SorcererPanel extends ConsumerWidget {
  const SorcererPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  Future<void> _slotToPoints(
    BuildContext context,
    WidgetRef ref,
    CharacterResource points,
    int level,
  ) async {
    final controller = panelController(ref, panel.character);
    await runCombat(
      context,
      () async {
        await controller.spendSpellSlot(level);
        await controller.restoreResource(points.id, amount: level);
      },
      success: level == 1
          ? 'Espacio de nivel 1 convertido en 1 punto.'
          : 'Espacio de nivel $level convertido en $level puntos.',
    );
  }

  Future<void> _pointsToSlot(
    BuildContext context,
    WidgetRef ref,
    CharacterResource points,
    int level,
  ) async {
    final cost = sorcerySlotCost[level]!;
    final controller = panelController(ref, panel.character);
    await runCombat(context, () async {
      await controller.spendResource(points.id, amount: cost);
      await controller.restoreSpellSlot(level);
    }, success: '$cost puntos convertidos en un espacio de nivel $level.');
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final c = panel.character;
    final level = panel.level;
    final points = findResource(c, 'sorcery-points');
    final remaining = remainingOf(points);
    final slots = regularSlots(c);
    final canConvert = panel.canEdit && points != null && points.max > 0;
    final subclass = c.classes
        .where((k) => k.classIndex == panel.classIndex)
        .firstOrNull
        ?.subclassIndex;
    // Roll tables of the subclass from the content packs (Wild Magic Surge…).
    final tables = subclass == null
        ? const <RollTable>[]
        : ref.watch(rollTablesProvider(subclass)).value ?? const <RollTable>[];

    return ClassPanelFrame(
      panel: panel,
      children: [
        if (tables.isNotEmpty)
          CombatCard(
            // No featureIndex: roll tables of a subclass, not a feature of the SRD.
            title: 'Tablas de la subclase',
            child: Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                for (final table in tables)
                  OutlinedButton.icon(
                    key: Key('roll-table-open-${table.key}'),
                    onPressed: () => showRollTableSheet(context, table),
                    icon: const Icon(Icons.casino_outlined, size: 18),
                    label: Text(table.name),
                  ),
              ],
            ),
          ),
        CombatCard(
          // No featureIndex: sorcery points are part of Font of Magic, linked from
          // "Magia flexible" (one button per feature).
          title: 'Puntos de hechicería',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              ClassResourceUses(
                panel: panel,
                resource: points,
                keyPrefix: 'sorcerer-points',
                label: 'Puntos',
                minLevel: 2,
              ),
              if (level >= 2)
                Text(
                  level >= 3
                      ? 'Toca para gastar un punto (metamagia); mantén pulsado para recuperarlo.'
                      : 'Toca para gastar un punto; mantén pulsado para recuperarlo.',
                  style: theme.textTheme.bodySmall,
                ),
            ],
          ),
        ),
        if (level >= 2)
          CombatCard(
            title: 'Magia flexible',
            featureIndex: 'font-of-magic',
            // SRD: both conversions of Flexible Casting are made "as a bonus action".
            actionKind: ActionKind.bonusAction,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Espacio → puntos', style: theme.textTheme.labelLarge),
                const SizedBox(height: 4),
                if (slots.every((s) => s.used >= s.max))
                  Text('No tienes espacios disponibles.', style: theme.textTheme.bodySmall),
                Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  children: [
                    for (final slot in slots)
                      if (slot.used < slot.max)
                        ActionChip(
                          key: Key('sorcerer-slot-to-points-${slot.level}'),
                          label: Text('Nivel ${slot.level} → +${slot.level}'),
                          // Points never go above their maximum.
                          onPressed: canConvert && points.used >= slot.level
                              ? () => _slotToPoints(context, ref, points, slot.level)
                              : null,
                        ),
                  ],
                ),
                const SizedBox(height: 12),
                Text('Puntos → espacio', style: theme.textTheme.labelLarge),
                const SizedBox(height: 4),
                Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  children: [
                    for (final slot in slots)
                      if (sorcerySlotCost.containsKey(slot.level))
                        ActionChip(
                          key: Key('sorcerer-points-to-slot-${slot.level}'),
                          label: Text('${sorcerySlotCost[slot.level]} → nivel ${slot.level}'),
                          onPressed:
                              canConvert &&
                                  slot.used > 0 &&
                                  remaining >= sorcerySlotCost[slot.level]!
                              ? () => _pointsToSlot(context, ref, points, slot.level)
                              : null,
                        ),
                  ],
                ),
                const SizedBox(height: 4),
                Text(
                  'Solo recupera espacios gastados, hasta nivel 5.',
                  style: theme.textTheme.bodySmall,
                ),
              ],
            ),
          ),
      ],
    );
  }
}
