import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/admin/ui/admin_content_page.dart';
import '../../features/admin/ui/admin_users_page.dart';
import '../../features/auth/ui/forgot_password_page.dart';
import '../../features/auth/ui/login_page.dart';
import '../../features/auth/ui/splash_page.dart';
import '../../features/campaigns/data/campaigns_controller.dart';
import '../../features/campaigns/domain/campaign_models.dart';
import '../../features/campaigns/ui/campaign_shell.dart';
import '../../features/campaigns/ui/general/campaign_general_page.dart';
import '../../features/campaigns/ui/general/campaign_section_page.dart';
import '../../features/change_requests/ui/change_requests_page.dart';
import '../../features/characters/ui/character_page.dart';
import '../../features/characters/ui/sheet_editor_page.dart';
import '../../features/characters/ui/wizard/character_wizard_page.dart';
import '../../features/catalog/ui/class_detail_page.dart';
import '../../features/catalog/ui/compendium_page.dart';
import '../../features/catalog/ui/item_detail_page.dart';
import '../../features/catalog/ui/race_detail_page.dart';
import '../../features/catalog/ui/spell_detail_page.dart';
import '../../features/home/ui/attribution_page.dart';
import '../../features/home/ui/home_page.dart';
import '../../features/library/ui/library_page.dart';
import '../../features/library/ui/pdf_viewer_page.dart';
import '../../features/lore/ui/lore_editor_page.dart';
import '../../features/lore/ui/lore_entry_page.dart';
import '../../features/maps/ui/map_viewer_page.dart';
import '../../features/server/ui/server_page.dart';
import '../../features/sessions/ui/session_form_page.dart';
import '../../features/sessions/ui/session_page.dart';
import '../../features/session/ui/dm/dm_session_page.dart';
import '../../features/session/ui/player/player_session_page.dart';
import '../../features/sessions/ui/summary_editor_page.dart';
import '../../features/items/ui/shop_page.dart';
import '../../features/items/ui/transactions_page.dart';
import '../auth/auth_controller.dart';
import '../auth/auth_state.dart';
import '../server/server_config_controller.dart';

abstract final class AppRoutes {
  static const splash = '/splash';
  static const server = '/server';
  static const login = '/login';
  static const forgotPassword = '/forgot-password';
  static const home = '/';
  static const adminUsers = '/admin/users';
  static const adminContent = '/admin/content';
  static const attributions = '/attributions';
  static const campaignDetail = '/campaigns/:id';
  static const campaignGeneral = '/campaigns/:id/general';
  static const campaignDm = '/campaigns/:id/dm';
  static const campaignPlayer = '/campaigns/:id/player';
  static const campaignChangeRequests = '/campaigns/:id/change-requests';
  static const campaignShop = '/campaigns/:id/shops/:shopId';
  static const campaignTransactions = '/campaigns/:id/transactions';
  static const campaignCharacterNew = '/campaigns/:id/characters/new';
  static const characterDetail = '/characters/:id';
  static const characterEditor = '/characters/:id/edit';
  static const campaignLoreNew = '/campaigns/:id/lore/new';
  static const campaignLoreEntry = '/campaigns/:id/lore/:entryId';
  static const campaignLoreEdit = '/campaigns/:id/lore/:entryId/edit';
  static const campaignMap = '/campaigns/:id/maps/:mapId';
  static const campaignSessionNew = '/campaigns/:id/sessions/new';
  static const campaignSession = '/campaigns/:id/sessions/:sessionId';
  static const campaignSessionEdit = '/campaigns/:id/sessions/:sessionId/edit';
  static const campaignSessionSummary = '/campaigns/:id/sessions/:sessionId/summary';
  static const campaignLibrary = '/campaigns/:id/library';
  static const library = '/library';
  static const libraryViewer = '/library/:docId/view';
  static const compendium = '/compendium';
  static const spellDetail = '/compendium/spells/:index';
  static const itemDetail = '/compendium/items/:id';
  static const classDetail = '/compendium/classes/:index';
  static const raceDetail = '/compendium/races/:index';

  /// Location of the campaign with the given [id]: its "General" view.
  static String campaign(String id) => '/campaigns/$id/general';

