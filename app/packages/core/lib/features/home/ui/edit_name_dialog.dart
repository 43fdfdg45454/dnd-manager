import 'package:flutter/material.dart';

/// Asks for the new display name. Pops the trimmed name, or null on cancel.
class EditNameDialog extends StatefulWidget {
  const EditNameDialog({super.key, required this.initialName});

  final String initialName;

  @override
  State<EditNameDialog> createState() => _EditNameDialogState();
}

class _EditNameDialogState extends State<EditNameDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _name;

  @override
  void initState() {
    super.initState();
    _name = TextEditingController(text: widget.initialName);
  }

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop(_name.text.trim());
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Editar nombre'),
      content: Form(
        key: _formKey,
        child: TextFormField(
          key: const Key('edit-name-field'),
          controller: _name,
          autofocus: true,
          maxLength: 100,
          decoration: const InputDecoration(labelText: 'Nombre visible'),
          validator: (v) => (v ?? '').trim().isEmpty ? 'El nombre no puede estar vacío.' : null,
          onFieldSubmitted: (_) => _submit(),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('edit-name-save'),
          onPressed: _submit,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}
