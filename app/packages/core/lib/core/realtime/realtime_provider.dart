import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/campaigns/data/campaigns_controller.dart';
import '../../features/characters/data/character_refresh.dart';
import '../../features/characters/data/characters_controller.dart';
import '../../features/content_packs/data/campaign_content_packs_controller.dart';
import '../../features/items/data/items_controllers.dart';
import '../../features/session/data/session_controllers.dart';
import '../../features/sessions/data/sessions_controllers.dart';
import '../auth/auth_controller.dart';
import '../auth/auth_repository.dart';
import '../auth/auth_state.dart';
import '../auth/token_storage.dart';
import '../catalog/catalog_sources.dart';
import '../network/connectivity.dart';
import '../server/server_config_controller.dart';
import '../server/server_url.dart';
import '../systems/system_registry.dart';
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

/// What the app bar icon and the connection banner show.
@immutable
class RealtimeState {
  const RealtimeState({required this.status, this.nextRetryAt, this.lastConnectedAt});

  final RealtimeStatus status;

  /// When the next automatic attempt happens (null when none is scheduled).
  final DateTime? nextRetryAt;

  /// Last time the hub was connected (null: never in this session).
  final DateTime? lastConnectedAt;

  RealtimeState copyWith({
    RealtimeStatus? status,
    DateTime? Function()? nextRetryAt,
    DateTime? lastConnectedAt,
  }) => RealtimeState(
    status: status ?? this.status,
    nextRetryAt: nextRetryAt == null ? this.nextRetryAt : nextRetryAt(),
    lastConnectedAt: lastConnectedAt ?? this.lastConnectedAt,
  );

  @override
  bool operator ==(Object other) =>
      other is RealtimeState &&
      other.status == status &&
      other.nextRetryAt == nextRetryAt &&
      other.lastConnectedAt == lastConnectedAt;

  @override
  int get hashCode => Object.hash(status, nextRetryAt, lastConnectedAt);
}

/// Realtime link of one campaign, alive while its shell is mounted: connects
/// the [RealtimeHub] to the campaign, keeps the connection state (the app bar
/// icon and the banner) and refreshes the data each event touches. Without
/// network or session it does not try and reports [RealtimeStatus.offline];
/// everything else comes from the hub (never from the outcome of HTTP
/// requests). A failed connection is retried with a growing delay, or at once
/// with [retryNow].
class CampaignRealtime extends Notifier<RealtimeState> {
  CampaignRealtime(this.campaignId);

  final String campaignId;

  _Connection? _link;

  /// Waits between failed attempts (the last one repeats).
  static const retryDelays = [
    Duration(seconds: 2),
    Duration(seconds: 5),
    Duration(seconds: 15),
    Duration(seconds: 30),
    Duration(seconds: 60),
  ];

  @override
  RealtimeState build() {
    final hasNetwork = ref.watch(connectivityProvider.select((s) => s.hasNetwork));
    final signedIn = ref.watch(authControllerProvider.select((s) => s is AuthSignedIn));
    _link = null;
    if (!hasNetwork || !signedIn) return const RealtimeState(status: RealtimeStatus.offline);

    final link = _Connection(ref.watch(realtimeHubProvider));
    _link = link;
    final events = link.hub.events.listen((event) => _onEvent(link, event));
    final statuses = link.hub.statusChanges.listen((status) => _onStatus(link, status));
    ref.onDispose(() {
      link.alive = false;
      link.retry?.cancel();
      unawaited(events.cancel());
      unawaited(statuses.cancel());
      if (link.hub.campaignId == campaignId) unawaited(link.hub.disconnect());
    });
    // Another network (Wi-Fi <-> mobile data, another Wi-Fi) or back from a
    // long time in the background: the connection may be bound to an
    // interface or address that is gone, so a new one is opened at once.
    ref.listen(
      connectivityProvider.select((s) => s.networkGeneration),
      (_, _) => unawaited(_restart(link)),
    );
    unawaited(_connect(link));
    return const RealtimeState(status: RealtimeStatus.connecting);
  }

  Future<void> _restart(_Connection link) async {
    if (!link.alive) return;
    link.retry?.cancel();
    link.retry = null;
    link.attempt = 0;
    state = state.copyWith(status: RealtimeStatus.connecting, nextRetryAt: () => null);
    try {
      await link.hub.restart();
    } catch (_) {
      if (!link.alive) return;
      state = state.copyWith(status: RealtimeStatus.disconnected);
      _scheduleRetry(link);
    }
  }

  /// Tries to connect now instead of waiting for the next scheduled attempt.
  /// Does nothing without network or session.
  Future<void> retryNow() async {
    final link = _link;
    if (link == null || !link.alive || state.status == RealtimeStatus.connected) return;
    link.retry?.cancel();
    link.retry = null;
    link.attempt = 0;
    state = state.copyWith(status: RealtimeStatus.connecting, nextRetryAt: () => null);
    await _connect(link);
  }

