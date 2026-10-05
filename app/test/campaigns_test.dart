import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/features/campaigns/data/campaigns_repository.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/campaigns/ui/campaign_detail_page.dart';
import 'package:dnd_companion/features/campaigns/ui/campaigns_page.dart';
import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

final _directory = [
  const UserSummary(id: 'u7', displayName: 'Carla', email: 'carla@example.com'),
  const UserSummary(id: 'u8', displayName: 'Diego', email: 'diego@example.com'),
  // Already a member of the default campaign: must not be offered again.
  const UserSummary(id: 'p2', displayName: 'Beto', email: 'p2@example.com'),
];

Widget _providerScope(FakeCampaignsRepository repository, Widget child) => ProviderScope(
  overrides: [
    authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
    campaignsRepositoryProvider.overrideWithValue(repository),
    charactersRepositoryProvider.overrideWithValue(FakeCharactersRepository()),
  ],
  child: child,
);

Future<void> _pumpList(WidgetTester tester, FakeCampaignsRepository repository) async {
  await tester.pumpWidget(_providerScope(repository, const MaterialApp(home: CampaignsPage())));
  await tester.pumpAndSettle();
}

/// Mounts the detail page under a router so `go('/')` after leaving works.
Future<void> _pumpDetail(WidgetTester tester, FakeCampaignsRepository repository) async {
  final router = GoRouter(
    initialLocation: '/campaigns/c1',
    routes: [
      GoRoute(
        path: '/',
        builder: (_, _) => const Scaffold(body: Text('inicio')),
      ),
      GoRoute(
        path: '/campaigns/:id',
        builder: (_, state) => CampaignDetailPage(campaignId: state.pathParameters['id']!),
      ),
    ],
  );
  addTearDown(router.dispose);
  await tester.pumpWidget(_providerScope(repository, MaterialApp.router(routerConfig: router)));
  await tester.pumpAndSettle();
}

Future<void> _openMembersTab(WidgetTester tester) async {
  await tester.tap(find.byKey(const Key('tab-members')));
  await tester.pumpAndSettle();
}

