import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/theme/components.dart';
import '../../../core/theme/textures.dart';
import '../../../core/theme/typography.dart';
import '../../../core/ui/stat_value.dart';
import '../../../core/ui/spell_category.dart';
import '../../catalog/data/catalog_controllers.dart';
import '../../catalog/data/models.dart' show ClassDetail, Feature, ItemModifier, RaceDetail, Trait;
import '../../catalog/domain/catalog_format.dart';
import '../../catalog/domain/item_modifier_format.dart';
import '../../catalog/ui/detail_widgets.dart';
import '../../dice/domain/dice_expression.dart';
import '../../dice/ui/dice_sheet.dart';
import '../data/characters_controller.dart';
import '../data/models.dart';
import '../domain/character_format.dart';
import '../domain/class_theme.dart';
import 'level_up/character_choices_section.dart';

/// Icon with the note of an override, shown on long press. Renders nothing for
/// values that were not overridden.
class OverrideMark extends StatelessWidget {
  const OverrideMark({super.key, required this.character, required this.field});

  final CharacterDetail character;

  /// Override field: `armorClass`, `ability.str`, `skill.stealth`, ...
  final String field;

  @override
  Widget build(BuildContext context) {
    final override = character.overrideOf(field);
    if (!character.sheet.isOverridden(field) && override == null) return const SizedBox.shrink();
    final note = override?.note;
    return Tooltip(
      message: (note == null || note.isEmpty) ? 'Valor modificado manualmente' : note,
      triggerMode: TooltipTriggerMode.longPress,
      showDuration: const Duration(seconds: 4),
      child: Padding(
        padding: const EdgeInsets.all(4),
        child: Icon(
          Icons.edit_note,
          key: Key('override-$field'),
          size: 18,
          color: Theme.of(context).colorScheme.primary,
        ),
      ),
    );
  }
}

class _TabList extends StatelessWidget {
  const _TabList({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return ListView(
      key: const Key('sheet-tab-list'),
      padding: EdgeInsets.fromLTRB(16, 8, 16, 88 + MediaQuery.paddingOf(context).bottom),
      children: children,
    );
  }
}

// ---------------------------------------------------------------------------
// Resumen
// ---------------------------------------------------------------------------

class SummaryTab extends StatelessWidget {
  const SummaryTab({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final c = character;
    final sheet = c.sheet;
    return _TabList(
      children: [
        if (c.invalidChoices.isNotEmpty) _InvalidChoicesNotice(character: c),
        const SectionTitle('Características'),
        _EqualGrid(
          key: const Key('abilities-grid'),
          columnsWide: 3,
          children: [for (final key in abilityKeys) _AbilityCard(character: c, abilityKey: key)],
        ),
        const SectionTitle('Combate'),
        _EqualGrid(
          key: const Key('combat-grid'),
          columnsWide: 6,
          children: [
            _StatTile(
              statKey: 'armor-class',
              breakdownKey: 'armorClass',
              label: 'CA',
              value: '${sheet.armorClass}',
              character: c,
              field: 'armorClass',
            ),
            _StatTile(
              statKey: 'initiative',
              breakdownKey: 'initiative',
              label: 'Iniciativa',
              value: formatModifier(sheet.initiative),
              character: c,
              field: 'initiative',
            ),
            _StatTile(
              statKey: 'speed',
              breakdownKey: 'speed',
              totalText: '${sheet.speed} pies',
              label: 'Velocidad',
              value: '${sheet.speed} pies',
              character: c,
              field: 'speed',
            ),
            _StatTile(
              statKey: 'hp',
              breakdownKey: 'hitPointsMax',
              label: 'PG máx',
              value: '${sheet.hitPointsMax}',
              character: c,
              field: 'hitPointsMax',
            ),
            _StatTile(
              statKey: 'passive-perception',
              breakdownKey: 'passivePerception',
              label: 'Percepción pasiva',
              value: '${sheet.passivePerception}',
              character: c,
              field: 'passivePerception',
            ),
            _StatTile(
              statKey: 'proficiency',
              breakdownKey: 'proficiencyBonus',
              label: 'Competencia',
              value: formatModifier(sheet.proficiencyBonus),
              character: c,
              field: 'proficiencyBonus',
            ),
          ],
        ),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _StatTile(
              statKey: 'hp-current',
              label: 'PG actuales',
              value: '${c.hitPointsCurrent} / ${sheet.hitPointsMax}',
              character: c,
            ),
            _StatTile(
              statKey: 'temp-hp',
              label: 'PG temporales',
              value: '${c.temporaryHitPoints}',
              character: c,
            ),
            _StatTile(
              statKey: 'inspiration',
              label: 'Inspiración',
              value: c.inspiration ? 'Sí' : 'No',
              character: c,
            ),
          ],
        ),
        const SectionTitle('Salvaciones'),
        for (final key in abilityKeys) _SavingThrowRow(character: c, abilityKey: key),
        if (sheet.resistances.isNotEmpty) ...[
          const SectionTitle('Resistencias'),
          Wrap(
            key: const Key('resistances'),
            spacing: 8,
            runSpacing: 4,
            children: [
              for (final r in sheet.resistances)
                Tooltip(
                  message: r.label,
                  child: Chip(
                    key: Key('resistance-${r.damageType}'),
                    label: Text(
                      r.label.isEmpty
                          ? damageTypeLabel(r.damageType)
                          : '${damageTypeLabel(r.damageType)} · ${r.label}',
                    ),
                  ),
                ),
            ],
          ),
        ],
        if (sheet.breathWeapon != null) _BreathWeaponCard(character: c),
        if (sheet.itemEffects.isNotEmpty) ...[
          const SectionTitle('Efectos de objetos'),
          for (var i = 0; i < sheet.itemEffects.length; i++)
            Padding(
              padding: const EdgeInsets.only(bottom: 4),
              child: Text(
                '${sheet.itemEffects[i].itemName}: ${describeItemModifier(ItemModifier(kind: sheet.itemEffects[i].kind, target: sheet.itemEffects[i].target, value: sheet.itemEffects[i].value))}',
                key: Key('item-effect-$i'),
              ),
            ),
        ],
      ],
    );
  }
}

