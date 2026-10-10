import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Kind of network interface the platform reports as up.
enum NetworkKind {
  wifi('Wi-Fi'),
  mobile('datos móviles'),
  ethernet('Ethernet'),
  vpn('VPN'),
  other('otra');

  const NetworkKind(this.label);

  /// Name shown to the user.
  final String label;
}

/// The set of network interfaces that are up (empty: no network).
@immutable
class NetworkInterfaces {
  const NetworkInterfaces(this.kinds);

  const NetworkInterfaces.none() : kinds = const {};

  /// What [AlwaysConnectedSource] and the default status assume.
  static const unknown = NetworkInterfaces({NetworkKind.other});

  final Set<NetworkKind> kinds;

  bool get hasNetwork => kinds.isNotEmpty;

  /// "Wi-Fi", "datos móviles + VPN", "sin red"...
  String get label => kinds.isEmpty
      ? 'sin red'
      : (kinds.toList()..sort((a, b) => a.index.compareTo(b.index)))
            .map((k) => k.label)
            .join(' + ');

  /// From the platform results (`none` alone means no network).
  factory NetworkInterfaces.fromResults(Iterable<ConnectivityResult> results) => NetworkInterfaces({
    for (final result in results)
      if (result != ConnectivityResult.none)
        switch (result) {
          ConnectivityResult.wifi => NetworkKind.wifi,
          ConnectivityResult.mobile => NetworkKind.mobile,
          ConnectivityResult.ethernet => NetworkKind.ethernet,
          ConnectivityResult.vpn => NetworkKind.vpn,
          _ => NetworkKind.other,
        },
  });

  @override
  bool operator ==(Object other) => other is NetworkInterfaces && setEquals(other.kinds, kinds);

  @override
  int get hashCode => Object.hashAllUnordered(kinds);

  @override
  String toString() => 'NetworkInterfaces($label)';
}

/// Which network interfaces are up.
abstract class ConnectivitySource {
  Future<NetworkInterfaces> current();

  /// Emits whenever the platform reports the interfaces (repeated values are
  /// possible; [ConnectivityController] ignores them).
  Stream<NetworkInterfaces> get changes;
}

/// Default source (tests, and until `main.dart` installs the platform one):
/// always connected, so only failed requests mark the app offline.
class AlwaysConnectedSource implements ConnectivitySource {
  const AlwaysConnectedSource();

  @override
  Future<NetworkInterfaces> current() async => NetworkInterfaces.unknown;

  @override
  Stream<NetworkInterfaces> get changes => const Stream.empty();
}

/// Backed by `connectivity_plus`.
class PlatformConnectivitySource implements ConnectivitySource {
  PlatformConnectivitySource([Connectivity? connectivity])
    : _connectivity = connectivity ?? Connectivity();

  final Connectivity _connectivity;

  @override
  Future<NetworkInterfaces> current() async =>
      NetworkInterfaces.fromResults(await _connectivity.checkConnectivity());

  @override
  Stream<NetworkInterfaces> get changes =>
      _connectivity.onConnectivityChanged.map(NetworkInterfaces.fromResults);
}

final connectivitySourceProvider = Provider<ConnectivitySource>(
  (ref) => const AlwaysConnectedSource(),
);

/// Emits when the app comes back to the foreground after a long time in the
/// background (the network may have changed meanwhile without the app being
/// told). Installed by `main.dart` with [AppResumeWatcher]; never in tests
/// unless they override it.
final appResumedAfterBackgroundProvider = Provider<Stream<void>>((ref) => const Stream.empty());

/// Turns the app lifecycle into "resumed after more than [threshold] in the
/// background" events. [onHidden] / [onResumed] are public so tests can drive
/// it without a binding; [attach] wires them to an [AppLifecycleListener].
class AppResumeWatcher {
  AppResumeWatcher({this.threshold = const Duration(seconds: 30), DateTime Function()? clock})
    : _clock = clock ?? DateTime.now;

  final Duration threshold;
  final DateTime Function() _clock;
  final _resumed = StreamController<void>.broadcast();
  DateTime? _hiddenAt;
  AppLifecycleListener? _listener;

  Stream<void> get resumedAfterBackground => _resumed.stream;

  /// Starts listening to the app lifecycle (needs the widgets binding).
  void attach() {
    _listener ??= AppLifecycleListener(onHide: onHidden, onResume: onResumed);
  }

  void onHidden() => _hiddenAt ??= _clock();

