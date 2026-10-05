import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:dnd_companion/app.dart';
import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_repository.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/auth/token_storage.dart';
import 'package:dnd_companion/core/network/api_client.dart';
import 'package:dnd_companion/core/network/api_error.dart';
import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/core/server/certificate_pinning.dart';
import 'package:dnd_companion/core/server/server_config.dart';
import 'package:dnd_companion/core/server/server_config_controller.dart';
import 'package:dnd_companion/core/server/server_config_repository.dart';
import 'package:dnd_companion/core/server/server_probe.dart';
import 'package:dnd_companion/core/server/server_url.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'helpers/fakes.dart';

const _fingerprint = 'AB:CD:EF:01:23:45:67:89';

Future<FakeServerProbe> _pumpApp(
  WidgetTester tester, {
  required FakeServerConfigRepository config,
  FakeServerProbe? probe,
  FakeTokenStorage? storage,
}) async {
  final fakeProbe =
      probe ??
      FakeServerProbe(
        result: const ServerProbeResult(name: 'Taberna', version: '1.2.3'),
      );
  final tokens = storage ?? FakeTokenStorage();
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        serverConfigRepositoryProvider.overrideWithValue(config),
        serverProbeProvider.overrideWithValue(fakeProbe),
        tokenStorageProvider.overrideWithValue(tokens),
        authRepositoryProvider.overrideWithValue(FakeAuthRepository(storage: tokens)),
        fakeServerInfoOverride,
        fakeCampaignsOverride,
      ],
      child: const DndCompanionApp(),
    ),
  );
  await tester.pumpAndSettle();
  return fakeProbe;
}

class _ThrowingAdapter implements HttpClientAdapter {
  _ThrowingAdapter({this.error, this.status, this.body});

  final DioException Function(RequestOptions)? error;
  final int? status;
  final Object? body;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (error != null) throw error!(options);
    return ResponseBody.fromString(
      jsonEncode(body),
      status!,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  group('normalizeServerUrl', () {
    test('añade http:// cuando falta el esquema', () {
      expect(normalizeServerUrl('192.168.1.50:8080'), 'http://192.168.1.50:8080');
      expect(normalizeServerUrl('dnd.example.com'), 'http://dnd.example.com');
      expect(normalizeServerUrl('localhost'), 'http://localhost');
    });

    test('quita espacios y barras finales', () {
      expect(normalizeServerUrl('  http://10.8.0.1:8080/  '), 'http://10.8.0.1:8080');
      expect(normalizeServerUrl('https://dnd.example.com///'), 'https://dnd.example.com');
    });

    test('conserva https, el puerto y un prefijo de ruta; pasa a minúsculas el host', () {
      expect(
        normalizeServerUrl('HTTPS://DND.Example.com:8443/api/'),
        'https://dnd.example.com:8443/api',
      );
      expect(normalizeServerUrl('http://[::1]:8080'), 'http://[::1]:8080');
    });

    test('descarta consulta y fragmento', () {
      expect(normalizeServerUrl('http://dnd.example.com/?a=1#x'), 'http://dnd.example.com');
    });

    test('rechaza texto inválido con mensajes en español', () {
      expect(
        () => normalizeServerUrl(''),
        throwsA(
          isA<ServerUrlException>().having((e) => e.message, 'message', contains('Introduce')),
        ),
      );
      expect(() => normalizeServerUrl('   '), throwsA(isA<ServerUrlException>()));
      expect(() => normalizeServerUrl('hola mundo'), throwsA(isA<ServerUrlException>()));
      expect(() => normalizeServerUrl('ftp://dnd.example.com'), throwsA(isA<ServerUrlException>()));
      expect(() => normalizeServerUrl('http://'), throwsA(isA<ServerUrlException>()));
      expect(() => normalizeServerUrl('http://exa_mple!.com'), throwsA(isA<ServerUrlException>()));
      expect(() => normalizeServerUrl('http://.example.com'), throwsA(isA<ServerUrlException>()));
      expect(
        () => normalizeServerUrl('http://dnd.example.com:99999'),
        throwsA(isA<ServerUrlException>()),
      );
      expect(
        () => normalizeServerUrl('http://user:pw@dnd.example.com'),
        throwsA(isA<ServerUrlException>()),
      );
    });

    test('validateServerUrl devuelve el mensaje o null', () {
      expect(validateServerUrl('dnd.example.com'), isNull);
      expect(validateServerUrl(''), isNotNull);
    });
  });

  group('ServerConfig', () {
    test('las recientes no repiten, ponen la más reciente primero y conservan 5', () {
      var config = ServerConfig();
      for (final url in ['http://a', 'http://b', 'http://c', 'http://d', 'http://e', 'http://f']) {
        config = config.withRecent(url);
      }
      expect(config.recentUrls, ['http://f', 'http://e', 'http://d', 'http://c', 'http://b']);

      config = config.withRecent('http://c');
      expect(config.recentUrls, ['http://c', 'http://f', 'http://e', 'http://d', 'http://b']);
      expect(config.recentUrls.length, ServerConfig.maxRecentUrls);
    });

    test('el constructor también limpia repetidos y excede 5', () {
      final config = ServerConfig(
        recentUrls: [
          'http://a',
          'http://a',
          'http://b',
          'http://c',
          'http://d',
          'http://e',
          'http://f',
        ],
      );
      expect(config.recentUrls, ['http://a', 'http://b', 'http://c', 'http://d', 'http://e']);
    });
  });

  group('ServerConfigRepository', () {
    test('usa el valor por defecto solo si no hay nada guardado y persiste los cambios', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();
      final repository = ServerConfigRepository(
        prefs,
        defaultBaseUrl: 'http://default.example.com',
      );

      expect(repository.load().baseUrl, 'http://default.example.com');

      await repository.save(
        ServerConfig(
          baseUrl: '',
          recentUrls: ['http://a'],
          trustedFingerprints: {'dnd.example.com': _fingerprint},
        ),
      );
      final loaded = ServerConfigRepository(
        prefs,
        defaultBaseUrl: 'http://default.example.com',
      ).load();
      expect(loaded.baseUrl, isEmpty, reason: 'un valor vacío guardado no se pisa con el defecto');
      expect(loaded.recentUrls, ['http://a']);
      expect(loaded.trustedFingerprints, {'dnd.example.com': _fingerprint});
      expect(prefs.getString('server.baseUrl'), '');
    });
  });

