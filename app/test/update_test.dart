import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:dnd_companion/app.dart';
import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/network/api_client.dart';
import 'package:dnd_companion/core/server/server_config.dart';
import 'package:dnd_companion/core/server/server_config_repository.dart';
import 'package:dnd_companion/core/storage/local_preferences.dart';
import 'package:dnd_companion/core/update/app_release.dart';
import 'package:dnd_companion/core/update/app_update_repository.dart';
import 'package:dnd_companion/core/update/update_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'helpers/fakes.dart';
import 'helpers/session_fakes.dart';

AppRelease _release({
  int build = 2,
  bool mandatory = false,
  String notes = 'Mejoras en el mapa.',
}) => AppRelease(
  version: '1.$build.0',
  buildNumber: build,
  notes: notes,
  isMandatory: mandatory,
  downloadUrl: '$testServerUrl/api/v1/app/download/$build',
  sizeBytes: 25 * 1024 * 1024,
  publishedAt: DateTime.utc(2026, 10, 1),
);

class _FakeUpdateRepository implements AppUpdateRepository {
  _FakeUpdateRepository([this.release]);

  AppRelease? release;
  Object? error;
  int calls = 0;

  @override
  Future<AppRelease?> latest() async {
    calls++;
    if (error != null) throw error!;
    return release;
  }
}

class _JsonAdapter implements HttpClientAdapter {
  _JsonAdapter(this.status, [this.body]);

  final int status;
  final Object? body;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async => ResponseBody.fromString(
    body == null ? '' : jsonEncode(body),
    status,
    headers: {
      Headers.contentTypeHeader: ['application/json'],
    },
  );

  @override
  void close({bool force = false}) {}
}

List<Override> _baseOverrides(
  _FakeUpdateRepository repository, {
  int? installed = 1,
  SharedPreferences? prefs,
  DateTime Function()? clock,
  List<Uri>? opened,
}) => [
  fakeServerConfigOverride(),
  appUpdateRepositoryProvider.overrideWithValue(repository),
  installedBuildProvider.overrideWithValue(installed),
  if (prefs != null) localPreferencesProvider.overrideWithValue(prefs),
  if (clock != null) updateClockProvider.overrideWithValue(clock),
  if (opened != null)
    urlOpenerProvider.overrideWithValue((url) async {
      opened.add(url);
      return true;
    }),
];

