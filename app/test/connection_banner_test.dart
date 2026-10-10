import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/realtime/connection_banner.dart';
import 'package:opentrpg_core/core/realtime/realtime_hub.dart';
import 'package:opentrpg_core/core/realtime/realtime_provider.dart';
import 'package:opentrpg_core/core/realtime/realtime_status_icon.dart';
import 'package:opentrpg_core/core/theme/app_theme.dart';

/// [CampaignRealtime] that holds a fixed state and records [retryNow].
class _FixedRealtime extends CampaignRealtime {
  _FixedRealtime(this.initial) : super('c1');

  final RealtimeState initial;
  int retries = 0;

  @override
  RealtimeState build() => initial;

  @override
  Future<void> retryNow() async => retries++;
}

final _t0 = DateTime(2026, 10, 6, 20);

Future<_FixedRealtime> _pump(
  WidgetTester tester,
  RealtimeState state, {
  DateTime Function()? now,
}) async {
  final fake = _FixedRealtime(state);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [campaignRealtimeProvider('c1').overrideWith(() => fake)],
      child: MaterialApp(
        theme: AppTheme.light(),
        home: Scaffold(
          appBar: AppBar(actions: const [RealtimeStatusIcon(campaignId: 'c1')]),
          body: Column(
            children: [ConnectionBanner(campaignId: 'c1', now: now ?? () => _t0)],
          ),
        ),
      ),
    ),
  );
  await tester.pump();
  return fake;
}

const _amber = Key('connection-banner-reconnecting');
const _red = Key('connection-banner-offline');

void main() {
  group('ConnectionBanner', () {
    testWidgets('conectado: sin franja y el icono dorado dice "En vivo"', (tester) async {
      await _pump(tester, const RealtimeState(status: RealtimeStatus.connected));

      expect(find.byKey(_amber), findsNothing);
      expect(find.byKey(_red), findsNothing);
      expect(find.byKey(const Key('realtime-retry')), findsNothing);
      expect(find.byKey(const Key('realtime-connected')), findsOneWidget);
      expect(find.byTooltip('En vivo'), findsOneWidget);
    });

    for (final status in [RealtimeStatus.connecting, RealtimeStatus.reconnecting]) {
      testWidgets('$status: franja ámbar "Reconectando…" y icono gris', (tester) async {
        await _pump(tester, RealtimeState(status: status));

        expect(find.byKey(_amber), findsOneWidget);
        expect(find.text('Reconectando…'), findsOneWidget);
        expect(find.byKey(_red), findsNothing);
        expect(find.byKey(const Key('realtime-reconnecting')), findsOneWidget);
        expect(find.byKey(const Key('realtime-connected')), findsNothing);
      });
    }

    testWidgets('desconectado con reintento programado: ámbar con cuenta atrás', (tester) async {
      var now = _t0;
      final fake = await _pump(
        tester,
        RealtimeState(
          status: RealtimeStatus.disconnected,
          nextRetryAt: _t0.add(const Duration(seconds: 5)),
        ),
        now: () => now,
      );

      expect(find.byKey(_amber), findsOneWidget);
      expect(find.text('Reconectando… (5 s)'), findsOneWidget);

      now = _t0.add(const Duration(seconds: 2));
      await tester.pump(const Duration(seconds: 1));
      expect(find.text('Reconectando… (3 s)'), findsOneWidget);

      now = _t0.add(const Duration(seconds: 9));
      await tester.pump(const Duration(seconds: 1));
      expect(find.text('Reconectando… (0 s)'), findsOneWidget);

      await tester.tap(find.byKey(const Key('realtime-retry')));
      expect(fake.retries, 1);
    });

    testWidgets('desconectado sin reintento: roja con la edad de los datos y Reintentar', (
      tester,
    ) async {
      final fake = await _pump(
        tester,
        RealtimeState(
          status: RealtimeStatus.disconnected,
          lastConnectedAt: _t0.subtract(const Duration(minutes: 5)),
        ),
      );

      expect(find.byKey(_red), findsOneWidget);
      expect(find.byKey(_amber), findsNothing);
      expect(find.text('Sin conexión en vivo · datos de hace 5 min'), findsOneWidget);
      expect(find.byKey(const Key('realtime-offline')), findsOneWidget);

      await tester.tap(find.byKey(const Key('realtime-retry')));
      expect(fake.retries, 1);
    });

    testWidgets('sin red: roja; sin datos previos no inventa una edad', (tester) async {
      final fake = await _pump(tester, const RealtimeState(status: RealtimeStatus.offline));

      expect(find.byKey(_red), findsOneWidget);
      expect(find.text('Sin conexión en vivo'), findsOneWidget);
      expect(find.byTooltip('Tiempo real: sin red'), findsOneWidget);

      await tester.tap(find.byKey(const Key('realtime-retry')));
      expect(fake.retries, 1);
    });
  });
}
