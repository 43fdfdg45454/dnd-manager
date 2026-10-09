import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/auth/user_dto.dart';
import 'package:dnd_companion/core/content/content_visibility.dart';
import 'package:dnd_companion/core/files/authenticated_image.dart';
import 'package:dnd_companion/core/files/external_file_opener.dart';
import 'package:dnd_companion/core/files/file_disk_cache.dart';
import 'package:dnd_companion/core/files/files_repository.dart';
import 'package:dnd_companion/core/files/image_upload.dart';
import 'package:dnd_companion/core/files/stored_file.dart';
import 'package:dnd_companion/core/network/api_client.dart';
import 'package:dnd_companion/core/network/api_error.dart';
import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/core/storage/local_preferences.dart';
import 'package:dnd_companion/core/ui/markdown_view.dart';
import 'package:dnd_companion/features/campaigns/data/campaigns_repository.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/catalog/data/catalog_repository.dart';
import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:dnd_companion/features/characters/ui/character_page.dart';
import 'package:dnd_companion/features/items/data/inventory_repository.dart';
import 'package:dnd_companion/features/library/data/library_controllers.dart';
import 'package:dnd_companion/features/library/data/library_repository.dart';
import 'package:dnd_companion/features/library/data/library_storage.dart';
import 'package:dnd_companion/features/library/data/models.dart';
import 'package:dnd_companion/features/library/ui/library_page.dart';
import 'package:dnd_companion/features/lore/data/lore_repository.dart';
import 'package:dnd_companion/features/lore/data/models.dart';
import 'package:dnd_companion/features/lore/ui/lore_editor_page.dart';
import 'package:dnd_companion/features/lore/ui/lore_entry_page.dart';
import 'package:dnd_companion/features/maps/data/maps_repository.dart';
import 'package:dnd_companion/features/maps/data/models.dart' show parsePinColor;
import 'package:dnd_companion/features/maps/ui/map_viewer_page.dart';
import 'package:dnd_companion/features/session/data/messages_repository.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/content_fakes.dart';
import 'helpers/fake_realtime_hub.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';
import 'helpers/motion.dart';
import 'helpers/party_fakes.dart';

