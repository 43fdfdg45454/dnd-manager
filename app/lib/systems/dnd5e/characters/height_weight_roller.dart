import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/systems/game_system_ui.dart';
import '../../../features/catalog/data/catalog_controllers.dart';
import '../../../features/characters/domain/height_weight.dart';
import '../../../features/dice/data/dice_controller.dart' show diceRandomProvider;

/// "Tirar" with the height and weight table of the race (or subrace) of
/// [scope]: rolls it with the app's dice engine, hands the result to
/// [HeightWeightScope.onRolled] and shows the roll in a SnackBar. Nothing
/// when the race has no table. Key: `<keyPrefix>-roll-height-weight`.
class Dnd5eHeightWeightRoller extends ConsumerWidget {
  const Dnd5eHeightWeightRoller({super.key, required this.scope});

  final HeightWeightScope scope;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final raceIndex = scope.raceIndex;
    if (raceIndex == null) return const SizedBox.shrink();
    final table = ref.watch(raceDetailProvider(raceIndex)).value?.heightWeightFor(scope.subraceIndex);
    if (table == null) return const SizedBox.shrink();
    return TextButton.icon(
      key: Key('${scope.keyPrefix}-roll-height-weight'),
      onPressed: () {
        final roll = rollHeightWeight(table, ref.read(diceRandomProvider));
        if (roll == null) return;
        scope.onRolled(roll.heightInches, roll.weightPounds);
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(roll.summary)));
      },
      icon: const Icon(Icons.casino_outlined),
      label: const Text('Tirar'),
    );
  }
}
