import 'package:opentrpg/core/network/connectivity.dart';
import 'package:opentrpg/core/realtime/realtime_events.dart';
import 'package:opentrpg/core/realtime/realtime_hub.dart';
import 'package:opentrpg/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg/features/campaigns/ui/campaign_shell.dart';
import 'package:opentrpg/features/characters/data/models.dart';
import 'package:opentrpg/features/session/data/models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'helpers/app_pump.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fake_realtime_hub.dart';
import 'helpers/fakes.dart';
import 'helpers/party_fakes.dart';

/// Counts the sheet reads, to observe the invalidations.
class _CountingCharacters extends FakeCharactersRepository {
  _CountingCharacters({super.characters, super.isDm});

  final List<String> reads = [];

  @override
  Future<CharacterDetail> get(String id) {
    reads.add(id);
    return super.get(id);
  }
}

/// Counts the stash reads.
class _CountingStash extends FakeStashRepository {
  int reads = 0;

  @override
  Future<PartyStash> get(String campaignId) {
    reads++;
    return super.get(campaignId);
  }
}

/// Connectivity that starts without network.
class _OfflineConnectivity extends ConnectivityController {
  @override
  ConnectivityStatus build() => const ConnectivityStatus(hasNetwork: false);
}

/// Connectivity with network whose last request failed.
class _FailedRequestConnectivity extends ConnectivityController {
  @override
  ConnectivityStatus build() => const ConnectivityStatus(lastRequestFailed: true);
}

DirectMessage _message(String id) =>
    DirectMessage(id: id, campaignId: 'c1', characterId: 'ch1', body: 'Cuidado con el posadero');

CharacterUpdated _characterUpdated({String campaignId = 'c1', String? characterId = 'ch1'}) =>
    CharacterUpdated(campaignId: campaignId, characterId: characterId);

typedef _Pumped = ({
  FakeRealtimeHub hub,
  _CountingCharacters characters,
  _CountingStash stash,
  GoRouter router,
});

Future<_Pumped> _pump(
  WidgetTester tester, {
  CampaignRole role = CampaignRole.player,
  String location = '/campaigns/c1/player',
  FakeRealtimeHub? hub,
  FakeMessagesRepository? messages,
  bool offline = false,
  bool lastRequestFailed = false,
}) async {
  final realtime = hub ?? FakeRealtimeHub();
  final characters = _CountingCharacters(
    characters: [makeCharacterJson(status: 'Active', combat: makeCombatJson())],
    isDm: role.isAtLeastDm,
  );
  final stash = _CountingStash();
  final fakes = AppFakes(
    campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
    characters: characters,
    stash: stash,
    messages: messages,
  );
  final router = await pumpRealApp(
    tester,
    location: location,
    fakes: fakes,
    realtime: realtime,
    overrides: [
      if (offline) connectivityProvider.overrideWith(_OfflineConnectivity.new),
      if (lastRequestFailed) connectivityProvider.overrideWith(_FailedRequestConnectivity.new),
    ],
  );
  return (hub: realtime, characters: characters, stash: stash, router: router);
}

