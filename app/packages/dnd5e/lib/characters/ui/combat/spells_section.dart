import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/components.dart';
import 'package:opentrpg_core/core/ui/stat_value.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';

import '../../../catalog/data/models.dart' show RollTable, SpellDetail;
import '../../../catalog/domain/catalog_format.dart' show spellLevelLabel;
import '../../../catalog/ui/catalog_detail_links.dart';
import '../../../ui/action_type.dart';
import '../../../ui/spell_category.dart';
import '../../dnd5e_characters_controller.dart';
import '../../domain/character_format.dart';
import '../../domain/spell_combat.dart';
import '../../models.dart';
import '../level_up/level_up_widgets.dart' show ExpandableText;
import '../skill_rolls.dart' show pickAdvantageMode;
import 'combat_support.dart';
import 'concentration_flow.dart' show confirmReplaceConcentration;
import 'resources_section.dart' show pactSlotsOf, regularSlots;
import 'wild_magic_surge.dart';

/// A castable spell with its catalog detail.
typedef _CombatSpell = ({CharacterSpell spell, SpellDetail detail});

/// "Conjuros": every spell the character can cast now (cantrips, prepared,
/// always prepared and known spells; catalog detail from [spellInfoProvider]).
/// Each card opens the spell's detail, shows its action type, rolls the spell
/// attack, shows the save DC, rolls damage or healing at the chosen cast level
/// (each number with its breakdown), doubles the dice with "Crítico" and
/// "Lanzar" spends a slot (and starts concentrating when the spell needs it).
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
                when isCastable(c, s, s.level ?? detail.level) && seen.add(s.spellIndex))
              (spell: s, detail: detail),
        ]..sort((a, b) {
          final byLevel = a.detail.level.compareTo(b.detail.level);
          return byLevel != 0 ? byLevel : a.detail.name.compareTo(b.detail.name);
        });
    if (spells.isEmpty) return const SizedBox.shrink();
    final surgeTable = wildMagicSurgeTable(ref, c);

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
            surgeTable: surgeTable,
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
    this.surgeTable,
  });

  final CharacterDetail character;
  final CharacterSpell spell;
  final SpellDetail detail;
  final bool canEdit;

  /// Wild Magic Surge table of the character's subclass: spending a slot
  /// offers to roll for a surge.
  final RollTable? surgeTable;

  @override
  ConsumerState<SpellCard> createState() => _SpellCardState();
}

class _SpellCardState extends ConsumerState<SpellCard> {
  /// The dice of the damage are doubled (set by a natural 20 on the attack).
  bool _critical = false;

  /// Chosen slot level; null = the lowest one available.
  int? _castLevel;

  /// A slot was just spent with a Wild Magic Surge subclass: the surge prompt
  /// is shown until closed.
  bool _surgePending = false;

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
  /// else a pact slot of that level. A concentration spell then becomes the
  /// one the character concentrates on.
  Future<void> _cast() async {
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
    final concentration = _detail.concentration;
    if (concentration &&
        !await confirmReplaceConcentration(context, ref, widget.character, _index)) {
      return;
    }
    if (!mounted) return;
    final controller = ref.read(dnd5eCharacterControllerProvider(widget.character.id).notifier);
    final spent = await runCombat(
      context,
      () => controller.spendSpellSlot(slotLevel),
      success: concentration ? null : 'Espacio de nivel $level gastado: ${_detail.name}.',
    );
    if (!spent || !mounted) return;
    if (concentration) {
      await runCombat(
        context,
        () => controller.setConcentration(_index),
        success: 'Espacio de nivel $level gastado. Concentrándote en ${_detail.name}.',
      );
      if (!mounted) return;
    }
    if (level >= 1 && widget.surgeTable != null) setState(() => _surgePending = true);
  }

  /// A concentration cantrip: only starts concentrating (nothing is spent).
  Future<void> _concentrate() async {
    if (!await confirmReplaceConcentration(context, ref, widget.character, _index)) return;
    if (!mounted) return;
    await runCombat(
      context,
      () => ref
          .read(dnd5eCharacterControllerProvider(widget.character.id).notifier)
          .setConcentration(_index),
      success: 'Concentrándote en ${_detail.name}.',
    );
  }

  /// "Ataque a distancia" / "Ataque cuerpo a cuerpo" / "Ataque".
  String get _attackLabel => switch (_detail.attackType) {
    'ranged' => 'Ataque a distancia',
    'melee' => 'Ataque cuerpo a cuerpo',
    _ => 'Ataque',
  };

  String? get _damageTypeText {
    final type = _detail.damageType;
    if (type == null || type.isEmpty) return null;
    return type.split(' + ').map(damageTypeLabel).join(' + ');
  }