  Future<void> _connect(_Connection link) async {
    try {
      await link.hub.connect(campaignId);
    } catch (_) {
      if (!link.alive) return;
      state = state.copyWith(status: RealtimeStatus.disconnected);
      _scheduleRetry(link);
    }
  }

  void _scheduleRetry(_Connection link) {
    if (!link.alive || (link.retry?.isActive ?? false)) return;
    final delay = retryDelays[link.attempt.clamp(0, retryDelays.length - 1)];
    link.attempt++;
    state = state.copyWith(nextRetryAt: () => DateTime.now().add(delay));
    link.retry = Timer(delay, () {
      link.retry = null;
      if (!link.alive) return;
      state = state.copyWith(status: RealtimeStatus.connecting, nextRetryAt: () => null);
      unawaited(_connect(link));
    });
  }

  void _onStatus(_Connection link, RealtimeStatus status) {
    if (!link.alive || link.hub.campaignId != campaignId) return;
    var next = state.copyWith(status: status);
    switch (status) {
      case RealtimeStatus.connected:
        link.retry?.cancel();
        link.retry = null;
        link.attempt = 0;
        next = next.copyWith(nextRetryAt: () => null, lastConnectedAt: DateTime.now());
        // Events may have been missed while the connection was down.
        if (link.wasConnected) _refreshAll();
        link.wasConnected = true;
      case RealtimeStatus.disconnected:
        state = next;
        _scheduleRetry(link);
        return;
      case RealtimeStatus.offline || RealtimeStatus.connecting || RealtimeStatus.reconnecting:
        break;
    }
    state = next;
  }

  void _onEvent(_Connection link, CampaignEvent event) {
    if (!link.alive || !event.isFor(campaignId)) return;
    switch (event) {
      case CharacterUpdated(:final characterId):
        _refreshCharacters(characterId);
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
      case ChangeRequestResolved(:final characterId):
        // The shell tells the requester; the sheet and its pending requests changed.
        ref.invalidate(changeRequestsControllerProvider);
        _refreshCharacters(characterId);
      case InvitationReceived():
        ref.invalidate(myInvitationsControllerProvider);
      case MembersUpdated():
        ref.invalidate(campaignInvitationsProvider(campaignId));
        ref.invalidate(myInvitationsControllerProvider);
        ref.read(campaignDetailControllerProvider(campaignId).notifier).refreshMembers();
      case MessageReceived():
        ref.invalidate(messagesControllerProvider(campaignId));
        ref.invalidate(unreadMessagesCountProvider(campaignId));
      case SessionUpdated(:final entityId):
        _refreshSessions(entityId);
      case RestRequestUpdated(:final characterId):
        // The DM's petitions, and the pending rest of the sheet and the roster.
        ref.invalidate(restRequestsControllerProvider(campaignId));
        _refreshCharacters(characterId);
      case MembershipRemoved():
        // The shell leaves the campaign; its list must not show it any more.
        ref.invalidate(campaignsControllerProvider);
      case CampaignUpdated():
        _refreshContentPacks();
      case UnknownCampaignEvent():
        // Events of the game system of the campaign (D&D 5e: party rests and
        // granted levels); ignored when the system does not know them.
        ref.read(campaignSystemUiProvider(campaignId)).onRealtimeEvent(ref, event);
    }
  }

  /// The packs of the campaign changed: its pack list, its catalog (wizard,
  /// shops, compendium) and the sheets (a pick may now be of a disabled pack).
  void _refreshContentPacks() {
    ref.invalidate(campaignContentPacksControllerProvider(campaignId));
    ref.read(campaignCatalogRevisionProvider(campaignId).notifier).bump();
    _refreshCharacters(null);
  }

  /// [characterId] null: every character of the campaign.
  void _refreshCharacters(String? characterId) =>
      refreshCampaignCharacters(ref, campaignId, characterId);

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
    ref.invalidate(restRequestsControllerProvider(campaignId));
    ref.invalidate(shopsControllerProvider(campaignId));
    ref.invalidate(shopControllerProvider);
    ref.invalidate(stashControllerProvider(campaignId));
    ref.invalidate(changeRequestsControllerProvider);
    ref.invalidate(messagesControllerProvider(campaignId));
    ref.invalidate(unreadMessagesCountProvider(campaignId));
    _refreshSessions(null);
    ref.invalidate(campaignContentPacksControllerProvider(campaignId));
    ref.read(campaignCatalogRevisionProvider(campaignId).notifier).bump();
  }
}

/// Realtime status of a campaign; listening to it keeps the connection open
/// (the campaign shell does while it is mounted).
final campaignRealtimeProvider = NotifierProvider.autoDispose
    .family<CampaignRealtime, RealtimeState, String>(CampaignRealtime.new);
