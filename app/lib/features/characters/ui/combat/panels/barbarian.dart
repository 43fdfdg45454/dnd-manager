import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/motion/flames.dart';
import '../../../../../core/theme/app_icon.dart';
import '../../../../../core/theme/components.dart';
import '../../../../../core/theme/icons.dart';
import '../../../../../core/ui/action_type.dart';
import '../../../../../core/theme/tokens.dart';
import '../combat_state.dart';
import '../combat_support.dart';
import 'panel_support.dart';

/// Barbarian: Rage (uses, rounds left and damage bonus) and Reckless Attack.
/// Both burn with a [FlameBorder] while active: the flames rise when they are
/// switched on and stay as embers until they end.
class BarbarianPanel extends ConsumerStatefulWidget {
  const BarbarianPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  ConsumerState<BarbarianPanel> createState() => _BarbarianPanelState();
}

class _BarbarianPanelState extends ConsumerState<BarbarianPanel> {
  /// Reckless Attack this turn (local, like the rage rounds).
  bool _reckless = false;

  @override
  Widget build(BuildContext context) {
    final panel = widget.panel;
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final c = panel.character;
    final data = panel.panel.data;
    final uses = panelUses(data['rageUses']);
    final bonus = panelInt(data['rageDamageBonus']) ?? 0;
    final brutal = panelInt(data['brutalCriticalDice']) ?? 0;
    final ac = panelInt(data['unarmoredDefenseAc']);
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
      if (_reckless) setState(() => _reckless = false);
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionHeader('Bárbaro', padding: combatSectionPadding),
        CombatCard(
          key: const Key('class-panel-barbarian'),
          title: 'Furia',
          // SRD: "you can enter a rage as a bonus action".
          actionKind: ActionKind.bonusAction,
          trailing: Text(
            'Usos: $remaining / ${uses.max}',
            key: const Key('rage-uses'),
            style: numbers,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              FlameBorder(
                key: const Key('rage-flames'),
                active: rage.active,
                child: rage.active
                    ? Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Container(
                            key: const Key('rage-active'),
                            width: double.infinity,
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: tokens.ember.withValues(alpha: 0.15),
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  children: [
                                    AppIcon(AppIcons.flame, color: tokens.ember),
                                    const SizedBox(width: 8),
                                    Text('Furia activa', style: theme.textTheme.titleMedium),
                                  ],
                                ),
                                Text('Bono de daño cuerpo a cuerpo: +$bonus', style: numbers),
                                Text(
                                  'Asaltos restantes: ${rage.roundsLeft}',
                                  key: const Key('rage-rounds'),
                                  style: numbers,
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 8),
                          Padding(
                            padding: const EdgeInsets.fromLTRB(8, 0, 8, 8),
                            child: Row(
                              children: [
                                Expanded(
                                  child: OutlinedButton(
                                    key: const Key('rage-next-round'),
                                    onPressed: nextRound,
                                    child: const Text('Siguiente asalto'),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: OutlinedButton(
                                    key: const Key('rage-end'),
                                    onPressed: rageController.end,
                                    child: const Text('Terminar furia'),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      )
                    : SizedBox(
                        width: double.infinity,
                        child: FilledButton.icon(
                          key: const Key('rage-start'),
                          onPressed: panel.canEdit && remaining > 0 ? startRage : null,
                          icon: const AppIcon(AppIcons.flame, size: 20),
                          label: Text(remaining > 0 ? 'Furia' : 'Furia (sin usos)'),
                        ),
                      ),
              ),
              const Divider(height: 24),
              if (data['recklessAttack'] == true)
                FlameBorder(
                  key: const Key('reckless-flames'),
                  active: _reckless,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      ListTile(
                        key: const Key('reckless-attack'),
                        contentPadding: const EdgeInsets.symmetric(horizontal: 8),
                        leading: AppIcon(
                          AppIcons.bolt,
                          color: _reckless ? tokens.ember : theme.colorScheme.primary,
                        ),
                        title: const Text('Ataque temerario'),
                        subtitle: const Text(
                          'Ventaja en tus ataques cuerpo a cuerpo con Fuerza este turno, pero los '
                          'ataques contra ti tienen ventaja hasta tu próximo turno.',
                        ),
                      ),
                      Padding(
                        padding: const EdgeInsets.fromLTRB(8, 0, 8, 8),
                        child: SizedBox(
                          width: double.infinity,
                          child: _reckless
                              ? OutlinedButton(
                                  key: const Key('reckless-toggle'),
                                  onPressed: () => setState(() => _reckless = false),
                                  child: const Text('Terminar ataque temerario'),
                                )
                              : FilledButton.tonalIcon(
                                  key: const Key('reckless-toggle'),
                                  onPressed: panel.canEdit
                                      ? () => setState(() => _reckless = true)
                                      : null,
                                  icon: const AppIcon(AppIcons.bolt, size: 20),
                                  label: const Text('Atacar temerariamente'),
                                ),
                        ),
                      ),
                    ],
                  ),
                ),
              if (ac != null) Text('Defensa sin armadura: CA $ac', style: numbers),
              if (brutal > 0)
                Text(
                  'Crítico brutal: $brutal ${brutal == 1 ? 'dado' : 'dados'} de daño adicional '
                  'en un crítico cuerpo a cuerpo.',
                ),
            ],
          ),
        ),
      ],
    );
  }
}
