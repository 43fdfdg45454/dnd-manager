import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../domain/measures.dart';

/// "Altura y peso" section: height (inches) and weight (pounds) fields with the
/// metric conversion below. The header comes from the game system of
/// [campaignId] ([GameSystemUi.heightWeightRoller]; D&D 5e: a "Tirar" button
/// that rolls the table of the race or subrace and fills both fields). Free
/// data without mechanical effect.
///
/// Keys: `<keyPrefix>-height`, `<keyPrefix>-weight` and
/// `<keyPrefix>-height-weight-preview` (D&D 5e adds
/// `<keyPrefix>-roll-height-weight`).
class HeightWeightFields extends ConsumerStatefulWidget {
  const HeightWeightFields({
    super.key,
    required this.campaignId,
    required this.keyPrefix,
    required this.initialHeight,
    required this.initialWeight,
    required this.onHeightChanged,
    required this.onWeightChanged,
    this.raceIndex,
    this.subraceIndex,
  });

  final String campaignId;
  final String keyPrefix;
  final String initialHeight;
  final String initialWeight;
  final ValueChanged<String> onHeightChanged;
  final ValueChanged<String> onWeightChanged;

  /// Race and subrace of the character, for the tables of the system.
  final String? raceIndex;
  final String? subraceIndex;

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

  void _onRolled(int heightInches, int weightPounds) {
    _height.text = '$heightInches';
    _weight.text = '$weightPounds';
    widget.onHeightChanged(_height.text);
    widget.onWeightChanged(_weight.text);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final prefix = widget.keyPrefix;
    final title = Text('Altura y peso', style: theme.textTheme.titleSmall);
    final header = ref
        .watch(campaignSystemUiProvider(widget.campaignId))
        .heightWeightRoller(
          HeightWeightScope(
            title: title,
            keyPrefix: prefix,
            raceIndex: widget.raceIndex,
            subraceIndex: widget.subraceIndex,
            onRolled: _onRolled,
          ),
        );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        header ?? Row(children: [Expanded(child: title)]),
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
