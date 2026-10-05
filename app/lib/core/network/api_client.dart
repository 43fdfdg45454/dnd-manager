import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_controller.dart';
import '../auth/auth_interceptor.dart';
import '../auth/auth_repository.dart';
import '../auth/auth_state.dart';
import '../auth/token_storage.dart';
import '../server/certificate_pinning.dart';
import '../server/server_config_controller.dart';
import '../server/server_url.dart';

/// Thrown (wrapped in a `DioException`) when a request is made before the user
/// has configured a server.
class ServerNotConfiguredException implements Exception {
  const ServerNotConfiguredException();

  static const message = 'Aún no has configurado el servidor.';

  @override
  String toString() => 'ServerNotConfiguredException';
}

/// Fails every request before it is sent while there is no server URL.
class _RequireServerInterceptor extends Interceptor {
  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    if (options.baseUrl.isEmpty && !options.path.contains('://')) {
      handler.reject(
        DioException(
          requestOptions: options,
          type: DioExceptionType.unknown,
          error: const ServerNotConfiguredException(),
        ),
      );
      return;
    }
    handler.next(options);
  }
}

/// Shared HTTP client. Authentication (bearer + refresh) is added as an
/// interceptor by [apiClientProvider].
class ApiClient {
  /// [pinnedFingerprint] is the SHA-256 of the self-signed certificate the
  /// user chose to trust; it is only honoured for the host of [baseUrl].
  ApiClient({required String baseUrl, Dio? dio, String? pinnedFingerprint})
    : dio = dio ?? _createDio(baseUrl, pinnedFingerprint);

  final Dio dio;

  static Dio _createDio(String baseUrl, String? pinnedFingerprint) {
    final dio = Dio(
      BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: const Duration(seconds: 10),
        receiveTimeout: const Duration(seconds: 30),
        headers: const {'Accept': 'application/json'},
      ),
    );
    final host = serverHost(baseUrl);
    if (pinnedFingerprint != null && host != null && baseUrl.startsWith('https://')) {
      dio.httpClientAdapter = pinnedCertificateAdapter(host: host, fingerprint: pinnedFingerprint);
    }
    dio.interceptors.add(_RequireServerInterceptor());
    return dio;
  }
}

final Provider<ApiClient> apiClientProvider = Provider<ApiClient>((ref) {
  // Rebuilt (together with every repository that watches it) only when the URL
  // or the pinned certificate of its host changes.
  final (baseUrl, pinned) = ref.watch(
    serverConfigProvider.select((config) {
      final host = serverHost(config.baseUrl);
      return (config.baseUrl, host == null ? null : config.trustedFingerprints[host]);
    }),
  );
  final client = ApiClient(baseUrl: baseUrl, pinnedFingerprint: pinned);
  // The callbacks read other providers lazily (at request time) so there is no
  // build-time cycle between the client, the repository and the controller.
  client.dio.interceptors.add(
    AuthInterceptor(
      dio: client.dio,
      storage: ref.watch(tokenStorageProvider),
      refresh: () async {
        final auth = await ref.read(authRepositoryProvider).refresh();
        final controller = ref.read(authControllerProvider.notifier);
        if (ref.read(authControllerProvider) is AuthSignedIn) {
          controller.updateUser(auth.user);
        }
        return auth;
      },
      onSessionExpired: () => ref.read(authControllerProvider.notifier).onSessionExpired(),
    ),
  );
  return client;
});
