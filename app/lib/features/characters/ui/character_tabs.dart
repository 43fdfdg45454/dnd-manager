import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_error.dart';
import '../../catalog/data/catalog_controllers.dart';
import '../../catalog/data/models.dart' show ClassDetail, Feature, RaceDetail, Trait;
import '../../catalog/domain/catalog_format.dart';
import '../../catalog/ui/detail_widgets.dart';
import '../../dice/domain/dice_expression.dart';
import '../../dice/ui/dice_sheet.dart';
import '../data/characters_controller.dart';
import '../data/models.dart';
import '../domain/character_format.dart';

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
        const SectionTitle('Características'),
        GridView.count(
          crossAxisCount: 3,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 8,
          crossAxisSpacing: 8,
          childAspectRatio: 1.15,
          children: [for (final key in abilityKeys) _AbilityCard(character: c, abilityKey: key)],
        ),
        const SectionTitle('Combate'),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _StatTile(
              statKey: 'proficiency',
              label: 'Bonificador de competencia',
              value: formatModifier(sheet.proficiencyBonus),
              character: c,
              field: 'proficiencyBonus',
            ),
            _StatTile(
              statKey: 'armor-class',
              label: 'CA',
              value: '${sheet.armorClass}',
              character: c,
              field: 'armorClass',
            ),
            _StatTile(
              statKey: 'initiative',
              label: 'Iniciativa',
              value: formatModifier(sheet.initiative),
              character: c,
              field: 'initiative',
            ),
            _StatTile(
              statKey: 'speed',
              label: 'Velocidad',
              value: '${sheet.speed} pies',
              character: c,
              field: 'speed',
            ),
            _StatTile(
              statKey: 'hp',
              label: 'PG',
              value: '${c.hitPointsCurrent} / ${sheet.hitPointsMax}',
              character: c,
              field: 'hitPointsMax',
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
            _StatTile(
              statKey: 'passive-perception',
              label: 'Percepción pasiva',
              value: '${sheet.passivePerception}',
              character: c,
              field: 'passivePerception',
            ),
          ],
        ),
        const SectionTitle('Salvaciones'),
        for (final key in abilityKeys) _SavingThrowRow(character: c, abilityKey: key),
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
    return Card(
      key: Key('ability-$abilityKey'),
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => rollAndShow(
          context,
          d20Expression(ability.modifier),
          label: 'Prueba de ${abilityLabel(abilityKey)}',
        ),
        child: Padding(
          padding: const EdgeInsets.all(8),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(abilityLabel(abilityKey), style: theme.textTheme.labelMedium),
              Text(formatModifier(ability.modifier), style: theme.textTheme.headlineSmall),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('${ability.score}', style: theme.textTheme.bodyMedium),
                  OverrideMark(character: character, field: 'ability.$abilityKey'),
                ],
              ),
            ],
          ),
        ),
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
  });

  final String statKey;
  final String label;
  final String value;
  final CharacterDetail character;
  final String? field;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      key: Key('stat-$statKey'),
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(label, style: theme.textTheme.labelMedium),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(value, style: theme.textTheme.titleLarge),
                if (field != null) OverrideMark(character: character, field: field!),
              ],
            ),
          ],
        ),
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
          Text(formatModifier(save?.value ?? 0)),
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
                Text(formatModifier(skill.value)),
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
        for (final cls in c.classes) _ClassFeatures(characterClass: cls),
        if (c.raceIndex != null) _RaceTraits(raceIndex: c.raceIndex!, subraceIndex: c.subraceIndex),
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
        for (final sc in c.sheet.spellcasting)
          FactRow(
            titleFromSpellIndex(sc.classIndex),
            'CD ${sc.saveDc} · Ataque ${formatModifier(sc.attackBonus)}'
            '${sc.preparedMax == null ? '' : ' · Preparados máx. ${sc.preparedMax}'}',
          ),
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
        const SectionTitle('Notas'),
        c.notes.trim().isEmpty ? const Text('Sin notas.') : SelectableText(c.notes),
        const SectionTitle('Historia del personaje'),
        c.backstory.trim().isEmpty ? const Text('Sin historia.') : SelectableText(c.backstory),
      ],
    );
  }
}
