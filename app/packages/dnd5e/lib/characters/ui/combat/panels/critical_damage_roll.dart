import 'package:flutter/material.dart';

import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';

/// [expression] with its dice doubled when [critical] (PHB ch. 9: a critical
/// hit rolls the damage dice twice, modifiers once). Text that is not a dice
/// expression is returned unchanged.
String criticalDamage(String expression, {required bool critical}) {
  if (!critical) return expression;
  return DiceExpression.tryParse(expression)?.doubleDice().toString() ?? expression;
}

/// "Crítico" chip next to a damage roll button: with the chip on, the dice of
/// [expression] are doubled and the label says "(crítico)".
///
/// Keys: `<keyPrefix>-critical` for the chip and `<keyPrefix>-roll` for the
/// button.
class CriticalDamageRoll extends StatefulWidget {
  const CriticalDamageRoll({
    super.key,
    required this.expression,
    required this.label,
    required this.keyPrefix,
  });

  /// Damage without the critical: "3d6", "1d6+3".
  final String expression;

  /// Name of the roll in the result and the history ("Ataque furtivo").
  final String label;
  final String keyPrefix;

  @override
  State<CriticalDamageRoll> createState() => _CriticalDamageRollState();
}

class _CriticalDamageRollState extends State<CriticalDamageRoll> {
  bool _critical = false;

  @override
  Widget build(BuildContext context) {
    final expression = criticalDamage(widget.expression, critical: _critical);
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        FilledButton.tonalIcon(
          key: Key('${widget.keyPrefix}-roll'),
          onPressed: () => rollAndShow(
            context,
            expression,
            label: _critical ? '${widget.label} (crítico)' : widget.label,
          ),
          icon: const AppIcon(AppIcons.d20, size: 20),
          label: Text('Tirar $expression'),
        ),
        FilterChip(
          key: Key('${widget.keyPrefix}-critical'),
          label: const Text('Crítico'),
          tooltip: 'Golpe crítico: se duplican los dados',
          selected: _critical,
          onSelected: (value) => setState(() => _critical = value),
        ),
      ],
    );
  }
}