void main() {
  group('CampaignEvent.fromJson', () {
    test('reconoce cada tipo y sus campos', () {
      final event = CampaignEvent.fromJson({
        'type': 'character.updated',
        'campaignId': 'c1',
        'characterId': 'ch1',
        'entityId': null,
        'at': '2026-10-06T18:30:00+00:00',
      });
      expect(event, isA<CharacterUpdated>());
      expect(event.campaignId, 'c1');
      expect(event.characterId, 'ch1');
      expect(event.entityId, isNull);
      expect(event.at, DateTime.utc(2026, 10, 6, 18, 30));

      const types = {
        'message.received': MessageReceived,
        'party.rest': PartyRest,
        'party.stash.updated': PartyStashUpdated,
        'shop.updated': ShopUpdated,
        'changeRequest.updated': ChangeRequestUpdated,
        'session.updated': SessionUpdated,
        'restRequest.updated': RestRequestUpdated,
        'levelUp.granted': LevelUpGranted,
        'membership.removed': MembershipRemoved,
      };
      for (final MapEntry(key: type, value: expected) in types.entries) {
        final parsed = CampaignEvent.fromJson({'type': type, 'campaignId': 'c1'});
        expect(parsed.runtimeType, expected, reason: type);
      }
    });

    test('acepta claves en PascalCase y deja los tipos desconocidos como Unknown', () {
      final event = CampaignEvent.fromJson({
        'Type': 'shop.updated',
        'CampaignId': 'C1',
        'EntityId': 's1',
      });
      expect(event, isA<ShopUpdated>());
      expect(event.entityId, 's1');
      expect(event.isFor('c1'), isTrue);

      final unknown = CampaignEvent.fromJson({'type': 'dragon.arrived', 'campaignId': 'c1'});
      expect(unknown, isA<Unknown>());
      expect((unknown as Unknown).rawType, 'dragon.arrived');
      expect(unknown.at, isNull);
    });
  });

  group('conexión del shell', () {
    testWidgets('conecta al montar el shell con su campaña y desconecta al salir', (tester) async {
      final (:hub, :router, characters: _, stash: _) = await _pump(
        tester,
        location: '/campaigns/c1',
      );

      expect(hub.connects, ['c1']);
      expect(hub.disconnects, 0);
      expect(find.byKey(const Key('realtime-connected')), findsOneWidget);

      router.go('/');
      await tester.pumpAndSettle();

      expect(find.byType(CampaignShell), findsNothing);
      expect(hub.disconnects, 1);
      expect(hub.campaignId, isNull);
    });

    testWidgets('sin red no intenta conectar y lo indica; conecta al volver la red', (
      tester,
    ) async {
      final (:hub, characters: _, stash: _, router: _) = await _pump(tester, offline: true);

      expect(hub.connects, isEmpty);
      expect(find.byKey(const Key('realtime-offline')), findsOneWidget);
      expect(find.byTooltip('Tiempo real: sin red'), findsOneWidget);

      final container = ProviderScope.containerOf(tester.element(find.byType(CampaignShell)));
      container.read(connectivityProvider.notifier).reportRequestSucceeded();
      await tester.pumpAndSettle();

      expect(hub.connects, ['c1']);
      expect(find.byKey(const Key('realtime-connected')), findsOneWidget);
    });

    testWidgets('el icono refleja la reconexión y al volver recarga los datos', (tester) async {
      final (:hub, :characters, stash: _, router: _) = await _pump(tester);
      final reads = characters.reads.length;

      hub.setStatus(RealtimeStatus.reconnecting);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('realtime-reconnecting')), findsOneWidget);
      expect(find.byTooltip('Tiempo real: reconectando…'), findsOneWidget);

      hub.setStatus(RealtimeStatus.connected);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('realtime-connected')), findsOneWidget);
      // Events may have been lost meanwhile: the sheet is read again.
      expect(characters.reads.length, greaterThan(reads));
    });

    testWidgets('una petición HTTP fallida no hace mentir al icono: manda el hub', (tester) async {
      final (:hub, characters: _, stash: _, router: _) = await _pump(
        tester,
        lastRequestFailed: true,
      );

      expect(hub.connects, ['c1']);
      expect(find.byKey(const Key('realtime-connected')), findsOneWidget);
      expect(find.byTooltip('En vivo'), findsOneWidget);
      expect(find.byKey(const Key('connection-banner-reconnecting')), findsNothing);
      expect(find.byKey(const Key('connection-banner-offline')), findsNothing);
    });

    testWidgets('si el hub no conecta, la franja ámbar cuenta atrás y Reintentar conecta ya', (
      tester,
    ) async {
      final failing = FakeRealtimeHub(failConnect: true);
      await _pump(tester, hub: failing);

      expect(failing.connects, ['c1']);
      expect(find.byKey(const Key('connection-banner-reconnecting')), findsOneWidget);
      expect(find.textContaining('Reconectando… ('), findsOneWidget);

      failing.failConnect = false;
      await tester.tap(find.byKey(const Key('realtime-retry')));
      await tester.pumpAndSettle();

      // No waiting for the 2 s timer: the manual retry connected at once.
      expect(failing.connects, ['c1', 'c1']);
      expect(find.byKey(const Key('realtime-connected')), findsOneWidget);
      expect(find.byKey(const Key('connection-banner-reconnecting')), findsNothing);
    });

    testWidgets('sin red la franja es roja y Reintentar no hace nada hasta que vuelve', (
      tester,
    ) async {
      final (:hub, characters: _, stash: _, router: _) = await _pump(tester, offline: true);

      expect(find.byKey(const Key('connection-banner-offline')), findsOneWidget);
      await tester.tap(find.byKey(const Key('realtime-retry')));
      await tester.pumpAndSettle();
      expect(hub.connects, isEmpty);
    });

    testWidgets('si la conexión falla lo reintenta pasado un rato', (tester) async {
      final failing = FakeRealtimeHub(failConnect: true);
      await _pump(tester, hub: failing);

      expect(failing.connects, ['c1']);
      expect(find.byKey(const Key('realtime-offline')), findsOneWidget);

      failing.failConnect = false;
      await tester.pump(const Duration(seconds: 2));
      await tester.pumpAndSettle();

      expect(failing.connects, ['c1', 'c1']);
      expect(find.byKey(const Key('realtime-connected')), findsOneWidget);
    });
  });

  group('eventos', () {
    testWidgets('character.updated recarga la hoja del personaje', (tester) async {
      final (:hub, :characters, stash: _, router: _) = await _pump(tester);
      final reads = characters.reads.where((id) => id == 'ch1').length;
      expect(reads, greaterThan(0));

      hub.emit(_characterUpdated());
      await tester.pumpAndSettle();

      expect(characters.reads.where((id) => id == 'ch1').length, reads + 1);
    });

    testWidgets('los eventos de otra campaña se ignoran', (tester) async {
      final (:hub, :characters, stash: _, router: _) = await _pump(tester);
      final reads = characters.reads.length;

      hub.emit(_characterUpdated(campaignId: 'c2'));
      await tester.pumpAndSettle();

      expect(characters.reads.length, reads);
    });

    testWidgets('message.received avisa al jugador y actualiza el contador de no leídos', (
      tester,
    ) async {
      final messages = FakeMessagesRepository();
      final (:hub, characters: _, stash: _, router: _) = await _pump(tester, messages: messages);
      Badge badge() => tester.widget<Badge>(find.byKey(const Key('nav-player-badge')));
      expect(badge().isLabelVisible, isFalse);

      messages.messages.add(_message('m1'));
      hub.emitJson({'type': 'message.received', 'campaignId': 'c1', 'entityId': 'm1'});
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('realtime-notice-message')), findsOneWidget);
      expect(find.text('Mensaje del DM'), findsOneWidget);
      expect(badge().isLabelVisible, isTrue);
    });

    testWidgets('party.rest avisa al jugador y recarga la hoja', (tester) async {
      final (:hub, :characters, stash: _, router: _) = await _pump(tester);
      final reads = characters.reads.length;

      hub.emit(const PartyRest(campaignId: 'c1'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('realtime-notice-rest')), findsOneWidget);
      expect(find.text('El DM ha declarado un descanso'), findsOneWidget);
      expect(characters.reads.length, greaterThan(reads));
    });

    testWidgets('changeRequest.resolved avisa al solicitante y lleva a la hoja', (tester) async {
      final (:hub, :characters, stash: _, :router) = await _pump(tester);
      characters.requests.add(
        makeChangeRequest(
          id: 'cr9',
          requestedByUserId: 'u1',
          status: 'Rejected',
          type: 'EditSheet',
          comment: 'Demasiado',
        ),
      );

      hub.emitJson({
        'type': 'changeRequest.resolved',
        'campaignId': 'c1',
        'characterId': 'ch1',
        'entityId': 'cr9',
      });
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('realtime-notice-request-resolved')), findsOneWidget);
      expect(find.text('El DM rechazó tu solicitud (edición de hoja) de Thorin: Demasiado'), findsOneWidget);

      await tester.tap(find.text('Ver'));
      await tester.pumpAndSettle();
      expect(router.state.uri.path, '/characters/ch1');
    });

    testWidgets('el DM no recibe avisos de sus propias acciones', (tester) async {
      final (:hub, characters: _, stash: _, router: _) = await _pump(
        tester,
        role: CampaignRole.dm,
        location: '/campaigns/c1/dm',
      );

      hub.emit(const PartyRest(campaignId: 'c1'));
      hub.emit(const MessageReceived(campaignId: 'c1'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('realtime-notice-rest')), findsNothing);
      expect(find.byKey(const Key('realtime-notice-message')), findsNothing);
    });

    testWidgets('party.stash.updated recarga el alijo del grupo', (tester) async {
      final (:hub, :stash, characters: _, router: _) = await _pump(
        tester,
        role: CampaignRole.dm,
        location: '/campaigns/c1/dm',
      );
      final reads = stash.reads;

      hub.emit(const PartyStashUpdated(campaignId: 'c1'));
      await tester.pumpAndSettle();

      expect(stash.reads, greaterThan(reads));
    });
  });
}