  group('ServerConfigController', () {
    late FakeTokenStorage storage;
    late FakeAuthRepository authRepository;
    late FakeServerConfigRepository configRepository;
    late ProviderContainer container;

    setUp(() {
      storage = FakeTokenStorage();
      authRepository = FakeAuthRepository(storage: storage, loginUser: makeUser());
      configRepository = FakeServerConfigRepository(
        ServerConfig(baseUrl: 'http://old.example.com'),
      );
      container = ProviderContainer(
        overrides: [
          tokenStorageProvider.overrideWithValue(storage),
          authRepositoryProvider.overrideWithValue(authRepository),
          serverConfigRepositoryProvider.overrideWithValue(configRepository),
        ],
      );
      addTearDown(container.dispose);
    });

    Future<void> signIn() async {
      container.read(authControllerProvider);
      await Future<void>.delayed(Duration.zero);
      await container.read(authControllerProvider.notifier).login('user@example.com', 'x');
      expect(container.read(authControllerProvider), isA<AuthSignedIn>());
      expect(storage.refresh, isNotNull);
    }

    test(
      'cambiar de servidor deja la sesión en signedOut y limpia los tokens sin llamar al servidor',
      () async {
        await signIn();

        await container.read(serverConfigProvider.notifier).setBaseUrl('10.8.0.1:8080/');

        expect(container.read(authControllerProvider), isA<AuthSignedOut>());
        expect(storage.access, isNull);
        expect(storage.refresh, isNull);
        expect(authRepository.logoutCalls, 0);
        expect(container.read(serverConfigProvider).baseUrl, 'http://10.8.0.1:8080');
        expect(configRepository.stored.baseUrl, 'http://10.8.0.1:8080');
      },
    );

    test('guardar la misma URL no cierra la sesión', () async {
      await signIn();

      await container.read(serverConfigProvider.notifier).setBaseUrl('http://old.example.com/');

      expect(container.read(authControllerProvider), isA<AuthSignedIn>());
      expect(storage.refresh, isNotNull);
    });

    test('una URL inválida lanza ServerUrlException y no cambia nada', () async {
      await signIn();

      await expectLater(
        container.read(serverConfigProvider.notifier).setBaseUrl('hola mundo'),
        throwsA(isA<ServerUrlException>()),
      );
      expect(container.read(authControllerProvider), isA<AuthSignedIn>());
      expect(container.read(serverConfigProvider).baseUrl, 'http://old.example.com');
    });

    test('las recientes se actualizan al guardar y se pueden quitar', () async {
      final notifier = container.read(serverConfigProvider.notifier);
      for (var i = 1; i <= 6; i++) {
        await notifier.setBaseUrl('http://s$i.example.com');
      }
      await notifier.setBaseUrl('http://s4.example.com');

      final recents = container.read(serverConfigProvider).recentUrls;
      expect(recents.first, 'http://s4.example.com');
      expect(recents.length, 5);
      expect(recents.toSet().length, 5);

      await notifier.removeRecent('http://s4.example.com');
      expect(
        container.read(serverConfigProvider).recentUrls,
        isNot(contains('http://s4.example.com')),
      );
      expect(configRepository.stored.recentUrls.length, 4);
    });

    test('trustFingerprint y untrust fijan y retiran la huella por host', () async {
      final notifier = container.read(serverConfigProvider.notifier);
      await notifier.trustFingerprint('DND.Example.com', _fingerprint);
      expect(container.read(serverConfigProvider).trustedFingerprints, {
        'dnd.example.com': _fingerprint,
      });

      await notifier.untrust('dnd.example.com');
      expect(container.read(serverConfigProvider).trustedFingerprints, isEmpty);
    });

    test('clear olvida el servidor y cierra la sesión', () async {
      await signIn();

      await container.read(serverConfigProvider.notifier).clear();

      expect(container.read(serverConfigProvider).isConfigured, isFalse);
      expect(container.read(authControllerProvider), isA<AuthSignedOut>());
      expect(storage.refresh, isNull);
    });
  });

