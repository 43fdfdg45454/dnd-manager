import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/ui/stat_value.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';

import '../../../catalog/ui/catalog_detail_links.dart' show DetailInfoButton;
import '../../../ui/action_type.dart';
import '../../domain/character_format.dart';
import '../../models.dart';
import 'combat_rows.dart';
import 'combat_state.dart';
import 'combat_support.dart';
import 'resources_section.dart' show openCombatItemDetail;

/// "Ataques": one row per attack (an action) with "Tirar ataque" (long press
/// for advantage or disadvantage) and "Tirar daño"; a weapon opens its item
/// detail.
class AttacksSection extends ConsumerWidget {
  const AttacksSection({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final attacks = character.combat.attacks;
    if (attacks.isEmpty) return const SizedBox.shrink();
    final rage = ref.watch(rageControllerProvider(character.id));
    final rageBonus = rage.active ? rageDamageBonusOf(character) : 0;
    return CombatGroup(
      key: const Key('combat-attacks'),
      title: 'Ataques',
      children: [
        for (var i = 0; i < attacks.length; i++)
          AttackRow(
            key: Key('attack-$i'),
            index: i,
            attack: attacks[i],
            rageBonus: attacks[i].isRanged ? 0 : rageBonus,
            onDetail: attacks[i].itemId == null
                ? null
                : () => openCombatItemDetail(context, ref, character.id, attacks[i].itemId!),
          ),
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 6),
          child: Text(
            'Mantén pulsado "Tirar ataque" para ventaja o desventaja.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ),
      ],
    );
  }
}

class AttackRow extends StatefulWidget {
  const AttackRow({
    super.key,
    required this.index,
    required this.attack,
    this.rageBonus = 0,
    this.onDetail,
  });

  final int index;
  final CombatAttack attack;

  /// Rage damage added to this attack (0 when not raging or for ranged attacks).
  final int rageBonus;

  /// Opens the weapon's item detail; null for the unarmed strike.
  final VoidCallback? onDetail;

  @override
  State<AttackRow> createState() => _AttackRowState();
}

class _AttackRowState extends State<AttackRow> {
  /// The last attack roll was a critical (natural 20, or from the attack's
  /// `criticalRange`): the damage dice are doubled.
  bool _critical = false;
  bool _twoHanded = false;

  CombatAttack get _attack => widget.attack;

  Future<void> _rollAttack([AdvantageMode mode = AdvantageMode.normal]) async {
    await rollAndShow(
      context,
      d20Expression(_attack.attackBonus, mode: mode),
      label: 'Ataque: ${_attack.name}',
      kind: RollKind.attack,
      criticalRange: _attack.criticalRange,
      onResult: (result) {
        if (!mounted) return;
        setState(() => _critical = result.isCriticalFrom(_attack.criticalRange));
      },
    );
  }

