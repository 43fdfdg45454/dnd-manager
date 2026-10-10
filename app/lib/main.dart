import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/cache/cache_database.dart';
import 'package:opentrpg_core/core/cache/response_cache.dart';
import 'package:opentrpg_core/core/network/connectivity.dart';
import 'package:opentrpg_core/core/network/trust_store.dart';
import 'package:opentrpg_core/core/server/server_config_repository.dart';
import 'package:opentrpg_core/core/storage/local_preferences.dart';
import 'package:opentrpg_core/core/systems/system_registry.dart';
import 'package:opentrpg_core/core/update/update_controller.dart';
import 'package:opentrpg_dnd5e/dnd5e_ui.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // Must run before any HttpClient is created so user-installed CAs are trusted.
  final trustStore = await installGlobalHttpOverrides();
  final prefs = await SharedPreferences.getInstance();
  final responseCache = DriftResponseCache(CacheDatabase.open());
  // Old searches and pages add up; trimming is best effort.
  responseCache.prune().ignore();
  // Coming back after a while: the network may have changed unnoticed.
  final resumeWatcher = AppResumeWatcher()..attach();
  runApp(
    ProviderScope(
      overrides: [
        // The game systems this build brings; the core never imports them.
        gameSystemsProvider.overrideWithValue(const [Dnd5eUi()]),
        serverConfigRepositoryProvider.overrideWithValue(ServerConfigRepository(prefs)),
        localPreferencesProvider.overrideWithValue(prefs),
        responseCacheProvider.overrideWithValue(responseCache),
        connectivitySourceProvider.overrideWithValue(PlatformConnectivitySource()),
        appResumedAfterBackgroundProvider.overrideWithValue(resumeWatcher.resumedAfterBackground),
        trustedUserCertificateCountProvider.overrideWithValue(trustStore.certificateCount),
        installedBuildProvider.overrideWithValue(await _installedBuild()),
      ],
      child: const OpenTrpgApp(),
    ),
  );
}

/// Build number of this APK (`version: x.y.z+build` in pubspec.yaml), or null
/// when it cannot be read (the update check is then skipped).
Future<int?> _installedBuild() async {
  try {
    return int.tryParse((await PackageInfo.fromPlatform()).buildNumber);
  } catch (_) {
    return null;
  }
}
