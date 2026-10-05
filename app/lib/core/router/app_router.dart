import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/admin/ui/admin_users_page.dart';
import '../../features/auth/ui/forgot_password_page.dart';
import '../../features/auth/ui/login_page.dart';
import '../../features/auth/ui/splash_page.dart';
import '../../features/campaigns/ui/campaign_detail_page.dart';
import '../../features/change_requests/ui/change_requests_page.dart';
import '../../features/characters/ui/character_page.dart';
import '../../features/characters/ui/sheet_editor_page.dart';
import '../../features/catalog/ui/class_detail_page.dart';
import '../../features/catalog/ui/compendium_page.dart';
import '../../features/catalog/ui/item_detail_page.dart';
import '../../features/catalog/ui/race_detail_page.dart';
import '../../features/catalog/ui/spell_detail_page.dart';
import '../../features/home/ui/home_page.dart';
import '../../features/library/ui/library_page.dart';
import '../../features/library/ui/pdf_viewer_page.dart';
import '../../features/lore/ui/lore_editor_page.dart';
import '../../features/lore/ui/lore_entry_page.dart';
import '../../features/maps/ui/map_viewer_page.dart';
import '../../features/server/ui/server_page.dart';
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
  static const campaignDetail = '/campaigns/:id';
  static const campaignChangeRequests = '/campaigns/:id/change-requests';
  static const campaignShop = '/campaigns/:id/shops/:shopId';
  static const campaignTransactions = '/campaigns/:id/transactions';
  static const characterDetail = '/characters/:id';
  static const characterEditor = '/characters/:id/edit';
  static const campaignLoreNew = '/campaigns/:id/lore/new';
  static const campaignLoreEntry = '/campaigns/:id/lore/:entryId';
  static const campaignLoreEdit = '/campaigns/:id/lore/:entryId/edit';
  static const campaignMap = '/campaigns/:id/maps/:mapId';
  static const campaignLibrary = '/campaigns/:id/library';
  static const library = '/library';
  static const libraryViewer = '/library/:docId/view';
  static const compendium = '/compendium';
  static const spellDetail = '/compendium/spells/:index';
  static const itemDetail = '/compendium/items/:id';
  static const classDetail = '/compendium/classes/:index';
  static const raceDetail = '/compendium/races/:index';

  /// Location of the campaign with the given [id].
  static String campaign(String id) => '/campaigns/$id';

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

  /// A map of the campaign.
  static String map(String campaignId, String mapId) => '/campaigns/$campaignId/maps/$mapId';

  /// Documents recommended in the campaign (on top of the whole library).
  static String campaignDocuments(String campaignId) => '/campaigns/$campaignId/library';

  /// PDF viewer of the library document [docId].
  static String libraryDocument(String docId) => '/library/$docId/view';

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

final routerProvider = Provider<GoRouter>((ref) {
  // Re-evaluates the redirect whenever the session or the server changes.
  bool hasServer() => ref.read(serverConfigProvider).isConfigured;
  final refresh = ValueNotifier<(AuthState, bool)>((ref.read(authControllerProvider), hasServer()));
  ref.listen<AuthState>(authControllerProvider, (_, next) => refresh.value = (next, hasServer()));
  ref.listen<bool>(
    serverConfigProvider.select((config) => config.isConfigured),
    (_, next) => refresh.value = (ref.read(authControllerProvider), next),
  );

  final router = GoRouter(
    initialLocation: AppRoutes.home,
    refreshListenable: refresh,
    redirect: (context, state) =>
        authRedirect(ref.read(authControllerProvider), state.uri.path, hasServer: hasServer()),
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
      GoRoute(
        path: AppRoutes.campaignDetail,
        builder: (context, state) => CampaignDetailPage(campaignId: state.pathParameters['id']!),
      ),
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