  Future<void> _chooseMode() async {
    final mode = await showModalBottomSheet<AdvantageMode>(
      context: context,
      showDragHandle: true,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(title: Text('Ataque: ${_attack.name}')),
            ListTile(
              key: const Key('mode-normal'),
              leading: const AppIcon(AppIcons.d20),
              title: const Text('Normal'),
              onTap: () => Navigator.of(sheetContext).pop(AdvantageMode.normal),
            ),
            ListTile(
              key: const Key('mode-advantage'),
              leading: const Icon(Icons.arrow_upward),
              title: const Text('Con ventaja'),
              onTap: () => Navigator.of(sheetContext).pop(AdvantageMode.advantage),
            ),
            ListTile(
              key: const Key('mode-disadvantage'),
              leading: const Icon(Icons.arrow_downward),
              title: const Text('Con desventaja'),
              onTap: () => Navigator.of(sheetContext).pop(AdvantageMode.disadvantage),
            ),
          ],
        ),
      ),
    );
    if (mode == null || !mounted) return;
    await _rollAttack(mode);
  }

  /// The damage expression: base dice, the rage bonus, and doubled dice on a critical.
  String _damageExpression() {
    final base = _twoHanded && _attack.versatileDamage != null
        ? _attack.versatileDamage!
        : _attack.damage;
    final parsed = DiceExpression.tryParse(base);
    var expression = parsed != null && _critical ? parsed.doubleDice().toString() : base;
    if (widget.rageBonus != 0) expression += bonusSuffix(widget.rageBonus);
    return expression;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final a = _attack;
    final details = [if (a.range != null) 'Alcance ${a.range}', ...a.properties].join(' · ');
    final small = theme.textTheme.bodySmall;
    return CombatActionRow(
      icon: a.isRanged ? AppIcons.bow : AppIcons.sword,
      title: a.name,
      actionKind: ActionKind.action,
      badge: StatValue(
        statKey: 'attack.${widget.index}.bonus',
        textKey: Key('attack-bonus-${widget.index}'),
        title: 'Ataque: ${a.name}',
        text: formatModifier(a.attackBonus),
        breakdown: a.attackBreakdown,
        style: numericStyle(theme.textTheme.titleMedium),
      ),
      info: widget.onDetail == null
          ? null
          : DetailInfoButton(key: Key('detail-item-${a.itemId}'), onPressed: widget.onDetail!),
      action: FilledButton(
        key: Key('attack-roll-${widget.index}'),
        onPressed: _rollAttack,
        onLongPress: _chooseMode,
        child: const Text('Tirar ataque', maxLines: 1, overflow: TextOverflow.ellipsis),
      ),
      details: [
        StatValue(
          statKey: 'attack.${widget.index}.damage',
          textKey: Key('attack-damage-text-${widget.index}'),
          title: 'Daño: ${a.name}',
          text: [
            a.damage.isEmpty ? 'Sin daño' : a.damage,
            if (a.damageType.isNotEmpty) a.damageType,
          ].join(' '),
          totalText: a.damageBreakdown == null ? null : formatModifier(a.damageBreakdown!.total),
          breakdown: a.damage.isEmpty ? null : a.damageBreakdown,
          style: numericStyle(theme.textTheme.bodyMedium),
        ),
        if (details.isNotEmpty) Text(details, style: small),
        if (a.notes != null)
          Text(
            a.natural ? 'Arma natural · ${a.notes}' : a.notes!,
            key: Key('attack-notes-${widget.index}'),
            style: small,
          ),
        if (a.criticalRange < 20)
          Text(
            [
              'Crítico con ${a.criticalRange}-20',
              if (a.criticalRangeSource != null) a.criticalRangeSource!,
            ].join(' · '),
            key: Key('attack-crit-range-${widget.index}'),
            style: small,
          ),
        for (var w = 0; w < a.warnings.length; w++)
          Padding(
            padding: const EdgeInsets.only(top: 2),
            child: Row(
              key: Key('attack-warning-${widget.index}-$w'),
              children: [
                Icon(Icons.warning_amber, size: 16, color: theme.colorScheme.error),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    a.warnings[w],
                    style: small?.copyWith(color: theme.colorScheme.error),
                  ),
                ),
              ],
            ),
          ),
        if (widget.rageBonus != 0)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Chip(
              key: Key('attack-rage-${widget.index}'),
              avatar: const AppIcon(AppIcons.flame, size: 18),
              label: Text('Furia ${formatModifier(widget.rageBonus)} al daño'),
              visualDensity: VisualDensity.compact,
            ),
          ),
      ],
      actions: [
        if (a.versatileDamage != null)
          FilterChip(
            key: Key('attack-versatile-${widget.index}'),
            label: Text('Dos manos (${a.versatileDamage})'),
            selected: _twoHanded,
            onSelected: (value) => setState(() => _twoHanded = value),
          ),
        FilterChip(
          key: Key('attack-crit-${widget.index}'),
          label: const Text('Crítico'),
          selected: _critical,
          onSelected: (value) => setState(() => _critical = value),
        ),
        FilledButton.tonal(
          key: Key('attack-damage-${widget.index}'),
          onPressed: a.damage.isEmpty
              ? null
              : () => rollAndShow(
                  context,
                  _damageExpression(),
                  label: 'Daño: ${a.name}${_critical ? ' (crítico)' : ''}',
                ),
          child: const Text('Tirar daño'),
        ),
      ],
    );
  }
}
