import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/domain/catalog_format.dart';
import '../../data/character_wizard_controller.dart';
import '../../data/models.dart';
import '../../domain/character_format.dart';
import 'step_basics.dart' show stepPadding;

/// Method selector plus one row per ability with its controls and the final
/// score (base + racial bonus) and modifier.
class AbilitiesStep extends ConsumerWidget {
  const AbilitiesStep({super.key, required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final theme = Theme.of(context);

    return ListView(
      key: const Key('step-abilities'),
      padding: stepPadding,
      children: [
        SegmentedButton<AbilityMethod>(
          key: const Key('wizard-method'),
          showSelectedIcon: false,
          segments: [
            for (final m in AbilityMethod.values) ButtonSegment(value: m, label: Text(m.label)),
          ],
          selected: {state.method},
          onSelectionChanged: (value) => controller.setMethod(value.first),
        ),
        const SizedBox(height: 12),
        if (state.method == AbilityMethod.pointBuy)
          Text(
            'Puntos restantes: ${pointBuyBudget - state.pointBuySpent} / $pointBuyBudget',
            key: const Key('wizard-pb-remaining'),
            style: theme.textTheme.titleMedium,
          ),
        if (state.method == AbilityMethod.standardArray)
          Text(
            'Asigna ${standardArray.join(', ')} una sola vez cada valor.',
            style: theme.textTheme.bodyMedium,
          ),
        const SizedBox(height: 4),
        for (final key in abilityKeys)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 2),
            child: Row(
              children: [
                Expanded(flex: 3, child: Text(abilityLabel(key))),
                switch (state.method) {
                  AbilityMethod.pointBuy => _Stepper(
                    keyPrefix: 'wizard-pb',
                    abilityKey: key,
                    value: state.pointBuyScores[key] ?? pointBuyMin,
                    onMinus: state.pointBuyScores[key]! > pointBuyMin
                        ? () => controller.changePointBuy(key, -1)
                        : null,
                    onPlus: _canRaise(state, key) ? () => controller.changePointBuy(key, 1) : null,
                  ),
                  AbilityMethod.manual => _Stepper(
                    keyPrefix: 'wizard-manual',
                    abilityKey: key,
                    value: state.manualScores[key] ?? 10,
                    onMinus: (state.manualScores[key] ?? 10) > 1
                        ? () => controller.setManualScore(key, state.manualScores[key]! - 1)
                        : null,
                    onPlus: (state.manualScores[key] ?? 10) < 20
                        ? () => controller.setManualScore(key, state.manualScores[key]! + 1)
                        : null,
                  ),
                  AbilityMethod.standardArray => _ArrayPicker(
                    abilityKey: key,
                    state: state,
                    onChanged: (value) => controller.assignArray(key, value),
                  ),
                },
                Expanded(
                  flex: 3,
                  child: _FinalScore(state: state, abilityKey: key),
                ),
              ],
            ),
          ),
        if (state.racialBonuses.isNotEmpty)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              'Bonos raciales aplicados: '
              '${state.racialBonuses.entries.map((e) => '${abilityLabel(e.key)} +${e.value}').join(', ')}',
              style: theme.textTheme.bodySmall,
            ),
          ),
      ],
    );
  }

  bool _canRaise(WizardState state, String key) {
    final score = state.pointBuyScores[key] ?? pointBuyMin;
    if (score >= pointBuyMax) return false;
    return pointBuyCost(score + 1) - pointBuyCost(score) <= pointBuyBudget - state.pointBuySpent;
  }
}

class _Stepper extends StatelessWidget {
  const _Stepper({
    required this.keyPrefix,
    required this.abilityKey,
    required this.value,
    required this.onMinus,
    required this.onPlus,
  });

  final String keyPrefix;
  final String abilityKey;
  final int value;
  final VoidCallback? onMinus;
  final VoidCallback? onPlus;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      IconButton(
        key: Key('$keyPrefix-minus-$abilityKey'),
        tooltip: 'Restar',
        visualDensity: VisualDensity.compact,
        onPressed: onMinus,
        icon: const Icon(Icons.remove_circle_outline),
      ),
      SizedBox(
        width: 28,
        child: Text(
          '$value',
          key: Key('$keyPrefix-score-$abilityKey'),
          textAlign: TextAlign.center,
        ),
      ),
      IconButton(
        key: Key('$keyPrefix-plus-$abilityKey'),
        tooltip: 'Sumar',
        visualDensity: VisualDensity.compact,
        onPressed: onPlus,
        icon: const Icon(Icons.add_circle_outline),
      ),
    ],
  );
}

/// Dropdown of the standard array: a value taken by another ability is disabled.
class _ArrayPicker extends StatelessWidget {
  const _ArrayPicker({required this.abilityKey, required this.state, required this.onChanged});

  final String abilityKey;
  final WizardState state;
  final ValueChanged<int?> onChanged;

  @override
  Widget build(BuildContext context) {
    final taken = {
      for (final e in state.arrayScores.entries)
        if (e.key != abilityKey) e.value,
    };
    return DropdownButton<int?>(
      key: Key('wizard-array-$abilityKey'),
      value: state.arrayScores[abilityKey],
      hint: const Text('—'),
      items: [
        const DropdownMenuItem<int?>(value: null, child: Text('—')),
        for (final v in standardArray)
          DropdownMenuItem<int?>(value: v, enabled: !taken.contains(v), child: Text('$v')),
      ],
      onChanged: onChanged,
    );
  }
}

/// "→ 16 (+3)" with the racial bonus hint.
class _FinalScore extends StatelessWidget {
  const _FinalScore({required this.state, required this.abilityKey});

  final WizardState state;
  final String abilityKey;

  @override
  Widget build(BuildContext context) {
    final score = state.finalScore(abilityKey);
    final bonus = state.racialBonuses[abilityKey] ?? 0;
    final text = score == null
        ? '—'
        : '$score (${formatModifier(((score - 10) / 2).floor())})'
              '${bonus == 0 ? '' : ' · raza ${bonus > 0 ? '+' : ''}$bonus'}';
    return Text(text, key: Key('wizard-final-$abilityKey'), textAlign: TextAlign.end);
  }
}
