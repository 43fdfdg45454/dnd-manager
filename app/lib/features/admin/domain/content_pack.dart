/// An imported content pack (`GET /api/v1/admin/content-packs`).
class ContentPack {
  const ContentPack({
    required this.id,
    required this.name,
    required this.version,
    this.importedAt,
    this.counts = const {},
  });

  factory ContentPack.fromJson(Map<String, dynamic> json) => ContentPack(
    id: json['id'] as String? ?? '',
    name: json['name'] as String? ?? json['id'] as String? ?? '',
    version: json['version'] as String? ?? '',
    importedAt: DateTime.tryParse(json['importedAt'] as String? ?? ''),
    counts: parseContentPackCounts(json['counts']),
  );

  final String id;
  final String name;
  final String version;
  final DateTime? importedAt;

  /// Definitions per type (`subclasses`, `items`, `spells`...).
  final Map<String, int> counts;

  /// Human readable summary of [counts] in Spanish ("3 objetos · 2 conjuros"),
  /// leaving out the types without definitions.
  String get countsSummary => contentPackCountsSummary(counts);
}

Map<String, int> parseContentPackCounts(Object? json) => json is Map
    ? {
        for (final entry in json.entries)
          if (entry.value is num) entry.key.toString(): (entry.value as num).toInt(),
      }
    : const {};

const _countLabels = <String, (String, String)>{
  'subclasses': ('subclase', 'subclases'),
  'features': ('rasgo de clase', 'rasgos de clase'),
  'items': ('objeto', 'objetos'),
  'spells': ('conjuro', 'conjuros'),
  'races': ('raza', 'razas'),
  'subraces': ('subraza', 'subrazas'),
  'traits': ('rasgo racial', 'rasgos raciales'),
  'backgrounds': ('trasfondo', 'trasfondos'),
  'trinkets': ('baratija', 'baratijas'),
};

String contentPackCountsSummary(Map<String, int> counts) {
  final parts = <String>[];
  for (final entry in _countLabels.entries) {
    final count = counts[entry.key] ?? 0;
    if (count <= 0) continue;
    parts.add('$count ${count == 1 ? entry.value.$1 : entry.value.$2}');
  }
  return parts.join(' · ');
}

/// Result of importing a pack (`POST /api/v1/admin/content-packs`).
class ContentPackImportResult {
  const ContentPackImportResult({
    required this.id,
    required this.name,
    required this.version,
    this.counts = const {},
  });

  factory ContentPackImportResult.fromJson(Map<String, dynamic> json) => ContentPackImportResult(
    id: json['id'] as String? ?? '',
    name: json['name'] as String? ?? json['id'] as String? ?? '',
    version: json['version'] as String? ?? '',
    counts: parseContentPackCounts(json['counts']),
  );

  final String id;
  final String name;
  final String version;
  final Map<String, int> counts;

  String get countsSummary => contentPackCountsSummary(counts);
}
