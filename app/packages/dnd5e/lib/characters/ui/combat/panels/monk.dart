import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart' show bonusSuffix;

import '../../../../ui/action_type.dart';
import '../../../models.dart';
import '../combat_rows.dart';
import '../combat_support.dart';
import 'critical_damage_roll.dart';
import 'panel_support.dart';

/// Martial Arts die by monk level (SRD): d4, d6 at 5, d8 at 11, d10 at 17.
String martialArtsDie(int level) => level >= 17
    ? 'd10'
    : level >= 11
    ? 'd8'
    : level >= 5
    ? 'd6'
    : 'd4';

/// Unarmored Movement bonus in feet by monk level (0 before level 2).
int unarmoredMovement(int level) => level >= 18
    ? 30
    : level >= 14
    ? 25
    : level >= 10
    ? 20
    : level >= 6
    ? 15
    : level >= 2
    ? 10
    : 0;

/// Monk: ki points (resource `ki`, from level 2) with the three basic ki
/// actions, Martial Arts with its roll, Stunning Strike and the reactions;
/// Unarmored Movement to remember.
const monkPanel = ClassPanelSpec(actions: _actions, more: _more);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) {
  final c = panel.character;
  final level = panel.level;
  final sheet = c.sheet;
  final ki = findResource(c, 'ki');
  final dc = 8 + sheet.proficiencyBonus + sheet.ability('wis').modifier;
  final die = martialArtsDie(level);
  final strMod = sheet.ability('str').modifier;
  final dexMod = sheet.ability('dex').modifier;
  final attackMod = strMod > dexMod ? strMod : dexMod;
  return [
    ClassResourceCountRow(
      panel: panel,
      resource: ki,
      keyPrefix: 'monk-ki',
      title: 'Ki',
      label: 'Puntos de ki',
      icon: AppIcons.monk,
      minLevel: 2,
      subtitle: 'Cada acción de ki gasta 1 punto. CD de salvación $dc.',
      info: const FeatureInfoButton(index: 'ki'),
      trailing: level >= 2 ? Text('CD $dc', key: const Key('monk-ki-dc')) : null,
    ),
    if (level >= 2) ...[
      // SRD: Flurry of Blows, Patient Defense and Step of the Wind are each
      // taken "as a bonus action".
      _KiActionRow(
        panel: panel,
        ki: ki,
        actionKey: 'monk-flurry-of-blows',
        title: 'Ráfaga de golpes',
        icon: AppIcons.monk,
        subtitle: 'Tras atacar, dos golpes sin armas.',
        success: 'Ráfaga de golpes: dos ataques sin armas como acción adicional.',
      ),
      _KiActionRow(
        panel: panel,
        ki: ki,
        actionKey: 'monk-patient-defense',
        title: 'Defensa paciente',
        icon: AppIcons.shield,
        subtitle: 'Esquivar.',
        success: 'Defensa paciente: Esquivar como acción adicional.',
      ),
      _KiActionRow(
        panel: panel,
        ki: ki,
        actionKey: 'monk-step-of-the-wind',
        title: 'Paso del viento',
        icon: AppIcons.bolt,
        subtitle: 'Destrabarse o Correr; tu salto se duplica.',
        success: 'Paso del viento: Destrabarse o Correr como acción adicional.',
      ),
    ],
    CombatActionRow(
      icon: AppIcons.monk,
      title: 'Artes marciales',
      cost: die,
      subtitle: 'Golpe sin armas o arma de monje; ataque sin armas extra como acción adicional.',
      info: const FeatureInfoButton(index: 'martial-arts'),
      details: [
        PanelFact(
          key: const Key('monk-martial-arts'),
          label: 'Dado de artes marciales',
          value: die,
        ),
        CriticalDamageRoll(
          expression: '1$die${bonusSuffix(attackMod)}',
          label: 'Artes marciales',
          keyPrefix: 'monk-martial-arts',
        ),
      ],
    ),
    if (level >= 5)
      const FeatureReminder(
        key: Key('monk-stunning-strike'),
        icon: AppIcons.bolt,
        title: 'Golpe aturdidor',
        featureIndex: 'stunning-strike',
        text:
            'Golpe aturdidor: 1 punto de ki al impactar. El objetivo hace una salvación de '
            'Constitución o queda aturdido.',
      ),
    if (level >= 3)
      // SRD: "you can use your reaction to deflect or catch the missile".
      FeatureReminder(
        key: const Key('monk-deflect-missiles'),
        icon: AppIcons.shield,
        title: 'Desviar proyectiles',
        featureIndex: 'deflect-missiles',
        actionKind: ActionKind.reaction,
        text:
            'Cuando te impacta un ataque con arma a distancia, reduces el daño en '
            '1d10 + tu modificador de Destreza + $level.',
      ),
    // SRD: "you can use your reaction when you fall to reduce any falling
    // damage you take by an amount equal to five times your monk level".
    if (level >= 4)
      FeatureReminder(
        key: const Key('monk-slow-fall'),
        icon: AppIcons.bolt,
        title: 'Caída lenta',
        featureIndex: 'slow-fall',
        actionKind: ActionKind.reaction,
        text: 'Al caer, reduces el daño por caída en ${5 * level}.',
      ),
  ];
}

List<Widget> _more(ClassPanelContext panel, WidgetRef ref) {
  final movement = unarmoredMovement(panel.level);
  return [
    if (movement > 0)
      FeatureReminder(
        key: const Key('monk-movement'),
        icon: AppIcons.bolt,
        title: 'Movimiento sin armadura',
        featureIndex: 'unarmored-movement-1',
        text: 'Movimiento sin armadura: +$movement pies',
      ),
  ];
}

/// A ki action: one point of ki, as a bonus action.
class _KiActionRow extends ConsumerWidget {
  const _KiActionRow({
    required this.panel,
    required this.ki,
    required this.actionKey,
    required this.title,
    required this.icon,
    required this.subtitle,
    required this.success,
  });

  final ClassPanelContext panel;
  final CharacterResource? ki;
  final String actionKey;
  final String title;
  final AppIcons icon;
  final String subtitle;
  final String success;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ki = this.ki;
    return CombatActionRow(
      icon: icon,
      title: title,
      actionKind: ActionKind.bonusAction,
      cost: '1 ki',
      subtitle: subtitle,
      action: FilledButton.tonalIcon(
        key: Key(actionKey),
        onPressed: panel.canEdit && ki != null && remainingOf(ki) > 0
            ? () => spendClassResource(context, ref, panel.character, ki, success: success)
            : null,
        icon: AppIcon(icon, size: 18),
        label: const Text('Usar'),
      ),
    );
  }
}
