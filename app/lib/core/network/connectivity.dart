import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Whether the device has a network interface up.
abstract class ConnectivitySource {
  Future<bool> hasNetwork();

  /// Emits on every change of the network interfaces.
  Stream<bool> get changes;
}

/// Default source (tests, and until `main.dart` installs the platform one):
/// always connected, so only failed requests mark the app offline.
class AlwaysConnectedSource implements ConnectivitySource {
  const AlwaysConnectedSource();

  @override
  Future<bool> hasNetwork() async => true;

  @override
  Stream<bool> get changes => const Stream.empty();
}

/// Backed by `connectivity_plus`.
class PlatformConnectivitySource implements ConnectivitySource {
  PlatformConnectivitySource([Connectivity? connectivity])
    : _connectivity = connectivity ?? Connectivity();

  final Connectivity _connectivity;

  static bool _connected(List<ConnectivityResult> results) =>
      results.any((r) => r != ConnectivityResult.none);

  @override
  Future<bool> hasNetwork() async => _connected(await _connectivity.checkConnectivity());

  @override
  Stream<bool> get changes => _connectivity.onConnectivityChanged.map(_connected).distinct();
}

final connectivitySourceProvider = Provider<ConnectivitySource>(
  (ref) => const AlwaysConnectedSource(),
);

@immutable
class ConnectivityStatus {
  const ConnectivityStatus({this.hasNetwork = true, this.lastRequestFailed = false});

  /// A network interface is up (according to the platform).
  final bool hasNetwork;

  /// The last request could not reach the server (no answer at all).
  final bool lastRequestFailed;

  bool get isOffline => !hasNetwork || lastRequestFailed;

  ConnectivityStatus copyWith({bool? hasNetwork, bool? lastRequestFailed}) => ConnectivityStatus(
    hasNetwork: hasNetwork ?? this.hasNetwork,
    lastRequestFailed: lastRequestFailed ?? this.lastRequestFailed,
  );

  @override
  bool operator ==(Object other) =>
      other is ConnectivityStatus &&
      other.hasNetwork == hasNetwork &&
      other.lastRequestFailed == lastRequestFailed;

  @override
  int get hashCode => Object.hash(hasNetwork, lastRequestFailed);
}

/// Connection state of the app: the platform's network interfaces plus the
/// outcome of the last request (a Wi-Fi without route to the server counts as
/// offline). The HTTP client reports every request.
class ConnectivityController extends Notifier<ConnectivityStatus> {
  @override
  ConnectivityStatus build() {
    final source = ref.watch(connectivitySourceProvider);
    final subscription = source.changes.listen(_onNetworkChanged, onError: (Object _) {});
    ref.onDispose(subscription.cancel);
    unawaited(_initialCheck(source));
    return const ConnectivityStatus();
  }

  Future<void> _initialCheck(ConnectivitySource source) async {
    try {
      final hasNetwork = await source.hasNetwork();
      if (ref.mounted) _set(state.copyWith(hasNetwork: hasNetwork));
    } catch (_) {
      // Unknown: keep assuming there is network.
    }
  }

  void _onNetworkChanged(bool hasNetwork) {
    // A new interface gives the server another chance.
    _set(ConnectivityStatus(hasNetwork: hasNetwork));
  }

  void reportRequestFailed() => _set(state.copyWith(lastRequestFailed: true));

  void reportRequestSucceeded() => _set(const ConnectivityStatus());

  void _set(ConnectivityStatus next) {
    if (ref.mounted && next != state) state = next;
  }
}

final connectivityProvider = NotifierProvider<ConnectivityController, ConnectivityStatus>(
  ConnectivityController.new,
);

/// Writes need the server: false while offline (ADR 0002).
final canWriteProvider = Provider<bool>((ref) => !ref.watch(connectivityProvider).isOffline);
