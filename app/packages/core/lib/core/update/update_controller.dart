import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../server/server_config_controller.dart';
import '../storage/local_preferences.dart';
import 'app_release.dart';
import 'app_update_repository.dart';

/// Build number of the installed app (`package_info_plus`), overridden in
/// `main.dart`. Null disables the update checks (tests, unknown build).
final installedBuildProvider = Provider<int?>((ref) => null);

/// Current time for the once-a-day check; tests replace it.
final updateClockProvider = Provider<DateTime Function()>((ref) => DateTime.now);

/// Opens a URL outside the app (the browser downloads the APK and Android
/// offers to install it). Tests replace it.
final urlOpenerProvider = Provider<Future<bool> Function(Uri url)>(
  (ref) =>
      (url) => launchUrl(url, mode: LaunchMode.externalApplication),
);

enum UpdateCheckOutcome {
  /// A newer build is published (now in the controller's state).
  available,

  /// The installed build is the latest one (or nothing is published).
  upToDate,

  /// Not checked: checked less than a day ago, no server or unknown build.
  skipped,

  /// The server could not be asked.
  failed,
}

/// The update published on the server that is newer than the installed app,
/// or null. The last answer is remembered (per server) so a mandatory update
/// keeps blocking the app across restarts until it is installed.
class UpdateController extends Notifier<AppRelease?> {
  static const lastCheckKey = 'update.lastCheck';
  static const latestKey = 'update.latest';
  static const checkInterval = Duration(days: 1);

  @override
  AppRelease? build() {
    final installed = ref.watch(installedBuildProvider);
    final baseUrl = ref.watch(serverConfigProvider.select((config) => config.baseUrl));
    if (installed == null || baseUrl.isEmpty) return null;
    final stored = _readStored(baseUrl);
    return stored != null && stored.buildNumber > installed ? stored : null;
  }

  /// Asks the server for the latest release, at most once a day unless
  /// [force]. Never throws: errors give [UpdateCheckOutcome.failed].
  Future<UpdateCheckOutcome> check({bool force = false}) async {
    final installed = ref.read(installedBuildProvider);
    final baseUrl = ref.read(serverConfigProvider).baseUrl;
    if (installed == null || baseUrl.isEmpty) return UpdateCheckOutcome.skipped;

    final prefs = ref.read(localPreferencesProvider);
    final now = ref.read(updateClockProvider)();
    if (!force) {
      final last = prefs?.getInt(lastCheckKey);
      if (last != null &&
          now.difference(DateTime.fromMillisecondsSinceEpoch(last)) < checkInterval) {
        return UpdateCheckOutcome.skipped;
      }
    }

    final AppRelease? latest;
    try {
      latest = await ref.read(appUpdateRepositoryProvider).latest();
    } catch (_) {
      // Offline or the server failed: silent, the next start tries again.
      return UpdateCheckOutcome.failed;
    }
    if (!ref.mounted) return UpdateCheckOutcome.skipped;

    await prefs?.setInt(lastCheckKey, now.millisecondsSinceEpoch);
    if (latest == null) {
      await prefs?.remove(latestKey);
    } else {
      await prefs?.setString(latestKey, jsonEncode({'server': baseUrl, ...latest.toJson()}));
    }

    final pending = latest != null && latest.buildNumber > installed ? latest : null;
    if (ref.mounted) state = pending;
    return pending == null ? UpdateCheckOutcome.upToDate : UpdateCheckOutcome.available;
  }

  AppRelease? _readStored(String baseUrl) {
    final raw = ref.read(localPreferencesProvider)?.getString(latestKey);
    if (raw == null) return null;
    try {
      final json = Map<String, dynamic>.from(jsonDecode(raw) as Map);
      if (json['server'] != baseUrl) return null;
      return AppRelease.fromJson(json);
    } catch (_) {
      return null;
    }
  }
}

final updateControllerProvider = NotifierProvider<UpdateController, AppRelease?>(
  UpdateController.new,
);
