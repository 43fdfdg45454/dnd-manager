/// An imported content pack (`GET /api/v1/admin/content-packs`).
class ContentPack {
  const ContentPack({
    required this.id,
    required this.name,
    required this.version,
    this.systemId = '',
    this.formatVersion = 0,
    this.isBase = false,
    this.requires = const [],
    this.importedAt,
    this.counts = const {},
  });

  factory ContentPack.fromJson(Map<String, dynamic> json) => ContentPack(
    id: json['id'] as String? ?? '',
    name: json['name'] as String? ?? json['id'] as String? ?? '',
    version: json['version'] as String? ?? '',
    systemId: json['systemId'] as String? ?? '',
    formatVersion: (json['formatVersion'] as num?)?.toInt() ?? 0,
    isBase: json['isBase'] as bool? ?? false,
    requires: parseContentPackIds(json['requires']),
    importedAt: DateTime.tryParse(json['importedAt'] as String? ?? ''),
    counts: parseContentPackCounts(json['counts']),
  );

  final String id;
  final String name;
  final String version;

  /// Game system that understands the pack (`dnd5e`).
  final String systemId;

  /// Version of the pack format: 3 for imported packs, 0 for the base pack
  /// the server loads by itself.
  final int formatVersion;

  /// The base pack of its system (the SRD): always active, never deleted.
  final bool isBase;

  /// Ids of the packs this one references; a campaign enables them together.
  final List<String> requires;
  final DateTime? importedAt;

  /// Definitions per type (`subclasses`, `items`, `spells`...).
  final Map<String, int> counts;

  /// Human readable summary of [counts] in Spanish ("3 objetos · 2 conjuros"),
  /// leaving out the types without definitions.
  String get countsSummary => contentPackCountsSummary(counts);
}

/// A list of pack ids (`requires`); anything else gives an empty list.
List<String> parseContentPackIds(Object? json) => json is List
    ? [
        for (final e in json)
          if (e is String && e.isNotEmpty) e,
      ]
    : const [];

Map<String, int> parseContentPackCounts(Object? json) => json is Map
    ? {
        for (final entry in json.entries)
          if (entry.value is num) entry.key.toString(): (entry.value as num).toInt(),
      }
    : const {};

const _countLabels = <String, (String, String)>{
  'classes': ('clase', 'clases'),
  'subclasses': ('subclase', 'subclases'),
  'features': ('rasgo de clase', 'rasgos de clase'),
  'items': ('objeto', 'objetos'),
  'spells': ('conjuro', 'conjuros'),
  'races': ('raza', 'razas'),
  'subraces': ('subraza', 'subrazas'),
  'raceExtensions': ('raza ampliada', 'razas ampliadas'),
  'traits': ('rasgo racial', 'rasgos raciales'),
  'backgrounds': ('trasfondo', 'trasfondos'),
  'feats': ('dote', 'dotes'),
  'creatures': ('criatura', 'criaturas'),
  'conditions': ('condición', 'condiciones'),
  'rules': ('regla', 'reglas'),
  'reference': ('entrada de vocabulario', 'entradas de vocabulario'),
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
