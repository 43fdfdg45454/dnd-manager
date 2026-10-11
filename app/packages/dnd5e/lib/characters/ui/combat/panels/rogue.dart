import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/icons.dart';

import '../../../../ui/action_type.dart';

import '../combat_rows.dart';
import '../combat_support.dart';
import 'critical_damage_roll.dart';
import 'panel_support.dart';

/// Sneak Attack dice (d6) by rogue level: half the level, rounded up.
int sneakAttackDice(int level) => (level + 1) ~/ 2;

/// Rogue: Sneak Attack with its roll, Cunning Action and Uncanny Dodge on
/// the turn; Evasion to remember.
const roguePanel = ClassPanelSpec(actions: _actions, more: _more);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) {
  final level = panel.level;
  final dice = '${sneakAttackDice(level)}d6';
  return [
    CombatActionRow(
      icon: AppIcons.hood,
      title: 'Ataque furtivo',
      cost: dice,
      costKey: const Key('rogue-sneak-attack'),
      subtitle:
          'Una vez por turno, con un arma sutil o a distancia, si tienes ventaja o un '
          'aliado está a 5 pies del objetivo (y no tienes desventaja).',
      info: const FeatureInfoButton(index: 'sneak-attack'),
      details: [
        CriticalDamageRoll(
          expression: dice,
          label: 'Ataque furtivo',
          keyPrefix: 'rogue-sneak-attack',
        ),
      ],
    ),
    if (level >= 2)
      const FeatureReminder(
        key: Key('rogue-cunning-action'),
        icon: AppIcons.hood,
        title: 'Acción astuta',
        featureIndex: 'cunning-action',
        // SRD: "you can take a bonus action on each of your turns in combat".
        actionKind: ActionKind.bonusAction,
        text: 'Correr, Destrabarse u Ocultarse como acción adicional.',
      ),
    if (level >= 5)
      const FeatureReminder(
        key: Key('rogue-uncanny-dodge'),
        icon: AppIcons.shield,
        title: 'Esquiva asombrosa',
        featureIndex: 'uncanny-dodge',
        // SRD: "you can use your reaction to halve the attack's damage".
        actionKind: ActionKind.reaction,
        text: 'Con tu reacción, reduces a la mitad el daño de un ataque que veas.',
      ),
  ];
}

List<Widget> _more(ClassPanelContext panel, WidgetRef ref) => [
  if (panel.level >= 7)
    const FeatureReminder(
      key: Key('rogue-evasion'),
      icon: AppIcons.bolt,
      title: 'Evasión',
      featureIndex: 'rogue-evasion',
      text: 'Salvación de Destreza superada: sin daño; fallada: la mitad.',
    ),
];
