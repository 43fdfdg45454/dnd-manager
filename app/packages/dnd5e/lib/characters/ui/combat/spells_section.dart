import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/ui/stat_value.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';

import '../../../catalog/data/models.dart' show RollTable, SpellDetail;
import '../../../catalog/ui/catalog_detail_links.dart';
import '../../../ui/action_type.dart';
import '../../../ui/spell_category.dart';
import '../../../dnd5e_routes.dart';
import '../../dnd5e_characters_controller.dart';
import '../../domain/character_format.dart';
import '../../domain/spell_combat.dart';
import '../../models.dart';
import '../level_up/level_up_widgets.dart' show ExpandableText;
import '../skill_rolls.dart' show pickAdvantageMode;
import 'combat_support.dart';
import 'combat_rows.dart';
import 'concentration_flow.dart' show confirmReplaceConcentration;
import 'resources_section.dart' show SpellSlotsSection, pactSlotsOf, regularSlots, resourcesOf;
import 'wild_magic_surge.dart';

/// A spell of the character with its catalog detail.
typedef _CombatSpell = ({CharacterSpell spell, SpellDetail detail});

/// The character's spells with their catalog detail (null while loading),
/// one entry per spell, by level and name. [castableOnly] keeps the ones it
/// can cast now ([isCastable]).
List<_CombatSpell>? _spellsOf(WidgetRef ref, CharacterDetail c, {required bool castableOnly}) {
  if (c.spells.isEmpty) return const [];
  final info = ref.watch(spellInfoProvider(spellInfoKey(c.spells.map((s) => s.spellIndex)))).value;
  if (info == null) return null;
  final seen = <String>{};
  // Castable entries first: a spell known by two classes shows the one that
  // can cast it.
  final castable = <_CombatSpell>[
    for (final s in c.spells)
      if (info[s.spellIndex] case final SpellDetail detail
          when isCastable(c, s, s.level ?? detail.level) && seen.add(s.spellIndex))
        (spell: s, detail: detail),
  ];
  final spells = [
    ...castable,
    if (!castableOnly)
      for (final s in c.spells)
        if (info[s.spellIndex] case final SpellDetail detail when seen.add(s.spellIndex))
          (spell: s, detail: detail),
  ];
  return spells..sort((a, b) {
    final byLevel = a.detail.level.compareTo(b.detail.level);
    return byLevel != 0 ? byLevel : a.detail.name.compareTo(b.detail.name);
  });
}

