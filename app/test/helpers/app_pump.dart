import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/auth/user_dto.dart';
import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/core/theme/app_theme.dart';
import 'package:dnd_companion/features/campaigns/data/campaigns_repository.dart';
import 'package:dnd_companion/features/catalog/data/catalog_repository.dart';
import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:dnd_companion/features/items/data/campaign_items_repository.dart';
import 'package:dnd_companion/features/items/data/inventory_repository.dart';
import 'package:dnd_companion/features/items/data/shops_repository.dart';
import 'package:dnd_companion/features/session/data/messages_repository.dart';
import 'package:dnd_companion/features/session/data/party_repository.dart';
import 'package:dnd_companion/features/session/data/stash_repository.dart';
import 'package:dnd_companion/features/sessions/data/sessions_controllers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'catalog_fakes.dart';
import 'character_fakes.dart';
import 'fakes.dart';
import 'item_fakes.dart';
import 'party_fakes.dart';
import 'session_fakes.dart';

/// In-memory backends of [pumpRealApp]; every one defaults to an empty fake.
class AppFakes {
  factory AppFakes({
    FakeCampaignsRepository? campaigns,
    FakeCharactersRepository? characters,
    FakeInventoryRepository? inventory,
    FakeShopsRepository? shops,
    FakeCampaignItemsRepository? campaignItems,
    FakeCatalogRepository? catalog,
    FakeSessionsRepository? sessions,
    FakePartyRepository? party,
    FakeStashRepository? stash,
    FakeMessagesRepository? messages,
  }) {
    final items = inventory ?? FakeInventoryRepository();
    return AppFakes._(
      campaigns: campaigns ?? FakeCampaignsRepository(campaigns: [makeCampaign()]),
      characters: characters ?? FakeCharactersRepository(),
      inventory: items,
      shops: shops ?? FakeShopsRepository(inventory: items),
      campaignItems: campaignItems ?? FakeCampaignItemsRepository(),
      catalog: catalog ?? FakeCatalogRepository(),
      sessions: sessions ?? FakeSessionsRepository(),
      party: party ?? FakePartyRepository(),
      stash: stash ?? FakeStashRepository(inventory: items),
      messages: messages ?? FakeMessagesRepository(),
    );
  }

  AppFakes._({
    required this.campaigns,
    required this.characters,
    required this.inventory,
    required this.shops,
    required this.campaignItems,
    required this.catalog,
    required this.sessions,
    required this.party,
    required this.stash,
    required this.messages,
  });

  final FakeCampaignsRepository campaigns;
  final FakeCharactersRepository characters;
  final FakeInventoryRepository inventory;
  final FakeShopsRepository shops;
  final FakeCampaignItemsRepository campaignItems;
  final FakeCatalogRepository catalog;
  final FakeSessionsRepository sessions;
  final FakePartyRepository party;
  final FakeStashRepository stash;
  final FakeMessagesRepository messages;

  List<Override> get overrides => [
    campaignsRepositoryProvider.overrideWithValue(campaigns),
    charactersRepositoryProvider.overrideWithValue(characters),
    inventoryRepositoryProvider.overrideWithValue(inventory),
    shopsRepositoryProvider.overrideWithValue(shops),
    campaignItemsRepositoryProvider.overrideWithValue(campaignItems),
    catalogRepositoryProvider.overrideWithValue(catalog),
    sessionsOverride(sessions),
    sessionsClockProvider.overrideWithValue(() => sessionsTestNow),
    partyRepositoryProvider.overrideWithValue(party),
    stashRepositoryProvider.overrideWithValue(stash),
    messagesRepositoryProvider.overrideWithValue(messages),
  ];
}

/// Pumps the whole app with the real router (`routerProvider`, with its auth
/// and campaign-role redirects) starting at [location], signed in as [user]
/// (`u1` by default) against a configured server and the in-memory [fakes].
/// Returns the router so tests can check `routeInformationProvider.value`.
Future<GoRouter> pumpRealApp(
  WidgetTester tester, {
  required String location,
  AppFakes? fakes,
  UserDto? user,
  List<Override> overrides = const [],
  Size size = const Size(800, 2400),
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final backends = fakes ?? AppFakes();
  late GoRouter router;
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authControllerProvider.overrideWith(
          () => FixedAuthController(AuthSignedIn(user ?? makeUser())),
        ),
        fakeServerConfigOverride(),
        fakeServerInfoOverride,
        routerInitialLocationProvider.overrideWithValue(location),
        ...backends.overrides,
        ...overrides,
      ],
      child: Consumer(
        builder: (context, ref, _) {
          router = ref.watch(routerProvider);
          return MaterialApp.router(theme: AppTheme.light(), routerConfig: router);
        },
      ),
    ),
  );
  await tester.pumpAndSettle();
  return router;
}

/// Current location of [router].
String locationOf(GoRouter router) => router.state.uri.toString();

/// A router for the tests that mount only some screens: [routes] plus the real
/// campaign routes (`/campaigns/:id` and its General / DM / player shell), so
/// `/campaigns/c1` opens the "General" view of the campaign.
GoRouter buildTestRouter({required String location, required List<RouteBase> routes}) {
  final rootKey = GlobalKey<NavigatorState>();
  final router = GoRouter(
    navigatorKey: rootKey,
    initialLocation: location,
    routes: [...routes, ...campaignShellRoutes(rootKey)],
  );
  addTearDown(router.dispose);
  return router;
}

/// Opens a section of the "General" view of a campaign from its card.
Future<void> openGeneralSection(WidgetTester tester, String section) async {
  final card = find.byKey(Key('general-$section'));
  await tester.ensureVisible(card);
  await tester.pumpAndSettle();
  await tester.tap(card);
  await tester.pumpAndSettle();
}
