import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../config/app_config.dart';
import 'server_config.dart';

/// Persists the [ServerConfig] in `shared_preferences`.
class ServerConfigRepository {
  ServerConfigRepository(this._prefs, {String? defaultBaseUrl})
    : _defaultBaseUrl = defaultBaseUrl ?? AppConfig.defaultServerUrl;

  static const baseUrlKey = 'server.baseUrl';
  static const recentUrlsKey = 'server.recentUrls';
  static const fingerprintsKey = 'server.trustedFingerprints';

  final SharedPreferences _prefs;
  final String _defaultBaseUrl;

  /// Reads the stored configuration. The build-time default is only used as the
  /// initial URL while nothing has ever been saved.
  ServerConfig load() => ServerConfig(
    baseUrl: _prefs.getString(baseUrlKey) ?? _defaultBaseUrl,
    recentUrls: _prefs.getStringList(recentUrlsKey) ?? const [],
    trustedFingerprints: _readFingerprints(),
  );

  Future<void> save(ServerConfig config) async {
    await _prefs.setString(baseUrlKey, config.baseUrl);
    await _prefs.setStringList(recentUrlsKey, config.recentUrls);
    await _prefs.setString(fingerprintsKey, jsonEncode(config.trustedFingerprints));
  }

  Map<String, String> _readFingerprints() {
    final raw = _prefs.getString(fingerprintsKey);
    if (raw == null) return const {};
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! Map) return const {};
      return {
        for (final entry in decoded.entries)
          if (entry.key is String && entry.value is String)
            entry.key as String: entry.value as String,
      };
    } on FormatException {
      return const {};
    }
  }
}

/// Must be overridden at startup with a repository built from the loaded
/// `SharedPreferences` instance (see `main.dart`).
final serverConfigRepositoryProvider = Provider<ServerConfigRepository>(
  (ref) => throw UnimplementedError('serverConfigRepositoryProvider must be overridden'),
);
