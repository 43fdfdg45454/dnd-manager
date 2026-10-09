import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/auth/user_dto.dart';
import 'package:dnd_companion/features/home/data/server_info_repository.dart';
import 'package:dnd_companion/features/home/ui/home_page.dart';
import 'package:dnd_companion/features/home/ui/profile_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/app_pump.dart';
import 'helpers/fakes.dart';

Widget _app({required UserDto user, bool serverUp = true, Widget home = const HomePage()}) =>
    ProviderScope(
      overrides: [
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(user))),
        fakeCampaignsOverride,
        fakeServerConfigOverride(),
        if (serverUp)
          fakeServerInfoOverride
        else
          serverInfoProvider.overrideWith((ref) async => throw Exception('sin red')),
      ],
      child: MaterialApp(home: home),
    );

void main() {
  group('Campañas', () {
    testWidgets('muestra el nombre y la versión del servidor', (tester) async {
      await tester.pumpWidget(_app(user: makeUser()));
      await tester.pumpAndSettle();

      expect(find.text('Conectado a dnd-companion-api v0.1.0'), findsOneWidget);
    });

    testWidgets('muestra error y botón de reintento si el servidor no responde', (tester) async {
      await tester.pumpWidget(_app(user: makeUser(), serverUp: false));
      await tester.pumpAndSettle();

      expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);
      expect(find.text('Reintentar'), findsOneWidget);
    });

    testWidgets('el cuerpo muestra la lista de campañas', (tester) async {
      await tester.pumpWidget(_app(user: makeUser()));
      await tester.pumpAndSettle();

      expect(find.text('Aún no tienes campañas'), findsOneWidget);
      expect(find.text('Nueva campaña'), findsOneWidget);
    });
  });

  group('Perfil', () {
    testWidgets('muestra nombre y rol, y oculta Usuarios a un User', (tester) async {
      await tester.pumpWidget(_app(user: makeUser(), home: const ProfilePage()));
      await tester.pumpAndSettle();

      expect(find.text('Usuario Demo'), findsOneWidget);
      expect(find.textContaining('· Usuario'), findsOneWidget);
      await revealProfileItem(tester, find.byKey(const Key('home-logout')));
      expect(find.text('Cerrar sesión'), findsOneWidget);
      expect(find.text('Usuarios'), findsNothing);
      expect(find.byKey(const Key('home-admin-content')), findsNothing);
    });

    testWidgets('da acceso a Personalización, Servidor y Atribuciones', (tester) async {
      await tester.pumpWidget(_app(user: makeUser(), home: const ProfilePage()));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('home-appearance')), findsOneWidget);
      await revealProfileItem(tester, find.byKey(const Key('home-server')));
      expect(find.text('Conectado a dnd-companion-api v0.1.0'), findsOneWidget);
      await revealProfileItem(tester, find.byKey(const Key('home-attribution')));
      expect(find.text('Atribuciones'), findsOneWidget);
    });

    testWidgets('un Admin ve el acceso a Usuarios y Contenido', (tester) async {
      await tester.pumpWidget(
        _app(
          user: makeUser(displayName: 'Admin Demo', role: UserRole.admin),
          home: const ProfilePage(),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.textContaining('Administrador'), findsOneWidget);
      await revealProfileItem(tester, find.byKey(const Key('home-admin-content')));
      expect(find.text('Usuarios'), findsOneWidget);
      expect(find.byKey(const Key('home-admin-users')), findsOneWidget);
    });
  });
}
