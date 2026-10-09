import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/theme/icons.dart';

import '../combat_support.dart';
import 'critical_damage_roll.dart';
import 'panel_support.dart';

/// Sneak Attack dice (d6) by rogue level: half the level, rounded up.
int sneakAttackDice(int level) => (level + 1) ~/ 2;

/// Rogue: Sneak Attack with its roll, and reminders of Cunning Action,
/// Uncanny Dodge and Evasion.
class RoguePanel extends ConsumerWidget {
  const RoguePanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final level = panel.level;
    final dice = '${sneakAttackDice(level)}d6';
    return ClassPanelFrame(
      panel: panel,
      children: [
        CombatCard(
          title: 'Ataque furtivo',
          trailing: Text(
            dice,
            key: const Key('rogue-sneak-attack'),
            style: theme.textTheme.titleLarge,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Una vez por turno, con un arma sutil o a distancia, si tienes ventaja o un '
                'aliado está a 5 pies del objetivo (y no tienes desventaja).',
                style: theme.textTheme.bodySmall,
              ),
              const SizedBox(height: 8),
              CriticalDamageRoll(
                expression: dice,
                label: 'Ataque furtivo',
                keyPrefix: 'rogue-sneak-attack',
              ),
            ],
          ),
        ),
        if (level >= 2)
          CombatCard(
            title: 'Recordatorios',
            child: Column(
              children: [
                const FeatureReminder(
                  key: Key('rogue-cunning-action'),
                  icon: AppIcons.hood,
                  title: 'Acción astuta',
                  text: 'Correr, Destrabarse u Ocultarse como acción adicional.',
                ),
                if (level >= 5)
                  const FeatureReminder(
                    key: Key('rogue-uncanny-dodge'),
                    icon: AppIcons.shield,
                    title: 'Esquiva asombrosa',
                    text: 'Con tu reacción, reduces a la mitad el daño de un ataque que veas.',
                  ),
                if (level >= 7)
                  const FeatureReminder(
                    key: Key('rogue-evasion'),
                    icon: AppIcons.bolt,
                    title: 'Evasión',
                    text: 'Salvación de Destreza superada: sin daño; fallada: la mitad.',
                  ),
              ],
            ),
          ),
      ],
    );
  }
}