/// Warning of the sheet while some option or feat no longer meets its
/// prerequisites: it lists them and opens the forced replacement page.
class _InvalidChoicesNotice extends StatelessWidget {
  const _InvalidChoicesNotice({required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    return Card(
      key: const Key('invalid-choices-notice'),
      color: scheme.errorContainer,
      margin: const EdgeInsets.only(top: 4, bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.warning_amber_rounded, color: scheme.onErrorContainer),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Hay elecciones que ya no cumplen sus requisitos',
                    style: theme.textTheme.titleSmall?.copyWith(color: scheme.onErrorContainer),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 4),
            for (final invalid in character.invalidChoices)
              Text(
                '${invalid.item.name}: ${invalid.reason}',
                key: Key('invalid-choice-${invalid.item.index}'),
                style: theme.textTheme.bodySmall?.copyWith(color: scheme.onErrorContainer),
              ),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton(
                key: const Key('invalid-choices-open'),
                onPressed: () => context.push(AppRoutes.characterInvalidChoices(character.id)),
                child: const Text('Sustituir'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Breath weapon of a draconic ancestry: damage, area, saving throw and a DC
/// that explains itself with its breakdown.
class _BreathWeaponCard extends StatelessWidget {
  const _BreathWeaponCard({required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final weapon = character.sheet.breathWeapon!;
    return Column(
      key: const Key('breath-weapon'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Arma de aliento'),
        Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(weapon.name, style: theme.textTheme.titleMedium),
                  Text(
                    [
                      if (weapon.dice.isNotEmpty)
                        '${weapon.dice} de daño ${damageTypeLabel(weapon.damageType)}',
                      if (weapon.area.isNotEmpty) weapon.area,
                      if (weapon.saveAbility.isNotEmpty)
                        'salvación de ${abilityLabel(weapon.saveAbility)}',
                    ].join(' · '),
                    key: const Key('breath-weapon-text'),
                    style: theme.textTheme.bodySmall,
                  ),
                ],
              ),
            ),
            StatValue(
              statKey: 'breath-weapon-dc',
              text: 'CD ${weapon.dc}',
              textKey: const Key('breath-weapon-dc'),
              title: 'CD de ${weapon.name}',
              totalText: '${weapon.dc}',
              breakdown: character.sheet.breakdown('breathWeapon.dc'),
              style: theme.textTheme.titleLarge,
            ),
          ],
        ),
      ],
    );
  }
}

/// Fixed grid of equal tiles: 3 columns, or [columnsWide] from 600 px wide.
class _EqualGrid extends StatelessWidget {
  const _EqualGrid({super.key, required this.columnsWide, required this.children});

  final int columnsWide;
  final List<Widget> children;

