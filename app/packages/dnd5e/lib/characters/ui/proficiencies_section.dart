import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/ui/detail_widgets.dart' show SectionTitle;
import 'package:opentrpg_core/features/items/data/items_controllers.dart';
import 'package:opentrpg_core/features/items/data/models.dart' show CharacterItem;

import '../../catalog/data/catalog_controllers.dart';
import '../../catalog/data/models.dart' show ReferenceEntry, ReferenceKinds;
import '../../catalog/ui/catalog_detail_links.dart' show DetailInfoButton;
import '../../catalog/ui/reference_sheet.dart';
import '../domain/character_format.dart';
import '../models.dart';

/// One proficiency of the sheet, merged when several origins grant the same key.
typedef _Row = ({String key, bool expertise, List<ProficiencySource> sources});

/// Armor, weapon, tool and language proficiencies of the "Habilidades" tab:
/// one group per type, one row per proficiency with its readable name, where
/// it comes from (class, race, background or added by hand) and, for tools,
/// whether the character also carries that tool. Tools and languages open
/// their catalog entry with the info button.
class ProficienciesSection extends ConsumerWidget {
  const ProficienciesSection({super.key, required this.character});

  final CharacterDetail character;

  static const _types = [
    ProficiencyType.armor,
    ProficiencyType.weapon,
    ProficiencyType.tool,
    ProficiencyType.language,
  ];

  static String _title(ProficiencyType type) => switch (type) {
    ProficiencyType.armor => 'Armaduras',
    ProficiencyType.weapon => 'Armas',
    ProficiencyType.tool => 'Herramientas',
    _ => 'Idiomas',
  };

  List<_Row> _rows(ProficiencyType type) {
    final byKey = <String, _Row>{};
    for (final p in character.proficiencies) {
      if (p.type != type || p.key.trim().isEmpty) continue;
      final previous = byKey[p.key];
      byKey[p.key] = (
        key: p.key,
        expertise: p.expertise || (previous?.expertise ?? false),
        sources: [
          ...?previous?.sources,
          if (!(previous?.sources.contains(p.source) ?? false)) p.source,
        ],
      );
    }
    return byKey.values.toList();
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final groups = [for (final type in _types) (type: type, rows: _rows(type))]
        .where((g) => g.rows.isNotEmpty)
        .toList();
    if (groups.isEmpty) return const SizedBox.shrink();

    final hasTools = groups.any((g) => g.type == ProficiencyType.tool);
    final hasLanguages = groups.any((g) => g.type == ProficiencyType.language);
    final tools = hasTools
        ? _ReferenceIndex(ref.watch(referenceProvider(ReferenceKinds.tools)).value)
        : const _ReferenceIndex(null);
    final languages = hasLanguages
        ? _ReferenceIndex(ref.watch(referenceProvider(ReferenceKinds.languages)).value)
        : const _ReferenceIndex(null);
    final inventory = hasTools
        ? ref.watch(inventoryControllerProvider(character.id)).value?.items
        : null;

    String nameOf(ProficiencyType type, String key) => switch (type) {
      ProficiencyType.tool => tools.nameOf(key),
      ProficiencyType.language => languages.nameOf(key),
      _ => armorWeaponProficiencyName(key),
    };

    return Column(
      key: const Key('proficiencies'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (final group in groups) ...[
          SectionTitle(_title(group.type)),
          for (final (:row, :name) in [
            for (final r in group.rows) (row: r, name: nameOf(group.type, r.key)),
          ]..sort((a, b) => foldForSort(a.name).compareTo(foldForSort(b.name))))
            _ProficiencyRow(
              type: group.type,
              row: row,
              name: name,
              entry: switch (group.type) {
                ProficiencyType.tool => tools.entryOf(row.key),
                ProficiencyType.language => languages.entryOf(row.key),
                _ => null,
              },
              inInventory: group.type == ProficiencyType.tool && inventory != null
                  ? carriesTool(inventory, row.key, tools.entryOf(row.key)?.name)
                  : null,
            ),
        ],
      ],
    );
  }
}

