/// A value read through the response cache.
class CachedResult<T> {
  const CachedResult(this.data, {required this.isStale, required this.fetchedAt});

  final T data;

  /// True when the server could not be reached and [data] is the last stored
  /// answer.
  final bool isStale;

  /// When [data] was received from the server.
  final DateTime fetchedAt;
}

/// Parser for `ApiClient.getCached` of a JSON object.
T Function(Object? json) parseObject<T>(T Function(Map<String, dynamic> json) fromJson) =>
    (json) => fromJson(json as Map<String, dynamic>);

/// Parser for `ApiClient.getCached` of a JSON array of objects.
List<T> Function(Object? json) parseList<T>(T Function(Map<String, dynamic> json) fromJson) =>
    (json) => [for (final e in json as List) fromJson(e as Map<String, dynamic>)];
