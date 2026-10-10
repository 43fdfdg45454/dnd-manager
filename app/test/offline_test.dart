import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:opentrpg/core/auth/auth_controller.dart';
import 'package:opentrpg/core/auth/auth_repository.dart';
import 'package:opentrpg/core/auth/auth_state.dart';
import 'package:opentrpg/core/auth/cached_user_store.dart';
import 'package:opentrpg/core/auth/token_storage.dart';
import 'package:opentrpg/core/cache/cache_database.dart';
import 'package:opentrpg/core/cache/cached_result.dart';
import 'package:opentrpg/core/cache/response_cache.dart';
import 'package:opentrpg/core/cache/stale_data.dart';
import 'package:opentrpg/core/files/file_disk_cache.dart';
import 'package:opentrpg/core/network/api_client.dart';
import 'package:opentrpg/core/network/connectivity.dart';
import 'package:opentrpg/core/server/server_config_controller.dart';
import 'package:opentrpg/core/storage/local_preferences.dart';
import 'package:opentrpg/core/ui/offline_widgets.dart';
import 'package:opentrpg/features/campaigns/data/campaigns_repository.dart';
import 'package:opentrpg/features/campaigns/ui/campaigns_page.dart';
import 'package:opentrpg/features/home/ui/home_page.dart';
import 'package:opentrpg/features/home/ui/profile_page.dart';
import 'package:opentrpg/features/library/data/library_controllers.dart';
import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'helpers/fakes.dart';
import 'helpers/session_fakes.dart';

