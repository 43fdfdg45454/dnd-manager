import 'package:flutter/foundation.dart';

/// Where the app talks to: the server URL, the recently used ones and the
/// pinned certificate fingerprints. Immutable.
@immutable
class ServerConfig {
  ServerConfig({
    this.baseUrl = '',
    List<String> recentUrls = const [],
    Map<String, String> trustedFingerprints = const {},
  }) : recentUrls = List.unmodifiable(_limitRecents(recentUrls)),
       trustedFingerprints = Map.unmodifiable(trustedFingerprints);

  /// Maximum number of remembered servers.
  static const maxRecentUrls = 5;

  /// Normalized base URL; empty when no server has been configured yet.
  final String baseUrl;

  /// Recently used URLs, the most recent first, without repeats (at most
  /// [maxRecentUrls]).
  final List<String> recentUrls;

  /// SHA-256 fingerprint (uppercase hex, colon separated) of the certificate
  /// the user chose to trust, by lower-cased host.
  final Map<String, String> trustedFingerprints;

  bool get isConfigured => baseUrl.isNotEmpty;

  ServerConfig copyWith({
    String? baseUrl,
    List<String>? recentUrls,
    Map<String, String>? trustedFingerprints,
  }) => ServerConfig(
    baseUrl: baseUrl ?? this.baseUrl,
    recentUrls: recentUrls ?? this.recentUrls,
    trustedFingerprints: trustedFingerprints ?? this.trustedFingerprints,
  );

  /// Copy with [url] moved (or added) to the front of [recentUrls].
  ServerConfig withRecent(String url) => copyWith(recentUrls: [url, ...recentUrls]);

  /// Copy without [url] in [recentUrls].
  ServerConfig withoutRecent(String url) =>
      copyWith(recentUrls: recentUrls.where((u) => u != url).toList());

  static List<String> _limitRecents(List<String> urls) {
    final seen = <String>{};
    return [
      for (final url in urls)
        if (url.isNotEmpty && seen.add(url)) url,
    ].take(maxRecentUrls).toList();
  }

  @override
  bool operator ==(Object other) =>
      other is ServerConfig &&
      other.baseUrl == baseUrl &&
      listEquals(other.recentUrls, recentUrls) &&
      mapEquals(other.trustedFingerprints, trustedFingerprints);

  @override
  int get hashCode => Object.hash(
    baseUrl,
    Object.hashAll(recentUrls),
    Object.hashAll(trustedFingerprints.entries.map((e) => Object.hash(e.key, e.value))),
  );
}
