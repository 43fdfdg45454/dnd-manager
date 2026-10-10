import 'package:dnd_companion/core/motion/flames.dart';
import 'package:dnd_companion/core/motion/flash.dart';
import 'package:dnd_companion/core/motion/motion_settings.dart';
import 'package:dnd_companion/core/motion/pulse.dart';
import 'package:dnd_companion/core/motion/rest_animations.dart';
import 'package:dnd_companion/core/motion/shake.dart';
import 'package:dnd_companion/core/motion/vignette.dart';
import 'package:dnd_companion/core/motion/wax_seal.dart';
import 'package:dnd_companion/core/realtime/realtime_events.dart';
import 'package:dnd_companion/core/realtime/realtime_hub.dart';
import 'package:dnd_companion/core/theme/app_theme.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/campaigns/ui/general/campaign_section_page.dart';
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/characters/ui/combat/combat_support.dart';
import 'package:dnd_companion/features/characters/ui/combat/rest_celebration.dart';
import 'package:dnd_companion/features/characters/ui/combat/vitals_section.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

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

/// Pumps frame by frame until [finder] finds something (one-shot overlays
/// under reduced motion live for a single frame). False when it never does.
Future<bool> _seenWithin(WidgetTester tester, Finder finder, {int frames = 30}) async {
  for (var i = 0; i < frames; i++) {
    if (finder.evaluate().isNotEmpty) return true;
    await tester.pump();
  }
  return finder.evaluate().isNotEmpty;
}

/// "Mi sesión" of `u1` with [character] (an Active `ch1` by default).
Future<({AppFakes fakes, FakeCharactersRepository characters, FakeRealtimeHub hub})> _pumpPlayer(
  WidgetTester tester, {
  Map<String, dynamic>? character,
  FakeMessagesRepository? messages,
  String location = '/campaigns/c1/player',
}) async {
  final characters = FakeCharactersRepository(
    characters: [character ?? makeCharacterJson(status: 'Active', combat: makeCombatJson())],
  );
  final fakes = AppFakes(
    campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.player)]),
    characters: characters,
    messages: messages,
  );
  final hub = FakeRealtimeHub();
  await pumpRealApp(tester, location: location, fakes: fakes, realtime: hub);
  return (fakes: fakes, characters: characters, hub: hub);
}

Future<void> _damage(WidgetTester tester, int amount, {String button = 'hp-minus'}) async {
  await tester.enterText(find.byKey(const Key('hp-amount')), '$amount');
  await _tapKey(tester, button);
}

/// [child] in the dark theme with real (not reduced) motion unless [reduced].
Widget _harness(Widget child, {bool reduced = false}) => ProviderScope(
  child: MaterialApp(
    theme: AppTheme.dark(),
    home: MotionScope(
      reduced: reduced,
      child: Scaffold(body: child),
    ),
  ),
);

double _shakeOffset(WidgetTester tester) {
  final transform = tester.widget<Transform>(
    find.descendant(of: find.byKey(const Key('hp-shake')), matching: find.byType(Transform)).first,
  );
  return transform.transform.getTranslation().x;
}

