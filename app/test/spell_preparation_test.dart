import 'package:opentrpg/core/realtime/realtime_events.dart';
import 'package:opentrpg/systems/dnd5e/ui/spell_category.dart';
import 'package:opentrpg/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg/features/catalog/data/models.dart' show SpellDetail;
import 'package:opentrpg/features/catalog/ui/spell_detail_page.dart';
import 'package:opentrpg/features/characters/data/models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fake_realtime_hub.dart';
import 'helpers/fakes.dart';

const _prepareRoute = '/characters/ch1/prepare-spells';

Map<String, dynamic> _cleric({bool pending = false, List<Map<String, dynamic>>? spells}) =>
    makeCharacterJson(
      status: 'Active',
      combat: makeCombatJson(),
      classes: [
        {'classIndex': 'cleric', 'className': 'Cleric', 'level': 1},
      ],
      spells: spells ?? const [],
      spellPreparationPending: pending,
      spellPreparationReason: pending ? 'LongRest' : null,
    );

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _tapKey(WidgetTester tester, String key) => _tap(tester, find.byKey(Key(key)));

/// The checkbox inside the row [key] (phase 27: rows are `ListTile`s).
Checkbox _box(WidgetTester tester, String key) => tester.widget<Checkbox>(
  find.descendant(of: find.byKey(Key(key)), matching: find.byType(Checkbox)),
);

Future<({FakeCharactersRepository characters, FakeRealtimeHub hub, GoRouter router})> _pump(
  WidgetTester tester, {
  Map<String, dynamic>? character,
  Map<String, dynamic>? preparation,
  String location = '/campaigns/c1/player',
  FakeCatalogRepository? catalog,
}) async {
  final characters = FakeCharactersRepository(characters: [character ?? _cleric(pending: true)]);
  characters.preparations['ch1'] = preparation ?? makePreparationJson();
  final fakes = AppFakes(
    campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
    characters: characters,
    catalog: catalog,
  );
  final hub = FakeRealtimeHub();
  final router = await pumpRealApp(tester, location: location, fakes: fakes, realtime: hub);
  return (characters: characters, hub: hub, router: router);
}

