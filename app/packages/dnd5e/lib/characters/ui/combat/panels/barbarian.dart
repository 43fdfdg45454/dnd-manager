import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/motion/flames.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/theme/tokens.dart';

import '../../../../ui/action_type.dart';
import '../combat_rows.dart';
import '../combat_state.dart';
import '../combat_support.dart';
import 'panel_support.dart';

/// Barbarian: Rage and Reckless Attack on the turn; Unarmored Defense and
/// Brutal Critical to remember.
const barbarianPanel = ClassPanelSpec(actions: _actions, more: _more);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) => [
  RageRow(panel: panel),
  if (panel.panel.data['recklessAttack'] == true) RecklessAttackRow(panel: panel),
];

List<Widget> _more(ClassPanelContext panel, WidgetRef ref) {
  final data = panel.panel.data;
  final brutal = panelInt(data['brutalCriticalDice']) ?? 0;
  final ac = panelInt(data['unarmoredDefenseAc']);
  return [
    if (ac != null)
      FeatureReminder(
        key: const Key('barbarian-unarmored-defense'),
        icon: AppIcons.shield,
        title: 'Defensa sin armadura',
        featureIndex: 'barbarian-unarmored-defense',
        text: 'Defensa sin armadura: CA $ac',
      ),
    if (brutal > 0)
      FeatureReminder(
        key: const Key('barbarian-brutal-critical'),
        icon: AppIcons.sword,
        title: 'Crítico brutal',
        featureIndex: 'brutal-critical-1-die',
        text:
            'Crítico brutal: $brutal ${brutal == 1 ? 'dado' : 'dados'} de daño adicional '
            'en un crítico cuerpo a cuerpo.',
      ),
  ];
}

/// Rage: uses, the button and, while active, the rounds left with "Siguiente
/// asalto" and "Terminar furia". It burns with a [FlameBorder] while active.
class RageRow extends ConsumerWidget {
  const RageRow({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final c = panel.character;
    final data = panel.panel.data;
    final uses = panelUses(data['rageUses']);
    final bonus = panelInt(data['rageDamageBonus']) ?? 0;
    final rage = ref.watch(rageControllerProvider(c.id));
    final rageController = ref.read(rageControllerProvider(c.id).notifier);
    final remaining = uses.max - uses.used;
    final numbers = numericStyle(theme.textTheme.bodyMedium);

    Future<void> startRage() async {
      final done = await runCombat(
        context,
        () => panelController(ref, c).rage(),
        success: 'Furia activada.',
      );
      if (done) rageController.start();
    }

    void nextRound() {
      rageController.nextRound();
      // Reckless Attack lasts until the barbarian's next turn.
      ref.read(recklessControllerProvider(c.id).notifier).set(false);
    }

    return FlameBorder(
      key: const Key('rage-flames'),
      active: rage.active,
      child: CombatActionRow(
        icon: AppIcons.flame,
        title: 'Furia',
        // SRD: "you can enter a rage as a bonus action".
        actionKind: ActionKind.bonusAction,
        cost: 'Usos: $remaining / ${uses.max}',
        costKey: const Key('rage-uses'),
        subtitle:
            'Ventaja en Fuerza, +$bonus al daño cuerpo a cuerpo y resistencia al daño '
            'contundente, cortante y perforante.',
        info: const FeatureInfoButton(index: 'rage'),
        action: rage.active
            ? null
            : FilledButton.icon(
                key: const Key('rage-start'),
                onPressed: panel.canEdit && remaining > 0 ? startRage : null,
                icon: const AppIcon(AppIcons.flame, size: 18),
                label: Text(remaining > 0 ? 'Furia' : 'Furia (sin usos)'),
              ),
        details: [
          if (rage.active)
            Container(
              key: const Key('rage-active'),
              width: double.infinity,
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: tokens.ember.withValues(alpha: 0.15),
                borderRadius: BorderRadius.circular(4),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Furia activa', style: theme.textTheme.titleSmall),
                  Text('Bono de daño cuerpo a cuerpo: +$bonus', style: numbers),
                  Text(
                    'Asaltos restantes: ${rage.roundsLeft}',
                    key: const Key('rage-rounds'),
                    style: numbers,
                  ),
                ],
              ),
            ),
        ],
        actions: [
          if (rage.active) ...[
            OutlinedButton(
              key: const Key('rage-next-round'),
              onPressed: nextRound,
              child: const Text('Siguiente asalto'),
            ),
            OutlinedButton(
              key: const Key('rage-end'),
              onPressed: rageController.end,
              child: const Text('Terminar furia'),
            ),
          ],
        ],
      ),
    );
  }
}

/// Reckless Attack this turn: a local switch that burns while on.
class RecklessAttackRow extends ConsumerWidget {
  const RecklessAttackRow({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final id = panel.character.id;
    final reckless = ref.watch(recklessControllerProvider(id));
    final set = ref.read(recklessControllerProvider(id).notifier).set;
    return FlameBorder(
      key: const Key('reckless-flames'),
      active: reckless,
      child: CombatActionRow(
        key: const Key('reckless-attack'),
        icon: AppIcons.bolt,
        title: 'Ataque temerario',
        subtitle:
            'Ventaja en tus ataques cuerpo a cuerpo con Fuerza este turno, pero los '
            'ataques contra ti tienen ventaja hasta tu próximo turno.',
        info: const FeatureInfoButton(index: 'reckless-attack'),
        action: reckless
            ? OutlinedButton(
                key: const Key('reckless-toggle'),
                onPressed: () => set(false),
                child: const Text('Terminar', maxLines: 1, overflow: TextOverflow.ellipsis),
              )
            : FilledButton.tonalIcon(
                key: const Key('reckless-toggle'),
                onPressed: panel.canEdit ? () => set(true) : null,
                icon: const AppIcon(AppIcons.bolt, size: 18),
                label: const Text('Atacar', maxLines: 1, overflow: TextOverflow.ellipsis),
              ),
      ),
    );
  }
}