  /// "Mesa del DM" view of the campaign (DM and Owner).
  static String campaignDmView(String id) => '/campaigns/$id/dm';

  /// "Mi sesión" view of the campaign (Player).
  static String campaignPlayerView(String id) => '/campaigns/$id/player';

  /// A section of the "General" view, as a full page.
  static String campaignSection(String id, CampaignSection section) =>
      '/campaigns/$id/general/${section.path}';

  /// Change requests of the campaign with the given [id].
  static String changeRequests(String id) => '/campaigns/$id/change-requests';

  /// One shop of the campaign.
  static String shop(String campaignId, String shopId) => '/campaigns/$campaignId/shops/$shopId';

  /// Transaction history of the campaign.
  static String transactions(String campaignId) => '/campaigns/$campaignId/transactions';

  /// A lore entry of the campaign.
  static String loreEntry(String campaignId, String entryId) =>
      '/campaigns/$campaignId/lore/$entryId';

  /// Editor of a new lore entry; [parentId] preselects its parent.
  static String loreNew(String campaignId, {String? parentId}) =>
      '/campaigns/$campaignId/lore/new${parentId == null ? '' : '?parentId=$parentId'}';

  static String loreEdit(String campaignId, String entryId) =>
      '/campaigns/$campaignId/lore/$entryId/edit';

  /// A session of the campaign.
  static String session(String campaignId, String sessionId) =>
      '/campaigns/$campaignId/sessions/$sessionId';

  /// Form to schedule a new session.
  static String sessionNew(String campaignId) => '/campaigns/$campaignId/sessions/new';

  static String sessionEdit(String campaignId, String sessionId) =>
      '/campaigns/$campaignId/sessions/$sessionId/edit';

  /// Editor of the journal summary of a session.
  static String sessionSummary(String campaignId, String sessionId) =>
      '/campaigns/$campaignId/sessions/$sessionId/summary';

  /// A map of the campaign.
  static String map(String campaignId, String mapId) => '/campaigns/$campaignId/maps/$mapId';

  /// Documents recommended in the campaign (on top of the whole library).
  static String campaignDocuments(String campaignId) => '/campaigns/$campaignId/library';

  /// PDF viewer of the library document [docId].
  static String libraryDocument(String docId) => '/library/$docId/view';

  /// Creation wizard of a new character; DMs may preselect the owner.
  static String characterNew(String campaignId, {String? ownerUserId}) =>
      '/campaigns/$campaignId/characters/new'
      '${ownerUserId == null ? '' : '?ownerUserId=${Uri.encodeQueryComponent(ownerUserId)}'}';

  static String character(String id) => '/characters/$id';

  static String characterEdit(String id) => '/characters/$id/edit';

  static String spell(String index) => '/compendium/spells/${Uri.encodeComponent(index)}';

  static String item(String id) => '/compendium/items/${Uri.encodeComponent(id)}';

  static String dndClass(String index) => '/compendium/classes/${Uri.encodeComponent(index)}';

  static String race(String index) => '/compendium/races/${Uri.encodeComponent(index)}';
}

/// Computes the redirect target for [location] given the session [auth] state,
/// or null when the location is allowed. Without a configured server
/// ([hasServer] false) everything goes to the server screen, which is otherwise
/// reachable in every session state.
String? authRedirect(AuthState auth, String location, {bool hasServer = true}) {
  if (!hasServer) return location == AppRoutes.server ? null : AppRoutes.server;
  if (location == AppRoutes.server) return null;
  switch (auth) {
    case AuthUnknown():
      return location == AppRoutes.splash ? null : AppRoutes.splash;
    case AuthSignedOut():
      const open = {AppRoutes.login, AppRoutes.forgotPassword};
      return open.contains(location) ? null : AppRoutes.login;
    case AuthSignedIn(:final user):
      const guestOnly = {AppRoutes.splash, AppRoutes.login, AppRoutes.forgotPassword};
      if (guestOnly.contains(location)) return AppRoutes.home;
      if (location.startsWith('/admin') && !user.isAdmin) return AppRoutes.home;
      return null;
  }
}

