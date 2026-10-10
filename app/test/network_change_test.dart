import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/auth/auth_controller.dart';
import 'package:opentrpg_core/core/auth/auth_state.dart';
import 'package:opentrpg_core/core/network/api_client.dart';
import 'package:opentrpg_core/core/network/connectivity.dart';
import 'package:opentrpg_core/core/realtime/connection_diagnostics.dart';
import 'package:opentrpg_core/core/realtime/realtime_hub.dart';
import 'package:opentrpg_core/core/realtime/realtime_provider.dart';
import 'package:opentrpg_core/core/realtime/signalr_realtime_hub.dart';
import 'package:signalr_netcore/signalr_client.dart';

import 'helpers/fakes.dart';

const _wifi = NetworkInterfaces({NetworkKind.wifi});
const _mobile = NetworkInterfaces({NetworkKind.mobile});

/// Platform interfaces driven by the test.
class _NetworkSource implements ConnectivitySource {
  final controller = StreamController<NetworkInterfaces>.broadcast();

  @override
  Future<NetworkInterfaces> current() async => _wifi;

  @override
  Stream<NetworkInterfaces> get changes => controller.stream;
}

/// Scripted SignalR connection.
class _FakeLink implements HubLink {
  _FakeLink(this.url, {this.hangStart = false, this.hangStop = false});

  final String url;

  /// `start` never completes (a socket stuck on the previous network).
  final bool hangStart;

  /// `stop` never completes (as `HubConnection.stop` while a start hangs).
  final bool hangStop;

  @override
  HubConnectionState? state = HubConnectionState.Disconnected;

  int starts = 0;
  int stops = 0;
  final List<String> invocations = [];

  void Function(List<Object?>?)? _onEvent;
  void Function()? _onReconnecting;
  void Function()? _onReconnected;
  void Function()? _onClose;

  @override
  Future<void> start() async {
    starts++;
    state = HubConnectionState.Connecting;
    if (hangStart) await Completer<void>().future;
    state = HubConnectionState.Connected;
  }

  @override
  Future<void> stop() {
    stops++;
    if (hangStop) return Completer<void>().future;
    state = HubConnectionState.Disconnected;
    return Future.value();
  }

  @override
  Future<Object?> invoke(String method, List<Object> args) async {
    invocations.add('$method ${args.join(',')}');
    return null;
  }

  @override
  void onEvent(void Function(List<Object?>? arguments) handler) => _onEvent = handler;

  @override
  void onReconnecting(void Function() handler) => _onReconnecting = handler;

  @override
  void onReconnected(void Function() handler) => _onReconnected = handler;

  @override
  void onClose(void Function() handler) => _onClose = handler;

  /// The transport dropped: the package starts reconnecting.
  void drop() {
    state = HubConnectionState.Reconnecting;
    _onReconnecting?.call();
  }

  /// The automatic reconnection ran out of attempts.
  void giveUp() {
    state = HubConnectionState.Disconnected;
    _onClose?.call();
  }

  void reconnected() {
    state = HubConnectionState.Connected;
    _onReconnected?.call();
  }

  bool get hasEventHandler => _onEvent != null;
}

/// Hands out [_FakeLink]s; [hang] lists the indexes whose start hangs.
class _Links {
  _Links({this.hang = const {}});

  final Set<int> hang;
  final List<_FakeLink> created = [];

  HubLink call(String url, Future<String> Function() accessToken) {
    final hangs = hang.contains(created.length);
    final link = _FakeLink(url, hangStart: hangs, hangStop: hangs);
    created.add(link);
    return link;
  }
}

typedef _Setup = ({
  ProviderContainer container,
  _NetworkSource source,
  _Links links,
  SignalRRealtimeHub hub,
});

