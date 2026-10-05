import 'package:flutter/material.dart';

typedef CampaignFormData = ({String name, String description});

/// Asks for the name and description of a campaign (create or edit).
/// Pops a [CampaignFormData], or null if cancelled.
class CampaignFormDialog extends StatefulWidget {
  const CampaignFormDialog({
    super.key,
    required this.title,
    required this.submitLabel,
    this.initialName = '',
    this.initialDescription = '',
  });

  final String title;
  final String submitLabel;
  final String initialName;
  final String initialDescription;

  @override
  State<CampaignFormDialog> createState() => _CampaignFormDialogState();
}

class _CampaignFormDialogState extends State<CampaignFormDialog> {
  final _formKey = GlobalKey<FormState>();
  late final _nameController = TextEditingController(text: widget.initialName);
  late final _descriptionController = TextEditingController(text: widget.initialDescription);

  @override
  void dispose() {
    _nameController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop<CampaignFormData>((
      name: _nameController.text.trim(),
      description: _descriptionController.text.trim(),
    ));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: SingleChildScrollView(
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                key: const Key('campaign-name'),
                controller: _nameController,
                maxLength: 100,
                textInputAction: TextInputAction.next,
                textCapitalization: TextCapitalization.sentences,
                decoration: const InputDecoration(labelText: 'Nombre'),
                validator: (value) =>
                    (value == null || value.trim().isEmpty) ? 'Introduce un nombre' : null,
              ),
              const SizedBox(height: 8),
              TextFormField(
                key: const Key('campaign-description'),
                controller: _descriptionController,
                maxLength: 2000,
                minLines: 3,
                maxLines: 6,
                textCapitalization: TextCapitalization.sentences,
                decoration: const InputDecoration(
                  labelText: 'Descripción',
                  alignLabelWithHint: true,
                ),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('campaign-form-submit'),
          onPressed: _submit,
          child: Text(widget.submitLabel),
        ),
      ],
    );
  }
}
