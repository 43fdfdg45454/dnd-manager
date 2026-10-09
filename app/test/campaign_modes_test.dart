import 'package:dnd_companion/core/router/app_router.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/campaigns/ui/general/campaign_section_page.dart';
import 'package:dnd_companion/features/catalog/data/models.dart' show Condition, ItemSummary;
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/items/data/models.dart';
import 'package:dnd_companion/features/session/data/models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';
import 'helpers/party_fakes.dart';

AppFakes _fakes({
  CampaignRole role = CampaignRole.owner,
  FakePartyRepository? party,
  FakeStashRepository? stash,
  FakeMessagesRepository? messages,
  FakeShopsRepository? shops,
  FakeInventoryRepository? inventory,
}) => AppFakes(
  campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
  party: party,
  stash: stash,
  messages: messages,
  shops: shops,
  inventory: inventory,
  catalog: FakeCatalogRepository(
    conditionList: const [
      Condition(index: 'poisoned', name: 'Envenenado'),
      Condition(index: 'prone', name: 'Derribado'),
    ],
  ),
);

FakePartyRepository _party() => FakePartyRepository(
  members: [
    makePartyMember(
      temporaryHitPoints: 3,
      conditions: const [CharacterCondition(index: 'poisoned')],
      concentratingOnSpellIndex: 'bless',
    ),
    makePartyMember(
      id: 'ch2',
      name: 'Elara',
      ownerUserId: 'p2',
      classIndex: 'wizard',
      hitPointsCurrent: 0,
      hitPointsMax: 14,
      deathSaveFailures: 1,
    ),
  ],
);

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

