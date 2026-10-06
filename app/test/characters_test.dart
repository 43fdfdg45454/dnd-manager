import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:dnd_companion/core/network/api_client.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';
import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/characters/data/view_mode_controller.dart';
import 'package:dnd_companion/features/characters/domain/character_format.dart';
import 'package:dnd_companion/features/characters/domain/payload_format.dart';
import 'package:flutter/material.dart' hide Page;
import 'package:flutter_test/flutter_test.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

FakeCatalogRepository _catalog() => FakeCatalogRepository(
  classList: [
    ClassSummary(index: 'fighter', name: 'Fighter', hitDie: 10),
    ClassSummary(index: 'wizard', name: 'Wizard', hitDie: 6, isSpellcaster: true),
  ],
  classDetails: {
    'wizard': ClassDetail(
      index: 'wizard',
      name: 'Wizard',
      hitDie: 6,
      isSpellcaster: true,
      savingThrows: const ['int', 'wis'],
      levels: const [
        ClassLevel(level: 1, spellSlots: [2, 0, 0, 0, 0, 0, 0, 0, 0]),
      ],
    ),
    'fighter': ClassDetail(
      index: 'fighter',
      name: 'Fighter',
      hitDie: 10,
      savingThrows: const ['str', 'con'],
      levels: const [
        ClassLevel(
          level: 1,
          features: [
            Feature(index: 'second-wind', name: 'Second Wind', level: 1, description: ['Heal.']),
          ],
        ),
        ClassLevel(
          level: 5,
          features: [Feature(index: 'extra-attack', name: 'Extra Attack', level: 5)],
        ),
      ],
    ),
  },
  raceList: [RaceSummary(index: 'human', name: 'Human')],
  raceDetails: {
    'human': RaceDetail(
      index: 'human',
      name: 'Human',
      traits: const [
        Trait(index: 'versatile', name: 'Versatile', description: ['Adaptable.']),
      ],
    ),
  },
  spellList: [
    makeSpell(index: 'fire-bolt', name: 'Fire Bolt', level: 0),
    makeSpell(index: 'magic-missile', name: 'Magic Missile', level: 1),
    makeSpell(index: 'fireball', name: 'Fireball', level: 3),
  ],
  spellDetails: {
    'fire-bolt': const SpellDetail(index: 'fire-bolt', name: 'Fire Bolt', level: 0),
    'magic-missile': const SpellDetail(index: 'magic-missile', name: 'Magic Missile', level: 1),
  },
);

/// The whole app (real router) at [location]. The signed-in user is `u1`.
Future<void> _pumpApp(
  WidgetTester tester, {
  required FakeCharactersRepository characters,
  required String location,
  CampaignRole role = CampaignRole.player,
  FakeCatalogRepository? catalog,
}) async {
  await pumpRealApp(
    tester,
    location: location,
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
      characters: characters,
      catalog: catalog ?? _catalog(),
    ),
  );
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

