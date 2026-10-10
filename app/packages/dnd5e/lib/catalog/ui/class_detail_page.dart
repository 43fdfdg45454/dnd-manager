import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:opentrpg_core/core/ui/source_chip.dart';

import '../data/catalog_controllers.dart';
import '../data/models.dart' hide Page;
import '../domain/catalog_format.dart';
import 'detail_widgets.dart';

/// A class with its level table, subclasses and expandable features. The
/// classes of content packs also bring a description, their spellcasting,
/// multiclassing and resources, shown with the same widgets.
class ClassDetailPage extends ConsumerWidget {
  const ClassDetailPage({super.key, required this.index});

  final String index;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(classDetailProvider(index));
    return Scaffold(
      appBar: AppBar(title: Text(detail.value?.name ?? 'Clase')),
      body: CatalogAsyncBody<ClassDetail>(
        value: detail,
        onRetry: () => ref.invalidate(classDetailProvider(index)),
        builder: (c) => DetailList(
          children: [
            Align(alignment: Alignment.centerLeft, child: SourceChip(c.source)),
            if (c.description.isNotEmpty) ...[Paragraphs(c.description), const SizedBox(height: 8)],
            FactRow('Dado de golpe', c.hitDie == null ? null : 'd${c.hitDie}'),
            FactRow('Salvaciones', c.savingThrows.map(abilityLabel).join(', ')),
            FactRow(
              'Habilidad de conjuro',
              c.spellcastingAbility == null
                  ? (c.isSpellcaster ? null : 'No lanza conjuros')
                  : abilityLabel(c.spellcastingAbility!),
            ),
            FactRow('Competencias', c.proficiencies.join(', ')),
            FactRow('Equipo inicial', c.startingEquipmentText),
            if (c.subclassLevel > 0)
              FactRow(c.subclassFlavor ?? 'Subclase', 'Se elige a nivel ${c.subclassLevel}'),
            if (c.spellcasting case final spellcasting?) ...[
              const SectionTitle('Lanzamiento de conjuros'),
              _SpellcastingFacts(spellcasting: spellcasting),
            ],
            if (c.resources.isNotEmpty) ...[
              const SectionTitle('Recursos'),
              for (final resource in c.resources) _ResourceEntry(resource: resource),
            ],
            if (c.multiclassing case final multiclassing?) ...[
              const SectionTitle('Multiclase'),
              _MulticlassingFacts(multiclassing: multiclassing),
            ],
            if (c.subclasses.isNotEmpty) ...[
              SectionTitle(
                c.subclassFlavor == null ? 'Subclases' : 'Subclases (${c.subclassFlavor})',
              ),
              for (final sub in c.subclasses) _SubclassEntry(subclass: sub),
            ],
            if (c.levels.isNotEmpty) ...[
              const SectionTitle('Tabla de niveles'),
              _LevelTable(detail: c),
            ],
            if (c.levels.any((l) => l.features.isNotEmpty)) ...[
              const SectionTitle('Rasgos'),
              for (final level in c.levels)
                for (final feature in level.features)
                  ExpandableEntry(
                    key: Key('feature-${level.level}-${feature.index}'),
                    title: feature.name,
                    subtitle: 'Nivel ${level.level}',
                    description: feature.description,
                  ),
            ],
          ],
        ),
      ),
    );
  }
}

class _SpellcastingFacts extends StatelessWidget {
  const _SpellcastingFacts({required this.spellcasting});

  final ClassSpellcasting spellcasting;

  @override
  Widget build(BuildContext context) {
    return Column(
      key: const Key('class-spellcasting'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        FactRow('Progresión', spellProgressionLabel(spellcasting.progression)),
        FactRow('Conjuros', spellPreparationLabel(spellcasting.preparation)),
        FactRow('Rituales', spellcasting.ritual ? 'Sí' : 'No'),
        FactRow('Foco', spellcasting.focus == null ? null : titleFromIndex(spellcasting.focus!)),
      ],
    );
  }
}

class _MulticlassingFacts extends StatelessWidget {
  const _MulticlassingFacts({required this.multiclassing});

  final ClassMulticlassing multiclassing;

  @override
  Widget build(BuildContext context) {
    final m = multiclassing;
    return Column(
      key: const Key('class-multiclassing'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        FactRow(
          'Requisitos',
          m.prerequisites.isEmpty
              ? 'Ninguno'
              : [for (final e in m.prerequisites.entries) '${abilityLabel(e.key)} ${e.value}']
                    .join(', '),
        ),
        FactRow(
          'Competencias',
          [...m.armor, ...m.weapons, ...m.tools].map(titleFromIndex).join(', '),
        ),
        if (m.skills > 0)
          FactRow('Habilidades', '${m.skills} ${m.skills == 1 ? 'habilidad' : 'habilidades'}'),
      ],
    );
  }
}

/// A resource of a class: name, recharge and maximum (a formula, or the table
/// column when it changes by level).
class _ResourceEntry extends StatelessWidget {
  const _ResourceEntry({required this.resource});

  final ClassResource resource;

  @override
  Widget build(BuildContext context) {
    final byTable = resource.maxByLevel != null || resource.max.startsWith('classSpecific:');
    return ListTile(
      key: Key('class-resource-${resource.key}'),
      contentPadding: EdgeInsets.zero,
      dense: true,
      leading: const Icon(Icons.bolt_outlined),
      title: Text(resource.name),
      subtitle: Text(
        [
          byTable
              ? 'Máximo según la tabla de niveles'
              : 'Máximo: ${resourceMaxLabel(resource.max)}',
          'Se recupera: ${rechargeLabel(resource.recharge)}',
        ].join(' · '),
      ),
    );
  }
}

