import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/auth/user_dto.dart';
import 'package:dnd_companion/features/home/data/server_info_repository.dart';
import 'package:dnd_companion/features/home/ui/home_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/fakes.dart';

Widget _app({required UserDto user, bool serverUp = true}) => ProviderScope(
  overrides: [
    authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(user))),
    if (serverUp)
      fakeServerInfoOverride
    else
      serverInfoProvider.overrideWith((ref) async => throw Exception('sin red')),
  ],
  child: const MaterialApp(home: HomePage()),
);

void main() {
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

  testWidgets('muestra nombre y rol, y oculta Usuarios a un User', (tester) async {
    await tester.pumpWidget(_app(user: makeUser()));
    await tester.pumpAndSettle();

    expect(find.text('Hola, Usuario Demo'), findsOneWidget);
    expect(find.text('Usuario'), findsOneWidget);
    expect(find.text('Cerrar sesión'), findsOneWidget);
    expect(find.text('Usuarios'), findsNothing);
  });

  testWidgets('un Admin ve el acceso a Usuarios', (tester) async {
    await tester.pumpWidget(
      _app(
        user: makeUser(displayName: 'Admin Demo', role: UserRole.admin),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Administrador'), findsOneWidget);
    expect(find.text('Usuarios'), findsOneWidget);
  });
}
