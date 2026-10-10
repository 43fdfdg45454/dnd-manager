import 'package:opentrpg/core/realtime/realtime_events.dart';
import 'package:opentrpg/systems/dnd5e/dnd5e_events.dart';
import 'package:opentrpg/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg/features/characters/data/models.dart';
import 'package:opentrpg/features/characters/ui/combat/rest_section.dart'
    show expectedShortRestHealing;
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'helpers/app_pump.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fake_realtime_hub.dart';
import 'helpers/fakes.dart';
import 'helpers/party_fakes.dart';

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _tapKey(WidgetTester tester, String key) => _tap(tester, find.byKey(Key(key)));

/// "Mi sesión" of `u1` (Player), owner of the Active character `ch1`.
Future<
  ({AppFakes fakes, FakeCharactersRepository characters, FakeRealtimeHub hub, GoRouter router})
>
_pumpPlayer(
  WidgetTester tester, {
  Map<String, dynamic>? character,
  String location = '/campaigns/c1/player',
}) async {
  final characters = FakeCharactersRepository(
    characters: [character ?? makeCharacterJson(status: 'Active', combat: makeCombatJson())],
  );
  final fakes = AppFakes(
    campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
    characters: characters,
  );
  final hub = FakeRealtimeHub();
  final router = await pumpRealApp(tester, location: location, fakes: fakes, realtime: hub);
  return (fakes: fakes, characters: characters, hub: hub, router: router);
}

/// "Mesa del DM" of the Owner with the party and the rest requests.
Future<AppFakes> _pumpDm(
  WidgetTester tester, {
  FakePartyRepository? party,
  FakeRestRequestsRepository? restRequests,
}) async {
  final fakes = AppFakes(
    campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.owner)]),
    party:
        party ??
        FakePartyRepository(
          members: [
            makePartyMember(),
            makePartyMember(id: 'ch2', name: 'Elara', ownerUserId: 'p2', classIndex: 'wizard'),
          ],
        ),
    restRequests: restRequests,
  );
  await pumpRealApp(tester, location: '/campaigns/c1/dm', fakes: fakes);
  return fakes;
}