  static const _wideBreakpoint = 600.0;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final wide = constraints.maxWidth >= _wideBreakpoint;
        return GridView.count(
          crossAxisCount: wide ? columnsWide : 3,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 8,
          crossAxisSpacing: 8,
          childAspectRatio: wide && columnsWide > 3 ? 1.3 : 1.15,
          children: children,
        );
      },
    );
  }
}

class _AbilityCard extends StatelessWidget {
  const _AbilityCard({required this.character, required this.abilityKey});

  final CharacterDetail character;
  final String abilityKey;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ability = character.sheet.ability(abilityKey);
    return RuneCard(
      key: Key('ability-$abilityKey'),
      margin: EdgeInsets.zero,
      padding: const EdgeInsets.all(8),
      onTap: () => rollAndShow(
        context,
        d20Expression(ability.modifier),
        label: 'Prueba de ${abilityLabel(abilityKey)}',
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(abilityLabel(abilityKey), style: theme.textTheme.labelMedium),
          Text(formatModifier(ability.modifier), style: _numeric(theme.textTheme.headlineSmall)),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              StatValue(
                statKey: 'ability.$abilityKey',
                title: abilityLabel(abilityKey),
                text: '${ability.score}',
                breakdown: character.sheet.breakdown('ability.$abilityKey'),
                style: _numeric(theme.textTheme.bodyMedium),
              ),
              OverrideMark(character: character, field: 'ability.$abilityKey'),
            ],
          ),
        ],
      ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({
    required this.statKey,
    required this.label,
    required this.value,
    required this.character,
    this.field,
    this.breakdownKey,
    this.totalText,
  });

  final String statKey;
  final String label;
  final String value;
  final CharacterDetail character;
  final String? field;

  /// Key in `sheet.breakdowns`; null for values without breakdown.
  final String? breakdownKey;

  /// Total line of the breakdown sheet when it differs from [value].
  final String? totalText;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return StoneCard(
      key: Key('tile-$statKey'),
      margin: EdgeInsets.zero,
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 8),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            label,
            style: theme.textTheme.labelMedium,
            textAlign: TextAlign.center,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
          FittedBox(
            fit: BoxFit.scaleDown,
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                StatValue(
                  statKey: breakdownKey ?? statKey,
                  title: label,
                  text: value,
                  totalText: totalText,
                  breakdown: breakdownKey == null ? null : character.sheet.breakdown(breakdownKey!),
                  style: _numeric(theme.textTheme.titleLarge),
                ),
                if (field != null) OverrideMark(character: character, field: field!),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SavingThrowRow extends StatelessWidget {
  const _SavingThrowRow({required this.character, required this.abilityKey});

  final CharacterDetail character;
  final String abilityKey;

  @override
  Widget build(BuildContext context) {
    final save = character.sheet.savingThrows[abilityKey];
    final proficient = save?.proficient ?? false;
    return ListTile(
      key: Key('save-$abilityKey'),
      dense: true,
      contentPadding: EdgeInsets.zero,
      onTap: () => rollAndShow(
        context,
        d20Expression(save?.value ?? 0),
        label: 'Salvación de ${abilityLabel(abilityKey)}',
      ),
      leading: Icon(
        proficient ? Icons.circle : Icons.radio_button_unchecked,
        key: Key(proficient ? 'save-proficient-$abilityKey' : 'save-plain-$abilityKey'),
        size: 18,
        semanticLabel: proficient ? 'Competente' : 'Sin competencia',
      ),
      title: Text(abilityLabel(abilityKey)),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.casino_outlined, size: 16),
          const SizedBox(width: 8),
          StatValue(
            statKey: 'save.$abilityKey',
            title: 'Salvación de ${abilityLabel(abilityKey)}',
            text: formatModifier(save?.value ?? 0),
            breakdown: character.sheet.breakdown('save.$abilityKey'),
            style: _numeric(Theme.of(context).textTheme.bodyLarge),
          ),
          OverrideMark(character: character, field: 'save.$abilityKey'),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Habilidades
// ---------------------------------------------------------------------------

class SkillsTab extends StatelessWidget {
  const SkillsTab({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final others = [
      for (final t in const [
        ProficiencyType.armor,
        ProficiencyType.weapon,
        ProficiencyType.tool,
        ProficiencyType.language,
      ])
        (
          type: t,
          keys: [
            for (final p in character.proficiencies)
              if (p.type == t) p.key,
          ],
        ),
    ];
    return _TabList(
      children: [
        const SectionTitle('Habilidades'),
        for (final skill in character.sheet.skills)
          ListTile(
            key: Key('skill-${skill.index}'),
            dense: true,
            contentPadding: EdgeInsets.zero,
            onTap: () => rollAndShow(
              context,
              d20Expression(skill.value),
              label: skillLabel(skill.index, skill.name),
            ),
            leading: Icon(
              skill.expertise
                  ? Icons.stars
                  : skill.proficient
                  ? Icons.circle
                  : Icons.radio_button_unchecked,
              size: 18,
              semanticLabel: skill.expertise
                  ? 'Pericia'
                  : skill.proficient
                  ? 'Competente'
                  : 'Sin competencia',
            ),
            title: Text(skillLabel(skill.index, skill.name)),
            subtitle: Text(
              [
                abilityAbbreviation(skill.ability),
                if (skill.expertise) 'Pericia' else if (skill.proficient) 'Competente',
              ].join(' · '),
            ),
            trailing: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.casino_outlined, size: 16),
                const SizedBox(width: 8),
                StatValue(
                  statKey: 'skill.${skill.index}',
                  title: skillLabel(skill.index, skill.name),
                  text: formatModifier(skill.value),
                  breakdown: character.sheet.breakdown('skill.${skill.index}'),
                  style: _numeric(Theme.of(context).textTheme.bodyLarge),
                ),
                OverrideMark(character: character, field: 'skill.${skill.index}'),
              ],
            ),
          ),
        for (final group in others)
          if (group.keys.isNotEmpty) ...[
            SectionTitle(switch (group.type) {
              ProficiencyType.armor => 'Armaduras',
              ProficiencyType.weapon => 'Armas',
              ProficiencyType.tool => 'Herramientas',
              _ => 'Idiomas',
            }),
            Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [for (final k in group.keys) Chip(label: Text(k))],
            ),
          ],
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Rasgos
// ---------------------------------------------------------------------------

class TraitsTab extends ConsumerWidget {
  const TraitsTab({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    if (c.classes.isEmpty && c.raceIndex == null) {
      return const _TabList(
        children: [Text('Elige una raza y una clase en el editor para ver sus rasgos.')],
      );
    }
    return _TabList(
      children: [
        if (c.choices.isNotEmpty) CharacterChoicesSection(character: c),
        for (final cls in c.classes) _ClassFeatures(characterClass: cls),
        if (c.raceIndex != null) _RaceTraits(raceIndex: c.raceIndex!, subraceIndex: c.subraceIndex),
        _FeatsSection(character: c),
      ],
    );
  }
}

/// "Dotes": every feat the character took, with the catalog text, where it was
/// taken and the ability it raised, next to the class and race features.
class _FeatsSection extends StatelessWidget {
  const _FeatsSection({required this.character});

  final CharacterDetail character;

  String _subtitle(CharacterFeat feat) {
    final where = feat.level == 0
        ? 'Raza o trasfondo'
        : character.classes.length > 1
        ? '${_className(feat.classIndex)} · nivel ${feat.level}'
        : 'Nivel ${feat.level}';
    final ability = feat.ability == null ? '' : ' · +1 ${abilityAbbreviation(feat.ability!)}';
    return '$where$ability';
  }

  String _className(String? classIndex) =>
      classThemes[classIndex]?.labelEs ??
      character.classes
          .where((c) => c.classIndex == classIndex)
          .map((c) => c.className)
          .firstOrNull ??
      classIndex ??
      '';

  @override
  Widget build(BuildContext context) {
    final feats = character.feats;
    return Column(
      key: const Key('sheet-feats'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Dotes'),
        if (feats.isEmpty) const Text('Sin dotes.'),
        for (final feat in feats)
          ExpandableEntry(
            key: Key('sheet-feat-${feat.index}'),
            title: feat.name,
            subtitle: _subtitle(feat),
            description: [
              if (feat.prerequisitesText != null) 'Requisito: ${feat.prerequisitesText}',
              ...feat.description,
            ],
            emptyText: 'Esta dote ya no está en el catálogo.',
          ),
      ],
    );
  }
}

class _AsyncSection<T> extends StatelessWidget {
  const _AsyncSection({
    required this.title,
    required this.value,
    required this.onRetry,
    required this.builder,
  });

  final String title;
  final AsyncValue<T> value;
  final VoidCallback onRetry;
  final List<Widget> Function(T data) builder;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionTitle(title),
        ...value.when(
          loading: () => [const Center(child: CircularProgressIndicator())],
          error: (error, _) => [
            Text(describeApiError(error)),
            TextButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Reintentar'),
            ),
          ],
          data: builder,
        ),
      ],
    );
  }
}

class _ClassFeatures extends ConsumerWidget {
  const _ClassFeatures({required this.characterClass});

  final CharacterClass characterClass;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cls = characterClass;
    final detail = ref.watch(classDetailProvider(cls.classIndex));
    return _AsyncSection<ClassDetail>(
      title: 'Rasgos de ${cls.className} (nivel ${cls.level})',
      value: detail,
      onRetry: () => ref.invalidate(classDetailProvider(cls.classIndex)),
      builder: (data) {
        final features = <Feature>[
          for (final l in data.levels)
            if (l.level <= cls.level) ...l.features,
        ];
        final subclass = data.subclasses.where((s) => s.index == cls.subclassIndex).firstOrNull;
        final subclassFeatures = [
          if (subclass != null)
            for (final f in subclass.features)
              if (f.level <= cls.level) f,
        ];
        if (features.isEmpty && subclassFeatures.isEmpty) {
          return [const Text('Sin rasgos disponibles.')];
        }
        return [
          for (final f in features)
            ExpandableEntry(
              title: f.name,
              subtitle: 'Nivel ${f.level}',
              description: f.description,
            ),
          for (final f in subclassFeatures)
            ExpandableEntry(
              title: f.name,
              subtitle: '${subclass!.name} · nivel ${f.level}',
              description: f.description,
            ),
        ];
      },
    );
  }
}