  group('cliente HTTP', () {
    test('sin URL configurada falla antes de pedir nada con un mensaje claro', () async {
      final container = ProviderContainer(
        overrides: [
          serverConfigRepositoryProvider.overrideWithValue(FakeServerConfigRepository()),
          tokenStorageProvider.overrideWithValue(FakeTokenStorage()),
        ],
      );
      addTearDown(container.dispose);

      final dio = container.read(apiClientProvider).dio;
      Object? caught;
      try {
        await dio.get<dynamic>('/api/v1/app/info');
      } catch (e) {
        caught = e;
      }

      expect(caught, isA<DioException>());
      expect((caught! as DioException).error, isA<ServerNotConfiguredException>());
      expect(describeApiError(caught), 'Aún no has configurado el servidor.');
    });

    test('el cliente toma la baseUrl de la configuración y se recrea al cambiarla', () async {
      final container = ProviderContainer(
        overrides: [
          serverConfigRepositoryProvider.overrideWithValue(
            FakeServerConfigRepository(ServerConfig(baseUrl: 'http://a.example.com')),
          ),
          tokenStorageProvider.overrideWithValue(FakeTokenStorage()),
          authRepositoryProvider.overrideWithValue(FakeAuthRepository(storage: FakeTokenStorage())),
        ],
      );
      addTearDown(container.dispose);

      expect(container.read(apiClientProvider).dio.options.baseUrl, 'http://a.example.com');
      await container.read(serverConfigProvider.notifier).setBaseUrl('http://b.example.com');
      expect(container.read(apiClientProvider).dio.options.baseUrl, 'http://b.example.com');
    });
  });

  group('huella de certificado', () {
    test('SHA-256 en hexadecimal con dos puntos y comparación tolerante', () {
      expect(
        certificateFingerprint(const []),
        'E3:B0:C4:42:98:FC:1C:14:9A:FB:F4:C8:99:6F:B9:24:'
        '27:AE:41:E4:64:9B:93:4C:A4:95:99:1B:78:52:B8:55',
      );
      expect(fingerprintsMatch('ab:cd:EF', 'ABCDef'), isTrue);
      expect(fingerprintsMatch('ab:cd:ee', 'ABCDEF'), isFalse);
    });
  });

