import 'package:opentrpg/features/campaigns/data/campaigns_controller.dart';
import 'package:opentrpg/features/campaigns/data/campaigns_repository.dart';
import 'package:opentrpg/features/sessions/data/models.dart';
import 'package:opentrpg/features/sessions/data/sessions_controllers.dart';
import 'package:opentrpg/features/sessions/ui/next_session_card.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fakes.dart';
import '../../helpers/session_fakes.dart';

/// Local clock of these tests.
final _now = DateTime.utc(2026, 10, 10, 15);

/// A session repository whose server answer is stale (it still lists sessions
/// that are over by [_now]) and that counts the requests of `mySessions`.
class _StaleSessionsRepository extends FakeSessionsRepository {
  _StaleSessionsRepository({super.sessions}) : super(now: DateTime.utc(2026, 1, 1));

  int mySessionsCalls = 0;

  @override
  Future<List<Session>> mySessions({DateTime? from, DateTime? to}) {
    mySessionsCalls++;
    return super.mySessions(from: from, to: to);
  }
}

// Ended an hour before [_now]: 10:00 + 240 assumed minutes = 14:00.
final _ended = makeSession(
  id: 's1',
  number: 1,
  title: 'Terminada',
  startsAt: DateTime.utc(2026, 10, 10, 10),
);
final _tomorrow = makeSession(
  id: 's2',
  number: 2,
  title: 'Mañana',
  startsAt: DateTime.utc(2026, 10, 11, 18),
);

Future<void> _pump(
  WidgetTester tester, {
  required List<Session> sessions,
  List<String> campaignIds = const ['c1'],
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        sessionsOverride(_StaleSessionsRepository(sessions: sessions)),
        sessionsClockProvider.overrideWithValue(() => _now),
        campaignsRepositoryProvider.overrideWithValue(
          FakeCampaignsRepository(campaigns: [for (final id in campaignIds) makeCampaign(id: id)]),
        ),
      ],
      child: const MaterialApp(home: Scaffold(body: NextSessionCard())),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('salta la sesión que ya terminó y muestra la siguiente', (tester) async {
    await _pump(tester, sessions: [_ended, _tomorrow]);

    expect(find.byKey(const Key('next-session-card')), findsOneWidget);
    expect(find.textContaining('Mañana'), findsOneWidget);
    expect(find.textContaining('Terminada'), findsNothing);
  });

  testWidgets('con una sola sesión terminada no hay tarjeta', (tester) async {
    await _pump(tester, sessions: [_ended]);

    expect(find.byKey(const Key('next-session-card')), findsNothing);
  });

  testWidgets('una sesión de una campaña que ya no está en la lista no se muestra', (tester) async {
    await _pump(
      tester,
      sessions: [
        makeSession(id: 's9', campaignId: 'gone', startsAt: DateTime.utc(2026, 10, 11, 18)),
      ],
    );

    expect(find.byKey(const Key('next-session-card')), findsNothing);
  });

  test('borrar o abandonar una campaña invalida las próximas sesiones', () async {
    final sessions = _StaleSessionsRepository(sessions: [_tomorrow]);
    final container = ProviderContainer(
      overrides: [
        sessionsOverride(sessions),
        campaignsRepositoryProvider.overrideWithValue(
          FakeCampaignsRepository(
            campaigns: [
              makeCampaign(),
              makeCampaign(id: 'c2'),
            ],
          ),
        ),
      ],
    );
    addTearDown(container.dispose);
    container.listen(mySessionsControllerProvider, (_, _) {});
    await container.read(mySessionsControllerProvider.future);
    expect(sessions.mySessionsCalls, 1);

    final deleted = campaignDetailControllerProvider('c1');
    container.listen(deleted, (_, _) {});
    await container.read(deleted.future);
    await container.read(deleted.notifier).delete();
    await container.read(mySessionsControllerProvider.future);
    expect(sessions.mySessionsCalls, 2);

    final left = campaignDetailControllerProvider('c2');
    container.listen(left, (_, _) {});
    await container.read(left.future);
    await container.read(left.notifier).leave();
    await container.read(mySessionsControllerProvider.future);
    expect(sessions.mySessionsCalls, 3);
  });
}