void main() {
  group('modelos', () {
    test('SpellPreparation lee clases, candidatos y motivo', () {
      final prep = SpellPreparation.fromJson(
        makePreparationJson(
          reason: 'LevelUp',
          canKeep: false,
          keepProblem: 'Ahora puedes preparar como máximo 1.',
          prepared: ['bless'],
        ),
      );
      expect(prep.pending, isTrue);
      expect(prep.reason, SpellPreparationReason.levelUp);
      expect(prep.reason!.label, 'Nuevo nivel');
      expect(prep.canKeep, isFalse);
      expect(prep.keepProblem, 'Ahora puedes preparar como máximo 1.');
      final cleric = prep.classes.single;
      expect(cleric.max, 2);
      expect(cleric.prepared, ['bless']);
      expect(cleric.candidates.map((s) => s.index), contains('cure-wounds'));
      expect(cleric.candidates.first.category, 'Healing');
    });

    test('CharacterDetail lee spellPreparationPending y la categoría de los conjuros', () {
      final c = CharacterDetail.fromJson(
        _cleric(
          pending: true,
          spells: [
            {
              'spellIndex': 'bless',
              'classIndex': 'cleric',
              'isPrepared': true,
              'alwaysPrepared': false,
              'category': 'Buff',
            },
          ],
        ),
      );
      expect(c.spellPreparationPending, isTrue);
      expect(c.spellPreparationReason, SpellPreparationReason.longRest);
      expect(c.spells.single.category, 'Buff');
      expect(CharacterDetail.fromJson(_cleric()).spellPreparationPending, isFalse);
    });

    test('SpellCategory conoce las siete categorías', () {
      expect(SpellCategory.values, hasLength(7));
      expect(SpellCategory.fromApi('Summoning'), SpellCategory.summoning);
      expect(SpellCategory.fromApi('Nope'), isNull);
      expect(SpellCategory.healing.label, 'Curación');
    });
  });

  group('Prepara tus conjuros', () {
    testWidgets('se abre sola al entrar en Mi sesión y no deja volver atrás', (tester) async {
      final (characters: _, hub: _, :router) = await _pump(tester);

      expect(locationOf(router), _prepareRoute);
      expect(find.byKey(const Key('prepare-spells')), findsOneWidget);
      expect(find.text('Tras el descanso largo'), findsOneWidget);
      expect(find.byType(BackButton), findsNothing);

      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(locationOf(router), _prepareRoute);
    });

    testWidgets('sin pendiente no se abre', (tester) async {
      final (characters: _, hub: _, :router) = await _pump(tester, character: _cleric());
      expect(locationOf(router), '/campaigns/c1/player');
      expect(find.byKey(const Key('prepare-spells')), findsNothing);
    });

    testWidgets('un character.updated que lo activa la abre', (tester) async {
      final (:characters, :hub, :router) = await _pump(tester, character: _cleric());
      expect(locationOf(router), '/campaigns/c1/player');

      characters.setSpellPreparationPending('ch1');
      hub.emit(const CharacterUpdated(campaignId: 'c1', characterId: 'ch1'));
      await tester.pumpAndSettle();

      expect(locationOf(router), _prepareRoute);
    });

    testWidgets('si hay nivel pendiente, primero el asistente de subida', (tester) async {
      final (characters: _, hub: _, :router) = await _pump(
        tester,
        character: makeCharacterJson(
          status: 'Active',
          combat: makeCombatJson(),
          pendingLevelUpTo: 2,
          spellPreparationPending: true,
          spellPreparationReason: 'LevelUp',
        ),
      );
      expect(locationOf(router), '/characters/ch1/level-up');
    });

    testWidgets('Mantener los de ayer llama a keep y cierra la pantalla', (tester) async {
      final (:characters, hub: _, :router) = await _pump(
        tester,
        preparation: makePreparationJson(prepared: ['bless']),
      );
      expect(find.byKey(const Key('prepare-keep')), findsOneWidget);

      await _tapKey(tester, 'prepare-keep');

      expect(characters.keepCalls, 1);
      expect(characters.prepareBodies, isEmpty);
      expect(locationOf(router), '/campaigns/c1/player');
      expect(find.byKey(const Key('prepare-spells')), findsNothing);
    });

    testWidgets('sin canKeep no hay botón y se explica el motivo', (tester) async {
      await _pump(
        tester,
        preparation: makePreparationJson(
          canKeep: false,
          keepProblem: 'Ahora puedes preparar como máximo 2 conjuros de Cleric y tienes 3.',
        ),
      );
      expect(find.byKey(const Key('prepare-keep')), findsNothing);
      expect(find.byKey(const Key('prepare-keep-problem')), findsOneWidget);
      expect(find.textContaining('tienes 3'), findsOneWidget);
    });

    testWidgets('contador y límite: no deja pasar del máximo', (tester) async {
      await _pump(tester, preparation: makePreparationJson(max: 2, prepared: ['bless']));
      expect(find.text('1 de 2'), findsOneWidget);
      // Prepared spells start checked.
      expect(_box(tester, 'prepare-spell-bless').value, isTrue);

      await _tapKey(tester, 'prepare-spell-cure-wounds');
      expect(find.text('2 de 2'), findsOneWidget);
      // The rest are disabled; unchecking one frees a slot.
      expect(_box(tester, 'prepare-spell-guiding-bolt').onChanged, isNull);
      await _tapKey(tester, 'prepare-spell-bless');
      expect(find.text('1 de 2'), findsOneWidget);
      expect(_box(tester, 'prepare-spell-guiding-bolt').onChanged, isNotNull);
    });

    testWidgets('Preparar solo se activa con 1..max y envía todas las clases', (tester) async {
      final (:characters, hub: _, :router) = await _pump(tester);
      FilledButton confirm() =>
          tester.widget<FilledButton>(find.byKey(const Key('prepare-confirm')));
      expect(confirm().onPressed, isNull);

      await _tapKey(tester, 'prepare-spell-cure-wounds');
      await _tapKey(tester, 'prepare-spell-bless');
      expect(confirm().onPressed, isNotNull);

      await _tapKey(tester, 'prepare-confirm');

      expect(characters.prepareBodies, [
        {
          'cleric': ['cure-wounds', 'bless'],
        },
      ]);
      expect(locationOf(router), '/campaigns/c1/player');
    });

    testWidgets('buscador y filtros por nivel y por categoría', (tester) async {
      await _pump(
        tester,
        preparation: makePreparationJson(
          max: 5,
          candidates: [
            makePreparationSpellJson('cure-wounds', 'Cure Wounds', category: 'Healing'),
            makePreparationSpellJson('bless', 'Bless', category: 'Buff'),
            makePreparationSpellJson(
              'spiritual-weapon',
              'Spiritual Weapon',
              level: 2,
              category: 'Damage',
            ),
          ],
        ),
      );
      expect(find.byKey(const Key('prepare-spell-cure-wounds')), findsOneWidget);
      expect(find.byKey(const Key('prepare-spell-bless')), findsOneWidget);
      expect(find.byKey(const Key('prepare-spell-spiritual-weapon')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('prepare-search-cleric')), 'bles');
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('prepare-spell-bless')), findsOneWidget);
      expect(find.byKey(const Key('prepare-spell-cure-wounds')), findsNothing);

      await tester.enterText(find.byKey(const Key('prepare-search-cleric')), '');
      await tester.pumpAndSettle();
      await _tapKey(tester, 'prepare-level-cleric-2');
      expect(find.byKey(const Key('prepare-spell-spiritual-weapon')), findsOneWidget);
      expect(find.byKey(const Key('prepare-spell-bless')), findsNothing);

      await _tapKey(tester, 'prepare-level-cleric-2');
      await _tapKey(tester, 'prepare-category-cleric-Healing');
      expect(find.byKey(const Key('prepare-spell-cure-wounds')), findsOneWidget);
      expect(find.byKey(const Key('prepare-spell-bless')), findsNothing);
    });

    testWidgets('los siempre preparados salen fijos arriba y no cuentan', (tester) async {
      await _pump(
        tester,
        preparation: makePreparationJson(
          alwaysPrepared: [makePreparationSpellJson('bless', 'Bless', category: 'Buff')],
        ),
      );
      expect(find.text('Siempre preparados'), findsOneWidget);
      expect(find.byKey(const Key('prepare-always-bless')), findsOneWidget);
      expect(find.text('0 de 2'), findsOneWidget);
    });

    testWidgets('los errores del servidor salen en línea', (tester) async {
      final (:characters, hub: _, router: _) = await _pump(tester);
      characters.prepareError = dioError(400);
      await _tapKey(tester, 'prepare-spell-cure-wounds');
      await _tapKey(tester, 'prepare-confirm');

      expect(find.byKey(const Key('prepare-error')), findsOneWidget);
      expect(find.byKey(const Key('prepare-spells')), findsOneWidget);
    });

    testWidgets('cada conjuro lleva el icono de su categoría', (tester) async {
      await _pump(tester);
      for (final category in ['Healing', 'Buff', 'Damage', 'Defense']) {
        expect(find.byKey(Key('spell-category-$category')), findsWidgets);
      }
    });
  });

  group('iconos de categoría', () {
    testWidgets('la pestaña Hechizos muestra el icono de cada conjuro', (tester) async {
      final characters = FakeCharactersRepository(
        characters: [
          _cleric(
            spells: [
              {
                'spellIndex': 'cure-wounds',
                'classIndex': 'cleric',
                'isPrepared': true,
                'alwaysPrepared': false,
                'category': 'Healing',
                'spellName': 'Cure Wounds',
                'spellLevel': 1,
              },
            ],
          ),
        ],
      );
      final fakes = AppFakes(
        campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
        characters: characters,
      );
      await pumpRealApp(tester, location: '/characters/ch1', fakes: fakes);
      await openDetailTab(tester, 'tab-spells');

      expect(
        find.descendant(
          of: find.byKey(const Key('spell-cure-wounds')),
          matching: find.byKey(const Key('spell-category-Healing')),
        ),
        findsOneWidget,
      );
    });
  });

  group('Fase 26: detalle de los conjuros', () {
    testWidgets('el botón de una fila abre el detalle sin marcarla', (tester) async {
      await _pump(
        tester,
        preparation: makePreparationJson(max: 2, prepared: const []),
        catalog: FakeCatalogRepository(
          spellDetails: const {
            'bless': SpellDetail(
              index: 'bless',
              name: 'Bless',
              level: 1,
              description: ['You bless up to three creatures of your choice within range.'],
            ),
          },
        ),
      );
      expect(find.text('0 de 2'), findsOneWidget);

      await _tapKey(tester, 'detail-spell-bless');
      expect(find.byType(SpellDetailPage), findsOneWidget);
      expect(
        find.text('You bless up to three creatures of your choice within range.'),
        findsOneWidget,
      );

      await tester.pageBack();
      await tester.pumpAndSettle();
      expect(find.byType(SpellDetailPage), findsNothing);
      expect(find.text('0 de 2'), findsOneWidget);
      expect(_box(tester, 'prepare-spell-bless').value, isFalse);
    });

    testWidgets('el botón de detalle y la casilla comparten el centro vertical', (tester) async {
      await _pump(tester, preparation: makePreparationJson(max: 2, prepared: const ['bless']));
      final row = find.byKey(const Key('prepare-spell-bless'));
      final info = tester.getCenter(find.byKey(const Key('detail-spell-bless')));
      final box = tester.getCenter(find.descendant(of: row, matching: find.byType(Checkbox)));
      expect(info.dy, moreOrLessEquals(box.dy, epsilon: 0.5));
      expect(info.dx, lessThan(box.dx));
    });
  });
}
