import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/theme/app_icon.dart';
import '../../../../../core/theme/icons.dart';
import '../../../../../core/ui/action_type.dart';

import '../../../../dice/domain/dice_expression.dart' show bonusSuffix;
import 'critical_damage_roll.dart';
import '../combat_support.dart';
import 'panel_support.dart';
import '../../../../../systems/dnd5e/characters/models.dart';

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
/// actions, and the Martial Arts die.
class MonkPanel extends ConsumerWidget {
  const MonkPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final c = panel.character;
    final level = panel.level;
    final sheet = c.sheet;
    final ki = findResource(c, 'ki');
    final remaining = remainingOf(ki);
    final dc = 8 + sheet.proficiencyBonus + sheet.ability('wis').modifier;
    final die = martialArtsDie(level);
    final strMod = sheet.ability('str').modifier;
    final dexMod = sheet.ability('dex').modifier;
    final attackMod = strMod > dexMod ? strMod : dexMod;
    final movement = unarmoredMovement(level);

    Widget kiButton(String key, String label, AppIcons icon, String success) =>
        FilledButton.tonalIcon(
          key: Key(key),
          onPressed: panel.canEdit && ki != null && remaining > 0
              ? () => spendClassResource(context, ref, c, ki, success: success)
              : null,
          icon: AppIcon(icon, size: 20),
          label: Text(label),
        );

    return ClassPanelFrame(
      panel: panel,
      children: [
        CombatCard(
          title: 'Ki',
          featureIndex: 'ki',
          // SRD: Flurry of Blows, Patient Defense and Step of the Wind are each
          // taken "as a bonus action".
          actionKind: level >= 2 ? ActionKind.bonusAction : null,
          trailing: level >= 2 ? Text('CD $dc', key: const Key('monk-ki-dc')) : null,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              ClassResourceUses(
                panel: panel,
                resource: ki,
                keyPrefix: 'monk-ki',
                label: 'Puntos de ki',
                minLevel: 2,
                tapToSpend: false,
              ),
              if (level >= 2) ...[
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    kiButton(
                      'monk-flurry-of-blows',
                      'Ráfaga de golpes',
                      AppIcons.monk,
                      'Ráfaga de golpes: dos ataques sin armas como acción adicional.',
                    ),
                    kiButton(
                      'monk-patient-defense',
                      'Defensa paciente',
                      AppIcons.shield,
                      'Defensa paciente: Esquivar como acción adicional.',
                    ),
                    kiButton(
                      'monk-step-of-the-wind',
                      'Paso del viento',
                      AppIcons.bolt,
                      'Paso del viento: Destrabarse o Correr como acción adicional.',
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                Text('Cada acción gasta 1 punto de ki.', style: theme.textTheme.bodySmall),
              ],
            ],
          ),
        ),
        CombatCard(
          title: 'Artes marciales',
          featureIndex: 'martial-arts',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              CriticalDamageRoll(
                expression: '1$die${bonusSuffix(attackMod)}',
                label: 'Artes marciales',
                keyPrefix: 'monk-martial-arts',
              ),
              const SizedBox(height: 4),
              PanelFact(
                key: const Key('monk-martial-arts'),
                label: 'Dado de artes marciales',
                value: die,
                detail: 'Golpe sin armas o arma de monje; ataque sin armas extra como acción adicional.',
              ),
              if (movement > 0)
                PanelFact(
                  key: const Key('monk-movement'),
                  label: 'Movimiento sin armadura',
                  value: '+$movement pies',
                ),
              if (level >= 5)
                const PanelFact(
                  key: Key('monk-stunning-strike'),
                  label: 'Golpe aturdidor',
                  value: '1 punto de ki al impactar',
                  detail: 'El objetivo hace una salvación de Constitución o queda aturdido.',
                ),
            ],
          ),
        ),
        if (level >= 3)
          CombatCard(
            // No featureIndex: it groups several features (no SRD entry of its own).
            title: 'Reacciones',
            child: Column(
              children: [
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
              ],
            ),
          ),
      ],
    );
  }
}
