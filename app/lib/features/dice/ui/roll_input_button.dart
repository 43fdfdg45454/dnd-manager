import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_icon.dart';
import '../../../core/theme/icons.dart';
import '../data/dice_controller.dart';
import '../domain/dice_expression.dart';

/// Rolls [expression] with the virtual dice and records it in the dice history
/// with [label]. Returns null (and records nothing) when the expression is not
/// valid.
DiceResult? rollVirtualDice(WidgetRef ref, String expression, {String? label}) {
  final parsed = DiceExpression.tryParse(expression);
  if (parsed == null) return null;
  final result = parsed.roll(ref.read(diceRandomProvider));
  ref.read(diceControllerProvider.notifier).record(result, label: label);
  return result;
}

/// "Tirar" button placed next to a field where the player types a physical
/// roll: it rolls [expression] with the virtual dice, records the roll in the
/// history and hands the total to [onRolled] so the caller fills its field.
///
/// With [announce] a short SnackBar shows the breakdown
/// ("4d6kh3: [6 5 4 (1)] = 15"); off by default because the field already
/// shows the total and a SnackBar would cover the buttons at the bottom.
class RollInputButton extends ConsumerWidget {
  const RollInputButton({
    super.key,
    required this.expression,
    required this.onRolled,
    this.label,
    this.enabled = true,
    this.announce = false,
  });

  /// What to roll, such as "1d10" or "4d6kh3".
  final String expression;

  /// Receives the total of the roll and the full result.
  final void Function(int total, DiceResult result) onRolled;

  /// Name of the roll in the history ("PG de nivel 5").
  final String? label;
  final bool enabled;
  final bool announce;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return IconButton(
      tooltip: 'Tirar $expression',
      icon: const AppIcon(AppIcons.d20),
      onPressed: enabled
          ? () {
              final result = rollVirtualDice(ref, expression, label: label);
              if (result == null) return;
              onRolled(result.total, result);
              if (!announce) return;
              final name = label == null ? expression : '$label ($expression)';
              ScaffoldMessenger.maybeOf(context)
                ?..hideCurrentSnackBar()
                ..showSnackBar(
                  SnackBar(content: Text('$name: ${result.breakdown} = ${result.total}')),
                );
            }
          : null,
    );
  }
}
