import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/icons.dart';

import '../../../../catalog/data/catalog_controllers.dart';
import '../../../../catalog/data/models.dart' show RollTable;
import '../../../../catalog/ui/roll_table_widgets.dart';
import '../../../../ui/action_type.dart';
import '../../../models.dart';
import '../combat_rows.dart';
import '../combat_support.dart';
import '../resources_section.dart' show regularSlots;
import 'panel_support.dart';

/// Sorcery points it costs to create a slot of each level (SRD Flexible
/// Casting; nothing above level 5).
const sorcerySlotCost = {1: 2, 2: 3, 3: 5, 4: 6, 5: 7};

/// Sorcerer: sorcery points (resource `sorcery-points`, from level 2) and
/// Flexible Casting on the turn: a slot becomes points (spend slot + restore
/// points) and points recover a spent slot (spend points + restore slot). The
/// roll tables of the subclass (Wild Magic Surge...) are in "Más".
const sorcererPanel = ClassPanelSpec(actions: _actions, more: _more);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) {
  final points = findResource(panel.character, 'sorcery-points');
  return [
    ClassResourceCountRow(
      panel: panel,
      resource: points,
      keyPrefix: 'sorcerer-points',
      title: 'Puntos de hechicería',
      label: 'Puntos',
      icon: AppIcons.sparkles,
      minLevel: 2,
      subtitle: panel.level >= 3
          ? 'Para la metamagia y la magia flexible; se gastan y recuperan en "Recursos".'
          : 'Para la magia flexible; se gastan y recuperan en "Recursos".',
    ),
    if (panel.level >= 2) FlexibleCastingRow(panel: panel, points: points),
  ];
}

List<Widget> _more(ClassPanelContext panel, WidgetRef ref) {
  final subclass = panel.character.classes
      .where((k) => k.classIndex == panel.classIndex)
      .firstOrNull
      ?.subclassIndex;
  final tables = subclass == null
      ? const <RollTable>[]
      : ref.watch(rollTablesProvider(subclass)).value ?? const <RollTable>[];
  return [
    if (tables.isNotEmpty)
      Builder(
        builder: (context) => CombatActionRow(
          // No info button: roll tables of a subclass, not a feature of the SRD.
          icon: AppIcons.d20,
          title: 'Tablas de la subclase',
          subtitle: 'Busca el resultado de una tirada.',
          actions: [
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
  ];
}

/// Flexible Casting: one chip per slot level to turn it into points, and one
/// per level to turn points into a slot.
class FlexibleCastingRow extends ConsumerWidget {
  const FlexibleCastingRow({super.key, required this.panel, required this.points});

  final ClassPanelContext panel;
  final CharacterResource? points;

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
    final points = this.points;
    final remaining = remainingOf(points);
    final slots = regularSlots(panel.character);
    final canConvert = panel.canEdit && points != null && points.max > 0;
    return CombatActionRow(
      icon: AppIcons.sparkles,
      title: 'Magia flexible',
      // SRD: both conversions of Flexible Casting are made "as a bonus action".
      actionKind: ActionKind.bonusAction,
      subtitle: 'Solo recupera espacios gastados, hasta nivel 5.',
      info: const FeatureInfoButton(index: 'font-of-magic'),
      details: [
        Text('Espacio → puntos', style: theme.textTheme.labelLarge),
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
        const SizedBox(height: 8),
        Text('Puntos → espacio', style: theme.textTheme.labelLarge),
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
                      canConvert && slot.used > 0 && remaining >= sorcerySlotCost[slot.level]!
                      ? () => _pointsToSlot(context, ref, points, slot.level)
                      : null,
                ),
          ],
        ),
      ],
    );
  }
}
