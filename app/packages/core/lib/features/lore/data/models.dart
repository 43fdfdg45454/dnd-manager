import '../../../core/content/content_visibility.dart';
import '../../../core/files/stored_file.dart';

DateTime? _date(Object? value) => value is String ? DateTime.tryParse(value) : null;

String? _strOrNull(Object? value) {
  final text = value?.toString() ?? '';
  return text.isEmpty ? null : text;
}

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) {
  if (value is! List) return const [];
  return [
    for (final e in value)
      if (e is Map) parse(Map<String, dynamic>.from(e)),
  ];
}

/// Category of a lore entry, in display order.
enum LoreCategory {
  world('World', 'Mundo'),
  region('Region', 'Regiones'),
  place('Place', 'Lugares'),
  npc('Npc', 'PNJ'),
  faction('Faction', 'Facciones'),
  event('Event', 'Eventos'),
  quest('Quest', 'Misiones'),
  note('Note', 'Notas'),
  other('Other', 'Otros');

  const LoreCategory(this.apiValue, this.label);

  final String apiValue;

  /// Spanish (plural) label used for the group headings.
  final String label;

  static LoreCategory fromApi(Object? value) => LoreCategory.values.firstWhere(
    (c) => c.apiValue.toLowerCase() == value.toString().toLowerCase(),
    orElse: () => LoreCategory.other,
  );
}

/// An entry of the flat lore tree (`GET /campaigns/{id}/lore`).
class LoreSummary {
  const LoreSummary({
    required this.id,
    required this.campaignId,
    required this.title,
    required this.slug,
    required this.category,
    required this.visibility,
    this.parentId,
    this.sortOrder = 0,
    this.coverFileId,
    this.coverUrl,
    this.updatedAt,
  });

  factory LoreSummary.fromJson(Map<String, dynamic> json) => LoreSummary(
    id: json['id'] as String,
    campaignId: json['campaignId'] as String? ?? '',
    title: json['title'] as String? ?? '',
    slug: json['slug'] as String? ?? '',
    category: LoreCategory.fromApi(json['category']),
    visibility: ContentVisibility.fromApi(json['visibility']),
    parentId: _strOrNull(json['parentId']),
    sortOrder: (json['sortOrder'] as num?)?.toInt() ?? 0,
    coverFileId: _strOrNull(json['coverFileId']),
    coverUrl: _strOrNull(json['coverUrl']),
    updatedAt: _date(json['updatedAt']),
  );

  final String id;
  final String campaignId;
  final String title;
  final String slug;
  final LoreCategory category;
  final ContentVisibility visibility;
  final String? parentId;
  final int sortOrder;
  final String? coverFileId;
  final String? coverUrl;
  final DateTime? updatedAt;

  /// Relative URL of the cover image, if the entry has one.
  String? get cover => coverUrl ?? (coverFileId == null ? null : filePath(coverFileId!));
}

class LoreAttachment {
  const LoreAttachment({
    required this.id,
    required this.fileId,
    required this.fileName,
    required this.contentType,
    required this.url,
    this.caption,
  });

  factory LoreAttachment.fromJson(Map<String, dynamic> json) => LoreAttachment(
    id: json['id'] as String,
    fileId: json['fileId'] as String? ?? '',
    fileName: json['fileName'] as String? ?? '',
    contentType: json['contentType'] as String? ?? '',
    url: json['url'] as String? ?? '',
    caption: _strOrNull(json['caption']),
  );

  final String id;
  final String fileId;
  final String fileName;
  final String contentType;
  final String url;
  final String? caption;

  bool get isImage => contentType.startsWith('image/');
}

/// One entry with its markdown content and attachments (`GET /lore/{id}`).
class LoreEntry extends LoreSummary {
  const LoreEntry({
    required super.id,
    required super.campaignId,
    required super.title,
    required super.slug,
    required super.category,
    required super.visibility,
    super.parentId,
    super.sortOrder,
    super.coverFileId,
    super.coverUrl,
    super.updatedAt,
    this.contentMarkdown = '',
    this.attachments = const [],
  });

  factory LoreEntry.fromJson(Map<String, dynamic> json) {
    final summary = LoreSummary.fromJson(json);
    return LoreEntry(
      id: summary.id,
      campaignId: summary.campaignId,
      title: summary.title,
      slug: summary.slug,
      category: summary.category,
      visibility: summary.visibility,
      parentId: summary.parentId,
      sortOrder: summary.sortOrder,
      coverFileId: summary.coverFileId,
      coverUrl: summary.coverUrl,
      updatedAt: summary.updatedAt,
      contentMarkdown: json['contentMarkdown'] as String? ?? '',
      attachments: _objects(json['attachments'], LoreAttachment.fromJson),
    );
  }

  final String contentMarkdown;
  final List<LoreAttachment> attachments;
}

/// The editable fields of an entry. It is the body of the create request and,
/// as every field is sent, of the edit request too (a null [parentId] moves
/// the entry to the root and a null [coverFileId] removes the cover).
class LoreDraft {
  const LoreDraft({
    required this.title,
    required this.category,
    required this.visibility,
    this.contentMarkdown = '',
    this.parentId,
    this.coverFileId,
  });

  final String title;
  final LoreCategory category;
  final ContentVisibility visibility;
  final String contentMarkdown;
  final String? parentId;
  final String? coverFileId;

  Map<String, dynamic> toJson() => {
    'title': title,
    'category': category.apiValue,
    'contentMarkdown': contentMarkdown,
    'visibility': visibility.apiValue,
    'parentId': parentId,
    'coverFileId': coverFileId,
  };
}

/// Entries matching [query] (title or slug, case-insensitive), grouped by
/// category in display order; empty categories are left out.
Map<LoreCategory, List<LoreSummary>> groupLore(List<LoreSummary> entries, String query) {
  final q = query.trim().toLowerCase();
  final grouped = <LoreCategory, List<LoreSummary>>{};
  for (final category in LoreCategory.values) {
    final inCategory =
        entries
            .where(
              (e) =>
                  e.category == category &&
                  (q.isEmpty ||
                      e.title.toLowerCase().contains(q) ||
                      e.slug.toLowerCase().contains(q)),
            )
            .toList()
          ..sort((a, b) {
            final byOrder = a.sortOrder.compareTo(b.sortOrder);
            return byOrder != 0 ? byOrder : a.title.toLowerCase().compareTo(b.title.toLowerCase());
          });
    if (inCategory.isNotEmpty) grouped[category] = inCategory;
  }
  return grouped;
}

/// Ids of [id] and of every entry below it, to keep it from being its own ancestor.
Set<String> descendantsOf(String id, List<LoreSummary> entries) {
  final result = <String>{id};
  var added = true;
  while (added) {
    added = false;
    for (final e in entries) {
      if (e.parentId != null && result.contains(e.parentId) && result.add(e.id)) added = true;
    }
  }
  return result;
}
