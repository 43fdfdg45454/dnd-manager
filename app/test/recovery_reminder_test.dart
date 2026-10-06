import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/characters/ui/combat/recovery_reminder.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/character_fakes.dart';
import 'helpers/fakes.dart';

Map<String, dynamic> _wizardJson({List<Map<String, dynamic>>? resources, bool used = false}) =>
    makeCharacterJson(
      status: 'Active',
      classes: const [
        {'classIndex': 'wizard', 'className': 'Wizard', 'level': 4},
      ],
      combat: makeCombatJson(
        spellSlots: [
          {'level': 1, 'max': 4, 'used': 3},
          {'level': 2, 'max': 3, 'used': 1},
        ],
        resources: resources ?? const [],
        classPanels: [
          {
            'classIndex': 'wizard',
            'level': 4,
            'data': {
              'arcaneRecovery': {'used': used, 'slotLevelsRecoverable': 2},
            },
          },
        ],
      ),
    );

Future<FakeCharactersRepository> _pump(WidgetTester tester, Map<String, dynamic> json) async {
  final repo = FakeCharactersRepository(characters: [json]);
  final character = CharacterDetail.fromJson(json);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authControllerProvider.overrideWith(() => FixedAuthController(AuthSignedIn(makeUser()))),
        charactersRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp(
        home: Scaffold(
          body: Consumer(
            builder: (context, ref, _) => TextButton(
              key: const Key('go'),
              onPressed: () => showRecoveryReminder(context, ref, character),
              child: const Text('go'),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.byKey(const Key('go')));
  await tester.pumpAndSettle();
  return repo;
}

void main() {
  group('recordatorio de Recuperación arcana', () {
    testWidgets('con usos restantes avisa y abre el selector de espacios', (tester) async {
      final repo = await _pump(
        tester,
        _wizardJson(
          resources: [
            {
              'id': 'r1',
              'key': 'arcane-recovery',
              'name': 'Arcane Recovery',
              'max': 1,
              'used': 0,
              'recharge': 'LongRest',
              'isAuto': true,
            },
          ],
        ),
      );
      expect(find.byKey(const Key('recovery-reminder')), findsOneWidget);
      expect(find.text('¿Usar Recuperación arcana?'), findsOneWidget);

      await tester.tap(find.text('Usar'));
      await tester.pumpAndSettle();
      expect(find.text('Niveles seleccionados: 0 / 2'), findsOneWidget);
      await tester.tap(find.byKey(const Key('arcane-level-2-plus')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('arcane-confirm')));
      await tester.pumpAndSettle();
      expect(repo.classActions.single.action, 'arcane-recovery');
      expect(repo.classActions.single.body, {
        'slotLevels': [2],
      });
    });

    testWidgets('sin recurso (panel sin Recuperación arcana) no avisa', (tester) async {
      await _pump(
        tester,
        makeCharacterJson(
          status: 'Active',
          combat: makeCombatJson(
            spellSlots: [
              {'level': 1, 'max': 4, 'used': 3},
            ],
          ),
        ),
      );
      expect(find.byKey(const Key('recovery-reminder')), findsNothing);
    });

    testWidgets('con el recurso agotado no avisa', (tester) async {
      await _pump(
        tester,
        _wizardJson(
          resources: [
            {
              'id': 'r1',
              'key': 'arcane-recovery',
              'name': 'Arcane Recovery',
              'max': 1,
              'used': 1,
              'recharge': 'LongRest',
              'isAuto': true,
            },
          ],
        ),
      );
      expect(find.byKey(const Key('recovery-reminder')), findsNothing);
    });
  });
}