  group('ServerProbe', () {
    ServerProbe probeWith(_ThrowingAdapter adapter) => ServerProbe(adapterFactory: () => adapter);

    test('devuelve nombre y versión', () async {
      final probe = probeWith(
        _ThrowingAdapter(status: 200, body: {'name': 'Taberna', 'version': '1.0.0'}),
      );
      final result = await probe.check('http://dnd.example.com');
      expect(result.name, 'Taberna');
      expect(result.version, '1.0.0');
    });

    Future<ServerProbeFailure> failureOf(_ThrowingAdapter adapter) async {
      try {
        await probeWith(adapter).check('http://dnd.example.com');
      } on ServerProbeException catch (e) {
        expect(e.message, isNotEmpty);
        return e.failure;
      }
      fail('Expected a ServerProbeException');
    }

    test('404 y respuestas sin forma de servidor son notAServer', () async {
      expect(
        await failureOf(_ThrowingAdapter(status: 404, body: {})),
        ServerProbeFailure.notAServer,
      );
      expect(
        await failureOf(_ThrowingAdapter(status: 200, body: {'hello': 1})),
        ServerProbeFailure.notAServer,
      );
    });

    test('un 5xx cuenta como servidor inaccesible', () async {
      expect(
        await failureOf(_ThrowingAdapter(status: 502, body: {})),
        ServerProbeFailure.unreachable,
      );
    });

    test('tiempo de espera, DNS, certificado y conexión rechazada', () async {
      DioException make(RequestOptions o, DioExceptionType type, [Object? error]) =>
          DioException(requestOptions: o, type: type, error: error);

      expect(
        await failureOf(
          _ThrowingAdapter(error: (o) => make(o, DioExceptionType.connectionTimeout)),
        ),
        ServerProbeFailure.timeout,
      );
      expect(
        await failureOf(
          _ThrowingAdapter(
            error: (o) => make(
              o,
              DioExceptionType.connectionError,
              const SocketException("Failed host lookup: 'nope.example.com'"),
            ),
          ),
        ),
        ServerProbeFailure.dns,
      );
      expect(
        await failureOf(
          _ThrowingAdapter(
            error: (o) =>
                make(o, DioExceptionType.connectionError, const HandshakeException('bad')),
          ),
        ),
        ServerProbeFailure.certificate,
      );
      expect(
        await failureOf(_ThrowingAdapter(error: (o) => make(o, DioExceptionType.badCertificate))),
        ServerProbeFailure.certificate,
      );
      expect(
        await failureOf(
          _ThrowingAdapter(
            error: (o) => make(
              o,
              DioExceptionType.connectionError,
              const SocketException('Connection refused'),
            ),
          ),
        ),
        ServerProbeFailure.unreachable,
      );
    });
  });

  group('authRedirect sin servidor', () {
    test('todo va a /server salvo /server', () {
      for (final auth in [const AuthUnknown(), const AuthSignedOut(), AuthSignedIn(makeUser())]) {
        expect(authRedirect(auth, '/', hasServer: false), AppRoutes.server);
        expect(authRedirect(auth, AppRoutes.login, hasServer: false), AppRoutes.server);
        expect(authRedirect(auth, AppRoutes.server, hasServer: false), isNull);
      }
    });

    test('con servidor, /server es accesible con y sin sesión', () {
      expect(authRedirect(const AuthSignedOut(), AppRoutes.server), isNull);
      expect(authRedirect(AuthSignedIn(makeUser()), AppRoutes.server), isNull);
      expect(authRedirect(const AuthUnknown(), AppRoutes.server), isNull);
    });
  });

