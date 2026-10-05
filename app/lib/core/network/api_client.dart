import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_controller.dart';
import '../auth/auth_interceptor.dart';
import '../auth/auth_repository.dart';
import '../auth/auth_state.dart';
import '../auth/token_storage.dart';
import '../config/app_config.dart';

/// Shared HTTP client. Authentication (bearer + refresh) is added as an
/// interceptor by [apiClientProvider].
class ApiClient {
  ApiClient({required String baseUrl, Dio? dio})
    : dio =
          dio ??
          Dio(
            BaseOptions(
              baseUrl: baseUrl,
              connectTimeout: const Duration(seconds: 10),
              receiveTimeout: const Duration(seconds: 30),
              headers: const {'Accept': 'application/json'},
            ),
          );

  final Dio dio;
}

final Provider<ApiClient> apiClientProvider = Provider<ApiClient>((ref) {
  final client = ApiClient(baseUrl: AppConfig.apiBaseUrl);
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
