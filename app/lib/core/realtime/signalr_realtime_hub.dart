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

/// [RealtimeHub] over SignalR (`<server>/hubs/campaign`). The JWT goes in the
/// `access_token` query parameter through [accessToken]; the connection
/// reconnects by itself after a drop and joins the campaign again.
///
/// TLS: the HTTP and WebSocket clients of the package are plain `dart:io`
/// ones, so they inherit the user CAs installed in `HttpOverrides.global`
/// (`trust_store.dart`). A certificate pinned for the server host is honoured
/// by running the connection inside a zone with [_PinnedHttpOverrides].
class SignalRRealtimeHub implements RealtimeHub {
  SignalRRealtimeHub({required this.endpoint, required this.accessToken});

  /// Read on every [connect].
  final RealtimeEndpoint Function() endpoint;

  /// Current access token (null when signed out). Called on every
  /// (re)connection, so a refreshed token is picked up.
  final Future<String?> Function() accessToken;

  static const path = '/hubs/campaign';
  static const eventMethod = 'campaignEvent';

  final _events = StreamController<CampaignEvent>.broadcast();
  final _statuses = StreamController<RealtimeStatus>.broadcast();

  HubConnection? _connection;
  String? _connectionUrl;
  String? _campaignId;
  String? _joined;
  RealtimeStatus _status = RealtimeStatus.disconnected;
  Future<void> _queue = Future.value();
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

  /// Runs [action] after the previous connect / disconnect has finished.
  Future<void> _serial(Future<void> Function() action) {
    final next = _queue.then((_) => action());
    _queue = next.then<void>((_) {}, onError: (Object _) {});
    return next;
  }

  @override
  Future<void> connect(String campaignId) {
    _campaignId = campaignId;
    return _serial(() => _connect(campaignId));
  }

  Future<void> _connect(String campaignId) async {
    if (_disposed || _campaignId != campaignId) return;
    final target = endpoint();
    if (target.baseUrl.isEmpty) throw StateError('No server configured');
    final url = '${target.baseUrl.replaceAll(RegExp(r'/+$'), '')}$path';

    var connection = _connection;
    if (connection != null && _connectionUrl != url) {
      await _stop();
      connection = null;
    }
    if (connection == null) {
      final token = await accessToken();
      if (token == null || token.isEmpty) throw StateError('Signed out');
      connection = _build(url, target.pinnedFingerprint);
      _connection = connection;
      _connectionUrl = url;
    }

    switch (connection.state) {
      case HubConnectionState.Disconnected:
        _setStatus(RealtimeStatus.connecting);
        try {
          await _inZone(target, () async => connection!.start());
        } catch (_) {
          if (identical(_connection, connection)) _setStatus(RealtimeStatus.disconnected);
          rethrow;
        }
      case HubConnectionState.Reconnecting:
        // `onreconnected` joins the campaign.
        return;
      case HubConnectionState.Connecting ||
          HubConnectionState.Connected ||
          HubConnectionState.Disconnecting ||
          null:
        break;
    }
    try {
      await _join(connection, campaignId);
    } catch (_) {
      // Not a member (any more) or the connection dropped meanwhile.
      if (identical(_connection, connection)) await _stop();
      rethrow;
    }
    _setStatus(RealtimeStatus.connected);
  }

  Future<void> _join(HubConnection connection, String campaignId) async {
    final previous = _joined;
    if (previous == campaignId) return;
    if (previous != null) {
      try {
        await connection.invoke('LeaveCampaign', args: [previous]);
      } catch (error) {
        debugPrint('Realtime: could not leave $previous: $error');
      }
    }
    _joined = null;
    await connection.invoke('JoinCampaign', args: [campaignId]);
    _joined = campaignId;
  }

  HubConnection _build(String url, String? pinnedFingerprint) {
    final connection = HubConnectionBuilder()
        .withUrl(
          url,
          options: HttpConnectionOptions(
            accessTokenFactory: () async => await accessToken() ?? '',
            requestTimeout: 15000,
          ),
        )
        .withAutomaticReconnect()
        .build();

    connection.on(eventMethod, (arguments) {
      final raw = arguments == null || arguments.isEmpty ? null : arguments.first;
      if (raw is Map && !_disposed) _events.add(CampaignEvent.fromJson(raw));
    });
    connection.onreconnecting(({error}) {
      if (!identical(_connection, connection)) return;
      _joined = null;
      _setStatus(RealtimeStatus.reconnecting);
    });
    connection.onreconnected(({connectionId}) {
      if (!identical(_connection, connection)) return;
      final campaignId = _campaignId;
      if (campaignId == null) return;
      unawaited(
        _serial(() async {
          if (!identical(_connection, connection)) return;
          try {
            await _join(connection, campaignId);
            _setStatus(RealtimeStatus.connected);
          } catch (error) {
            debugPrint('Realtime: could not join again: $error');
            _setStatus(RealtimeStatus.disconnected);
          }
        }),
      );
    });
    connection.onclose(({error}) {
      if (!identical(_connection, connection)) return;
      _joined = null;
      _setStatus(RealtimeStatus.disconnected);
    });
    return connection;
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
    _connection = null;
    _connectionUrl = null;
    _joined = null;
    if (connection != null) {
      try {
        await connection.stop();
      } catch (error) {
        debugPrint('Realtime: error while stopping: $error');
      }
    }
    _setStatus(RealtimeStatus.disconnected);
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
          await connection.stop();
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
