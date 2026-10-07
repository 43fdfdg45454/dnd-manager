import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../server/certificate_pinning.dart';
import '../server/server_url.dart';
import 'realtime_events.dart';
import 'realtime_hub.dart';

/// Where to connect: the server base URL (empty when none is configured) and
/// the SHA-256 fingerprint of the self-signed certificate pinned for its host.
typedef RealtimeEndpoint = ({String baseUrl, String? pinnedFingerprint});

/// The part of a SignalR `HubConnection` [SignalRRealtimeHub] uses, so tests
/// can replace the connection with a fake ([HubLinkFactory]).
abstract interface class HubLink {
  HubConnectionState? get state;

  Future<void> start();

  Future<void> stop();

  Future<Object?> invoke(String method, List<Object> args);

  /// Payloads of [SignalRRealtimeHub.eventMethod].
  void onEvent(void Function(List<Object?>? arguments) handler);

  /// The connection dropped and the automatic reconnection started.
  void onReconnecting(void Function() handler);

  void onReconnected(void Function() handler);

  /// Closed for good: stopped, or the automatic reconnection gave up.
  void onClose(void Function() handler);
}

/// Builds the [HubLink] to [url]; [accessToken] is asked on every
/// (re)connection.
typedef HubLinkFactory = HubLink Function(String url, Future<String> Function() accessToken);

/// [RealtimeHub] over SignalR (`<server>/hubs/campaign`). The JWT goes in the
/// `access_token` query parameter through [accessToken]; the connection
/// reconnects by itself after a drop (with [reconnectDelays]) and joins the
/// campaign again.
///
/// Nothing can leave it stuck: [start] and the join give up after
/// [startTimeout], a reconnection that lasts longer than [reconnectingTimeout]
/// is abandoned, and [restart] (a network change) throws the connection away
/// without waiting for whatever it is doing. Each time the hub ends up
/// [RealtimeStatus.disconnected] and `CampaignRealtime` retries with its own
/// delays, always with a brand-new connection (new sockets, new DNS lookup).
///
/// TLS: the HTTP and WebSocket clients of the package are plain `dart:io`
/// ones, so they inherit the user CAs installed in `HttpOverrides.global`
/// (`trust_store.dart`). A certificate pinned for the server host is honoured
/// by running the connection inside a zone with [_PinnedHttpOverrides].
class SignalRRealtimeHub implements RealtimeHub {
  SignalRRealtimeHub({
    required this.endpoint,
    required this.accessToken,
    HubLinkFactory? linkFactory,
    this.startTimeout = const Duration(seconds: 15),
    this.reconnectingTimeout = const Duration(seconds: 45),
    this.stopTimeout = const Duration(seconds: 3),
  }) : _linkFactory = linkFactory ?? _signalRLink;

  /// Read on every [connect].
  final RealtimeEndpoint Function() endpoint;

  /// Current access token (null when signed out). Called on every
  /// (re)connection, so a refreshed token is picked up.
  final Future<String?> Function() accessToken;

  /// Longest wait for the connection to open (negotiate, transport,
  /// handshake) and for the server to accept the join.
  final Duration startTimeout;

  /// Longest time in [RealtimeStatus.reconnecting]: the attempts of the
  /// package have no timeout of their own and may hang on a dead route.
  final Duration reconnectingTimeout;

  /// Longest wait for a stop on [disconnect] / [dispose].
  final Duration stopTimeout;

  final HubLinkFactory _linkFactory;

  static const path = '/hubs/campaign';
  static const eventMethod = 'campaignEvent';

  /// Pings sent to the server. The server closes a client it has not heard
  /// from in its `ClientTimeoutInterval` (60 s), which must be at least twice
  /// this.
  static const keepAliveInterval = Duration(seconds: 15);

  /// Silence after which the connection counts as dead. The server pings every
  /// `KeepAliveInterval` (15 s); this must be at least twice that.
  static const serverTimeout = Duration(seconds: 30);

