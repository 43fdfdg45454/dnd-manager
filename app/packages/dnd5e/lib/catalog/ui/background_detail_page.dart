import 'package:flutter/material.dart';

import 'package:opentrpg_core/core/ui/source_chip.dart';
import 'package:opentrpg_core/core/ui/text_format.dart';

import '../../characters/domain/character_format.dart' show skillLabel;
import '../data/models.dart' hide Page;
import 'detail_widgets.dart';

/// Spanish name of each part of the decisions a background asks for.
const _choiceLabels = <String, String>{
  'abilityBonuses': 'Bonos de característica',
  'skills': 'Habilidades',
  'languages': 'Idiomas',
  'tools': 'Herramientas',
  'cantrip': 'Truco',
  'feats': 'Dote',
  'traitOptions': 'Rasgos a elegir',
};

String _skillOf(String name) =>
    skillLabel(name.trim().toLowerCase().replaceAll(RegExp(r'\s+'), '-'), name);

/// A background from the data of the catalog list (no request): feature,
/// skills, equipment, decisions and personality tables.
class BackgroundDetailPage extends StatelessWidget {
  const BackgroundDetailPage({super.key, required this.background});

  final Background background;

  @override
  Widget build(BuildContext context) {
    final b = background;
    final personality = b.personality;
    final languages = b.choices.languages;
    final choices = [
      for (final kind in b.choices.kinds)
        kind == 'languages' && languages != null && languages.choose > 0
            ? '${_choiceLabels[kind]} (${languages.choose})'
            : _choiceLabels[kind] ?? kind,
    ];
    return Scaffold(
      appBar: AppBar(title: Text(b.name)),
      body: DetailList(
        key: Key('background-detail-${b.index}'),
        children: [
          if (b.source != null) Align(alignment: Alignment.centerLeft, child: SourceChip(b.source)),
          if (b.skillProficiencies.isNotEmpty)
            FactRow('Habilidades', b.skillProficiencies.map(_skillOf).join(', ')),
          if (b.startingEquipmentText != null)
            FactRow('Equipo', cleanText(b.startingEquipmentText!)),
          if (choices.isNotEmpty) FactRow('Elecciones', choices.join(', ')),
          if ((b.featureName ?? '').isNotEmpty || b.featureDescription.isNotEmpty) ...[
            const SectionTitle('Rasgo'),
            ExpandableEntry(
              title: (b.featureName ?? '').isEmpty ? 'Rasgo del trasfondo' : b.featureName!,
              description: b.featureDescription,
            ),
          ],
          for (final table in b.optionalTables)
            if (table.entries.isNotEmpty) ...[SectionTitle(table.name), _Numbered(table.entries)],
          if (personality != null) ...[
            if (personality.traits.isNotEmpty) ...[
              const SectionTitle('Rasgos de personalidad'),
              _Numbered(personality.traits),
            ],
            if (personality.ideals.isNotEmpty) ...[
              const SectionTitle('Ideales'),
              _Numbered([
                for (final i in personality.ideals)
                  i.alignment == null ? i.text : '${i.text} (${i.alignment})',
              ]),
            ],
            if (personality.bonds.isNotEmpty) ...[
              const SectionTitle('Vínculos'),
              _Numbered(personality.bonds),
            ],
            if (personality.flaws.isNotEmpty) ...[
              const SectionTitle('Defectos'),
              _Numbered(personality.flaws),
            ],
          ],
        ],
      ),
    );
  }
}

/// A die table: "1. …", "2. …".
class _Numbered extends StatelessWidget {
  const _Numbered(this.entries);

  final List<String> entries;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      for (var i = 0; i < entries.length; i++)
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 2),
          child: Text('${i + 1}. ${entries[i]}'),
        ),
    ],
  );
}
