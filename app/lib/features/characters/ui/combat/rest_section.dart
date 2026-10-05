import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../campaigns/ui/confirm_dialog.dart';
import '../../data/characters_controller.dart';
import '../../data/models.dart';
import '../character_tabs.dart' show titleFromSpellIndex;
import 'combat_state.dart';
import 'combat_support.dart';

/// "Descanso corto" (asks for the hit dice to spend) and "Descanso largo".
class RestSection extends ConsumerWidget {
  const RestSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  Future<void> _shortRest(BuildContext context, WidgetRef ref) async {
    final spent = await showDialog<Map<String, int>>(
      context: context,
      builder: (_) => ShortRestDialog(character: character),
    );
    if (spent == null || !context.mounted) return;
    await runCombat(
      context,
      () => ref.read(characterControllerProvider(character.id).notifier).shortRest(hitDice: spent),
      success: 'Descanso corto realizado.',
    );
  }

  Future<void> _longRest(BuildContext context, WidgetRef ref) async {
    final confirmed = await confirmAction(
      context,
      title: 'Descanso largo',
      message:
          'Se recuperarán los puntos de golpe, los espacios de conjuro y los recursos que '
          'se recargan con un descanso largo.',
      confirmLabel: 'Descansar',
    );
    if (!confirmed || !context.mounted) return;
    final rage = ref.read(rageControllerProvider(character.id).notifier);
    final done = await runCombat(
      context,
      () => ref.read(characterControllerProvider(character.id).notifier).longRest(),
      success: 'Descanso largo realizado.',
    );
    if (done) rage.end();
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return CombatCard(
      title: 'Descansos',
      child: Row(
        children: [
          Expanded(
            child: OutlinedButton.icon(
              key: const Key('rest-short'),
              onPressed: canEdit ? () => _shortRest(context, ref) : null,
              icon: const Icon(Icons.local_cafe_outlined),
              label: const Text('Descanso corto'),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: FilledButton.icon(
              key: const Key('rest-long'),
              onPressed: canEdit ? () => _longRest(context, ref) : null,
              icon: const Icon(Icons.bedtime_outlined),
              label: const Text('Descanso largo'),
            ),
          ),
        ],
      ),
    );
  }
}

/// Lets the player pick how many hit dice of each class to spend. Resolves to
/// the dice spent by class index (only classes with at least one).
class ShortRestDialog extends StatefulWidget {
  const ShortRestDialog({super.key, required this.character});

  final CharacterDetail character;

  @override
  State<ShortRestDialog> createState() => _ShortRestDialogState();
}

class _ShortRestDialogState extends State<ShortRestDialog> {
  final Map<String, int> _spent = {};

  String _className(String classIndex) {
    for (final c in widget.character.classes) {
      if (c.classIndex == classIndex) return c.className;
    }
    return titleFromSpellIndex(classIndex);
  }

  @override
  Widget build(BuildContext context) {
    final dice = widget.character.sheet.hitDice;
    return AlertDialog(
      title: const Text('Descanso corto'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('¿Cuántos dados de golpe gastas para recuperar puntos de golpe?'),
            const SizedBox(height: 8),
            if (dice.isEmpty) const Text('Este personaje no tiene dados de golpe.'),
            for (final d in dice)
              Row(
                key: Key('hit-dice-${d.classIndex}'),
                children: [
                  Expanded(
                    child: Text(
                      '${_className(d.classIndex)} (d${d.die})\n'
                      'Quedan ${d.remaining} de ${d.total}',
                    ),
                  ),
                  IconButton(
                    key: Key('hit-dice-${d.classIndex}-minus'),
                    tooltip: 'Menos',
                    onPressed: (_spent[d.classIndex] ?? 0) > 0
                        ? () => setState(() => _spent[d.classIndex] = _spent[d.classIndex]! - 1)
                        : null,
                    icon: const Icon(Icons.remove_circle_outline),
                  ),
                  SizedBox(
                    width: 24,
                    child: Text('${_spent[d.classIndex] ?? 0}', textAlign: TextAlign.center),
                  ),
                  IconButton(
                    key: Key('hit-dice-${d.classIndex}-plus'),
                    tooltip: 'Más',
                    onPressed: (_spent[d.classIndex] ?? 0) < d.remaining
                        ? () =>
                              setState(() => _spent[d.classIndex] = (_spent[d.classIndex] ?? 0) + 1)
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
          key: const Key('rest-short-confirm'),
          onPressed: () => Navigator.of(context).pop({
            for (final e in _spent.entries)
              if (e.value > 0) e.key: e.value,
          }),
          child: const Text('Descansar'),
        ),
      ],
    );
  }
}