void main() {
  group('CharacterDetail y PartyMember', () {
    test('leen pendingRest y pendingLevelUpTo', () {
      final character = CharacterDetail.fromJson(
        makeCharacterJson(
          status: 'Active',
          pendingRest: {
            'id': 'rr1',
            'kind': 'Short',
            'hitDice': {'fighter': 2},
            'requestedAt': '2026-10-01T20:00:00Z',
          },
          pendingLevelUpTo: 4,
        ),
      );
      expect(character.pendingLevelUpTo, 4);
      expect(character.pendingRest!.id, 'rr1');
      expect(character.pendingRest!.kind, RestKind.short);
      expect(character.pendingRest!.hitDice, {'fighter': 2});
      expect(character.pendingRest!.description, 'descanso corto (2 dados)');

      final none = CharacterDetail.fromJson(makeCharacterJson(status: 'Active'));
      expect(none.pendingRest, isNull);
      expect(none.pendingLevelUpTo, isNull);
    });

    test('la curación esperada suma los dados y el modificador de Constitución', () {
      final character = CharacterDetail.fromJson(makeCharacterJson(status: 'Active'));
      // Constitución +2: 2 dados → +4.
      expect(expectedShortRestHealing(character, {'fighter': 2}), '2d10 + 4');
      expect(expectedShortRestHealing(character, {}), isNull);
    });
  });

  group('Mi sesión · descansos', () {
    testWidgets('pide un descanso corto con 2 dados y espera al DM', (tester) async {
      final (fakes: _, :characters, hub: _, router: _) = await _pumpPlayer(tester);
      await _tapKey(tester, 'player-subview-detail');

      expect(find.byKey(const Key('rest-short')), findsNothing);
      await _tapKey(tester, 'rest-request-short');
      expect(find.text('Fighter (d10)\nQuedan 3 de 3'), findsOneWidget);
      expect(
        find.text('Sin dados de golpe: solo se recargarán los recursos de descanso corto.'),
        findsOneWidget,
      );
      await _tapKey(tester, 'hit-dice-fighter-plus');
      await _tapKey(tester, 'hit-dice-fighter-plus');
      expect(find.text('Curación esperada: 2d10 + 4'), findsOneWidget);
      await _tapKey(tester, 'rest-request-short-confirm');

      expect(characters.restRequests, hasLength(1));
      expect(characters.restRequests.single.kind, RestKind.short);
      expect(characters.restRequests.single.hitDice, {'fighter': 2});
      expect(find.text('Petición de descanso corto enviada al DM.'), findsOneWidget);
      expect(find.byKey(const Key('rest-pending')), findsOneWidget);
      expect(find.text('Esperando al DM · descanso corto (2 dados)'), findsOneWidget);
      expect(find.byKey(const Key('rest-request-short')), findsNothing);

      await _tapKey(tester, 'rest-cancel');
      expect(characters.restCancellations, 1);
      expect(find.byKey(const Key('rest-pending')), findsNothing);
      expect(find.byKey(const Key('rest-request-short')), findsOneWidget);
      expect(find.text('Descanso aprobado'), findsNothing);
    });

    testWidgets('pide un descanso largo con confirmación', (tester) async {
      final (fakes: _, :characters, hub: _, router: _) = await _pumpPlayer(tester);
      await _tapKey(tester, 'player-subview-detail');

      await _tapKey(tester, 'rest-request-long');
      expect(characters.restRequests, isEmpty);
      await _tapKey(tester, 'confirm-action');

      expect(characters.restRequests, hasLength(1));
      expect(characters.restRequests.single.kind, RestKind.long);
      expect(characters.restRequests.single.hitDice, isEmpty);
      expect(find.text('Esperando al DM · descanso largo'), findsOneWidget);
    });

    testWidgets('al aprobarse el descanso avisa con "Descanso aprobado"', (tester) async {
      final (fakes: _, :characters, :hub, router: _) = await _pumpPlayer(tester);
      await _tapKey(tester, 'player-subview-detail');
      await _tapKey(tester, 'rest-request-long');
      await _tapKey(tester, 'confirm-action');
      expect(find.byKey(const Key('rest-pending')), findsOneWidget);

      characters.approvePendingRest('ch1', hitPointsCurrent: 28);
      hub.emit(const RestRequestUpdated(campaignId: 'c1', characterId: 'ch1', entityId: 'rr1'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('rest-pending')), findsNothing);
      expect(find.text('Descanso aprobado'), findsOneWidget);
      expect(find.byKey(const Key('rest-request-long')), findsOneWidget);
    });

    testWidgets('con una petición pendiente al abrir la hoja se muestra la tarjeta', (
      tester,
    ) async {
      await _pumpPlayer(
        tester,
        character: makeCharacterJson(
          status: 'Active',
          pendingRest: {
            'id': 'rr9',
            'kind': 'Short',
            'hitDice': {'fighter': 1},
            'requestedAt': '2026-10-01T20:00:00Z',
          },
        ),
      );
      await _tapKey(tester, 'player-subview-detail');
      expect(find.text('Esperando al DM · descanso corto (1 dado)'), findsOneWidget);
    });
  });

  group('Mi sesión · nivel concedido', () {
    testWidgets('con nivel pendiente el asistente se abre solo y no se puede cerrar', (
      tester,
    ) async {
      final (fakes: _, characters: _, hub: _, :router) = await _pumpPlayer(
        tester,
        character: makeCharacterJson(status: 'Active', pendingLevelUpTo: 4),
      );
      // The card stays below the forced wizard.
      expect(find.byKey(const Key('level-up-card'), skipOffstage: false), findsOneWidget);
      expect(find.text('¡Puedes subir a nivel 4!', skipOffstage: false), findsOneWidget);
      expect(locationOf(router), '/characters/ch1/level-up');
    });

    testWidgets('sin nivel pendiente no hay tarjeta', (tester) async {
      await _pumpPlayer(tester);
      expect(find.byKey(const Key('level-up-card')), findsNothing);
    });

    testWidgets('levelUp.granted avisa al dueño y muestra la tarjeta', (tester) async {
      final (fakes: _, :characters, :hub, router: _) = await _pumpPlayer(tester);
      expect(find.byKey(const Key('level-up-card')), findsNothing);

      characters.setPendingLevelUp('ch1', 4);
      hub.emit(const LevelUpGranted(campaignId: 'c1', characterId: 'ch1'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('realtime-notice-level-up')), findsOneWidget);
      expect(
        find.descendant(
          of: find.byKey(const Key('realtime-notice-level-up')),
          matching: find.text('¡Puedes subir a nivel 4!'),
        ),
        findsOneWidget,
      );
      expect(find.byKey(const Key('level-up-card'), skipOffstage: false), findsOneWidget);
    });

    testWidgets('levelUp.granted de otro personaje no avisa', (tester) async {
      final (fakes: _, :characters, :hub, router: _) = await _pumpPlayer(tester);
      characters.setPendingLevelUp('ch1', 4);
      hub.emit(const LevelUpGranted(campaignId: 'c1', characterId: 'ch2'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('realtime-notice-level-up')), findsNothing);
    });
  });

  group('membership.removed', () {
    testWidgets('saca al jugador de la campaña con un aviso', (tester) async {
      final (fakes: _, characters: _, :hub, router: _) = await _pumpPlayer(tester);
      expect(find.byKey(const Key('campaign-title')), findsOneWidget);

      hub.emit(const MembershipRemoved(campaignId: 'c1'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('campaign-title')), findsNothing);
      expect(find.text('Ya no formas parte de esta campaña'), findsOneWidget);
    });
  });

  group('Mesa del DM · peticiones', () {
    testWidgets('lista los descansos pedidos con nombre, tipo, dados y antigüedad', (tester) async {
      final restRequests = FakeRestRequestsRepository(
        requests: [
          makeRestRequest(requestedAt: DateTime.now().subtract(const Duration(minutes: 5))),
          makeRestRequest(
            id: 'rr2',
            characterId: 'ch2',
            characterName: 'Elara',
            kind: RestKind.long,
            hitDice: const {},
            requestedAt: DateTime.now().subtract(const Duration(minutes: 20)),
          ),
        ],
      );
      await _pumpDm(tester, restRequests: restRequests);

      expect(find.byKey(const Key('dm-petitions')), findsOneWidget);
      expect(find.text('descanso corto (2 dados) · hace 5 min'), findsOneWidget);
      expect(find.text('descanso largo · hace 20 min'), findsOneWidget);
      expect(
        find.descendant(
          of: find.byKey(const Key('rest-petition-rr1')),
          matching: find.text('Thorin'),
        ),
        findsOneWidget,
      );
      // La entrada de solicitudes de cambio sigue en la misma tarjeta.
      expect(find.byKey(const Key('dm-change-requests')), findsOneWidget);
    });

    testWidgets('sin peticiones lo dice', (tester) async {
      await _pumpDm(tester);
      expect(find.byKey(const Key('dm-petitions-empty')), findsOneWidget);
    });

    testWidgets('Aprobar llama al repositorio y la fila desaparece', (tester) async {
      final restRequests = FakeRestRequestsRepository(requests: [makeRestRequest()]);
      await _pumpDm(tester, restRequests: restRequests);

      await _tapKey(tester, 'rest-approve-rr1');

      expect(restRequests.approved, ['rr1']);
      expect(find.byKey(const Key('rest-petition-rr1')), findsNothing);
      expect(find.text('Descanso aprobado para Thorin.'), findsOneWidget);
    });

    testWidgets('Rechazar llama al repositorio', (tester) async {
      final restRequests = FakeRestRequestsRepository(requests: [makeRestRequest()]);
      await _pumpDm(tester, restRequests: restRequests);

      await _tapKey(tester, 'rest-reject-rr1');

      expect(restRequests.rejected, [(id: 'rr1', comment: null)]);
      expect(find.byKey(const Key('rest-petition-rr1')), findsNothing);
    });

    testWidgets('restRequest.updated recarga las peticiones', (tester) async {
      final restRequests = FakeRestRequestsRepository();
      final fakes = AppFakes(
        campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.owner)]),
        party: FakePartyRepository(members: [makePartyMember()]),
        restRequests: restRequests,
      );
      final hub = FakeRealtimeHub();
      await pumpRealApp(tester, location: '/campaigns/c1/dm', fakes: fakes, realtime: hub);
      expect(find.byKey(const Key('rest-petition-rr1')), findsNothing);

      restRequests.requests.add(makeRestRequest());
      hub.emit(const RestRequestUpdated(campaignId: 'c1', characterId: 'ch1', entityId: 'rr1'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('rest-petition-rr1')), findsOneWidget);
    });
  });

  group('Mesa del DM · conceder nivel', () {
    testWidgets('a la selección, con confirmación', (tester) async {
      final party = FakePartyRepository(
        members: [
          makePartyMember(),
          makePartyMember(id: 'ch2', name: 'Elara', ownerUserId: 'p2', classIndex: 'wizard'),
        ],
      );
      await _pumpDm(tester, party: party);

      await _tapKey(tester, 'party-select-ch1');
      expect(find.text('Conceder nivel (1)'), findsOneWidget);
      await _tapKey(tester, 'party-grant-level');
      expect(find.textContaining('¿Conceder el siguiente nivel a Thorin?'), findsOneWidget);
      expect(party.grants, isEmpty);
      await _tapKey(tester, 'confirm-action');

      expect(party.grants, [
        ['ch1'],
      ]);
      expect(find.byKey(const Key('party-levelup-ch1')), findsOneWidget);
      expect(find.text('↑ 4'), findsOneWidget);
      expect(find.byKey(const Key('party-levelup-ch2')), findsNothing);
    });

    testWidgets('sin selección va a todo el grupo y se puede cancelar', (tester) async {
      final party = FakePartyRepository(
        members: [
          makePartyMember(),
          makePartyMember(id: 'ch2', name: 'Elara', ownerUserId: 'p2', classIndex: 'wizard'),
        ],
      );
      await _pumpDm(tester, party: party);

      await _tapKey(tester, 'party-grant-level');
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
      expect(party.grants, isEmpty);

      await _tapKey(tester, 'party-grant-level');
      expect(find.textContaining('¿Conceder el siguiente nivel a todo el grupo?'), findsOneWidget);
      await _tapKey(tester, 'confirm-action');

      expect(party.grants, [null]);
      expect(find.byKey(const Key('party-levelup-ch1')), findsOneWidget);
      expect(find.byKey(const Key('party-levelup-ch2')), findsOneWidget);
    });

    testWidgets('la lista marca nivel pendiente ("↑ N") y descanso pedido ("Zz")', (tester) async {
      final party = FakePartyRepository(
        members: [
          makePartyMember(
            pendingLevelUpTo: 4,
            pendingRest: const PendingRest(id: 'rr1', kind: RestKind.long),
          ),
          makePartyMember(id: 'ch2', name: 'Elara', ownerUserId: 'p2'),
        ],
      );
      await _pumpDm(tester, party: party);

      expect(find.text('↑ 4'), findsOneWidget);
      expect(find.text('Zz'), findsOneWidget);
      expect(find.byKey(const Key('party-levelup-ch2')), findsNothing);
      expect(find.byKey(const Key('party-rest-ch2')), findsNothing);
    });
  });
}
