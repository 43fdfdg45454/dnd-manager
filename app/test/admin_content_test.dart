import 'package:dio/dio.dart';
import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/auth/user_dto.dart';
import 'package:dnd_companion/features/admin/data/admin_users_repository.dart';
import 'package:dnd_companion/features/admin/data/content_packs_controller.dart';
import 'package:dnd_companion/features/admin/data/content_packs_repository.dart';
import 'package:dnd_companion/features/admin/domain/content_pack.dart';
import 'package:dnd_companion/features/admin/ui/admin_content_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/app_pump.dart';
import 'helpers/content_pack_fakes.dart';
import 'helpers/fakes.dart';

final _admin = makeUser(
  id: 'admin',
  email: 'admin@example.com',
  displayName: 'Admin Demo',
  role: UserRole.admin,
);

const _picked = (path: '/tmp/mercaderes-ejemplo.json', name: 'mercaderes-ejemplo.json');

Future<FakeContentPacksRepository> _pumpPage(
  WidgetTester tester, {
  List<ContentPack>? packs,
  PickedPackFile? picked = _picked,
}) async {
  final repository = FakeContentPacksRepository(packs ?? [makeContentPack()]);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(_admin))),
        contentPacksRepositoryProvider.overrideWithValue(repository),
        contentPackPickerProvider.overrideWithValue(fakePackPicker(picked)),
      ],
      child: const MaterialApp(home: AdminContentPage()),
    ),
  );
  await tester.pumpAndSettle();
  return repository;
}