  /// Waits of the automatic reconnection after a drop (milliseconds); after
  /// the last failed attempt the connection closes and the app retries with a
  /// new one.
  static const reconnectDelays = [0, 2000, 5000, 10000];

  final _events = StreamController<CampaignEvent>.broadcast();
  final _statuses = StreamController<RealtimeStatus>.broadcast();

  HubLink? _connection;
  String? _connectionUrl;
  String? _campaignId;
  String? _joined;
  RealtimeStatus _status = RealtimeStatus.disconnected;
  Future<void> _queue = Future.value();

  /// Grows on every [restart]: work queued before it is dropped.
  int _epoch = 0;
  Timer? _reconnectWatchdog;
  bool _disposed = false;

  @override
  Stream<CampaignEvent> get events => _events.stream;

  @override
  Stream<RealtimeStatus> get statusChanges => _statuses.stream;

  @override
  RealtimeStatus get status => _status;

  @override
  String? get campaignId => _campaignId;

  void _setStatus(RealtimeStatus next) {
    if (_disposed || next == _status) return;
    _status = next;
    _statuses.add(next);
  }

  /// Runs [action] after the previous connect / disconnect has finished,
  /// unless a [restart] happens first.
  Future<void> _serial(Future<void> Function() action) {
    final epoch = _epoch;
    final next = _queue.then((_) => epoch == _epoch ? action() : Future<void>.value());
    _queue = next.then<void>((_) {}, onError: (Object _) {});
    return next;
  }

  @override
  Future<void> connect(String campaignId) {
    _campaignId = campaignId;
    return _serial(() => _connect(campaignId));
  }

  @override
  Future<void> restart() {
    if (_disposed) return Future.value();
    // Whatever the queue is waiting for (a start that never ends) is abandoned.
    _epoch++;
    _queue = Future.value();
    _detach();
    final campaignId = _campaignId;
    if (campaignId == null) {
      _setStatus(RealtimeStatus.disconnected);
      return Future.value();
    }
    _setStatus(RealtimeStatus.connecting);
    return _serial(() => _connect(campaignId));
  }

  Future<void> _connect(String campaignId) async {
    if (_disposed || _campaignId != campaignId) return;
    final target = endpoint();
    if (target.baseUrl.isEmpty) throw StateError('No server configured');
    final url = '${target.baseUrl.replaceAll(RegExp(r'/+$'), '')}$path';

    var connection = _connection;
    if (connection != null &&
        (_connectionUrl != url || connection.state == HubConnectionState.Disconnected)) {
      // Another server, or closed: never reuse it, start from scratch.
      _detach();
      connection = null;
    }
    if (connection == null) {
      final token = await accessToken();
      if (token == null || token.isEmpty) throw StateError('Signed out');
      if (_disposed || _campaignId != campaignId || _connection != null) return;
      connection = _build(url);
      _connection = connection;
      _connectionUrl = url;
    }

    switch (connection.state) {
      case HubConnectionState.Disconnected:
        _setStatus(RealtimeStatus.connecting);
        try {
          await _inZone(target, connection.start).timeout(startTimeout);
        } catch (error) {
          // Replaced meanwhile (restart): its successor reports the status.
          if (!identical(_connection, connection)) return;
          debugPrint('Realtime: could not connect: $error');
          _detach();
          _setStatus(RealtimeStatus.disconnected);
          rethrow;
        }
      case HubConnectionState.Reconnecting:
        // `onReconnected` joins the campaign.
        return;
      case HubConnectionState.Connecting ||
          HubConnectionState.Connected ||
          HubConnectionState.Disconnecting ||
          null:
        break;
    }
    if (!identical(_connection, connection)) return;
    try {
      await _join(connection, campaignId);
    } catch (_) {
      // Not a member (any more) or the connection dropped meanwhile.
      if (!identical(_connection, connection)) return;
      _detach();
      _setStatus(RealtimeStatus.disconnected);
      rethrow;
    }
    if (identical(_connection, connection)) _setStatus(RealtimeStatus.connected);
  }