/// Id of the campaign of [location] (`/campaigns/<id>/...`), or null.
String? campaignIdOf(String location) {
  final segments = Uri.parse(location).pathSegments.where((s) => s.isNotEmpty).toList();
  if (segments.length < 2 || segments.first != 'campaigns') return null;
  return segments[1];
}

/// Keeps every user in the campaign views of their [role]: `/campaigns/:id`
/// goes to the "General" view, the DM view (`/dm`) is only for DM and Owner
/// and the player view (`/player`) only for Players; the others are sent to
/// "General". Returns null when [location] is allowed or the role is not
/// known yet (null), in which case the router asks again once it is.
String? campaignModeRedirect(CampaignRole? role, String location) {
  final segments = Uri.parse(location).pathSegments.where((s) => s.isNotEmpty).toList();
  if (segments.length < 2 || segments.first != 'campaigns') return null;
  final id = segments[1];
  if (segments.length == 2) return AppRoutes.campaign(id);
  if (role == null) return null;
  return switch (segments[2]) {
    'dm' when !role.isAtLeastDm => AppRoutes.campaign(id),
    'player' when role != CampaignRole.player => AppRoutes.campaign(id),
    _ => null,
  };
}

/// Routes of a campaign: `/campaigns/:id` (redirected to "General") and the
/// shell with the "General", "Mesa del DM" and "Mi sesión" branches. The
/// sections of "General" are full pages on [rootNavigatorKey].
List<RouteBase> campaignShellRoutes(GlobalKey<NavigatorState> rootNavigatorKey) => [
  GoRoute(
    path: AppRoutes.campaignDetail,
    redirect: (context, state) => campaignModeRedirect(null, state.uri.path),
    routes: [
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) => CampaignShell(
          campaignId: state.pathParameters['id']!,
          navigationShell: navigationShell,
        ),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: 'general',
                builder: (context, state) =>
                    CampaignGeneralPage(campaignId: state.pathParameters['id']!),
                routes: [
                  for (final section in CampaignSection.values)
                    GoRoute(
                      path: section.path,
                      parentNavigatorKey: rootNavigatorKey,
                      builder: (context, state) => CampaignSectionPage(
                        campaignId: state.pathParameters['id']!,
                        section: section,
                      ),
                    ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: 'dm',
                builder: (context, state) => DmSessionPage(campaignId: state.pathParameters['id']!),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: 'player',
                builder: (context, state) =>
                    PlayerSessionPage(campaignId: state.pathParameters['id']!),
              ),
            ],
          ),
        ],
      ),
    ],
  ),
];

/// First location of the router; tests start the real router elsewhere.
final routerInitialLocationProvider = Provider<String>((ref) => AppRoutes.home);

