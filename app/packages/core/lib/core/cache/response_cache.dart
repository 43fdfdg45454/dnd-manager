import 'package:flutter_riverpod/flutter_riverpod.dart';

/// One stored response: the JSON body of a GET and when it was received.
class CachedEntry {
  const CachedEntry({required this.body, required this.fetchedAt});

  /// JSON-encoded response body.
  final String body;
  final DateTime fetchedAt;
}

/// Last successful answer of each read-only GET, so screens can be shown
/// without a connection (ADR 0002). Keys come from [responseCacheKey].
abstract class ResponseCache {
  Future<CachedEntry?> read(String key);

  Future<void> write(String key, String body, DateTime fetchedAt);

  /// Removes every entry.
  Future<void> clear();

  /// Removes the entries whose key starts with [prefix].
  Future<void> clearPrefix(String prefix);
}

/// Cache key of a GET: the path plus the query parameters sorted by name
/// (null values are dropped), so the same request always maps to one entry.
String responseCacheKey(String path, [Map<String, Object?>? query]) {
  final params = <String, String>{
    for (final entry in [...?query?.entries]..sort((a, b) => a.key.compareTo(b.key)))
      if (entry.value != null) entry.key: _queryValue(entry.value!),
  };
  if (params.isEmpty) return path;
  return '$path?${Uri(queryParameters: params).query}';
}

String _queryValue(Object value) =>
    value is DateTime ? value.toUtc().toIso8601String() : value.toString();

/// Volatile cache used by tests and as the default until `main.dart` installs
/// the SQLite one.
class InMemoryResponseCache implements ResponseCache {
  final Map<String, CachedEntry> entries = {};

  @override
  Future<CachedEntry?> read(String key) async => entries[key];

  @override
  Future<void> write(String key, String body, DateTime fetchedAt) async =>
      entries[key] = CachedEntry(body: body, fetchedAt: fetchedAt);

  @override
  Future<void> clear() async => entries.clear();

  @override
  Future<void> clearPrefix(String prefix) async =>
      entries.removeWhere((key, _) => key.startsWith(prefix));
}

/// The response cache of the app. Overridden in `main.dart` with the SQLite
/// implementation; in memory otherwise (tests).
final responseCacheProvider = Provider<ResponseCache>((ref) => InMemoryResponseCache());
