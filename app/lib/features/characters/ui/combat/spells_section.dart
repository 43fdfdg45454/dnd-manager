import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/components.dart';
import '../../../../core/ui/spell_category.dart';
import '../../../catalog/data/models.dart' show SpellDetail;
import '../../../catalog/domain/catalog_format.dart' show spellLevelLabel;
import '../../../dice/domain/dice_expression.dart';
import '../../../dice/ui/dice_sheet.dart';
import '../../data/characters_controller.dart';
import '../../data/models.dart';
import '../../domain/character_format.dart';
import '../../domain/spell_combat.dart';
import 'combat_support.dart';
import 'resources_section.dart' show pactSlotsOf, regularSlots;
import 'skill_rolls.dart' show pickAdvantageMode;

/// A castable spell with its catalog detail.
typedef _CombatSpell = ({CharacterSpell spell, SpellDetail detail});

/// "Conjuros": cantrips and prepared or known spells with an attack, a saving
/// throw, damage or healing (catalog detail from [spellInfoProvider]). Each
/// card rolls the spell attack, shows the save DC, rolls damage or healing at
/// the chosen cast level, doubles the dice with "Crítico" and spends a slot.
class SpellsSection extends ConsumerWidget {
  const SpellsSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    if (c.spells.isEmpty) return const SizedBox.shrink();
    final info = ref
        .watch(spellInfoProvider(spellInfoKey(c.spells.map((s) => s.spellIndex))))
        .value;
    if (info == null) return const SizedBox.shrink();

    final seen = <String>{};
    final spells =
        <_CombatSpell>[
          for (final s in c.spells)
            if (info[s.spellIndex] case final SpellDetail detail
                when isCombatSpell(detail) &&
                    isCastable(c, s, s.level ?? detail.level) &&
                    seen.add(s.spellIndex))
              (spell: s, detail: detail),
        ]..sort((a, b) {
          final byLevel = a.detail.level.compareTo(b.detail.level);
          return byLevel != 0 ? byLevel : a.detail.name.compareTo(b.detail.name);
        });
    if (spells.isEmpty) return const SizedBox.shrink();

    return Column(
      key: const Key('combat-spells'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionHeader('Conjuros', padding: combatSectionPadding),
        for (final s in spells)
          SpellCard(
            key: Key('combat-spell-${s.detail.index}'),
            character: c,
            spell: s.spell,
            detail: s.detail,
            canEdit: canEdit,
          ),
      ],
    );
  }
}

class SpellCard extends ConsumerStatefulWidget {
  const SpellCard({
    super.key,
    required this.character,
    required this.spell,
    required this.detail,
    required this.canEdit,
  });

  final CharacterDetail character;
  final CharacterSpell spell;
  final SpellDetail detail;
  final bool canEdit;

  @override
  ConsumerState<SpellCard> createState() => _SpellCardState();
}

class _SpellCardState extends ConsumerState<SpellCard> {
  /// The dice of the damage are doubled (set by a natural 20 on the attack).
  bool _critical = false;

  /// Chosen slot level; null = the lowest one available.
  int? _castLevel;

  SpellDetail get _detail => widget.detail;
  String get _index => _detail.index;

  List<int> get _levels =>
      castLevels(_detail.level, regularSlots(widget.character), pactSlotsOf(widget.character));

  int get _level {
    final levels = _levels;
    final chosen = _castLevel;
    return chosen != null && levels.contains(chosen) ? chosen : levels.first;
  }

  Spellcasting? get _casting => spellcastingFor(widget.character, widget.spell);

  int get _abilityModifier {
    final ability = _casting?.ability;
    if (ability == null || ability.isEmpty) return 0;
    return widget.character.sheet.abilities[abilityKeyOf(ability)]?.modifier ?? 0;
  }

  Future<void> _rollAttack([AdvantageMode mode = AdvantageMode.normal]) async {
    await rollAndShow(
      context,
      d20Expression(_casting?.attackBonus ?? 0, mode: mode),
      label: 'Ataque: ${_detail.name}',
      onResult: (result) {
        if (!mounted) return;
        setState(() => _critical = result.isCritical);
      },
    );
  }

  Future<void> _chooseMode() async {
    final mode = await pickAdvantageMode(context, 'Ataque: ${_detail.name}');
    if (mode == null || !mounted) return;
    await _rollAttack(mode);
  }

  String? get _damage => spellDamageExpression(
    _detail,
    castLevel: _level,
    characterLevel: widget.character.totalLevel,
    critical: _critical,
  );