/// Answers every request with [body] (JSON) and [status], or fails as if
/// there were no connection while [offline] is set.
class _ScriptedAdapter implements HttpClientAdapter {
  bool offline = false;
  int status = 200;
  Object? body = const <String, dynamic>{};
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    if (offline) {
      throw DioException.connectionError(requestOptions: options, reason: 'sin red');
    }
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Map<String, dynamic> _campaignJson({String id = 'c1', String name = 'La Mina Perdida'}) => {
  'id': id,
  'name': name,
  'description': '',
  'ownerId': 'u1',
  'ownerDisplayName': 'Usuario Demo',
  'myRole': 'Owner',
  'memberCount': 2,
  'createdAt': '2026-01-01T00:00:00Z',
};

ApiClient _client(
  _ScriptedAdapter adapter,
  ResponseCache cache, {
  FreshnessReporter? onFreshness,
}) => ApiClient(
  baseUrl: testServerUrl,
  dio: Dio(BaseOptions(baseUrl: testServerUrl))..httpClientAdapter = adapter,
  cache: cache,
  onFreshness: onFreshness,
);

/// Lets the microtasks and pending futures of the providers run.
Future<void> _settle() async {
  for (var i = 0; i < 5; i++) {
    await Future<void>.delayed(Duration.zero);
  }
}

void main() {
  group('clave de caché', () {
    test('ordena la query y descarta los nulos', () {
      expect(responseCacheKey('/api/v1/campaigns'), '/api/v1/campaigns');
      expect(
        responseCacheKey('/api/v1/systems/dnd5e/catalog/spells', {
          'search': 'bola fuego',
          'level': 3,
          'x': null,
        }),
        '/api/v1/systems/dnd5e/catalog/spells?level=3&search=bola+fuego',
      );
      expect(
        responseCacheKey('/a', {'b': 1, 'a': true}),
        responseCacheKey('/a', {'a': true, 'b': 1}),
      );
    });
  });

  group('getCached', () {
    late _ScriptedAdapter adapter;
    late InMemoryResponseCache cache;
    late List<(String, DateTime?)> reports;
    late ApiClient client;

    setUp(() {
      adapter = _ScriptedAdapter();
      cache = InMemoryResponseCache();
      reports = [];
      client = _client(adapter, cache, onFreshness: (key, since) => reports.add((key, since)));
    });

    test('con red devuelve datos frescos y los guarda', () async {
      adapter.body = [_campaignJson()];
      final result = await client.getCached('/api/v1/campaigns', parse: (json) => json as List);

      expect(result.isStale, isFalse);
      expect(result.data, hasLength(1));
      expect(cache.entries.keys, ['/api/v1/campaigns']);
      expect(reports.single, ('/api/v1/campaigns', null));
    });

    test('sin red devuelve lo guardado marcado como obsoleto', () async {
      final fetchedAt = DateTime.utc(2026, 10, 1, 18);
      await cache.write('/api/v1/campaigns', jsonEncode([_campaignJson()]), fetchedAt);
      adapter.offline = true;

      final result = await client.getCached('/api/v1/campaigns', parse: (json) => json as List);

      expect(result.isStale, isTrue);
      expect(result.fetchedAt, fetchedAt);
      expect(reports.single, ('/api/v1/campaigns', fetchedAt));
    });

    test('sin red y sin caché propaga el error', () async {
      adapter.offline = true;
      await expectLater(
        client.getCached('/api/v1/campaigns', parse: (json) => json),
        throwsA(
          isA<DioException>().having((e) => e.type, 'type', DioExceptionType.connectionError),
        ),
      );
      expect(reports, isEmpty);
    });

    test('un error del servidor no usa la caché', () async {
      await cache.write('/api/v1/campaigns/c1', jsonEncode(_campaignJson()), DateTime.utc(2026));
      adapter
        ..status = 404
        ..body = {'title': 'Not found'};
      await expectLater(
        client.getCached('/api/v1/campaigns/c1', parse: (json) => json),
        throwsA(isA<DioException>().having((e) => e.response?.statusCode, 'status', 404)),
      );
    });

    test('la query forma parte de la clave', () async {
      adapter.body = {'items': [], 'total': 0, 'page': 1, 'pageSize': 50};
      await client.getCached(
        '/api/v1/systems/dnd5e/catalog/spells',
        query: {'search': 'luz', 'page': 1, 'level': null},
        parse: (json) => json,
      );
      expect(cache.entries.keys, ['/api/v1/systems/dnd5e/catalog/spells?page=1&search=luz']);
      expect(adapter.requests.single.queryParameters, {'search': 'luz', 'page': 1});
    });
  });

  group('repositorio cacheado', () {
    test('devuelve el valor guardado cuando la red falla y marca isStale', () async {
      final adapter = _ScriptedAdapter()..body = [_campaignJson(name: 'Guardada')];
      final container = ProviderContainer();
      addTearDown(container.dispose);
      final repository = CampaignsRepository(
        _client(
          adapter,
          InMemoryResponseCache(),
          onFreshness: (key, since) =>
              container.read(staleDataProvider.notifier).report(key, since),
        ),
      );
      final scope = staleExact(CampaignsRepository.listPath);

      expect((await repository.list()).single.name, 'Guardada');
      expect(container.read(staleSinceProvider(scope)), isNull);

      adapter.offline = true;
      final offline = await repository.list();

      expect(offline.single.name, 'Guardada');
      expect(container.read(staleSinceProvider(scope)), isNotNull);
    });

    test('sin caché propaga el error de red', () async {
      final adapter = _ScriptedAdapter()..offline = true;
      final repository = CampaignsRepository(_client(adapter, InMemoryResponseCache()));

      await expectLater(repository.get('c1'), throwsA(isA<DioException>()));
    });

    test('una lectura con red quita la marca de obsoleto', () async {
      final container = ProviderContainer();
      addTearDown(container.dispose);
      final stale = container.read(staleDataProvider.notifier);
      final at = DateTime.utc(2026, 10, 1);
      stale.report('/api/v1/campaigns/c1', at);
      stale.report('/api/v1/campaigns/c1/lore', at.subtract(const Duration(hours: 1)));
      stale.report('/api/v1/campaigns', at);

      // The tree of a campaign covers its sub-resources; the list is exact.
      expect(
        container.read(staleSinceProvider(staleTree('/api/v1/campaigns/c1'))),
        at.subtract(const Duration(hours: 1)),
      );
      expect(container.read(staleSinceProvider(staleTree('/api/v1/campaigns/c2'))), isNull);
      expect(container.read(staleSinceProvider(staleExact('/api/v1/campaigns'))), at);

      stale.report('/api/v1/campaigns', null);
      expect(container.read(staleSinceProvider(staleExact('/api/v1/campaigns'))), isNull);
    });
  });

  group('ResponseCache en SQLite', () {
    test('lee, escribe, vacía por prefijo y entero', () async {
      final cache = DriftResponseCache(CacheDatabase(NativeDatabase.memory()), maxEntries: 2);
      addTearDown(cache.close);
      final at = DateTime.utc(2026, 10, 1, 12);

      await cache.write('/api/v1/campaigns/c1', '{"a":1}', at);
      await cache.write('/api/v1/campaigns/c1', '{"a":2}', at.add(const Duration(minutes: 1)));
      await cache.write('/api/v1/campaigns/C1/lore', '[]', at);
      await cache.write('/api/v1/systems/dnd5e/catalog/spells?level=1', '{}', at);

      final entry = await cache.read('/api/v1/campaigns/c1');
      expect(entry!.body, '{"a":2}');
      expect(entry.fetchedAt, at.add(const Duration(minutes: 1)));

      // Prefixes are case-sensitive and have no wildcards.
      await cache.clearPrefix('/api/v1/campaigns/c');
      expect(await cache.read('/api/v1/campaigns/c1'), isNull);
      expect(await cache.read('/api/v1/campaigns/C1/lore'), isNotNull);

      await cache.write('/x', '1', at.subtract(const Duration(days: 1)));
      await cache.prune();
      expect(await cache.read('/x'), isNull);
      expect(await cache.read('/api/v1/systems/dnd5e/catalog/spells?level=1'), isNotNull);

      await cache.clear();
      expect(await cache.read('/api/v1/systems/dnd5e/catalog/spells?level=1'), isNull);
    });
  });

  group('banner sin conexión', () {
    test('texto con la antigüedad de los datos', () {
      final now = DateTime(2026, 10, 5, 12);
      expect(offlineBannerText(now, now), 'Sin conexión · datos de hace un momento');
      expect(describeDataAge(now.subtract(const Duration(minutes: 5)), now), 'hace 5 min');
      expect(describeDataAge(now.subtract(const Duration(hours: 3)), now), 'hace 3 h');
      expect(describeDataAge(now.subtract(const Duration(days: 1)), now), 'hace 1 día');
      expect(describeDataAge(now.subtract(const Duration(days: 4)), now), 'hace 4 días');
    });

    testWidgets('el inicio muestra las campañas guardadas y el aviso cuando no hay red', (
      tester,
    ) async {
      final adapter = _ScriptedAdapter()..offline = true;
      final cache = InMemoryResponseCache();
      await cache.write(
        '/api/v1/campaigns',
        jsonEncode([_campaignJson(name: 'Campaña sin red')]),
        DateTime.now().toUtc().subtract(const Duration(hours: 2)),
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authControllerProvider.overrideWith(
              () => FixedAuthController(AuthSignedIn(makeUser())),
            ),
            fakeServerConfigOverride(),
            fakeServerInfoOverride,
            sessionsOverride(FakeSessionsRepository()),
            apiClientProvider.overrideWith(
              (ref) => _client(
                adapter,
                cache,
                onFreshness: (key, since) =>
                    ref.read(staleDataProvider.notifier).report(key, since),
              ),
            ),
          ],
          child: const MaterialApp(home: HomePage()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Campaña sin red'), findsOneWidget);
      expect(find.byKey(const Key('offline-banner')), findsOneWidget);
      expect(find.text('Sin conexión · datos de hace 2 h'), findsOneWidget);
    });

    testWidgets('no aparece con datos frescos', (tester) async {
      final adapter = _ScriptedAdapter()..body = [_campaignJson(name: 'Con red')];
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authControllerProvider.overrideWith(
              () => FixedAuthController(AuthSignedIn(makeUser())),
            ),
            fakeServerConfigOverride(),
            fakeServerInfoOverride,
            sessionsOverride(FakeSessionsRepository()),
            apiClientProvider.overrideWith(
              (ref) => _client(
                adapter,
                InMemoryResponseCache(),
                onFreshness: (key, since) =>
                    ref.read(staleDataProvider.notifier).report(key, since),
              ),
            ),
          ],
          child: const MaterialApp(home: HomePage()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Con red'), findsOneWidget);
      expect(find.byKey(const Key('offline-banner')), findsNothing);
    });
  });

  group('escrituras sin conexión', () {
    testWidgets('el botón de nueva campaña se deshabilita con el tooltip', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            fakeCampaignsOverride,
            connectivitySourceProvider.overrideWithValue(const _FixedSource(false)),
          ],
          child: const MaterialApp(home: CampaignsPage()),
        ),
      );
      await tester.pumpAndSettle();

      final fab = tester.widget<FloatingActionButton>(find.byKey(const Key('campaigns-new')));
      expect(fab.onPressed, isNull);
      expect(fab.tooltip, needsConnectionMessage);

      await tester.tap(find.byKey(const Key('campaigns-new')));
      await tester.pumpAndSettle();
      expect(find.text('Nueva campaña'), findsOneWidget); // No dialog opened.
    });

