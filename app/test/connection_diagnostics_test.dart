import 'package:dio/dio.dart';
import 'package:dnd_companion/core/realtime/connection_diagnostics.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/app_pump.dart';
import 'helpers/fake_realtime_hub.dart';

DiagnosticAction _ok(String detail) =>
    () async => detail;

DiagnosticAction _fail(Object error) =>
    () async => throw error;

DioException _http(int status) => DioException(
  requestOptions: RequestOptions(path: '/x'),
  response: Response(
    requestOptions: RequestOptions(path: '/x'),
    statusCode: status,
  ),
  type: DioExceptionType.badResponse,
);

ConnectionDiagnostics _diagnostics({
  DiagnosticAction? health,
  DiagnosticAction? session,
  DiagnosticAction? negotiate,
  DiagnosticAction? hub,
  bool signedIn = true,
}) => ConnectionDiagnostics(
  health: health ?? _ok('HTTP 200'),
  session: session ?? _ok('Sesión válida'),
  negotiate: negotiate ?? _ok('transportes: WebSockets, ServerSentEvents, LongPolling'),
  hub: hub ?? _ok('WebSockets'),
  isSignedIn: () => signedIn,
);

void main() {
  group('ConnectionDiagnostics', () {
    test('todo bien: cuatro pasos correctos en orden y sin avisos', () async {
      final progress = <int>[];
      final results = await _diagnostics().run(onProgress: (r) => progress.add(r.length));

      expect([for (final r in results) r.step], DiagnosticStep.values);
      expect(results.every((r) => r.ok), isTrue);
      expect(results.every((r) => r.cause == null), isTrue);
      expect(results[2].detail, contains('WebSockets'));
      expect(results[3].detail, 'WebSockets');
      expect(progress, [1, 2, 3, 4]);
    });

    test('el paso 3 con 404: muestra el código y la pista del proxy', () async {
      final results = await _diagnostics(negotiate: _fail(_http(404))).run();

      final negotiate = results[2];
      expect(negotiate.outcome, DiagnosticOutcome.failed);
      expect(negotiate.detail, 'HTTP 404');
      expect(negotiate.cause, contains('proxy no reenvía /hubs/'));
      // The other steps still run.
      expect(results[0].ok, isTrue);
      expect(results[3].ok, isTrue);
    });

    test('si el transporte no es WebSockets avisa de que el proxy va más lento', () async {
      final results = await _diagnostics(hub: _ok('LongPolling')).run();

      expect(results[3].ok, isTrue);
      expect(results[3].detail, 'LongPolling');
      expect(results[3].cause, ConnectionDiagnostics.proxyHint);
      expect(ConnectionDiagnostics.proxyHint, contains('Upgrade/Connection'));
    });

    test('sin sesión salta los pasos 2 a 4 y lo explica', () async {
      final results = await _diagnostics(signedIn: false).run();

      expect(results.length, 4);
      expect(results[0].ok, isTrue);
      for (final r in results.skip(1)) {
        expect(r.outcome, DiagnosticOutcome.skipped);
      }
      expect(results[1].detail, 'Inicia sesión para probar los pasos 2-4');
    });

    test('un fallo del servidor inalcanzable resume la excepción y propone la causa', () async {
      final results = await _diagnostics(
        health: _fail(
          DioException(
            requestOptions: RequestOptions(path: '/health'),
            type: DioExceptionType.connectionTimeout,
          ),
        ),
        session: _fail(_http(401)),
        hub: _fail(StateError('boom\nlínea larga')),
      ).run();

      expect(results[0].detail, 'Tiempo de espera agotado');
      expect(results[0].cause, contains('no es alcanzable'));
      expect(results[1].detail, 'HTTP 401');
      expect(results[1].cause, contains('sesión caducó'));
      expect(results[3].detail, 'Bad state: boom');
      expect(results[3].cause, contains('Upgrade/Connection'));
    });
  });

  group('Servidor · Diagnosticar conexión', () {
    Future<void> pump(WidgetTester tester, ConnectionDiagnostics diagnostics) async {
      await pumpRealApp(
        tester,
        location: '/server',
        fakes: AppFakes(),
        realtime: FakeRealtimeHub(),
        overrides: [diagnosticsProvider.overrideWithValue(diagnostics)],
      );
    }

    testWidgets('muestra una línea por paso con su icono', (tester) async {
      await pump(tester, _diagnostics(hub: _ok('ServerSentEvents')));

      await tester.ensureVisible(find.byKey(const Key('server-diagnose')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('server-diagnose')));
      await tester.pumpAndSettle();

      for (final step in DiagnosticStep.values) {
        expect(find.byKey(Key('diagnose-step-${step.name}')), findsOneWidget, reason: step.name);
      }
      expect(find.byKey(const Key('diagnose-icon-health-ok')), findsOneWidget);
      expect(find.byKey(const Key('diagnose-icon-negotiate-ok')), findsOneWidget);
      expect(find.text('ServerSentEvents'), findsOneWidget);
      expect(find.byKey(const Key('diagnose-cause-hub')), findsOneWidget);
      expect(find.textContaining('funciona, pero más lento'), findsOneWidget);
    });

    testWidgets('el paso 3 con 404 sale en rojo con la pista del proxy', (tester) async {
      await pump(tester, _diagnostics(negotiate: _fail(_http(404))));

      await tester.ensureVisible(find.byKey(const Key('server-diagnose')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('server-diagnose')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('diagnose-icon-negotiate-failed')), findsOneWidget);
      expect(find.text('HTTP 404'), findsOneWidget);
      expect(find.byKey(const Key('diagnose-cause-negotiate')), findsOneWidget);
      expect(find.textContaining('proxy no reenvía /hubs/'), findsOneWidget);
    });
  });
}
