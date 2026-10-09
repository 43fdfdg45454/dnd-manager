import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/party_fakes.dart';

/// Phase 20: a DM has no characters of their own, only NPCs.

const _promoteConflict =
    'Beto tiene personajes en la campaña (Elara). Reasígnalos o conviértelos en PNJ antes de '
    'nombrarlo DM.';

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

FakeCharactersRepository _characters() => FakeCharactersRepository(
  isDm: true,
  characters: [
    makeCharacterJson(id: 'ch1', name: 'Thorin', ownerUserId: null, status: 'Active'),
    makeCharacterJson(
      id: 'ch2',
      name: 'Elara',
      ownerUserId: 'p2',
      ownerDisplayName: 'Beto',
      status: 'Active',
    ),
  ],
);

FakePartyRepository _party() => FakePartyRepository(
  members: [
    makePartyMember(id: 'ch1', name: 'Thorin', ownerUserId: null),
    makePartyMember(id: 'ch2', name: 'Elara', ownerUserId: 'p2'),
  ],
);

void main() {
  group('Mesa del DM: cambiar jugador', () {
    testWidgets('el DM entrega un PNJ a un jugador desde la ficha rápida', (tester) async {
      final characters = _characters();
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: AppFakes(
          campaigns: FakeCampaignsRepository(campaigns: [makeCampaign()]),
          characters: characters,
          party: _party(),
        ),
      );

      await _tap(tester, find.byKey(const Key('party-member-ch1')));
      expect(tester.widget<Text>(find.byKey(const Key('dm-sheet-owner'))).data, 'PNJ');
      await _tap(tester, find.byKey(const Key('character-owner')));

      expect(find.byKey(const Key('character-owner-dialog')), findsOneWidget);
      // Only players are offered: neither the DM (u1, Owner) nor "Yo".
      expect(find.byKey(const Key('character-owner-u1')), findsNothing);
      expect(find.text('Yo'), findsNothing);
      // Already an NPC: converting it again is not offered.
      expect(
        tester.widget<ListTile>(find.byKey(const Key('character-owner-npc'))).enabled,
        isFalse,
      );

      await _tap(tester, find.byKey(const Key('character-owner-p2')));

      expect(characters.ownerChanges.single, (id: 'ch1', ownerUserId: 'p2'));
      expect(find.text('Thorin es ahora de Beto.'), findsOneWidget);
    });

    testWidgets('el DM convierte en PNJ el personaje de un jugador', (tester) async {
      final characters = _characters();
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: AppFakes(
          campaigns: FakeCampaignsRepository(campaigns: [makeCampaign()]),
          characters: characters,
          party: _party(),
        ),
      );

      await _tap(tester, find.byKey(const Key('party-member-ch2')));
      await _tap(tester, find.byKey(const Key('character-owner')));
      expect(tester.widget<ListTile>(find.byKey(const Key('character-owner-p2'))).enabled, isFalse);
      await _tap(tester, find.byKey(const Key('character-owner-npc')));

      expect(characters.ownerChanges.single, (id: 'ch2', ownerUserId: null));
      expect(find.text('Elara es ahora un PNJ.'), findsOneWidget);
    });

    testWidgets('la hoja completa ofrece "Cambiar jugador" al DM', (tester) async {
      final characters = _characters();
      await pumpRealApp(
        tester,
        location: '/characters/ch2',
        fakes: AppFakes(
          campaigns: FakeCampaignsRepository(campaigns: [makeCampaign()]),
          characters: characters,
          catalog: FakeCatalogRepository(),
        ),
      );

      await _tap(tester, find.byKey(const Key('character-menu')));
      await _tap(tester, find.byKey(const Key('character-menu-owner')));
      await _tap(tester, find.byKey(const Key('character-owner-npc')));

      expect(characters.ownerChanges.single, (id: 'ch2', ownerUserId: null));
    });

    testWidgets('un jugador no tiene "Cambiar jugador" en su hoja', (tester) async {
      await pumpRealApp(
        tester,
        location: '/characters/ch1',
        fakes: AppFakes(
          campaigns: FakeCampaignsRepository(
            campaigns: [makeCampaign(myRole: CampaignRole.player)],
          ),
          characters: FakeCharactersRepository(characters: [makeCharacterJson(status: 'Active')]),
        ),
      );

      expect(find.byKey(const Key('character-menu')), findsNothing);
    });
  });

  group('lista de personajes del DM', () {
    testWidgets('las tarjetas sin dueño llevan la insignia PNJ', (tester) async {
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/characters',
        fakes: AppFakes(
          campaigns: FakeCampaignsRepository(campaigns: [makeCampaign()]),
          characters: _characters(),
        ),
      );

      expect(find.byKey(const Key('character-npc-ch1')), findsOneWidget);
      expect(find.byKey(const Key('character-npc-ch2')), findsNothing);
      expect(find.text('Jugador: Beto'), findsOneWidget);
    });
  });

  group('miembros', () {
    testWidgets('el 409 al ascender a DM a un jugador con personajes se ve en línea', (
      tester,
    ) async {
      final campaigns = FakeCampaignsRepository(campaigns: [makeCampaign()]);
      await pumpRealApp(
        tester,
        location: '/campaigns/c1',
        fakes: AppFakes(campaigns: campaigns),
      );
      await openGeneralSection(tester, 'members');
      campaigns.error = dioError(409, data: {'status': 409, 'detail': _promoteConflict});

      await _tap(tester, find.byKey(const Key('member-menu-p2')));
      await _tap(tester, find.text('Cambiar rol'));
      await _tap(tester, find.byKey(const Key('member-role-DM')));

      expect(find.byKey(const Key('member-role-error')), findsOneWidget);
      expect(find.text(_promoteConflict), findsOneWidget);
      expect(find.text('Rol de Beto'), findsOneWidget);
      expect(find.text('Rol actualizado.'), findsNothing);
      expect(
        campaigns.campaigns.single.members.firstWhere((m) => m.userId == 'p2').role,
        CampaignRole.player,
      );
    });

    testWidgets('el 409 al transferir la propiedad se ve en línea', (tester) async {
      final campaigns = FakeCampaignsRepository(campaigns: [makeCampaign()]);
      await pumpRealApp(
        tester,
        location: '/campaigns/c1',
        fakes: AppFakes(campaigns: campaigns),
      );
      await openGeneralSection(tester, 'settings');
      campaigns.error = dioError(409, data: {'status': 409, 'detail': _promoteConflict});

      await _tap(tester, find.byKey(const Key('campaign-transfer')));
      await _tap(tester, find.byKey(const Key('transfer-submit')));

      expect(find.byKey(const Key('transfer-error')), findsOneWidget);
      expect(find.text(_promoteConflict), findsOneWidget);
      expect(find.byKey(const Key('transfer-submit')), findsOneWidget);
      expect(campaigns.campaigns.single.ownerId, 'u1');
    });
  });
}