void main() {
  group('shell de campaña', () {
    testWidgets('el sello late solo con la conexión en vivo', (tester) async {
      final hub = FakeRealtimeHub();
      await pumpRealApp(tester, location: '/campaigns/c1', realtime: hub);

      expect(find.byKey(const Key('realtime-status')), findsOneWidget);
      final seal = find.byType(PulseSeal);
      expect(seal, findsOneWidget);
      expect(tester.widget<PulseSeal>(seal).active, isTrue);
      expect(find.descendant(of: seal, matching: find.byType(AppIcon)), findsOneWidget);

      hub.setStatus(RealtimeStatus.reconnecting);
      await tester.pumpAndSettle();
      expect(find.byType(PulseSeal), findsNothing);
      final still = tester.widget<AppIcon>(find.byKey(const Key('realtime-reconnecting')));
      expect(still.icon, AppIcons.seal);

      hub.setStatus(RealtimeStatus.connected);
      await tester.pumpAndSettle();
      expect(find.byType(PulseSeal), findsOneWidget);
    });

    testWidgets('las páginas de la campaña llevan grano y las tarjetas de General, glifos', (
      tester,
    ) async {
      await pumpRealApp(tester, location: '/campaigns/c1/general');

      expect(find.byType(GrainBackground), findsOneWidget);
      for (final section in CampaignSection.values) {
        if (section == CampaignSection.characters) continue;
        final card = find.byKey(Key('general-${section.path}'));
        expect(card, findsOneWidget, reason: section.path);
        final icon = find.descendant(of: card, matching: find.byType(AppIcon));
        expect(icon, findsOneWidget, reason: section.path);
        expect(tester.widget<AppIcon>(icon).icon, section.icon);
        expect(find.descendant(of: card, matching: find.byType(Icon)), findsNothing);
        expect(
          find.descendant(of: card, matching: find.byType(RuneCard)),
          findsOneWidget,
          reason: section.path,
        );
      }
      expect(
        find.descendant(of: find.byKey(const Key('nav-general')), matching: find.byType(AppIcon)),
        findsOneWidget,
      );
    });

    testWidgets('el aviso de mensaje nuevo lleva el sello', (tester) async {
      final (fakes: _, characters: _, :hub) = await _pumpPlayer(tester, location: '/campaigns/c1');

      hub.emit(const MessageReceived(campaignId: 'c1', characterId: 'ch1', entityId: 'm9'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('realtime-notice-message')), findsOneWidget);
      final icon = tester.widget<AppIcon>(find.byKey(const Key('realtime-notice-message-icon')));
      expect(icon.icon, AppIcons.seal);
    });
  });

  group('Mi sesión · combate', () {
    testWidgets('el daño sacude la tarjeta de PG y la curación la hace latir', (tester) async {
      await _pumpPlayer(tester);
      final shake = find.byKey(const Key('hp-shake'));
      final pulse = find.byKey(const Key('hp-pulse'));
      expect(find.byType(ShakeAndTint), findsOneWidget);
      expect(tester.widget<ShakeAndTint>(shake).trigger, isNull);
      expect(tester.widget<PulseTint>(pulse).trigger, isNull);

      // 3 temporales + 2: los PG bajan.
      await _damage(tester, 5);
      expect(tester.widget<ShakeAndTint>(shake).trigger, 1);
      expect(tester.widget<PulseTint>(pulse).trigger, isNull);

      await _damage(tester, 1, button: 'hp-plus');
      expect(tester.widget<PulseTint>(pulse).trigger, 1);
      expect(tester.widget<ShakeAndTint>(shake).trigger, 1);
    });

    testWidgets('solo los PG temporales no sacuden la tarjeta', (tester) async {
      await _pumpPlayer(tester);
      await _damage(tester, 2);
      expect(tester.widget<ShakeAndTint>(find.byKey(const Key('hp-shake'))).trigger, isNull);
    });

    testWidgets('a 0 PG la viñeta oscura cubre la sesión y las salvaciones son pips', (
      tester,
    ) async {
      await _pumpPlayer(tester);
      final vignette = find.byKey(const Key('player-vignette'));
      expect(tester.widget<DarkVignette>(vignette).active, isFalse);
      expect(find.byKey(const Key('death-saves')), findsNothing);

      await _damage(tester, 30);

      expect(tester.widget<DarkVignette>(vignette).active, isTrue);
      final saves = find.byKey(const Key('death-saves'));
      expect(saves, findsOneWidget);
      expect(find.descendant(of: saves, matching: find.byType(Pip)), findsNWidgets(6));

      await _tapKey(tester, 'death-success-0');
      final lit = tester
          .widgetList<Pip>(find.descendant(of: saves, matching: find.byType(Pip)))
          .where((p) => p.filled);
      expect(lit, hasLength(1));

      await _damage(tester, 5, button: 'hp-plus');
      expect(tester.widget<DarkVignette>(vignette).active, isFalse);
    });

    testWidgets('Furia y Ataque temerario encienden sus llamas', (tester) async {
      await _pumpPlayer(
        tester,
        character: makeCharacterJson(
          status: 'Active',
          classes: const [
            {'classIndex': 'barbarian', 'className': 'Barbarian', 'level': 3},
          ],
          combat: makeCombatJson(
            classPanels: [
              {
                'classIndex': 'barbarian',
                'level': 3,
                'data': {
                  'rageDamageBonus': 2,
                  'rageUses': {'max': 3, 'used': 0},
                  'recklessAttack': true,
                },
              },
            ],
          ),
        ),
      );
      final rage = find.byKey(const Key('rage-flames'));
      final reckless = find.byKey(const Key('reckless-flames'));
      expect(tester.widget<FlameBorder>(rage).active, isFalse);
      expect(tester.widget<FlameBorder>(reckless).active, isFalse);

      await _tapKey(tester, 'rage-start');
      expect(tester.widget<FlameBorder>(rage).active, isTrue);
      expect(find.descendant(of: rage, matching: find.byKey(const Key('rage-active'))), findsOne);

      await _tapKey(tester, 'reckless-toggle');
      expect(tester.widget<FlameBorder>(reckless).active, isTrue);

      // The next round ends the reckless attack; the rage keeps burning.
      await _tapKey(tester, 'rage-next-round');
      expect(tester.widget<FlameBorder>(reckless).active, isFalse);
      expect(tester.widget<FlameBorder>(rage).active, isTrue);

      await _tapKey(tester, 'rage-end');
      expect(tester.widget<FlameBorder>(rage).active, isFalse);
    });

    testWidgets('Castigo divino dispara el destello dorado', (tester) async {
      final (fakes: _, :characters, hub: _) = await _pumpPlayer(
        tester,
        character: makeCharacterJson(
          status: 'Active',
          classes: const [
            {'classIndex': 'paladin', 'className': 'Paladin', 'level': 5},
          ],
          combat: makeCombatJson(
            classPanels: [
              {
                'classIndex': 'paladin',
                'level': 5,
                'data': {
                  'layOnHands': {'pool': 25, 'used': 0},
                  'divineSmite': {
                    'slotsByLevel': [
                      {'level': 1, 'available': 2, 'extraDice': 2},
                    ],
                  },
                  'channelDivinity': {'max': 1, 'used': 0},
                },
              },
            ],
          ),
        ),
      );
      final flash = find.byKey(const Key('smite-flash'));
      expect(tester.widget<RadialFlash>(flash).trigger, isNull);

      await _tapKey(tester, 'smite-confirm');
      expect(characters.smites, [1]);
      expect(tester.widget<RadialFlash>(flash).trigger, 1);
    });

    testWidgets('la tarjeta de subir de nivel late con su glifo', (tester) async {
      await _pumpPlayer(
        tester,
        character: makeCharacterJson(
          status: 'Active',
          combat: makeCombatJson(),
          pendingLevelUpTo: 4,
        ),
      );
      // The forced level-up wizard covers the card; it is still there below.
      final seal = find.byKey(const Key('level-up-seal'), skipOffstage: false);
      expect(
        find.descendant(
          of: find.byKey(const Key('level-up-card'), skipOffstage: false),
          matching: seal,
          skipOffstage: false,
        ),
        findsOneWidget,
      );
      final icon = find.descendant(
        of: seal,
        matching: find.byType(AppIcon, skipOffstage: false),
        skipOffstage: false,
      );
      expect(tester.widget<AppIcon>(icon).icon, AppIcons.levelUp);
      expect(tester.widget<PulseSeal>(seal).active, isTrue);
    });
  });

  group('descansos aprobados', () {
    testWidgets('el jugador ve la luna al aprobarse su descanso largo', (tester) async {
      final (fakes: _, :characters, :hub) = await _pumpPlayer(tester);
      await _tapKey(tester, 'player-subview-detail');
      await _tapKey(tester, 'rest-request-long');
      await _tapKey(tester, 'confirm-action');
      expect(find.byKey(const Key('rest-pending')), findsOneWidget);
      expect(find.byType(MoonPass), findsNothing);

      characters.approvePendingRest('ch1', hitPointsCurrent: 28);
      hub.emit(const RestRequestUpdated(campaignId: 'c1', characterId: 'ch1', entityId: 'rr1'));

      expect(await _seenWithin(tester, find.byKey(const Key('rest-celebration-long'))), isTrue);
      expect(find.byType(CampfireBurst), findsNothing);
      await tester.pumpAndSettle();
      // The overlay removes itself when the animation ends.
      expect(find.byType(MoonPass), findsNothing);
      expect(find.text('Descanso aprobado'), findsOneWidget);
    });

    testWidgets('el DM ve la hoguera al aprobar un descanso corto', (tester) async {
      final restRequests = FakeRestRequestsRepository(requests: [makeRestRequest()]);
      final fakes = AppFakes(
        campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: CampaignRole.owner)]),
        restRequests: restRequests,
      );
      await pumpRealApp(tester, location: '/campaigns/c1/dm', fakes: fakes);

      final approve = find.byKey(const Key('rest-approve-rr1'));
      await tester.ensureVisible(approve);
      await tester.pumpAndSettle();
      await tester.tap(approve);

      expect(await _seenWithin(tester, find.byKey(const Key('rest-celebration-short'))), isTrue);
      await tester.pumpAndSettle();
      expect(restRequests.approved, ['rr1']);
      expect(find.byType(CampfireBurst), findsNothing);
    });
  });

  group('mensajes del DM', () {
    testWidgets('abrir un mensaje sin leer rompe su sello; uno leído no lo lleva', (tester) async {
      final messages = FakeMessagesRepository(
        messages: [
          makeMessage(),
          makeMessage(id: 'm2', body: 'Mensaje antiguo', read: true),
        ],
      );
      await _pumpPlayer(tester, messages: messages);
      await _tapKey(tester, 'player-subview-detail');

      final unread = find.byKey(const Key('message-m1'));
      final unreadIcon = find.descendant(of: unread, matching: find.byType(AppIcon));
      expect(tester.widget<AppIcon>(unreadIcon).icon, AppIcons.seal);

      await _tap(tester, unread);
      final seal = find.byKey(const Key('message-seal'));
      expect(seal, findsOneWidget);
      expect(tester.widget<SealBreak>(seal).broken, isTrue);
      expect(find.byKey(const Key('message-body-view')), findsOneWidget);
      await _tapKey(tester, 'message-close');

      await _tap(tester, find.byKey(const Key('message-m2')));
      expect(find.byType(SealBreak), findsNothing);
    });
  });

  group('animaciones sin reducir', () {
    final base = CharacterDetail.fromJson(
      makeCharacterJson(status: 'Active', temporaryHitPoints: 0, combat: makeCombatJson()),
    );
    CharacterDetail withHp(int hp) => CharacterDetail.fromJson(
      makeCharacterJson(
        status: 'Active',
        hitPointsCurrent: hp,
        temporaryHitPoints: 0,
        combat: makeCombatJson(),
      ),
    );

    testWidgets('la tarjeta de PG tiembla al bajar y vuelve a su sitio', (tester) async {
      final character = ValueNotifier(base);
      addTearDown(character.dispose);
      await tester.pumpWidget(
        _harness(
          ValueListenableBuilder<CharacterDetail>(
            valueListenable: character,
            builder: (_, c, _) =>
                SingleChildScrollView(child: HpCard(character: c, canEdit: false)),
          ),
        ),
      );
      expect(_shakeOffset(tester), 0);

      character.value = withHp(12);
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 30));
      expect(_shakeOffset(tester), isNot(0));

      await tester.pumpAndSettle();
      expect(_shakeOffset(tester), 0);

      // Healing does not shake.
      character.value = withHp(18);
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 30));
      expect(_shakeOffset(tester), 0);
      await tester.pumpAndSettle();
    });

    for (final reduced in [false, true]) {
      testWidgets('un pip que se gasta se tacha (${reduced ? 'reducidas' : 'todas'})', (
        tester,
      ) async {
        final filled = ValueNotifier(3);
        addTearDown(filled.dispose);
        await tester.pumpWidget(
          _harness(
            ValueListenableBuilder<int>(
              valueListenable: filled,
              builder: (_, n, _) => PipRow(total: 3, filled: n),
            ),
            reduced: reduced,
          ),
        );
        PipPainter painterOf(int index) =>
            tester
                    .widget<CustomPaint>(
                      find
                          .descendant(
                            of: find.byType(Pip).at(index),
                            matching: find.byType(CustomPaint),
                          )
                          .first,
                    )
                    .painter!
                as PipPainter;

        expect(painterOf(2).spending, isFalse);
        filled.value = 2;
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 80));
        expect(painterOf(2).spending, reduced ? isFalse : isTrue);
        expect(painterOf(1).spending, isFalse);

        await tester.pump(Pip.strokeDuration);
        expect(painterOf(2).spending, isFalse);
        expect(tester.widget<Pip>(find.byType(Pip).at(2)).filled, isFalse);
      });
    }

    testWidgets('la hoguera de un descanso cubre la pantalla y se retira sola', (tester) async {
      await tester.pumpWidget(
        _harness(
          Builder(
            builder: (context) => TextButton(
              key: const Key('play'),
              onPressed: () => showRestCelebration(context, RestKind.short),
              child: const Text('Descansar'),
            ),
          ),
        ),
      );
      await tester.tap(find.byKey(const Key('play')));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));
      expect(find.byKey(const Key('rest-celebration-short')), findsOneWidget);
      // Never blocks taps.
      await tester.tap(find.byKey(const Key('play')));

      await tester.pump(CampfireBurst.duration);
      await tester.pumpAndSettle();
      expect(find.byType(CampfireBurst), findsNothing);
    });
  });
}
