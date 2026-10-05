import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_controller.dart';
import 'app_session_epoch.dart';
import 'server_config.dart';
import 'server_config_repository.dart';
import 'server_url.dart';

/// Holds the [ServerConfig], loaded synchronously at startup, and persists
/// every change.
class ServerConfigController extends Notifier<ServerConfig> {
  ServerConfigRepository get _repository => ref.read(serverConfigRepositoryProvider);

  @override
  ServerConfig build() => ref.watch(serverConfigRepositoryProvider).load();

  /// Switches to [url] (any user input; it is normalized first, throwing a
  /// [ServerUrlException] when invalid). When the server actually changes the
  /// session belongs to the previous one: it is closed locally (the old server
  /// is not contacted), the tokens are removed and the cached data is dropped.
  Future<void> setBaseUrl(String url) async {
    final normalized = normalizeServerUrl(url);
    final changed = normalized != state.baseUrl;
    if (changed) await _closeSession();
    await _update(state.copyWith(baseUrl: normalized).withRecent(normalized));
  }

  /// Forgets the current server (the app asks for one again).
  Future<void> clear() async {
    if (state.isConfigured) await _closeSession();
    await _update(state.copyWith(baseUrl: ''));
  }

  /// Removes [url] from the recent servers.
  Future<void> removeRecent(String url) => _update(state.withoutRecent(url));

  /// Pins [fingerprint] (SHA-256 of the certificate) for [host].
  Future<void> trustFingerprint(String host, String fingerprint) => _update(
    state.copyWith(
      trustedFingerprints: {...state.trustedFingerprints, host.toLowerCase(): fingerprint},
    ),
  );

  /// Removes the pinned certificate of [host].
  Future<void> untrust(String host) => _update(
    state.copyWith(trustedFingerprints: {...state.trustedFingerprints}..remove(host.toLowerCase())),
  );

  Future<void> _closeSession() async {
    // Clears the tokens too, without calling the server.
    await ref.read(authControllerProvider.notifier).signOutLocally();
    ref.read(appSessionEpochProvider.notifier).bump();
  }

  Future<void> _update(ServerConfig next) async {
    state = next;
    await _repository.save(next);
  }
}

final serverConfigProvider = NotifierProvider<ServerConfigController, ServerConfig>(
  ServerConfigController.new,
);
