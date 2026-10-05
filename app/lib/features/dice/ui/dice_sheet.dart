import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/dice_controller.dart';
import '../domain/dice_expression.dart';

const _quickDice = [4, 6, 8, 10, 12, 20, 100];

/// Rolls [expression], records it in the history and shows the outcome in a
/// bottom sheet (short animation, critical and fumble highlighted).
///
/// An invalid expression shows its Spanish error in a SnackBar and returns null.
/// [onResult] is called as soon as the dice are thrown, before the sheet closes,
/// so callers can react to a critical right away. The future completes when the
/// sheet is dismissed.
Future<DiceResult?> rollAndShow(
  BuildContext context,
  String expression, {
  String? label,
  void Function(DiceResult result)? onResult,
}) async {
  final container = ProviderScope.containerOf(context);
  final DiceResult result;
  try {
    result = DiceExpression.parse(expression).roll(container.read(diceRandomProvider));
  } on FormatException catch (error) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(error.message)));
    return null;
  }
  container.read(diceControllerProvider.notifier).record(result, label: label);
  onResult?.call(result);
  if (!context.mounted) return result;
  await showModalBottomSheet<void>(
    context: context,
    showDragHandle: true,
    useSafeArea: true,
    builder: (_) => SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        child: DiceResultView(result: result, label: label),
      ),
    ),
  );
  return result;
}

/// Opens the dice tray (quick dice, expression field, history and favorites).
Future<void> showDiceSheet(BuildContext context) => showModalBottomSheet<void>(
  context: context,
  isScrollControlled: true,
  showDragHandle: true,
  useSafeArea: true,
  builder: (_) => const DiceSheet(),
);

/// Total and per-die breakdown of a roll. The total counts up with a short
/// animation; criticals are highlighted in green and fumbles in red.
class DiceResultView extends StatelessWidget {
  const DiceResultView({super.key, required this.result, this.label});

  final DiceResult result;
  final String? label;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final color = result.isCritical
        ? Colors.green.shade700
        : result.isFumble
        ? scheme.error
        : scheme.onSurface;
    return Column(
      key: const Key('dice-result'),
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        if (label != null)
          Text(label!, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
        Text(result.expression.toString(), style: theme.textTheme.labelLarge),
        const SizedBox(height: 4),
        TweenAnimationBuilder<double>(
          tween: Tween(begin: 0, end: 1),
          duration: const Duration(milliseconds: 450),
          builder: (context, t, _) {
            // While the animation runs, show cycling numbers; it ends on the total.
            final shown = t >= 1
                ? result.total
                : (result.total * 7 + (t * 24).floor() * 13) % 20 + 1;
            return Text(
              '$shown',
              key: const Key('dice-result-total'),
              style: theme.textTheme.displayLarge?.copyWith(
                color: color,
                fontWeight: FontWeight.bold,
              ),
            );
          },
        ),
        if (result.isCritical)
          Chip(
            key: const Key('dice-critical'),
            avatar: const Icon(Icons.auto_awesome, size: 18),
            label: const Text('¡Crítico!'),
            backgroundColor: Colors.green.shade100,
          ),
        if (result.isFumble)
          Chip(
            key: const Key('dice-fumble'),
            avatar: const Icon(Icons.warning_amber, size: 18),
            label: const Text('¡Pifia!'),
            backgroundColor: scheme.errorContainer,
          ),
        const SizedBox(height: 8),
        Text(
          result.breakdown,
          key: const Key('dice-breakdown'),
          style: theme.textTheme.bodyLarge,
          textAlign: TextAlign.center,
        ),
      ],
    );
  }
}

/// The dice tray: quick dice build an expression, which is rolled with the
/// chosen advantage mode. Below it, favorites and the last 100 rolls.
class DiceSheet extends ConsumerStatefulWidget {
  const DiceSheet({super.key});

  @override
  ConsumerState<DiceSheet> createState() => _DiceSheetState();
}

class _DiceSheetState extends ConsumerState<DiceSheet> {
  final _controller = TextEditingController();
  AdvantageMode _mode = AdvantageMode.normal;
  DiceResult? _last;
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  /// "2d6" + d6 -> "3d6"; a different die is appended as "+1dX".
  void _addDie(int sides) {
    final text = _controller.text.trim();
    final match = RegExp(r'(\d*)d(\d+)$').firstMatch(text);
    String next;
    if (text.isEmpty) {
      next = '1d$sides';
    } else if (match != null && match.group(2) == '$sides') {
      final count = int.tryParse(match.group(1)!.isEmpty ? '1' : match.group(1)!) ?? 1;
      next = '${text.substring(0, match.start)}${count + 1}d$sides';
    } else {
      next = '$text+1d$sides';
    }
    setState(() {
      _controller.text = next;
      _error = null;
    });
  }

