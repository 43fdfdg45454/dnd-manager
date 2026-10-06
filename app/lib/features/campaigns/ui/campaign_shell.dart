import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/cache/stale_data.dart';
import '../../../core/network/api_error.dart';
import '../../../core/realtime/connection_banner.dart';
import '../../../core/realtime/realtime_events.dart';
import '../../../core/realtime/realtime_provider.dart';
import '../../../core/realtime/realtime_status_icon.dart';
import '../../../core/router/app_router.dart';
import '../../../core/theme/app_icon.dart';
import '../../../core/theme/icons.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../characters/data/characters_controller.dart';
import '../../session/data/session_controllers.dart';
import '../data/campaigns_controller.dart';
import '../data/campaigns_repository.dart';
import '../domain/campaign_models.dart';
import 'general/campaign_section_page.dart';

/// Index of each branch of the campaign shell (see `campaignShellRoutes`).
abstract final class CampaignBranches {
  static const general = 0;
  static const dm = 1;
  static const player = 2;
}

enum _MenuAction { settings, documents }

/// Frame of a campaign: app bar (name, real-time status, transactions, change
/// requests and menu) and a navigation bar with "General" plus "Mesa del DM"
/// (DM and Owner) or "Mi sesión" (Player).
///
/// While mounted it keeps the realtime connection of the campaign open
/// ([campaignRealtimeProvider]) and tells players when the DM forces a rest or
/// sends them a secret message.
class CampaignShell extends ConsumerStatefulWidget {
  const CampaignShell({super.key, required this.campaignId, required this.navigationShell});

  final String campaignId;
  final StatefulNavigationShell navigationShell;

  @override
  ConsumerState<CampaignShell> createState() => _CampaignShellState();
}

class _CampaignShellState extends ConsumerState<CampaignShell> {
  ProviderSubscription<Object?>? _realtime;
  StreamSubscription<CampaignEvent>? _notices;

  String get campaignId => widget.campaignId;
  StatefulNavigationShell get navigationShell => widget.navigationShell;

  @override
  void initState() {
    super.initState();
    _listenRealtime();
  }

  @override
  void didUpdateWidget(CampaignShell oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.campaignId != widget.campaignId) {
      _stopRealtime();
      _listenRealtime();
    }
  }

  @override
  void dispose() {
    _stopRealtime();
    super.dispose();
  }

  void _listenRealtime() {
    _realtime = ref.listenManual(campaignRealtimeProvider(campaignId), (_, _) {});
    _notices = ref.read(realtimeHubProvider).events.listen(_onRealtimeEvent);
  }

  void _stopRealtime() {
    _realtime?.close();
    _realtime = null;
    unawaited(_notices?.cancel());
    _notices = null;
  }

  /// The user is no longer a member: back to the campaign list with a notice.
  void _onMembershipRemoved() {
    ref.invalidate(campaignsControllerProvider);
    final messenger = ScaffoldMessenger.maybeOf(context);
    final router = GoRouter.of(context);
    router.go(AppRoutes.home);
    messenger
      ?..hideCurrentSnackBar()
      ..showSnackBar(
        const SnackBar(
          key: Key('realtime-notice-removed'),
          content: Text('Ya no formas parte de esta campaña'),
        ),
      );
  }

  /// "¡Puedes subir a nivel N!" for the owner of the character the DM granted
  /// a level to (the event also reaches the rest of the campaign).
  Future<void> _announceLevelUp(String? characterId) async {
    if (characterId == null) return;
    final auth = ref.read(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : null;
    if (myUserId == null) return;
    final known = ref.read(campaignCharactersControllerProvider(campaignId)).value;
    if (known != null &&
        !known.any(
          (c) => c.id.toLowerCase() == characterId.toLowerCase() && c.ownerUserId == myUserId,
        )) {
      return;
    }
    try {
      final character = await ref.read(characterControllerProvider(characterId).future);
      final level = character.pendingLevelUpTo;
      if (!mounted || character.ownerUserId != myUserId || level == null) return;
      ScaffoldMessenger.maybeOf(context)
        ?..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            key: const Key('realtime-notice-level-up'),
            content: Text('¡Puedes subir a nivel $level!'),
            action: SnackBarAction(
              label: 'Ver',
              onPressed: () => navigationShell.goBranch(CampaignBranches.player),
            ),
          ),
        );
    } catch (_) {
      // Not the owner (no access to the sheet) or offline: the card of "Mi
      // sesión" shows the grant when the sheet loads.
    }
  }

  /// Banner for players (the DM is the one who caused these events).
  void _onRealtimeEvent(CampaignEvent event) {
    if (!mounted || !event.isFor(campaignId)) return;
    if (event is MembershipRemoved) {
      _onMembershipRemoved();
      return;
    }
    final role = ref.read(campaignDetailControllerProvider(campaignId)).value?.myRole;
    if (role == null || role.isAtLeastDm) return;
    if (event is LevelUpGranted) {
      unawaited(_announceLevelUp(event.characterId));
      return;
    }
    final (key, text) = switch (event) {
      MessageReceived() => ('realtime-notice-message', 'Mensaje del DM'),
      PartyRest() => ('realtime-notice-rest', 'El DM ha declarado un descanso'),
      _ => (null, null),
    };
    if (key == null || text == null) return;
    ScaffoldMessenger.maybeOf(context)
      ?..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          key: Key(key),
          content: Text(text),
          action: event is MessageReceived
              ? SnackBarAction(
                  label: 'Ver',
                  onPressed: () => navigationShell.goBranch(CampaignBranches.player),
                )
              : null,
        ),
      );
  }

  void _onMenu(BuildContext context, _MenuAction action) => switch (action) {
    _MenuAction.settings => context.push(
      AppRoutes.campaignSection(campaignId, CampaignSection.settings),
    ),
    _MenuAction.documents => context.push(AppRoutes.campaignDocuments(campaignId)),
  };

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(campaignDetailControllerProvider(campaignId));
    final campaign = detail.value;
    final role = campaign?.myRole;

    return Scaffold(
      appBar: AppBar(
        title: Text(campaign?.name ?? 'Campaña', key: const Key('campaign-title')),
        actions: [
          RealtimeStatusIcon(campaignId: campaignId),
          if (campaign != null) ...[
            IconButton(
              key: const Key('transactions-button'),
              tooltip: 'Transacciones',
              onPressed: () => context.push(AppRoutes.transactions(campaignId)),
              icon: const Icon(Icons.receipt_long_outlined),
            ),
            ChangeRequestsButton(campaign: campaign),
          ],
          PopupMenuButton<_MenuAction>(
            key: const Key('campaign-menu'),
            onSelected: (action) => _onMenu(context, action),
            itemBuilder: (_) => const [
              PopupMenuItem(
                key: Key('campaign-menu-settings'),
                value: _MenuAction.settings,
                child: Text('Ajustes de la campaña'),
              ),
              PopupMenuItem(
                key: Key('campaign-menu-documents'),
                value: _MenuAction.documents,
                child: Text('Documentos recomendados'),
              ),
            ],
          ),
        ],
      ),
      body: Column(
        children: [
          ConnectionBanner(campaignId: campaignId),
          Expanded(
            child: OfflineBannerLayout(
              scopes: [staleTree(CampaignsRepository.campaignPath(campaignId))],
              child: detail.when(
                skipLoadingOnReload: true,
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (error, _) => Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(describeCampaignError(error), textAlign: TextAlign.center),
                        const SizedBox(height: 12),
                        FilledButton.icon(
                          onPressed: () =>
                              ref.invalidate(campaignDetailControllerProvider(campaignId)),
                          icon: const Icon(Icons.refresh),
                          label: const Text('Reintentar'),
                        ),
                      ],
                    ),
                  ),
                ),
                data: (_) => navigationShell,
              ),
            ),
          ),
        ],
      ),
      bottomNavigationBar: role == null
          ? null
          : _ModeBar(campaignId: campaignId, role: role, navigationShell: navigationShell),
    );
  }
}

