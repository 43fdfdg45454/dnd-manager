import 'package:dnd_companion/app.dart';
import 'package:dnd_companion/core/auth/auth_repository.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/auth/token_storage.dart';
import 'package:dnd_companion/core/auth/user_dto.dart';
import 'package:dnd_companion/core/router/app_router.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/fakes.dart';

Future<void> _pumpApp(
  WidgetTester tester, {
  required FakeTokenStorage storage,
  required FakeAuthRepository repository,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        tokenStorageProvider.overrideWithValue(storage),
        authRepositoryProvider.overrideWithValue(repository),
        fakeServerInfoOverride,
        fakeCampaignsOverride,
      ],
      child: const DndCompanionApp(),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _fillAndSubmit(WidgetTester tester) async {
  await tester.enterText(find.byKey(const Key('login-email')), 'user@example.com');
  await tester.enterText(find.byKey(const Key('login-password')), 'change-me-123');
  await tester.tap(find.text('Entrar'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('sin sesión guardada muestra el login', (tester) async {
    final storage = FakeTokenStorage();
    await _pumpApp(
      tester,
      storage: storage,
      repository: FakeAuthRepository(storage: storage),
    );

    expect(find.text('Entrar'), findsOneWidget);
    expect(find.text('¿Olvidaste tu contraseña?'), findsOneWidget);
  });

  testWidgets('login correcto pasa a signedIn y muestra el inicio', (tester) async {
    final storage = FakeTokenStorage();
    final repository = FakeAuthRepository(storage: storage, loginUser: makeUser());
    await _pumpApp(tester, storage: storage, repository: repository);

    await _fillAndSubmit(tester);

    await tester.tap(find.byKey(const Key('home-user-menu')));
    await tester.pumpAndSettle();
    expect(find.text('Hola, Usuario Demo'), findsOneWidget);
    expect(find.text('Entrar'), findsNothing);
    expect(storage.refresh, isNotNull);
  });

  testWidgets('credenciales inválidas muestran el error y siguen en el login', (tester) async {
    final storage = FakeTokenStorage();
    final repository = FakeAuthRepository(storage: storage, loginError: dioError(401));
    await _pumpApp(tester, storage: storage, repository: repository);

    await _fillAndSubmit(tester);

    expect(find.text('Correo o contraseña incorrectos.'), findsOneWidget);
    expect(find.text('Entrar'), findsOneWidget);
  });

  testWidgets('fallo de red muestra el mensaje de conexión', (tester) async {
    final storage = FakeTokenStorage();
    final repository = FakeAuthRepository(storage: storage, loginError: dioError(null));
    await _pumpApp(tester, storage: storage, repository: repository);

    await _fillAndSubmit(tester);

    expect(find.text('No se pudo conectar con el servidor. Revisa tu conexión.'), findsOneWidget);
  });

  testWidgets('valida el formulario antes de llamar al servidor', (tester) async {
    final storage = FakeTokenStorage();
    final repository = FakeAuthRepository(storage: storage, loginUser: makeUser());
    await _pumpApp(tester, storage: storage, repository: repository);

    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Introduce tu correo electrónico'), findsOneWidget);
    expect(find.text('Introduce tu contraseña'), findsOneWidget);
    expect(storage.refresh, isNull);
  });

  testWidgets('restaura la sesión con el refresh token guardado', (tester) async {
    final storage = FakeTokenStorage()..refresh = 'refresh-1';
    final repository = FakeAuthRepository(storage: storage, meUser: makeUser());
    await _pumpApp(tester, storage: storage, repository: repository);

    await tester.tap(find.byKey(const Key('home-user-menu')));
    await tester.pumpAndSettle();
    expect(find.text('Hola, Usuario Demo'), findsOneWidget);
  });

  testWidgets('cerrar sesión vuelve al login', (tester) async {
    final storage = FakeTokenStorage()..refresh = 'refresh-1';
    final repository = FakeAuthRepository(storage: storage, meUser: makeUser());
    await _pumpApp(tester, storage: storage, repository: repository);

    await tester.tap(find.byKey(const Key('home-user-menu')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cerrar sesión'));
    await tester.pumpAndSettle();

    expect(find.text('Entrar'), findsOneWidget);
    expect(repository.logoutCalls, 1);
    expect(storage.refresh, isNull);
  });

  testWidgets('olvidé mi contraseña muestra la confirmación', (tester) async {
    final storage = FakeTokenStorage();
    final repository = FakeAuthRepository(storage: storage);
    await _pumpApp(tester, storage: storage, repository: repository);

    await tester.tap(find.text('¿Olvidaste tu contraseña?'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('forgot-email')), 'nobody@example.com');
    await tester.tap(find.text('Enviar enlace'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Si el correo existe, recibirás un enlace'), findsOneWidget);
    expect(repository.forgotRequests, ['nobody@example.com']);
  });

  group('authRedirect', () {
    final admin = AuthSignedIn(makeUser(role: UserRole.admin));
    final user = AuthSignedIn(makeUser());

    test('unknown solo permite el splash', () {
      expect(authRedirect(const AuthUnknown(), '/'), AppRoutes.splash);
      expect(authRedirect(const AuthUnknown(), AppRoutes.splash), isNull);
    });

    test('signedOut solo permite login y recuperación', () {
      expect(authRedirect(const AuthSignedOut(), '/'), AppRoutes.login);
      expect(authRedirect(const AuthSignedOut(), AppRoutes.adminUsers), AppRoutes.login);
      expect(authRedirect(const AuthSignedOut(), AppRoutes.login), isNull);
      expect(authRedirect(const AuthSignedOut(), AppRoutes.forgotPassword), isNull);
    });

    test('signedIn bloquea el login y /admin a un User', () {
      expect(authRedirect(user, AppRoutes.login), AppRoutes.home);
      expect(authRedirect(user, AppRoutes.splash), AppRoutes.home);
      expect(authRedirect(user, AppRoutes.adminUsers), AppRoutes.home);
      expect(authRedirect(user, '/'), isNull);
      expect(authRedirect(admin, AppRoutes.adminUsers), isNull);
    });
  });
}
