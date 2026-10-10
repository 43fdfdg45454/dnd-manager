import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/auth/auth_controller.dart';
import 'package:opentrpg_core/core/auth/auth_state.dart';
import 'package:opentrpg_core/core/auth/user_dto.dart';
import 'package:opentrpg_core/features/admin/data/admin_users_repository.dart';
import 'package:opentrpg_core/features/admin/ui/admin_users_page.dart';

import 'helpers/fakes.dart';

final _admin = makeUser(
  id: 'admin',
  email: 'admin@example.com',
  displayName: 'Admin Demo',
  role: UserRole.admin,
);

Future<FakeAdminUsersRepository> _pumpPage(WidgetTester tester) async {
  final repository = FakeAdminUsersRepository([
    _admin,
    makeUser(id: 'u2', email: 'ana@example.com', displayName: 'Ana'),
  ]);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(_admin))),
        adminUsersRepositoryProvider.overrideWithValue(repository),
      ],
      child: const MaterialApp(home: AdminUsersPage()),
    ),
  );
  await tester.pumpAndSettle();
  return repository;
}

Future<void> _openNewUserDialog(WidgetTester tester) async {
  await tester.tap(find.text('Nuevo usuario'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('lista los usuarios', (tester) async {
    await _pumpPage(tester);

    expect(find.text('Admin Demo'), findsOneWidget);
    expect(find.text('Ana'), findsOneWidget);
  });

  testWidgets('el admin crea un usuario y aparece en la lista', (tester) async {
    final repository = await _pumpPage(tester);

    await _openNewUserDialog(tester);
    await tester.enterText(find.byKey(const Key('new-user-email')), 'beto@example.com');
    await tester.enterText(find.byKey(const Key('new-user-name')), 'Beto');
    await tester.tap(find.text('Crear'));
    await tester.pumpAndSettle();

    expect(find.text('Beto'), findsOneWidget);
    expect(find.textContaining('Pendiente de alta'), findsOneWidget);
    expect(find.text('Usuario creado. Se ha enviado el correo de alta.'), findsOneWidget);
    expect(repository.users, hasLength(3));
  });

  testWidgets('un 409 muestra que el correo ya existe', (tester) async {
    final repository = await _pumpPage(tester);
    repository.createError = dioError(409);

    await _openNewUserDialog(tester);
    await tester.enterText(find.byKey(const Key('new-user-email')), 'ana@example.com');
    await tester.enterText(find.byKey(const Key('new-user-name')), 'Otra Ana');
    await tester.tap(find.text('Crear'));
    await tester.pumpAndSettle();

    expect(find.text('Ya existe un usuario con ese correo.'), findsOneWidget);
    expect(find.text('Otra Ana'), findsNothing);
  });

  testWidgets('el diálogo valida los campos', (tester) async {
    await _pumpPage(tester);

    await _openNewUserDialog(tester);
    await tester.tap(find.text('Crear'));
    await tester.pumpAndSettle();

    expect(find.text('Introduce tu correo electrónico'), findsOneWidget);
    expect(find.text('Introduce un nombre'), findsOneWidget);
  });

  testWidgets('desactiva un usuario desde el menú', (tester) async {
    final repository = await _pumpPage(tester);

    await tester.tap(find.byKey(const Key('user-menu-u2')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Desactivar'));
    await tester.pumpAndSettle();

    expect(repository.users[1].isActive, isFalse);
    expect(find.text('Usuario desactivado.'), findsOneWidget);
  });

  testWidgets('cambia el rol de un usuario', (tester) async {
    final repository = await _pumpPage(tester);

    await tester.tap(find.byKey(const Key('user-menu-u2')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cambiar rol'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Administrador'));
    await tester.pumpAndSettle();

    expect(repository.users[1].role, UserRole.admin);
  });

  testWidgets('reenvía el correo de alta', (tester) async {
    final repository = await _pumpPage(tester);

    await tester.tap(find.byKey(const Key('user-menu-u2')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Reenviar correo de alta'));
    await tester.pumpAndSettle();

    expect(repository.resent, ['u2']);
  });

  testWidgets('el propio admin no puede desactivarse desde el menú', (tester) async {
    final repository = await _pumpPage(tester);

    await tester.tap(find.byKey(const Key('user-menu-admin')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Desactivar'), warnIfMissed: false);
    await tester.pumpAndSettle();

    expect(repository.users[0].isActive, isTrue);
  });

  testWidgets('la búsqueda filtra la lista', (tester) async {
    await _pumpPage(tester);

    await tester.enterText(find.byKey(const Key('admin-search')), 'ana');
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pumpAndSettle();

    expect(find.text('Ana'), findsOneWidget);
    expect(find.text('Admin Demo'), findsNothing);
  });
}
