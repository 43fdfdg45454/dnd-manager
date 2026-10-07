import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:dio/io.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_controller.dart';
import '../auth/auth_interceptor.dart';
import '../auth/auth_repository.dart';
import '../auth/auth_state.dart';
import '../auth/token_storage.dart';
import '../cache/cached_result.dart';
import '../cache/response_cache.dart';
import '../cache/stale_data.dart';
import '../server/certificate_pinning.dart';
import '../server/server_config_controller.dart';
import '../server/server_url.dart';
import 'api_error.dart';
import 'connectivity.dart';

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

/// Called after every cached read with its cache key: [staleSince] is when the
/// returned data was received if it came from the cache, null if it is fresh.
typedef FreshnessReporter = void Function(String key, DateTime? staleSince);

/// Shared HTTP client. Authentication (bearer + refresh) is added as an
/// interceptor by [apiClientProvider].
class ApiClient {
  /// [pinnedFingerprint] is the SHA-256 of the self-signed certificate the
  /// user chose to trust; it is only honoured for the host of [baseUrl].
  /// [cache] stores the answers of [getCached] (in memory when omitted).
  ApiClient({
    required this.baseUrl,
    Dio? dio,
    String? pinnedFingerprint,
    ResponseCache? cache,
    this.onFreshness,
    HttpClientAdapter Function()? adapterFactory,
  }) : _adapterFactory =
           adapterFactory ??
           (dio == null ? () => _createAdapter(baseUrl, pinnedFingerprint) : null),
       dio = dio ?? _createDio(baseUrl),
       cache = cache ?? InMemoryResponseCache() {
    final factory = _adapterFactory;
    if (factory != null) this.dio.httpClientAdapter = factory();
  }

  final String baseUrl;
  final Dio dio;
  final ResponseCache cache;
  final FreshnessReporter? onFreshness;

  /// Creates the adapter (and so the `HttpClient` and its connection pool);
  /// null when the client was given a [Dio] without one (tests).
  final HttpClientAdapter Function()? _adapterFactory;

  static Dio _createDio(String baseUrl) {
    final dio = Dio(
      BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: const Duration(seconds: 10),
        receiveTimeout: const Duration(seconds: 30),
        headers: const {'Accept': 'application/json'},
      ),
    );
    dio.interceptors.add(_RequireServerInterceptor());
    return dio;
  }

  /// A plain `dart:io` adapter (it inherits the user CAs of
  /// `HttpOverrides.global`), or one that also accepts the certificate pinned
  /// for the server host. DNS is not cached here: every new connection
  /// resolves the host again.
  static HttpClientAdapter _createAdapter(String baseUrl, String? pinnedFingerprint) {
    final host = serverHost(baseUrl);
    if (pinnedFingerprint != null && host != null && baseUrl.startsWith('https://')) {
      return pinnedCertificateAdapter(host: host, fingerprint: pinnedFingerprint);
    }
    return IOHttpClientAdapter();
  }

  /// Drops every pooled (keep-alive) connection and starts a new pool. Called
  /// when the network changes: an idle connection may be bound to the old
  /// interface or to the address the host had on the previous network, and
  /// reusing it would hang until it times out. Requests in flight fail and
  /// are retried by their callers as any network failure.
  void resetConnections() {
    final factory = _adapterFactory;
    if (factory == null) return;
    final previous = dio.httpClientAdapter;
    dio.httpClientAdapter = factory();
    previous.close(force: true);
  }

  /// Absolute URL of [url], which may be relative to the server (`/api/...`).
  String absoluteUrl(String url) {
    if (url.contains('://')) return url;
    final base = baseUrl.replaceAll(RegExp(r'/+$'), '');
    return url.startsWith('/') ? '$base$url' : '$base/$url';
  }

  /// Read-only GET through the response cache: asks the server and stores the
  /// answer; when the server cannot be reached (connection error or timeout)
  /// returns the stored answer marked as stale. Without a stored answer, and
  /// for any other error (401, 404, 500...), the error is rethrown.
  ///
  /// [parse] receives the decoded JSON body. Null [query] values are dropped.
  Future<CachedResult<T>> getCached<T>(
    String path, {
    Map<String, Object?>? query,
    required T Function(Object? json) parse,
  }) async {
    final params = <String, Object>{
      for (final entry in (query ?? const <String, Object?>{}).entries)
        if (entry.value != null) entry.key: entry.value!,
    };
    final key = responseCacheKey(path, params);

    final Response<Object?> response;
    try {
      response = await dio.get<Object?>(path, queryParameters: params.isEmpty ? null : params);
    } on DioException catch (error, stack) {
      if (!isNetworkFailure(error)) rethrow;
      final stale = await _readStale(key, parse);
      if (stale == null) Error.throwWithStackTrace(error, stack);
      onFreshness?.call(key, stale.fetchedAt);
      return stale;
    }

    final data = parse(response.data);
    final fetchedAt = DateTime.now().toUtc();
    try {
      await cache.write(key, jsonEncode(response.data), fetchedAt);
    } catch (_) {
      // Caching is best effort.
    }
    onFreshness?.call(key, null);
    return CachedResult(data, isStale: false, fetchedAt: fetchedAt);
  }

  Future<CachedResult<T>?> _readStale<T>(String key, T Function(Object? json) parse) async {
    try {
      final entry = await cache.read(key);
      if (entry == null) return null;
      return CachedResult(parse(jsonDecode(entry.body)), isStale: true, fetchedAt: entry.fetchedAt);
    } catch (_) {
      // A damaged or outdated entry is as good as none.
      return null;
    }
  }
}

/// Reports to [connectivityProvider] whether each request reached the server.
class _ConnectivityInterceptor extends Interceptor {
  _ConnectivityInterceptor(this._report);

  final void Function(bool reached) _report;

  @override
  void onResponse(Response<dynamic> response, ResponseInterceptorHandler handler) {
    _report(true);
    handler.next(response);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    if (isNetworkFailure(err)) {
      _report(false);
    } else if (err.response != null) {
      _report(true);
    }
    handler.next(err);
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
  final client = ApiClient(
    baseUrl: baseUrl,
    pinnedFingerprint: pinned,
    cache: ref.watch(responseCacheProvider),
    onFreshness: (key, staleSince) {
      if (ref.mounted) ref.read(staleDataProvider.notifier).report(key, staleSince);
    },
  );
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
  // A new network: forget the connections opened on the previous one.
  ref.listen(
    connectivityProvider.select((status) => status.networkGeneration),
    (_, _) => client.resetConnections(),
  );
  client.dio.interceptors.add(
    _ConnectivityInterceptor((reached) {
      if (!ref.mounted) return;
      final connectivity = ref.read(connectivityProvider.notifier);
      reached ? connectivity.reportRequestSucceeded() : connectivity.reportRequestFailed();
    }),
  );
  return client;
});