class _ModeBar extends ConsumerWidget {
  const _ModeBar({required this.campaignId, required this.role, required this.navigationShell});

  final String campaignId;
  final CampaignRole role;
  final StatefulNavigationShell navigationShell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isDm = role.isAtLeastDm;
    final modeBranch = isDm ? CampaignBranches.dm : CampaignBranches.player;
    final unread = isDm ? 0 : (ref.watch(unreadMessagesCountProvider(campaignId)).value ?? 0);
    final current = navigationShell.currentIndex;

    return NavigationBar(
      key: const Key('campaign-mode-bar'),
      selectedIndex: current == CampaignBranches.general ? 0 : 1,
      onDestinationSelected: (index) {
        final branch = index == 0 ? CampaignBranches.general : modeBranch;
        navigationShell.goBranch(branch, initialLocation: branch == current);
      },
      destinations: [
        const NavigationDestination(
          key: Key('nav-general'),
          icon: AppIcon(AppIcons.compass),
          label: 'General',
        ),
        if (isDm)
          const NavigationDestination(
            key: Key('nav-dm'),
            icon: AppIcon(AppIcons.crown),
            label: 'Mesa del DM',
          )
        else
          NavigationDestination(
            key: const Key('nav-player'),
            icon: Badge(
              key: const Key('nav-player-badge'),
              isLabelVisible: unread > 0,
              label: Text('$unread'),
              child: const AppIcon(AppIcons.hood),
            ),
            label: 'Mi sesión',
          ),
      ],
    );
  }
}

/// App bar shortcut to the change requests; DMs see the pending count as a badge.
class ChangeRequestsButton extends ConsumerWidget {
  const ChangeRequestsButton({super.key, required this.campaign});

  final CampaignDetail campaign;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isDm = campaign.myRole.isAtLeastDm;
    final pending = isDm ? ref.watch(pendingChangeRequestCountProvider(campaign.id)) : 0;
    return IconButton(
      key: const Key('change-requests-button'),
      tooltip: 'Solicitudes de cambio',
      onPressed: () => context.push(AppRoutes.changeRequests(campaign.id)),
      icon: Badge(
        key: const Key('change-requests-badge'),
        isLabelVisible: pending > 0,
        label: Text('$pending'),
        child: const Icon(Icons.fact_check_outlined),
      ),
    );
  }
}