/// Where a proficiency comes from, as the sheet says it.
String proficiencySourceLabel(ProficiencySource source) => switch (source) {
  ProficiencySource.manual => 'añadida a mano',
  _ => source.label.toLowerCase(),
};

/// Spanish name of an armor or weapon proficiency: the group label, or the
/// key as a title (a single weapon, "hand-crossbow" -> "Hand crossbow").
String armorWeaponProficiencyName(String key) {
  final normalized = catalogKeyOf(key);
  final label = armorWeaponProficiencyLabels[normalized];
  if (label != null) return label;
  final text = key.replaceAll('-', ' ').trim();
  return text.isEmpty ? key : '${text[0].toUpperCase()}${text.substring(1)}';
}

/// True when [items] hold the tool [key]: by the catalog index of the item's
/// template, or else by its name ([toolName] is the catalog name of the tool).
bool carriesTool(List<CharacterItem> items, String key, String? toolName) {
  final wanted = {catalogKeyOf(key), if (toolName != null) catalogKeyOf(toolName)};
  for (final item in items) {
    final index = item.templateIndex;
    if (index != null && index.isNotEmpty) {
      if (catalogKeyOf(index) == catalogKeyOf(key)) return true;
      // An item with an index is matched by name only when renamed by hand.
      if (!item.hasOverrides) continue;
    }
    for (final name in [item.effective.name, ?item.templateName]) {
      if (name.trim().isNotEmpty && wanted.contains(catalogKeyOf(name))) return true;
    }
  }
  return false;
}

/// Entries of a vocabulary by index, also found by name (a proficiency added
/// by hand may carry the name instead of the index).
class _ReferenceIndex {
  const _ReferenceIndex(this.byIndex);

  final Map<String, ReferenceEntry>? byIndex;

  ReferenceEntry? entryOf(String key) {
    final entries = byIndex;
    if (entries == null) return null;
    final direct = entries[key];
    if (direct != null) return direct;
    final wanted = catalogKeyOf(key);
    for (final e in entries.values) {
      if (catalogKeyOf(e.index) == wanted || catalogKeyOf(e.name) == wanted) return e;
    }
    return null;
  }

  String nameOf(String key) {
    final name = entryOf(key)?.name;
    if (name != null && name.trim().isNotEmpty) return name;
    final text = key.replaceAll('-', ' ').trim();
    return text.isEmpty ? key : '${text[0].toUpperCase()}${text.substring(1)}';
  }
}

class _ProficiencyRow extends StatelessWidget {
  const _ProficiencyRow({
    required this.type,
    required this.row,
    required this.name,
    required this.entry,
    required this.inInventory,
  });

  final ProficiencyType type;
  final _Row row;
  final String name;
  final ReferenceEntry? entry;

  /// Tools only: the character carries it (null when unknown or not a tool).
  final bool? inInventory;

  @override
  Widget build(BuildContext context) {
    final origin = row.sources.map(proficiencySourceLabel).join(', ');
    final hasInfo = type == ProficiencyType.tool || type == ProficiencyType.language;
    final subtitle = [
      if (row.expertise) 'Pericia',
      'Origen: $origin',
      if (inInventory case final bool carried) carried ? 'En el inventario' : 'No la tienes',
    ].join(' · ');
    final id = '${type.apiValue.toLowerCase()}-${row.key}';
    return ListTile(
      key: Key('proficiency-$id'),
      dense: true,
      contentPadding: EdgeInsets.zero,
      leading: Icon(
        row.expertise ? Icons.stars : Icons.circle,
        size: 18,
        semanticLabel: row.expertise ? 'Pericia' : 'Competente',
      ),
      title: Text(name),
      subtitle: Text(subtitle, key: Key('proficiency-detail-$id')),
      trailing: hasInfo
          ? DetailInfoButton(
              key: Key('proficiency-info-$id'),
              tooltip: 'Ver descripción',
              onPressed: () => showReferenceSheet(
                context,
                title: name,
                subtitle: type == ProficiencyType.tool ? 'Herramienta' : 'Idioma',
                description: entry?.description ?? const [],
              ),
            )
          : null,
    );
  }
}
