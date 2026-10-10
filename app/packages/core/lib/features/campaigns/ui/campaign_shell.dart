import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/cache/stale_data.dart';
import '../../../core/characters/models.dart';
import '../../../core/network/api_error.dart';
import '../../../core/realtime/connection_banner.dart';
import '../../../core/realtime/realtime_events.dart';
import '../../../core/realtime/realtime_provider.dart';
import '../../../core/realtime/realtime_status_icon.dart';
import '../../../core/router/app_router.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/theme/app_icon.dart';
import '../../../core/theme/icons.dart';
import '../../../core/theme/textures.dart';
import '../../../core/theme/tokens.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../characters/data/characters_controller.dart';
import '../../characters/data/characters_repository.dart';
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
  static const characters = 3;
}

enum _MenuAction { settings, documents }

/// Frame of a campaign: app bar (name, real-time status, transactions, change
/// requests and menu) and a navigation bar with "Mesa del DM" (DM and Owner)
/// or "Mi sesión" (Player), "Personajes" and "Campaña".
///
/// The app bar always offers the way back to Campañas, whatever the tab and
/// however the campaign was opened. The system back button first returns to
/// the tab of the role, then leaves the campaign (to the screen that opened it,
/// or to Campañas when the campaign was the first screen), never closing the
/// app.
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

  /// The character [characterId] when it belongs to the user (the game system
  /// tells its own notices about it), or null: someone else's, or offline.
  Future<CharacterDetail?> _ownCharacter(String characterId) async {
    final auth = ref.read(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : null;
    if (myUserId == null) return null;
    final known = ref.read(campaignCharactersControllerProvider(campaignId)).value;
    if (known != null &&
        !known.any(
          (c) => c.id.toLowerCase() == characterId.toLowerCase() && c.ownerUserId == myUserId,
        )) {
      return null;
    }
    try {
      final character = await ref.read(characterControllerProvider(characterId).future);
      return character.ownerUserId == myUserId ? character : null;
    } catch (_) {
      // Not the owner (no access to the sheet) or offline: the sheet shows
      // what changed when it loads.
      return null;
    }
  }

  /// The notice of an event of the game system (D&D 5e: "¡Puedes subir a
  /// nivel N!", "El DM ha declarado un descanso").
  Future<void> _announceSystemEvent(UnknownCampaignEvent event) async {
    final notice = await ref
        .read(campaignSystemUiProvider(campaignId))
        .playerNotice(event, ownCharacter: _ownCharacter);
    if (notice == null || !mounted) return;
    final key = 'realtime-notice-${notice.id}';
    final icon = notice.icon;
    final tokens = context.tokens;
    ScaffoldMessenger.maybeOf(context)
      ?..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          key: Key(key),
          content: icon == null
              ? Text(notice.text)
              : Row(
                  children: [
                    AppIcon(icon, key: Key('$key-icon'), size: 20, color: tokens.ember),
                    const SizedBox(width: 10),
                    Expanded(child: Text(notice.text)),
                  ],
                ),
          action: notice.openSession
              ? SnackBarAction(
                  label: 'Ver',
                  onPressed: () => navigationShell.goBranch(CampaignBranches.player),
                )
              : null,
        ),
      );
  }

  /// "El DM aceptó/rechazó …" for the requester, with a shortcut to the sheet.
  Future<void> _announceResolvedRequest(String? requestId) async {
    if (requestId == null) return;
    try {
      final request = await ref.read(charactersRepositoryProvider).changeRequest(requestId);
      if (!mounted) return;
      final verb = switch (request.status) {
        ChangeRequestStatus.approved => 'aceptó',
        ChangeRequestStatus.rejected => 'rechazó',
        _ => null,
      };
      if (verb == null) return;
      final what = request.type == ChangeRequestType.activate
          ? 'tu personaje ${request.characterName}'
          : 'tu solicitud (${request.type.label.toLowerCase()}) de ${request.characterName}';
      final comment = request.comment == null ? '' : ': ${request.comment}';
      ScaffoldMessenger.maybeOf(context)
        ?..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            key: const Key('realtime-notice-request-resolved'),
            content: Text('El DM $verb $what$comment'),
            duration: const Duration(seconds: 8),
            action: SnackBarAction(
              label: 'Ver',
              onPressed: () => context.push(AppRoutes.character(request.characterId)),
            ),
          ),
        );
    } catch (_) {
      // Offline or no longer visible: the sheet shows the outcome when it loads.
    }
  }

  /// Banner for players (the DM is the one who caused these events).
  void _onRealtimeEvent(CampaignEvent received) {
    if (!mounted || !received.isFor(campaignId)) return;
    final event = received;
    if (event is MembershipRemoved) {
      _onMembershipRemoved();
      return;
    }
    if (event is ChangeRequestResolved) {
      unawaited(_announceResolvedRequest(event.entityId));
      return;
    }
    final role = ref.read(campaignDetailControllerProvider(campaignId)).value?.myRole;
    if (role == null || role.isAtLeastDm) return;
    if (event is UnknownCampaignEvent) {
      unawaited(_announceSystemEvent(event));
      return;
    }
    if (event is! MessageReceived) return;
    const key = 'realtime-notice-message';
    final tokens = context.tokens;
    ScaffoldMessenger.maybeOf(context)
      ?..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          key: const Key(key),
          content: Row(
            children: [
              AppIcon(AppIcons.seal, key: const Key('$key-icon'), size: 20, color: tokens.blood),
              const SizedBox(width: 10),
              const Expanded(child: Text('Mensaje del DM')),
            ],
          ),
          action: SnackBarAction(
            label: 'Ver',
            onPressed: () => navigationShell.goBranch(CampaignBranches.player),
          ),
        ),
      );
  }

  /// Branch of the role: "Mesa del DM", "Mi sesión" or "Campaña" while unknown.
  int _roleBranch(CampaignRole? role) => switch (role) {
    null => CampaignBranches.general,
    CampaignRole.player => CampaignBranches.player,
    _ => CampaignBranches.dm,
  };

  /// Leaves the campaign for the Campañas tab.
  void _goHome() => GoRouter.of(context).go(AppRoutes.home);

  /// System back: the tab of the role first, then out of the campaign.
  void _onBack(CampaignRole? role) {
    final roleBranch = _roleBranch(role);
    if (navigationShell.currentIndex != roleBranch) {
      navigationShell.goBranch(roleBranch);
      return;
    }
    final navigator = Navigator.of(context, rootNavigator: true);
    if (navigator.canPop()) {
      navigator.pop();
    } else {
      _goHome();
    }
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

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _onBack(role);
      },
      child: Scaffold(
        appBar: AppBar(
          automaticallyImplyLeading: false,
          leading: IconButton(
            key: const Key('campaign-home'),
            tooltip: 'Campañas',
            onPressed: _goHome,
            icon: const AppIcon(AppIcons.castle),
          ),
          title: Text(campaign?.name ?? 'Campaña', key: const Key('campaign-title')),
          actions: [
            RealtimeStatusIcon(campaignId: campaignId),
            if (campaign != null) ...[
              IconButton(
                key: const Key('transactions-button'),
                tooltip: 'Transacciones',
                onPressed: () => context.push(AppRoutes.transactions(campaignId)),
                icon: const AppIcon(AppIcons.coins),
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
        body: GrainBackground(
          child: Column(
            children: [
              ConnectionBanner(campaignId: campaignId),
              Expanded(
                child: OfflineBannerLayout(
                  scopes: [
                    staleTree(CampaignsRepository.campaignPath(campaignId)),
                    if (ref.watch(campaignSystemUiProvider(campaignId)).staleScope(campaignId)
                        case final scope?)
                      staleTree(scope),
                  ],
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
        ),
        bottomNavigationBar: role == null
            ? null
            : _ModeBar(campaignId: campaignId, role: role, navigationShell: navigationShell),
      ),
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
    // Destinations in bar order; the role view comes first.
    final branches = [modeBranch, CampaignBranches.characters, CampaignBranches.general];
    final selected = branches.indexOf(current);

    return NavigationBar(
      key: const Key('campaign-mode-bar'),
      selectedIndex: selected < 0 ? 0 : selected,
      onDestinationSelected: (index) {
        final branch = branches[index];
        navigationShell.goBranch(branch, initialLocation: branch == current);
      },
      destinations: [
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
        const NavigationDestination(
          key: Key('nav-characters'),
          icon: AppIcon(AppIcons.users),
          label: 'Personajes',
        ),
        const NavigationDestination(
          key: Key('nav-general'),
          icon: AppIcon(AppIcons.compass),
          label: 'Campaña',
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
        child: const AppIcon(AppIcons.quill),
      ),
    );
  }
}
