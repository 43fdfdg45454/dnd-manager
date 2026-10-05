/// Configuración de compilación. Se fija con `--dart-define`, por ejemplo:
/// `flutter run --dart-define=API_BASE_URL=http://10.0.2.2:8080`.
abstract final class AppConfig {
  static const String appName = 'D&D Companion';

  /// URL base de la API. Por defecto apunta al host del emulador Android.
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:8080',
  );
}