class _RaceTraits extends ConsumerWidget {
  const _RaceTraits({required this.raceIndex, this.subraceIndex});

  final String raceIndex;
  final String? subraceIndex;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(raceDetailProvider(raceIndex));
    return _AsyncSection<RaceDetail>(
      title: 'Rasgos raciales',
      value: detail,
      onRetry: () => ref.invalidate(raceDetailProvider(raceIndex)),
      builder: (data) {
        final subrace = data.subraces.where((s) => s.index == subraceIndex).firstOrNull;
        final traits = <Trait>[...data.traits, ...?subrace?.traits];
        if (traits.isEmpty) return [const Text('Sin rasgos raciales.')];
        return [for (final t in traits) ExpandableEntry(title: t.name, description: t.description)];
      },
    );
  }
}

// ---------------------------------------------------------------------------
// Hechizos
// ---------------------------------------------------------------------------

class SpellsTab extends ConsumerWidget {
  const SpellsTab({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final info = ref
        .watch(spellInfoProvider(spellInfoKey(c.spells.map((s) => s.spellIndex))))
        .value;
    final theme = Theme.of(context);

    // Level by spell: the DTO value, else the catalog one, else unknown (-1).
    int levelOf(CharacterSpell s) => s.level ?? info?[s.spellIndex]?.level ?? -1;
    String nameOf(CharacterSpell s) =>
        s.name ?? info?[s.spellIndex]?.name ?? titleFromSpellIndex(s.spellIndex);

    final byLevel = <int, List<CharacterSpell>>{};
    for (final s in c.spells) {
      byLevel.putIfAbsent(levelOf(s), () => []).add(s);
    }
    final levels = byLevel.keys.toList()..sort((a, b) => a == -1 ? 1 : (b == -1 ? -1 : a - b));
    final slots = c.spellSlots.where((s) => s.max > 0).toList()
      ..sort((a, b) => a.level.compareTo(b.level));
    final concentrating = c.concentratingOnSpellIndex;

    return _TabList(
      children: [
        for (final sc in c.sheet.spellcasting) _SpellcastingRow(character: c, spellcasting: sc),
        if (concentrating != null)
          FactRow(
            'Concentración',
            info?[concentrating]?.name ?? titleFromSpellIndex(concentrating),
          ),
        if (slots.isNotEmpty) ...[
          const SectionTitle('Espacios de conjuro'),
          for (final slot in slots)
            ListTile(
              key: Key('slot-${slot.level}'),
              dense: true,
              contentPadding: EdgeInsets.zero,
              title: Text(slot.level == 0 ? 'Magia de pacto' : 'Nivel ${slot.level}'),
              trailing: Text('Usados ${slot.used} / ${slot.max}'),
            ),
        ],
        if (c.spells.isEmpty) ...[
          const SectionTitle('Hechizos'),
          Text('Este personaje no tiene hechizos.', style: theme.textTheme.bodyMedium),
        ],
        for (final level in levels) ...[
          SectionTitle(level < 0 ? 'Otros' : spellLevelLabel(level)),
          for (final s in byLevel[level]!)
            ListTile(
              key: Key('spell-${s.spellIndex}'),
              dense: true,
              contentPadding: EdgeInsets.zero,
              leading: SpellCategoryIcon(s.category ?? info?[s.spellIndex]?.category),
              title: Text(nameOf(s)),
              subtitle: Text(titleFromSpellIndex(s.classIndex)),
              trailing: s.alwaysPrepared
                  ? const Chip(
                      label: Text('Siempre preparado'),
                      visualDensity: VisualDensity.compact,
                    )
                  : s.isPrepared
                  ? const Chip(label: Text('Preparado'), visualDensity: VisualDensity.compact)
                  : null,
            ),
        ],
      ],
    );
  }
}

/// "Clase: CD 14 · Ataque +6 · Preparados máx. 5", with the DC and the attack
/// bonus tappable to see where they come from.
class _SpellcastingRow extends StatelessWidget {
  const _SpellcastingRow({required this.character, required this.spellcasting});

