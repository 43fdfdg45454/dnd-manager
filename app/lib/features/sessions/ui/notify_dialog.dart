import 'package:flutter/material.dart';

/// Subject and message of an email notice.
typedef NoticeData = ({String subject, String message});

/// Asks the DM for the subject and message of an immediate notice to the
/// members of the campaign. Pops a [NoticeData] or null.
class NotifyDialog extends StatefulWidget {
  const NotifyDialog({super.key});

  @override
  State<NotifyDialog> createState() => _NotifyDialogState();
}

class _NotifyDialogState extends State<NotifyDialog> {
  final _formKey = GlobalKey<FormState>();
  final _subject = TextEditingController();
  final _message = TextEditingController();

  @override
  void dispose() {
    _subject.dispose();
    _message.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop((subject: _subject.text.trim(), message: _message.text.trim()));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Enviar aviso'),
      content: SingleChildScrollView(
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('Se enviará un correo ahora a los miembros que lo tengan activado.'),
              const SizedBox(height: 12),
              TextFormField(
                key: const Key('notice-subject'),
                controller: _subject,
                maxLength: 150,
                decoration: const InputDecoration(labelText: 'Asunto'),
                validator: (v) => (v ?? '').trim().isEmpty ? 'Indica el asunto.' : null,
              ),
              TextFormField(
                key: const Key('notice-message'),
                controller: _message,
                maxLength: 5000,
                minLines: 3,
                maxLines: 6,
                decoration: const InputDecoration(
                  labelText: 'Mensaje',
                  alignLabelWithHint: true,
                ),
                validator: (v) => (v ?? '').trim().isEmpty ? 'Escribe el mensaje.' : null,
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('notice-send'),
          onPressed: _submit,
          child: const Text('Enviar'),
        ),
      ],
    );
  }
}
