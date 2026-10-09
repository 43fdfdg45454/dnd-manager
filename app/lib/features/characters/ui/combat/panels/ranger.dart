import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/theme/app_icon.dart';
import '../../../../../core/theme/icons.dart';

import '../combat_support.dart';
import '../concentration_flow.dart';
import 'critical_damage_roll.dart';
import 'panel_support.dart';

/// Spell index of Hunter's Mark in the SRD catalog.
const huntersMarkIndex = 'hunters-mark';

/// Favored enemies by ranger level (SRD): 1, 2 at 6, 3 at 14.
int favoredEnemies(int level) => level >= 14
    ? 3
    : level >= 6
    ? 2
    : 1;

/// Favored terrains by ranger level (SRD): 1, 2 at 6, 3 at 10.
int favoredTerrains(int level) => level >= 10
    ? 3
    : level >= 6
    ? 2
    : 1;

/// Ranger: Favored Enemy and Natural Explorer reminders and a Hunter's Mark
/// button that sets the concentration.
class RangerPanel extends ConsumerWidget {
  const RangerPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final c = panel.character;
    final level = panel.level;
    final marking = c.concentratingOnSpellIndex == huntersMarkIndex;
    final enemies = favoredEnemies(level);
    final terrains = favoredTerrains(level);

    return ClassPanelFrame(
      panel: panel,
      children: [
        CombatCard(
          title: 'Marca del cazador',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (level < 2)
                Text(
                  'Marca del cazador: se obtiene a nivel 2 (lanzamiento de conjuros).',
                  key: const Key('ranger-hunters-mark-locked'),
                  style: theme.textTheme.bodySmall,
                )
              else
                SizedBox(
                  width: double.infinity,
                  child: FilledButton.tonalIcon(
                    key: const Key('ranger-hunters-mark'),
                    onPressed: panel.canEdit && !marking
                        ? () async {
                            if (!await confirmReplaceConcentration(
                              context,
                              ref,
                              c,
                              huntersMarkIndex,
                            )) {
                              return;
                            }
                            if (!context.mounted) return;
                            await runCombat(
                              context,
                              () => panelController(ref, c).setConcentration(huntersMarkIndex),
                              success: 'Concentración: Marca del cazador.',
                            );
                          }
                        : null,
                    icon: const AppIcon(AppIcons.bow, size: 20),
                    label: Text(marking ? 'Marca del cazador (activa)' : 'Marca del cazador'),
                  ),
                ),
              const SizedBox(height: 6),
              Text(
                '+1d6 de daño a la criatura marcada y ventaja para rastrearla. Requiere '
                'concentración; gasta el espacio en "Espacios de conjuro".',
                style: theme.textTheme.bodySmall,
              ),
              const SizedBox(height: 8),
              const CriticalDamageRoll(
                expression: '1d6',
                label: 'Marca del cazador',
                keyPrefix: 'ranger-hunters-mark',
              ),
            ],
          ),
        ),
        CombatCard(
          title: 'Rasgos de explorador',
          child: Column(
            children: [
              FeatureReminder(
                key: const Key('ranger-favored-enemy'),
                icon: AppIcons.eye,
                title: 'Enemigo predilecto ($enemies)',
                text:
                    'Ventaja en pruebas de Sabiduría (Supervivencia) para rastrearlos y de '
                    'Inteligencia para recordar información sobre ellos.',
              ),
              FeatureReminder(
                key: const Key('ranger-natural-explorer'),
                icon: AppIcons.map,
                title: 'Explorador natural ($terrains)',
                text:
                    'En tu terreno predilecto el terreno difícil no te ralentiza y no te '
                    'pierdes salvo por magia.',
              ),
              if (level >= 5)
                const FeatureReminder(
                  key: Key('ranger-extra-attack'),
                  icon: AppIcons.sword,
                  title: 'Ataque adicional',
                  text: 'Dos ataques por acción de Atacar.',
                ),
            ],
          ),
        ),
      ],
    );
  }
}