  group('ServerPage', () {
    testWidgets('sin URL guardada la app arranca en la pantalla de servidor', (tester) async {
      await _pumpApp(tester, config: FakeServerConfigRepository());

      expect(find.byKey(const Key('server-url')), findsOneWidget);
      expect(find.text('Probar conexión'), findsOneWidget);
      expect(find.text('Entrar'), findsNothing);
    });

    testWidgets('con URL guardada la app arranca en el login y ofrece cambiar de servidor', (
      tester,
    ) async {
      await _pumpApp(
        tester,
        config: FakeServerConfigRepository(ServerConfig(baseUrl: testServerUrl)),
      );

      expect(find.text('Entrar'), findsOneWidget);
      expect(find.text('Cambiar servidor · $testServerUrl'), findsOneWidget);

      await tester.tap(find.byKey(const Key('login-change-server')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('server-url')), findsOneWidget);
      final field = tester.widget<TextFormField>(find.byKey(const Key('server-url')));
      expect(field.controller!.text, testServerUrl);
    });

    testWidgets('probar conexión muestra el nombre y la versión del servidor', (tester) async {
      final config = FakeServerConfigRepository();
      final probe = await _pumpApp(tester, config: config);

      await tester.enterText(find.byKey(const Key('server-url')), '192.168.1.50:8080/');
      await tester.tap(find.byKey(const Key('server-test')));
      await tester.pumpAndSettle();

      expect(find.text('Conectado a Taberna v1.2.3'), findsOneWidget);
      expect(probe.calls.single.url, 'http://192.168.1.50:8080');
    });

    testWidgets('una URL inválida muestra el error del campo y no prueba', (tester) async {
      final probe = await _pumpApp(tester, config: FakeServerConfigRepository());

      await tester.enterText(find.byKey(const Key('server-url')), 'hola mundo');
      await tester.tap(find.byKey(const Key('server-test')));
      await tester.pumpAndSettle();

      expect(find.text('La dirección no puede contener espacios.'), findsOneWidget);
      expect(probe.calls, isEmpty);
    });

    testWidgets('un error del servidor se muestra en español', (tester) async {
      final probe = FakeServerProbe(
        failure: const ServerProbeException(ServerProbeFailure.notAServer),
      );
      await _pumpApp(tester, config: FakeServerConfigRepository(), probe: probe);

      await tester.enterText(find.byKey(const Key('server-url')), 'dnd.example.com');
      await tester.tap(find.byKey(const Key('server-test')));
      await tester.pumpAndSettle();

      expect(find.text('No parece un servidor de D&D Companion.'), findsOneWidget);
      expect(find.byKey(const Key('server-trust')), findsNothing);
    });

    testWidgets('error de certificado: muestra la huella y confiar la fija y reintenta', (
      tester,
    ) async {
      final config = FakeServerConfigRepository();
      final probe = FakeServerProbe(
        result: const ServerProbeResult(name: 'Taberna', version: '1.2.3'),
        failure: const ServerProbeException(
          ServerProbeFailure.certificate,
          fingerprint: _fingerprint,
        ),
      );
      await _pumpApp(tester, config: config, probe: probe);

      await tester.enterText(find.byKey(const Key('server-url')), 'https://dnd.example.com');
      await tester.tap(find.byKey(const Key('server-test')));
      await tester.pumpAndSettle();

      expect(find.text('El certificado del servidor no es de confianza.'), findsOneWidget);
      expect(find.text(_fingerprint), findsOneWidget);

      await tester.ensureVisible(find.byKey(const Key('server-trust')));
      await tester.tap(find.byKey(const Key('server-trust')));
      await tester.pumpAndSettle();

      expect(config.stored.trustedFingerprints, {'dnd.example.com': _fingerprint});
      expect(probe.calls.last.pinned, _fingerprint);
      expect(find.text('Conectado a Taberna v1.2.3'), findsOneWidget);
    });

    testWidgets('guardar y continuar guarda la URL y lleva al login', (tester) async {
      final config = FakeServerConfigRepository();
      await _pumpApp(tester, config: config);

      await tester.enterText(find.byKey(const Key('server-url')), 'dnd.example.com');
      await tester.tap(find.byKey(const Key('server-save')));
      await tester.pumpAndSettle();

      expect(config.stored.baseUrl, 'http://dnd.example.com');
      expect(config.stored.recentUrls, ['http://dnd.example.com']);
      expect(find.text('Entrar'), findsOneWidget);
    });

    testWidgets('la lista de recientes selecciona al tocar y borra al deslizar', (tester) async {
      final config = FakeServerConfigRepository(
        ServerConfig(
          baseUrl: 'http://a.example.com',
          recentUrls: ['http://a.example.com', 'http://b.example.com'],
        ),
      );
      await _pumpApp(tester, config: config);
      await tester.tap(find.byKey(const Key('login-change-server')));
      await tester.pumpAndSettle();

      expect(find.text('Servidores recientes'), findsOneWidget);

      final recentB = find.byKey(const Key('server-recent-http://b.example.com'));
      await tester.tap(recentB);
      await tester.pumpAndSettle();
      final field = tester.widget<TextFormField>(find.byKey(const Key('server-url')));
      expect(field.controller!.text, 'http://b.example.com');

      await tester.drag(recentB, const Offset(-600, 0));
      await tester.pumpAndSettle();
      expect(recentB, findsNothing);
      expect(config.stored.recentUrls, ['http://a.example.com']);
    });

    testWidgets('cambiar de servidor desde el inicio cierra la sesión y lleva al login', (
      tester,
    ) async {
      final storage = FakeTokenStorage();
      final config = FakeServerConfigRepository(ServerConfig(baseUrl: 'http://a.example.com'));
      final repository = FakeAuthRepository(storage: storage, loginUser: makeUser());
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            serverConfigRepositoryProvider.overrideWithValue(config),
            tokenStorageProvider.overrideWithValue(storage),
            authRepositoryProvider.overrideWithValue(repository),
            fakeServerInfoOverride,
            fakeCampaignsOverride,
          ],
          child: const DndCompanionApp(),
        ),
      );
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('login-email')), 'user@example.com');
      await tester.enterText(find.byKey(const Key('login-password')), 'change-me-123');
      await tester.tap(find.text('Entrar'));
      await tester.pumpAndSettle();
      expect(find.text('Servidor: http://a.example.com'), findsOneWidget);

      await tester.tap(find.byKey(const Key('home-user-menu')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('home-server')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('server-url')), 'http://b.example.com');
      await tester.tap(find.byKey(const Key('server-save')));
      await tester.pumpAndSettle();

      expect(find.text('Entrar'), findsOneWidget);
      expect(storage.refresh, isNull);
      expect(repository.logoutCalls, 0);
      expect(config.stored.baseUrl, 'http://b.example.com');
    });
  });
}