    test('un fallo de red deshabilita las escrituras y un acierto las rehabilita', () {
      final container = ProviderContainer();
      addTearDown(container.dispose);

      expect(container.read(canWriteProvider), isTrue);
      container.read(connectivityProvider.notifier).reportRequestFailed();
      expect(container.read(canWriteProvider), isFalse);
      container.read(connectivityProvider.notifier).reportRequestSucceeded();
      expect(container.read(canWriteProvider), isTrue);
    });

    test('el cliente informa del resultado de cualquier petición', () async {
      final adapter = _ScriptedAdapter()..offline = true;
      final container = ProviderContainer(
        overrides: [
          fakeServerConfigOverride(),
          tokenStorageProvider.overrideWithValue(FakeTokenStorage()),
        ],
      );
      addTearDown(container.dispose);
      final dio = container.read(apiClientProvider).dio..httpClientAdapter = adapter;

      await expectLater(dio.get<void>('/api/v1/campaigns'), throwsA(isA<DioException>()));
      expect(container.read(connectivityProvider).isOffline, isTrue);
      // The failure drops the connection pool: the client has a new adapter.
      expect(dio.httpClientAdapter, isNot(same(adapter)));

      adapter.offline = false;
      dio.httpClientAdapter = adapter;
      await dio.get<Object?>('/api/v1/campaigns');
      expect(container.read(connectivityProvider).isOffline, isFalse);
    });
  });

  group('sesión sin red', () {
    late SharedPreferences prefs;

    setUp(() async {
      SharedPreferences.setMockInitialValues({
        CachedUserStore.key: jsonEncode(makeUser(displayName: 'Guardado').toJson()),
      });
      prefs = await SharedPreferences.getInstance();
    });

    ProviderContainer open(FakeAuthRepository repository, FakeTokenStorage storage) {
      final container = ProviderContainer(
        overrides: [
          tokenStorageProvider.overrideWithValue(storage),
          authRepositoryProvider.overrideWithValue(repository),
          localPreferencesProvider.overrideWithValue(prefs),
        ],
      );
      addTearDown(container.dispose);
      container.listen(authControllerProvider, (_, _) {});
      return container;
    }

    test('si /auth/me falla por red entra con el usuario guardado', () async {
      final storage = FakeTokenStorage()..refresh = 'refresh-1';
      final repository = FakeAuthRepository(storage: storage, meError: dioError(null));
      final container = open(repository, storage);
      await _settle();

      final state = container.read(authControllerProvider);
      expect(state, isA<AuthSignedIn>());
      expect((state as AuthSignedIn).user.displayName, 'Guardado');
      expect(state.isOffline, isTrue);
      expect(container.read(connectivityProvider).isOffline, isTrue);
      expect(container.read(canWriteProvider), isFalse);
    });

    test('al volver la red confirma la sesión con el servidor', () async {
      final storage = FakeTokenStorage()..refresh = 'refresh-1';
      final repository = FakeAuthRepository(storage: storage, meError: dioError(null));
      final container = open(repository, storage);
      await _settle();

      repository
        ..meError = null
        ..meUser = makeUser(displayName: 'Actualizado');
      container.read(connectivityProvider.notifier).reportRequestSucceeded();
      await _settle();

      final state = container.read(authControllerProvider) as AuthSignedIn;
      expect(state.isOffline, isFalse);
      expect(state.user.displayName, 'Actualizado');
      expect(container.read(cachedUserStoreProvider).read()!.displayName, 'Actualizado');
    });

    test('un 401 no usa el usuario guardado', () async {
      final storage = FakeTokenStorage()..refresh = 'refresh-1';
      final repository = FakeAuthRepository(storage: storage, meError: dioError(401));
      final container = open(repository, storage);
      await _settle();

      expect(container.read(authControllerProvider), isA<AuthSignedOut>());
    });

    test('sin usuario guardado vuelve al login', () async {
      await prefs.remove(CachedUserStore.key);
      final storage = FakeTokenStorage()..refresh = 'refresh-1';
      final repository = FakeAuthRepository(storage: storage, meError: dioError(null));
      final container = open(repository, storage);
      await _settle();

      expect(container.read(authControllerProvider), isA<AuthSignedOut>());
    });

    test('login guarda el usuario y logout lo olvida junto con la caché', () async {
      await prefs.remove(CachedUserStore.key);
      final storage = FakeTokenStorage();
      final repository = FakeAuthRepository(storage: storage, loginUser: makeUser());
      final cache = InMemoryResponseCache();
      await cache.write('/api/v1/campaigns', '[]', DateTime.utc(2026));
      final container = ProviderContainer(
        overrides: [
          tokenStorageProvider.overrideWithValue(storage),
          authRepositoryProvider.overrideWithValue(repository),
          localPreferencesProvider.overrideWithValue(prefs),
          responseCacheProvider.overrideWithValue(cache),
          fileDiskCacheProvider.overrideWithValue(_FakeDiskCache()),
        ],
      );
      addTearDown(container.dispose);
      container.listen(authControllerProvider, (_, _) {});
      await _settle();

      await container.read(authControllerProvider.notifier).login('user@example.com', 'x');
      expect(prefs.getString(CachedUserStore.key), isNotNull);

      await container.read(authControllerProvider.notifier).logout();
      await _settle();
      expect(prefs.getString(CachedUserStore.key), isNull);
      expect(cache.entries, isEmpty);
    });
  });

  group('biblioteca', () {
    test('la lista guardada se marca sin conexión', () async {
      final adapter = _ScriptedAdapter()
        ..body = [
          {'id': 'd1', 'title': 'Reglas básicas', 'category': 'Rules', 'fileId': 'f1'},
        ];
      final container = ProviderContainer(
        overrides: [
          apiClientProvider.overrideWith(
            (ref) => _client(
              adapter,
              InMemoryResponseCache(),
              onFreshness: (key, since) => ref.read(staleDataProvider.notifier).report(key, since),
            ),
          ),
        ],
      );
      addTearDown(container.dispose);
      container.listen(libraryControllerProvider, (_, _) {});

      final online = await container.read(libraryControllerProvider.future);
      expect(online.offline, isFalse);

      adapter.offline = true;
      await container.read(libraryControllerProvider.notifier).reload();
      final offline = container.read(libraryControllerProvider).requireValue;
      expect(offline.offline, isTrue);
      expect(offline.documents.single.title, 'Reglas básicas');
    });
  });

  group('vaciar caché', () {
    testWidgets('el menú de usuario vacía la caché tras confirmar', (tester) async {
      final cache = InMemoryResponseCache();
      await cache.write('/api/v1/campaigns', '[]', DateTime.utc(2026));
      final files = _FakeDiskCache();
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authControllerProvider.overrideWith(
              () => FixedAuthController(AuthSignedIn(makeUser())),
            ),
            fakeCampaignsOverride,
            fakeServerConfigOverride(),
            fakeServerInfoOverride,
            sessionsOverride(FakeSessionsRepository()),
            responseCacheProvider.overrideWithValue(cache),
            fileDiskCacheProvider.overrideWithValue(files),
          ],
          child: const MaterialApp(home: ProfilePage()),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('home-clear-cache')));
      await tester.pumpAndSettle();
      expect(find.text('Vaciar caché'), findsNWidgets(2)); // Tile and confirmation title.
      await tester.tap(find.byKey(const Key('confirm-action')));
      await tester.pumpAndSettle();

      expect(cache.entries, isEmpty);
      expect(files.cleared, isTrue);
      expect(find.text('Caché vaciada.'), findsOneWidget);
    });

    test('cambiar de servidor vacía la caché', () async {
      final cache = InMemoryResponseCache();
      await cache.write('/api/v1/campaigns', '[]', DateTime.utc(2026));
      final files = _FakeDiskCache();
      final storage = FakeTokenStorage()..refresh = 'refresh-1';
      final container = ProviderContainer(
        overrides: [
          fakeServerConfigOverride(),
          tokenStorageProvider.overrideWithValue(storage),
          authRepositoryProvider.overrideWithValue(
            FakeAuthRepository(storage: storage, meUser: makeUser()),
          ),
          responseCacheProvider.overrideWithValue(cache),
          fileDiskCacheProvider.overrideWithValue(files),
        ],
      );
      addTearDown(container.dispose);
      container.listen(authControllerProvider, (_, _) {});
      await _settle();
      container.read(staleDataProvider.notifier).report('/api/v1/campaigns', DateTime.utc(2026));

      await container.read(serverConfigProvider.notifier).setBaseUrl('http://otro.example.com');
      await _settle();

      expect(cache.entries, isEmpty);
      expect(files.cleared, isTrue);
      expect(container.read(staleDataProvider), isEmpty);
      expect(container.read(authControllerProvider), isA<AuthSignedOut>());
    });
  });

  group('CachedResult', () {
    test('parsers de objetos y listas', () {
      final one = parseObject((json) => json['id'] as String)({'id': 'a'});
      final many = parseList((json) => json['id'] as String)([
        {'id': 'a'},
        {'id': 'b'},
      ]);
      expect(one, 'a');
      expect(many, ['a', 'b']);
    });
  });
}

class _FakeDiskCache extends FileDiskCache {
  bool cleared = false;

  @override
  Future<void> clear() async => cleared = true;
}

class _FixedSource implements ConnectivitySource {
  const _FixedSource(this.connected);

  final bool connected;

  @override
  Future<NetworkInterfaces> current() async =>
      connected ? const NetworkInterfaces({NetworkKind.wifi}) : const NetworkInterfaces.none();

  @override
  Stream<NetworkInterfaces> get changes => const Stream.empty();
}
