import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/ui/source_chip.dart';

import '../../ui/action_type.dart';
import '../../ui/spell_category.dart';
import '../data/catalog_controllers.dart';
import '../data/models.dart' hide Page;
import '../domain/catalog_format.dart';
import 'detail_widgets.dart';

/// Every field of one spell.
class SpellDetailPage extends ConsumerWidget {
  const SpellDetailPage({super.key, required this.index});

  final String index;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final spell = ref.watch(spellDetailProvider(index));
    // Names of the classes that cast it as a ritual (only when it says so).
    final classNames = spell.value?.ritualForClasses.isNotEmpty ?? false
        ? {
            for (final c in ref.watch(classesProvider(null)).value ?? const <ClassSummary>[])
              c.index: c.name,
          }
        : const <String, String>{};
    // Names of its tags (content packs), only when it has some.
    final tagNames = spell.value?.tags.isNotEmpty ?? false
        ? ref.watch(referenceProvider(ReferenceKinds.spellTags)).value
        : null;
    return Scaffold(
      appBar: AppBar(title: Text(spell.value?.name ?? 'Hechizo')),
      body: CatalogAsyncBody<SpellDetail>(
        value: spell,
        onRetry: () => ref.invalidate(spellDetailProvider(index)),
        builder: (s) => DetailList(
          children: [
            Align(alignment: Alignment.centerLeft, child: SourceChip(s.source)),
            Row(
              children: [
                SpellCategoryIcon(s.category),
                if (SpellCategory.fromApi(s.category) != null) const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    [spellLevelLabel(s.level), ?s.school].join(' · '),
                    key: const Key('spell-subtitle'),
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                ),
                if (s.castingTime != null) ActionTypeChip.castingTime(s.castingTime),
              ],
            ),
            if (ActionKind.fromCastingTime(s.castingTime).note case final note?)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  'Reacción: $note',
                  key: const Key('spell-reaction-note'),
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
            const SizedBox(height: 8),
            FactRow('Tiempo de lanzamiento', s.castingTime),
            FactRow('Alcance', s.range),
            FactRow('Componentes', _components(s)),
            FactRow('Duración', s.duration),
            if (s.concentration) const FactRow('Concentración', 'Sí'),
            if (s.ritual) const FactRow('Ritual', 'Sí'),
            if (!s.ritual && s.ritualForClasses.isNotEmpty)
              FactRow(
                'Ritual',
                'Solo para ${s.ritualForClasses.map((c) => classNames[c] ?? titleFromIndex(c)).join(', ')}',
              ),
            FactRow('Clases', s.classes.join(', ')),
            if (s.tags.isNotEmpty)
              FactRow(
                'Etiquetas',
                s.tags.map((t) => tagNames?[t]?.name ?? titleFromIndex(t)).join(', '),
                key: const Key('spell-tags'),
              ),
            for (final e in s.expandedBy)
              Padding(
                key: Key('spell-expanded-${e.subclassIndex}'),
                padding: const EdgeInsets.symmetric(vertical: 3),
                child: Text(
                  '${e.label} (${titleFromIndex(e.classIndex)})',
                  style: Theme.of(context).textTheme.bodyMedium
                      ?.copyWith(fontWeight: FontWeight.w600),
                ),
              ),
            FactRow('Tipo de ataque', s.attackType),
            FactRow('Salvación', s.dcAbility == null ? null : abilityLabel(s.dcAbility!)),
            FactRow('Daño', s.damage),
            const SectionTitle('Descripción'),
            s.description.isEmpty ? const Text('Sin descripción.') : Paragraphs(s.description),
            if (s.higherLevel.isNotEmpty) ...[
              const SectionTitle('A niveles superiores'),
              Paragraphs(s.higherLevel),
            ],
          ],
        ),
      ),
    );
  }

  static String? _components(SpellDetail s) {
    if (s.components.isEmpty) return null;
    final text = s.components.join(', ');
    final material = s.material;
    return material == null || material.isEmpty ? text : '$text ($material)';
  }
}