/// "Conjuros" of the "Acciones" tab: every spell the character can cast now
/// (cantrips, prepared, always prepared and known spells), each with what it
/// costs and one tap to cast it ([SpellRow]).
class SpellsSection extends ConsumerWidget {
  const SpellsSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final spells = _spellsOf(ref, c, castableOnly: true);
    if (spells == null || spells.isEmpty) return const SizedBox.shrink();
    final surgeTable = wildMagicSurgeTable(ref, c);
    return CombatGroup(
      key: const Key('combat-spells'),
      title: 'Conjuros',
      children: [
        for (final s in spells)
          SpellRow(
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

/// The "Conjuros" tab: the spell slots (and pact slots), the class rows (the
/// wizard's spellbook), "Preparar conjuros" for classes that prepare, and
/// every spell by level with a "Preparados" / "Todos" filter. Each spell can
/// be cast (as a ritual when it is one) from its row.
class SpellListSection extends ConsumerStatefulWidget {
  const SpellListSection({
    super.key,
    required this.character,
    required this.canEdit,
    this.isDm = false,
    this.spellRows = const [],
  });

  final CharacterDetail character;
  final bool canEdit;
  final bool isDm;

  /// Rows of the classes for this tab, above the list.
  final List<Widget> spellRows;

  @override
  ConsumerState<SpellListSection> createState() => _SpellListSectionState();
}

class _SpellListSectionState extends ConsumerState<SpellListSection> {
  bool _all = false;

  @override
  Widget build(BuildContext context) {
    final c = widget.character;
    final theme = Theme.of(context);
    final all = _spellsOf(ref, c, castableOnly: false) ?? const <_CombatSpell>[];
    final castable = {
      for (final s in all)
        if (isCastable(c, s.spell, s.spell.level ?? s.detail.level)) s.detail.index,
    };
    final hasUnprepared = castable.length < all.length;
    final shown = _all || !hasUnprepared
        ? all
        : [
            for (final s in all)
              if (castable.contains(s.detail.index)) s,
          ];
    final prepares = c.sheet.spellcasting.any((s) => s.preparedMax != null);
    final surgeTable = wildMagicSurgeTable(ref, c);
    final levels = {for (final s in shown) s.detail.level}.toList()..sort();
    return Column(
      key: const Key('spell-list'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SpellSlotsSection(character: c, canEdit: widget.canEdit),
        ...widget.spellRows,
        if (hasUnprepared || (prepares && widget.canEdit))
          Padding(
            padding: const EdgeInsets.only(top: 12),
            child: Wrap(
              spacing: 8,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                if (hasUnprepared)
                  SegmentedButton<bool>(
                    key: const Key('spells-filter'),
                    showSelectedIcon: false,
                    segments: const [
                      ButtonSegment(
                        value: false,
                        label: Text('Preparados', key: Key('spells-filter-prepared')),
                      ),
                      ButtonSegment(
                        value: true,
                        label: Text('Todos', key: Key('spells-filter-all')),
                      ),
                    ],
                    selected: {_all},
                    onSelectionChanged: (value) => setState(() => _all = value.first),
                  ),
                if (prepares && widget.canEdit)
                  OutlinedButton.icon(
                    key: const Key('spells-prepare'),
                    onPressed: () => context.push(Dnd5eRoutes.prepareSpells(c.id)),
                    icon: const AppIcon(AppIcons.spellbook, size: 18),
                    label: const Text('Preparar conjuros'),
                  ),
              ],
            ),
          ),
        for (final level in levels)
          CombatGroup(
            key: Key('spell-level-group-$level'),
            title: level == 0 ? 'Trucos' : 'Nivel $level',
            children: [
              for (final s in shown)
                if (s.detail.level == level)
                  SpellRow(
                    key: Key('combat-spell-${s.detail.index}'),
                    character: c,
                    spell: s.spell,
                    detail: s.detail,
                    canEdit: widget.canEdit,
                    surgeTable: surgeTable,
                    castable: castable.contains(s.detail.index),
                    showRolls: false,
                  ),
            ],
          ),
        if (all.isEmpty && c.spells.isNotEmpty)
          Padding(
            padding: const EdgeInsets.all(16),
            child: Text('Cargando conjuros…', style: theme.textTheme.bodySmall),
          ),
      ],
    );
  }
}

/// A spell: its name with the casting time and what casting it costs ("Truco",
/// "Espacio de nivel 2", "1/descanso largo", "6 PG", "Ritual"), range,
/// duration, concentration and ritual, and "Lanzar" on the right. With
/// [showRolls] it also has the attack, the save DC, the damage or healing at
/// the chosen level (each with its breakdown), "Crítico" and the roll buttons.
///
/// Casting goes through the server, which decides the cost: a use of the
/// spell's own resource (a spell granted with uses per rest), the hit points
/// of a class that casts with them, a slot of the chosen level, or nothing
/// for a ritual. A concentration spell then becomes the one the character
/// concentrates on.
class SpellRow extends ConsumerStatefulWidget {
  const SpellRow({
    super.key,
    required this.character,
    required this.spell,
    required this.detail,
    required this.canEdit,
    this.surgeTable,
    this.castable = true,
    this.showRolls = true,
  });

  final CharacterDetail character;
  final CharacterSpell spell;
  final SpellDetail detail;
  final bool canEdit;

  /// Wild Magic Surge table of the character's subclass: spending a slot
  /// offers to roll for a surge.
  final RollTable? surgeTable;

  /// False for a spell that is not prepared: no "Lanzar" (but a ritual can
  /// still be cast as one).
  final bool castable;
  final bool showRolls;

  @override
  ConsumerState<SpellRow> createState() => _SpellRowState();
}

class _SpellRowState extends ConsumerState<SpellRow> {
  /// The dice of the damage are doubled (set by a natural 20 on the attack).
  bool _critical = false;

  /// Chosen slot level; null = the lowest one available.
  int? _castLevel;

  /// A slot was just spent with a Wild Magic Surge subclass: the surge prompt
  /// is shown until closed.
  bool _surgePending = false;

  SpellDetail get _detail => widget.detail;
  String get _index => _detail.index;

  /// The class of the spell casts with hit points instead of slots.
  bool get _withHitPoints {
    final casting = _casting;
    return casting != null &&
        casting.castsWithHitPoints &&
        casting.classIndex == widget.spell.classIndex;
  }

  /// The class of the spell has no slots and casts only rituals.
  bool get _ritualOnly {
    final casting = _casting;
    return casting != null && casting.ritualsOnly && casting.classIndex == widget.spell.classIndex;
  }

  /// The spell's own resource: a spell granted with uses per rest
  /// (`<classIndex>.<spellIndex>`, e.g. `race.speak-with-dead`). Casting it
  /// spends a use instead of a slot.
  CharacterResource? get _useResource {
    final key = '${widget.spell.classIndex}.$_index';
    for (final r in resourcesOf(widget.character)) {
      if (r.key == key && r.max > 0) return r;
    }
    return null;
  }

  List<int> get _levels => _ritualOnly || _useResource != null
      ? [_detail.level]
      : _withHitPoints
      ? hitPointCastLevels(_detail.level, _casting!)
      : castLevels(_detail.level, regularSlots(widget.character), pactSlotsOf(widget.character));

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

  /// Spell attack bonus: the cantrip one when a feature changes it.
  int get _attackBonus {
    final casting = _casting;
    if (casting == null) return 0;
    return _detail.level == 0
        ? casting.cantripAttackBonus ?? casting.attackBonus
        : casting.attackBonus;
  }

  Future<void> _rollAttack([AdvantageMode mode = AdvantageMode.normal]) async {
    final criticalRange = _casting?.criticalRange ?? 20;
    await rollAndShow(
      context,
      d20Expression(_attackBonus, mode: mode),
      label: 'Ataque: ${_detail.name}',
      kind: RollKind.attack,
      criticalRange: criticalRange,
      onResult: (result) {
        if (!mounted) return;
        setState(() => _critical = result.isCriticalFrom(criticalRange));
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

  /// What casting it costs now: "Truco", "1/descanso largo" (a use of its
  /// own resource), "Ritual", the hit points of the level, or the slot.
  String get _costLabel {
    if (_detail.level == 0) return 'Truco';
    if (_useResource case final use?) return '1/${use.recharge.label.toLowerCase()}';
    if (_ritualOnly) return 'Ritual';
    if (_withHitPoints) return '${hitPointCastCost(_casting!, _level)} PG';
    return 'Espacio de nivel $_level';
  }

  /// Whether the spell can be cast as a ritual without spending anything.
  bool get _isRitual =>
      _detail.level > 0 &&
      (_detail.ritual || _detail.ritualForClasses.contains(widget.spell.classIndex));

  /// Casts the spell through the server, which decides what it costs
  /// ([SpellCastResult.cost]); [asRitual] asks for the ritual (nothing spent).
  /// A concentration spell then becomes the one the character concentrates on.
  Future<void> _cast({bool asRitual = false}) async {
    final use = _useResource;
    final level = _level;
    final ritual = asRitual || (_ritualOnly && use == null);
    if (!ritual && use == null && !_withHitPoints) {
      final regular = regularSlots(widget.character).where((s) => s.level == level).firstOrNull;
      final pact = pactSlotsOf(widget.character);
      final hasSlot =
          (regular != null && regular.used < regular.max) ||
          (pact != null && pact.level == level && pact.used < pact.max);
      if (!hasSlot) {
        showCombatMessage(context, 'No quedan espacios de nivel $level.');
        return;
      }
    }
    final name = _detail.name;
    final concentration = _detail.concentration;
    if (concentration &&
        !await confirmReplaceConcentration(context, ref, widget.character, _index)) {
      return;
    }
    if (!mounted) return;
    final controller = ref.read(dnd5eCharacterControllerProvider(widget.character.id).notifier);
    SpellCastResult? result;
    final cast = await runCombat(context, () async {
      result = await controller.castSpell(
        _index,
        classIndex: widget.spell.classIndex,
        level: ritual ? null : level,
        ritual: ritual,
      );
    });
    final done = result;
    if (!cast || done == null || !mounted) return;
    if (concentration) {
      final concentrated = await runCombat(context, () => controller.setConcentration(_index));
      if (!concentrated || !mounted) return;
    }
    final slotLevel = done.slotLevel ?? level;
    final message = switch (done.cost) {
      SpellCastCost.use => 'Uso de $name gastado (quedan ${done.usesLeft ?? 0})',
      SpellCastCost.hitPoints =>
        '$name lanzado a nivel $level: pierdes ${done.hitPointsLost ?? 0} PG',
      SpellCastCost.ritual => '$name lanzado como ritual (10 minutos más)',
      SpellCastCost.cantrip => '$name lanzado',
      SpellCastCost.slot || SpellCastCost.pactSlot =>
        concentration
            ? 'Espacio de nivel $slotLevel gastado'
            : 'Espacio de nivel $slotLevel gastado: $name',
    };
    showCombatMessage(context, concentration ? '$message. Concentrándote en $name.' : '$message.');
    final slot = done.cost == SpellCastCost.slot || done.cost == SpellCastCost.pactSlot;
    if (slot && level >= 1 && widget.surgeTable != null) setState(() => _surgePending = true);
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
    // Cantrips use their own DC and attack when a feature changes them.
    final cantrip = d.level == 0;
    final cantripDc = cantrip ? casting?.cantripSaveDc : null;
    final saveDc = cantripDc ?? casting?.saveDc ?? 0;
    final dcKey = 'spellSaveDc.${casting?.classIndex}${cantripDc != null ? '.cantrip' : ''}';
    final attackKey =
        'spellAttackBonus.${casting?.classIndex}${cantrip && casting?.cantripAttackBonus != null ? '.cantrip' : ''}';
    return [
      if (d.attackType != null && casting != null)
        _fact(
          _attackLabel,
          StatValue(
            statKey: 'spell.$_index.attack',
            title: 'Ataque de conjuro: ${d.name}',
            text: formatModifier(_attackBonus),
            breakdown: breakdowns[attackKey],
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
                  text: 'CD $saveDc (${abilityName(d.dcAbility!)})',
                  totalText: 'CD $saveDc',
                  breakdown: breakdowns[dcKey],
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

  /// "Lanzar" (or why not), or the concentration of a cantrip; null for a
  /// plain cantrip.
  Widget? _castButton() {
    final d = _detail;
    if (d.level == 0) {
      if (!d.concentration) return null;
      final concentrating = widget.character.concentratingOnSpellIndex == _index;
      return OutlinedButton(
        key: Key('spell-concentrate-$_index'),
        onPressed: widget.canEdit && !concentrating ? _concentrate : null,
        child: Text(concentrating ? 'Concentrado' : 'Concentrarse'),
      );
    }
    if (!widget.castable) return null;
    final use = _useResource;
    if (use != null && use.used >= use.max) {
      return OutlinedButton(
        key: Key('spell-spend-$_index'),
        onPressed: null,
        child: Text(
          'Sin usos hasta el ${use.recharge.label.toLowerCase()}',
          maxLines: 2,
          textAlign: TextAlign.center,
        ),
      );
    }
    return FilledButton.tonal(
      key: Key('spell-spend-$_index'),
      onPressed: widget.canEdit ? _cast : null,
      child: Text(
        _ritualOnly && use == null ? 'Lanzar como ritual' : 'Lanzar',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final d = _detail;
    final casting = _casting;
    final damage = _damage;
    final heal = _heal;
    final levels = _levels;
    final rolls = widget.showRolls;
    final facts = rolls ? _facts(casting) : const <Widget>[];
    final timing = ActionKind.fromCastingTime(d.castingTime);
    final about = [
      if (!widget.castable) 'No preparado',
      ?d.range,
      ?d.duration,
      if (d.concentration) 'Concentración',
      if (d.ritual || d.ritualForClasses.contains(widget.spell.classIndex)) 'Ritual',
    ].where((e) => e.isNotEmpty).join(' · ');
    final small = theme.textTheme.bodySmall;
    // A ritual can be cast as one (nothing spent) besides "Lanzar".
    final offerRitual =
        !rolls &&
        _isRitual &&
        !_ritualOnly &&
        _useResource == null &&
        (widget.castable || widget.spell.classIndex == 'wizard');

    return CombatActionRow(
      icon: null,
      title: d.name,
      actionKind: d.castingTime == null ? null : timing.kind,
      actionLabel: d.castingTime == null ? null : timing.label,
      cost: _costLabel,
      costKey: Key('spell-cost-$_index'),
      badge: SpellCategoryIcon(widget.spell.category ?? d.category),
      info: DetailInfoButton(
        key: Key('detail-spell-$_index'),
        onPressed: () => openSpellDetail(context, _index),
      ),
      action: _castButton(),
      details: [
        if (about.isNotEmpty) Text(about, key: Key('spell-about-$_index'), style: small),
        if (facts.isNotEmpty)
          Wrap(
            key: Key('spell-facts-$_index'),
            spacing: 12,
            runSpacing: 4,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: facts,
          )
        else if (rolls && d.description.isNotEmpty)
          ExpandableText([d.description.first], key: Key('spell-summary-$_index')),
        if (d.level > 0 && _withHitPoints && levels.isNotEmpty && widget.castable)
          Text(
            'Sin espacios: lanzarlo a nivel $_level cuesta '
            '${hitPointCastCost(_casting!, _level)} PG.',
            key: Key('spell-hp-cost-$_index'),
            style: small,
          ),
        if (d.level > 0 && levels.length > 1 && widget.castable)
          Row(
            children: [
              Text('Lanzar a nivel', style: theme.textTheme.bodyMedium),
              const SizedBox(width: 12),
              DropdownButton<int>(
                key: Key('spell-level-$_index'),
                value: _level,
                isDense: true,
                items: [for (final l in levels) DropdownMenuItem(value: l, child: Text('$l'))],
                onChanged: (value) => setState(() => _castLevel = value),
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
      actions: [
        if (rolls && d.attackType != null)
          FilledButton(
            key: Key('spell-attack-$_index'),
            onPressed: _rollAttack,
            onLongPress: _chooseMode,
            child: const Text('Tirar ataque'),
          ),
        if (rolls && damage != null)
          FilterChip(
            key: Key('spell-crit-$_index'),
            label: const Text('Crítico'),
            selected: _critical,
            onSelected: (value) => setState(() => _critical = value),
          ),
        if (rolls && damage != null)
          FilledButton.tonal(
            key: Key('spell-damage-$_index'),
            onPressed: _rollDamage,
            child: const Text('Tirar daño'),
          ),
        if (rolls && heal != null)
          FilledButton.tonal(
            key: Key('spell-heal-$_index'),
            onPressed: _rollHeal,
            child: const Text('Tirar curación'),
          ),
        if (offerRitual)
          OutlinedButton(
            key: Key('spell-ritual-$_index'),
            onPressed: widget.canEdit ? () => _cast(asRitual: true) : null,
            child: const Text('Como ritual'),
          ),
      ],
    );
  }
}
