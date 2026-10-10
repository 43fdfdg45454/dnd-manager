import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/items/data/models.dart';
import 'package:dnd_companion/features/items/domain/combat_usable.dart';
import 'package:dnd_companion/features/catalog/data/models.dart' show ItemArmor, ItemModifier;
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/app_pump.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';
import 'helpers/item_fakes.dart';
import 'helpers/party_fakes.dart';

final _sword = makeCharacterItem(id: 'sword', equipped: true);
final _potion = makeCharacterItem(
  id: 'potion',
  templateId: 't-potion',
  quantity: 2,
  effective: makeEffective(name: 'Potion of Healing', category: 'Consumable', damageDice: null),
);
final _rope = makeCharacterItem(
  id: 'rope',
  templateId: 't-rope',
  effective: makeEffective(name: 'Rope', category: 'AdventuringGear', damageDice: null),
);
final _wand = makeCharacterItem(
  id: 'wand',
  templateId: 't-wand',
  charges: 7,
  effective: makeEffective(name: 'Wand of Magic Missiles', category: 'MagicItem', damageDice: null),
);

/// "Mi sesión" of `u1` (Player), owner of the Active character `ch1` (Thorin);
/// `ch2` (Elara) belongs to another player.
Future<AppFakes> _pump(
  WidgetTester tester, {
  List<Map<String, dynamic>>? characters,
  FakeInventoryRepository? inventory,
  FakeStashRepository? stash,
  FakeMessagesRepository? messages,
  bool playersCanTake = false,
}) async {
  final items =
      inventory ??
      FakeInventoryRepository(
        items: {
          'ch1': [_sword, _potion, _rope, _wand],
        },
      );
  final fakes = AppFakes(
    campaigns: FakeCampaignsRepository(
      campaigns: [
        makeCampaign(myRole: CampaignRole.player, playersCanTakeFromStash: playersCanTake),
      ],
    ),
    characters: FakeCharactersRepository(
      characters:
          characters ??
          [
            makeCharacterJson(status: 'Active', combat: makeCombatJson()),
            makeCharacterJson(id: 'ch2', name: 'Elara', ownerUserId: 'p2', status: 'Active'),
          ],
    ),
    inventory: items,
    stash: stash ?? FakeStashRepository(inventory: items, playersCanTake: playersCanTake),
    messages: messages,
  );
  await pumpRealApp(tester, location: '/campaigns/c1/player', fakes: fakes);
  return fakes;
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

/// "Detalle" opens on its "Sesión" sub-tab by default.
Future<void> _detail(WidgetTester tester) =>
    _tap(tester, find.byKey(const Key('player-subview-detail')));

void main() {
  group('isCombatUsable', () {
    test('armas, escudos, armaduras, consumibles, cargas y bonos de ataque', () {
      expect(isCombatUsable(makeEffective()), isTrue);
      expect(isCombatUsable(makeEffective(category: 'Shield', damageDice: null)), isTrue);
      expect(
        isCombatUsable(
          const EffectiveItem(name: 'Chain Mail', category: 'Armor', armor: ItemArmor(baseAc: 16)),
        ),
        isTrue,
      );
      expect(isCombatUsable(makeEffective(category: 'Consumable', damageDice: null)), isTrue);
      expect(isCombatUsable(makeEffective(category: 'AdventuringGear', damageDice: null)), isFalse);
      expect(
        isCombatUsable(makeEffective(category: 'MagicItem', damageDice: null), hasCharges: true),
        isTrue,
      );
      expect(
        isCombatUsable(
          makeEffective(
            category: 'MagicItem',
            damageDice: null,
            modifiers: const [ItemModifier(kind: 'AttackBonus', value: 1)],
          ),
        ),
        isTrue,
      );
      expect(isCombatUsableItem(_wand), isTrue);
      expect(isCombatUsableItem(_rope), isFalse);
    });
  });

  group('Mi sesión', () {
    testWidgets('combate: hoja de combate sin descansos y objetos filtrados', (tester) async {
      final fakes = await _pump(tester);

      expect(find.byKey(const Key('player-subview')), findsOneWidget);
      expect(find.byKey(const Key('player-combat')), findsOneWidget);
      expect(find.byKey(const Key('player-character-name')), findsOneWidget);
      expect(find.byKey(const Key('hp-bar')), findsOneWidget);
      expect(find.byKey(const Key('rest-short')), findsNothing);

      expect(find.byKey(const Key('combat-item-sword')), findsOneWidget);
      expect(find.byKey(const Key('combat-item-potion')), findsOneWidget);
      expect(find.byKey(const Key('combat-item-wand')), findsOneWidget);
      expect(find.byKey(const Key('combat-item-rope')), findsNothing);
      expect(find.byKey(const Key('combat-item-use-sword')), findsNothing);

      await _tap(tester, find.byKey(const Key('combat-item-use-potion')));
      expect(fakes.inventory.used, ['potion']);
      expect(find.text('Usado: Potion of Healing.'), findsOneWidget);
    });

    testWidgets('detalle: Sesión con descansos, tiendas, botín y mensajes y las seis subpestañas', (
      tester,
    ) async {
      await _pump(tester);
      expect(find.text('Fuera de combate'), findsNothing);
      await _detail(tester);

      expect(find.byKey(const Key('player-character-name')), findsOneWidget);
      final tabs = tester.widget<TabBar>(find.byKey(const Key('character-detail-tabs')));
      expect(
        [for (final t in tabs.tabs) (t as Tab).text],
        ['Sesión', 'Resumen', 'Habilidades', 'Rasgos', 'Hechizos', 'Inventario', 'Notas'],
      );
      expect(tabs.controller!.index, 0);
      expect(find.byKey(const Key('player-session')), findsOneWidget);
      expect(find.byKey(const Key('rest-request-short')), findsOneWidget);
      expect(find.byKey(const Key('rest-request-long')), findsOneWidget);
      expect(find.byKey(const Key('rest-short')), findsNothing);
      expect(find.byKey(const Key('player-shops')), findsOneWidget);
      expect(find.byKey(const Key('stash-card')), findsOneWidget);
      expect(find.byKey(const Key('messages-inbox')), findsOneWidget);
      expect(find.byKey(const Key('hp-bar')), findsNothing);
      expect(find.byKey(const Key('player-open-sheet')), findsNothing);
      expect(find.byKey(const Key('player-open-inventory')), findsNothing);

      await openDetailTab(tester, 'tab-inventory');
      expect(find.byKey(const Key('inventory-money')), findsOneWidget);
      expect(find.byKey(const Key('inv-item-rope')), findsOneWidget);

      // Al volver a Combate y a Detalle sigue en Inventario.
      await _tap(tester, find.byKey(const Key('player-subview-combat')));
      expect(find.byKey(const Key('player-combat')), findsOneWidget);
      await _detail(tester);
      expect(find.byKey(const Key('inventory-money')), findsOneWidget);

      await openDetailTab(tester, 'tab-skills');
      expect(find.text('Atletismo'), findsOneWidget);
    });

    testWidgets('nunca enlaza la hoja de otro jugador', (tester) async {
      await _pump(tester);

      expect(find.text('Elara'), findsNothing);
      expect(find.byKey(const Key('player-character-select')), findsNothing);
    });

    testWidgets('con varios personajes activos se elige cuál jugar', (tester) async {
      await _pump(
        tester,
        characters: [
          makeCharacterJson(status: 'Active', combat: makeCombatJson()),
          makeCharacterJson(id: 'ch3', name: 'Brom', status: 'Active', combat: makeCombatJson()),
          makeCharacterJson(id: 'ch2', name: 'Elara', ownerUserId: 'p2', status: 'Active'),
        ],
      );

      expect(find.byKey(const Key('player-character-select')), findsOneWidget);
      await _tap(tester, find.byKey(const Key('player-character-select')));
      expect(find.text('Elara'), findsNothing);
      await _tap(tester, find.text('Brom').last);
      expect(tester.widget<Text>(find.byKey(const Key('player-character-name'))).data, 'Brom');
    });

    testWidgets('sin personaje activo avisa y ofrece crear uno con el asistente', (tester) async {
      final fakes = await _pump(
        tester,
        characters: [
          makeCharacterJson(id: 'ch2', name: 'Elara', ownerUserId: 'p2', status: 'Active'),
        ],
      );

      expect(find.text('No tienes personaje en esta campaña'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('player-create-character')));

      // Opens the creation wizard, not the quick name dialog.
      expect(find.byKey(const Key('wizard-step-title')), findsOneWidget);
      expect(find.byKey(const Key('character-name')), findsNothing);
      expect(fakes.characters.created, isEmpty);
    });

    testWidgets('la bandeja marca leído al abrir un mensaje', (tester) async {
      final messages = FakeMessagesRepository(
        messages: [
          makeMessage(),
          makeMessage(id: 'm2', body: 'Mensaje antiguo', read: true),
        ],
      );
      await _pump(tester, messages: messages);

      final navBadge = tester.widget<Badge>(find.byKey(const Key('nav-player-badge')));
      expect(navBadge.isLabelVisible, isTrue);

      await _detail(tester);
      final badge = find.byKey(const Key('messages-unread-badge'));
      expect(tester.widget<Badge>(badge).isLabelVisible, isTrue);
      expect(find.descendant(of: badge, matching: find.text('1')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('message-m1')));
      expect(find.byKey(const Key('message-body-view')), findsOneWidget);
      expect(messages.marked, ['m1']);
      await _tap(tester, find.byKey(const Key('message-close')));

      expect(tester.widget<Badge>(badge).isLabelVisible, isFalse);
      expect(
        tester.widget<Badge>(find.byKey(const Key('nav-player-badge'))).isLabelVisible,
        isFalse,
      );

      await _tap(tester, find.byKey(const Key('message-m2')));
      expect(messages.marked, ['m1']);
    });

    testWidgets('tomar del botín solo si la campaña lo permite', (tester) async {
      final inventory = FakeInventoryRepository(
        items: {
          'ch1': [_sword],
        },
      );
      final stash = FakeStashRepository(
        inventory: inventory,
        items: [makeStashItem(name: 'Gem', category: 'Other', quantity: 3)],
        playersCanTake: true,
      );
      await _pump(tester, inventory: inventory, stash: stash, playersCanTake: true);
      await _detail(tester);

      await _tap(tester, find.byKey(const Key('stash-take-st1')));
      await _tap(tester, find.byKey(const Key('quantity-plus')));
      await _tap(tester, find.byKey(const Key('quantity-confirm')));

      expect(stash.taken.single, (itemId: 'st1', characterId: 'ch1', quantity: 2));
      expect(find.text('×1'), findsOneWidget);
    });

    testWidgets('sin permiso no hay botón Tomar ni Devolver al grupo', (tester) async {
      await _pump(tester, stash: FakeStashRepository(items: [makeStashItem()]));
      await _detail(tester);

      expect(find.byKey(const Key('stash-item-st1')), findsOneWidget);
      expect(find.byKey(const Key('stash-take-st1')), findsNothing);

      await openDetailTab(tester, 'tab-inventory');
      await _tap(tester, find.byKey(const Key('inv-menu-rope')));
      expect(find.text('Devolver al grupo'), findsNothing);
    });

    testWidgets('desde el inventario se devuelve un objeto al grupo', (tester) async {
      final inventory = FakeInventoryRepository(
        items: {
          'ch1': [_rope],
        },
      );
      final stash = FakeStashRepository(inventory: inventory, playersCanTake: true);
      await _pump(tester, inventory: inventory, stash: stash, playersCanTake: true);
      await _detail(tester);
      await openDetailTab(tester, 'tab-inventory');

      await _tap(tester, find.byKey(const Key('inv-menu-rope')));
      await _tap(tester, find.text('Devolver al grupo'));
      await _tap(tester, find.byKey(const Key('quantity-confirm')));

      expect(stash.returned.single, (characterId: 'ch1', characterItemId: 'rope', quantity: 1));
      expect(find.text('Devuelto al botín del grupo.'), findsOneWidget);
      expect(find.byKey(const Key('inv-item-rope')), findsNothing);
    });
  });
}
