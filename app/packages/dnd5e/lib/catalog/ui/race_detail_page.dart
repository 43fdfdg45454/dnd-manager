import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:opentrpg_core/core/ui/source_chip.dart';

import '../../characters/domain/height_weight.dart' show describeHeightWeightTable;
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
            Align(alignment: Alignment.centerLeft, child: SourceChip(r.source)),
            FactRow('Velocidad', r.speed == null ? null : '${r.speed} pies'),
            FactRow('Tamaño', r.size),
            FactRow('Bonos de característica', _bonuses(r.abilityBonuses)),
            FactRow('Idiomas', r.languages.join(', ')),
            FactRow('Edad', r.age),
            FactRow('Alineamiento', r.alignment),
            FactRow('Descripción del tamaño', r.sizeDescription),
            if (r.heightWeight != null)
              FactRow(
                'Altura y peso',
                describeHeightWeightTable(r.heightWeight!),
                key: const Key('race-height-weight'),
              ),
            if (r.traits.isNotEmpty) ...[
              const SectionTitle('Rasgos'),
              for (final t in r.traits) ExpandableEntry(title: t.name, description: t.description),
            ],
            if (r.subraces.isNotEmpty) ...[
              const SectionTitle('Subrazas'),
              for (final sub in r.subraces) SubraceCard(subrace: sub),
            ],
          ],
        ),
      ),
    );
  }
}

/// A subrace inside the detail of its race: name, bonuses, height and weight,
/// description and traits.
class SubraceCard extends StatelessWidget {
  const SubraceCard({super.key, required this.subrace});

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
            ...subraceDetails(subrace),
          ],
        ),
      ),
    );
  }
}

/// Bonuses, height and weight table, description and traits of [subrace].
List<Widget> subraceDetails(Subrace subrace) => [
  FactRow('Bonos de característica', _bonuses(subrace.abilityBonuses)),
  if (subrace.languages.isNotEmpty) FactRow('Idiomas', subrace.languages.join(', ')),
  if (subrace.heightWeight != null)
    FactRow(
      'Altura y peso',
      describeHeightWeightTable(subrace.heightWeight!),
      key: Key('subrace-height-weight-${subrace.index}'),
    ),
  if (subrace.description.isNotEmpty) Paragraphs(subrace.description),
  for (final t in subrace.traits) ExpandableEntry(title: t.name, description: t.description),
];

/// Detail of a subrace from the data its race already brought (no request):
/// what the subrace adds on top of [raceName].
class SubraceDetailPage extends StatelessWidget {
  const SubraceDetailPage({super.key, required this.subrace, this.raceName});

  final Subrace subrace;
  final String? raceName;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(subrace.name)),
      body: DetailList(
        key: Key('subrace-detail-${subrace.index}'),
        children: [
          if (raceName != null) FactRow('Subraza de', raceName),
          ...subraceDetails(subrace),
          if (subrace.traits.isEmpty && subrace.description.isEmpty)
            const Text('Sin rasgos propios.'),
        ],
      ),
    );
  }
}