  Future<void> _join(HubLink connection, String campaignId) async {
    final previous = _joined;
    if (previous == campaignId) return;
    if (previous != null) {
      try {
        await connection.invoke('LeaveCampaign', [previous]).timeout(startTimeout);
      } catch (error) {
        debugPrint('Realtime: could not leave $previous: $error');
      }
    }
    _joined = null;
    await connection.invoke('JoinCampaign', [campaignId]).timeout(startTimeout);
    if (identical(_connection, connection)) _joined = campaignId;
  }

  HubLink _build(String url) {
    final connection = _linkFactory(url, () async => await accessToken() ?? '');

    connection.onEvent((arguments) {
      final raw = arguments == null || arguments.isEmpty ? null : arguments.first;
      if (raw is Map && !_disposed) _events.add(CampaignEvent.fromJson(raw));
    });
    connection.onReconnecting(() {
      if (!identical(_connection, connection)) return;
      _joined = null;
      _setStatus(RealtimeStatus.reconnecting);
      _reconnectWatchdog?.cancel();
      _reconnectWatchdog = Timer(reconnectingTimeout, () {
        _reconnectWatchdog = null;
        if (!identical(_connection, connection)) return;
        debugPrint('Realtime: reconnection took too long, starting over');
        _detach();
        _setStatus(RealtimeStatus.disconnected);
      });
    });
    connection.onReconnected(() {
      if (!identical(_connection, connection)) return;
      _cancelWatchdog();
      final campaignId = _campaignId;
      if (campaignId == null) return;
      unawaited(
        _serial(() async {
          if (!identical(_connection, connection)) return;
          try {
            await _join(connection, campaignId);
            if (identical(_connection, connection)) _setStatus(RealtimeStatus.connected);
          } catch (error) {
            debugPrint('Realtime: could not join again: $error');
            if (!identical(_connection, connection)) return;
            _detach();
            _setStatus(RealtimeStatus.disconnected);
          }
        }),
      );
    });
    connection.onClose(() {
      if (!identical(_connection, connection)) return;
      // Stopped by the server or the reconnection gave up: the next attempt
      // builds a new connection.
      _detach();
      _setStatus(RealtimeStatus.disconnected);
    });
    return connection;
  }

  /// The real connection: SignalR with explicit keep-alive, server timeout and
  /// reconnection delays.
  static HubLink _signalRLink(String url, Future<String> Function() accessToken) {
    final connection = HubConnectionBuilder()
        .withUrl(
          url,
          options: HttpConnectionOptions(accessTokenFactory: accessToken, requestTimeout: 15000),
        )
        .withAutomaticReconnect(retryDelays: reconnectDelays)
        .build();
    connection.keepAliveIntervalInMilliseconds = keepAliveInterval.inMilliseconds;
    connection.serverTimeoutInMilliseconds = serverTimeout.inMilliseconds;
    return _SignalRLink(connection);
  }

  void _cancelWatchdog() {
    _reconnectWatchdog?.cancel();
    _reconnectWatchdog = null;
  }

  /// Forgets the current connection at once and stops it in the background:
  /// `HubConnection.stop` waits for a pending start, which may never end.
  void _detach() {
    final connection = _connection;
    _connection = null;
    _connectionUrl = null;
    _joined = null;
    _cancelWatchdog();
    if (connection != null) unawaited(_stopQuietly(connection));
  }

  Future<void> _stopQuietly(HubLink connection) async {
    try {
      await connection.stop().timeout(stopTimeout);
    } catch (error) {
      debugPrint('Realtime: error while stopping: $error');
    }
  }

