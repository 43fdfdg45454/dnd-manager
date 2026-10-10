import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app.dart';
import 'core/cache/cache_database.dart';
import 'core/cache/response_cache.dart';
import 'core/network/connectivity.dart';
import 'core/network/trust_store.dart';
import 'core/server/server_config_repository.dart';
import 'core/storage/local_preferences.dart';
import 'core/update/update_controller.dart';

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