void main() {
  group('lista de campañas', () {
    testWidgets('muestra nombre, rol propio y número de miembros', (tester) async {
      await _pumpList(
        tester,
        FakeCampaignsRepository(
          campaigns: [
            makeCampaign(id: 'c1', name: 'La Mina Perdida'),
            makeCampaign(id: 'c2', name: 'Tormenta de Reyes', myRole: CampaignRole.player),
          ],
        ),
      );

      expect(find.text('La Mina Perdida'), findsOneWidget);
      expect(find.text('Tormenta de Reyes'), findsOneWidget);
      expect(find.text('Dueño'), findsOneWidget);
      expect(find.text('Jugador'), findsOneWidget);
      expect(find.text('2 miembros'), findsOneWidget);
      expect(find.text('3 miembros'), findsOneWidget);
    });

    testWidgets('sin campañas muestra el estado vacío', (tester) async {
      await _pumpList(tester, FakeCampaignsRepository());

      expect(find.text('Aún no tienes campañas'), findsOneWidget);
    });

    testWidgets('crear una campaña la añade a la lista', (tester) async {
      final repository = FakeCampaignsRepository();
      await _pumpList(tester, repository);

      await tester.tap(find.text('Nueva campaña'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('campaign-name')), 'Nueva Aventura');
      await tester.enterText(find.byKey(const Key('campaign-description')), 'Una descripción');
      await tester.tap(find.byKey(const Key('campaign-form-submit')));
      await tester.pumpAndSettle();

      expect(find.text('Nueva Aventura'), findsOneWidget);
      expect(find.text('Aún no tienes campañas'), findsNothing);
      expect(find.text('Campaña creada.'), findsOneWidget);
      expect(repository.campaigns.single.description, 'Una descripción');
    });

    testWidgets('el diálogo exige un nombre', (tester) async {
      final repository = FakeCampaignsRepository();
      await _pumpList(tester, repository);

      await tester.tap(find.text('Nueva campaña'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('campaign-form-submit')));
      await tester.pumpAndSettle();

      expect(find.text('Introduce un nombre'), findsOneWidget);
      expect(repository.campaigns, isEmpty);
    });

    testWidgets('un error de red muestra el mensaje y permite reintentar', (tester) async {
      final repository = FakeCampaignsRepository()..error = dioError(null);
      await _pumpList(tester, repository);

      expect(find.text('No se pudo conectar con el servidor. Revisa tu conexión.'), findsOneWidget);

      repository.error = null;
      await tester.tap(find.text('Reintentar'));
      await tester.pumpAndSettle();
      expect(find.text('Aún no tienes campañas'), findsOneWidget);
    });
  });

  group('detalle: resumen', () {
    testWidgets('un Player no ve Editar, Transferir ni Eliminar, pero sí Salir', (tester) async {
      await _pumpDetail(
        tester,
        FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
      );

      expect(find.text('Una aventura para niveles 1 a 3.'), findsOneWidget);
      expect(find.text('Editar'), findsNothing);
      expect(find.text('Transferir propiedad'), findsNothing);
      expect(find.text('Eliminar campaña'), findsNothing);
      expect(find.text('Salir de la campaña'), findsOneWidget);
    });

    testWidgets('un DM puede editar pero no transferir ni eliminar', (tester) async {
      await _pumpDetail(
        tester,
        FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.dm)]),
      );

      expect(find.text('Editar'), findsOneWidget);
      expect(find.text('Transferir propiedad'), findsNothing);
      expect(find.text('Eliminar campaña'), findsNothing);
      expect(find.text('Salir de la campaña'), findsOneWidget);
    });

    testWidgets('el Owner ve Transferir y Eliminar y no Salir', (tester) async {
      await _pumpDetail(tester, FakeCampaignsRepository(campaigns: [makeCampaign()]));

      expect(find.text('Editar'), findsOneWidget);
      expect(find.text('Transferir propiedad'), findsOneWidget);
      expect(find.text('Eliminar campaña'), findsOneWidget);
      expect(find.text('Salir de la campaña'), findsNothing);
    });

    testWidgets('editar guarda el nuevo nombre y descripción', (tester) async {
      final repository = FakeCampaignsRepository(campaigns: [makeCampaign()]);
      await _pumpDetail(tester, repository);

      await tester.tap(find.byKey(const Key('campaign-edit')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('campaign-name')), 'Mina Recuperada');
      await tester.tap(find.byKey(const Key('campaign-form-submit')));
      await tester.pumpAndSettle();

      expect(repository.campaigns.single.name, 'Mina Recuperada');
      expect(find.text('Campaña actualizada.'), findsOneWidget);
    });

    testWidgets('transferir propiedad conserva el rol elegido', (tester) async {
      final repository = FakeCampaignsRepository(campaigns: [makeCampaign()]);
      await _pumpDetail(tester, repository);

      await tester.tap(find.byKey(const Key('campaign-transfer')));
      await tester.pumpAndSettle();
      // The first candidate is the only other member (Beto).
      await tester.tap(find.byKey(const Key('transfer-role')).first);
      await tester.tap(find.text('Jugador'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('transfer-submit')));
      await tester.pumpAndSettle();

      final campaign = repository.campaigns.single;
      expect(campaign.ownerId, 'p2');
      expect(campaign.myRole, CampaignRole.player);
      expect(campaign.members.firstWhere((m) => m.userId == 'u1').role, CampaignRole.player);
      // The previous owner is no longer Owner: the transfer action disappears.
      expect(find.text('Transferir propiedad'), findsNothing);
    });

    testWidgets('salir de la campaña pide confirmación y vuelve al inicio', (tester) async {
      final repository = FakeCampaignsRepository(
        campaigns: [makeCampaign(myRole: CampaignRole.player)],
      );
      await _pumpDetail(tester, repository);

      await tester.tap(find.byKey(const Key('campaign-leave')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
      expect(repository.campaigns, hasLength(1));

      await tester.tap(find.byKey(const Key('campaign-leave')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm-action')));
      await tester.pumpAndSettle();

      expect(repository.campaigns, isEmpty);
      expect(find.text('inicio'), findsOneWidget);
    });

    testWidgets('eliminar la campaña pide confirmación', (tester) async {
      final repository = FakeCampaignsRepository(campaigns: [makeCampaign()]);
      await _pumpDetail(tester, repository);

      await tester.tap(find.byKey(const Key('campaign-delete')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm-action')));
      await tester.pumpAndSettle();

      expect(repository.campaigns, isEmpty);
      expect(find.text('inicio'), findsOneWidget);
    });

    testWidgets('un 403 se muestra en español', (tester) async {
      final repository = FakeCampaignsRepository(campaigns: [makeCampaign()]);
      await _pumpDetail(tester, repository);

      repository.error = dioError(403);
      await tester.tap(find.byKey(const Key('campaign-edit')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('campaign-form-submit')));
      await tester.pumpAndSettle();

      expect(find.text('No tienes permiso para hacer eso en esta campaña.'), findsOneWidget);
    });
  });

  group('detalle: miembros', () {
    testWidgets('lista los miembros con su rol', (tester) async {
      await _pumpDetail(tester, FakeCampaignsRepository(campaigns: [makeCampaign()]));
      await _openMembersTab(tester);

      expect(find.text('Usuario Demo (tú)'), findsOneWidget);
      expect(find.text('Beto'), findsOneWidget);
      expect(find.text('Dueño'), findsOneWidget);
      expect(find.text('Jugador'), findsOneWidget);
    });

    testWidgets('un Player no ve el botón Añadir ni menús', (tester) async {
      await _pumpDetail(
        tester,
        FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
      );
      await _openMembersTab(tester);

      expect(find.text('Añadir'), findsNothing);
      expect(find.byType(PopupMenuButton<Object>), findsNothing);
      expect(find.byIcon(Icons.more_vert), findsNothing);
    });

    testWidgets('el buscador muestra resultados y añade un miembro', (tester) async {
      final repository = FakeCampaignsRepository(
        campaigns: [makeCampaign()],
        directory: _directory,
      );
      await _pumpDetail(tester, repository);
      await _openMembersTab(tester);

      await tester.tap(find.byKey(const Key('members-add')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('member-search')), 'c');
      await tester.pump(const Duration(milliseconds: 500));
      expect(repository.searches, isEmpty); // below the 2-character minimum

      await tester.enterText(find.byKey(const Key('member-search')), 'carl');
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      expect(repository.searches, ['carl']);
      expect(find.text('Carla'), findsOneWidget);
      expect(find.text('carla@example.com'), findsOneWidget);

      await tester.tap(find.byKey(const Key('search-result-u7')));
      await tester.pumpAndSettle();

      expect(find.text('Carla'), findsOneWidget);
      expect(repository.campaigns.single.members.map((m) => m.userId), contains('u7'));
      expect(find.text('Carla se ha añadido como Jugador.'), findsOneWidget);
    });

    testWidgets('el buscador no ofrece a quien ya es miembro', (tester) async {
      final repository = FakeCampaignsRepository(
        campaigns: [makeCampaign()],
        directory: _directory,
      );
      await _pumpDetail(tester, repository);
      await _openMembersTab(tester);

      await tester.tap(find.byKey(const Key('members-add')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('member-search')), 'example');
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('search-result-u7')), findsOneWidget);
      expect(find.byKey(const Key('search-result-p2')), findsNothing);
    });

    testWidgets('el Owner puede añadir un DM; un DM solo Player', (tester) async {
      final owner = FakeCampaignsRepository(campaigns: [makeCampaign()], directory: _directory);
      await _pumpDetail(tester, owner);
      await _openMembersTab(tester);
      await tester.tap(find.byKey(const Key('members-add')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('member-role')), findsOneWidget);
      expect(find.text('DM'), findsOneWidget);

      await tester.tap(find.text('DM'));
      await tester.enterText(find.byKey(const Key('member-search')), 'diego');
      await tester.pump(const Duration(milliseconds: 500));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('search-result-u8')));
      await tester.pumpAndSettle();
      expect(
        owner.campaigns.single.members.firstWhere((m) => m.userId == 'u8').role,
        CampaignRole.dm,
      );
    });

    testWidgets('un DM no puede elegir el rol DM al añadir', (tester) async {
      final repository = FakeCampaignsRepository(
        campaigns: [makeCampaign(myRole: CampaignRole.dm)],
        directory: _directory,
      );
      await _pumpDetail(tester, repository);
      await _openMembersTab(tester);

      await tester.tap(find.byKey(const Key('members-add')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('member-role')), findsNothing);
      expect(find.text('Se añadirá como Jugador.'), findsOneWidget);
    });

    testWidgets('el Owner cambia el rol de un jugador a DM', (tester) async {
      final repository = FakeCampaignsRepository(campaigns: [makeCampaign()]);
      await _pumpDetail(tester, repository);
      await _openMembersTab(tester);

      await tester.tap(find.byKey(const Key('member-menu-p2')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Cambiar rol'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('DM'));
      await tester.pumpAndSettle();

      expect(
        repository.campaigns.single.members.firstWhere((m) => m.userId == 'p2').role,
        CampaignRole.dm,
      );
    });

    testWidgets('el Owner quita a un miembro y no tiene menú propio', (tester) async {
      final repository = FakeCampaignsRepository(campaigns: [makeCampaign()]);
      await _pumpDetail(tester, repository);
      await _openMembersTab(tester);

      expect(find.byKey(const Key('member-menu-u1')), findsNothing);

      await tester.tap(find.byKey(const Key('member-menu-p2')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Quitar'));
      await tester.pumpAndSettle();

      expect(repository.campaigns.single.members.map((m) => m.userId), ['u1']);
    });

    testWidgets('un DM solo puede quitar a Players y no cambia roles', (tester) async {
      final repository = FakeCampaignsRepository(
        campaigns: [
          makeCampaign(
            myRole: CampaignRole.dm,
            members: [
              makeMember(userId: 'owner', displayName: 'Dueña Demo', role: CampaignRole.owner),
              makeMember(userId: 'u1', role: CampaignRole.dm),
              makeMember(userId: 'd2', displayName: 'Otro DM', role: CampaignRole.dm),
              makeMember(userId: 'p2', displayName: 'Beto'),
            ],
          ),
        ],
      );
      await _pumpDetail(tester, repository);
      await _openMembersTab(tester);

      expect(find.byKey(const Key('member-menu-owner')), findsNothing);
      expect(find.byKey(const Key('member-menu-d2')), findsNothing);
      await tester.tap(find.byKey(const Key('member-menu-p2')));
      await tester.pumpAndSettle();
      expect(find.text('Cambiar rol'), findsNothing);
      await tester.tap(find.text('Quitar'));
      await tester.pumpAndSettle();

      expect(repository.campaigns.single.members.map((m) => m.userId), isNot(contains('p2')));
    });
  });

  group('MemberPermissions', () {
    final owner = makeMember(userId: 'o', role: CampaignRole.owner);
    final dm = makeMember(userId: 'd', role: CampaignRole.dm);
    final player = makeMember(userId: 'p');

    test('solo el Owner cambia roles, y nunca el del Owner', () {
      expect(MemberPermissions.canChangeRole(CampaignRole.owner, player), isTrue);
      expect(MemberPermissions.canChangeRole(CampaignRole.owner, owner), isFalse);
      expect(MemberPermissions.canChangeRole(CampaignRole.dm, player), isFalse);
      expect(MemberPermissions.canChangeRole(CampaignRole.player, player), isFalse);
    });

    test('quitar: Owner a cualquiera salvo sí mismo; DM a Players', () {
      expect(MemberPermissions.canRemove(CampaignRole.owner, 'o', dm), isTrue);
      expect(MemberPermissions.canRemove(CampaignRole.owner, 'o', player), isTrue);
      expect(MemberPermissions.canRemove(CampaignRole.owner, 'o', owner), isFalse);
      expect(MemberPermissions.canRemove(CampaignRole.dm, 'x', player), isTrue);
      expect(MemberPermissions.canRemove(CampaignRole.dm, 'x', dm), isFalse);
      expect(MemberPermissions.canRemove(CampaignRole.dm, 'x', owner), isFalse);
      expect(MemberPermissions.canRemove(CampaignRole.player, 'x', player), isFalse);
    });

    test('los roles se ordenan Owner > DM > Player', () {
      expect(CampaignRole.owner.atLeast(CampaignRole.dm), isTrue);
      expect(CampaignRole.dm.atLeast(CampaignRole.dm), isTrue);
      expect(CampaignRole.player.atLeast(CampaignRole.dm), isFalse);
      expect(CampaignRole.fromApi('DM'), CampaignRole.dm);
    });
  });
}