  /// Runs [body] with the pinned certificate of the server host accepted, so
  /// every HTTP / WebSocket client the connection creates (now and when it
  /// reconnects) honours it.
  Future<void> _inZone(RealtimeEndpoint target, Future<void> Function() body) {
    final host = serverHost(target.baseUrl);
    final fingerprint = target.pinnedFingerprint;
    if (host == null || fingerprint == null || !target.baseUrl.startsWith('https://')) {
      return body();
    }
    return HttpOverrides.runWithHttpOverrides(
      body,
      _PinnedHttpOverrides(host: host, fingerprint: fingerprint, previous: HttpOverrides.current),
    );
  }

  Future<void> _stop() async {
    final connection = _connection;
    _detach();
    _setStatus(RealtimeStatus.disconnected);
    // Already stopping in the background; give it a moment to say goodbye.
    if (connection != null) await _stopQuietly(connection);
  }

  /// Transports tried by [probe], best first.
  static const _probeTransports = [
    (HttpTransportType.WebSockets, 'WebSockets'),
    (HttpTransportType.ServerSentEvents, 'ServerSentEvents'),
    (HttpTransportType.LongPolling, 'LongPolling'),
  ];

  @override
  Future<String> probe() async {
    final target = endpoint();
    if (target.baseUrl.isEmpty) throw StateError('No server configured');
    final token = await accessToken();
    if (token == null || token.isEmpty) throw StateError('Signed out');
    final url = '${target.baseUrl.replaceAll(RegExp(r'/+$'), '')}$path';

    Object? lastError;
    for (final (transport, name) in _probeTransports) {
      final connection = HubConnectionBuilder()
          .withUrl(
            url,
            options: HttpConnectionOptions(
              accessTokenFactory: () async => token,
              transport: transport,
              requestTimeout: 8000,
            ),
          )
          .build();
      try {
        await _inZone(target, () async => connection.start()).timeout(const Duration(seconds: 8));
        return name;
      } catch (error) {
        lastError = error;
      } finally {
        try {
          await connection.stop().timeout(const Duration(seconds: 2));
        } catch (_) {
          // Nothing to close.
        }
      }
    }
    throw lastError ?? StateError('Could not connect');
  }

  @override
  Future<void> disconnect() {
    _campaignId = null;
    return _serial(_stop);
  }

  @override
  Future<void> dispose() async {
    _campaignId = null;
    _cancelWatchdog();
    await _serial(_stop);
    _disposed = true;
    await _events.close();
    await _statuses.close();
  }
}

/// Accepts the pinned self-signed certificate of [host]; everything else is
/// verified as usual by the clients of [previous] (the user CAs installed in
/// `HttpOverrides.global`).
class _PinnedHttpOverrides extends HttpOverrides {
  _PinnedHttpOverrides({required String host, required this.fingerprint, this.previous})
    : host = host.toLowerCase();

  final String host;
  final String fingerprint;
  final HttpOverrides? previous;

  @override
  HttpClient createHttpClient(SecurityContext? context) {
    final client = previous?.createHttpClient(context) ?? super.createHttpClient(context);
    client.badCertificateCallback = (cert, requestHost, port) =>
        requestHost.toLowerCase() == host &&
        fingerprintsMatch(certificateFingerprint(cert.der), fingerprint);
    return client;
  }
}

class _SignalRLink implements HubLink {
  _SignalRLink(this._connection);

  final HubConnection _connection;

  @override
  HubConnectionState? get state => _connection.state;

  @override
  Future<void> start() async => _connection.start();

  @override
  Future<void> stop() => _connection.stop();

  @override
  Future<Object?> invoke(String method, List<Object> args) =>
      _connection.invoke(method, args: args);

  @override
  void onEvent(void Function(List<Object?>? arguments) handler) =>
      _connection.on(SignalRRealtimeHub.eventMethod, handler);

  @override
  void onReconnecting(void Function() handler) => _connection.onreconnecting(({error}) {
    debugPrint('Realtime: reconnecting after $error');
    handler();
  });

  @override
  void onReconnected(void Function() handler) =>
      _connection.onreconnected(({connectionId}) => handler());

  @override
  void onClose(void Function() handler) => _connection.onclose(({error}) => handler());
}
