import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_repository.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/network/api_client.dart';
import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/features/campaigns/data/campaigns_repository.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/home/ui/home_page.dart';
import 'package:dnd_companion/features/session/data/messages_repository.dart';
import 'package:dnd_companion/features/sessions/data/models.dart';
import 'package:dnd_companion/features/sessions/data/sessions_controllers.dart';
import 'package:dnd_companion/features/sessions/data/sessions_repository.dart';
import 'package:dnd_companion/features/sessions/domain/journal_entries.dart';
import 'package:dnd_companion/features/sessions/domain/sessions_format.dart';
import 'package:dnd_companion/features/sessions/ui/session_form_page.dart';
import 'package:dnd_companion/features/sessions/ui/session_page.dart';
import 'package:dnd_companion/features/sessions/ui/summary_editor_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:table_calendar/table_calendar.dart';

import 'helpers/app_pump.dart';
import 'helpers/fake_realtime_hub.dart';
import 'helpers/fakes.dart';
import 'helpers/motion.dart';
import 'helpers/party_fakes.dart';
import 'helpers/session_fakes.dart';

/// App with the routes of the phase 8 screens. The signed-in user is `u1`.
Future<GoRouter> _pumpApp(
  WidgetTester tester, {
  required String location,
  CampaignRole role = CampaignRole.player,
  FakeSessionsRepository? sessions,
  FakeCampaignsRepository? campaigns,
  FakeAuthRepository? authRepository,
}) async {
  final router = buildTestRouter(
    location: location,
    routes: [
      GoRoute(path: '/', builder: (_, _) => const HomePage()),
      GoRoute(
        path: AppRoutes.campaignSessionNew,
        builder: (_, state) => SessionFormPage(campaignId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: AppRoutes.campaignSession,
        builder: (_, state) => SessionPage(
          campaignId: state.pathParameters['id']!,
          sessionId: state.pathParameters['sessionId']!,
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignSessionEdit,
        builder: (_, state) => SessionFormPage(
          campaignId: state.pathParameters['id']!,
          sessionId: state.pathParameters['sessionId'],
        ),
      ),
      GoRoute(
        path: AppRoutes.campaignSessionSummary,
        builder: (_, state) => SummaryEditorPage(
          campaignId: state.pathParameters['id']!,
          sessionId: state.pathParameters['sessionId']!,
        ),
      ),
    ],
  );
  tester.view.physicalSize = const Size(1400, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final user = makeUser();
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        messagesRepositoryProvider.overrideWithValue(FakeMessagesRepository()),
        fakeRealtimeOverride(),
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(user))),
        authRepositoryProvider.overrideWithValue(
          authRepository ?? FakeAuthRepository(storage: FakeTokenStorage(), meUser: user),
        ),
        campaignsRepositoryProvider.overrideWithValue(
          campaigns ?? FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
        ),
        sessionsOverride(sessions ?? FakeSessionsRepository(isDm: role.isAtLeastDm)),
        sessionsClockProvider.overrideWithValue(() => sessionsTestNow),
        fakeServerConfigOverride(),
        fakeServerInfoOverride,
      ],
      child: MaterialApp.router(routerConfig: router, builder: reducedMotionBuilder),
    ),
  );
  await tester.pumpAndSettle();
  return router;
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

final _upcoming = makeSession(
  id: 's3',
  number: 3,
  title: 'La Torre del Mago',
  startsAt: DateTime.utc(2026, 10, 10, 18),
  location: 'Casa de Marta',
  myRsvp: RsvpStatus.yes,
  counts: const RsvpCounts(yes: 2, no: 1, maybe: 0, pending: 1),
);

final _past = makeSession(
  id: 's2',
  number: 2,
  title: 'Cruce de Caminos',
  startsAt: DateTime.utc(2026, 9, 20, 18),
  status: SessionStatus.done,
  summary: 'Los héroes **cruzaron** el puente.',
  counts: const RsvpCounts(yes: 3, no: 1),
);

final _oldest = makeSession(
  id: 's1',
  number: 1,
  title: 'La Taberna',
  startsAt: DateTime.utc(2026, 9, 6, 18),
  status: SessionStatus.done,
  summary: 'Se conocieron en la taberna.',
);

