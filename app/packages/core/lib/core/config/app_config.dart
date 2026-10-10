import 'package:flutter/foundation.dart';

/// Build-time configuration.
abstract final class AppConfig {
  static const String appName = 'OpenTRPG';

  static const String _definedServerUrl = String.fromEnvironment('API_BASE_URL');

  /// Server URL suggested the first time the app runs, before the user has
  /// saved one. Set with `--dart-define=API_BASE_URL=...`; empty in release
  /// builds by default and the Android emulator host in debug builds.
  static String get defaultServerUrl {
    if (_definedServerUrl.isNotEmpty) return _definedServerUrl;
    return kDebugMode ? 'http://10.0.2.2:8080' : '';
  }
}
