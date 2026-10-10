import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/characters/models.dart' as core;
import 'package:opentrpg_core/core/realtime/realtime_events.dart';
import 'package:opentrpg_core/core/systems/game_system_ui.dart';
import 'package:opentrpg_core/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg_core/core/systems/system_registry.dart';
import 'package:opentrpg_core/core/systems/unsupported_system_ui.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';
import 'package:opentrpg_dnd5e/characters/models.dart';
import 'package:opentrpg_dnd5e/dnd5e_events.dart';
import 'package:opentrpg_dnd5e/dnd5e_ui.dart';

import 'helpers/app_pump.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

/// Every die shows [face].
class _Always implements Random {
  _Always(this.face);

  final int face;

  @override
  int nextInt(int max) => (face - 1).clamp(0, max - 1);

  @override
  double nextDouble() => 0;

  @override
  bool nextBool() => false;
}

void main() {
  group('partición de modelos de personaje', () {
    test('CharacterDetail.fromJson conserva el JSON en raw', () {
      final json = makeCharacterJson(name: 'Brenna', pendingLevelUpTo: 4);
      final detail = core.CharacterDetail.fromJson(json);
      expect(detail.name, 'Brenna');
      expect(detail.copperPieces, 1550);
      expect(detail.raw, json);
      expect(detail.raw['sheet'], json['sheet']);
    });

    test('Dnd5eCharacter.fromDetail reproduce la hoja y se cachea', () {
      final detail = core.CharacterDetail.fromJson(makeCharacterJson(pendingLevelUpTo: 4));
      final dnd5e = Dnd5eCharacter.fromDetail(detail);
      expect(identical(dnd5e, Dnd5eCharacter.fromDetail(detail)), isTrue);
      expect(dnd5e.raceName, 'Human');
      expect(dnd5e.totalLevel, 3);
      expect(dnd5e.hitPointsCurrent, 20);
      expect(dnd5e.inspiration, isTrue);
      expect(dnd5e.pendingLevelUpTo, 4);
      expect(dnd5e.sheet.armorClass, 17);
      expect(dnd5e.sheet.hitPointsMax, 28);
      // The extension getters read the same values.
      expect(detail.sheet.armorClass, 17);
      expect(detail.classes.single.classIndex, 'fighter');
    });

    test('la línea del listado sale del resumen', () {
      final summary = core.CharacterSummary.fromJson({
        'id': 'ch1',
        'campaignId': 'c1',
        'name': 'Thorin',
        'status': 'Active',
        'raceName': 'Dwarf',
        'classes': [
          {'classIndex': 'fighter', 'className': 'Fighter', 'level': 3},
        ],
      });
      expect(const Dnd5eUi().rosterSubtitle(summary), 'Dwarf · Fighter 3 · Nivel 3');
    });
  });

  group('registro de sistemas', () {
    test('resuelve el sistema registrado o UnsupportedSystemUi', () {
      final container = ProviderContainer(
        overrides: [
          gameSystemsProvider.overrideWithValue(const [Dnd5eUi()]),
        ],
      );
      addTearDown(container.dispose);
      expect(container.read(gameSystemUiProvider('dnd5e')), isA<Dnd5eUi>());
      final other = container.read(gameSystemUiProvider('pathfinder2e'));
      expect(other, isA<UnsupportedSystemUi>());
      expect((other as UnsupportedSystemUi).notice, 'Esta app no incluye el sistema pathfinder2e.');
      expect(container.read(defaultGameSystemUiProvider), isA<Dnd5eUi>());
    });

    test('sin sistemas registrados el sistema por defecto no está soportado', () {
      final container = ProviderContainer();
      addTearDown(container.dispose);
      expect(container.read(defaultGameSystemUiProvider), isA<UnsupportedSystemUi>());
    });

    testWidgets('UnsupportedSystemUi muestra el aviso en la ficha', (tester) async {
      const system = UnsupportedSystemUi('pathfinder2e');
      final character = core.CharacterDetail.fromJson(makeCharacterJson());
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Builder(
              builder: (context) => system.combatView(context, character, canEdit: true),
            ),
          ),
        ),
      );
      expect(find.byKey(const Key('unsupported-system')), findsOneWidget);
      expect(find.text('Esta app no incluye el sistema pathfinder2e.'), findsOneWidget);
      expect(system.detailTabs(character, canEdit: true), hasLength(1));
      expect(system.routes(GlobalKey<NavigatorState>()), isEmpty);
    });
    testWidgets('la ficha de una campaña de otro sistema muestra el aviso', (tester) async {
      await pumpRealApp(
        tester,
        location: '/characters/ch1',
        fakes: AppFakes(
          campaigns: FakeCampaignsRepository(
            campaigns: [makeCampaign(myRole: CampaignRole.player, systemId: 'otro')],
          ),
          characters: FakeCharactersRepository(
            characters: [makeCharacterJson(status: 'Active', ownerUserId: 'u1')],
          ),
        ),
      );
      expect(find.byKey(const Key('unsupported-system')), findsWidgets);
      expect(find.text('Esta app no incluye el sistema otro.'), findsWidgets);
      expect(find.text('Thorin'), findsWidgets);
    });
  });

  group('Dnd5eUi', () {
    const ui = Dnd5eUi();

    test('clasifica las tiradas naturales', () {
      DiceResult roll(int value) => DiceExpression.parse('1d20').roll(_Always(value));
      expect(ui.classifyRoll(roll(20)), RollClass.critical);
      expect(ui.classifyRoll(roll(1)), RollClass.fumble);
      expect(ui.classifyRoll(roll(10)), RollClass.normal);
    });

    test('las pestañas de detalle conservan ids y etiquetas', () {
      final character = core.CharacterDetail.fromJson(makeCharacterJson());
      final tabs = ui.detailTabs(character, canEdit: true);
      expect([for (final t in tabs) t.id], ['summary', 'skills', 'traits', 'spells']);
      expect([for (final t in tabs) t.label], ['Resumen', 'Habilidades', 'Rasgos', 'Hechizos']);
      expect(tabs.first.tabKey, const Key('tab-summary'));
    });

    test('el icono del desglose sale de la clase', () {
      expect(
        ui.breakdownIcon(const BreakdownPart(source: 'class', label: 'Guerrero', value: 2)),
        isNotNull,
      );
      expect(
        ui.breakdownIcon(const BreakdownPart(source: 'item', label: 'Anillo', value: 1)),
        isNull,
      );
    });

    test('reconstruye los eventos 5e de tiempo real', () {
      final event = CampaignEvent.fromJson({'type': 'party.rest', 'campaignId': 'c1'});
      expect(event, isA<UnknownCampaignEvent>());
      expect(dnd5eEventOf(event), isA<PartyRest>());
    });
  });
}