  /// "Ataque a distancia +7": a label and a value with its breakdown.
  Widget _fact(String label, Widget value) {
    final theme = Theme.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          label,
          style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        const SizedBox(width: 2),
        value,
      ],
    );
  }

  /// Attack, save DC, damage and healing, each one with its breakdown.
  List<Widget> _facts(Spellcasting? casting) {
    final theme = Theme.of(context);
    final d = _detail;
    final c = widget.character;
    final style = numericStyle(theme.textTheme.bodyLarge);
    final breakdowns = c.sheet.breakdowns;
    final level = _level;
    final source = spellDamageSource(d, castLevel: level, characterLevel: c.totalLevel);
    final damage = _damage;
    final damageType = _damageTypeText;
    final healFormula = scaledValue(d.healAtSlotLevel, level);
    final heal = _heal;
    final modifier = _abilityModifier;
    final ability = casting?.ability ?? '';
    return [
      if (d.attackType != null && casting != null)
        _fact(
          _attackLabel,
          StatValue(
            statKey: 'spell.$_index.attack',
            title: 'Ataque de conjuro: ${d.name}',
            text: formatModifier(casting.attackBonus),
            breakdown: breakdowns['spellAttackBonus.${casting.classIndex}'],
            style: style,
          ),
        ),
      if (d.dcAbility != null)
        casting == null
            ? Text('Salvación de ${abilityName(d.dcAbility!)}', style: theme.textTheme.bodyMedium)
            : _fact(
                'Salvación',
                StatValue(
                  statKey: 'spell.$_index.dc',
                  title: 'CD de salvación: ${d.name}',
                  text: 'CD ${casting.saveDc} (${abilityName(d.dcAbility!)})',
                  totalText: 'CD ${casting.saveDc}',
                  breakdown: breakdowns['spellSaveDc.${casting.classIndex}'],
                  style: style,
                ),
              ),
      if (source != null)
        _fact(
          'Daño',
          StatValue(
            statKey: 'spell.$_index.damage',
            title: 'Daño: ${d.name}',
            text: [source.dice.replaceAll(RegExp(r'\s+'), ''), ?damageType].join(' '),
            totalText: [damage ?? source.dice, ?damageType].join(' '),
            lines: [
              source.byCharacterLevel
                  ? 'Tabla del conjuro a nivel de personaje ${c.totalLevel}: ${source.dice}'
                  : 'Tabla del conjuro a nivel $level: ${source.dice}',
              if (_critical && damage != null) 'Crítico: dados doblados ($damage)',
              'Sin modificador de característica',
            ],
            style: style,
          ),
        ),
      if (heal != null && healFormula != null)
        _fact(
          'Cura',
          StatValue(
            statKey: 'spell.$_index.heal',
            title: 'Curación: ${d.name}',
            text: heal,
            lines: ['Tabla del conjuro a nivel $level: $healFormula'],
            breakdown: ValueBreakdown(
              total: modifier,
              parts: [
                if (healFormula.toUpperCase().contains('MOD'))
                  BreakdownPart(
                    source: 'ability',
                    label: ability.isEmpty
                        ? 'Modificador de característica'
                        : 'Modificador de ${abilityName(ability)}',
                    value: modifier,
                  ),
              ],
            ),
            style: style,
          ),
        ),
    ];
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final d = _detail;
    final casting = _casting;
    final damage = _damage;
    final heal = _heal;
    final levels = _levels;
    final facts = _facts(casting);
    final concentrating = widget.character.concentratingOnSpellIndex == _index;
    final about = [
      ?d.range,
      ?d.duration,
      if (d.concentration) 'Concentración',
      if (d.ritual) 'Ritual',
    ].where((e) => e.isNotEmpty).join(' · ');

    return CombatCard(
      title: d.name,
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          DetailInfoButton(
            key: Key('detail-spell-$_index'),
            onPressed: () => openSpellDetail(context, _index),
          ),
          const SizedBox(width: 4),
          SpellCategoryIcon(widget.spell.category ?? d.category),
          const SizedBox(width: 6),
          Text(spellLevelLabel(d.level), style: theme.textTheme.bodySmall),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (d.castingTime != null || about.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Wrap(
                spacing: 8,
                runSpacing: 4,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  if (d.castingTime != null) ActionTypeChip.castingTime(d.castingTime),
                  if (about.isNotEmpty)
                    Text(about, key: Key('spell-about-$_index'), style: theme.textTheme.bodySmall),
                ],
              ),
            ),
          if (facts.isNotEmpty)
            Wrap(
              key: Key('spell-facts-$_index'),
              spacing: 12,
              runSpacing: 4,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: facts,
            )
          else if (d.description.isNotEmpty)
            ExpandableText([d.description.first], key: Key('spell-summary-$_index')),
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
                  onPressed: widget.canEdit ? _cast : null,
                  child: const Text('Lanzar'),
                )
              else if (d.concentration)
                OutlinedButton(
                  key: Key('spell-concentrate-$_index'),
                  onPressed: widget.canEdit && !concentrating ? _concentrate : null,
                  child: Text(concentrating ? 'Concentrado' : 'Concentrarse'),
                ),
            ],
          ),
          if (_surgePending && widget.surgeTable != null)
            WildMagicSurgePrompt(
              character: widget.character,
              table: widget.surgeTable!,
              canEdit: widget.canEdit,
              onDismiss: () => setState(() => _surgePending = false),
            ),
        ],
      ),
    );
  }
}