  String? get _heal => spellHealExpression(_detail, castLevel: _level, modifier: _abilityModifier);

  String get _levelSuffix => _detail.level == 0 ? '' : ' (nivel $_level)';

  Future<void> _rollDamage() async {
    final expression = _damage;
    if (expression == null) return;
    await rollAndShow(
      context,
      expression,
      label: 'Daño: ${_detail.name}$_levelSuffix${_critical ? ' (crítico)' : ''}',
    );
  }

  Future<void> _rollHeal() async {
    final expression = _heal;
    if (expression == null) return;
    await rollAndShow(context, expression, label: 'Curación: ${_detail.name}$_levelSuffix');
  }

  /// Spends a slot of the cast level: a regular one when there is one left,
  /// else a pact slot of that level.
  Future<void> _spendSlot() async {
    final level = _level;
    final regular = regularSlots(widget.character).where((s) => s.level == level).firstOrNull;
    final pact = pactSlotsOf(widget.character);
    final int slotLevel;
    if (regular != null && regular.used < regular.max) {
      slotLevel = level;
    } else if (pact != null && pact.level == level && pact.used < pact.max) {
      slotLevel = 0;
    } else {
      showCombatMessage(context, 'No quedan espacios de nivel $level.');
      return;
    }
    await runCombat(
      context,
      () => ref
          .read(characterControllerProvider(widget.character.id).notifier)
          .spendSpellSlot(slotLevel),
      success: 'Espacio de nivel $level gastado: ${_detail.name}.',
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final d = _detail;
    final casting = _casting;
    final damage = _damage;
    final baseDamage = spellDamageExpression(
      d,
      castLevel: _level,
      characterLevel: widget.character.totalLevel,
    );
    final heal = _heal;
    final levels = _levels;
    final facts = [
      if (d.attackType != null && casting != null)
        'Ataque ${formatModifier(casting.attackBonus)}'
            '${d.attackType == 'ranged'
                ? ' a distancia'
                : d.attackType == 'melee'
                ? ' cuerpo a cuerpo'
                : ''}',
      if (d.dcAbility != null)
        'Salvación de ${abilityName(d.dcAbility!)}${casting == null ? '' : ' CD ${casting.saveDc}'}',
      if (baseDamage != null)
        [
          baseDamage,
          if (d.damageType != null) d.damageType!.split(' + ').map(damageTypeLabel).join(' + '),
        ].join(' '),
      if (heal != null) 'Cura $heal',
    ];

    return CombatCard(
      title: d.name,
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          SpellCategoryIcon(widget.spell.category ?? d.category),
          const SizedBox(width: 6),
          Text(spellLevelLabel(d.level), style: theme.textTheme.bodySmall),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (facts.isNotEmpty)
            Text(
              facts.join(' · '),
              key: Key('spell-facts-$_index'),
              style: theme.textTheme.bodyMedium,
            ),
          if (d.level > 0 && levels.length > 1)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Row(
                children: [
                  Text('Lanzar a nivel', style: theme.textTheme.bodyMedium),
                  const SizedBox(width: 12),
                  DropdownButton<int>(
                    key: Key('spell-level-$_index'),
                    value: _level,
                    items: [for (final l in levels) DropdownMenuItem(value: l, child: Text('$l'))],
                    onChanged: (value) => setState(() => _castLevel = value),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              if (d.attackType != null)
                FilledButton(
                  key: Key('spell-attack-$_index'),
                  onPressed: _rollAttack,
                  onLongPress: _chooseMode,
                  child: const Text('Tirar ataque'),
                ),
              if (damage != null)
                FilledButton.tonal(
                  key: Key('spell-damage-$_index'),
                  onPressed: _rollDamage,
                  child: const Text('Tirar daño'),
                ),
              if (heal != null)
                FilledButton.tonal(
                  key: Key('spell-heal-$_index'),
                  onPressed: _rollHeal,
                  child: const Text('Tirar curación'),
                ),
              if (damage != null)
                FilterChip(
                  key: Key('spell-crit-$_index'),
                  label: const Text('Crítico'),
                  selected: _critical,
                  onSelected: (value) => setState(() => _critical = value),
                ),
              if (d.level > 0)
                OutlinedButton(
                  key: Key('spell-spend-$_index'),
                  onPressed: widget.canEdit ? _spendSlot : null,
                  child: const Text('Gastar espacio'),
                ),
            ],
          ),
        ],
      ),
    );
  }
}
