import 'package:flutter/material.dart' hide Page;
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/theme/app_theme.dart';
import 'package:opentrpg_dnd5e/characters/models.dart';
import 'package:opentrpg_dnd5e/characters/ui/character_tabs.dart';

import 'helpers/app_pump.dart';
import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

/// Sheet rules of the content packs (format 3): defenses, class DCs, size and
/// speeds, passives and cantrip DCs, each with where it comes from.
CharacterDetail _character({
  Map<String, dynamic> sheetExtra = const {},
  Map<String, dynamic> breakdowns = const {},
  List<Map<String, dynamic>> resistances = const [],
}) => CharacterDetail.fromJson(
  makeCharacterJson(
    status: 'Active',
    sheetExtra: sheetExtra,
    breakdowns: breakdowns,
    resistances: resistances,
  ),
);

Future<void> _pumpTab(WidgetTester tester, Widget tab) async {
  tester.view.physicalSize = const Size(800, 5000);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.light(),
      home: Scaffold(body: tab),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

void main() {
  group('Defensas', () {
    testWidgets('resistencias, inmunidades, estados y salvaciones perdidas con su origen', (
      tester,
    ) async {
      final c = _character(
        resistances: [
          {'damageType': 'cold', 'label': 'Escamado'},
        ],
        sheetExtra: {
          'immunities': [
            {'type': 'poison', 'source': 'race', 'label': 'Escamado'},
          ],
          'conditionImmunities': [
            {'type': 'charmed', 'source': 'feature', 'label': 'Mente firme (nivel 3)'},
          ],
          'lostSavingThrows': [
            {'type': 'wis', 'source': 'feat', 'label': 'Renuncia (dote inicial)'},
          ],
          'lostSkills': [
            {'type': 'athletics', 'source': 'race', 'label': 'Pétreo'},
          ],
        },
      );
      expect(c.sheet.lostSkills.single.type, 'athletics');
      expect(c.sheet.immunities.single.type, 'poison');
      expect(c.sheet.lostSavingThrows.single.label, 'Renuncia (dote inicial)');

      await _pumpTab(tester, SummaryTab(character: c));

      expect(find.byKey(const Key('defenses')), findsOneWidget);
      expect(find.text('Defensas'), findsOneWidget);
      expect(find.byKey(const Key('resistance-cold')), findsOneWidget);
      expect(find.byKey(const Key('immunity-poison')), findsOneWidget);
      expect(find.text('veneno · Escamado'), findsOneWidget);
      expect(find.byKey(const Key('condition-immunity-charmed')), findsOneWidget);
      expect(find.text('hechizado · Mente firme (nivel 3)'), findsOneWidget);
      expect(find.byKey(const Key('lost-save-wis')), findsOneWidget);
      expect(find.textContaining('Renuncia (dote inicial)'), findsOneWidget);
      expect(find.byKey(const Key('lost-skills')), findsOneWidget);
      expect(find.byKey(const Key('lost-skill-athletics')), findsOneWidget);
      expect(find.textContaining('Pétreo'), findsOneWidget);
    });

    testWidgets('sin defensas no hay sección', (tester) async {
      await _pumpTab(tester, SummaryTab(character: _character()));
      expect(find.byKey(const Key('defenses')), findsNothing);
      expect(find.byKey(const Key('summary-size')), findsNothing);
    });
  });

  group('Resumen', () {
    testWidgets('CD de clase, tamaño y velocidades extra se explican al tocarlas', (tester) async {
      final c = _character(
        sheetExtra: {
          'classSaveDcs': [
            {
              'classIndex': 'vigia',
              'name': 'CD de vigilancia',
              'ability': 'wis',
              'dc': 12,
              'breakdownKey': 'classSaveDc.vigia.0',
            },
          ],
          'size': 'Small',
          'sizeSource': 'Escamado',
          'speeds': {'swim': 30, 'climb': 20},
        },
        breakdowns: {
          'classSaveDc.vigia.0': makeBreakdownJson([
            ('base', 'Base', 8),
            ('proficiency', 'Competencia', 2),
            ('ability', 'Sabiduría', 1),
            ('feature', 'Ojo atento (nivel 3)', 1),
          ]),
          'speed.swim': makeBreakdownJson([('race', 'Escamado', 30)]),
        },
      );
      await _pumpTab(tester, SummaryTab(character: c));

      expect(find.text('CD de clase'), findsOneWidget);
      expect(
        tester.widget<Text>(find.byKey(const Key('class-save-dc-classSaveDc.vigia.0'))).data,
        'CD 12',
      );
      expect(find.text('Tamaño: Pequeño · Escamado', findRichText: true), findsOneWidget);
      expect(find.byKey(const Key('tile-speed-swim')), findsOneWidget);
      expect(find.byKey(const Key('tile-speed-climb')), findsOneWidget);
      expect(find.text('Nado'), findsOneWidget);
      expect(find.text('Trepar'), findsOneWidget);

      await _tap(tester, find.byKey(const Key('class-save-dc-classSaveDc.vigia.0')));
      final sheet = find.byKey(const Key('breakdown-sheet'));
      expect(sheet, findsOneWidget);
      expect(
        find.descendant(of: sheet, matching: find.text('Ojo atento (nivel 3)')),
        findsOneWidget,
      );
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '12');
    });
  });

  group('Habilidades', () {
    testWidgets('las tres pasivas se muestran y se explican', (tester) async {
      final c = _character(
        sheetExtra: {'passiveInvestigation': 12, 'passiveInsight': 16},
        breakdowns: {
          'passiveInsight': makeBreakdownJson([
            ('base', 'Base', 10),
            ('ability', 'Sabiduría', 1),
            ('feat', 'Oído fino (dote inicial)', 5),
          ]),
        },
      );
      await _pumpTab(tester, SkillsTab(character: c));

      expect(find.byKey(const Key('passives')), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('passive-passivePerception'))).data, '11');
      expect(tester.widget<Text>(find.byKey(const Key('passive-passiveInvestigation'))).data, '12');
      expect(tester.widget<Text>(find.byKey(const Key('passive-passiveInsight'))).data, '16');

      await _tap(tester, find.byKey(const Key('passive-passiveInsight')));
      expect(find.text('Oído fino (dote inicial)'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '16');
    });
  });

  group('Hechizos', () {
    testWidgets('los conjuros concedidos van en su sección y la característica cambiada se marca', (
      tester,
    ) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(
            status: 'Active',
            classes: [
              {'classIndex': 'wizard', 'className': 'Wizard', 'level': 3},
            ],
            spellcasting: [
              {
                'classIndex': 'wizard',
                'ability': 'wis',
                'saveDc': 13,
                'attackBonus': 5,
                'abilityOverridden': true,
              },
              {
                'classIndex': 'brio-ejemplo-chispa',
                'name': 'Chispa de ejemplo',
                'ability': 'int',
                'saveDc': 13,
                'attackBonus': 5,
              },
            ],
            overrides: [
              {'field': 'spellcastingAbility.wizard', 'value': 5, 'note': 'Regla de mesa'},
            ],
            spells: [
              {
                'spellIndex': 'magic-missile',
                'classIndex': 'brio-ejemplo-chispa',
                'name': 'Magic Missile',
                'level': 1,
                'alwaysPrepared': true,
              },
              {'spellIndex': 'fire-bolt', 'classIndex': 'wizard', 'name': 'Fire Bolt', 'level': 0},
            ],
            resources: [
              {
                'id': 'r1',
                'key': 'brio-ejemplo-chispa.magic-missile',
                'name': 'Magic Missile',
                'max': 1,
                'used': 0,
                'recharge': 'LongRest',
              },
            ],
          ),
        ],
      );
      await pumpRealApp(
        tester,
        location: '/characters/ch1',
        fakes: AppFakes(
          campaigns: FakeCampaignsRepository(campaigns: [makeCampaign()]),
          characters: repository,
        ),
      );
      await openDetailTab(tester, 'tab-spells');

      final section = find.byKey(const Key('spells-granted-brio-ejemplo-chispa'));
      expect(section, findsOneWidget);
      expect(
        find.descendant(of: section, matching: find.text('Chispa de ejemplo: ')),
        findsOneWidget,
      );
      expect(find.byKey(const Key('spell-brio-ejemplo-chispa-magic-missile')), findsOneWidget);
      expect(find.textContaining('1 de 1 uso por descanso largo'), findsOneWidget);
      // The granted spell is not repeated in the level lists.
      expect(find.byKey(const Key('spell-magic-missile')), findsNothing);
      expect(find.byKey(const Key('spell-fire-bolt')), findsOneWidget);

      expect(find.byKey(const Key('sheet-spell-ability-wizard')), findsOneWidget);
      expect(find.text(' · Sabiduría'), findsOneWidget);
      expect(find.byKey(const Key('override-spellcastingAbility.wizard')), findsOneWidget);
    });

    testWidgets('CD y ataque propios de los trucos y rango de crítico de conjuro', (tester) async {
      final repository = FakeCharactersRepository(
        characters: [
          makeCharacterJson(
            status: 'Active',
            classes: [
              {'classIndex': 'sorcerer', 'className': 'Sorcerer', 'level': 3},
            ],
            spellcasting: [
              {
                'classIndex': 'sorcerer',
                'ability': 'cha',
                'saveDc': 11,
                'attackBonus': 3,
                'cantripSaveDc': 13,
                'cantripAttackBonus': 5,
                'criticalRange': 19,
                'criticalRangeSource': 'Chispa (nivel 3)',
              },
            ],
            breakdowns: {
              'spellSaveDc.sorcerer.cantrip': makeBreakdownJson([
                ('base', 'Base', 8),
                ('proficiency', 'Competencia', 2),
                ('ability', 'Carisma', 1),
                ('feature', 'Chispa (nivel 3)', 2),
              ]),
            },
          ),
        ],
      );
      await pumpRealApp(
        tester,
        location: '/characters/ch1',
        fakes: AppFakes(
          campaigns: FakeCampaignsRepository(campaigns: [makeCampaign()]),
          characters: repository,
        ),
      );
      await openDetailTab(tester, 'tab-spells');

      expect(tester.widget<Text>(find.byKey(const Key('sheet-cantrip-dc-sorcerer'))).data, '13');
      expect(
        tester.widget<Text>(find.byKey(const Key('sheet-cantrip-attack-sorcerer'))).data,
        '+5',
      );
      expect(find.byKey(const Key('sheet-spell-crit-sorcerer')), findsOneWidget);

      await _tap(tester, find.byKey(const Key('sheet-cantrip-dc-sorcerer')));
      expect(find.text('Chispa (nivel 3)'), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('breakdown-total'))).data, '13');
    });
  });
}
