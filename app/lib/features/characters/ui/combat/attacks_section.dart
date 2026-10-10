import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../systems/dnd5e/ui/action_type.dart';
import '../../../../core/ui/stat_value.dart';
import '../../../catalog/ui/catalog_detail_links.dart' show DetailInfoButton;
import '../../../dice/domain/dice_expression.dart';
import '../../../dice/ui/dice_sheet.dart';
import '../../data/models.dart';
import '../../domain/character_format.dart';
import 'combat_state.dart';
import 'combat_support.dart';
import 'resources_section.dart' show openCombatItemDetail;

/// One card per attack (an action) with "Tirar ataque" and "Tirar daño"; a
/// weapon opens its item detail.
class AttacksSection extends ConsumerWidget {
  const AttacksSection({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final attacks = character.combat.attacks;
    final rage = ref.watch(rageControllerProvider(character.id));
    final rageBonus = rage.active ? rageDamageBonusOf(character) : 0;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionHeader('Ataques', padding: combatSectionPadding),
        if (attacks.isEmpty) const Text('Sin ataques disponibles.'),
        for (var i = 0; i < attacks.length; i++)
          AttackCard(
            key: Key('attack-$i'),
            index: i,
            attack: attacks[i],
            rageBonus: attacks[i].isRanged ? 0 : rageBonus,
            onDetail: attacks[i].itemId == null
                ? null
                : () => openCombatItemDetail(context, ref, character.id, attacks[i].itemId!),
          ),
      ],
    );
  }
}

class AttackCard extends StatefulWidget {
  const AttackCard({
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
  State<AttackCard> createState() => _AttackCardState();
}

class _AttackCardState extends State<AttackCard> {
  /// The last attack roll was a natural 20: the damage dice are doubled.
  bool _critical = false;
  bool _twoHanded = false;

  CombatAttack get _attack => widget.attack;

  Future<void> _rollAttack([AdvantageMode mode = AdvantageMode.normal]) async {
    await rollAndShow(
      context,
      d20Expression(_attack.attackBonus, mode: mode),
      label: 'Ataque: ${_attack.name}',
      kind: RollKind.attack,
      onResult: (result) {
        if (!mounted) return;
        setState(() => _critical = result.isCritical);
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
    return CombatCard(
      title: a.name,
      actionKind: ActionKind.action,
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (widget.onDetail != null)
            DetailInfoButton(key: Key('detail-item-${a.itemId}'), onPressed: widget.onDetail!),
          StatValue(
            statKey: 'attack.${widget.index}.bonus',
            textKey: Key('attack-bonus-${widget.index}'),
            title: 'Ataque: ${a.name}',
            text: formatModifier(a.attackBonus),
            breakdown: a.attackBreakdown,
            style: numericStyle(theme.textTheme.headlineSmall),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
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
            style: numericStyle(theme.textTheme.bodyLarge),
          ),
          if (details.isNotEmpty) Text(details, style: theme.textTheme.bodySmall),
          if (a.notes != null) Text(a.notes!, style: theme.textTheme.bodySmall),
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
          Wrap(
            spacing: 8,
            children: [
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
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: FilledButton(
                  key: Key('attack-roll-${widget.index}'),
                  onPressed: _rollAttack,
                  onLongPress: _chooseMode,
                  child: const Text('Tirar ataque'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: FilledButton.tonal(
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
              ),
            ],
          ),
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Text(
              'Mantén pulsado "Tirar ataque" para ventaja o desventaja.',
              style: theme.textTheme.bodySmall,
            ),
          ),
        ],
      ),
    );
  }
}
