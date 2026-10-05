import 'package:flutter/material.dart';

import '../../../core/content/content_visibility.dart';
import '../../lore/data/models.dart';
import '../data/models.dart';

/// Create or edit a pin: title, note (markdown), icon, colour, lore entry and
/// visibility. Pops a [PinDraft].
class PinFormDialog extends StatefulWidget {
  const PinFormDialog({super.key, required this.title, this.initial, this.loreEntries = const []});

  final String title;
  final PinDraft? initial;

  /// Entries a pin can link to.
  final List<LoreSummary> loreEntries;

  @override
  State<PinFormDialog> createState() => _PinFormDialogState();
}

class _PinFormDialogState extends State<PinFormDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title = TextEditingController(text: widget.initial?.title);
  late final TextEditingController _note = TextEditingController(text: widget.initial?.note);
  late PinIcon _icon = widget.initial?.icon ?? PinIcon.place;
  late String? _color = widget.initial?.color;
  late String? _loreEntryId = widget.initial?.loreEntryId;
  late ContentVisibility _visibility = widget.initial?.visibility ?? ContentVisibility.players;

  @override
  void dispose() {
    _title.dispose();
    _note.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop(
      PinDraft(
        title: _title.text.trim(),
        note: _note.text.trim(),
        icon: _icon,
        color: _color,
        loreEntryId: _loreEntryId,
        visibility: _visibility,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final lore = [...widget.loreEntries]
      ..sort((a, b) => a.title.toLowerCase().compareTo(b.title.toLowerCase()));
    final loreValue = lore.any((e) => e.id == _loreEntryId) ? _loreEntryId : null;
    return AlertDialog(
      title: Text(widget.title),
      content: SizedBox(
        width: double.maxFinite,
        child: Form(
          key: _formKey,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                TextFormField(
                  key: const Key('pin-field-title'),
                  controller: _title,
                  autofocus: true,
                  maxLength: 100,
                  decoration: const InputDecoration(labelText: 'Título'),
                  validator: (v) => (v ?? '').trim().isEmpty ? 'Escribe un título.' : null,
                ),
                TextFormField(
                  key: const Key('pin-field-note'),
                  controller: _note,
                  minLines: 2,
                  maxLines: 5,
                  maxLength: 10000,
                  decoration: const InputDecoration(
                    labelText: 'Nota (markdown)',
                    alignLabelWithHint: true,
                  ),
                ),
                const SizedBox(height: 8),
                Text('Icono', style: Theme.of(context).textTheme.labelLarge),
                const SizedBox(height: 4),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [
                    for (final icon in PinIcon.values)
                      ChoiceChip(
                        key: Key('pin-icon-${icon.apiValue}'),
                        avatar: Icon(icon.icon, size: 18),
                        label: Text(icon.label),
                        selected: _icon == icon,
                        onSelected: (_) => setState(() => _icon = icon),
                      ),
                  ],
                ),
                const SizedBox(height: 12),
                Text('Color', style: Theme.of(context).textTheme.labelLarge),
                const SizedBox(height: 4),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    _ColorDot(
                      key: const Key('pin-color-none'),
                      color: null,
                      selected: _color == null,
                      onTap: () => setState(() => _color = null),
                    ),
                    for (final hex in pinColors)
                      _ColorDot(
                        key: Key('pin-color-$hex'),
                        color: parsePinColor(hex),
                        selected: _color == hex,
                        onTap: () => setState(() => _color = hex),
                      ),
                  ],
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String?>(
                  key: const Key('pin-field-lore'),
                  initialValue: loreValue,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Entrada de lore'),
                  items: [
                    const DropdownMenuItem<String?>(value: null, child: Text('Ninguna')),
                    for (final e in lore)
                      DropdownMenuItem<String?>(
                        value: e.id,
                        child: Text(e.title, overflow: TextOverflow.ellipsis),
                      ),
                  ],
                  onChanged: (v) => setState(() => _loreEntryId = v),
                ),
                DropdownButtonFormField<ContentVisibility>(
                  key: const Key('pin-field-visibility'),
                  initialValue: _visibility,
                  decoration: const InputDecoration(labelText: 'Visibilidad'),
                  items: [
                    for (final v in ContentVisibility.values)
                      DropdownMenuItem(value: v, child: Text(v.label)),
                  ],
                  onChanged: (v) => setState(() => _visibility = v ?? _visibility),
                ),
              ],
            ),
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('pin-form-submit'),
          onPressed: _submit,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}

class _ColorDot extends StatelessWidget {
  const _ColorDot({super.key, required this.color, required this.selected, required this.onTap});

  final Color? color;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return InkResponse(
      onTap: onTap,
      child: Container(
        width: 30,
        height: 30,
        decoration: BoxDecoration(
          color: color,
          shape: BoxShape.circle,
          border: Border.all(
            color: selected ? scheme.primary : scheme.outline,
            width: selected ? 3 : 1,
          ),
        ),
        child: color == null ? Icon(Icons.block, size: 16, color: scheme.outline) : null,
      ),
    );
  }
}
