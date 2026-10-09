import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/theme/icons.dart';
import '../../../../../core/ui/action_type.dart';

import '../combat_support.dart';
import 'panel_support.dart';

/// Bardic Inspiration die by bard level (SRD): d6, d8 at 5, d10 at 10, d12 at 15.
String bardicInspirationDie(int level) => level >= 15
    ? 'd12'
    : level >= 10
    ? 'd10'
    : level >= 5
    ? 'd8'
    : 'd6';

/// Song of Rest die by bard level (from 2); null before.
String? songOfRestDie(int level) => level < 2
    ? null
    : level >= 17
    ? 'd12'
    : level >= 13
    ? 'd10'
    : level >= 9
    ? 'd8'
    : 'd6';

/// Bard: Bardic Inspiration (resource `bardic-inspiration`) with its die and
/// the Song of Rest die.
class BardPanel extends ConsumerWidget {
  const BardPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final level = panel.level;
    // The server sends the die with the resource; older servers do not.
    final die =
        findResource(panel.character, 'bardic-inspiration')?.dice ?? bardicInspirationDie(level);
    final song = songOfRestDie(level);
    return ClassPanelFrame(
      panel: panel,
      children: [
        ClassResourceActionCard(
          panel: panel,
          resourceKey: 'bardic-inspiration',
          title: 'Inspiración bárdica',
          // SRD: "You can inspire others ... use a bonus action on your turn".
          actionKind: ActionKind.bonusAction,
          actionKey: 'bard-inspire',
          buttonLabel: 'Inspirar (1$die)',
          icon: AppIcons.bard,
          success: 'Inspiración bárdica concedida: 1$die.',
          trailing: Text('Dado: $die', key: const Key('bard-inspiration-die')),
          description:
              'Acción adicional: una criatura que te oiga gana un dado $die para sumarlo a '
              'una prueba, ataque o salvación en los próximos 10 minutos.',
        ),
        if (song != null)
          CombatCard(
            title: 'Canción de descanso',
            child: PanelFact(
              key: const Key('bard-song-of-rest'),
              label: 'Curación extra',
              value: '1$song',
              detail:
                  'Durante un descanso corto, quienes gasten dados de golpe recuperan 1$song PG más.',
            ),
          ),
      ],
    );
  }
}
