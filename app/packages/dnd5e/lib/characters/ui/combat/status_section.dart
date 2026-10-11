import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/theme/tokens.dart';
import 'package:opentrpg_core/core/ui/breakdown.dart';
import 'package:opentrpg_core/core/ui/stat_value.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';

import '../../dnd5e_characters_controller.dart';
import '../../models.dart';
import 'combat_rows.dart';
import 'combat_support.dart';
import 'vitals_section.dart' show ConditionsPanel;

Dnd5eCharacterController _controller(WidgetRef ref, CharacterDetail character) =>
    ref.read(dnd5eCharacterControllerProvider(character.id).notifier);

/// The "Estado" tab of the combat view: first the states of the character
/// ("Condiciones", [ConditionsPanel]: the chips with "+" to add one), then
/// one row per stat with its full name, armor class, initiative (with its
/// roll), speed, passive perception, proficiency bonus and inspiration (a tap
/// toggles it). Each value opens its breakdown ([StatValue]).
class CombatStatusSection extends ConsumerWidget {
  const CombatStatusSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        CombatGroup(
          key: const Key('combat-conditions'),
          title: 'Condiciones',
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 6),
              child: ConditionsPanel(character: character, canEdit: canEdit),
            ),
          ],
        ),
        _StatsGroup(character: character, canEdit: canEdit),
      ],
    );
  }
}

/// The six stats of the "Estado" tab.
class _StatsGroup extends ConsumerWidget {
  const _StatsGroup({required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final sheet = c.sheet;
    final tokens = context.tokens;
    return CombatGroup(
      key: const Key('combat-stats-grid'),
      title: 'Estado',
      children: [
        _StatRow(
          key: const Key('combat-ac'),
          statKey: 'armorClass',
          label: 'Clase de Armadura',
          value: '${sheet.armorClass}',
          breakdown: sheet.breakdown('armorClass'),
        ),
        _StatRow(
          key: const Key('combat-initiative'),
          statKey: 'initiative',
          label: 'Iniciativa',
          value: formatModifier(sheet.initiative),
          breakdown: sheet.breakdown('initiative'),
          trailing: IconButton(
            key: const Key('roll-initiative'),
            tooltip: 'Tirar iniciativa',
            visualDensity: VisualDensity.compact,
            onPressed: () =>
                rollAndShow(context, d20Expression(sheet.initiative), label: 'Iniciativa'),
            icon: AppIcon(AppIcons.d20, size: 20, color: Theme.of(context).colorScheme.primary),
          ),
        ),
        _StatRow(
          key: const Key('combat-speed'),
          statKey: 'speed',
          label: 'Velocidad',
          value: '${sheet.speed} pies',
          breakdown: sheet.breakdown('speed'),
        ),
        _StatRow(
          key: const Key('combat-perception'),
          statKey: 'passivePerception',
          label: 'Percepción pasiva',
          value: '${sheet.passivePerception}',
          breakdown: sheet.breakdown('passivePerception'),
        ),
        _StatRow(
          key: const Key('combat-proficiency'),
          statKey: 'proficiencyBonus',
          label: 'Bonificador de competencia',
          value: formatModifier(sheet.proficiencyBonus),
          breakdown: sheet.breakdown('proficiencyBonus'),
        ),
        KeyedSubtree(
          key: const Key('inspiration'),
          child: _StatRow(
            key: const Key('combat-inspiration'),
            statKey: 'inspiration',
            label: 'Inspiración',
            value: c.inspiration ? 'Sí' : 'No',
            valueColor: c.inspiration ? tokens.oldGold : null,
            onTap: canEdit
                ? () => runCombat(
                    context,
                    () => _controller(ref, c).patchCombat(CombatPatch(inspiration: !c.inspiration)),
                  )
                : null,
          ),
        ),
      ],
    );
  }
}

/// A row of [CombatStatusSection]: the full name on the left, the value (with
/// its breakdown) on the right and, before it, an optional [trailing] control.
class _StatRow extends StatelessWidget {
  const _StatRow({
    super.key,
    required this.statKey,
    required this.label,
    required this.value,
    this.breakdown,
    this.trailing,
    this.onTap,
    this.valueColor,
  });

  final String statKey;
  final String label;
  final String value;
  final ValueBreakdown? breakdown;
  final Widget? trailing;
  final VoidCallback? onTap;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final style = numericStyle(theme.textTheme.titleMedium)?.copyWith(color: valueColor);
    final body = Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Expanded(child: Text(label, style: theme.textTheme.bodyLarge)),
          ?trailing,
          StatValue(
            statKey: statKey,
            title: label,
            text: value,
            breakdown: breakdown,
            style: style,
          ),
        ],
      ),
    );
    return onTap == null
        ? body
        : InkWell(borderRadius: BorderRadius.circular(6), onTap: onTap, child: body);
  }
}
