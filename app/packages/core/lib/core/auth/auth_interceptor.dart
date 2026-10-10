import 'package:dio/dio.dart';

import 'auth_repository.dart';
import 'auth_response.dart';
import 'token_storage.dart';

/// Adds the bearer token and, on a `401`, performs a single shared refresh
/// (guarded by a lock so parallel requests do not each trigger one) and
/// retries the request once. If the server rejects the refresh the session is
/// closed through [onSessionExpired].
class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required this.dio,
    required this.storage,
    required this.refresh,
    required this.onSessionExpired,
  });

  /// Client used to replay the failed request (the one this interceptor is on).
  final Dio dio;
  final TokenStorage storage;

  /// Performs the refresh call and persists the new tokens.
  final Future<AuthResponse> Function() refresh;

  /// Called after the stored session has been cleared.
  final void Function() onSessionExpired;

  static const _retriedKey = 'auth_retried';
  static const _publicPaths = {
    '/api/v1/auth/login',
    '/api/v1/auth/refresh',
    '/api/v1/auth/password/forgot',
    '/api/v1/auth/password/set',
  };

  Future<String?>? _refreshInFlight;

  bool _isPublic(RequestOptions options) => _publicPaths.contains(options.path);

  @override
  Future<void> onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    if (!_isPublic(options)) {
      final token = await storage.readAccessToken();
      if (token != null) options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final request = err.requestOptions;
    if (err.response?.statusCode != 401 ||
        _isPublic(request) ||
        request.extra[_retriedKey] == true) {
      return handler.next(err);
    }

    final String? newToken;
    try {
      newToken = await _refreshedToken(request.headers['Authorization']);
    } on DioException {
      // Network failure while refreshing: keep the session, surface the error.
      return handler.next(err);
    }
    if (newToken == null) return handler.next(err);

    try {
      final retry = request.copyWith(
        headers: {...request.headers, 'Authorization': 'Bearer $newToken'},
        extra: {...request.extra, _retriedKey: true},
      );
      handler.resolve(await dio.fetch<dynamic>(retry));
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }

  /// Returns a valid access token, or null when the session has ended.
  Future<String?> _refreshedToken(Object? sentAuthorization) async {
    final current = await storage.readAccessToken();
    // Another request already refreshed after this one was sent.
    if (current != null && 'Bearer $current' != sentAuthorization) {
      return current;
    }
    return _refreshInFlight ??= _doRefresh().whenComplete(() => _refreshInFlight = null);
  }

  Future<String?> _doRefresh() async {
    try {
      return (await refresh()).accessToken;
    } on SessionExpiredException {
      await _expire();
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      if (status != null && status >= 400 && status < 500) {
        await _expire();
      } else {
        rethrow;
      }
    }
    return null;
  }

  Future<void> _expire() async {
    await storage.clear();
    onSessionExpired();
  }
}
