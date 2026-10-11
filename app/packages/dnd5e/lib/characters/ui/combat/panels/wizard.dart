import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';

import '../../../../ui/spell_category.dart';
import '../../../dnd5e_characters_controller.dart' show spellInfoKey, spellInfoProvider;
import '../../../models.dart';
import '../../character_tabs.dart' show titleFromSpellIndex;
import '../combat_rows.dart';
import '../combat_support.dart';
import '../resources_section.dart' show regularSlots;
import 'panel_support.dart';

/// Wizard: the spellbook with the prepared spells ("Conjuros") and Arcane
/// Recovery after a short rest ("Más").
const wizardPanel = ClassPanelSpec(actions: _actions, more: _more, spells: _spells);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) => const [];

List<Widget> _more(ClassPanelContext panel, WidgetRef ref) => [ArcaneRecoveryRow(panel: panel)];

List<Widget> _spells(ClassPanelContext panel, WidgetRef ref) => [SpellbookRow(panel: panel)];

/// The spellbook: how many spells are prepared and, unfolded, every spell
/// with its level and whether it is prepared.
class SpellbookRow extends ConsumerWidget {
  const SpellbookRow({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = panel.panel.data;
    final spellbook = panelStrings(data['spellbook']);
    final prepared = panelStrings(data['prepared']).toSet();
    final preparedMax = panelInt(data['preparedMax']);
    final info = spellbook.isEmpty
        ? null
        : ref.watch(spellInfoProvider(spellInfoKey(spellbook))).value;
    return ExpansionTile(
      key: const Key('wizard-spellbook'),
      tilePadding: EdgeInsets.zero,
      leading: AppIcon(AppIcons.spellbook, color: Theme.of(context).colorScheme.primary),
      title: const Text('Libro de hechizos'),
      subtitle: Text(
        preparedMax == null
            ? 'Preparados: ${prepared.length}'
            : 'Preparados: ${prepared.length} / $preparedMax',
        key: const Key('wizard-prepared-count'),
      ),
      children: [
        if (spellbook.isEmpty) const ListTile(title: Text('El libro está vacío.')),
        for (final index in spellbook)
          ListTile(
            key: Key('spellbook-$index'),
            dense: true,
            contentPadding: EdgeInsets.zero,
            leading: Icon(
              prepared.contains(index) ? Icons.check_circle : Icons.circle_outlined,
              size: 20,
              semanticLabel: prepared.contains(index) ? 'Preparado' : 'No preparado',
            ),
            title: Text(info?[index]?.name ?? titleFromSpellIndex(index)),
            trailing: SpellCategoryIcon(info?[index]?.category),
            subtitle: info?[index] == null
                ? null
                : Text(info![index]!.level == 0 ? 'Truco' : 'Nivel ${info[index]!.level}'),
          ),
      ],
    );
  }
}

/// Arcane Recovery: once a day, after a short rest, spell slots whose levels
/// add up to half the wizard level.
class ArcaneRecoveryRow extends ConsumerWidget {
  const ArcaneRecoveryRow({super.key, required this.panel});

  final ClassPanelContext panel;

  Future<void> _arcaneRecovery(BuildContext context, WidgetRef ref, int budget) async {
    final levels = await showDialog<List<int>>(
      context: context,
      builder: (_) => ArcaneRecoveryDialog(character: panel.character, budget: budget),
    );
    if (levels == null || levels.isEmpty || !context.mounted) return;
    await runCombat(
      context,
      () => panelController(ref, panel.character).arcaneRecovery(levels),
      success: 'Recuperación arcana aplicada.',
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final recovery = panelMap(panel.panel.data['arcaneRecovery']);
    final recovered = recovery['used'] == true || (panelInt(recovery['used']) ?? 0) > 0;
    final budget = panelInt(recovery['slotLevelsRecoverable']) ?? (panel.panel.level + 1) ~/ 2;
    return CombatActionRow(
      icon: AppIcons.spellbook,
      title: 'Recuperación arcana',
      cost: recovered ? 'Usada' : '1/día',
      subtitle:
          'Tras un descanso corto, recupera espacios de conjuro cuyo nivel sumado sea '
          '$budget como máximo (ninguno de nivel 6 o superior).',
      info: const FeatureInfoButton(index: 'arcane-recovery'),
      action: FilledButton.tonal(
        key: const Key('arcane-recovery'),
        onPressed: panel.canEdit && !recovered ? () => _arcaneRecovery(context, ref, budget) : null,
        child: Text(
          recovered ? 'Recuperación arcana (usada)' : 'Recuperar',
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
      ),
    );
  }
}

/// Picks the spell slots to recover: any number per level while the sum of the
/// levels stays within [budget] and no slot above level 5 is chosen. Resolves
/// to the chosen levels, one entry per slot (`[1, 1, 2]`).
class ArcaneRecoveryDialog extends StatefulWidget {
  const ArcaneRecoveryDialog({
    super.key,
    required this.character,
    required this.budget,
    this.title = 'Recuperación arcana',
  });

  final CharacterDetail character;
  final int budget;

  /// "Recuperación natural" for the druid, which follows the same rules.
  final String title;

  @override
  State<ArcaneRecoveryDialog> createState() => _ArcaneRecoveryDialogState();
}

class _ArcaneRecoveryDialogState extends State<ArcaneRecoveryDialog> {
  final Map<int, int> _chosen = {};

  int get _sum => _chosen.entries.fold(0, (s, e) => s + e.key * e.value);

  @override
  Widget build(BuildContext context) {
    final slots = [
      for (final s in regularSlots(widget.character))
        if (s.level <= 5) s,
    ];
    final left = widget.budget - _sum;
    return AlertDialog(
      title: Text(widget.title),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Niveles seleccionados: $_sum / ${widget.budget}', key: const Key('arcane-sum')),
            const SizedBox(height: 8),
            if (slots.isEmpty) const Text('No tienes espacios de nivel 5 o inferior.'),
            for (final slot in slots)
              Row(
                key: Key('arcane-level-${slot.level}'),
                children: [
                  Expanded(child: Text('Nivel ${slot.level} (gastados ${slot.used})')),
                  IconButton(
                    key: Key('arcane-level-${slot.level}-minus'),
                    tooltip: 'Menos',
                    onPressed: (_chosen[slot.level] ?? 0) > 0
                        ? () => setState(() => _chosen[slot.level] = _chosen[slot.level]! - 1)
                        : null,
                    icon: const Icon(Icons.remove_circle_outline),
                  ),
                  SizedBox(
                    width: 24,
                    child: Text('${_chosen[slot.level] ?? 0}', textAlign: TextAlign.center),
                  ),
                  IconButton(
                    key: Key('arcane-level-${slot.level}-plus'),
                    tooltip: 'Más',
                    onPressed: (_chosen[slot.level] ?? 0) < slot.used && slot.level <= left
                        ? () => setState(() => _chosen[slot.level] = (_chosen[slot.level] ?? 0) + 1)
                        : null,
                    icon: const Icon(Icons.add_circle_outline),
                  ),
                ],
              ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('arcane-confirm'),
          onPressed: _sum == 0
              ? null
              : () => Navigator.of(context).pop(
                  [
                    for (final e in _chosen.entries)
                      for (var i = 0; i < e.value; i++) e.key,
                  ]..sort(),
                ),
          child: const Text('Recuperar'),
        ),
      ],
    );
  }
}
