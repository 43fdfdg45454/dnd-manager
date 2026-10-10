import 'package:flutter/material.dart';

import '../../../core/auth/user_dto.dart';
import '../../auth/ui/validators.dart';

typedef NewUserData = ({String email, String displayName, UserRole role});

/// Asks for the data of a new user. Pops a [NewUserData], or null if cancelled.
class CreateUserDialog extends StatefulWidget {
  const CreateUserDialog({super.key});

  @override
  State<CreateUserDialog> createState() => _CreateUserDialogState();
}

class _CreateUserDialogState extends State<CreateUserDialog> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _nameController = TextEditingController();
  UserRole _role = UserRole.user;

  @override
  void dispose() {
    _emailController.dispose();
    _nameController.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop<NewUserData>((
      email: _emailController.text.trim(),
      displayName: _nameController.text.trim(),
      role: _role,
    ));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Nuevo usuario'),
      content: SingleChildScrollView(
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                key: const Key('new-user-email'),
                controller: _emailController,
                keyboardType: TextInputType.emailAddress,
                textInputAction: TextInputAction.next,
                decoration: const InputDecoration(labelText: 'Correo electrónico'),
                validator: validateEmail,
              ),
              const SizedBox(height: 8),
              TextFormField(
                key: const Key('new-user-name'),
                controller: _nameController,
                textInputAction: TextInputAction.done,
                textCapitalization: TextCapitalization.words,
                decoration: const InputDecoration(labelText: 'Nombre'),
                validator: (value) =>
                    (value == null || value.trim().isEmpty) ? 'Introduce un nombre' : null,
              ),
              const SizedBox(height: 16),
              SegmentedButton<UserRole>(
                key: const Key('new-user-role'),
                showSelectedIcon: false,
                segments: [
                  for (final role in UserRole.values)
                    ButtonSegment(value: role, label: Text(role.label)),
                ],
                selected: {_role},
                onSelectionChanged: (selection) => setState(() => _role = selection.first),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('new-user-submit'),
          onPressed: _submit,
          child: const Text('Crear'),
        ),
      ],
    );
  }
}