void main() {
  testWidgets('lista los paquetes con versión, fecha y recuentos', (tester) async {
    await _pumpPage(
      tester,
      packs: [
        makeContentPack(),
        makeContentPack(id: 'otro', name: 'Otro Paquete', version: '2.1.0', counts: const {}),
      ],
    );

    expect(find.byKey(const Key('content-pack-reinos-ejemplo')), findsOneWidget);
    expect(find.text('Reinos de Ejemplo'), findsOneWidget);
    expect(find.textContaining('Versión 1.0.0'), findsOneWidget);
    expect(find.textContaining('14/03/2026'), findsWidgets);
    expect(find.textContaining('1 subclase · 2 objetos · 1 conjuro'), findsOneWidget);
    expect(find.byKey(const Key('content-pack-otro')), findsOneWidget);
    expect(find.byKey(const Key('content-delete-otro')), findsOneWidget);
    expect(find.byKey(const Key('content-import')), findsOneWidget);
  });

  testWidgets('sin paquetes lo explica', (tester) async {
    await _pumpPage(tester, packs: const []);

    expect(find.byKey(const Key('content-empty')), findsOneWidget);
  });

  testWidgets('importar envía el fichero elegido y refresca la lista', (tester) async {
    final repository = await _pumpPage(tester);

    await tester.tap(find.byKey(const Key('content-import')));
    await tester.pumpAndSettle();

    expect(repository.imported, [(path: _picked.path, name: _picked.name)]);
    expect(find.byKey(const Key('content-pack-mercaderes-ejemplo')), findsOneWidget);
    expect(find.text('Paquete mercaderes-ejemplo'), findsOneWidget);
    expect(find.byKey(const Key('content-import-progress')), findsNothing);
    expect(find.textContaining('importado: 3 objetos'), findsOneWidget);
    // The previous pack is still there.
    expect(find.byKey(const Key('content-pack-reinos-ejemplo')), findsOneWidget);
  });

  testWidgets('cancelar el selector no sube nada', (tester) async {
    final repository = await _pumpPage(tester, picked: null);

    await tester.tap(find.byKey(const Key('content-import')));
    await tester.pumpAndSettle();

    expect(repository.imported, isEmpty);
  });

  testWidgets('los errores de validación se listan uno a uno', (tester) async {
    final repository = await _pumpPage(tester);
    repository.importError = contentPackInvalid([
      'items[3].modifiers[0].kind: Tipo de modificador desconocido.',
      'spells[1].level: El nivel debe estar entre 0 y 9.',
    ]);

    await tester.tap(find.byKey(const Key('content-import')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('content-import-errors')), findsOneWidget);
    expect(
      find.text('items[3].modifiers[0].kind: Tipo de modificador desconocido.'),
      findsOneWidget,
    );
    expect(find.text('spells[1].level: El nivel debe estar entre 0 y 9.'), findsOneWidget);
    expect(find.text('El paquete de contenido tiene 2 errores.'), findsOneWidget);
    expect(find.byKey(const Key('content-import-progress')), findsNothing);
    expect(repository.packs, hasLength(1));

    await tester.tap(find.byKey(const Key('content-import-errors-close')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('content-import-errors')), findsNothing);
  });

  testWidgets('un error sin lista usa un mensaje en español', (tester) async {
    final repository = await _pumpPage(tester);
    repository.importError = dioError(413);

    await tester.tap(find.byKey(const Key('content-import')));
    await tester.pumpAndSettle();

    expect(find.text('El paquete supera el tamaño máximo permitido (20 MB).'), findsOneWidget);
    expect(find.byKey(const Key('content-import-errors')), findsNothing);
  });

  testWidgets('borrar pide confirmación y llama al repositorio', (tester) async {
    final repository = await _pumpPage(tester);

    await tester.tap(find.byKey(const Key('content-delete-reinos-ejemplo')));
    await tester.pumpAndSettle();
    expect(find.text('Eliminar paquete'), findsWidgets);

    // Cancelling keeps the pack.
    await tester.tap(find.text('Cancelar'));
    await tester.pumpAndSettle();
    expect(repository.deleted, isEmpty);
    expect(find.byKey(const Key('content-pack-reinos-ejemplo')), findsOneWidget);

    await tester.tap(find.byKey(const Key('content-delete-reinos-ejemplo')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm-action')));
    await tester.pumpAndSettle();

    expect(repository.deleted, ['reinos-ejemplo']);
    expect(find.byKey(const Key('content-pack-reinos-ejemplo')), findsNothing);
    expect(find.text('Paquete eliminado.'), findsOneWidget);
  });

  testWidgets('un error al borrar se muestra', (tester) async {
    final repository = await _pumpPage(tester);
    repository.deleteError = dioError(404);

    await tester.tap(find.byKey(const Key('content-delete-reinos-ejemplo')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm-action')));
    await tester.pumpAndSettle();

    expect(find.text('El paquete ya no existe.'), findsOneWidget);
  });

  test('contentPackErrors lee la lista y también el mapa por campo', () {
    DioException badRequest(Object? errors) {
      final options = RequestOptions(path: '/x');
      return DioException(
        requestOptions: options,
        response: Response<dynamic>(
          requestOptions: options,
          statusCode: 400,
          data: {'errors': errors},
        ),
      );
    }

    expect(contentPackErrors(badRequest(['a: uno', 'b: dos'])), ['a: uno', 'b: dos']);
    expect(
      contentPackErrors(
        badRequest({
          'file': ['Falta el fichero del paquete.'],
        }),
      ),
      ['file: Falta el fichero del paquete.'],
    );
    expect(contentPackErrors(badRequest(null)), isEmpty);
    expect(contentPackErrors(dioError(500)), isEmpty);
    expect(contentPackErrors(StateError('x')), isEmpty);
  });

  test('ContentPack.fromJson y el resumen de recuentos', () {
    final pack = ContentPack.fromJson({
      'id': 'p',
      'name': 'P',
      'version': '1',
      'importedAt': '2026-03-14T12:00:00Z',
      'counts': {'subclasses': 1, 'features': 4, 'items': 0, 'traits': 2, 'backgrounds': 1},
    });
    expect(pack.importedAt, DateTime.utc(2026, 3, 14, 12));
    expect(pack.countsSummary, '1 subclase · 4 rasgos de clase · 2 rasgos raciales · 1 trasfondo');
    expect(contentPackCountsSummary(const {}), isEmpty);
  });

  group('navegación', () {
    Future<void> pumpAdminApp(WidgetTester tester, {UserDto? user}) async {
      final packs = FakeContentPacksRepository([makeContentPack()]);
      await pumpRealApp(
        tester,
        location: '/admin/users',
        user: user ?? _admin,
        overrides: [
          adminUsersRepositoryProvider.overrideWithValue(FakeAdminUsersRepository([_admin])),
          contentPacksRepositoryProvider.overrideWithValue(packs),
        ],
      );
    }

    testWidgets('la pantalla de usuarios enlaza con el contenido', (tester) async {
      await pumpAdminApp(tester);

      await tester.tap(find.byKey(const Key('admin-content')));
      await tester.pumpAndSettle();

      expect(find.byType(AdminContentPage), findsOneWidget);
      expect(find.byKey(const Key('content-pack-reinos-ejemplo')), findsOneWidget);
    });

    testWidgets('un usuario sin rol de administrador no entra en /admin/content', (tester) async {
      final router = await pumpRealApp(
        tester,
        location: '/admin/content',
        overrides: [
          contentPacksRepositoryProvider.overrideWithValue(FakeContentPacksRepository(const [])),
        ],
      );

      expect(find.byType(AdminContentPage), findsNothing);
      expect(locationOf(router), isNot('/admin/content'));
    });
  });
}