/// The maximum of [resource] at [level] when it comes from the level table
/// (`maxByLevel`, or `classSpecific:<key>` of the level), else null.
int? _resourceAt(ClassResource resource, ClassLevel level) {
  final byLevel = resource.maxByLevel;
  if (byLevel != null) {
    // The table may list only the levels where it changes: the last reached.
    final reached = byLevel.keys.where((k) => k <= level.level);
    return reached.isEmpty ? null : byLevel[reached.reduce((a, b) => a > b ? a : b)];
  }
  const prefix = 'classSpecific:';
  if (resource.max.startsWith(prefix)) {
    final value = level.classSpecific[resource.max.substring(prefix.length)];
    return value is num ? value.toInt() : int.tryParse('$value');
  }
  return null;
}

/// A subclass: its description plus its features grouped by level.
class _SubclassEntry extends StatelessWidget {
  const _SubclassEntry({required this.subclass});

  final Subclass subclass;

  @override
  Widget build(BuildContext context) {
    final byLevel = <int, List<Feature>>{};
    for (final feature in subclass.features) {
      byLevel.putIfAbsent(feature.level, () => []).add(feature);
    }
    final textTheme = Theme.of(context).textTheme;
    return ExpansionTile(
      key: Key('subclass-${subclass.index}'),
      tilePadding: EdgeInsets.zero,
      childrenPadding: const EdgeInsets.only(bottom: 8),
      expandedCrossAxisAlignment: CrossAxisAlignment.start,
      title: NameWithSource(subclass.name, subclass.source),
      subtitle: subclass.flavor == null ? null : Text(subclass.flavor!),
      children: [
        if (subclass.description.isEmpty && byLevel.isEmpty)
          const Text('Sin descripción.')
        else
          Paragraphs(subclass.description),
        for (final entry in byLevel.entries) ...[
          Padding(
            padding: const EdgeInsets.only(top: 12, bottom: 4),
            child: Text(
              entry.key > 0 ? 'Nivel ${entry.key}' : 'Rasgos',
              style: textTheme.titleSmall,
            ),
          ),
          for (final feature in entry.value)
            ExpandableEntry(
              key: Key('subclass-feature-${subclass.index}-${feature.index}'),
              title: feature.name,
              description: feature.description,
            ),
        ],
      ],
    );
  }
}

class _LevelTable extends StatelessWidget {
  const _LevelTable({required this.detail});

  final ClassDetail detail;

  @override
  Widget build(BuildContext context) {
    final levels = detail.levels;
    final showCantrips = levels.any((l) => l.cantripsKnown != null);
    final showSpellsKnown = levels.any((l) => l.spellsKnown != null);
    // Resources whose maximum comes from the table get a column of their own.
    final resourceColumns = [
      for (final r in detail.resources)
        if (levels.any((l) => _resourceAt(r, l) != null)) r,
    ];
    // Only the spell levels that ever get slots, to keep the table narrow.
    final slotColumns = detail.hasSpellSlots
        ? [
            for (var i = 0; i < 9; i++)
              if (levels.any((l) => l.spellSlots[i] > 0)) i,
          ]
        : const <int>[];

    return SingleChildScrollView(
      key: const Key('class-level-table'),
      scrollDirection: Axis.horizontal,
      child: DataTable(
        columnSpacing: 16,
        dataRowMinHeight: 40,
        dataRowMaxHeight: double.infinity,
        columns: [
          const DataColumn(label: Text('Nivel'), numeric: true),
          const DataColumn(label: Text('PB'), numeric: true),
          const DataColumn(label: Text('Rasgos')),
          if (showCantrips) const DataColumn(label: Text('Trucos'), numeric: true),
          if (showSpellsKnown) const DataColumn(label: Text('Conjuros'), numeric: true),
          for (final r in resourceColumns) DataColumn(label: Text(r.name), numeric: true),
          for (final i in slotColumns) DataColumn(label: Text('${i + 1}º'), numeric: true),
        ],
        rows: [
          for (final l in levels)
            DataRow(
              key: ValueKey('level-row-${l.level}'),
              cells: [
                DataCell(Text('${l.level}')),
                DataCell(Text(l.profBonus == null ? '—' : '+${l.profBonus}')),
                DataCell(
                  ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 220),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 6),
                      child: Text(
                        l.features.isEmpty ? '—' : l.features.map((f) => f.name).join(', '),
                      ),
                    ),
                  ),
                ),
                if (showCantrips) DataCell(Text(l.cantripsKnown?.toString() ?? '—')),
                if (showSpellsKnown) DataCell(Text(l.spellsKnown?.toString() ?? '—')),
                for (final r in resourceColumns)
                  DataCell(
                    Text(
                      _resourceAt(r, l)?.toString() ?? '—',
                      key: Key('resource-${l.level}-${r.key}'),
                    ),
                  ),
                for (final i in slotColumns)
                  DataCell(
                    Text(
                      l.spellSlots[i] == 0 ? '—' : '${l.spellSlots[i]}',
                      key: Key('slot-${l.level}-${i + 1}'),
                    ),
                  ),
              ],
            ),
        ],
      ),
    );
  }
}
