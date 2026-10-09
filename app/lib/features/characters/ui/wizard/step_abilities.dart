import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/domain/catalog_format.dart';
import '../../../dice/ui/roll_input_button.dart';
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
        if (state.method == AbilityMethod.rolled) ...[
          Text(
            'Tira 4d6 seis veces, descarta el dado menor de cada tirada y escribe los totales, '
            'o usa el dado virtual.',
            key: const Key('roll-help'),
            style: theme.textTheme.bodyMedium,
          ),
          const SizedBox(height: 8),
          _RollInputs(state: state, onChanged: controller.setRollInput),
          const SizedBox(height: 8),
          if (state.rollValues != null)
            Text(
              'Valores ordenados: ${state.rollValues!.join(', ')}. Asigna cada uno una sola vez.',
              key: const Key('roll-sorted'),
              style: theme.textTheme.bodyMedium,
            ),
        ],
        const SizedBox(height: 4),
        if (state.method != AbilityMethod.rolled || state.rollValues != null)
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
                      onPlus: _canRaise(state, key)
                          ? () => controller.changePointBuy(key, 1)
                          : null,
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
                    AbilityMethod.standardArray => _ValuePicker(
                      keyName: 'wizard-array-$key',
                      values: standardArray,
                      assigned: {
                        for (final e in state.arrayScores.entries)
                          e.key: standardArray.indexOf(e.value),
                      },
                      abilityKey: key,
                      onChanged: (slot) =>
                          controller.assignArray(key, slot == null ? null : standardArray[slot]),
                    ),
                    AbilityMethod.rolled => _ValuePicker(
                      keyName: 'wizard-roll-$key',
                      values: state.rollValues ?? const [],
                      assigned: state.rollAssignment,
                      abilityKey: key,
                      onChanged: (slot) => controller.assignRoll(key, slot),
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

/// Dropdown over a list of values (standard array or sorted rolls). [assigned]
/// maps ability key -> slot in [values]; a slot taken by another ability is
/// disabled, so equal rolled values stay distinguishable.
class _ValuePicker extends StatelessWidget {
  const _ValuePicker({
    required this.keyName,
    required this.values,
    required this.assigned,
    required this.abilityKey,
    required this.onChanged,
  });

  final String keyName;
  final List<int> values;
  final Map<String, int> assigned;
  final String abilityKey;
  final ValueChanged<int?> onChanged;

  @override
  Widget build(BuildContext context) {
    final taken = {
      for (final e in assigned.entries)
        if (e.key != abilityKey) e.value,
    };
    return DropdownButton<int?>(
      key: Key(keyName),
      value: assigned[abilityKey],
      hint: const Text('—'),
      items: [
        const DropdownMenuItem<int?>(value: null, child: Text('—')),
        for (var i = 0; i < values.length; i++)
          DropdownMenuItem<int?>(
            key: Key('$keyName-slot-$i'),
            value: i,
            enabled: !taken.contains(i),
            child: Text('${values[i]}'),
          ),
      ],
      onChanged: onChanged,
    );
  }
}

/// Six numeric fields for the typed 4d6 totals with inline range validation.
/// Each one can be rolled with the virtual dice (4d6kh3), and "Tirar las seis"
/// rolls them all at once.
class _RollInputs extends ConsumerStatefulWidget {
  const _RollInputs({required this.state, required this.onChanged});

  final WizardState state;
  final void Function(int index, String text) onChanged;

  @override
  ConsumerState<_RollInputs> createState() => _RollInputsState();
}

class _RollInputsState extends ConsumerState<_RollInputs> {
  late final List<TextEditingController> _fields = [
    for (var i = 0; i < 6; i++) TextEditingController(text: widget.state.rollInputs[i]),
  ];

  @override
  void didUpdateWidget(_RollInputs oldWidget) {
    super.didUpdateWidget(oldWidget);
    // A virtual roll changes the state from outside the field.
    for (var i = 0; i < 6; i++) {
      final text = widget.state.rollInputs[i];
      if (_fields[i].text != text) _fields[i].text = text;
    }
  }

  @override
  void dispose() {
    for (final f in _fields) {
      f.dispose();
    }
    super.dispose();
  }

  void _rollAll() {
    for (var i = 0; i < 6; i++) {
      final result = rollVirtualDice(ref, '4d6kh3', label: 'Característica: tirada ${i + 1}');
      if (result != null) widget.onChanged(i, '${result.total}');
    }
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Wrap(
        spacing: 8,
        runSpacing: 8,
        children: [
          for (var i = 0; i < 6; i++)
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                SizedBox(
                  width: 88,
                  child: TextField(
                    key: Key('roll-score-$i'),
                    controller: _fields[i],
                    keyboardType: TextInputType.number,
                    inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                    decoration: InputDecoration(
                      labelText: 'Tirada ${i + 1}',
                      errorText:
                          widget.state.rollInputs[i].isEmpty ||
                              parseRollScore(widget.state.rollInputs[i]) != null
                          ? null
                          : '$rollMin a $rollMax',
                    ),
                    onChanged: (t) => widget.onChanged(i, t),
                  ),
                ),
                RollInputButton(
                  key: Key('roll-score-dice-$i'),
                  expression: '4d6kh3',
                  label: 'Característica: tirada ${i + 1}',
                  onRolled: (total, _) => widget.onChanged(i, '$total'),
                ),
              ],
            ),
        ],
      ),
      const SizedBox(height: 8),
      OutlinedButton.icon(
        key: const Key('roll-all-scores'),
        onPressed: _rollAll,
        icon: const Icon(Icons.casino_outlined),
        label: const Text('Tirar las seis'),
      ),
    ],
  );
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
