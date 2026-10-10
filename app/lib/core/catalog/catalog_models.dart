// Catalog types every game system shares: the paged envelope of the catalog
// endpoints and the sources of content (the SRD, content packs).

String _str(Object? value, [String fallback = '']) {
  if (value == null) return fallback;
  return value is String ? value : value.toString();
}

String? _strOrNull(Object? value) {
  final text = _str(value);
  return text.isEmpty ? null : text;
}

int? _int(Object? value) {
  if (value is num) return value.toInt();
  if (value is String) return int.tryParse(value);
  return null;
}

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) {
  if (value is! List) return const [];
  return [
    for (final e in value)
      if (e is Map) parse(Map<String, dynamic>.from(e)),
  ];
}

/// Generic `{ items, total, page, pageSize }` envelope.
class Page<T> {
  const Page({
    required this.items,
    required this.total,
    required this.page,
    required this.pageSize,
  });

  factory Page.fromJson(Map<String, dynamic> json, T Function(Map<String, dynamic>) parse) {
    final items = _objects(json['items'], parse);
    return Page(
      items: items,
      total: _int(json['total']) ?? items.length,
      page: _int(json['page']) ?? 1,
      pageSize: _int(json['pageSize']) ?? items.length,
    );
  }

  final List<T> items;
  final int total;
  final int page;
  final int pageSize;

  bool get hasMore => items.length < total;

  Page<T> copyWith({List<T>? items, int? total, int? page}) => Page(
    items: items ?? this.items,
    total: total ?? this.total,
    page: page ?? this.page,
    pageSize: pageSize,
  );
}

/// A source of catalog content (`GET /catalog/sources`): "srd" or a content
/// pack imported by the administrator.
class CatalogSource {
  const CatalogSource({required this.id, required this.name, this.version});

  factory CatalogSource.fromJson(Map<String, dynamic> json) => CatalogSource(
    id: _str(json['id']),
    name: _str(json['name'], _str(json['id'])),
    version: _strOrNull(json['version']),
  );

  final String id;
  final String name;
  final String? version;
}
