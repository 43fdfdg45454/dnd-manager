import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:opentrpg_core/core/theme/components.dart';
import 'package:opentrpg_core/core/ui/selection_grid.dart';

import '../../../catalog/data/models.dart' show BackgroundTable;
import '../../data/character_wizard_controller.dart';
import '../../domain/character_format.dart';
import 'step_basics.dart' show stepPadding;

// ---------------------------------------------------------------------------
// 6. Personality (traits, ideal, bond, flaw and the optional table)
// ---------------------------------------------------------------------------

/// Personality of the background: each block can be rolled ("Tirar dN"),
/// picked from the table of the background or written by hand. Optional:
/// "Siguiente" only warns when something is missing.
class PersonalityStep extends ConsumerWidget {
  const PersonalityStep({super.key, required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final background = state.background;
    final theme = Theme.of(context);
    final warning = state.personalityWarning;

    return ListView(
      key: const Key('step-personality'),
      padding: stepPadding,
      children: [
        Text(
          background?.personality == null
              ? 'Escribe dos rasgos de personalidad, un ideal, un vínculo y un defecto.'
              : 'Tira o elige en las tablas de ${background!.name}, o escribe tu propio texto.',
          style: theme.textTheme.bodyMedium,
        ),
        for (final kind in PersonalityKind.values)
          _PersonalityBlock(
            kind: kind,
            entries: state.personalityEntries(kind),
            alignments: kind == PersonalityKind.ideal
                ? [for (final i in background?.personality?.ideals ?? const []) i.alignment]
                : const [],
            values: state.personalityOf(kind),
            onRoll: () => controller.rollPersonality(kind),
            onToggle: (entry) => controller.togglePersonalityEntry(kind, entry),
            onText: (slot, text) => controller.setPersonalityText(kind, slot, text),
          ),
        for (final table in background?.optionalTables ?? const <BackgroundTable>[])
          _TableBlock(
            table: table,
            detail: state.backgroundDetail,
            onRoll: () => controller.rollBackgroundTable(table),
            onToggle: (entry) => controller.toggleBackgroundTableEntry(table, entry),
          ),
        if (background?.optionalTables.isNotEmpty ?? false) ...[
          const SizedBox(height: 8),
          _SyncedTextField(
            fieldKey: const Key('personality-text-detail'),
            value: state.backgroundDetail,
            label: 'Detalle del trasfondo',
            maxLength: backgroundDetailMaxLength,
            onChanged: controller.setBackgroundDetail,
          ),
        ],
        if (warning != null)
          Padding(
            padding: const EdgeInsets.only(top: 12),
            child: Text(
              warning,
              key: const Key('personality-warning'),
              style: theme.textTheme.bodySmall,
            ),
          ),
      ],
    );
  }
}

/// One block: header with the roll button, the table as one column of
/// selectable cards and a text field per slot.
class _PersonalityBlock extends StatelessWidget {
  const _PersonalityBlock({
    required this.kind,
    required this.entries,
    required this.alignments,
    required this.values,
    required this.onRoll,
    required this.onToggle,
    required this.onText,
  });

  final PersonalityKind kind;
  final List<String> entries;

  /// Alignment of each entry (ideals only).
  final List<String?> alignments;
  final List<String> values;
  final VoidCallback onRoll;
  final ValueChanged<String> onToggle;
  final void Function(int slot, String text) onText;

  @override
  Widget build(BuildContext context) {
    final full = values.every((v) => v.trim().isNotEmpty);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(
          kind.slots > 1 ? '${kind.label} (${kind.slots})' : kind.label,
          padding: const EdgeInsets.only(top: 20, bottom: 4),
          trailing: entries.isEmpty
              ? null
              : TextButton.icon(
                  key: Key('personality-roll-${kind.name}'),
                  onPressed: onRoll,
                  icon: const Icon(Icons.casino_outlined, size: 18),
                  label: Text('Tirar d${entries.length}'),
                ),
        ),
        for (var i = 0; i < entries.length; i++)
          Padding(
            padding: const EdgeInsets.only(bottom: 6),
            child: SelectionTile(
              key: Key('personality-${kind.name}-$i'),
              wrap: true,
              label: '${i + 1}. ${entries[i]}',
              caption: i < alignments.length && alignments[i] != null
                  ? idealAlignmentLabel(alignments[i]!)
                  : null,
              state: values.contains(entries[i])
                  ? SelectionState.selected
                  : full && kind.slots > 1
                  ? SelectionState.blocked
                  : SelectionState.available,
              onTap: !values.contains(entries[i]) && full && kind.slots > 1
                  ? null
                  : () => onToggle(entries[i]),
            ),
          ),
        for (var slot = 0; slot < values.length; slot++)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: _SyncedTextField(
              fieldKey: Key('personality-text-${kind.name}-$slot'),
              value: values[slot],
              label: kind.slots > 1 ? 'Rasgo ${slot + 1}' : kind.label,
              maxLength: personalityTextMaxLength,
              onChanged: (text) => onText(slot, text),
            ),
          ),
      ],
    );
  }
}

/// Optional table of the background: one entry becomes the background detail.
class _TableBlock extends StatelessWidget {
  const _TableBlock({
    required this.table,
    required this.detail,
    required this.onRoll,
    required this.onToggle,
  });

  final BackgroundTable table;
  final String detail;
  final VoidCallback onRoll;
  final ValueChanged<String> onToggle;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(
          table.name,
          padding: const EdgeInsets.only(top: 20, bottom: 4),
          trailing: table.entries.isEmpty
              ? null
              : TextButton.icon(
                  key: Key('personality-roll-table-${table.key}'),
                  onPressed: onRoll,
                  icon: const Icon(Icons.casino_outlined, size: 18),
                  label: Text('Tirar d${table.entries.length}'),
                ),
        ),
        for (var i = 0; i < table.entries.length; i++)
          Padding(
            padding: const EdgeInsets.only(bottom: 6),
            child: SelectionTile(
              key: Key('personality-table-${table.key}-$i'),
              wrap: true,
              label: '${i + 1}. ${table.entries[i]}',
              state: detail == backgroundTableDetail(table, table.entries[i])
                  ? SelectionState.selected
                  : SelectionState.available,
              onTap: () => onToggle(table.entries[i]),
            ),
          ),
      ],
    );
  }
}

/// A text field that follows [value] when it changes from outside (a roll or
/// a tapped entry) without losing the cursor while the user types.
class _SyncedTextField extends StatefulWidget {
  const _SyncedTextField({
    required this.fieldKey,
    required this.value,
    required this.label,
    required this.maxLength,
    required this.onChanged,
  });

  final Key fieldKey;
  final String value;
  final String label;
  final int maxLength;
  final ValueChanged<String> onChanged;

  @override
  State<_SyncedTextField> createState() => _SyncedTextFieldState();
}

class _SyncedTextFieldState extends State<_SyncedTextField> {
  late final _controller = TextEditingController(text: widget.value);

  @override
  void didUpdateWidget(_SyncedTextField oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.value != _controller.text) {
      _controller.value = TextEditingValue(
        text: widget.value,
        selection: TextSelection.collapsed(offset: widget.value.length),
      );
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => TextField(
    key: widget.fieldKey,
    controller: _controller,
    minLines: 1,
    maxLines: 4,
    maxLength: widget.maxLength,
    decoration: InputDecoration(labelText: widget.label, counterText: ''),
    onChanged: widget.onChanged,
  );
}