/// Records every request and answers with [body].
class _RecordingAdapter implements HttpClientAdapter {
  final List<RequestOptions> requests = [];
  Object? body = <String, dynamic>{};

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    return ResponseBody.fromString(
      jsonEncode(body),
      200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Map<String, dynamic> _sessionJson() => {
  'id': 's1',
  'number': 1,
  'campaignId': 'c1',
  'campaignName': 'La Mina Perdida',
  'title': 'T',
  'startsAt': '2026-10-10T18:00:00+00:00',
  'startsAtLocal': '2026-10-10T20:00:00+02:00',
  'timeZoneId': 'Europe/Madrid',
  'status': 'Scheduled',
  'rsvps': [],
  'counts': {'yes': 0, 'no': 0, 'maybe': 0, 'pending': 1},
};

void main() {
  group('repositorio de sesiones: contrato HTTP', () {
    late _RecordingAdapter adapter;
    late SessionsRepository repository;

    setUp(() {
      adapter = _RecordingAdapter();
      repository = SessionsRepository(
        ApiClient(
          baseUrl: 'http://localhost',
          dio: Dio(BaseOptions(baseUrl: 'http://localhost'))..httpClientAdapter = adapter,
        ),
      );
    });

    test('crear envía startsAt en UTC y omite lo vacío', () async {
      adapter.body = _sessionJson();
      await repository.create(
        'c1',
        SessionDraft(title: 'T', startsAt: DateTime.utc(2026, 10, 10, 18), location: '  '),
      );
      final request = adapter.requests.single;
      expect((request.method, request.path), ('POST', '/api/v1/campaigns/c1/sessions'));
      expect(request.data, {'title': 'T', 'startsAt': '2026-10-10T18:00:00.000Z'});
    });

    test('listar pide también las pasadas', () async {
      adapter.body = [_sessionJson()];
      final sessions = await repository.list('c1');
      expect(adapter.requests.single.queryParameters, {'includePast': true});
      expect(sessions.single.id, 's1');
    });

    test('responder, resumen, aviso y diario usan las rutas del contrato', () async {
      adapter.body = _sessionJson();
      await repository.rsvp('s1', RsvpStatus.no, comment: ' Viaje ');
      expect(adapter.requests.last.method, 'PUT');
      expect(adapter.requests.last.path, '/api/v1/sessions/s1/rsvp');
      expect(adapter.requests.last.data, {'status': 'No', 'comment': 'Viaje'});

      await repository.setSummary('s1', '');
      expect(adapter.requests.last.path, '/api/v1/sessions/s1/summary');
      expect(adapter.requests.last.data, {'summaryMarkdown': ''});

      await repository.notify('s1', subject: 'Asunto', message: 'Mensaje');
      expect(adapter.requests.last.path, '/api/v1/sessions/s1/notify');
      expect(adapter.requests.last.data, {'subject': 'Asunto', 'message': 'Mensaje'});

      adapter.body = {
        'items': [
          {
            'id': 's1',
            'number': 1,
            'title': 'T',
            'startsAt': '2026-10-10T18:00:00+00:00',
            'startsAtLocal': '2026-10-10T20:00:00+02:00',
            'status': 'Done',
            'summaryMarkdown': 'Texto',
            'summaryUpdatedAt': null,
          },
        ],
        'total': 1,
        'page': 1,
        'pageSize': 100,
      };
      final page = await repository.journal('c1');
      expect(adapter.requests.last.path, '/api/v1/campaigns/c1/journal');
      expect(page.items.single.summaryMarkdown, 'Texto');

      adapter.body = [_sessionJson()];
      await repository.mySessions();
      expect(adapter.requests.last.path, '/api/v1/me/sessions');
    });

    test('ajustes de campaña y perfil envían solo los campos indicados', () async {
      final campaigns = CampaignsRepository(
        ApiClient(
          baseUrl: 'http://localhost',
          dio: Dio(BaseOptions(baseUrl: 'http://localhost'))..httpClientAdapter = adapter,
        ),
      );
      adapter.body = {
        'id': 'c1',
        'name': 'N',
        'description': '',
        'ownerId': 'o',
        'ownerDisplayName': 'O',
        'myRole': 'DM',
        'members': [],
        'createdAt': '2026-01-01T00:00:00Z',
        'updatedAt': '2026-01-01T00:00:00Z',
        'timeZoneId': 'Asia/Tokyo',
        'reminderOffsetsMinutes': [60],
      };
      final updated = await campaigns.updateSettings('c1', reminderOffsetsMinutes: [60]);
      expect(adapter.requests.last.method, 'PATCH');
      expect(adapter.requests.last.path, '/api/v1/campaigns/c1/settings');
      expect(adapter.requests.last.data, {
        'reminderOffsetsMinutes': [60],
      });
      expect(updated.timeZoneId, 'Asia/Tokyo');
      expect(updated.reminderOffsetsMinutes, [60]);

      final auth = AuthRepository(
        ApiClient(
          baseUrl: 'http://localhost',
          dio: Dio(BaseOptions(baseUrl: 'http://localhost'))..httpClientAdapter = adapter,
        ),
        FakeTokenStorage(),
      );
      adapter.body = {
        'id': 'u1',
        'email': 'user@example.com',
        'displayName': 'X',
        'role': 'User',
        'isActive': true,
        'hasPassword': true,
        'createdAt': '2026-01-01T00:00:00Z',
        'notificationsEnabled': false,
      };
      final user = await auth.updateProfile(notificationsEnabled: false);
      expect(adapter.requests.last.method, 'PATCH');
      expect(adapter.requests.last.path, '/api/v1/auth/me');
      expect(adapter.requests.last.data, {'notificationsEnabled': false});
      expect(user.notificationsEnabled, isFalse);
    });
  });

  group('sesiones: pestaña', () {
    testWidgets('lista próximas y pasadas con número, fecha local, lugar y respuestas', (
      tester,
    ) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        sessions: FakeSessionsRepository(sessions: [_past, _upcoming]),
      );
      await openGeneralSection(tester, 'sessions');

      expect(find.byKey(const Key('sessions-upcoming-header')), findsOneWidget);
      expect(find.byKey(const Key('sessions-past-header')), findsOneWidget);
      expect(find.text('Sesión 3 · La Torre del Mago'), findsOneWidget);
      expect(find.text('Sesión 2 · Cruce de Caminos'), findsOneWidget);
      // 18:00 UTC is 20:00 in Europe/Madrid (UTC+2).
      expect(find.text('sáb 10 oct 2026 · 20:00'), findsOneWidget);
      expect(find.text('Casa de Marta'), findsOneWidget);
      expect(find.text('Sí 2'), findsOneWidget);
      expect(find.text('No 1'), findsAtLeastNWidgets(1));
      expect(find.text('Quizá 0'), findsAtLeastNWidgets(1));
      expect(find.text('Tu respuesta: Sí'), findsOneWidget);
      expect(find.text('Hecha'), findsOneWidget);

      // The upcoming one is listed before the past one.
      final upcomingY = tester.getTopLeft(find.byKey(const Key('session-tile-s3'))).dy;
      final pastY = tester.getTopLeft(find.byKey(const Key('session-tile-s2'))).dy;
      expect(upcomingY, lessThan(pastY));
    });

    testWidgets('un jugador no ve "Nueva sesión"', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        sessions: FakeSessionsRepository(sessions: [_upcoming]),
      );
      await openGeneralSection(tester, 'sessions');

      expect(find.byKey(const Key('sessions-new')), findsNothing);
      expect(find.text('Nueva sesión'), findsNothing);
    });

