import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/catalog_controllers.dart';
import '../data/models.dart' hide Page;
import '../domain/catalog_format.dart';
import 'detail_widgets.dart';

/// A class with its level table, subclasses and expandable features.
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
            if (c.subclasses.isNotEmpty) ...[
              SectionTitle(
                c.subclassFlavor == null ? 'Subclases' : 'Subclases (${c.subclassFlavor})',
              ),
              for (final sub in c.subclasses)
                ExpandableEntry(
                  title: sub.name,
                  subtitle: sub.flavor,
                  description: sub.description,
                ),
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

class _LevelTable extends StatelessWidget {
  const _LevelTable({required this.detail});

  final ClassDetail detail;

  @override
  Widget build(BuildContext context) {
    final levels = detail.levels;
    final showCantrips = levels.any((l) => l.cantripsKnown != null);
    final showSpellsKnown = levels.any((l) => l.spellsKnown != null);
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
