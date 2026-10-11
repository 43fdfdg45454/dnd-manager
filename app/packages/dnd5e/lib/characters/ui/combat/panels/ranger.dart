import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';

import '../../../../ui/action_type.dart';
import '../../../../catalog/ui/catalog_detail_links.dart';
import '../../../models.dart';
import '../combat_rows.dart';
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

/// Ranger: a Hunter's Mark button that sets the concentration and Extra
/// Attack on the turn; Favored Enemy and Natural Explorer to remember.
const rangerPanel = ClassPanelSpec(actions: _actions, more: _more);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) => [
  HuntersMarkRow(panel: panel),
  if (panel.level >= 5)
    const FeatureReminder(
      key: Key('ranger-extra-attack'),
      icon: AppIcons.sword,
      title: 'Ataque adicional',
      featureIndex: 'ranger-extra-attack',
      text: 'Dos ataques por acción de Atacar.',
    ),
];

List<Widget> _more(ClassPanelContext panel, WidgetRef ref) {
  final enemies = favoredEnemies(panel.level);
  final terrains = favoredTerrains(panel.level);
  return [
    FeatureReminder(
      key: const Key('ranger-favored-enemy'),
      icon: AppIcons.eye,
      title: 'Enemigo predilecto ($enemies)',
      featureIndex: 'favored-enemy-1-type',
      text:
          'Ventaja en pruebas de Sabiduría (Supervivencia) para rastrearlos y de '
          'Inteligencia para recordar información sobre ellos.',
    ),
    FeatureReminder(
      key: const Key('ranger-natural-explorer'),
      icon: AppIcons.map,
      title: 'Explorador natural ($terrains)',
      featureIndex: 'natural-explorer-1-terrain-type',
      text:
          'En tu terreno predilecto el terreno difícil no te ralentiza y no te '
          'pierdes salvo por magia.',
    ),
  ];
}

/// Hunter's Mark: concentrates on the spell (its slot is spent from
/// "Conjuros") and rolls its extra damage.
class HuntersMarkRow extends ConsumerWidget {
  const HuntersMarkRow({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = panel.character;
    final marking = c.concentratingOnSpellIndex == huntersMarkIndex;
    final unlocked = panel.level >= 2;
    return CombatActionRow(
      icon: AppIcons.bow,
      title: 'Marca del cazador',
      // SRD, Hunter's Mark: "Casting Time: 1 bonus action".
      actionKind: unlocked ? ActionKind.bonusAction : null,
      subtitle:
          '+1d6 de daño a la criatura marcada y ventaja para rastrearla. Requiere '
          'concentración; el espacio se gasta en "Conjuros".',
      info: DetailInfoButton(
        key: const Key('detail-spell-$huntersMarkIndex'),
        onPressed: () => openSpellDetail(context, huntersMarkIndex),
      ),
      action: unlocked
          ? FilledButton.tonalIcon(
              key: const Key('ranger-hunters-mark'),
              onPressed: panel.canEdit && !marking
                  ? () async {
                      if (!await confirmReplaceConcentration(context, ref, c, huntersMarkIndex)) {
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
              icon: const AppIcon(AppIcons.bow, size: 18),
              label: Text(
                marking ? 'Marca del cazador (activa)' : 'Marcar',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            )
          : null,
      details: [
        if (!unlocked)
          Text(
            'Marca del cazador: se obtiene a nivel 2 (lanzamiento de conjuros).',
            key: const Key('ranger-hunters-mark-locked'),
            style: Theme.of(context).textTheme.bodySmall,
          )
        else
          const CriticalDamageRoll(
            expression: '1d6',
            label: 'Marca del cazador',
            keyPrefix: 'ranger-hunters-mark',
          ),
      ],
    );
  }
}
