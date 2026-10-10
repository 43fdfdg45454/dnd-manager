import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../catalog/data/models.dart' show HeightWeightTable;
import '../../dice/data/dice_controller.dart' show diceRandomProvider;
import '../domain/height_weight.dart';

/// "Altura y peso" section: height (inches) and weight (pounds) fields with the
/// metric conversion below and, when the race or subrace has a table, a "Tirar"
/// button that rolls it with the app's dice engine, fills both fields and shows
/// the roll in a SnackBar. Free data without mechanical effect.
///
/// Keys: `<keyPrefix>-height`, `<keyPrefix>-weight`,
/// `<keyPrefix>-roll-height-weight` and `<keyPrefix>-height-weight-preview`.
class HeightWeightFields extends ConsumerStatefulWidget {
  const HeightWeightFields({
    super.key,
    required this.keyPrefix,
    required this.initialHeight,
    required this.initialWeight,
    required this.onHeightChanged,
    required this.onWeightChanged,
    this.table,
  });

  final String keyPrefix;
  final String initialHeight;
  final String initialWeight;
  final ValueChanged<String> onHeightChanged;
  final ValueChanged<String> onWeightChanged;

  /// Table of the race or subrace; null hides the "Tirar" button.
  final HeightWeightTable? table;

  @override
  ConsumerState<HeightWeightFields> createState() => _HeightWeightFieldsState();
}

class _HeightWeightFieldsState extends ConsumerState<HeightWeightFields> {
  late final _height = TextEditingController(text: widget.initialHeight);
  late final _weight = TextEditingController(text: widget.initialWeight);

  @override
  void dispose() {
    _height.dispose();
    _weight.dispose();
    super.dispose();
  }

  void _roll(HeightWeightTable table) {
    final roll = rollHeightWeight(table, ref.read(diceRandomProvider));
    if (roll == null) return;
    _height.text = '${roll.heightInches}';
    _weight.text = '${roll.weightPounds}';
    widget.onHeightChanged(_height.text);
    widget.onWeightChanged(_weight.text);
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(roll.summary)));
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final table = widget.table;
    final prefix = widget.keyPrefix;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text('Altura y peso', style: theme.textTheme.titleSmall)),
            if (table != null)
              TextButton.icon(
                key: Key('$prefix-roll-height-weight'),
                onPressed: () => _roll(table),
                icon: const Icon(Icons.casino_outlined),
                label: const Text('Tirar'),
              ),
          ],
        ),
        if (table != null)
          Text(
            'Tabla de la raza: ${describeHeightWeightTable(table)}',
            style: theme.textTheme.bodySmall,
          ),
        const SizedBox(height: 4),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: TextFormField(
                key: Key('$prefix-height'),
                controller: _height,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                autovalidateMode: AutovalidateMode.onUserInteraction,
                decoration: const InputDecoration(
                  labelText: 'Altura (pulgadas)',
                  helperText: '1 pie = 12 pulgadas',
                ),
                onChanged: widget.onHeightChanged,
                validator: (v) => measureError(
                  v ?? '',
                  min: minHeightInches,
                  max: maxHeightInches,
                  label: 'La altura',
                  unit: 'pulgadas',
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: TextFormField(
                key: Key('$prefix-weight'),
                controller: _weight,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                autovalidateMode: AutovalidateMode.onUserInteraction,
                decoration: const InputDecoration(labelText: 'Peso (libras)'),
                onChanged: widget.onWeightChanged,
                validator: (v) => measureError(
                  v ?? '',
                  min: minWeightPounds,
                  max: maxWeightPounds,
                  label: 'El peso',
                  unit: 'libras',
                ),
              ),
            ),
          ],
        ),
        ListenableBuilder(
          listenable: Listenable.merge([_height, _weight]),
          builder: (context, _) {
            final text = formatHeightAndWeight(
              parseMeasure(_height.text, min: minHeightInches, max: maxHeightInches),
              parseMeasure(_weight.text, min: minWeightPounds, max: maxWeightPounds),
            );
            return text == null
                ? const SizedBox.shrink()
                : Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(
                      text,
                      key: Key('$prefix-height-weight-preview'),
                      style: theme.textTheme.bodySmall,
                    ),
                  );
          },
        ),
      ],
    );
  }
}