Future<void> _pumpApp(WidgetTester tester, List<Override> overrides) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        ...overrides,
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
        fakeCampaignsOverride,
        fakeServerInfoOverride,
        sessionsOverride(FakeSessionsRepository()),
      ],
      child: const DndCompanionApp(),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('AppUpdateRepository', () {
    ApiClient client(HttpClientAdapter adapter) => ApiClient(
      baseUrl: testServerUrl,
      dio: Dio(BaseOptions(baseUrl: testServerUrl))..httpClientAdapter = adapter,
    );

    test('convierte la URL de descarga en absoluta', () async {
      final repository = AppUpdateRepository(
        client(
          _JsonAdapter(200, {
            'version': '1.2.0',
            'buildNumber': 12,
            'notes': '',
            'isMandatory': true,
            'downloadUrl': '/api/v1/app/download/12',
            'sizeBytes': 1000,
            'publishedAt': '2026-10-01T10:00:00+00:00',
          }),
        ),
      );

      final release = await repository.latest();

      expect(release!.buildNumber, 12);
      expect(release.isMandatory, isTrue);
      expect(release.downloadUrl, '$testServerUrl/api/v1/app/download/12');
    });

    test('204 significa que no hay ninguna publicada', () async {
      final repository = AppUpdateRepository(client(_JsonAdapter(204)));
      expect(await repository.latest(), isNull);
    });

    test('respeta el prefijo de ruta del servidor', () {
      final withPrefix = ApiClient(baseUrl: 'https://dnd.example.com/dnd/');
      expect(
        withPrefix.absoluteUrl('/api/v1/app/download/3'),
        'https://dnd.example.com/dnd/api/v1/app/download/3',
      );
      expect(
        withPrefix.absoluteUrl('https://otro.example.com/a.apk'),
        'https://otro.example.com/a.apk',
      );
    });
  });

  group('UpdateController', () {
    late SharedPreferences prefs;
    late DateTime now;

    setUp(() async {
      SharedPreferences.setMockInitialValues({});
      prefs = await SharedPreferences.getInstance();
      now = DateTime(2026, 10, 5, 12);
    });

    ProviderContainer open(_FakeUpdateRepository repository, {int? installed = 1}) {
      final container = ProviderContainer(
        overrides: _baseOverrides(repository, installed: installed, prefs: prefs, clock: () => now),
      );
      addTearDown(container.dispose);
      return container;
    }

    test('hay actualización cuando el build remoto es mayor', () async {
      final container = open(_FakeUpdateRepository(_release(build: 2)));
      expect(
        await container.read(updateControllerProvider.notifier).check(),
        UpdateCheckOutcome.available,
      );
      expect(container.read(updateControllerProvider)!.buildNumber, 2);
    });

    test('no la hay cuando el build es el mismo o no hay ninguna', () async {
      final repository = _FakeUpdateRepository(_release(build: 1));
      final container = open(repository);
      final controller = container.read(updateControllerProvider.notifier);

      expect(await controller.check(), UpdateCheckOutcome.upToDate);
      expect(container.read(updateControllerProvider), isNull);

      repository.release = null;
      expect(await controller.check(force: true), UpdateCheckOutcome.upToDate);
    });

    test('comprueba como mucho una vez al día salvo si se fuerza', () async {
      final repository = _FakeUpdateRepository(_release(build: 1));
      final container = open(repository);
      final controller = container.read(updateControllerProvider.notifier);

      await controller.check();
      now = now.add(const Duration(hours: 5));
      expect(await controller.check(), UpdateCheckOutcome.skipped);
      expect(repository.calls, 1);

      expect(await controller.check(force: true), UpdateCheckOutcome.upToDate);
      expect(repository.calls, 2);

      now = now.add(const Duration(days: 1));
      await controller.check();
      expect(repository.calls, 3);
    });

    test('sin red es silencioso y vuelve a intentarlo en el siguiente arranque', () async {
      final repository = _FakeUpdateRepository()..error = dioError(null);
      final container = open(repository);
      final controller = container.read(updateControllerProvider.notifier);

      expect(await controller.check(), UpdateCheckOutcome.failed);
      expect(prefs.getInt(UpdateController.lastCheckKey), isNull);
      expect(container.read(updateControllerProvider), isNull);
    });

    test('sin build instalado conocido no comprueba nada', () async {
      final repository = _FakeUpdateRepository(_release());
      final container = open(repository, installed: null);
      expect(
        await container.read(updateControllerProvider.notifier).check(force: true),
        UpdateCheckOutcome.skipped,
      );
      expect(repository.calls, 0);
    });

    test('una obligatoria sigue pendiente tras reiniciar sin volver a preguntar', () async {
      final first = open(_FakeUpdateRepository(_release(build: 3, mandatory: true)));
      await first.read(updateControllerProvider.notifier).check();

      final restarted = open(_FakeUpdateRepository());
      expect(restarted.read(updateControllerProvider)?.isMandatory, isTrue);

      // Installed already: nothing pending.
      final updated = open(_FakeUpdateRepository(), installed: 3);
      expect(updated.read(updateControllerProvider), isNull);
    });

    test('lo guardado de otro servidor no cuenta', () async {
      await prefs.setString(
        UpdateController.latestKey,
        jsonEncode({'server': 'http://otro.example.com', ..._release(mandatory: true).toJson()}),
      );
      final container = open(_FakeUpdateRepository());
      expect(container.read(updateControllerProvider), isNull);
    });

    test('formato del tamaño', () {
      expect(formatReleaseSize(25 * 1024 * 1024), '25,0 MB');
      expect(formatReleaseSize(1500), '2 KB');
      expect(formatReleaseSize(0), '');
    });
  });

  group('diálogo de actualización', () {
    testWidgets('aparece al arrancar si el build remoto es mayor y descarga', (tester) async {
      final opened = <Uri>[];
      await _pumpApp(tester, _baseOverrides(_FakeUpdateRepository(_release()), opened: opened));

      expect(find.byKey(const Key('update-dialog')), findsOneWidget);
      expect(find.text('Nueva versión disponible'), findsOneWidget);
      expect(find.text('Versión 1.2.0 · 25,0 MB'), findsOneWidget);
      expect(find.text('Mejoras en el mapa.'), findsOneWidget);

      await tester.tap(find.byKey(const Key('update-download')));
      await tester.pumpAndSettle();

      expect(opened, [Uri.parse('$testServerUrl/api/v1/app/download/2')]);
      expect(find.byKey(const Key('update-dialog')), findsNothing);
      expect(find.byKey(const Key('app-nav-bar')), findsOneWidget);
    });

    testWidgets('no aparece si el build es el mismo', (tester) async {
      await _pumpApp(tester, _baseOverrides(_FakeUpdateRepository(_release(build: 1))));

      expect(find.byKey(const Key('update-dialog')), findsNothing);
      expect(find.byKey(const Key('app-nav-bar')), findsOneWidget);
    });

    testWidgets('"Más tarde" lo cierra y deja usar la app', (tester) async {
      await _pumpApp(tester, _baseOverrides(_FakeUpdateRepository(_release())));

      await tester.tap(find.byKey(const Key('update-later')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('update-dialog')), findsNothing);
      expect(find.byKey(const Key('app-nav-bar')), findsOneWidget);
    });

    testWidgets('isMandatory bloquea la app hasta actualizar', (tester) async {
      final opened = <Uri>[];
      final repository = _FakeUpdateRepository(_release(build: 5, mandatory: true));
      await _pumpApp(tester, _baseOverrides(repository, opened: opened));

      expect(find.byKey(const Key('update-required')), findsOneWidget);
      expect(find.text('Actualización obligatoria'), findsOneWidget);
      expect(find.byKey(const Key('app-nav-bar')), findsNothing);
      expect(find.byKey(const Key('update-dialog')), findsNothing);

      // The system back button cannot leave the page.
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('update-required')), findsOneWidget);

      await tester.tap(find.byKey(const Key('update-required-download')));
      await tester.pumpAndSettle();
      expect(opened.single.path, '/api/v1/app/download/5');

      // Once the server no longer requires it, the app is usable again.
      repository.release = _release(build: 1);
      await tester.tap(find.byKey(const Key('update-required-recheck')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('update-required')), findsNothing);
      expect(find.byKey(const Key('app-nav-bar')), findsOneWidget);
    });

    testWidgets('"Buscar actualizaciones" fuerza la comprobación', (tester) async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();
      final repository = _FakeUpdateRepository(_release(build: 1));
      await _pumpApp(tester, _baseOverrides(repository, prefs: prefs));
      expect(repository.calls, 1);

      Future<void> checkFromMenu() async {
        await tester.tap(find.byKey(const Key('nav-profile')));
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const Key('home-check-updates')));
        await tester.pumpAndSettle();
      }

      await checkFromMenu();
      expect(repository.calls, 2);
      expect(find.text('Tienes la última versión.'), findsOneWidget);

      repository.release = _release(build: 4);
      await checkFromMenu();
      expect(find.byKey(const Key('update-dialog')), findsOneWidget);
    });

    testWidgets('sin red al arrancar no muestra nada', (tester) async {
      final repository = _FakeUpdateRepository()..error = dioError(null);
      await _pumpApp(tester, _baseOverrides(repository));

      expect(repository.calls, 1);
      expect(find.byKey(const Key('update-dialog')), findsNothing);
      expect(find.byType(SnackBar), findsNothing);
    });

    testWidgets('sin servidor configurado no comprueba', (tester) async {
      final repository = _FakeUpdateRepository(_release());
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            serverConfigRepositoryProvider.overrideWithValue(
              FakeServerConfigRepository(ServerConfig()),
            ),
            appUpdateRepositoryProvider.overrideWithValue(repository),
            installedBuildProvider.overrideWithValue(1),
            fakeServerInfoOverride,
          ],
          child: const DndCompanionApp(),
        ),
      );
      await tester.pumpAndSettle();

      expect(repository.calls, 0);
      expect(find.byKey(const Key('update-dialog')), findsNothing);
    });
  });
}