    testWidgets('un DM ve "Nueva sesión" y abre el formulario', (tester) async {
      await _pumpApp(tester, location: '/campaigns/c1', role: CampaignRole.dm);
      await openGeneralSection(tester, 'sessions');

      expect(find.text('No hay sesiones programadas.'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('sessions-new')));
      expect(find.byKey(const Key('session-field-title')), findsOneWidget);
      expect(
        find.textContaining('Zona horaria de la campaña: Europe/Madrid (UTC+'),
        findsOneWidget,
      );
    });

    testWidgets('la vista mensual marca los días con sesión y lista las del día elegido', (
      tester,
    ) async {
      final inMonth = makeSession(
        id: 's9',
        number: 9,
        title: 'Sesión del día',
        startsAt: DateTime.utc(2026, 10, 15, 17),
      );
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        sessions: FakeSessionsRepository(sessions: [inMonth]),
      );
      await openGeneralSection(tester, 'sessions');
      await _tap(tester, find.byKey(const Key('sessions-view-calendar')));

      final calendar = tester.widget<TableCalendar<Session>>(
        find.byKey(const Key('sessions-calendar')),
      );
      expect(calendar.eventLoader!(DateTime.utc(2026, 10, 15)), hasLength(1));
      expect(calendar.eventLoader!(DateTime.utc(2026, 10, 16)), isEmpty);

      await tester.tap(find.text('15'));
      await tester.pumpAndSettle();
      expect(find.text('Sesión 9 · Sesión del día'), findsOneWidget);

      await tester.tap(find.byKey(const Key('sessions-view-list')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('sessions-calendar')), findsNothing);
    });

    testWidgets('un error de red muestra el mensaje y permite reintentar', (tester) async {
      final fake = FakeSessionsRepository(sessions: [_upcoming])..error = dioError(null);
      await _pumpApp(tester, location: '/campaigns/c1', sessions: fake);
      await openGeneralSection(tester, 'sessions');

      expect(find.text('No se pudo conectar con el servidor. Revisa tu conexión.'), findsOneWidget);
      fake.error = null;
      await _tap(tester, find.text('Reintentar'));
      expect(find.text('Sesión 3 · La Torre del Mago'), findsOneWidget);
    });
  });

  group('sesiones: detalle y asistencia', () {
    testWidgets('responder asistencia actualiza el chip, el contador y envía el comentario', (
      tester,
    ) async {
      final fake = FakeSessionsRepository(
        sessions: [
          makeSession(
            id: 's3',
            number: 3,
            counts: const RsvpCounts(pending: 3),
            location: 'Casa de Marta',
          ),
        ],
      );
      await _pumpApp(tester, location: '/campaigns/c1/sessions/s3', sessions: fake);

      expect(find.text('Sin responder'), findsOneWidget);
      expect(find.text('Casa de Marta'), findsOneWidget);
      ChoiceChip chip(String key) => tester.widget<ChoiceChip>(find.byKey(Key(key)));
      expect(chip('rsvp-yes').selected, isFalse);

      await tester.enterText(find.byKey(const Key('rsvp-comment')), 'Llego tarde');
      await _tap(tester, find.byKey(const Key('rsvp-maybe')));

      expect(fake.rsvpCalls.single.status, RsvpStatus.maybe);
      expect(fake.rsvpCalls.single.comment, 'Llego tarde');
      expect(chip('rsvp-maybe').selected, isTrue);
      expect(chip('rsvp-yes').selected, isFalse);
      expect(find.text('Tu respuesta: Quizá'), findsOneWidget);
      expect(find.text('Quizá 1'), findsOneWidget);
      // The answers list shows my answer with the comment and the rest as pending.
      expect(find.byKey(const Key('response-u1')), findsOneWidget);
      expect(
        find.descendant(
          of: find.byKey(const Key('response-u1')),
          matching: find.text('Llego tarde'),
        ),
        findsOneWidget,
      );
      expect(find.byKey(const Key('pending-p2')), findsOneWidget);
      expect(find.byKey(const Key('pending-owner')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('rsvp-yes')));
      expect(chip('rsvp-yes').selected, isTrue);
      expect(chip('rsvp-maybe').selected, isFalse);
    });

    testWidgets('en una sesión cancelada no se puede responder', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/sessions/s3',
        sessions: FakeSessionsRepository(
          sessions: [makeSession(id: 's3', number: 3, status: SessionStatus.cancelled)],
        ),
      );

      expect(find.text('La sesión está cancelada.'), findsOneWidget);
      expect(tester.widget<ChoiceChip>(find.byKey(const Key('rsvp-yes'))).onSelected, isNull);
    });

    testWidgets('un 409 al responder muestra "La sesión está cancelada."', (tester) async {
      final fake = FakeSessionsRepository(sessions: [makeSession(id: 's3', number: 3)]);
      await _pumpApp(tester, location: '/campaigns/c1/sessions/s3', sessions: fake);
      fake.error = dioError(409);

      await _tap(tester, find.byKey(const Key('rsvp-yes')));
      expect(find.text('La sesión está cancelada.'), findsOneWidget);
    });

    testWidgets('un jugador no ve el menú de acciones, los recordatorios ni el botón de resumen', (
      tester,
    ) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/sessions/s3',
        sessions: FakeSessionsRepository(sessions: [makeSession(id: 's3', number: 3)]),
      );

      expect(find.byKey(const Key('session-menu')), findsNothing);
      expect(find.text('Recordatorios por correo'), findsNothing);
      expect(find.byKey(const Key('session-summary-edit')), findsNothing);
      expect(find.text('Aún no hay resumen de esta sesión.'), findsOneWidget);
    });

    testWidgets('el DM ve los recordatorios con su estado y envía un aviso', (tester) async {
      final fake = FakeSessionsRepository(
        isDm: true,
        sessions: [
          makeSession(
            id: 's3',
            number: 3,
            reminders: [
              SessionReminder(
                offsetMinutes: 1440,
                sendAt: DateTime.utc(2026, 10, 9, 18),
                sentAt: DateTime.utc(2026, 10, 9, 18, 1),
              ),
              SessionReminder(offsetMinutes: 120, sendAt: DateTime.utc(2026, 10, 10, 16)),
              SessionReminder(
                offsetMinutes: 30,
                sendAt: DateTime.utc(2026, 10, 10, 17, 30),
                failedAt: DateTime.utc(2026, 10, 10, 17, 40),
              ),
            ],
          ),
        ],
      );
      await _pumpApp(
        tester,
        location: '/campaigns/c1/sessions/s3',
        role: CampaignRole.dm,
        sessions: fake,
      );

      expect(find.text('24 h antes'), findsOneWidget);
      expect(find.text('Enviado'), findsOneWidget);
      expect(find.text('Pendiente'), findsAtLeastNWidgets(1));
      expect(find.text('Fallido'), findsOneWidget);

      await tester.tap(find.byKey(const Key('session-menu')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('session-notify')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('notice-send')));
      await tester.pumpAndSettle();
      expect(find.text('Indica el asunto.'), findsOneWidget);
      expect(fake.notices, isEmpty);

      await tester.enterText(find.byKey(const Key('notice-subject')), 'Cambio de sitio');
      await tester.enterText(find.byKey(const Key('notice-message')), 'Jugamos en la biblioteca.');
      await tester.tap(find.byKey(const Key('notice-send')));
      await tester.pumpAndSettle();

      expect(fake.notices.single.subject, 'Cambio de sitio');
      expect(fake.notices.single.message, 'Jugamos en la biblioteca.');
      expect(find.text('Aviso enviado a los miembros.'), findsOneWidget);
    });

    testWidgets('el DM cancela y borra la sesión', (tester) async {
      final fake = FakeSessionsRepository(isDm: true, sessions: [makeSession(id: 's3', number: 3)]);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/sessions/s3',
        role: CampaignRole.dm,
        sessions: fake,
      );

      await tester.tap(find.byKey(const Key('session-menu')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('session-cancel')));
      await tester.pumpAndSettle();
      expect(fake.patches.single.patch.status, SessionStatus.cancelled);
      expect(find.text('Cancelada'), findsOneWidget);

      await tester.tap(find.byKey(const Key('session-menu')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('session-reactivate')), findsOneWidget);
      await tester.tap(find.byKey(const Key('session-delete')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm-action')));
      await tester.pumpAndSettle();
      expect(fake.deleted, ['s3']);
    });

    testWidgets('el DM programa una sesión: la hora se envía en UTC según la zona de la campaña', (
      tester,
    ) async {
      final fake = FakeSessionsRepository(isDm: true);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/sessions/new',
        role: CampaignRole.dm,
        sessions: fake,
      );

      await tester.tap(find.byKey(const Key('session-save')));
      await tester.pumpAndSettle();
      expect(find.text('Escribe un título.'), findsOneWidget);
      expect(find.byKey(const Key('session-date-error')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('session-field-title')), 'Gran final');
      await tester.enterText(find.byKey(const Key('session-field-location')), 'Mi casa');
      await tester.enterText(find.byKey(const Key('session-field-duration')), '180');
      await tester.tap(find.byKey(const Key('session-pick-date')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('OK'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('session-pick-time')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('OK'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('session-save')));
      await tester.pumpAndSettle();

      final draft = fake.created.single;
      expect(draft.title, 'Gran final');
      expect(draft.durationMinutes, 180);
      expect(draft.location, 'Mi casa');
      // The time picker proposes 20:00 on the clocks of Europe/Madrid, which is
      // not 20:00 UTC: the instant is sent in UTC.
      final wall = utcToWallClock('Europe/Madrid', draft.startsAt);
      expect((wall.hour, wall.minute), (20, 0));
      expect(draft.startsAt.hour, isNot(20));
      expect(draft.startsAt.isUtc, isTrue);
    });
  });

  group('sesiones: edición', () {
    testWidgets('editar solo envía los campos cambiados y permite vaciar el lugar', (tester) async {
      final fake = FakeSessionsRepository(
        isDm: true,
        sessions: [makeSession(id: 's3', number: 3, title: 'Antes', location: 'Casa de Marta')],
      );
      await _pumpApp(
        tester,
        location: '/campaigns/c1/sessions/s3/edit',
        role: CampaignRole.dm,
        sessions: fake,
      );

      expect(find.text('Antes'), findsOneWidget);
      expect(find.text('Casa de Marta'), findsOneWidget);
      await tester.enterText(find.byKey(const Key('session-field-title')), 'Después');
      await tester.enterText(find.byKey(const Key('session-field-location')), '');
      await tester.tap(find.byKey(const Key('session-save')));
      await tester.pumpAndSettle();

      final patch = fake.patches.single.patch;
      expect(patch.title, 'Después');
      // The date was not touched, so the reminders are not regenerated.
      expect(patch.startsAt, isNull);
      expect(patch.location!.value, isNull);
      expect(patch.notes, isNull);
      expect(patch.toJson(), {'title': 'Después', 'location': null});
    });
  });

  group('diario', () {
    testWidgets('muestra los resúmenes en orden cronológico y el jugador no puede editar', (
      tester,
    ) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        sessions: FakeSessionsRepository(
          // The upcoming session has no summary and must not appear for a player.
          sessions: [_past, _upcoming, _oldest],
        ),
      );
      await openGeneralSection(tester, 'journal');

      expect(find.text('Sesión 1 · La Taberna · 6 sept 2026'), findsOneWidget);
      expect(find.text('Sesión 2 · Cruce de Caminos · 20 sept 2026'), findsOneWidget);
      expect(find.text('Se conocieron en la taberna.'), findsOneWidget);
      expect(find.textContaining('cruzaron'), findsOneWidget);
      expect(find.textContaining('La Torre del Mago'), findsNothing);
      expect(
        tester.getTopLeft(find.byKey(const Key('journal-entry-s1'))).dy,
        lessThan(tester.getTopLeft(find.byKey(const Key('journal-entry-s2'))).dy),
      );
      expect(find.byKey(const Key('journal-edit-s1')), findsNothing);
      expect(find.byKey(const Key('journal-edit-s2')), findsNothing);
      expect(find.text('Sin resumen todavía'), findsNothing);
    });

    testWidgets('un jugador no ve las sesiones canceladas en el diario', (tester) async {
      final cancelled = makeSession(
        id: 's4',
        number: 4,
        title: 'Cancelada con resumen',
        startsAt: DateTime.utc(2026, 9, 27, 18),
        status: SessionStatus.cancelled,
        summary: 'Texto oculto',
      );
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        sessions: FakeSessionsRepository(sessions: [_oldest, cancelled]),
      );
      await openGeneralSection(tester, 'journal');

      expect(find.textContaining('La Taberna'), findsOneWidget);
      expect(find.textContaining('Cancelada con resumen'), findsNothing);
    });

    testWidgets('la búsqueda filtra en local por título y por contenido', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        sessions: FakeSessionsRepository(sessions: [_past, _oldest]),
      );
      await openGeneralSection(tester, 'journal');

      await tester.enterText(find.byKey(const Key('journal-search')), 'puente');
      await tester.pumpAndSettle();
      expect(find.textContaining('Cruce de Caminos'), findsOneWidget);
      expect(find.textContaining('La Taberna'), findsNothing);

      await tester.enterText(find.byKey(const Key('journal-search')), 'zzz');
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('journal-no-results')), findsOneWidget);
    });

    testWidgets('un diario vacío lo explica', (tester) async {
      await _pumpApp(tester, location: '/campaigns/c1');
      await openGeneralSection(tester, 'journal');

      expect(find.byKey(const Key('journal-empty')), findsOneWidget);
    });

    testWidgets('el DM ve "Sin resumen todavía" y el botón de editar', (tester) async {
      final noSummary = makeSession(
        id: 's5',
        number: 5,
        title: 'Sin escribir',
        startsAt: DateTime.utc(2026, 10, 1, 18),
        status: SessionStatus.done,
      );
      await _pumpApp(
        tester,
        location: '/campaigns/c1',
        role: CampaignRole.dm,
        sessions: FakeSessionsRepository(isDm: true, sessions: [_oldest, noSummary, _upcoming]),
      );
      await openGeneralSection(tester, 'journal');

      expect(find.byKey(const Key('journal-empty-s5')), findsOneWidget);
      expect(find.text('Sin resumen todavía'), findsOneWidget);
      expect(find.byKey(const Key('journal-edit-s5')), findsOneWidget);
      expect(find.byKey(const Key('journal-edit-s1')), findsOneWidget);
      // A future session is not part of the journal yet.
      expect(find.textContaining('La Torre del Mago'), findsNothing);
    });

    testWidgets('el DM guarda un resumen y aparece en el diario', (tester) async {
      final noSummary = makeSession(
        id: 's5',
        number: 5,
        title: 'Sin escribir',
        startsAt: DateTime.utc(2026, 10, 1, 18),
        status: SessionStatus.done,
      );
      final fake = FakeSessionsRepository(isDm: true, sessions: [_oldest, noSummary]);
      await _pumpApp(tester, location: '/campaigns/c1', role: CampaignRole.dm, sessions: fake);
      await openGeneralSection(tester, 'journal');

      await _tap(tester, find.byKey(const Key('journal-edit-s5')));
      expect(find.byKey(const Key('summary-field')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('summary-field')), 'Derrotaron al **dragón**.');
      await tester.tap(find.byKey(const Key('summary-tab-preview')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('summary-preview')), findsOneWidget);
      expect(find.textContaining('dragón'), findsOneWidget);

      await tester.tap(find.byKey(const Key('summary-save')));
      await tester.pumpAndSettle();

      expect(fake.summaryCalls.single.id, 's5');
      expect(fake.summaryCalls.single.markdown, 'Derrotaron al **dragón**.');
      // Back in the journal, the entry shows the new summary.
      expect(find.byKey(const Key('journal-summary-s5')), findsOneWidget);
      expect(find.byKey(const Key('journal-empty-s5')), findsNothing);
    });

    testWidgets('un resumen vacío lo borra', (tester) async {
      final fake = FakeSessionsRepository(isDm: true, sessions: [_oldest]);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/sessions/s1/summary',
        role: CampaignRole.dm,
        sessions: fake,
      );

      await tester.enterText(find.byKey(const Key('summary-field')), '   ');
      await tester.tap(find.byKey(const Key('summary-save')));
      await tester.pumpAndSettle();

      expect(fake.summaryCalls.single.markdown, isEmpty);
      expect(fake.sessions.single.summaryMarkdown, isNull);
    });

    testWidgets('el editor de resumen no está disponible para un jugador', (tester) async {
      await _pumpApp(
        tester,
        location: '/campaigns/c1/sessions/s1/summary',
        sessions: FakeSessionsRepository(sessions: [_oldest]),
      );

      expect(find.text('Solo el DM puede editar el resumen.'), findsOneWidget);
      expect(find.byKey(const Key('summary-field')), findsNothing);
    });
  });

  group('ajustes del calendario', () {
    testWidgets('el DM cambia la zona horaria y los recordatorios', (tester) async {
      final campaigns = FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.dm)]);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/general/settings',
        role: CampaignRole.dm,
        campaigns: campaigns,
      );

      expect(find.text('Zona horaria: Europe/Madrid'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('campaign-calendar-settings')));
      expect(find.byKey(const Key('settings-offset-1440')), findsOneWidget);
      expect(find.byKey(const Key('settings-offset-120')), findsOneWidget);

      // Remove the 24 h reminder, add one of 3 hours and one of 45 minutes.
      await tester.tap(
        find.descendant(
          of: find.byKey(const Key('settings-offset-1440')),
          matching: find.byIcon(Icons.clear),
        ),
      );
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('settings-offset-amount')), '3');
      await tester.tap(find.byKey(const Key('settings-offset-add')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('settings-offset-unit')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('minutos').last);
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('settings-offset-amount')), '45');
      await tester.tap(find.byKey(const Key('settings-offset-add')));
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('settings-zone-custom')), 'Asia/Tokyo');
      await tester.tap(find.byKey(const Key('settings-save')));
      await tester.pumpAndSettle();

      final saved = campaigns.campaigns.single;
      expect(saved.timeZoneId, 'Asia/Tokyo');
      expect(saved.reminderOffsetsMinutes, [180, 120, 45]);
      expect(find.text('Zona horaria: Asia/Tokyo'), findsOneWidget);
    });

    testWidgets('una zona desconocida se rechaza antes de enviar', (tester) async {
      final campaigns = FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.dm)]);
      await _pumpApp(
        tester,
        location: '/campaigns/c1/general/settings',
        role: CampaignRole.dm,
        campaigns: campaigns,
      );

      await _tap(tester, find.byKey(const Key('campaign-calendar-settings')));
      await tester.enterText(find.byKey(const Key('settings-zone-custom')), 'Marte/Olympus');
      await tester.tap(find.byKey(const Key('settings-save')));
      await tester.pumpAndSettle();

      expect(find.textContaining('Zona no reconocida'), findsOneWidget);
      expect(campaigns.campaigns.single.timeZoneId, 'Europe/Madrid');
    });

    testWidgets('un jugador ve la zona pero no el botón de ajustes', (tester) async {
      await _pumpApp(tester, location: '/campaigns/c1/general/settings');

      expect(find.byKey(const Key('campaign-calendar-info')), findsOneWidget);
      expect(find.byKey(const Key('campaign-calendar-settings')), findsNothing);
    });
  });

  group('inicio y perfil', () {
    testWidgets('la tarjeta "Próxima sesión" muestra campaña, fecha y mi respuesta', (
      tester,
    ) async {
      await _pumpApp(
        tester,
        location: '/',
        sessions: FakeSessionsRepository(sessions: [_upcoming]),
      );

      expect(find.byKey(const Key('next-session-card')), findsOneWidget);
      expect(find.text('Próxima sesión'), findsOneWidget);
      expect(find.textContaining('La Mina Perdida · Sesión 3 · La Torre del Mago'), findsOneWidget);
      expect(find.text('sáb 10 oct 2026 · 20:00'), findsOneWidget);
      expect(find.text('Tu respuesta: Sí'), findsOneWidget);
    });

    testWidgets('tocar la tarjeta abre la sesión', (tester) async {
      await _pumpApp(
        tester,
        location: '/',
        sessions: FakeSessionsRepository(sessions: [_upcoming]),
      );

      await tester.tap(find.byKey(const Key('next-session-card')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('session-title')), findsOneWidget);
      expect(find.text('La Torre del Mago'), findsOneWidget);
    });

    testWidgets('sin sesiones próximas no hay tarjeta', (tester) async {
      await _pumpApp(
        tester,
        location: '/',
        sessions: FakeSessionsRepository(sessions: [_past]),
      );

      expect(find.byKey(const Key('next-session-card')), findsNothing);
    });

    testWidgets('el menú de usuario activa y desactiva los correos', (tester) async {
      final auth = FakeAuthRepository(storage: FakeTokenStorage(), meUser: makeUser());
      await _pumpApp(tester, location: '/', authRepository: auth);

      await tester.tap(find.byKey(const Key('home-user-menu')));
      await tester.pumpAndSettle();
      expect(
        tester.widget<Switch>(find.byKey(const Key('home-notifications-switch'))).value,
        isTrue,
      );

      await tester.tap(find.byKey(const Key('home-notifications')));
      await tester.pumpAndSettle();
      expect(auth.profileUpdates.single, (displayName: null, notificationsEnabled: false));

      await tester.tap(find.byKey(const Key('home-user-menu')));
      await tester.pumpAndSettle();
      expect(
        tester.widget<Switch>(find.byKey(const Key('home-notifications-switch'))).value,
        isFalse,
      );
    });

    testWidgets('el menú de usuario permite editar el nombre', (tester) async {
      final auth = FakeAuthRepository(storage: FakeTokenStorage(), meUser: makeUser());
      await _pumpApp(tester, location: '/', authRepository: auth);

      await tester.tap(find.byKey(const Key('home-user-menu')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('home-edit-name')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('edit-name-field')), 'Nuevo Nombre');
      await tester.tap(find.byKey(const Key('edit-name-save')));
      await tester.pumpAndSettle();

      expect(auth.profileUpdates.single, (displayName: 'Nuevo Nombre', notificationsEnabled: null));
      expect(find.text('Nuevo Nombre'), findsOneWidget);
    });
  });

  group('modelos y utilidades', () {
    test('Session.fromJson lee el contrato completo', () {
      final s = Session.fromJson({
        'id': 's1',
        'number': 4,
        'campaignId': 'c1',
        'campaignName': 'La Mina Perdida',
        'title': 'Título',
        'startsAt': '2026-10-10T18:00:00+00:00',
        'startsAtLocal': '2026-10-10T20:00:00+02:00',
        'timeZoneId': 'Europe/Madrid',
        'durationMinutes': 120,
        'location': null,
        'notes': 'Nota',
        'summaryMarkdown': null,
        'summaryUpdatedAt': null,
        'status': 'Cancelled',
        'myRsvp': 'Maybe',
        'rsvps': [
          {'userId': 'u1', 'displayName': 'Ana', 'status': 'Yes', 'comment': 'Voy'},
        ],
        'counts': {'yes': 1, 'no': 0, 'maybe': 2, 'pending': 3},
        'reminders': [
          {
            'offsetMinutes': 120,
            'sendAt': '2026-10-10T16:00:00Z',
            'sentAt': null,
            'failedAt': null,
          },
        ],
      });
      expect(s.number, 4);
      expect(s.status, SessionStatus.cancelled);
      expect(s.myRsvp, RsvpStatus.maybe);
      expect(s.rsvps.single.comment, 'Voy');
      expect(s.counts.pending, 3);
      expect(s.reminders!.single.state, ReminderState.pending);
      expect(s.startsAt.isUtc, isTrue);
      expect(s.localStart, DateTime(2026, 10, 10, 20));
      expect(s.hasSummary, isFalse);
      expect(s.commentOf('u1'), 'Voy');
    });

    test('SessionPatch solo envía lo que cambia y permite vaciar campos', () {
      expect(
        SessionPatch(
          title: 'Nuevo',
          startsAt: DateTime.utc(2026, 10, 10, 18),
          location: const Clearable(null),
          durationMinutes: const Clearable(90),
        ).toJson(),
        {
          'title': 'Nuevo',
          'startsAt': '2026-10-10T18:00:00.000Z',
          'durationMinutes': 90,
          'location': null,
        },
      );
      expect(const SessionPatch().isEmpty, isTrue);
      expect(const SessionPatch(status: SessionStatus.done).toJson(), {'status': 'Done'});
    });

    test('zonedToUtc respeta el horario de verano e invierno de Europe/Madrid', () {
      expect(
        zonedToUtc('Europe/Madrid', DateTime(2026, 10, 10, 20)),
        DateTime.utc(2026, 10, 10, 18),
      );
      expect(
        zonedToUtc('Europe/Madrid', DateTime(2026, 12, 12, 20)),
        DateTime.utc(2026, 12, 12, 19),
      );
      expect(zonedToUtc('Marte/Olympus', DateTime(2026, 10, 10, 20)), isNull);
      expect(zoneOffsetLabel('Europe/Madrid', DateTime(2026, 10, 10, 20)), 'UTC+2');
      expect(zoneOffsetLabel('Asia/Kolkata', DateTime(2026, 10, 10, 20)), 'UTC+5:30');
      expect(
        utcToWallClock('Europe/Madrid', DateTime.utc(2026, 10, 10, 18)),
        DateTime(2026, 10, 10, 20),
      );
    });

    test('formatMinutes y formatOffsetBefore', () {
      expect(formatMinutes(45), '45 min');
      expect(formatMinutes(120), '2 h');
      expect(formatMinutes(90), '1 h 30 min');
      expect(formatMinutes(1440), '24 h');
      expect(formatMinutes(4320), '3 días');
      expect(formatOffsetBefore(1440), '24 h antes');
    });

    test(
      'splitSessions separa próximas (primero la más cercana) y pasadas (primero la última)',
      () {
        final a = makeSession(id: 'a', startsAt: DateTime.utc(2026, 10, 20));
        final b = makeSession(id: 'b', startsAt: DateTime.utc(2026, 10, 8));
        final c = makeSession(id: 'c', startsAt: DateTime.utc(2026, 9, 1));
        final d = makeSession(id: 'd', startsAt: DateTime.utc(2026, 9, 15));
        final split = splitSessions([a, c, b, d], sessionsTestNow);
        expect([for (final s in split.upcoming) s.id], ['b', 'a']);
        expect([for (final s in split.past) s.id], ['d', 'c']);
      },
    );

    test(
      'buildJournalEntries ordena por fecha y solo añade al DM las sesiones sin resumen ya pasadas',
      () {
        final summaries = [
          SessionSummary(
            id: 's2',
            number: 2,
            title: 'B',
            startsAt: DateTime.utc(2026, 9, 20),
            startsAtLocal: '2026-09-20T20:00:00+02:00',
            status: SessionStatus.done,
            summaryMarkdown: 'texto',
          ),
        ];
        final missing = makeSession(id: 's1', number: 1, startsAt: DateTime.utc(2026, 9, 6));
        final future = makeSession(id: 's3', number: 3, startsAt: DateTime.utc(2026, 11, 1));
        final cancelled = makeSession(
          id: 's4',
          number: 4,
          startsAt: DateTime.utc(2026, 9, 7),
          status: SessionStatus.cancelled,
        );

        final player = buildJournalEntries(summaries, now: sessionsTestNow);
        expect([for (final e in player) e.id], ['s2']);
        final dm = buildJournalEntries(
          summaries,
          dmSessions: [future, cancelled, missing],
          now: sessionsTestNow,
        );
        expect([for (final e in dm) e.id], ['s1', 's2']);
        expect(dm.first.hasSummary, isFalse);
        expect(filterJournal(dm, 'texto').single.id, 's2');
        expect(filterJournal(dm, 'sesión 1').single.id, 's1');
      },
    );
  });
}