final routerProvider = Provider<GoRouter>((ref) {
  // Re-evaluates the redirect whenever the session, the server or a known
  // campaign role changes.
  bool hasServer() => ref.read(serverConfigProvider).isConfigured;
  final refresh = ValueNotifier<int>(0);
  void bump() => refresh.value++;
  ref.listen<AuthState>(authControllerProvider, (_, _) => bump());
  ref.listen<bool>(serverConfigProvider.select((config) => config.isConfigured), (_, _) => bump());
  ref.listen<Map<String, CampaignRole>>(campaignRoleCacheProvider, (_, _) => bump());

  final rootNavigatorKey = GlobalKey<NavigatorState>();
  final router = GoRouter(
    navigatorKey: rootNavigatorKey,
    initialLocation: ref.read(routerInitialLocationProvider),
    refreshListenable: refresh,
    redirect: (context, state) {
      final location = state.uri.path;
      final auth = authRedirect(ref.read(authControllerProvider), location, hasServer: hasServer());
      if (auth != null) return auth;
      final campaignId = campaignIdOf(location);
      if (campaignId == null) return null;
      return campaignModeRedirect(ref.read(campaignRoleCacheProvider)[campaignId], location);
    },
    routes: [
      GoRoute(path: AppRoutes.server, builder: (context, state) => const ServerPage()),
      GoRoute(path: AppRoutes.splash, builder: (context, state) => const SplashPage()),
      GoRoute(path: AppRoutes.login, builder: (context, state) => const LoginPage()),
      GoRoute(
        path: AppRoutes.forgotPassword,
        builder: (context, state) => const ForgotPasswordPage(),
      ),
      GoRoute(path: AppRoutes.home, builder: (context, state) => const HomePage()),
      GoRoute(path: AppRoutes.adminUsers, builder: (context, state) => const AdminUsersPage()),
      GoRoute(path: AppRoutes.adminContent, builder: (context, state) => const AdminContentPage()),
      GoRoute(path: AppRoutes.attributions, builder: (context, state) => const AttributionPage()),
      ...campaignShellRoutes(rootNavigatorKey),
      GoRoute(
        path: AppRoutes.campaignChangeRequests,
        builder: (context, state) => ChangeRequestsPage(campaignId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: AppRoutes.campaignShop,
        builder: (context, state) => ShopPage(
          campaignId: state.pathParameters['id']!,
          shopId: state.pathParameters['shopId']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignTransactions,
        builder: (context, state) => TransactionsPage(campaignId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: AppRoutes.campaignCharacterNew,
        builder: (context, state) => CharacterWizardPage(
          campaignId: state.pathParameters['id']!,
          ownerUserId: state.uri.queryParameters['ownerUserId'],
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignLoreNew,
        builder: (context, state) => LoreEditorPage(
          campaignId: state.pathParameters['id']!,
          initialParentId: state.uri.queryParameters['parentId'],
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignLoreEntry,
        builder: (context, state) => LoreEntryPage(
          campaignId: state.pathParameters['id']!,
          entryId: state.pathParameters['entryId']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignLoreEdit,
        builder: (context, state) => LoreEditorPage(
          campaignId: state.pathParameters['id']!,
          entryId: state.pathParameters['entryId'],
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignMap,
        builder: (context, state) => MapViewerPage(
          campaignId: state.pathParameters['id']!,
          mapId: state.pathParameters['mapId']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignSessionNew,
        builder: (context, state) => SessionFormPage(campaignId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: AppRoutes.campaignSession,
        builder: (context, state) => SessionPage(
          campaignId: state.pathParameters['id']!,
          sessionId: state.pathParameters['sessionId']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignSessionEdit,
        builder: (context, state) => SessionFormPage(
          campaignId: state.pathParameters['id']!,
          sessionId: state.pathParameters['sessionId'],
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignSessionSummary,
        builder: (context, state) => SummaryEditorPage(
          campaignId: state.pathParameters['id']!,
          sessionId: state.pathParameters['sessionId']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignLibrary,
        builder: (context, state) => LibraryPage(campaignId: state.pathParameters['id']),
      ),
      GoRoute(path: AppRoutes.library, builder: (context, state) => const LibraryPage()),
      GoRoute(
        path: AppRoutes.libraryViewer,
        builder: (context, state) => PdfViewerPage(documentId: state.pathParameters['docId']!),
      ),
      GoRoute(
        path: AppRoutes.characterDetail,
        builder: (context, state) => CharacterPage(characterId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: AppRoutes.characterEditor,
        builder: (context, state) => SheetEditorPage(characterId: state.pathParameters['id']!),
      ),
      GoRoute(path: AppRoutes.compendium, builder: (context, state) => const CompendiumPage()),
      GoRoute(
        path: AppRoutes.spellDetail,
        builder: (context, state) => SpellDetailPage(index: state.pathParameters['index']!),
      ),
      GoRoute(
        path: AppRoutes.itemDetail,
        builder: (context, state) => ItemDetailPage(id: state.pathParameters['id']!),
      ),
      GoRoute(
        path: AppRoutes.classDetail,
        builder: (context, state) => ClassDetailPage(index: state.pathParameters['index']!),
      ),
      GoRoute(
        path: AppRoutes.raceDetail,
        builder: (context, state) => RaceDetailPage(index: state.pathParameters['index']!),
      ),
    ],
  );

  ref.onDispose(() {
    router.dispose();
    refresh.dispose();
  });
  return router;
});