  void onResumed() {
    final hiddenAt = _hiddenAt;
    _hiddenAt = null;
    if (hiddenAt != null && _clock().difference(hiddenAt) > threshold) _resumed.add(null);
  }

  void dispose() {
    _listener?.dispose();
    _listener = null;
    unawaited(_resumed.close());
  }
}

@immutable
class ConnectivityStatus {
  const ConnectivityStatus({
    this.hasNetwork = true,
    this.lastRequestFailed = false,
    this.network = NetworkInterfaces.unknown,
    this.networkGeneration = 0,
  });

  /// A network interface is up (according to the platform).
  final bool hasNetwork;

  /// The last request could not reach the server (no answer at all).
  final bool lastRequestFailed;

  /// The interfaces that are up now.
  final NetworkInterfaces network;

  /// Grows every time the network changes (any change of [network], not only
  /// connected / disconnected) or the app returns from a long time in the
  /// background. Connections opened before a change may be bound to an
  /// interface or address that is gone, so the HTTP client and the realtime
  /// hub drop theirs when it moves.
  final int networkGeneration;

  bool get isOffline => !hasNetwork || lastRequestFailed;

  ConnectivityStatus copyWith({
    bool? hasNetwork,
    bool? lastRequestFailed,
    NetworkInterfaces? network,
    int? networkGeneration,
  }) => ConnectivityStatus(
    hasNetwork: hasNetwork ?? this.hasNetwork,
    lastRequestFailed: lastRequestFailed ?? this.lastRequestFailed,
    network: network ?? this.network,
    networkGeneration: networkGeneration ?? this.networkGeneration,
  );

  @override
  bool operator ==(Object other) =>
      other is ConnectivityStatus &&
      other.hasNetwork == hasNetwork &&
      other.lastRequestFailed == lastRequestFailed &&
      other.network == network &&
      other.networkGeneration == networkGeneration;

  @override
  int get hashCode => Object.hash(hasNetwork, lastRequestFailed, network, networkGeneration);
}

/// Connection state of the app: the platform's network interfaces plus the
/// outcome of the last request (a Wi-Fi without route to the server counts as
/// offline). The HTTP client reports every request.
class ConnectivityController extends Notifier<ConnectivityStatus> {
  /// Last interfaces reported by the platform (null until the first answer).
  NetworkInterfaces? _known;

  @override
  ConnectivityStatus build() {
    _known = null;
    final source = ref.watch(connectivitySourceProvider);
    final subscription = source.changes.listen(_onInterfaces, onError: (Object _) {});
    ref.onDispose(subscription.cancel);
    final resumes = ref.watch(appResumedAfterBackgroundProvider).listen((_) => networkChanged());
    ref.onDispose(resumes.cancel);
    unawaited(_initialCheck(source));
    return const ConnectivityStatus();
  }

  Future<void> _initialCheck(ConnectivitySource source) async {
    try {
      final interfaces = await source.current();
      // A change may have arrived first; it is more recent.
      if (!ref.mounted || _known != null) return;
      _known = interfaces;
      _set(state.copyWith(hasNetwork: interfaces.hasNetwork, network: interfaces));
    } catch (_) {
      // Unknown: keep assuming there is network.
    }
  }

  void _onInterfaces(NetworkInterfaces interfaces) {
    final previous = _known;
    _known = interfaces;
    if (previous == interfaces) return;
    // A new interface gives the server another chance; the first answer of the
    // platform is not a change.
    _set(
      ConnectivityStatus(
        hasNetwork: interfaces.hasNetwork,
        network: interfaces,
        networkGeneration: previous == null ? state.networkGeneration : state.networkGeneration + 1,
      ),
    );
  }

  /// Signals that the connections opened so far may be stale (the app was in
  /// the background for a while).
  void networkChanged() => _set(
    state.copyWith(lastRequestFailed: false, networkGeneration: state.networkGeneration + 1),
  );

  void reportRequestFailed() => _set(state.copyWith(lastRequestFailed: true));

  /// The server answered, so there is network whatever the platform said.
  void reportRequestSucceeded() => _set(state.copyWith(hasNetwork: true, lastRequestFailed: false));

  void _set(ConnectivityStatus next) {
    if (ref.mounted && next != state) state = next;
  }
}

final connectivityProvider = NotifierProvider<ConnectivityController, ConnectivityStatus>(
  ConnectivityController.new,
);

/// Writes need the server: false while offline (ADR 0002).
final canWriteProvider = Provider<bool>((ref) => !ref.watch(connectivityProvider).isOffline);
