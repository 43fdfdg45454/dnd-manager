import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/ui/source_chip.dart';
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
    return Scaffold(
      appBar: AppBar(title: Text(spell.value?.name ?? 'Hechizo')),
      body: CatalogAsyncBody<SpellDetail>(
        value: spell,
        onRetry: () => ref.invalidate(spellDetailProvider(index)),
        builder: (s) => DetailList(
          children: [
            Align(alignment: Alignment.centerLeft, child: SourceChip(s.source)),
            Text(
              [spellLevelLabel(s.level), ?s.school].join(' · '),
              key: const Key('spell-subtitle'),
              style: Theme.of(context).textTheme.titleSmall,
            ),
            const SizedBox(height: 8),
            FactRow('Tiempo de lanzamiento', s.castingTime),
            FactRow('Alcance', s.range),
            FactRow('Componentes', _components(s)),
            FactRow('Duración', s.duration),
            if (s.concentration) const FactRow('Concentración', 'Sí'),
            if (s.ritual) const FactRow('Ritual', 'Sí'),
            FactRow('Clases', s.classes.join(', ')),
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
