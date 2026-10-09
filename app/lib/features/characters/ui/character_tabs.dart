import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/theme/textures.dart';
import '../../../core/theme/typography.dart';
import '../../../core/ui/action_type.dart';
import '../../../core/ui/stat_tiles.dart';
import '../../../core/ui/stat_value.dart';
import '../../../core/ui/spell_category.dart';
import '../../catalog/data/catalog_controllers.dart';
import '../../catalog/data/models.dart' show ClassDetail, Feature, ItemModifier, RaceDetail, Trait;
import '../../catalog/domain/catalog_format.dart';
import '../../catalog/domain/item_modifier_format.dart';
import '../../catalog/ui/catalog_detail_links.dart';
import '../../catalog/ui/detail_widgets.dart';
import '../../dice/domain/dice_expression.dart';
import '../../dice/ui/dice_sheet.dart';
import '../data/characters_controller.dart';
import '../data/models.dart';
import '../domain/character_format.dart';
import '../domain/class_theme.dart';
import 'combat/companion_section.dart';
import 'level_up/character_choices_section.dart';
import 'skill_rolls.dart' show rollSkill, rollSkillWithMode;

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
  const SummaryTab({super.key, required this.character, this.canEdit = false, this.isDm = false});

  final CharacterDetail character;

  /// The viewer may write (owner or DM): enables "Elegir compañero".
  final bool canEdit;
  final bool isDm;

  @override
  Widget build(BuildContext context) {
    final c = character;
    final sheet = c.sheet;
    return _TabList(
      children: [
        if (c.invalidChoices.isNotEmpty) _InvalidChoicesNotice(character: c),
        const SectionTitle('Características'),
        StatTileGrid(
          key: const Key('abilities-grid'),
          columnsWide: 3,
          children: [for (final key in abilityKeys) _AbilityCard(character: c, abilityKey: key)],
        ),
        const SectionTitle('Combate'),
        StatTileGrid(
          key: const Key('combat-grid'),
          columnsWide: 6,
          children: [
            StatTile(
              statKey: 'armor-class',
              breakdownKey: 'armorClass',
              label: 'CA',
              value: '${sheet.armorClass}',
              breakdown: sheet.breakdown('armorClass'),
              mark: OverrideMark(character: c, field: 'armorClass'),
            ),
            StatTile(
              statKey: 'initiative',
              breakdownKey: 'initiative',
              label: 'Iniciativa',
              value: formatModifier(sheet.initiative),
              breakdown: sheet.breakdown('initiative'),
              mark: OverrideMark(character: c, field: 'initiative'),
            ),
            StatTile(
              statKey: 'speed',
              breakdownKey: 'speed',
              totalText: '${sheet.speed} pies',
              label: 'Velocidad',
              value: '${sheet.speed} pies',
              breakdown: sheet.breakdown('speed'),
              mark: OverrideMark(character: c, field: 'speed'),
            ),
            StatTile(
              statKey: 'hp',
              breakdownKey: 'hitPointsMax',
              label: 'PG máx',
              value: '${sheet.hitPointsMax}',
              breakdown: sheet.breakdown('hitPointsMax'),
              mark: OverrideMark(character: c, field: 'hitPointsMax'),
            ),
            StatTile(
              statKey: 'passive-perception',
              breakdownKey: 'passivePerception',
              label: 'Percepción pasiva',
              value: '${sheet.passivePerception}',
              breakdown: sheet.breakdown('passivePerception'),
              mark: OverrideMark(character: c, field: 'passivePerception'),
            ),
            StatTile(
              statKey: 'proficiency',
              breakdownKey: 'proficiencyBonus',
              label: 'Competencia',
              value: formatModifier(sheet.proficiencyBonus),
              breakdown: sheet.breakdown('proficiencyBonus'),
              mark: OverrideMark(character: c, field: 'proficiencyBonus'),
            ),
          ],
        ),
        const SizedBox(height: 8),
        StatTileGrid(
          key: const Key('state-grid'),
          columnsWide: 3,
          children: [
            StatTile(
              statKey: 'hp-current',
              label: 'PG actuales',
              value: '${c.hitPointsCurrent} / ${sheet.hitPointsMax}',
            ),
            StatTile(statKey: 'temp-hp', label: 'PG temporales', value: '${c.temporaryHitPoints}'),
            StatTile(
              statKey: 'inspiration',
              label: 'Inspiración',
              value: c.inspiration ? 'Sí' : 'No',
            ),
          ],
        ),
        if (c.companion != null || c.companionFeature != null) ...[
          const SectionTitle('Compañero animal'),
          CompanionSection(character: c, canEdit: canEdit, isDm: isDm),
        ],
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

  /// Abilities with skills, in the order of the SRD sheet (Constitution has none).
  static const _skillAbilities = ['str', 'dex', 'int', 'wis', 'cha'];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final skillGroups = [
      for (final ability in _skillAbilities)
        (
          ability: ability,
          skills: [
            for (final skill in character.sheet.skills)
              if (abilityKeyOf(skill.ability) == ability) skill,
          ]..sort((a, b) => skillLabel(a.index, a.name).compareTo(skillLabel(b.index, b.name))),
        ),
    ];
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
        Text(
          'Toca para tirar; mantén pulsado para ventaja o desventaja.',
          key: const Key('skills-hint'),
          style: theme.textTheme.bodySmall,
        ),
        for (final group in skillGroups)
          if (group.skills.isNotEmpty) ...[
            Padding(
              padding: const EdgeInsets.only(top: 12, bottom: 2),
              child: Text(
                '${abilityLabel(group.ability)} '
                '${formatModifier(character.sheet.ability(group.ability).modifier)}',
                key: Key('skills-group-${group.ability}'),
                style: theme.textTheme.labelLarge,
              ),
            ),
            for (final skill in group.skills) _SkillRow(character: character, skill: skill),
          ],
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

/// A skill of [SkillsTab]: tap rolls, long press asks for advantage or
/// disadvantage first; the value opens its breakdown.
class _SkillRow extends StatelessWidget {
  const _SkillRow({required this.character, required this.skill});

  final CharacterDetail character;
  final SheetSkill skill;

  @override
  Widget build(BuildContext context) {
    final status = skill.expertise
        ? 'Pericia'
        : skill.proficient
        ? 'Competente'
        : null;
    return ListTile(
      key: Key('skill-${skill.index}'),
      dense: true,
      contentPadding: EdgeInsets.zero,
      onTap: () => rollSkill(context, skill),
      onLongPress: () => rollSkillWithMode(context, skill),
      leading: Icon(
        skill.expertise
            ? Icons.stars
            : skill.proficient
            ? Icons.circle
            : Icons.radio_button_unchecked,
        size: 18,
        semanticLabel: status ?? 'Sin competencia',
      ),
      title: Text(skillLabel(skill.index, skill.name)),
      subtitle: status == null ? null : Text(status),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          StatValue(
            statKey: 'skill.${skill.index}',
            title: skillLabel(skill.index, skill.name),
            text: formatModifier(skill.value),
            breakdown: character.sheet.breakdown('skill.${skill.index}'),
            style: _numeric(Theme.of(context).textTheme.titleMedium),
          ),
          OverrideMark(character: character, field: 'skill.${skill.index}'),
        ],
      ),
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

    // Racial spells (no class: `race`) go to their own "Raza" section.
    final raceCasting = c.sheet.spellcasting
        .where((s) => s.classIndex == raceSpellClassIndex)
        .firstOrNull;
    final raceSpells = c.spells.where((s) => s.classIndex == raceSpellClassIndex).toList()
      ..sort((a, b) {
        final byLevel = levelOf(a).compareTo(levelOf(b));
        return byLevel != 0 ? byLevel : nameOf(a).compareTo(nameOf(b));
      });

    final byLevel = <int, List<CharacterSpell>>{};
    for (final s in c.spells.where((s) => s.classIndex != raceSpellClassIndex)) {
      byLevel.putIfAbsent(levelOf(s), () => []).add(s);
    }
    final levels = byLevel.keys.toList()..sort((a, b) => a == -1 ? 1 : (b == -1 ? -1 : a - b));
    final slots = c.spellSlots.where((s) => s.max > 0).toList()
      ..sort((a, b) => a.level.compareTo(b.level));
    final concentrating = c.concentratingOnSpellIndex;

    return _TabList(
      children: [
        for (final sc in c.sheet.spellcasting)
          if (sc.classIndex != raceSpellClassIndex)
            _SpellcastingRow(character: c, spellcasting: sc),
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
        if (raceCasting != null || raceSpells.isNotEmpty)
          Column(
            key: const Key('spells-race'),
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SectionTitle('Raza'),
              if (raceCasting != null) _SpellcastingRow(character: c, spellcasting: raceCasting),
              for (final s in raceSpells)
                ListTile(
                  key: Key('spell-race-${s.spellIndex}'),
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  leading: SpellCategoryIcon(s.category ?? info?[s.spellIndex]?.category),
                  title: Text(nameOf(s)),
                  subtitle: CastingTimeSubtitle(
                    castingTime: info?[s.spellIndex]?.castingTime,
                    text: _racialSpellDetail(c, s, levelOf(s)),
                  ),
                  onTap: () => openSpellDetail(context, s.spellIndex),
                  trailing: const Chip(
                    label: Text('Siempre preparado'),
                    visualDensity: VisualDensity.compact,
                  ),
                ),
            ],
          ),
        for (final level in levels) ...[
          SectionTitle(level < 0 ? 'Otros' : spellLevelLabel(level)),
          for (final s in byLevel[level]!)
            ListTile(
              key: Key('spell-${s.spellIndex}'),
              dense: true,
              contentPadding: EdgeInsets.zero,
              leading: SpellCategoryIcon(s.category ?? info?[s.spellIndex]?.category),
              title: Text(nameOf(s)),
              subtitle: CastingTimeSubtitle(
                castingTime: info?[s.spellIndex]?.castingTime,
                text: titleFromSpellIndex(s.classIndex),
              ),
              onTap: () => openSpellDetail(context, s.spellIndex),
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

/// `classIndex` of the spells granted by the race or subrace (and of the
/// "Raza" spellcasting of the sheet).
const raceSpellClassIndex = 'race';

/// "Truco", or "Nivel 3 · 1 de 1 usos por descanso largo" when the race gives
/// the spell a number of casts per long rest (resource `race.<spell>`).
String _racialSpellDetail(CharacterDetail character, CharacterSpell spell, int level) {
  final label = level < 0 ? 'Conjuro' : spellLevelLabel(level);
  final uses = character.resources
      .where((r) => r.key == '$raceSpellClassIndex.${spell.spellIndex}')
      .firstOrNull;
  if (uses == null) return label;
  final left = (uses.max - uses.used).clamp(0, uses.max);
  return '$label · $left de ${uses.max} ${uses.max == 1 ? 'uso' : 'usos'} por descanso largo';
}

/// Label of a spellcasting entry: "Raza" for the racial one, else the class.
String spellcastingLabel(String classIndex) =>
    classIndex == raceSpellClassIndex ? 'Raza' : titleFromSpellIndex(classIndex);

/// "Clase: CD 14 · Ataque +6 · Preparados máx. 5" (or "Conocidos máx. 3 ·
/// Trucos máx. 2" for a class that casts through its subclass), with the DC and the attack
/// bonus tappable to see where they come from.
class _SpellcastingRow extends StatelessWidget {
  const _SpellcastingRow({required this.character, required this.spellcasting});

  final CharacterDetail character;
  final Spellcasting spellcasting;

  @override
  Widget build(BuildContext context) {
    final sc = spellcasting;
    final className = spellcastingLabel(sc.classIndex);
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
          if (sc.spellsKnownMax != null)
            Text(
              ' · Conocidos máx. ${sc.spellsKnownMax}',
              key: Key('sheet-spells-known-max-${sc.classIndex}'),
              style: style,
            ),
          if (sc.cantripsKnownMax != null)
            Text(
              ' · Trucos máx. ${sc.cantripsKnownMax}',
              key: Key('sheet-cantrips-known-max-${sc.classIndex}'),
              style: style,
            ),
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