  void _roll(String text, {String? label}) {
    if (text.trim().isEmpty) {
      setState(() => _error = 'Escribe una expresión de dados, por ejemplo 1d20+5.');
      return;
    }
    try {
      final expression = DiceExpression.parse(text).withAdvantage(_mode);
      final result = expression.roll(ref.read(diceRandomProvider));
      ref.read(diceControllerProvider.notifier).record(result, label: label);
      setState(() {
        _last = result;
        _error = null;
      });
    } on FormatException catch (error) {
      setState(() => _error = error.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(diceControllerProvider);
    final controller = ref.read(diceControllerProvider.notifier);
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: DraggableScrollableSheet(
        expand: false,
        initialChildSize: .85,
        minChildSize: .4,
        maxChildSize: .95,
        builder: (context, scroll) => ListView(
          controller: scroll,
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          children: [
            Text('Dados', style: theme.textTheme.titleLarge),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final sides in _quickDice)
                  FilledButton.tonal(
                    key: Key('die-d$sides'),
                    onPressed: () => _addDie(sides),
                    child: Text('d$sides'),
                  ),
              ],
            ),
            const SizedBox(height: 12),
            TextField(
              key: const Key('dice-expression'),
              controller: _controller,
              autocorrect: false,
              textInputAction: TextInputAction.done,
              decoration: InputDecoration(
                labelText: 'Expresión',
                hintText: '2d6+3, 4d6kh3, adv+5…',
                errorText: _error,
                suffixIcon: IconButton(
                  tooltip: 'Borrar',
                  icon: const Icon(Icons.backspace_outlined),
                  onPressed: () => setState(() {
                    _controller.clear();
                    _error = null;
                  }),
                ),
              ),
              onSubmitted: _roll,
            ),
            const SizedBox(height: 8),
            SegmentedButton<AdvantageMode>(
              key: const Key('dice-advantage'),
              showSelectedIcon: false,
              segments: const [
                ButtonSegment(value: AdvantageMode.normal, label: Text('Normal')),
                ButtonSegment(value: AdvantageMode.advantage, label: Text('Ventaja')),
                ButtonSegment(value: AdvantageMode.disadvantage, label: Text('Desventaja')),
              ],
              selected: {_mode},
              onSelectionChanged: (s) => setState(() => _mode = s.first),
            ),
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                'Ventaja y desventaja se aplican al primer 1d20 de la expresión.',
                style: theme.textTheme.bodySmall,
              ),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: FilledButton.icon(
                    key: const Key('dice-roll'),
                    onPressed: () => _roll(_controller.text),
                    icon: const Icon(Icons.casino_outlined),
                    label: const Text('Tirar'),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton.outlined(
                  key: const Key('dice-favorite-add'),
                  tooltip: 'Guardar como favorita',
                  onPressed: () {
                    final parsed = DiceExpression.tryParse(_controller.text);
                    if (parsed == null) {
                      setState(() => _error = 'Escribe una expresión válida para guardarla.');
                      return;
                    }
                    if (!state.favorites.contains(parsed.toString())) {
                      controller.toggleFavorite(parsed.toString());
                    }
                  },
                  icon: const Icon(Icons.star_outline),
                ),
              ],
            ),
            if (_last != null)
              Padding(
                padding: const EdgeInsets.only(top: 16),
                child: DiceResultView(result: _last!),
              ),
            if (state.favorites.isNotEmpty) ...[
              const SizedBox(height: 16),
              Text('Favoritas', style: theme.textTheme.titleMedium),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  for (final favorite in state.favorites)
                    InputChip(
                      key: Key('favorite-$favorite'),
                      label: Text(favorite),
                      onPressed: () => _roll(favorite),
                      onDeleted: () => controller.toggleFavorite(favorite),
                      deleteButtonTooltipMessage: 'Quitar de favoritas',
                    ),
                ],
              ),
            ],
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(child: Text('Historial', style: theme.textTheme.titleMedium)),
                if (state.history.isNotEmpty)
                  TextButton(
                    key: const Key('dice-clear-history'),
                    onPressed: controller.clearHistory,
                    child: const Text('Vaciar'),
                  ),
              ],
            ),
            if (state.history.isEmpty)
              const Text('Aún no has tirado ningún dado.')
            else
              for (var i = 0; i < state.history.length; i++)
                _HistoryTile(
                  key: Key('history-$i'),
                  entry: state.history[i],
                  favorite: state.favorites.contains(state.history[i].expression),
                  onRoll: () => _roll(state.history[i].expression, label: state.history[i].label),
                  onFavorite: () => controller.toggleFavorite(state.history[i].expression),
                ),
          ],
        ),
      ),
    );
  }
}

class _HistoryTile extends StatelessWidget {
  const _HistoryTile({
    super.key,
    required this.entry,
    required this.favorite,
    required this.onRoll,
    required this.onFavorite,
  });

  final DiceHistoryEntry entry;
  final bool favorite;
  final VoidCallback onRoll;
  final VoidCallback onFavorite;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return ListTile(
      dense: true,
      contentPadding: EdgeInsets.zero,
      onTap: onRoll,
      leading: SizedBox(
        width: 44,
        child: Text(
          '${entry.total}',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.titleLarge?.copyWith(
            color: entry.critical
                ? Colors.green.shade700
                : entry.fumble
                ? scheme.error
                : null,
          ),
        ),
      ),
      title: Text(entry.label == null ? entry.expression : '${entry.label} · ${entry.expression}'),
      subtitle: Text(entry.detail),
      trailing: IconButton(
        tooltip: favorite ? 'Quitar de favoritas' : 'Guardar como favorita',
        icon: Icon(favorite ? Icons.star : Icons.star_outline),
        onPressed: onFavorite,
      ),
    );
  }
}
