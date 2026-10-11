import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';

import '../../../../ui/action_type.dart';
import '../../../models.dart';
import '../combat_rows.dart';
import '../combat_support.dart';
import 'panel_support.dart';

/// Highest challenge rating destroyed by Destroy Undead (SRD); null below 5.
String? destroyUndeadCr(int level) => level >= 17
    ? '4'
    : level >= 14
    ? '3'
    : level >= 11
    ? '2'
    : level >= 8
    ? '1'
    : level >= 5
    ? '1/2'
    : null;

/// Rolls Divine Intervention (1d100) and says whether the deity answers: it
/// does on [level] or less.
Future<void> rollDivineIntervention(BuildContext context, int level) async {
  final result = await rollAndShow(context, '1d100', label: 'Intervención divina');
  if (result == null || !context.mounted) return;
  showCombatMessage(
    context,
    result.total <= level
        ? '${result.total}: tu deidad interviene. No podrás pedirlo de nuevo en 7 días.'
        : '${result.total}: tu deidad no interviene. Puedes volver a pedirlo tras un descanso largo.',
  );
}

/// Cleric: Channel Divinity (resource `channel-divinity`, from level 2) with
/// Turn and Destroy Undead, and Divine Intervention.
const clericPanel = ClassPanelSpec(actions: _actions);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) {
  final level = panel.level;
  final cr = destroyUndeadCr(level);
  final dc = panel.character.sheet.spellcasting
      .where((s) => s.classIndex == 'cleric')
      .firstOrNull
      ?.saveDc;
  return [
    ClassResourceActionRow(
      panel: panel,
      resourceKey: 'channel-divinity',
      title: 'Canalizar divinidad',
      featureIndex: 'channel-divinity-1-rest',
      // SRD, Turn Undead: "As an action, you present your holy symbol".
      actionKind: ActionKind.action,
      actionKey: 'cleric-channel-divinity',
      buttonLabel: 'Canalizar',
      icon: AppIcons.sun,
      minLevel: 2,
      success: 'Canalizar divinidad gastado.',
      extra: [
        if (level >= 2)
          PanelFact(
            key: const Key('cleric-turn-undead'),
            label: 'Expulsar muertos vivientes',
            value: dc == null ? 'salvación de Sabiduría' : 'salvación de Sabiduría CD $dc',
            detail: 'Los muertos vivientes a 30 pies que fallen huyen durante 1 minuto.',
          ),
        if (cr != null)
          PanelFact(
            key: const Key('cleric-destroy-undead'),
            label: 'Destruir muertos vivientes',
            value: 'VD $cr o inferior',
            detail: 'Los que fallen la salvación contra Expulsar quedan destruidos.',
          ),
      ],
    ),
    if (level >= 10) _DivineInterventionRow(level: level),
  ];
}

class _DivineInterventionRow extends StatelessWidget {
  const _DivineInterventionRow({required this.level});

  final int level;

  @override
  Widget build(BuildContext context) => CombatActionRow(
    icon: AppIcons.sun,
    title: 'Intervención divina',
    // SRD: "Imploring your deity's aid requires you to use your action".
    actionKind: ActionKind.action,
    subtitle: level >= 20
        ? 'Tu deidad interviene sin necesidad de tirada. Una vez cada 7 días.'
        : 'Tira 1d100: tu deidad interviene con $level o menos. Si lo hace, espera '
              '7 días; si no, hasta tu próximo descanso largo.',
    subtitleKey: const Key('cleric-divine-intervention'),
    info: const FeatureInfoButton(index: 'divine-intervention'),
    action: level >= 20
        ? null
        : FilledButton.tonal(
            key: const Key('cleric-divine-intervention-roll'),
            onPressed: () => rollDivineIntervention(context, level),
            child: const Text('Tirar 1d100'),
          ),
  );
}