  final CharacterDetail character;
  final Spellcasting spellcasting;

  @override
  Widget build(BuildContext context) {
    final sc = spellcasting;
    final className = titleFromSpellIndex(sc.classIndex);
    final sheet = character.sheet;
    final style = Theme.of(context).textTheme.bodyMedium;
    final bold = style?.copyWith(fontWeight: FontWeight.w600);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Wrap(
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text('$className: ', style: bold),
          Text('CD ', style: style),
          StatValue(
            statKey: 'spellSaveDc.${sc.classIndex}',
            title: 'CD de conjuros ($className)',
            text: '${sc.saveDc}',
            breakdown: sheet.breakdown('spellSaveDc.${sc.classIndex}'),
            style: style,
          ),
          Text(' · Ataque ', style: style),
          StatValue(
            statKey: 'spellAttackBonus.${sc.classIndex}',
            title: 'Ataque de conjuros ($className)',
            text: formatModifier(sc.attackBonus),
            breakdown: sheet.breakdown('spellAttackBonus.${sc.classIndex}'),
            style: style,
          ),
          if (sc.preparedMax != null) Text(' · Preparados máx. ${sc.preparedMax}', style: style),
        ],
      ),
    );
  }
}

/// "sleight-of-hand" -> "Sleight of hand" (display name from a catalog index).
String titleFromSpellIndex(String index) {
  final text = index.replaceAll('-', ' ');
  return text.isEmpty ? text : '${text[0].toUpperCase()}${text.substring(1)}';
}

