import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../catalog/ui/detail_widgets.dart' show SectionTitle;
import '../combat_state.dart';
import '../combat_support.dart';
import 'panel_support.dart';

class BarbarianPanel extends ConsumerWidget {
  const BarbarianPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final c = panel.character;
    final data = panel.panel.data;
    final uses = panelUses(data['rageUses']);
    final bonus = panelInt(data['rageDamageBonus']) ?? 0;
    final brutal = panelInt(data['brutalCriticalDice']) ?? 0;
    final ac = panelInt(data['unarmoredDefenseAc']);
    final rage = ref.watch(rageControllerProvider(c.id));
    final rageController = ref.read(rageControllerProvider(c.id).notifier);
    final remaining = uses.max - uses.used;

    Future<void> startRage() async {
      final done = await runCombat(
        context,
        () => panelController(ref, c).rage(),
        success: 'Furia activada.',
      );
      if (done) rageController.start();
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Bárbaro'),
        CombatCard(
          key: const Key('class-panel-barbarian'),
          title: 'Furia',
          trailing: Text('Usos: $remaining / ${uses.max}', key: const Key('rage-uses')),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (rage.active) ...[
                Container(
                  key: const Key('rage-active'),
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.errorContainer,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          const Icon(Icons.local_fire_department),
                          const SizedBox(width: 8),
                          Text('Furia activa', style: theme.textTheme.titleMedium),
                        ],
                      ),
                      Text('Bono de daño cuerpo a cuerpo: +$bonus'),
                      Text('Asaltos restantes: ${rage.roundsLeft}', key: const Key('rage-rounds')),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        key: const Key('rage-next-round'),
                        onPressed: rageController.nextRound,
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
              ] else
                SizedBox(
                  width: double.infinity,
                  child: FilledButton.icon(
                    key: const Key('rage-start'),
                    onPressed: panel.canEdit && remaining > 0 ? startRage : null,
                    icon: const Icon(Icons.local_fire_department),
                    label: Text(remaining > 0 ? 'Furia' : 'Furia (sin usos)'),
                  ),
                ),
              const Divider(height: 24),
              if (data['recklessAttack'] == true)
                ListTile(
                  key: const Key('reckless-attack'),
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.bolt),
                  title: const Text('Ataque temerario'),
                  subtitle: const Text(
                    'Ventaja en tus ataques cuerpo a cuerpo con Fuerza este turno, pero los '
                    'ataques contra ti tienen ventaja hasta tu próximo turno.',
                  ),
                ),
              if (ac != null) Text('Defensa sin armadura: CA $ac'),
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
