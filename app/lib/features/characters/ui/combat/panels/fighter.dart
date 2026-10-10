import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/theme/icons.dart';
import '../../../../../core/ui/action_type.dart';

import '../../../../dice/data/dice_controller.dart';
import '../../../../dice/domain/dice_expression.dart';
import '../../../data/characters_controller.dart';
import '../../../data/models.dart';
import '../../../domain/combat_math.dart';
import '../combat_support.dart';
import 'panel_support.dart';
import '../../../../../systems/dnd5e/characters/dnd5e_characters_controller.dart';

/// Attacks per Attack action by fighter level (SRD Extra Attack).
int fighterAttacks(int level) => level >= 20
    ? 4
    : level >= 11
    ? 3
    : level >= 5
    ? 2
    : 1;

/// Fighter: Second Wind (heals `1d10 + level`), Action Surge and Indomitable.
class FighterPanel extends ConsumerWidget {
  const FighterPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  /// Spends Second Wind, rolls `1d10 + level` (recorded in the dice history)
  /// and heals that much through the combat PATCH.
  Future<void> _secondWind(BuildContext context, WidgetRef ref, CharacterResource resource) async {
    final container = ProviderScope.containerOf(context, listen: false);
    final c = panel.character;
    if (remainingOf(resource) <= 0) {
      showCombatMessage(context, 'No quedan usos de Segundo aliento.');
      return;
    }
    final roll = DiceExpression.parse('1d10+${panel.level}')
        .roll(container.read(diceRandomProvider));
    var healed = 0;
    final done = await runCombat(context, () async {
      final controller = container.read(dnd5eCharacterControllerProvider(c.id).notifier);
      await controller.spendResource(resource.id);
      final current = container.read(characterControllerProvider(c.id)).value ?? c;
      final next = applyHealing(
        hp: current.hitPointsCurrent,
        max: current.sheet.hitPointsMax,
        amount: roll.total,
      );
      healed = next - current.hitPointsCurrent;
      final revived = current.hitPointsCurrent == 0 && next > 0;
      await controller.patchCombat(
        CombatPatch(
          hitPointsCurrent: next,
          deathSaveSuccesses: revived && current.deathSaveSuccesses != 0 ? 0 : null,
          deathSaveFailures: revived && current.deathSaveFailures != 0 ? 0 : null,
        ),
      );
    });
    if (!done) return;
    container.read(diceControllerProvider.notifier).record(roll, label: 'Segundo aliento');
    if (context.mounted) {
      showCombatMessage(
        context,
        'Segundo aliento: ${roll.total} (${roll.breakdown}). Recuperas $healed PG.',
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final level = panel.level;
    final attacks = fighterAttacks(level);
    return ClassPanelFrame(
      panel: panel,
      children: [
        ClassResourceActionCard(
          panel: panel,
          resourceKey: 'second-wind',
          title: 'Segundo aliento',
          featureIndex: 'second-wind',
          // SRD: "On your turn, you can use a bonus action to regain hit points".
          actionKind: ActionKind.bonusAction,
          actionKey: 'fighter-second-wind',
          buttonLabel: 'Recuperar 1d10 + $level PG',
          icon: AppIcons.heart,
          onUse: _secondWind,
          description: 'Acción adicional: recuperas 1d10 + tu nivel de guerrero en PG.',
        ),
        ClassResourceActionCard(
          panel: panel,
          resourceKey: 'action-surge',
          title: 'Oleada de acción',
          featureIndex: 'action-surge-1-use',
          actionKey: 'fighter-action-surge',
          buttonLabel: 'Oleada de acción',
          icon: AppIcons.bolt,
          minLevel: 2,
          success: 'Oleada de acción: tienes una acción adicional este turno.',
          description: 'Una acción más en tu turno (no otra Oleada en el mismo turno).',
        ),
        ClassResourceActionCard(
          panel: panel,
          resourceKey: 'indomitable',
          title: 'Indomable',
          featureIndex: 'indomitable-1-use',
          actionKey: 'fighter-indomitable',
          buttonLabel: 'Repetir salvación',
          icon: AppIcons.d20,
          minLevel: 9,
          success: 'Indomable: repite la salvación fallida.',
          description: 'Repites una salvación fallida y te quedas con la nueva tirada.',
        ),
        if (attacks > 1)
          CombatCard(
            child: PanelFact(
              key: const Key('fighter-extra-attack'),
              label: 'Ataque adicional',
              value: '$attacks ataques por acción de Atacar',
            ),
          ),
      ],
    );
  }
}