// ---------------------------------------------------------------------------
// Notas
// ---------------------------------------------------------------------------

class NotesTab extends StatelessWidget {
  const NotesTab({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final c = character;
    final background =
        c.backgroundName ??
        (c.backgroundIndex == null ? null : titleFromSpellIndex(c.backgroundIndex!));
    return _TabList(
      children: [
        FactRow('Trasfondo', background),
        FactRow('Alineamiento', c.alignment == null ? null : alignmentLabel(c.alignment!)),
        const SectionTitle('Personalidad'),
        if (!c.hasPersonality)
          const Text('Sin personalidad.', key: Key('sheet-personality-empty'))
        else
          Column(
            key: const Key('sheet-personality'),
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _PersonalityRow('Rasgos de personalidad', c.personalityTraits),
              _PersonalityRow('Ideal', c.ideals),
              _PersonalityRow('Vínculo', c.bonds),
              _PersonalityRow('Defecto', c.flaws),
              _PersonalityRow('Detalle del trasfondo', c.backgroundDetail),
            ],
          ),
        const SectionTitle('Notas'),
        c.notes.trim().isEmpty ? const Text('Sin notas.') : SelectableText(c.notes),
        const SectionTitle('Historia del personaje'),
        c.backstory.trim().isEmpty ? const Text('Sin historia.') : SelectableText(c.backstory),
      ],
    );
  }
}

/// A labelled personality text; nothing when [text] is empty.
class _PersonalityRow extends StatelessWidget {
  const _PersonalityRow(this.label, this.text);

  final String label;
  final String text;

  @override
  Widget build(BuildContext context) {
    if (text.trim().isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: theme.textTheme.labelMedium),
          SelectableText(text.trim()),
        ],
      ),
    );
  }
}

/// [style] with the tabular figures of [AppTypography.numeric].
TextStyle? _numeric(TextStyle? style) =>
    style == null ? AppTypography.numeric : style.merge(AppTypography.numeric);