/// App with the routes of the phase 7 screens. The signed-in user is `u1`.
Future<void> _pumpApp(
  WidgetTester tester, {
  required String location,
  CampaignRole role = CampaignRole.player,
  FakeLoreRepository? lore,
  FakeMapsRepository? maps,
  FakeLibraryRepository? library,
  FakeLibraryStorage? storage,
  FakeFilesRepository? files,
  FakeCharactersRepository? characters,
  UserRole userRole = UserRole.user,
  SharedPreferences? prefs,
  List<Override> overrides = const [],
}) async {
  final router = buildTestRouter(
    location: location,
    routes: [
      GoRoute(
        path: '/',
        builder: (_, _) => const Scaffold(body: Text('inicio')),
      ),
      GoRoute(
        path: AppRoutes.campaignLoreNew,
        builder: (_, state) => LoreEditorPage(campaignId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: AppRoutes.campaignLoreEntry,
        builder: (_, state) => LoreEntryPage(
          campaignId: state.pathParameters['id']!,
          entryId: state.pathParameters['entryId']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignMap,
        builder: (_, state) => MapViewerPage(
          campaignId: state.pathParameters['id']!,
          mapId: state.pathParameters['mapId']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignLibrary,
        builder: (_, state) => LibraryPage(campaignId: state.pathParameters['id']),
      ),
      GoRoute(path: AppRoutes.library, builder: (_, _) => const LibraryPage()),
      GoRoute(
        path: AppRoutes.libraryViewer,
        builder: (_, state) => Scaffold(body: Text('visor ${state.pathParameters['docId']}')),
      ),
      GoRoute(
        path: AppRoutes.characterDetail,
        builder: (_, state) => CharacterPage(characterId: state.pathParameters['id']!),
      ),
    ],
  );
  tester.view.physicalSize = const Size(800, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final fakeStorage = storage ?? FakeLibraryStorage();
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        messagesRepositoryProvider.overrideWithValue(FakeMessagesRepository()),
        fakeRealtimeOverride(),
        authControllerProvider.overrideWith(
          () => FixedAuthController(AuthSignedIn(makeUser(role: userRole))),
        ),
        campaignsRepositoryProvider.overrideWithValue(
          FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
        ),
        loreRepositoryProvider.overrideWithValue(
          lore ?? FakeLoreRepository(isDm: role.isAtLeastDm),
        ),
        mapsRepositoryProvider.overrideWithValue(
          maps ?? FakeMapsRepository(isDm: role.isAtLeastDm),
        ),
        libraryRepositoryProvider.overrideWithValue(library ?? FakeLibraryRepository()),
        libraryStorageProvider.overrideWithValue(fakeStorage),
        filesRepositoryProvider.overrideWithValue(
          files ?? FakeFilesRepository(storage: fakeStorage),
        ),
        charactersRepositoryProvider.overrideWithValue(
          characters ?? FakeCharactersRepository(isDm: role.isAtLeastDm),
        ),
        inventoryRepositoryProvider.overrideWithValue(FakeInventoryRepository()),
        catalogRepositoryProvider.overrideWithValue(FakeCatalogRepository()),
        localPreferencesProvider.overrideWithValue(prefs),
        fakeImageLoaderOverride(),
        ...overrides,
      ],
      child: MaterialApp.router(routerConfig: router, builder: reducedMotionBuilder),
    ),
  );
  await tester.pumpAndSettle();
}

final _shownLore = [
  makeLore(id: 'l1', title: 'Valle del Norte'),
  makeLore(
    id: 'l2',
    title: 'Secreto del Culto',
    category: LoreCategory.faction,
    visibility: ContentVisibility.dmOnly,
  ),
  makeLore(id: 'l3', title: 'Reina Mara', category: LoreCategory.npc),
];

void main() {
  group('lore: lista', () {
    testWidgets(
      'un jugador solo ve lo que devuelve el repositorio: sin insignia ni botón de crear',
      (tester) async {
        await _pumpApp(
          tester,
          location: '/campaigns/c1',
          lore: FakeLoreRepository(entries: _shownLore, isDm: false),
        );
        await openGeneralSection(tester, 'lore');

        expect(find.text('Valle del Norte'), findsOneWidget);
        expect(find.text('Reina Mara'), findsOneWidget);
        expect(find.text('Secreto del Culto'), findsNothing);
        expect(find.text('Solo DM'), findsNothing);
        expect(find.byKey(const Key('lore-new')), findsNothing);
        // Grouped by category, with Spanish labels.
        expect(find.byKey(const Key('lore-group-Region')), findsOneWidget);
        expect(find.text('Regiones'), findsOneWidget);
        expect(find.text('PNJ'), findsOneWidget);
      },
    );

    testWidgets('un jugador no ve la insignia aunque el servidor enviara una entrada oculta', (
      tester,
    ) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        // A repository that (wrongly) leaks a hidden entry: the badge is still DM-only.
        lore: FakeLoreRepository(entries: _shownLore, isDm: true),
      );
      // The campaign role is Player: no badge, no create button.
      await openGeneralSection(tester, 'lore');
      expect(find.byKey(const Key('lore-dm-badge-l2')), findsNothing);
      expect(find.byKey(const Key('lore-new')), findsNothing);
    });

    testWidgets('el DM ve las entradas ocultas con la insignia "Solo DM" y puede crear', (
      tester,
    ) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        role: CampaignRole.dm,
        lore: FakeLoreRepository(entries: _shownLore, isDm: true),
      );
      await openGeneralSection(tester, 'lore');

      expect(find.text('Secreto del Culto'), findsOneWidget);
      expect(find.byKey(const Key('lore-dm-badge-l2')), findsOneWidget);
      expect(find.byKey(const Key('lore-dm-badge-l1')), findsNothing);
      expect(find.text('Solo DM'), findsOneWidget);
      expect(find.byKey(const Key('lore-new')), findsOneWidget);
    });

    testWidgets('la búsqueda filtra por título en local', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        lore: FakeLoreRepository(entries: _shownLore),
      );
      await openGeneralSection(tester, 'lore');

      await tester.enterText(find.byKey(const Key('lore-search')), 'mara');
      await tester.pumpAndSettle();
      expect(find.text('Reina Mara'), findsOneWidget);
      expect(find.text('Valle del Norte'), findsNothing);

      await tester.enterText(find.byKey(const Key('lore-search')), 'zzz');
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('lore-empty')), findsOneWidget);
    });

    testWidgets('sin entradas el jugador ve un aviso', (tester) async {
      await _pumpApp(tester, location: '/campaigns/c1');
      await openGeneralSection(tester, 'lore');
      expect(find.text('El DM aún no ha publicado lore'), findsOneWidget);
    });

    test('groupLore agrupa por categoría en orden y respeta sortOrder', () {
      final grouped = groupLore([
        makeLore(id: 'a', title: 'B', category: LoreCategory.npc),
        makeLore(id: 'b', title: 'A', category: LoreCategory.npc),
        makeLore(id: 'c', title: 'Mundo', category: LoreCategory.world),
      ], '');
      expect(grouped.keys, [LoreCategory.world, LoreCategory.npc]);
      expect([for (final e in grouped[LoreCategory.npc]!) e.title], ['A', 'B']);
    });
  });

  group('lore: entrada', () {
    final entries = [
      makeLore(id: 'l1', title: 'Valle del Norte'),
      makeLore(
        id: 'l2',
        title: 'Reina Mara',
        category: LoreCategory.npc,
        visibility: ContentVisibility.dmOnly,
      ),
    ];
    final content = {
      'l1': 'Un valle frío.',
      'l2': 'Gobierna desde el [[valle-del-norte]] y teme a [[nadie-conocido]].',
    };

    testWidgets('el jugador no ve el botón de editar', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/lore/l1',
        lore: FakeLoreRepository(entries: entries, content: content),
      );
      expect(find.byKey(const Key('lore-title')), findsOneWidget);
      expect(find.text('Un valle frío.'), findsOneWidget);
      expect(find.byKey(const Key('lore-edit')), findsNothing);
      expect(find.byKey(const Key('lore-delete')), findsNothing);
      expect(find.byKey(const Key('lore-dm-badge')), findsNothing);
    });

    testWidgets('un jugador recibe 404 en una entrada solo para el DM', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/lore/l2',
        lore: FakeLoreRepository(entries: entries, content: content),
      );
      expect(find.text('El contenido no existe o no tienes acceso.'), findsOneWidget);
    });

    testWidgets('el DM ve editar, eliminar y la insignia', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/lore/l2',
        role: CampaignRole.dm,
        lore: FakeLoreRepository(entries: entries, content: content, isDm: true),
      );
      expect(find.byKey(const Key('lore-edit')), findsOneWidget);
      expect(find.byKey(const Key('lore-delete')), findsOneWidget);
      expect(find.byKey(const Key('lore-dm-badge')), findsOneWidget);
    });

    testWidgets('[[slug]] se renderiza como enlace y navega a la entrada', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/lore/l2',
        role: CampaignRole.dm,
        lore: FakeLoreRepository(entries: entries, content: content, isDm: true),
      );
      // The link shows the title of the entry it points to.
      await tester.tapOnText(find.textRange.ofSubstring('Valle del Norte'));
      await tester.pumpAndSettle();
      expect(find.text('Un valle frío.'), findsOneWidget);
    });

    testWidgets('un enlace a una entrada inexistente avisa', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/lore/l2',
        role: CampaignRole.dm,
        lore: FakeLoreRepository(entries: entries, content: content, isDm: true),
      );
      await tester.tapOnText(find.textRange.ofSubstring('nadie-conocido'));
      await tester.pumpAndSettle();
      expect(find.text('No existe la entrada "nadie-conocido".'), findsOneWidget);
    });
  });

  group('lore: editor', () {
    testWidgets('el DM crea una entrada con vista previa de [[slug]]', (tester) async {
      final repository = FakeLoreRepository(
        entries: [makeLore(id: 'l1', title: 'Valle del Norte')],
        isDm: true,
      );
      await _pumpApp(tester, location: '/campaigns/c1', role: CampaignRole.dm, lore: repository);
      await openGeneralSection(tester, 'lore');
      await tester.tap(find.byKey(const Key('lore-new')));
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('lore-field-title')), 'La Torre');
      await tester.enterText(
        find.byKey(const Key('lore-field-content')),
        'Se ve desde el [[valle-del-norte]].',
      );
      await tester.tap(find.byKey(const Key('lore-tab-preview')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('lore-preview')), findsOneWidget);
      expect(find.textContaining('Valle del Norte'), findsOneWidget);
      expect(find.textContaining('[['), findsNothing);

      await tester.tap(find.byKey(const Key('lore-tab-edit')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('lore-save')));
      await tester.pumpAndSettle();

      final draft = repository.created.single;
      expect(draft.title, 'La Torre');
      expect(draft.category, LoreCategory.note);
      expect(draft.visibility, ContentVisibility.players);
      expect(draft.contentMarkdown, 'Se ve desde el [[valle-del-norte]].');
      expect(find.text('La Torre'), findsOneWidget);
    });

    testWidgets('el título es obligatorio', (tester) async {
      final repository = FakeLoreRepository(isDm: true);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/lore/new',
        role: CampaignRole.dm,
        lore: repository,
      );
      await tester.tap(find.byKey(const Key('lore-save')));
      await tester.pumpAndSettle();
      expect(find.text('Escribe un título.'), findsOneWidget);
      expect(repository.created, isEmpty);
    });

    testWidgets('un jugador no puede abrir el editor', (tester) async {
      await _pumpApp(tester, location: '/campaigns/c1/lore/new');
      expect(find.text('Solo el DM puede editar el lore.'), findsOneWidget);
      expect(find.byKey(const Key('lore-save')), findsNothing);
    });
  });

  group('markdown', () {
    test('expandWikiLinks convierte [[slug]] y [[slug|texto]] en enlaces lore:', () {
      expect(
        expandWikiLinks('Ve a [[valle-norte]] o [[valle-norte|el valle]].'),
        'Ve a [valle-norte](lore:valle-norte) o [el valle](lore:valle-norte).',
      );
      expect(
        expandWikiLinks('[[valle-norte]]', titles: {'valle-norte': 'Valle del Norte'}),
        '[Valle del Norte](lore:valle-norte)',
      );
      expect(expandWikiLinks('sin enlaces [x] y [[ ]]'), 'sin enlaces [x] y [[ ]]');
    });

    test('loreSlugOfHref solo reconoce enlaces lore:', () {
      expect(loreSlugOfHref('lore:valle-norte'), 'valle-norte');
      expect(loreSlugOfHref('lore:${Uri.encodeComponent('mi entrada')}'), 'mi entrada');
      expect(loreSlugOfHref('https://example.com'), isNull);
      expect(loreSlugOfHref(null), isNull);
    });

    testWidgets('el visor renderiza el enlace y avisa con el slug al tocarlo', (tester) async {
      String? tapped;
      await tester.pumpWidget(
        ProviderScope(
          overrides: [fakeImageLoaderOverride()],
          child: MaterialApp(
            home: Scaffold(
              body: MarkdownView(
                data: 'Texto con **negrita** y un enlace a [[valle-norte]].',
                titles: const {'valle-norte': 'Valle del Norte'},
                onLoreLink: (slug) => tapped = slug,
              ),
            ),
          ),
        ),
      );
      expect(find.textContaining('Texto con'), findsOneWidget);
      // The raw `[[...]]` syntax is gone.
      expect(find.textContaining('[['), findsNothing);
      await tester.tapOnText(find.textRange.ofSubstring('Valle del Norte'));
      expect(tapped, 'valle-norte');
    });
  });

  group('mapas', () {
    final map = makeMap(
      pins: [
        makePin(id: 'p1', title: 'Fortaleza', note: 'Una **vieja** torre.', loreEntryId: 'l1'),
        makePin(id: 'p2', title: 'Alijo secreto', x: 0.7, visibility: ContentVisibility.dmOnly),
      ],
    );

    testWidgets('la pestaña lista los mapas devueltos', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        maps: FakeMapsRepository(
          maps: [
            makeMap(),
            makeMap(id: 'm2', name: 'Mapa oculto', visibility: ContentVisibility.dmOnly),
          ],
        ),
      );
      await openGeneralSection(tester, 'maps');
      expect(find.text('Costa de la Espada'), findsOneWidget);
      expect(find.text('Mapa oculto'), findsNothing);
      expect(find.byKey(const Key('maps-new')), findsNothing);
    });

    testWidgets('el DM puede subir mapas y ve la insignia de los ocultos', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        role: CampaignRole.dm,
        maps: FakeMapsRepository(
          maps: [makeMap(id: 'm2', name: 'Mapa oculto', visibility: ContentVisibility.dmOnly)],
          isDm: true,
        ),
      );
      await openGeneralSection(tester, 'maps');
      expect(find.byKey(const Key('maps-new')), findsOneWidget);
      expect(find.text('Solo DM'), findsOneWidget);
    });

    testWidgets('el DM sube un mapa: imagen MapImage y luego crea el mapa', (tester) async {
      final repository = FakeMapsRepository(isDm: true);
      final files = FakeFilesRepository();
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        role: CampaignRole.dm,
        maps: repository,
        files: files,
        overrides: [imagePickerProvider.overrideWithValue(FakeImagePicker())],
      );
      await openGeneralSection(tester, 'maps');
      await tester.tap(find.byKey(const Key('maps-new')));
      await tester.pumpAndSettle();

      expect(files.uploads.single.kind, FileKind.mapImage);
      expect(files.uploads.single.campaignId, 'c1');
      await tester.enterText(find.byKey(const Key('map-field-name')), 'Isla Calavera');
      await tester.tap(find.byKey(const Key('map-form-submit')));
      await tester.pumpAndSettle();

      expect(repository.created, [(name: 'Isla Calavera', fileId: 'up1', visibility: 'Players')]);
      expect(find.text('Isla Calavera'), findsOneWidget);
    });

    testWidgets('el jugador no ve el pin DmOnly ni el botón de crear pines', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/maps/m1',
        maps: FakeMapsRepository(maps: [map], isDm: false),
      );
      expect(find.byKey(const Key('map-pin-p1')), findsOneWidget);
      expect(find.byKey(const Key('map-pin-p2')), findsNothing);
      expect(find.byKey(const Key('map-add-pin')), findsNothing);
    });

    testWidgets('el DM ve todos los pines y el botón de crear', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/maps/m1',
        role: CampaignRole.dm,
        maps: FakeMapsRepository(maps: [map], isDm: true),
      );
      expect(find.byKey(const Key('map-pin-p1')), findsOneWidget);
      expect(find.byKey(const Key('map-pin-p2')), findsOneWidget);
      expect(find.byKey(const Key('map-add-pin')), findsOneWidget);
    });

    testWidgets('los pines se colocan según su posición relativa', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/maps/m1',
        maps: FakeMapsRepository(maps: [map]),
      );
      final canvas = tester.getRect(find.byKey(const Key('map-canvas')));
      // 2000x1000 image: twice as wide as high.
      expect(canvas.width / canvas.height, closeTo(2, 0.01));
      final pin = tester.getRect(find.byKey(const Key('map-pin-p1')));
      // x = 0.25 and y = 0.5; the pin is anchored by its bottom centre.
      expect(pin.center.dx, closeTo(canvas.left + canvas.width * 0.25, 0.5));
      expect(pin.bottom, closeTo(canvas.top + canvas.height * 0.5, 0.5));
    });

    testWidgets('tocar un pin abre la hoja con nota markdown y enlace al lore', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/maps/m1',
        maps: FakeMapsRepository(maps: [map]),
        lore: FakeLoreRepository(
          entries: [makeLore(id: 'l1', title: 'Valle del Norte')],
          content: const {'l1': 'Un valle frío.'},
        ),
      );
      await tester.tap(find.byKey(const Key('map-pin-p1')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('pin-sheet')), findsOneWidget);
      expect(find.text('Fortaleza'), findsWidgets);
      expect(find.textContaining('torre'), findsOneWidget);
      expect(find.byKey(const Key('pin-edit')), findsNothing);
      expect(find.byKey(const Key('pin-delete')), findsNothing);

      await tester.tap(find.byKey(const Key('pin-lore-link')));
      await tester.pumpAndSettle();
      expect(find.text('Un valle frío.'), findsOneWidget);
    });

    testWidgets('el DM crea un pin en el centro desde el botón', (tester) async {
      final repository = FakeMapsRepository(maps: [map], isDm: true);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/maps/m1',
        role: CampaignRole.dm,
        maps: repository,
      );
      await tester.tap(find.byKey(const Key('map-add-pin')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('pin-field-title')), 'Taberna');
      await tester.tap(find.byKey(const Key('pin-icon-city')));
      await tester.tap(find.byKey(const Key('pin-form-submit')));
      await tester.pumpAndSettle();

      final created = repository.createdPins.single;
      expect(created.draft.title, 'Taberna');
      expect(created.draft.icon.apiValue, 'city');
      expect(created.x, closeTo(0.5, 0.01));
      expect(created.y, closeTo(0.5, 0.01));
      expect(find.byKey(const Key('map-pin-new1')), findsOneWidget);
    });

    testWidgets('el DM crea un pin con pulsación larga en la posición tocada', (tester) async {
      final repository = FakeMapsRepository(maps: [map], isDm: true);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/maps/m1',
        role: CampaignRole.dm,
        maps: repository,
      );
      final canvas = tester.getRect(find.byKey(const Key('map-canvas')));
      await tester.longPressAt(
        Offset(canvas.left + canvas.width * 0.8, canvas.top + canvas.height * 0.2),
      );
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('pin-field-title')), 'Puerto');
      await tester.tap(find.byKey(const Key('pin-form-submit')));
      await tester.pumpAndSettle();

      final created = repository.createdPins.single;
      expect(created.x, closeTo(0.8, 0.01));
      expect(created.y, closeTo(0.2, 0.01));
    });

    testWidgets('el DM arrastra un pin y se guarda la nueva posición al soltar', (tester) async {
      final repository = FakeMapsRepository(maps: [map], isDm: true);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/maps/m1',
        role: CampaignRole.dm,
        maps: repository,
      );
      final gesture = await tester.startGesture(
        tester.getCenter(find.byKey(const Key('map-pin-p1'))),
      );
      await gesture.moveBy(const Offset(30, 0));
      await tester.pump();
      for (var i = 0; i < 10; i++) {
        await gesture.moveBy(const Offset(20, 0));
        await tester.pump();
      }
      await gesture.up();
      await tester.pumpAndSettle();

      final moved = repository.movedPins.single;
      expect(moved.pinId, 'p1');
      expect(moved.x, greaterThan(0.25));
    });

    testWidgets('el DM elimina un pin desde la hoja', (tester) async {
      final repository = FakeMapsRepository(maps: [map], isDm: true);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/maps/m1',
        role: CampaignRole.dm,
        maps: repository,
      );
      await tester.tap(find.byKey(const Key('map-pin-p1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('pin-delete')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm-action')));
      await tester.pumpAndSettle();
      expect(repository.deletedPins, ['p1']);
      expect(find.byKey(const Key('map-pin-p1')), findsNothing);
    });

    test('parsePinColor admite #RRGGBB y descarta lo demás', () {
      expect(parsePinColor('#D32F2F'), isNotNull);
      expect(parsePinColor('rojo'), isNull);
      expect(parsePinColor(null), isNull);
    });
  });

  group('biblioteca', () {
    final docs = [
      makeDocument(id: 'd1', title: 'Reglas básicas', isSystem: true),
      makeDocument(
        id: 'd2',
        title: 'La Mina Perdida',
        category: LibraryCategory.adventure,
        description: 'Aventura para nivel 1',
      ),
    ];

    testWidgets('la lista muestra los documentos con su estado y el botón de descarga', (
      tester,
    ) async {
      await _pumpApp(
        tester,
        location: '/library',
        library: FakeLibraryRepository(documents: docs),
      );
      expect(find.text('Reglas básicas'), findsOneWidget);
      expect(find.text('La Mina Perdida'), findsOneWidget);
      expect(find.text('Reglas · 120 págs. · 2,0 MB'), findsOneWidget);
      expect(find.byKey(const Key('library-download-d1')), findsOneWidget);
      expect(find.byKey(const Key('library-download-d2')), findsOneWidget);
      expect(find.text('No descargado'), findsNWidgets(2));
      // Not an admin: no upload button.
      expect(find.byKey(const Key('library-upload')), findsNothing);
    });

    testWidgets('descargar muestra el estado "disponible" y se puede eliminar la descarga', (
      tester,
    ) async {
      final files = FakeFilesRepository();
      await _pumpApp(
        tester,
        location: '/library',
        library: FakeLibraryRepository(documents: docs),
        files: files,
      );
      await tester.tap(find.byKey(const Key('library-download-d2')));
      await tester.pumpAndSettle();

      expect(files.downloads, ['/api/v1/files/f-d2']);
      expect(find.byKey(const Key('library-remove-d2')), findsOneWidget);
      expect(find.byKey(const Key('library-download-d2')), findsNothing);
      expect(find.text('Disponible sin conexión'), findsOneWidget);

      await tester.tap(find.byKey(const Key('library-remove-d2')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('library-download-d2')), findsOneWidget);
      expect(find.text('Descarga eliminada.'), findsOneWidget);
    });

    testWidgets('un documento ya descargado aparece disponible sin red', (tester) async {
      await _pumpApp(
        tester,
        location: '/library',
        library: FakeLibraryRepository(documents: docs),
        storage: FakeLibraryStorage({'d1'}),
      );
      expect(find.byKey(const Key('library-remove-d1')), findsOneWidget);
      expect(find.byKey(const Key('library-download-d2')), findsOneWidget);
    });

    testWidgets('tocar un documento lo descarga y lo abre con una app del sistema', (tester) async {
      final files = FakeFilesRepository();
      final opener = _FakeOpener(ExternalOpenResult.opened);
      await _pumpApp(
        tester,
        location: '/library',
        library: FakeLibraryRepository(documents: docs),
        files: files,
        overrides: [externalFileOpenerProvider.overrideWithValue(opener)],
      );
      await tester.tap(find.text('La Mina Perdida'));
      await tester.pumpAndSettle();

      expect(files.downloads, ['/api/v1/files/f-d2']);
      expect(opener.opened, [('/fake/library/d2.pdf', 'application/pdf')]);
      expect(find.byKey(const Key('library-open-failed')), findsNothing);
      expect(find.text('visor d2'), findsNothing);

      // Already downloaded: opens without downloading again.
      await tester.tap(find.text('La Mina Perdida'));
      await tester.pumpAndSettle();
      expect(files.downloads, hasLength(1));
      expect(opener.opened, hasLength(2));
    });

    testWidgets('sin app para PDF se ofrece verlo en la app', (tester) async {
      await _pumpApp(
        tester,
        location: '/library',
        library: FakeLibraryRepository(documents: docs),
        storage: FakeLibraryStorage({'d1'}),
        overrides: [
          externalFileOpenerProvider.overrideWithValue(_FakeOpener(ExternalOpenResult.noApp)),
        ],
      );
      await tester.tap(find.text('Reglas básicas'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('library-open-failed')), findsOneWidget);
      expect(find.textContaining('No hay ninguna aplicación'), findsOneWidget);
      await tester.tap(find.text('Ver en la app'));
      await tester.pumpAndSettle();
      expect(find.text('visor d1'), findsOneWidget);
    });

    testWidgets('el menú del documento abre el visor integrado', (tester) async {
      await _pumpApp(
        tester,
        location: '/library',
        library: FakeLibraryRepository(documents: docs),
      );
      await tester.tap(find.byKey(const Key('library-menu-d2')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('library-view-d2')));
      await tester.pumpAndSettle();
      expect(find.text('visor d2'), findsOneWidget);
    });

    testWidgets('busca por texto y filtra por categoría', (tester) async {
      await _pumpApp(
        tester,
        location: '/library',
        library: FakeLibraryRepository(documents: docs),
      );
      await tester.enterText(find.byKey(const Key('library-search')), 'nivel 1');
      await tester.pumpAndSettle();
      expect(find.text('La Mina Perdida'), findsOneWidget);
      expect(find.text('Reglas básicas'), findsNothing);

      await tester.enterText(find.byKey(const Key('library-search')), '');
      await tester.tap(find.byKey(const Key('library-category-Rules')));
      await tester.pumpAndSettle();
      expect(find.text('Reglas básicas'), findsOneWidget);
      expect(find.text('La Mina Perdida'), findsNothing);
    });

    testWidgets('el administrador ve subir y borrar, salvo en los documentos del sistema', (
      tester,
    ) async {
      final repository = FakeLibraryRepository(documents: docs);
      await _pumpApp(tester, location: '/library', library: repository, userRole: UserRole.admin);
      expect(find.byKey(const Key('library-upload')), findsOneWidget);
      // System documents only offer the built-in viewer.
      await tester.tap(find.byKey(const Key('library-menu-d1')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('library-view-d1')), findsOneWidget);
      expect(find.text('Eliminar'), findsNothing);
      await tester.tapAt(Offset.zero);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('library-menu-d2')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Eliminar'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm-action')));
      await tester.pumpAndSettle();
      expect(repository.deleted, ['d2']);
      expect(find.text('La Mina Perdida'), findsNothing);
    });

    testWidgets('en la campaña el DM recomienda documentos y el jugador no puede', (tester) async {
      final repository = FakeLibraryRepository(documents: docs);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/library',
        role: CampaignRole.dm,
        library: repository,
      );
      await tester.tap(find.byKey(const Key('library-recommend-d2')));
      await tester.pumpAndSettle();
      expect(repository.recommended, {'d2'});

      await tester.tap(find.byKey(const Key('library-filter-recommended')));
      await tester.pumpAndSettle();
      expect(find.text('La Mina Perdida'), findsOneWidget);
      expect(find.text('Reglas básicas'), findsNothing);
    });

    testWidgets('un jugador ve lo recomendado pero no el botón de recomendar', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/library',
        library: FakeLibraryRepository(documents: docs, recommended: ['d2']),
      );
      expect(find.byKey(const Key('library-recommend-d2')), findsNothing);
      expect(find.byKey(const Key('library-recommend-d1')), findsNothing);
    });

    testWidgets('la campaña enlaza a los documentos recomendados', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        library: FakeLibraryRepository(documents: docs),
      );
      await openGeneralSection(tester, 'settings');
      await tester.tap(find.byKey(const Key('campaign-documents')));
      await tester.pumpAndSettle();
      expect(find.text('Documentos de la campaña'), findsOneWidget);
      expect(find.text('Reglas básicas'), findsOneWidget);
    });

    testWidgets('la tarjeta Biblioteca de General abre los documentos de la campaña', (
      tester,
    ) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        library: FakeLibraryRepository(documents: docs),
      );
      await openGeneralSection(tester, 'library');
      expect(find.text('Documentos de la campaña'), findsOneWidget);
      expect(find.text('Reglas básicas'), findsOneWidget);
    });

    test('sin conexión se usa la última lista guardada', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();
      final repository = FakeLibraryRepository(documents: docs);
      final container = ProviderContainer(
        overrides: [
          libraryRepositoryProvider.overrideWithValue(repository),
          libraryStorageProvider.overrideWithValue(FakeLibraryStorage()),
          localPreferencesProvider.overrideWithValue(prefs),
        ],
      );
      addTearDown(container.dispose);

      final online = await container.read(libraryControllerProvider.future);
      expect(online.offline, isFalse);
      expect(online.documents, hasLength(2));

      repository.error = dioError(null);
      await container.read(libraryControllerProvider.notifier).reload();
      final offline = container.read(libraryControllerProvider).requireValue;
      expect(offline.offline, isTrue);
      expect(offline.documents.map((d) => d.title), ['Reglas básicas', 'La Mina Perdida']);
    });

    test('sin conexión ni lista guardada se muestra el error', () async {
      final repository = FakeLibraryRepository()..error = dioError(null);
      final container = ProviderContainer(
        overrides: [
          libraryRepositoryProvider.overrideWithValue(repository),
          libraryStorageProvider.overrideWithValue(FakeLibraryStorage()),
        ],
      );
      addTearDown(container.dispose);
      await expectLater(
        container.read(libraryControllerProvider.future),
        throwsA(isA<DioException>()),
      );
    });

    test('la última página leída se guarda con la clave library.<id>.page', () {
      expect(libraryPageKey('abc'), 'library.abc.page');
    });
  });

  group('imágenes y ficheros', () {
    testWidgets('AuthenticatedImage muestra el error y reintenta al tocarlo', (tester) async {
      var calls = 0;
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            fileBytesLoaderProvider.overrideWithValue((url) async {
              calls++;
              throw dioError(401);
            }),
          ],
          child: const MaterialApp(
            home: Scaffold(
              body: AuthenticatedImage(url: '/api/v1/files/abc', width: 200, height: 100),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(calls, 1);
      expect(find.byKey(const Key('image-retry')), findsOneWidget);
      expect(find.text('No se pudo cargar. Toca para reintentar'), findsOneWidget);

      await tester.tap(find.byKey(const Key('image-retry')));
      await tester.pumpAndSettle();
      expect(calls, 2);
    });

    testWidgets('AuthenticatedImage pide la URL relativa al cargador con sesión', (tester) async {
      final requested = <String>[];
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            fileBytesLoaderProvider.overrideWithValue((url) async {
              requested.add(url);
              return tinyPng;
            }),
          ],
          child: const MaterialApp(
            home: Scaffold(body: AuthenticatedImage(url: '/api/v1/files/xyz')),
          ),
        ),
      );
      await tester.pump();
      expect(requested, ['/api/v1/files/xyz']);
    });

    test('helpers de URL, tipo y tamaño', () {
      expect(
        resolveFileUrl('http://dnd.example.com:8080/', '/api/v1/files/a'),
        'http://dnd.example.com:8080/api/v1/files/a',
      );
      expect(
        resolveFileUrl('http://dnd.example.com', 'https://otro.example.com/x'),
        'https://otro.example.com/x',
      );
      expect(contentTypeForFileName('Mapa.PNG'), 'image/png');
      expect(contentTypeForFileName('a.jpeg'), 'image/jpeg');
      expect(contentTypeForFileName('libro.pdf'), 'application/pdf');
      expect(contentTypeForFileName('raro'), 'application/octet-stream');
      expect(formatFileSize(512), '512 B');
      expect(formatFileSize(1536), '1,5 KB');
      expect(formatFileSize(3 * 1024 * 1024), '3,0 MB');
      expect(
        FileDiskCache.keyFor('/api/v1/files/0f8fad5b-d9cb-469f-a165-70867728950e'),
        '0f8fad5b-d9cb-469f-a165-70867728950e',
      );
      expect(FileDiskCache.keyFor('/otra/ruta?x=1'), hasLength(64));
    });

    test('describeContentError: 413, 403 y el detalle de un 400', () {
      expect(describeContentError(dioError(413)), 'El fichero supera el tamaño máximo permitido.');
      expect(describeContentError(dioError(403)), 'No tienes permiso para hacer eso.');
      expect(
        describeContentError(dioProblem(400, 'El icono no es válido.')),
        'El icono no es válido.',
      );
      expect(describeContentError(dioError(null)), networkErrorMessage);
    });

    test('upload envía multipart con kind, campaignId y characterId', () async {
      final adapter = _CaptureAdapter();
      final repository = FilesRepository(
        ApiClient(
          baseUrl: testServerUrl,
          dio: Dio(BaseOptions(baseUrl: testServerUrl))..httpClientAdapter = adapter,
        ),
      );
      final dir = await Directory.systemTemp.createTemp('upload_test');
      addTearDown(() => dir.delete(recursive: true));
      final file = File('${dir.path}/retrato.png')..writeAsBytesSync(tinyPng);

      final stored = await repository.upload(
        filePath: file.path,
        fileName: 'retrato.png',
        kind: FileKind.portrait,
        campaignId: 'c1',
        characterId: 'ch1',
      );

      expect(stored.id, 'f1');
      expect(stored.url, '/api/v1/files/f1');
      final request = adapter.requests.single;
      expect(request.method, 'POST');
      expect(request.path, '/api/v1/files');
      final form = request.data as FormData;
      expect(
        {for (final f in form.fields) f.key: f.value},
        {'kind': 'Portrait', 'campaignId': 'c1', 'characterId': 'ch1'},
      );
      expect(form.files.single.key, 'file');
      expect(form.files.single.value.filename, 'retrato.png');
      expect(form.files.single.value.contentType.toString(), 'image/png');
    });
  });

  group('retrato del personaje', () {
    testWidgets('el dueño cambia el retrato: sube Portrait y lo asigna', (tester) async {
      final characters = FakeCharactersRepository(characters: [makeCharacterJson()]);
      final files = FakeFilesRepository();
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        characters: characters,
        files: files,
        overrides: [imagePickerProvider.overrideWithValue(FakeImagePicker())],
      );
      await tester.tap(find.byKey(const Key('character-avatar')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('portrait-remove')), findsNothing);
      await tester.tap(find.byKey(const Key('portrait-pick')));
      await tester.pumpAndSettle();

      final upload = files.uploads.single;
      expect(upload.kind, FileKind.portrait);
      expect(upload.campaignId, 'c1');
      expect(upload.characterId, 'ch1');
      expect(characters.portraits, ['up1']);
      expect(find.text('Retrato actualizado.'), findsOneWidget);
    });

    testWidgets('quien no es dueño ni DM no puede cambiar el retrato', (tester) async {
      await _pumpApp(
        tester,
        location: '/characters/ch1',
        characters: FakeCharactersRepository(characters: [makeCharacterJson(ownerUserId: 'p2')]),
      );
      expect(find.byKey(const Key('character-avatar')), findsOneWidget);
      await tester.tap(find.byKey(const Key('character-avatar')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('portrait-pick')), findsNothing);
    });

    testWidgets('con retrato se ofrece quitarlo', (tester) async {
      final characters = FakeCharactersRepository(
        characters: [makeCharacterJson()..['portraitUrl'] = '/api/v1/files/p1'],
      );
      await _pumpApp(tester, location: '/characters/ch1', characters: characters);
      await tester.tap(find.byKey(const Key('character-avatar')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('portrait-remove')));
      await tester.pumpAndSettle();
      expect(characters.portraits, [null]);
    });
  });
}

class _CaptureAdapter implements HttpClientAdapter {
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    return ResponseBody.fromString(
      jsonEncode({
        'id': 'f1',
        'fileName': 'retrato.png',
        'contentType': 'image/png',
        'sizeBytes': tinyPng.length,
        'url': '/api/v1/files/f1',
      }),
      201,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

/// Records what the library hands to the system and answers [result].
class _FakeOpener implements ExternalFileOpener {
  _FakeOpener(this.result);

  final ExternalOpenResult result;
  final opened = <(String, String?)>[];

  @override
  Future<ExternalOpenResult> open(String path, {String? mimeType}) async {
    opened.add((path, mimeType));
    return result;
  }
}