Future<_Setup> _setUp(WidgetTester tester, {Set<int> hang = const {}}) async {
  final source = _NetworkSource();
  final links = _Links(hang: hang);
  final hub = SignalRRealtimeHub(
    endpoint: () => (baseUrl: 'https://dnd.example.com', pinnedFingerprint: null),
    accessToken: () async => 'token',
    linkFactory: links.call,
  );
  final container = ProviderContainer(
    overrides: [
      connectivitySourceProvider.overrideWithValue(source),
      authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
      realtimeHubProvider.overrideWithValue(hub),
    ],
  );
  container.listen(campaignRealtimeProvider('c1'), (_, _) {});
  await tester.pump();
  await tester.pump();
  return (container: container, source: source, links: links, hub: hub);
}

Future<void> _tearDown(WidgetTester tester, _Setup setup) async {
  setup.container.dispose();
  // Let the pending timeouts of abandoned connections expire.
  await tester.pump(const Duration(minutes: 1));
  await setup.source.controller.close();
}

RealtimeState _realtime(_Setup setup) => setup.container.read(campaignRealtimeProvider('c1'));

void main() {
  group('ConnectivityController', () {
    testWidgets('cuenta cada cambio del conjunto de interfaces, no solo conectado/desconectado', (
      tester,
    ) async {
      final source = _NetworkSource();
      final container = ProviderContainer(
        overrides: [connectivitySourceProvider.overrideWithValue(source)],
      );
      addTearDown(container.dispose);
      container.listen(connectivityProvider, (_, _) {});
      await tester.pump();

      ConnectivityStatus status() => container.read(connectivityProvider);
      expect(status().network, _wifi);
      expect(status().networkGeneration, 0);

      // The platform repeats the same interfaces: not a change.
      source.controller.add(_wifi);
      await tester.pump();
      expect(status().networkGeneration, 0);

      source.controller.add(_mobile);
      await tester.pump();
      expect(status().hasNetwork, isTrue);
      expect(status().network, _mobile);
      expect(status().networkGeneration, 1);

      source.controller.add(const NetworkInterfaces.none());
      await tester.pump();
      expect(status().hasNetwork, isFalse);
      expect(status().networkGeneration, 2);

      // A request that works does not reset the generation.
      container.read(connectivityProvider.notifier).reportRequestSucceeded();
      expect(status().hasNetwork, isTrue);
      expect(status().networkGeneration, 2);
      await source.controller.close();
    });

    testWidgets('volver tras más de 30 s en segundo plano cuenta como cambio de red', (
      tester,
    ) async {
      var now = DateTime(2026, 10, 7, 20);
      final watcher = AppResumeWatcher(clock: () => now);
      final container = ProviderContainer(
        overrides: [
          appResumedAfterBackgroundProvider.overrideWithValue(watcher.resumedAfterBackground),
        ],
      );
      addTearDown(container.dispose);
      container.listen(connectivityProvider, (_, _) {});
      await tester.pump();

      watcher.onHidden();
      now = now.add(const Duration(seconds: 10));
      watcher.onResumed();
      await tester.pump();
      expect(container.read(connectivityProvider).networkGeneration, 0);

      watcher.onHidden();
      now = now.add(const Duration(seconds: 31));
      watcher.onResumed();
      await tester.pump();
      expect(container.read(connectivityProvider).networkGeneration, 1);
      watcher.dispose();
    });
  });

  group('SignalRRealtimeHub ante cambios de red', () {
    testWidgets('pasar de Wi-Fi a datos móviles reconecta con una conexión nueva', (tester) async {
      final setup = await _setUp(tester);
      final links = setup.links.created;
      expect(links, hasLength(1));
      expect(links[0].invocations, ['JoinCampaign c1']);
      expect(links[0].hasEventHandler, isTrue);
      expect(_realtime(setup).status, RealtimeStatus.connected);

      setup.source.controller.add(_mobile);
      await tester.pump();
      await tester.pump();

      expect(links, hasLength(2));
      expect(links[0].stops, 1);
      expect(links[1].starts, 1);
      expect(links[1].invocations, ['JoinCampaign c1']);
      expect(setup.hub.status, RealtimeStatus.connected);
      expect(_realtime(setup).status, RealtimeStatus.connected);
      expect(_realtime(setup).nextRetryAt, isNull);
      await _tearDown(tester, setup);
    });

    testWidgets('un start colgado no bloquea la conexión siguiente', (tester) async {
      final setup = await _setUp(tester, hang: {0});
      final links = setup.links.created;
      expect(links, hasLength(1));
      expect(_realtime(setup).status, RealtimeStatus.connecting);

      // The network changes while the first start hangs: no waiting for it.
      setup.source.controller.add(_mobile);
      await tester.pump();
      await tester.pump();

      expect(links, hasLength(2));
      expect(links[0].stops, 1);
      expect(links[1].invocations, ['JoinCampaign c1']);
      expect(_realtime(setup).status, RealtimeStatus.connected);

      // The abandoned start times out later without touching the new link.
      await tester.pump(const Duration(seconds: 20));
      expect(_realtime(setup).status, RealtimeStatus.connected);
      expect(_realtime(setup).nextRetryAt, isNull);
      expect(links, hasLength(2));
      await _tearDown(tester, setup);
    });

    testWidgets('si start no termina en 15 s la marca desconectada y reintenta con otra', (
      tester,
    ) async {
      final setup = await _setUp(tester, hang: {0});
      final links = setup.links.created;

      await tester.pump(const Duration(seconds: 15));
      expect(links[0].stops, 1);
      expect(_realtime(setup).status, RealtimeStatus.disconnected);
      expect(_realtime(setup).nextRetryAt, isNotNull);

      await tester.pump(const Duration(seconds: 2));
      await tester.pump();
      expect(links, hasLength(2));
      expect(_realtime(setup).status, RealtimeStatus.connected);
      await _tearDown(tester, setup);
    });

    testWidgets('al agotarse la reconexión automática entra el reintento propio', (tester) async {
      final setup = await _setUp(tester);
      final links = setup.links.created;

      links[0].drop();
      await tester.pump();
      expect(_realtime(setup).status, RealtimeStatus.reconnecting);

      links[0].giveUp();
      await tester.pump();
      expect(_realtime(setup).status, RealtimeStatus.disconnected);
      expect(_realtime(setup).nextRetryAt, isNotNull);

      await tester.pump(CampaignRealtime.retryDelays.first);
      await tester.pump();
      expect(links, hasLength(2));
      expect(links[1].invocations, ['JoinCampaign c1']);
      expect(_realtime(setup).status, RealtimeStatus.connected);
      await _tearDown(tester, setup);
    });

    testWidgets('no se queda para siempre en "Reconectando"', (tester) async {
      final setup = await _setUp(tester);
      final links = setup.links.created;

      links[0].drop();
      await tester.pump();
      await tester.pump(const Duration(seconds: 44));
      expect(_realtime(setup).status, RealtimeStatus.reconnecting);

      await tester.pump(const Duration(seconds: 1));
      expect(links[0].stops, 1);
      expect(_realtime(setup).status, RealtimeStatus.disconnected);
      expect(_realtime(setup).nextRetryAt, isNotNull);

      await tester.pump(CampaignRealtime.retryDelays.first);
      await tester.pump();
      expect(links, hasLength(2));
      expect(_realtime(setup).status, RealtimeStatus.connected);
      await _tearDown(tester, setup);
    });

    testWidgets('si la reconexión automática funciona vuelve a unirse a la campaña', (
      tester,
    ) async {
      final setup = await _setUp(tester);
      final link = setup.links.created.single;

      link.drop();
      await tester.pump();
      link.reconnected();
      await tester.pump();
      await tester.pump();

      expect(link.invocations, ['JoinCampaign c1', 'JoinCampaign c1']);
      expect(_realtime(setup).status, RealtimeStatus.connected);
      // The watchdog was cancelled.
      await tester.pump(const Duration(minutes: 1));
      expect(_realtime(setup).status, RealtimeStatus.connected);
      expect(setup.links.created, hasLength(1));
      await _tearDown(tester, setup);
    });

    test('los tiempos del cliente encajan con los del servidor', () {
      // Server (Program.cs): ClientTimeoutInterval 60 s, KeepAliveInterval 15 s.
      const serverClientTimeout = Duration(seconds: 60);
      const serverKeepAlive = Duration(seconds: 15);
      expect(serverClientTimeout >= SignalRRealtimeHub.keepAliveInterval * 2, isTrue);
      expect(SignalRRealtimeHub.serverTimeout >= serverKeepAlive * 2, isTrue);
      expect(SignalRRealtimeHub.reconnectDelays, isNotEmpty);
    });
  });

  group('pool HTTP', () {
    test('resetConnections cierra el adaptador a la fuerza y crea otro', () {
      final adapters = <_RecordingAdapter>[];
      final client = ApiClient(
        baseUrl: 'https://dnd.example.com',
        adapterFactory: () {
          final adapter = _RecordingAdapter();
          adapters.add(adapter);
          return adapter;
        },
      );
      expect(adapters, hasLength(1));
      expect(client.dio.httpClientAdapter, same(adapters[0]));

      client.resetConnections();

      expect(adapters, hasLength(2));
      expect(adapters[0].closedWithForce, isTrue);
      expect(client.dio.httpClientAdapter, same(adapters[1]));
      expect(adapters[1].closedWithForce, isNull);
    });

    testWidgets('el cliente de la app renueva su pool en cada cambio de red', (tester) async {
      final source = _NetworkSource();
      final container = ProviderContainer(
        overrides: [
          connectivitySourceProvider.overrideWithValue(source),
          fakeServerConfigOverride(),
        ],
      );
      addTearDown(container.dispose);
      container.listen(connectivityProvider, (_, _) {});
      final client = container.read(apiClientProvider);
      await tester.pump();
      final first = client.dio.httpClientAdapter;

      source.controller.add(_wifi);
      await tester.pump();
      expect(client.dio.httpClientAdapter, same(first));

      source.controller.add(_mobile);
      await tester.pump();
      expect(client.dio.httpClientAdapter, isNot(same(first)));
      // The old pool is closed: it cannot be used any more.
      await expectLater(
        first.fetch(RequestOptions(path: 'https://dnd.example.com/health'), null, null),
        throwsA(isA<StateError>()),
      );
      await source.controller.close();
    });
  });

  group('diagnóstico de red', () {
    test('muestra la red y la resolución y avisa de una IP privada con datos móviles', () {
      const home = NetworkReport(
        network: _wifi,
        host: 'dnd.example.com',
        addresses: ['192.168.1.20'],
      );
      expect(home.networkLine, 'Red actual: Wi-Fi');
      expect(home.resolutionLine, 'dnd.example.com → 192.168.1.20');
      expect(home.hint, isNull);

      const outside = NetworkReport(
        network: _mobile,
        host: 'dnd.example.com',
        addresses: ['192.168.1.20'],
      );
      expect(outside.networkLine, 'Red actual: datos móviles');
      expect(outside.hint, contains('IP privada'));

      const public = NetworkReport(
        network: _mobile,
        host: 'dnd.example.com',
        addresses: ['203.0.113.7'],
      );
      expect(public.hint, isNull);

      const failed = NetworkReport(network: _mobile, host: 'dnd.example.com', error: 'sin DNS');
      expect(failed.resolutionLine, 'dnd.example.com → no se pudo resolver (sin DNS)');
    });
  });
}

/// Adapter that records how it was closed.
class _RecordingAdapter implements HttpClientAdapter {
  bool? closedWithForce;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<List<int>>? requestStream,
    Future<void>? cancelFuture,
  ) => throw UnimplementedError();

  @override
  void close({bool force = false}) => closedWithForce = force;
}
