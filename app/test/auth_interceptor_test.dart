import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:opentrpg/core/auth/auth_interceptor.dart';
import 'package:opentrpg/core/auth/auth_response.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/fakes.dart';

/// Answers 200 only when the bearer token is `access-new`; 401 otherwise.
class _FakeAdapter implements HttpClientAdapter {
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    final authorized = options.headers['Authorization'] == 'Bearer access-new';
    final isLogin = options.path == '/api/v1/auth/login';
    return ResponseBody.fromString(
      jsonEncode({'ok': authorized}),
      authorized && !isLogin ? 200 : 401,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  late FakeTokenStorage storage;
  late _FakeAdapter adapter;
  late Dio dio;
  late int refreshCalls;
  late int expiredCalls;
  late Future<AuthResponse> Function() refreshImpl;

  setUp(() {
    storage = FakeTokenStorage()
      ..access = 'access-old'
      ..refresh = 'refresh-old';
    adapter = _FakeAdapter();
    dio = Dio(BaseOptions(baseUrl: 'http://localhost'))..httpClientAdapter = adapter;
    refreshCalls = 0;
    expiredCalls = 0;
    refreshImpl = () async {
      refreshCalls++;
      await Future<void>.delayed(const Duration(milliseconds: 20));
      final auth = makeAuthResponse(makeUser(), suffix: 'new');
      await storage.save(auth);
      return auth;
    };
    dio.interceptors.add(
      AuthInterceptor(
        dio: dio,
        storage: storage,
        refresh: () => refreshImpl(),
        onSessionExpired: () => expiredCalls++,
      ),
    );
  });

  test('añade el bearer a las peticiones autenticadas', () async {
    storage.access = 'access-new';
    await dio.get<dynamic>('/api/v1/auth/me');

    expect(adapter.requests.single.headers['Authorization'], 'Bearer access-new');
  });

  test('un 401 dispara un único refresh compartido y reintenta todas las peticiones', () async {
    final responses = await Future.wait([
      dio.get<dynamic>('/api/v1/auth/me'),
      dio.get<dynamic>('/api/v1/admin/users'),
      dio.get<dynamic>('/api/v1/app/info'),
    ]);

    expect(responses.map((r) => r.statusCode), everyElement(200));
    expect(refreshCalls, 1);
    expect(expiredCalls, 0);
    // 3 initial attempts + 3 retries.
    expect(adapter.requests, hasLength(6));
  });

  test('si el refresh es rechazado limpia la sesión y propaga el 401', () async {
    refreshImpl = () async {
      refreshCalls++;
      throw dioError(401);
    };

    await expectLater(
      dio.get<dynamic>('/api/v1/auth/me'),
      throwsA(isA<DioException>().having((e) => e.response?.statusCode, 'status', 401)),
    );
    expect(refreshCalls, 1);
    expect(expiredCalls, 1);
    expect(storage.refresh, isNull);
  });

  test('un fallo de red en el refresh no cierra la sesión', () async {
    refreshImpl = () async {
      refreshCalls++;
      throw dioError(null);
    };

    await expectLater(dio.get<dynamic>('/api/v1/auth/me'), throwsA(isA<DioException>()));
    expect(expiredCalls, 0);
    expect(storage.refresh, 'refresh-old');
  });

  test('el 401 del login no intenta refrescar', () async {
    await expectLater(
      dio.post<dynamic>('/api/v1/auth/login', data: {'email': 'a@example.com', 'password': 'x'}),
      throwsA(isA<DioException>()),
    );
    expect(refreshCalls, 0);
    expect(adapter.requests.single.headers.containsKey('Authorization'), isFalse);
  });

  test('no reintenta más de una vez', () async {
    refreshImpl = () async {
      refreshCalls++;
      // Pretend the refresh succeeded but the new token is still rejected.
      final auth = makeAuthResponse(makeUser(), suffix: 'bad');
      await storage.save(auth);
      return auth;
    };

    await expectLater(dio.get<dynamic>('/api/v1/auth/me'), throwsA(isA<DioException>()));
    expect(refreshCalls, 1);
    expect(adapter.requests, hasLength(2));
  });
}
