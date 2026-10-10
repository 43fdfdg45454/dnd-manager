import '../../admin/domain/content_pack.dart' show parseContentPackIds;

/// A content pack of the system of a campaign as the campaign sees it
/// (`GET /api/v1/campaigns/{id}/content-packs`).
class CampaignContentPack {
  const CampaignContentPack({
    required this.id,
    required this.name,
    this.version = '',
    this.isBase = false,
    this.enabled = false,
    this.requires = const [],
  });

  factory CampaignContentPack.fromJson(Map<String, dynamic> json) => CampaignContentPack(
    id: json['id'] as String? ?? '',
    name: json['name'] as String? ?? json['id'] as String? ?? '',
    version: json['version'] as String? ?? '',
    isBase: json['isBase'] as bool? ?? false,
    enabled: (json['enabled'] as bool? ?? false) || (json['isBase'] as bool? ?? false),
    requires: parseContentPackIds(json['requires']),
  );

  final String id;
  final String name;
  final String version;

  /// The base pack of the system (the SRD): always enabled.
  final bool isBase;
  final bool enabled;

  /// Ids of the packs that must be enabled together with this one.
  final List<String> requires;
}

/// The packs [enabled] needs and are not enabled (the base packs always
/// are), by the id of the pack that needs them. Empty when every requirement
/// is met.
Map<String, List<String>> missingPackRequirements(
  List<CampaignContentPack> packs,
  Set<String> enabled,
) {
  final base = {
    for (final p in packs)
      if (p.isBase) p.id,
  };
  return {
    for (final pack in packs)
      if (!pack.isBase && enabled.contains(pack.id))
        if (pack.requires.where((r) => !enabled.contains(r) && !base.contains(r)).toList()
            case final missing when missing.isNotEmpty)
          pack.id: missing,
  };
}
