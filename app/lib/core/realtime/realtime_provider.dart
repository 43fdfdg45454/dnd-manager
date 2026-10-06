import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/characters/data/characters_controller.dart';
import '../../features/items/data/items_controllers.dart';
import '../../features/session/data/session_controllers.dart';
import '../../features/sessions/data/sessions_controllers.dart';
import '../auth/auth_controller.dart';
import '../auth/auth_repository.dart';
import '../auth/auth_state.dart';
import '../auth/token_storage.dart';
import '../network/connectivity.dart';
import '../server/server_config_controller.dart';
import '../server/server_url.dart';
import 'realtime_events.dart';
import 'realtime_hub.dart';
import 'signalr_realtime_hub.dart';

/// The access token for the hub. When the stored one has expired (or is about
/// to) an authenticated request is made first so the HTTP client's
/// `AuthInterceptor` refreshes it with its shared lock (refreshing here
/// directly could race with it and reuse a rotated refresh token).
Future<String?> freshAccessToken(Ref ref) async {
  final storage = ref.read(tokenStorageProvider);
  try {
    final expiry = await storage.readAccessTokenExpiry();
    if (expiry != null && expiry.isBefore(DateTime.now().add(const Duration(seconds: 30)))) {
      await ref.read(authRepositoryProvider).me();
    }
  } catch (_) {
    // Offline or the session ended: the connection fails and is retried.
  }
  return storage.readAccessToken();
}

/// The app's [RealtimeHub] (SignalR to the configured server). Tests override
/// it with a fake.
final realtimeHubProvider = Provider<RealtimeHub>((ref) {
  final hub = SignalRRealtimeHub(
    endpoint: () {
      final config = ref.read(serverConfigProvider);
      final host = serverHost(config.baseUrl);
      return (
        baseUrl: config.baseUrl,
        pinnedFingerprint: host == null ? null : config.trustedFingerprints[host],
      );
    },
    accessToken: () => freshAccessToken(ref),
  );
  ref.onDispose(hub.dispose);
  return hub;
});

/// Mutable state of one build of [CampaignRealtime] (a rebuild starts over).
class _Connection {
  _Connection(this.hub);

  final RealtimeHub hub;
  bool alive = true;
  bool wasConnected = false;
  int attempt = 0;
  Timer? retry;
}

/// Realtime link of one campaign, alive while its shell is mounted: connects
/// the [RealtimeHub] to the campaign, keeps the connection status (the app bar
/// icon) and refreshes the data each event touches. Without network or session
/// it does not try and reports [RealtimeStatus.offline]; a failed connection
/// is retried with a growing delay.
class CampaignRealtime extends Notifier<RealtimeStatus> {
  CampaignRealtime(this.campaignId);

  final String campaignId;

  /// Waits between failed attempts (the last one repeats).
  static const retryDelays = [
    Duration(seconds: 2),
    Duration(seconds: 5),
    Duration(seconds: 15),
    Duration(seconds: 30),
    Duration(seconds: 60),
  ];

  @override
  RealtimeStatus build() {
    final offline = ref.watch(connectivityProvider.select((s) => s.isOffline));
    final signedIn = ref.watch(authControllerProvider.select((s) => s is AuthSignedIn));
    if (offline || !signedIn) return RealtimeStatus.offline;

    final link = _Connection(ref.watch(realtimeHubProvider));
    final events = link.hub.events.listen((event) => _onEvent(link, event));
    final statuses = link.hub.statusChanges.listen((status) => _onStatus(link, status));
    ref.onDispose(() {
      link.alive = false;
      link.retry?.cancel();
      unawaited(events.cancel());
      unawaited(statuses.cancel());
      if (link.hub.campaignId == campaignId) unawaited(link.hub.disconnect());
    });
    unawaited(_connect(link));
    return RealtimeStatus.connecting;
  }

  Future<void> _connect(_Connection link) async {
    try {
      await link.hub.connect(campaignId);
    } catch (_) {
      if (!link.alive) return;
      state = RealtimeStatus.disconnected;
      _scheduleRetry(link);
    }
  }

  void _scheduleRetry(_Connection link) {
    if (!link.alive || (link.retry?.isActive ?? false)) return;
    final delay = retryDelays[link.attempt.clamp(0, retryDelays.length - 1)];
    link.attempt++;
    link.retry = Timer(delay, () {
      link.retry = null;
      if (link.alive) unawaited(_connect(link));
    });
  }

  void _onStatus(_Connection link, RealtimeStatus status) {
    if (!link.alive || link.hub.campaignId != campaignId) return;
    switch (status) {
      case RealtimeStatus.connected:
        link.retry?.cancel();
        link.attempt = 0;
        // Events may have been missed while the connection was down.
        if (link.wasConnected) _refreshAll();
        link.wasConnected = true;
      case RealtimeStatus.disconnected:
        _scheduleRetry(link);
      case RealtimeStatus.offline || RealtimeStatus.connecting || RealtimeStatus.reconnecting:
        break;
    }
    state = status;
  }

  void _onEvent(_Connection link, CampaignEvent event) {
    if (!link.alive || !event.isFor(campaignId)) return;
    switch (event) {
      case CharacterUpdated(:final characterId):
        _refreshCharacters(characterId);
      case PartyRest():
        _refreshCharacters(null);
      case ShopUpdated(:final entityId):
        ref.invalidate(shopsControllerProvider(campaignId));
        entityId == null
            ? ref.invalidate(shopControllerProvider)
            : ref.invalidate(shopControllerProvider(entityId));
      case PartyStashUpdated():
        ref.invalidate(stashControllerProvider(campaignId));
      case ChangeRequestUpdated():
        // The pending counter of the app bar derives from these lists.
        ref.invalidate(changeRequestsControllerProvider);
      case MessageReceived():
        ref.invalidate(messagesControllerProvider(campaignId));
        ref.invalidate(unreadMessagesCountProvider(campaignId));
      case SessionUpdated(:final entityId):
        _refreshSessions(entityId);
      case Unknown():
        break;
    }
  }

  /// [characterId] null: every character of the campaign (a party rest).
  void _refreshCharacters(String? characterId) {
    if (characterId == null) {
      ref.invalidate(characterControllerProvider);
      ref.invalidate(inventoryControllerProvider);
    } else {
      ref.invalidate(characterControllerProvider(characterId));
      ref.invalidate(inventoryControllerProvider(characterId));
    }
    ref.invalidate(campaignCharactersControllerProvider(campaignId));
    ref.invalidate(partyControllerProvider(campaignId));
  }

  void _refreshSessions(String? sessionId) {
    ref.invalidate(sessionsControllerProvider(campaignId));
    sessionId == null
        ? ref.invalidate(sessionControllerProvider)
        : ref.invalidate(sessionControllerProvider(sessionId));
    ref.invalidate(journalControllerProvider(campaignId));
    ref.invalidate(mySessionsControllerProvider);
  }

  void _refreshAll() {
    _refreshCharacters(null);
    ref.invalidate(shopsControllerProvider(campaignId));
    ref.invalidate(shopControllerProvider);
    ref.invalidate(stashControllerProvider(campaignId));
    ref.invalidate(changeRequestsControllerProvider);
    ref.invalidate(messagesControllerProvider(campaignId));
    ref.invalidate(unreadMessagesCountProvider(campaignId));
    _refreshSessions(null);
  }
}

/// Realtime status of a campaign; listening to it keeps the connection open
/// (the campaign shell does while it is mounted).
final campaignRealtimeProvider = NotifierProvider.autoDispose
    .family<CampaignRealtime, RealtimeStatus, String>(CampaignRealtime.new);