void main() {
  group('lista de personajes', () {
    testWidgets('la pestaña Personajes muestra una tarjeta por personaje', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(id: 'ch1', name: 'Thorin', status: 'Active'),
          makeCharacterJson(id: 'ch2', name: 'Elara', ownerUserId: null),
        ],
      );
      await _pumpApp(tester, characters: repository, location: '/campaigns/c1');

      await openGeneralSection(tester, 'characters');

      expect(find.byKey(const Key('character-ch1')), findsOneWidget);
      expect(find.byKey(const Key('character-ch2')), findsOneWidget);
      expect(find.text('Thorin'), findsOneWidget);
      expect(find.text('Human · Fighter 3 · Nivel 3'), findsNWidgets(2));
      expect(find.text('Activo'), findsOneWidget);
      expect(find.text('Borrador'), findsOneWidget);
      // A player sees the hit points of their own characters only.
      expect(find.text('PG 20 / 28'), findsOneWidget);
      expect(find.text('Nuevo personaje'), findsOneWidget);
    });

    testWidgets('un jugador solo abre sus personajes; el DM abre todos', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(id: 'ch1', name: 'Thorin', status: 'Active'),
          makeCharacterJson(id: 'ch2', name: 'Elara', ownerUserId: 'p2', status: 'Active'),
        ],
      );
      await _pumpApp(tester, characters: repository, location: '/campaigns/c1/general/characters');

      await tester.tap(find.byKey(const Key('character-ch2')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('character-title')), findsNothing);
      expect(find.text('Jugador: Usuario Demo'), findsNWidgets(2));

      await tester.tap(find.byKey(const Key('character-ch1')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('character-title')), findsOneWidget);
    });

    testWidgets('el DM ve los PG y abre la hoja de cualquier personaje', (tester) async {
      final repository = FakeCharactersRepository(
        isDm: true,
        characters: [
          makeCharacterJson(id: 'ch1', name: 'Thorin', status: 'Active'),
          makeCharacterJson(id: 'ch2', name: 'Elara', ownerUserId: 'p2', status: 'Active'),
        ],
      );
      await _pumpApp(
        tester,
        characters: repository,
        location: '/campaigns/c1/general/characters',
        role: CampaignRole.dm,
      );

      expect(find.text('PG 20 / 28'), findsNWidgets(2));
      await tester.tap(find.byKey(const Key('character-ch2')));
      await tester.pumpAndSettle();
      expect(find.text('Elara'), findsOneWidget);
      expect(find.byKey(const Key('character-title')), findsOneWidget);
    });

    testWidgets('"Nuevo personaje" abre el asistente sin elegir dueño para un jugador', (
      tester,
    ) async {
      final repository = FakeCharactersRepository();
      await _pumpApp(tester, characters: repository, location: '/campaigns/c1');
      await openGeneralSection(tester, 'characters');
      expect(find.text('Aún no hay personajes en esta campaña'), findsOneWidget);
      expect(find.byKey(const Key('characters-more')), findsNothing);

      await tester.tap(find.byKey(const Key('characters-new')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('step-name')), findsOneWidget);
      expect(find.byKey(const Key('wizard-owner')), findsNothing);
      expect(repository.created, isEmpty);
    });

    testWidgets('un DM sigue pudiendo crear un PNJ rápido sin dueño', (tester) async {
      final repository = FakeCharactersRepository(isDm: true);
      await _pumpApp(
        tester,
        characters: repository,
        location: '/campaigns/c1',
        role: CampaignRole.dm,
      );
      await openGeneralSection(tester, 'characters');

      await tester.tap(find.byKey(const Key('characters-more')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('characters-quick')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('character-name')), 'Guardia');
      await tester.tap(find.byKey(const Key('character-owner')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Sin dueño (PNJ)').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('character-create-submit')));
      await tester.pumpAndSettle();

      expect(repository.created.single.owner, (userId: null));
      expect(find.text('PNJ'), findsOneWidget);
    });

    testWidgets('el DM ve en la insignia el número de solicitudes pendientes', (tester) async {
      final repository = FakeCharactersRepository(
        isDm: true,
        requests: [
          makeChangeRequest(id: 'a'),
          makeChangeRequest(id: 'b'),
        ],
      );
      await _pumpApp(
        tester,
        characters: repository,
        location: '/campaigns/c1',
        role: CampaignRole.dm,
      );

      expect(
        find.descendant(
          of: find.byKey(const Key('change-requests-badge')),
          matching: find.text('2'),
        ),
        findsOneWidget,
      );
    });
  });

  group('hoja detallada', () {
    testWidgets('muestra los modificadores y valores calculados del DTO', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      Finder inCard(String key, String text) =>
          find.descendant(of: find.byKey(Key(key)), matching: find.text(text));

      expect(inCard('ability-str', '+3'), findsOneWidget);
      expect(inCard('ability-str', '16'), findsOneWidget);
      expect(inCard('ability-cha', '-1'), findsOneWidget);
      expect(inCard('tile-armor-class', '17'), findsOneWidget);
      expect(inCard('tile-initiative', '+2'), findsOneWidget);
      expect(inCard('tile-hp', '20 / 28'), findsOneWidget);
      expect(inCard('tile-temp-hp', '3'), findsOneWidget);
      expect(inCard('tile-passive-perception', '11'), findsOneWidget);
      expect(inCard('save-str', '+5'), findsOneWidget);
      expect(find.byKey(const Key('save-proficient-str')), findsOneWidget);
      expect(find.byKey(const Key('save-plain-dex')), findsOneWidget);
      expect(find.text('Human · Fighter 3 · Nivel 3'), findsOneWidget);
    });

    testWidgets('el icono de override muestra la nota con pulsación larga', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(
            overriddenFields: ['armorClass'],
            overrides: [
              {'field': 'armorClass', 'value': 17, 'note': 'Armadura encantada'},
            ],
          ),
        ],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      expect(find.byKey(const Key('override-armorClass')), findsOneWidget);
      expect(find.byKey(const Key('override-speed')), findsNothing);
      await tester.longPress(find.byKey(const Key('override-armorClass')));
      await tester.pumpAndSettle();
      expect(find.text('Armadura encantada'), findsOneWidget);
    });

    test('CharacterSheet.fromJson lee breakdowns e itemEffects', () {
      final sheet = CharacterSheet.fromJson({
        'itemEffects': [
          {'itemName': 'Guantes ágiles', 'kind': 'AbilityBonus', 'target': 'dex', 'value': 3},
          {'itemName': 'Capa', 'kind': 'SaveBonus', 'target': null, 'value': 1},
        ],
        'breakdowns': {
          'ability.dex': makeBreakdownJson([('base', 'Base', 14), ('item', 'Guantes ágiles', 3)]),
          'armorClass': {'total': 12, 'parts': <Object>[]},
        },
      });
      expect(sheet.itemEffects, hasLength(2));
      expect(sheet.itemEffects.first.itemName, 'Guantes ágiles');
      expect(sheet.itemEffects.first.kind, 'AbilityBonus');
      expect(sheet.itemEffects.first.target, 'dex');
      expect(sheet.itemEffects.first.value, 3);
      expect(sheet.itemEffects.last.target, isNull);
      final dex = sheet.breakdown('ability.dex')!;
      expect(dex.total, 17);
      expect(dex.parts.map((p) => (p.source, p.label, p.value)), [
        ('base', 'Base', 14),
        ('item', 'Guantes ágiles', 3),
      ]);
      expect(dex.hasItemOrOverride, isTrue);
      expect(sheet.breakdown('armorClass')!.hasItemOrOverride, isFalse);
      expect(sheet.breakdown('speed'), isNull);
      // Older servers send neither.
      final bare = CharacterSheet.fromJson(const {});
      expect(bare.itemEffects, isEmpty);
      expect(bare.breakdowns, isEmpty);
    });

    testWidgets('tocar un valor abre el desglose con sus partes y el total', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(
            breakdowns: {
              'ability.str': makeBreakdownJson([('base', 'Base', 15), ('race', 'Humano', 1)]),
              'armorClass': makeBreakdownJson([
                ('base', 'Armadura de cuero', 11),
                ('ability', 'Destreza', 2),
                ('shield', 'Escudo', 2),
                ('override', 'Ajuste manual: bendición', 2),
              ]),
            },
          ),
        ],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      expect(find.byKey(const Key('breakdown-sheet')), findsNothing);
      await tester.tap(find.byKey(const Key('stat-ability.str')));
      await tester.pumpAndSettle();

      final sheet = find.byKey(const Key('breakdown-sheet'));
      expect(sheet, findsOneWidget);
      expect(find.descendant(of: sheet, matching: find.text('Fuerza')), findsOneWidget);
      expect(find.descendant(of: sheet, matching: find.text('Base')), findsOneWidget);
      expect(find.descendant(of: sheet, matching: find.text('15')), findsOneWidget);
      expect(find.descendant(of: sheet, matching: find.text('Humano')), findsOneWidget);
      expect(find.descendant(of: sheet, matching: find.text('+1')), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '16');
      // Without items or overrides there is no gold dot.
      expect(find.byKey(const Key('stat-mark-ability.str')), findsNothing);

      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('breakdown-sheet')), findsNothing);

      await _tap(tester, find.byKey(const Key('stat-armorClass')));
      expect(find.byKey(const Key('breakdown-sheet')), findsOneWidget);
      expect(find.text('Ajuste manual: bendición'), findsOneWidget);
      expect(find.text('Armadura de cuero'), findsOneWidget);
      expect(find.text('Escudo'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '17');
      // Override parts do mark the value.
      expect(find.byKey(const Key('stat-mark-armorClass')), findsOneWidget);
    });

    testWidgets('un valor afectado por un objeto muestra el punto dorado', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(
            itemEffects: [
              {'itemName': 'Guantes ágiles', 'kind': 'AbilityBonus', 'target': 'dex', 'value': 3},
            ],
            breakdowns: {
              'ability.dex': makeBreakdownJson([
                ('base', 'Base', 14),
                ('item', 'Guantes ágiles', 3),
              ]),
              'ability.str': makeBreakdownJson([('base', 'Base', 16)]),
            },
          ),
        ],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      expect(find.byKey(const Key('stat-mark-ability.dex')), findsOneWidget);
      expect(find.byKey(const Key('stat-mark-ability.str')), findsNothing);
      expect(find.text('Guantes ágiles: +3 Destreza'), findsOneWidget);

      await tester.tap(find.byKey(const Key('stat-ability.dex')));
      await tester.pumpAndSettle();
      expect(find.text('Guantes ágiles'), findsOneWidget);
      expect(find.text('+3'), findsWidgets);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '15');
    });

    testWidgets('salvaciones, habilidades y velocidad también tienen desglose', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(
            breakdowns: {
              'save.str': makeBreakdownJson([
                ('ability', 'Fuerza', 3),
                ('proficiency', 'Competencia', 2),
              ]),
              'speed': makeBreakdownJson([('base', 'Humano', 30)]),
              'skill.stealth': makeBreakdownJson([
                ('ability', 'Destreza', 2),
                ('item', 'Botas élficas', 2),
              ]),
            },
          ),
        ],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      await _tap(tester, find.byKey(const Key('stat-save.str')));
      expect(find.text('Salvación de Fuerza'), findsWidgets);
      expect(find.text('Competencia'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '+5');
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      await _tap(tester, find.byKey(const Key('stat-speed')));
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '30 pies');
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      await _tap(tester, find.byKey(const Key('tab-skills')));
      expect(find.byKey(const Key('stat-mark-skill.stealth')), findsOneWidget);
      await _tap(tester, find.byKey(const Key('stat-skill.stealth')));
      expect(find.text('Botas élficas'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '+2');
    });

    testWidgets('Habilidades muestra valor y competencia', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      await tester.tap(find.byKey(const Key('tab-skills')));
      await tester.pumpAndSettle();

      expect(find.text('Atletismo'), findsOneWidget);
      expect(find.text('Sigilo'), findsOneWidget);
      expect(find.text('Des · Competente'), findsNothing);
      expect(find.text('Fue · Competente'), findsOneWidget);
    });

    testWidgets('Rasgos muestra los de clase hasta el nivel actual y los raciales', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      await tester.tap(find.byKey(const Key('tab-traits')));
      await tester.pumpAndSettle();

      expect(find.text('Second Wind'), findsOneWidget);
      // Level 5 features are not available to a level 3 fighter.
      expect(find.text('Extra Attack'), findsNothing);
      expect(find.text('Versatile'), findsOneWidget);
      await tester.tap(find.text('Second Wind'));
      await tester.pumpAndSettle();
      expect(find.text('Heal.'), findsOneWidget);
    });

    testWidgets('Hechizos agrupa por nivel, marca preparados y muestra los slots', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(
            classes: [
              {'classIndex': 'wizard', 'className': 'Wizard', 'level': 1},
            ],
            spells: [
              {
                'spellIndex': 'magic-missile',
                'classIndex': 'wizard',
                'isPrepared': true,
                'alwaysPrepared': false,
              },
              {
                'spellIndex': 'fire-bolt',
                'classIndex': 'wizard',
                'isPrepared': false,
                'alwaysPrepared': false,
              },
            ],
            spellSlots: [
              {'level': 1, 'max': 2, 'used': 1},
            ],
          ),
        ],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      await tester.tap(find.byKey(const Key('tab-spells')));
      await tester.pumpAndSettle();

      expect(find.text('Truco'), findsOneWidget);
      expect(find.text('Nivel 1'), findsNWidgets(2)); // slot row + spell group
      expect(find.text('Usados 1 / 2'), findsOneWidget);
      expect(find.text('Magic Missile'), findsOneWidget);
      expect(find.text('Preparado'), findsOneWidget);
    });

    testWidgets('Inventario, Notas y trasfondo', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [makeCharacterJson(notes: 'Debe dinero al herrero')],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      await tester.tap(find.byKey(const Key('tab-inventory')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('inventory-money')), findsOneWidget);
      expect(find.byKey(const Key('inventory-add')), findsOneWidget);

      await tester.tap(find.byKey(const Key('tab-notes')));
      await tester.pumpAndSettle();
      expect(find.text('Debe dinero al herrero'), findsOneWidget);
      expect(find.text('Sin historia.'), findsOneWidget);
    });

    testWidgets('la ficha avisa de "Contenido no disponible" y nombra lo que falta', (
      tester,
    ) async {
      final repository = FakeCharactersRepository(
        characters: [
          {
            ...makeCharacterJson(
              classes: [
                {
                  'classIndex': 'fighter',
                  'className': 'Fighter',
                  'level': 3,
                  'subclassIndex': 'cavaliere',
                  'catalogMissing': true,
                },
              ],
              spells: [
                {
                  'spellIndex': 'rayo-ejemplo',
                  'classIndex': 'fighter',
                  'isPrepared': true,
                  'alwaysPrepared': false,
                  'catalogMissing': true,
                },
              ],
            ),
            'raceCatalogMissing': true,
            'catalogMissing': true,
          },
        ],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      expect(find.byKey(const Key('catalog-missing')), findsOneWidget);
      expect(find.text('Contenido no disponible'), findsOneWidget);
      final tooltip = tester.widget<Tooltip>(
        find.ancestor(of: find.byKey(const Key('catalog-missing')), matching: find.byType(Tooltip)),
      );
      expect(tooltip.message, contains('clase o subclase, raza, conjuros'));
      expect(tooltip.message, isNot(contains('trasfondo')));
    });

    testWidgets('sin contenido ausente no hay aviso', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      expect(find.byKey(const Key('catalog-missing')), findsNothing);
    });

    test('CharacterDetail lee los indicadores catalogMissing', () {
      final detail = CharacterDetail.fromJson({
        ...makeCharacterJson(),
        'backgroundCatalogMissing': true,
        'catalogMissing': true,
      });
      expect(detail.backgroundCatalogMissing, isTrue);
      expect(detail.raceCatalogMissing, isFalse);
      expect(detail.missingContent, ['trasfondo']);

      // Only the global flag: a generic description.
      final generic = CharacterDetail.fromJson({...makeCharacterJson(), 'catalogMissing': true});
      expect(generic.missingContent, ['contenido']);
      expect(CharacterDetail.fromJson(makeCharacterJson()).missingContent, isEmpty);
    });

    testWidgets('el conmutador Detallado / Combate está habilitado', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      expect(find.text('Detallado'), findsOneWidget);
      expect(find.text('Combate'), findsWidgets);
      final segmented = tester.widget<SegmentedButton<CharacterViewMode>>(
        find.byKey(const Key('view-mode')),
      );
      expect(segmented.segments.every((s) => s.enabled), isTrue);
      expect(find.byKey(const Key('dice-fab')), findsOneWidget);
    });

    testWidgets('el dueño en Draft ve Enviar al DM; un Player no ve Activar', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      expect(find.text('Editar hoja'), findsOneWidget);
      expect(find.text('Enviar al DM'), findsOneWidget);
      expect(find.text('Activar'), findsNothing);

      await tester.tap(find.byKey(const Key('character-submit')));
      await tester.pumpAndSettle();
      expect(repository.submitted, ['ch1']);
      expect(find.text('Enviado al DM para aprobación'), findsOneWidget);
      // The pending activation hides the button and shows the pending chip.
      expect(find.text('Enviar al DM'), findsNothing);
      expect(find.text('1 solicitud pendiente'), findsOneWidget);
    });

    testWidgets('un Player con el personaje activo no ve Activar ni Enviar', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [makeCharacterJson(status: 'Active')],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      expect(find.text('Editar hoja'), findsOneWidget);
      expect(find.text('Activar'), findsNothing);
      expect(find.text('Enviar al DM'), findsNothing);
      // The owner cannot delete an Active character.
      expect(find.byKey(const Key('character-menu')), findsNothing);
    });

    testWidgets('el DM activa un personaje en Draft', (tester) async {
      final repository = FakeCharactersRepository(
        isDm: true,
        characters: [makeCharacterJson(ownerUserId: 'p2')],
      );
      await _pumpApp(
        tester,
        characters: repository,
        location: '/characters/ch1',
        role: CampaignRole.dm,
      );

      expect(find.text('Activar'), findsOneWidget);
      expect(find.text('Enviar al DM'), findsNothing);
      await tester.tap(find.byKey(const Key('character-activate')));
      await tester.pumpAndSettle();

      expect(repository.activated, ['ch1']);
      expect(find.text('Personaje activado.'), findsOneWidget);
      expect(find.text('Activar'), findsNothing);
      expect(find.text('Activo'), findsOneWidget);
    });

    testWidgets('eliminar pide confirmación', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      await tester.tap(find.byKey(const Key('character-menu')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Eliminar'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm-action')));
      await tester.pumpAndSettle();

      expect(repository.deleted, ['ch1']);
      expect(find.text('Personaje eliminado.'), findsOneWidget);
    });

    testWidgets('un 403 se muestra en español con Reintentar', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()])
        ..error = dioError(403);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      expect(find.text('No tienes permiso para hacer eso con este personaje.'), findsOneWidget);
      expect(find.text('Reintentar'), findsOneWidget);
    });
  });

  group('editor de hoja', () {
    const approvalNotice = 'Los cambios de un personaje activo se envían al DM para su aprobación.';

    testWidgets('el aviso de aprobación se muestra a un jugador con personaje activo', (
      tester,
    ) async {
      final repository = FakeCharactersRepository(
        characters: [makeCharacterJson(status: 'Active')],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1/edit');

      expect(find.byKey(const Key('editor-name')), findsOneWidget);
      expect(find.text(approvalNotice), findsOneWidget);
    });

    testWidgets('el aviso de aprobación no aparece para el DM', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [makeCharacterJson(status: 'Active')],
        isDm: true,
      );
      await _pumpApp(
        tester,
        characters: repository,
        location: '/characters/ch1/edit',
        role: CampaignRole.dm,
      );

      expect(find.byKey(const Key('editor-name')), findsOneWidget);
      expect(find.text(approvalNotice), findsNothing);
    });

    testWidgets('el aviso de aprobación no aparece para el Owner', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [makeCharacterJson(status: 'Active')],
        isDm: true,
      );
      await _pumpApp(
        tester,
        characters: repository,
        location: '/characters/ch1/edit',
        role: CampaignRole.owner,
      );

      expect(find.text(approvalNotice), findsNothing);
    });

    testWidgets('guardar en un personaje Active muestra el envío al DM', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [makeCharacterJson(status: 'Active')],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      await tester.tap(find.byKey(const Key('character-edit')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('editor-name')), 'Thorin II');
      await tester.tap(find.byKey(const Key('editor-save')));
      await tester.pumpAndSettle();

      expect(find.text('Enviado al DM para aprobación'), findsOneWidget);
      // Only the changed field is sent.
      expect(repository.patches.single.toJson(), {'name': 'Thorin II'});
      // Back on the sheet, which still shows the old name until the DM approves.
      expect(find.byKey(const Key('character-title')), findsOneWidget);
      expect(find.text('Thorin'), findsOneWidget);
      expect(find.text('1 solicitud pendiente'), findsOneWidget);
    });

    testWidgets('el DM guarda directamente y la hoja se actualiza', (tester) async {
      final repository = FakeCharactersRepository(
        isDm: true,
        characters: [makeCharacterJson(status: 'Active', ownerUserId: 'p2')],
      );
      await _pumpApp(
        tester,
        characters: repository,
        location: '/characters/ch1',
        role: CampaignRole.dm,
      );

      await tester.tap(find.byKey(const Key('character-edit')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('editor-name')), 'Thorin II');
      await tester.tap(find.byKey(const Key('editor-save')));
      await tester.pumpAndSettle();

      expect(find.text('Hoja guardada.'), findsOneWidget);
      expect(find.text('Thorin II'), findsOneWidget);
    });

    testWidgets('sin cambios no se envía nada', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      await tester.tap(find.byKey(const Key('character-edit')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('editor-save')));
      await tester.pumpAndSettle();

      expect(repository.patches, isEmpty);
      expect(find.text('No hay cambios que guardar.'), findsOneWidget);
    });

    testWidgets('valida el nombre y las puntuaciones base', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1');

      await tester.tap(find.byKey(const Key('character-edit')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('editor-name')), '');
      await tester.enterText(find.byKey(const Key('base-str')), '31');
      await tester.tap(find.byKey(const Key('editor-save')));
      await tester.pumpAndSettle();

      expect(find.text('Introduce un nombre'), findsOneWidget);
      expect(find.text('1 a 30'), findsOneWidget);
      expect(repository.patches, isEmpty);
    });

    testWidgets('Compra por puntos fija las puntuaciones con 27 puntos', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1/edit');

      await _tap(tester, find.byKey(const Key('editor-point-buy')));
      expect(find.text('Puntos restantes: 27 / 27'), findsOneWidget);
      for (var i = 0; i < 7; i++) {
        await tester.tap(find.byKey(const Key('point-buy-plus-str')));
        await tester.pump();
      }
      // 8 -> 15 costs 9 points; the plus button is disabled at 15.
      expect(find.text('Puntos restantes: 18 / 27'), findsOneWidget);
      expect(
        tester.widget<IconButton>(find.byKey(const Key('point-buy-plus-str'))).onPressed,
        isNull,
      );
      await tester.tap(find.byKey(const Key('point-buy-apply')));
      await tester.pumpAndSettle();

      expect(
        tester.widget<TextFormField>(find.byKey(const Key('base-str'))).controller!.text,
        '15',
      );
      expect(tester.widget<TextFormField>(find.byKey(const Key('base-dex'))).controller!.text, '8');
    });

    testWidgets('elegir la clase principal marca sus salvaciones y suma el nivel', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1/edit');

      expect(find.text('Nivel total: 3'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('class-select-0')));
      await tester.tap(find.text('Wizard').last);
      await tester.pumpAndSettle();

      expect(tester.widget<FilterChip>(find.byKey(const Key('save-prof-int'))).selected, isTrue);
      expect(tester.widget<FilterChip>(find.byKey(const Key('save-prof-str'))).selected, isFalse);

      await _tap(tester, find.byKey(const Key('editor-add-class')));
      expect(find.byKey(const Key('class-select-1')), findsOneWidget);
      expect(find.text('Nivel total: 4'), findsOneWidget);
      await _tap(tester, find.byKey(const Key('class-remove-1')));
      expect(find.text('Nivel total: 3'), findsOneWidget);
    });

    testWidgets('las listas modificadas se envían completas; las demás no', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1/edit');

      await _tap(tester, find.byKey(const Key('skill-prof-stealth')));
      await _tap(tester, find.byKey(const Key('skill-exp-stealth')));
      await _tap(tester, find.byKey(const Key('editor-add-override')));
      await tester.enterText(find.byKey(const Key('override-value-0')), '18');
      await tester.enterText(find.byKey(const Key('override-note-0')), 'Bendición');
      await tester.enterText(find.byKey(const Key('editor-gold')), '20,5');
      await _tap(tester, find.byKey(const Key('editor-save-bottom')));

      final json = repository.patches.single.toJson();
      expect(json.keys.toSet(), {'proficiencies', 'overrides', 'copperPieces'});
      expect(json['copperPieces'], 2050);
      expect(json['overrides'], [
        {'field': 'hitPointsMax', 'value': 18, 'note': 'Bendición'},
      ]);
      expect(json['proficiencies'], [
        {'type': 'Skill', 'key': 'stealth', 'expertise': true},
      ]);
    });

    testWidgets('el buscador de hechizos filtra por nivel máximo y añade el elegido', (
      tester,
    ) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(
            classes: [
              {'classIndex': 'wizard', 'className': 'Wizard', 'level': 1},
            ],
          ),
        ],
      );
      await _pumpApp(tester, characters: repository, location: '/characters/ch1/edit');

      await _tap(tester, find.byKey(const Key('editor-add-spell')));
      // A level 1 wizard only has level 1 slots: Fireball (3) is not offered.
      expect(find.byKey(const Key('picker-spell-fire-bolt')), findsOneWidget);
      expect(find.byKey(const Key('picker-spell-magic-missile')), findsOneWidget);
      expect(find.byKey(const Key('picker-spell-fireball')), findsNothing);

      await tester.tap(find.byKey(const Key('picker-spell-magic-missile')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('spell-picker-done')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('spell-row-magic-missile')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('spell-prepared-magic-missile')));
      await _tap(tester, find.byKey(const Key('editor-save-bottom')));

      expect(repository.patches.single.toJson(), {
        'spells': [
          {
            'spellIndex': 'magic-missile',
            'classIndex': 'wizard',
            'isPrepared': true,
            'alwaysPrepared': false,
          },
        ],
      });
    });

    testWidgets('sin clase lanzadora no se abre el buscador de hechizos', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1/edit');

      await _tap(tester, find.byKey(const Key('editor-add-spell')));

      expect(find.text('Añade primero una clase que lance conjuros.'), findsOneWidget);
      expect(find.byKey(const Key('spell-picker-search')), findsNothing);
    });

    testWidgets('el modo manual de PG exige un override de PG máximos', (tester) async {
      final repository = FakeCharactersRepository(characters: [makeCharacterJson()]);
      await _pumpApp(tester, characters: repository, location: '/characters/ch1/edit');

      await _tap(tester, find.text('Manual'));
      await _tap(tester, find.byKey(const Key('editor-save-bottom')));

      expect(find.textContaining('requiere un valor modificado'), findsWidgets);
      expect(repository.patches, isEmpty);
    });
  });

  group('solicitudes de cambio', () {
    testWidgets('el DM aprueba y la solicitud desaparece de pendientes', (tester) async {
      final repository = FakeCharactersRepository(
        isDm: true,
        characters: [makeCharacterJson(status: 'Active')],
        requests: [
          makeChangeRequest(
            payload: {
              'name': 'Thorin II',
              'baseAbilities': {'str': 17, 'dex': 14, 'con': 13, 'int': 10, 'wis': 12, 'cha': 8},
              'copperPieces': 2050,
            },
          ),
        ],
      );
      await _pumpApp(
        tester,
        characters: repository,
        location: '/campaigns/c1/change-requests',
        role: CampaignRole.dm,
      );

      expect(find.byKey(const Key('change-request-cr1')), findsOneWidget);
      expect(find.textContaining('Edición de hoja · Pendiente'), findsOneWidget);
      // Readable diff of the payload.
      await tester.tap(find.byKey(const Key('change-request-tile-cr1')));
      await tester.pumpAndSettle();
      expect(find.text('Nombre: Thorin II'), findsOneWidget);
      expect(find.textContaining('Fue 17'), findsOneWidget);
      expect(find.textContaining('20.5 gp'), findsOneWidget);

      await tester.tap(find.byKey(const Key('approve-cr1')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('comment-field')), 'Aprobado');
      await tester.tap(find.byKey(const Key('comment-submit')));
      await tester.pumpAndSettle();

      expect(repository.approvals.single, (id: 'cr1', comment: 'Aprobado'));
      expect(find.text('Solicitud aprobada.'), findsOneWidget);
      expect(find.byKey(const Key('change-request-cr1')), findsNothing);
      expect(find.text('No hay solicitudes en esta lista'), findsOneWidget);

      // It moved to the approved list.
      await tester.tap(find.byKey(const Key('filter-Approved')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('change-request-cr1')), findsOneWidget);
    });

    testWidgets('rechazar exige un comentario', (tester) async {
      final repository = FakeCharactersRepository(
        isDm: true,
        characters: [makeCharacterJson(status: 'Active')],
        requests: [makeChangeRequest()],
      );
      await _pumpApp(
        tester,
        characters: repository,
        location: '/campaigns/c1/change-requests',
        role: CampaignRole.dm,
      );

      await tester.tap(find.byKey(const Key('reject-cr1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('comment-submit')));
      await tester.pumpAndSettle();
      expect(find.text('Escribe un comentario'), findsOneWidget);
      expect(repository.rejections, isEmpty);

      await tester.enterText(find.byKey(const Key('comment-field')), 'Demasiado fuerte');
      await tester.tap(find.byKey(const Key('comment-submit')));
      await tester.pumpAndSettle();

      expect(repository.rejections.single, (id: 'cr1', comment: 'Demasiado fuerte'));
      expect(find.text('Solicitud rechazada.'), findsOneWidget);
      expect(find.byKey(const Key('change-request-cr1')), findsNothing);
    });

    testWidgets('el solicitante solo puede cancelar la suya', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [makeCharacterJson(status: 'Active')],
        requests: [makeChangeRequest(requestedByUserId: 'u1')],
      );
      await _pumpApp(tester, characters: repository, location: '/campaigns/c1/change-requests');

      expect(find.byKey(const Key('approve-cr1')), findsNothing);
      expect(find.byKey(const Key('reject-cr1')), findsNothing);
      await tester.tap(find.byKey(const Key('cancel-cr1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm-action')));
      await tester.pumpAndSettle();

      expect(repository.cancellations, ['cr1']);
      expect(find.text('Solicitud cancelada.'), findsOneWidget);
      expect(find.byKey(const Key('change-request-cr1')), findsNothing);
    });

    testWidgets('un 409 al aprobar se explica y refresca la lista', (tester) async {
      final repository = FakeCharactersRepository(
        isDm: true,
        characters: [makeCharacterJson(status: 'Active')],
        requests: [makeChangeRequest()],
      );
      await _pumpApp(
        tester,
        characters: repository,
        location: '/campaigns/c1/change-requests',
        role: CampaignRole.dm,
      );
      // Someone else resolves it between loading the list and tapping.
      repository.requests[0] = makeChangeRequest(status: 'Approved');

      await tester.tap(find.byKey(const Key('approve-cr1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('comment-submit')));
      await tester.pumpAndSettle();

      expect(find.text('La solicitud ya no está pendiente.'), findsOneWidget);
      expect(find.byKey(const Key('change-request-cr1')), findsNothing);
    });
  });

  group('modelos y utilidades', () {
    test('CharacterDetail lee los campos almacenados y la hoja', () {
      final detail = CharacterDetail.fromJson(
        makeCharacterJson(
          overrides: [
            {'field': 'speed', 'value': 40, 'note': 'Botas'},
          ],
          pending: [makeChangeRequestJson(type: 'Activate')],
        ),
      );
      expect(detail.status, CharacterStatus.draft);
      expect(detail.baseAbilities['str'], 15);
      expect(detail.sheet.ability('str').modifier, 3);
      expect(detail.sheet.savingThrows['con']!.proficient, isTrue);
      expect(detail.totalLevel, 3);
      expect(detail.overrideOf('speed')!.note, 'Botas');
      expect(detail.hasPendingActivation, isTrue);
      expect(CharacterStatus.draft.label, 'Borrador');
      expect(CharacterStatus.active.label, 'Activo');
    });

    test('SheetPatch solo serializa los campos presentes', () {
      expect(const SheetPatch().toJson(), isEmpty);
      expect(const SheetPatch().isEmpty, isTrue);
      final json = const SheetPatch(
        name: 'Elara',
        hpMode: HpMode.manual,
        applyRacialBonuses: false,
        classes: [SheetPatchClass(classIndex: 'wizard', level: 2)],
        clear: {'subraceIndex'},
      ).toJson();
      expect(json, {
        'name': 'Elara',
        'hpMode': 'Manual',
        'applyRacialBonuses': false,
        'classes': [
          {'classIndex': 'wizard', 'level': 2},
        ],
        'subraceIndex': null,
      });
    });

    test('compra por puntos y dinero', () {
      expect(pointBuyCost(8), 0);
      expect(pointBuyCost(13), 5);
      expect(pointBuyCost(15), 9);
      expect(pointBuyTotal([15, 15, 15, 8, 8, 8]), 27);
      expect(copperToGoldText(1500), '15');
      expect(copperToGoldText(1550), '15.5');
      expect(copperToGoldText(1505), '15.05');
      expect(goldTextToCopper('15,5'), 1550);
      expect(goldTextToCopper('abc'), isNull);
      expect(goldTextToCopper('-1'), isNull);
      expect(formatModifier(3), '+3');
      expect(formatModifier(0), '+0');
      expect(formatModifier(-2), '-2');
    });

    test('describePayload da etiquetas y valores legibles', () {
      final lines = describePayload({
        'classes': [
          {'classIndex': 'wizard', 'level': 2, 'subclassIndex': 'evocation'},
        ],
        'applyRacialBonuses': true,
        'overrides': [
          {'field': 'armorClass', 'value': 17, 'note': 'Escudo'},
        ],
        'extra': {'a': 1},
      });
      expect(lines[0], (label: 'Clases', value: 'Wizard 2 (Evocation)'));
      expect(lines[1], (label: 'Aplicar bonos raciales', value: 'Sí'));
      expect(lines[2].value, 'Clase de armadura: 17 (Escudo)');
      expect(lines[3], (label: 'extra', value: 'a: 1'));
    });

    test('los estados y tipos de solicitud tienen etiqueta en español', () {
      expect(ChangeRequestStatus.fromApi('Approved').label, 'Aprobada');
      expect(ChangeRequestType.fromApi('EditSheet').label, 'Edición de hoja');
      expect(ChangeRequestType.fromApi('???'), ChangeRequestType.other);
    });

    test('CharacterPermissions según rol y estado', () {
      CharacterPermissions of(String status, String owner, CampaignRole? role) =>
          CharacterPermissions(
            character: CharacterDetail.fromJson(
              makeCharacterJson(status: status, ownerUserId: owner),
            ),
            myUserId: 'u1',
            myRole: role,
          );
      final playerDraft = of('Draft', 'u1', CampaignRole.player);
      expect(playerDraft.canSubmit, isTrue);
      expect(playerDraft.canActivate, isFalse);
      expect(playerDraft.canDelete, isTrue);
      final playerActive = of('Active', 'u1', CampaignRole.player);
      expect(playerActive.canEdit, isTrue);
      expect(playerActive.canDelete, isFalse);
      final stranger = of('Draft', 'p2', CampaignRole.player);
      expect(stranger.canEdit, isFalse);
      final dm = of('Draft', 'p2', CampaignRole.dm);
      expect(dm.canActivate, isTrue);
      expect(dm.canEdit, isTrue);
      expect(dm.canSubmit, isFalse);
      expect(of('Draft', 'p2', null).canActivate, isFalse);
    });
  });

  group('CharactersRepository', () {
    late _StatusAdapter adapter;
    late CharactersRepository repository;

    setUp(() {
      adapter = _StatusAdapter();
      repository = CharactersRepository(
        ApiClient(
          baseUrl: 'http://localhost',
          dio: Dio(BaseOptions(baseUrl: 'http://localhost'))..httpClientAdapter = adapter,
        ),
      );
    });

    test('PATCH sheet: 200 es Saved y 202 es PendingApproval', () async {
      adapter.status = 200;
      adapter.body = makeCharacterJson();
      final saved = await repository.patchSheet('ch1', const SheetPatch(name: 'X'));
      expect(saved, isA<Saved>());
      expect(adapter.requests.last.path, '/api/v1/characters/ch1/sheet');
      expect(adapter.requests.last.method, 'PATCH');
      expect(adapter.requests.last.data, {'name': 'X'});

      adapter.status = 202;
      adapter.body = makeChangeRequestJson();
      final pending = await repository.patchSheet('ch1', const SheetPatch(name: 'X'));
      expect(pending, isA<PendingApproval>());
      expect((pending as PendingApproval).changeRequest.id, 'cr1');
    });

    test('crear: el dueño solo se envía cuando se indica', () async {
      adapter.status = 201;
      adapter.body = makeCharacterJson();
      await repository.create('c1', name: 'A');
      expect(adapter.requests.last.data, {'name': 'A'});
      await repository.create('c1', name: 'B', owner: (userId: null));
      expect(adapter.requests.last.data, {'name': 'B', 'ownerUserId': null});
      await repository.create('c1', name: 'C', owner: (userId: 'p2'));
      expect(adapter.requests.last.data, {'name': 'C', 'ownerUserId': 'p2'});
    });

    test('las solicitudes se filtran por estado y se resuelven', () async {
      adapter.status = 200;
      adapter.body = [makeChangeRequestJson()];
      final list = await repository.changeRequests('c1', status: ChangeRequestStatus.pending);
      expect(list.single.isPending, isTrue);
      expect(adapter.requests.last.queryParameters, {'status': 'Pending'});
      await repository.changeRequests('c1');
      expect(adapter.requests.last.queryParameters, isEmpty);

      adapter.body = makeChangeRequestJson(status: 'Rejected');
      await repository.reject('cr1', comment: 'No');
      expect(adapter.requests.last.path, '/api/v1/change-requests/cr1/reject');
      expect(adapter.requests.last.data, {'comment': 'No'});
      await repository.approve('cr1');
      expect(adapter.requests.last.data, <String, dynamic>{});
    });
  });
}

/// Answers every request with [status] and [body], recording the requests.
class _StatusAdapter implements HttpClientAdapter {
  int status = 200;
  Object? body;
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
