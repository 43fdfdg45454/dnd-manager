import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/systems/game_system_ui.dart';
import 'package:opentrpg_core/core/ui/app_message.dart';
import 'package:opentrpg_core/features/dice/data/dice_controller.dart' show diceRandomProvider;

import '../catalog/data/catalog_controllers.dart';
import '../catalog/data/models.dart' show HeightWeightTable;
import 'domain/height_weight.dart';

/// Header of the height and weight fields: the title with "Tirar" beside it
/// when the race (or subrace) of [scope] has a table, and the table below;
/// without a table, a short note to type them by hand.
/// "Tirar" rolls it with the app's dice engine, hands the result to
/// [HeightWeightScope.onRolled] and shows the roll in a SnackBar. Key:
/// `<keyPrefix>-roll-height-weight`.
class Dnd5eHeightWeightRoller extends ConsumerWidget {
  const Dnd5eHeightWeightRoller({super.key, required this.scope});

  final HeightWeightScope scope;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final raceIndex = scope.raceIndex;
    final race = raceIndex == null ? null : ref.watch(raceDetailProvider(raceIndex)).value;
    final table = race?.heightWeightFor(scope.subraceIndex);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: scope.title),
            if (table != null)
              TextButton.icon(
                key: Key('${scope.keyPrefix}-roll-height-weight'),
                onPressed: () => _roll(context, ref, table),
                icon: const Icon(Icons.casino_outlined),
                label: const Text('Tirar'),
              ),
          ],
        ),
        if (table != null)
          Text(
            'Tabla de la raza: ${describeHeightWeightTable(table)}',
            style: Theme.of(context).textTheme.bodySmall,
          )
        else if (race != null)
          Text(
            'Esta raza no tiene tabla de altura y peso; escríbelos a mano.',
            key: Key('${scope.keyPrefix}-no-height-weight-table'),
            style: Theme.of(context).textTheme.bodySmall
                ?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant),
          ),
      ],
    );
  }

  void _roll(BuildContext context, WidgetRef ref, HeightWeightTable table) {
    final roll = rollHeightWeight(table, ref.read(diceRandomProvider));
    if (roll == null) return;
    scope.onRolled(roll.heightInches, roll.weightPounds);
    showAppMessage(context, roll.summary);
  }
}