void main() {
  group('campaignModeRedirect', () {
    test('la campaña sin rol conocido abre la vista Campaña', () {
      expect(campaignModeRedirect(null, '/campaigns/c1'), '/campaigns/c1/general');
      expect(AppRoutes.campaign('c1'), '/campaigns/c1');
      expect(AppRoutes.campaignGeneralView('c1'), '/campaigns/c1/general');
      expect(AppRoutes.campaignCharactersView('c1'), '/campaigns/c1/characters');
      expect(
        AppRoutes.campaignSection('c1', CampaignSection.characters),
        '/campaigns/c1/characters',
      );
    });

    test('la campaña abre en la vista del rol', () {
      expect(campaignModeRedirect(CampaignRole.player, '/campaigns/c1/'), '/campaigns/c1/player');
      expect(campaignModeRedirect(CampaignRole.dm, '/campaigns/c1'), '/campaigns/c1/dm');
      expect(campaignModeRedirect(CampaignRole.owner, '/campaigns/c1'), '/campaigns/c1/dm');
    });

    test('un jugador no entra en la Mesa del DM', () {
      expect(
        campaignModeRedirect(CampaignRole.player, '/campaigns/c1/dm'),
        '/campaigns/c1/player',
      );
      expect(campaignModeRedirect(CampaignRole.player, '/campaigns/c1/player'), isNull);
    });

    test('DM y dueño no entran en Mi sesión', () {
      expect(campaignModeRedirect(CampaignRole.dm, '/campaigns/c1/player'), '/campaigns/c1/dm');
      expect(
        campaignModeRedirect(CampaignRole.owner, '/campaigns/c1/player'),
        '/campaigns/c1/dm',
      );
      expect(campaignModeRedirect(CampaignRole.dm, '/campaigns/c1/dm'), isNull);
      expect(campaignModeRedirect(CampaignRole.owner, '/campaigns/c1/dm'), isNull);
    });

    test('sin rol conocido o fuera de los modos no redirige', () {
      expect(campaignModeRedirect(null, '/campaigns/c1/dm'), isNull);
      expect(campaignModeRedirect(CampaignRole.player, '/campaigns/c1/general'), isNull);
      expect(campaignModeRedirect(CampaignRole.player, '/campaigns/c1/characters'), isNull);
      expect(campaignModeRedirect(CampaignRole.player, '/campaigns/c1/general/lore'), isNull);
      expect(campaignModeRedirect(CampaignRole.player, '/campaigns/c1/lore/l1'), isNull);
      expect(campaignModeRedirect(CampaignRole.player, '/characters/ch1'), isNull);
      expect(campaignIdOf('/campaigns/c9/dm'), 'c9');
      expect(campaignIdOf('/compendium'), isNull);
    });
  });

  group('shell de campaña', () {
    testWidgets('un jugador es redirigido fuera de /dm y ve Mi sesión', (tester) async {
      final router = await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(role: CampaignRole.player),
      );

      expect(locationOf(router), '/campaigns/c1/player');
      expect(find.byKey(const Key('nav-general')), findsOneWidget);
      expect(find.byKey(const Key('nav-characters')), findsOneWidget);
      expect(find.byKey(const Key('nav-player')), findsOneWidget);
      expect(find.byKey(const Key('nav-dm')), findsNothing);
      expect(find.byKey(const Key('dm-session')), findsNothing);
    });

    testWidgets('un DM es redirigido fuera de /player y ve la Mesa del DM', (tester) async {
      final router = await pumpRealApp(
        tester,
        location: '/campaigns/c1/player',
        fakes: _fakes(role: CampaignRole.dm),
      );

      expect(locationOf(router), '/campaigns/c1/dm');
      expect(find.byKey(const Key('nav-dm')), findsOneWidget);
      expect(find.byKey(const Key('nav-player')), findsNothing);
      expect(find.byKey(const Key('dm-session')), findsOneWidget);
    });

    testWidgets('la barra muestra el nombre, el hueco de tiempo real y las secciones', (
      tester,
    ) async {
      await pumpRealApp(tester, location: '/campaigns/c1/general', fakes: _fakes());

      expect(find.byKey(const Key('campaign-title')), findsOneWidget);
      expect(find.byKey(const Key('realtime-status')), findsOneWidget);
      for (final section in CampaignSection.values) {
        if (section == CampaignSection.characters) continue;
        expect(find.byKey(Key('general-${section.path}')), findsOneWidget);
      }
      expect(find.byKey(const Key('general-characters')), findsNothing);
    });

    testWidgets('desde el inicio se abre la campaña en la vista del rol y se cambia de pestaña', (
      tester,
    ) async {
      final router = await pumpRealApp(tester, location: '/', fakes: _fakes());

      expect(find.byKey(const Key('app-nav-bar')), findsOneWidget);
      await _tap(tester, find.text('La Mina Perdida'));
      expect(locationOf(router), '/campaigns/c1/dm');
      expect(find.byKey(const Key('dm-session')), findsOneWidget);
      // The campaign is full screen: the app bar stays behind.
      expect(find.byKey(const Key('app-nav-bar')), findsNothing);

      await _tap(tester, find.byKey(const Key('nav-characters')));
      expect(locationOf(router), '/campaigns/c1/characters');
      expect(find.byKey(const Key('campaign-characters')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('nav-general')));
      expect(locationOf(router), '/campaigns/c1/general');
      expect(find.byKey(const Key('campaign-general')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('nav-dm')));
      expect(find.byKey(const Key('dm-session')), findsOneWidget);
    });

    testWidgets('una tarjeta de Campaña abre su sección como página completa', (tester) async {
      final router = await pumpRealApp(
        tester,
        location: '/campaigns/c1/general',
        fakes: _fakes(),
      );

      await openGeneralSection(tester, 'members');
      expect(locationOf(router), '/campaigns/c1/general/members');
      expect(find.text('Usuario Demo (tú)'), findsOneWidget);
      expect(find.byKey(const Key('nav-general')), findsNothing);

      await tester.pageBack();
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('campaign-general')), findsOneWidget);
    });

    testWidgets('la barra de la app cambia de pestaña y conserva la de campañas', (tester) async {
      final router = await pumpRealApp(tester, location: '/', fakes: _fakes());

      await _tap(tester, find.byKey(const Key('nav-compendium')));
      expect(locationOf(router), AppRoutes.compendium);
      await _tap(tester, find.byKey(const Key('nav-dice')));
      expect(locationOf(router), AppRoutes.dice);
      await _tap(tester, find.byKey(const Key('nav-profile')));
      expect(locationOf(router), AppRoutes.profile);
      expect(find.byKey(const Key('profile-list')), findsOneWidget);
      await _tap(tester, find.byKey(const Key('nav-campaigns')));
      expect(locationOf(router), AppRoutes.home);
      expect(find.text('La Mina Perdida'), findsOneWidget);
    });
  });

  group('Mesa del DM', () {
    testWidgets('el grupo muestra PG, temporales, condiciones, concentración y muerte', (
      tester,
    ) async {
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(party: _party()),
      );

      expect(find.text('PG 20 / 28 (+3 temp.)'), findsOneWidget);
      expect(find.byKey(const Key('party-temp-bar-ch1')), findsOneWidget);
      expect(find.byKey(const Key('party-condition-ch1-poisoned')), findsOneWidget);
      expect(find.text('Envenenado'), findsOneWidget);
      expect(find.byKey(const Key('party-concentration-ch1')), findsOneWidget);
      expect(find.byKey(const Key('party-death-ch2')), findsOneWidget);
      expect(find.byKey(const Key('party-death-ch1')), findsNothing);
      expect(find.text('CA 17 · Inic. +2 · Perc. 11'), findsNWidgets(2));
    });

    testWidgets('el descanso largo llama a party/rest para todos', (tester) async {
      final party = _party();
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(party: party),
      );

      await _tap(tester, find.byKey(const Key('dm-long-rest')));
      await _tap(tester, find.byKey(const Key('confirm-action')));

      expect(party.rests.single.kind, PartyRestKind.long);
      expect(party.rests.single.characterIds, isNull);
      expect(find.text('PG 28 / 28'), findsOneWidget);
      expect(find.text('Descanso largo aplicado a todo el grupo.'), findsOneWidget);
    });

    testWidgets('el descanso corto se aplica solo a los seleccionados', (tester) async {
      final party = _party();
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(party: party),
      );

      await _tap(tester, find.byKey(const Key('party-select-ch2')));
      expect(find.text('Descanso corto (1)'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('dm-short-rest')));
      await _tap(tester, find.byKey(const Key('confirm-action')));

      expect(party.rests.single.kind, PartyRestKind.short);
      expect(party.rests.single.characterIds, ['ch2']);
      expect(find.text('Descanso corto (1)'), findsNothing);
    });

    testWidgets('el daño rápido llama a party/adjust y actualiza la fila', (tester) async {
      final party = _party();
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(party: party),
      );

      await _tap(tester, find.byKey(const Key('party-member-ch1')));
      expect(find.byKey(const Key('dm-character-sheet')), findsOneWidget);
      await tester.enterText(find.byKey(const Key('dm-amount')), '5');
      await tester.pump();
      await _tap(tester, find.byKey(const Key('dm-damage')));

      final adjustment = party.adjustments.single.single;
      expect(adjustment.characterId, 'ch1');
      expect(adjustment.hitPointsDelta, -5);
      expect(adjustment.toJson(), {'characterId': 'ch1', 'hitPointsDelta': -5});
      // 3 temporary hit points absorb part of the damage.
      expect(find.byKey(const Key('dm-sheet-hp')), findsOneWidget);
      expect(find.text('PG 18 / 28'), findsWidgets);
    });

    testWidgets('el daño a un concentrado pregunta la salvación y "No" termina la concentración', (
      tester,
    ) async {
      final party = _party()
        ..nextDamage = [
          const DamageOutcome(
            characterId: 'ch1',
            damage: 20,
            hitPointsCurrent: 3,
            concentratingOn: 'bless',
            concentrationCheckDc: 10,
          ),
        ];
      final characters = FakeCharactersRepository(
        characters: [makeCharacterJson(status: 'Active', concentratingOnSpellIndex: 'bless')],
      );
      final fakes = AppFakes(
        campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.dm)]),
        party: party,
        characters: characters,
      );
      await pumpRealApp(tester, location: '/campaigns/c1/dm', fakes: fakes);

      await _tap(tester, find.byKey(const Key('party-member-ch1')));
      await tester.enterText(find.byKey(const Key('dm-amount')), '20');
      await tester.pump();
      await _tap(tester, find.byKey(const Key('dm-damage')));

      expect(find.text('Concentración de Thorin'), findsOneWidget);
      expect(
        find.textContaining('¿Superaste la salvación de Constitución (CD 10)?'),
        findsOneWidget,
      );
      await _tap(tester, find.byKey(const Key('concentration-save-no')));
      expect(characters.concentrationCalls, [null]);
      expect(find.text('Thorin: Pierdes la concentración en Bless.'), findsOneWidget);
    });

    testWidgets('si la concentración termina sola el DM solo recibe el aviso', (tester) async {
      final party = _party()
        ..nextDamage = [
          const DamageOutcome(
            characterId: 'ch1',
            damage: 40,
            concentratingOn: 'bless',
            concentrationEnded: true,
          ),
        ];
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(party: party),
      );
      await _tap(tester, find.byKey(const Key('party-member-ch1')));
      await tester.enterText(find.byKey(const Key('dm-amount')), '40');
      await tester.pump();
      await _tap(tester, find.byKey(const Key('dm-damage')));

      expect(find.byKey(const Key('concentration-save-dialog')), findsNothing);
      expect(find.text('Thorin: Pierdes la concentración en Bless.'), findsOneWidget);
    });

    testWidgets('las condiciones se añaden y se quitan con party/adjust', (tester) async {
      final party = _party();
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(party: party),
      );

      await _tap(tester, find.byKey(const Key('party-member-ch1')));
      await _tap(tester, find.byKey(const Key('dm-condition-add')));
      await _tap(tester, find.byKey(const Key('pick-condition-prone')));
      expect(party.adjustments.last.single.addConditions!.single.index, 'prone');
      expect(find.byKey(const Key('dm-condition-prone')), findsOneWidget);

      final chip = find.byKey(const Key('dm-condition-poisoned'));
      await _tap(tester, find.descendant(of: chip, matching: find.byIcon(Icons.clear)));
      expect(party.adjustments.last.single.removeConditions, ['poisoned']);
      expect(find.byKey(const Key('dm-condition-poisoned')), findsNothing);
    });

    testWidgets('los PG máximos se sobrescriben desde la ficha rápida', (tester) async {
      final party = _party();
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(party: party),
      );

      await _tap(tester, find.byKey(const Key('party-member-ch1')));
      await _tap(tester, find.byKey(const Key('dm-hp-max')));
      await tester.enterText(find.byKey(const Key('number-field')), '30');
      await _tap(tester, find.byKey(const Key('number-confirm')));

      expect(party.adjustments.single.single.hitPointsMax, 30);
    });

    testWidgets('el mensaje secreto se envía a los personajes elegidos', (tester) async {
      final messages = FakeMessagesRepository();
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(party: _party(), messages: messages),
      );

      await _tap(tester, find.byKey(const Key('party-select-ch2')));
      await _tap(tester, find.byKey(const Key('dm-message')));
      expect(
        tester.widget<CheckboxListTile>(find.byKey(const Key('message-target-ch2'))).value,
        isTrue,
      );
      await tester.enterText(find.byKey(const Key('message-body')), 'Notas un frío extraño.');
      await tester.pump();
      await _tap(tester, find.byKey(const Key('message-send')));

      expect(messages.sent.single.characterIds, ['ch2']);
      expect(messages.sent.single.body, 'Notas un frío extraño.');
      expect(find.text('Mensaje enviado.'), findsOneWidget);
    });

    testWidgets('el botín: oro, reparto y dar un objeto a un personaje', (tester) async {
      final inventory = FakeInventoryRepository();
      final stash = FakeStashRepository(
        inventory: inventory,
        copperPieces: 1000,
        items: [makeStashItem(quantity: 2)],
      );
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(party: _party(), stash: stash, inventory: inventory),
      );

      expect(find.text('1 pp'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('stash-gold-add')));
      await tester.enterText(find.byKey(const Key('stash-gold-amount')), '5');
      await _tap(tester, find.byKey(const Key('stash-gold-confirm')));
      expect(stash.goldChanges, [500]);
      expect(find.text('1 pp 5 gp'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('stash-menu-st1')));
      await _tap(tester, find.text('Dar a…'));
      await _tap(tester, find.byKey(const Key('pick-character-ch2')));
      await _tap(tester, find.byKey(const Key('pick-characters-confirm')));
      await _tap(tester, find.byKey(const Key('quantity-minus')));
      await _tap(tester, find.byKey(const Key('quantity-confirm')));
      expect(stash.taken.single, (itemId: 'st1', characterId: 'ch2', quantity: 1));
      expect(inventory.items['ch2']!.single.effective.name, 'Longsword');
      expect(find.byKey(const Key('stash-qty-st1')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('stash-gold-split')));
      await _tap(tester, find.byKey(const Key('pick-character-ch2')));
      await _tap(tester, find.byKey(const Key('pick-characters-confirm')));
      expect(stash.splits.single, ['ch1']);
      expect(find.text('0 cp'), findsOneWidget);
    });

    testWidgets('el DM añade botín del catálogo y permite tomar a los jugadores', (tester) async {
      final stash = FakeStashRepository();
      final fakes = AppFakes(
        campaigns: FakeCampaignsRepository(campaigns: [makeCampaign()]),
        stash: stash,
        campaignItems: FakeCampaignItemsRepository(
          srd: [const ItemSummary(id: 't-rope', name: 'Rope', category: 'AdventuringGear')],
        ),
      );
      await pumpRealApp(tester, location: '/campaigns/c1/dm', fakes: fakes);

      expect(find.byKey(const Key('stash-empty')), findsOneWidget);
      await _tap(tester, find.byKey(const Key('stash-add')));
      await _tap(tester, find.text('Rope'));
      await tester.enterText(find.byKey(const Key('stash-add-quantity')), '3');
      await tester.pump();
      await _tap(tester, find.byKey(const Key('stash-add-submit')));
      expect(stash.added.single.templateId, 't-rope');
      expect(stash.added.single.quantity, 3);
      expect(find.byKey(const Key('stash-item-st1')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('stash-players-can-take')));
      expect(fakes.campaigns.campaigns.single.playersCanTakeFromStash, isTrue);
    });

    testWidgets('el conmutador de una tienda la abre o la cierra', (tester) async {
      final inventory = FakeInventoryRepository();
      final shops = FakeShopsRepository(
        inventory: inventory,
        isDm: true,
        shops: [const Shop(id: 's1', name: 'Armería', isOpen: false, buybackPercent: 50)],
      );
      await pumpRealApp(
        tester,
        location: '/campaigns/c1/dm',
        fakes: _fakes(shops: shops, inventory: inventory),
      );

      expect(find.text('Cerrada'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('dm-shop-s1')));
      expect(shops.toggles.single, (shopId: 's1', isOpen: true));
      expect(find.text('Abierta'), findsOneWidget);
    });
  });
}
