import 'package:flutter/material.dart';

import '../../catalog/domain/catalog_format.dart';
import '../domain/character_format.dart';
import '../models.dart';

/// Standard 27-point buy helper: every score starts at 8. Pops the chosen base
/// scores (a map by ability key) or null if cancelled. The result is only a
/// suggestion: the editor fields remain freely editable afterwards.
class PointBuyDialog extends StatefulWidget {
  const PointBuyDialog({super.key});

  @override
  State<PointBuyDialog> createState() => _PointBuyDialogState();
}

class _PointBuyDialogState extends State<PointBuyDialog> {
  final Map<String, int> _scores = {for (final k in abilityKeys) k: pointBuyMin};

  int get _remaining => pointBuyBudget - pointBuyTotal(_scores.values);

  bool _canIncrease(String key) {
    final score = _scores[key]!;
    if (score >= pointBuyMax) return false;
    return pointBuyCost(score + 1) - pointBuyCost(score) <= _remaining;
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Compra por puntos'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'Puntos restantes: $_remaining / $pointBuyBudget',
              key: const Key('point-buy-remaining'),
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            for (final key in abilityKeys)
              Row(
                children: [
                  Expanded(child: Text(abilityLabel(key))),
                  IconButton(
                    key: Key('point-buy-minus-$key'),
                    tooltip: 'Restar',
                    onPressed: _scores[key]! > pointBuyMin
                        ? () => setState(() => _scores[key] = _scores[key]! - 1)
                        : null,
                    icon: const Icon(Icons.remove_circle_outline),
                  ),
                  SizedBox(
                    width: 28,
                    child: Text(
                      '${_scores[key]}',
                      key: Key('point-buy-score-$key'),
                      textAlign: TextAlign.center,
                    ),
                  ),
                  IconButton(
                    key: Key('point-buy-plus-$key'),
                    tooltip: 'Sumar',
                    onPressed: _canIncrease(key)
                        ? () => setState(() => _scores[key] = _scores[key]! + 1)
                        : null,
                    icon: const Icon(Icons.add_circle_outline),
                  ),
                ],
              ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('point-buy-apply'),
          onPressed: () => Navigator.of(context).pop<Map<String, int>>(Map.of(_scores)),
          child: const Text('Aplicar'),
        ),
      ],
    );
  }
}
