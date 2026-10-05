import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/catalog_controllers.dart';
import '../data/models.dart' hide Page;
import '../domain/catalog_format.dart';
import 'detail_widgets.dart';

String _bonuses(List<AbilityBonus> bonuses) => bonuses
    .map((b) => '${abilityLabel(b.ability)} ${b.bonus >= 0 ? '+' : ''}${b.bonus}')
    .join(', ');

/// A race with its bonuses, traits and subraces.
class RaceDetailPage extends ConsumerWidget {
  const RaceDetailPage({super.key, required this.index});

  final String index;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(raceDetailProvider(index));
    return Scaffold(
      appBar: AppBar(title: Text(detail.value?.name ?? 'Raza')),
      body: CatalogAsyncBody<RaceDetail>(
        value: detail,
        onRetry: () => ref.invalidate(raceDetailProvider(index)),
        builder: (r) => DetailList(
          children: [
            FactRow('Velocidad', r.speed == null ? null : '${r.speed} pies'),
            FactRow('Tamaño', r.size),
            FactRow('Bonos de característica', _bonuses(r.abilityBonuses)),
            FactRow('Idiomas', r.languages.join(', ')),
            FactRow('Edad', r.age),
            FactRow('Alineamiento', r.alignment),
            FactRow('Descripción del tamaño', r.sizeDescription),
            if (r.traits.isNotEmpty) ...[
              const SectionTitle('Rasgos'),
              for (final t in r.traits) ExpandableEntry(title: t.name, description: t.description),
            ],
            if (r.subraces.isNotEmpty) ...[
              const SectionTitle('Subrazas'),
              for (final sub in r.subraces) _SubraceCard(subrace: sub),
            ],
          ],
        ),
      ),
    );
  }
}

class _SubraceCard extends StatelessWidget {
  const _SubraceCard({required this.subrace});

  final Subrace subrace;

  @override
  Widget build(BuildContext context) {
    return Card(
      key: Key('subrace-${subrace.index}'),
      margin: const EdgeInsets.symmetric(vertical: 6),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(subrace.name, style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 6),
            FactRow('Bonos de característica', _bonuses(subrace.abilityBonuses)),
            if (subrace.description.isNotEmpty) Paragraphs(subrace.description),
            for (final t in subrace.traits)
              ExpandableEntry(title: t.name, description: t.description),
          ],
        ),
      ),
    );
  }
}
