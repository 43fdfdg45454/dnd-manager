import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../systems/data/systems_repository.dart';
import '../../systems/domain/game_system.dart';

/// [systemId] is the game system chosen when creating, or the campaign's own
/// (unchanged) one when editing.
typedef CampaignFormData = ({String name, String description, String systemId});

/// Asks for the name and description of a campaign (create or edit) and shows
/// its game system. Creating, the system is chosen among those of the server
/// (with a dropdown only when there is more than one); editing, it is only
/// shown, since the system of a campaign never changes.
/// Pops a [CampaignFormData], or null if cancelled.
class CampaignFormDialog extends ConsumerStatefulWidget {
  const CampaignFormDialog({
    super.key,
    required this.title,
    required this.submitLabel,
    this.initialName = '',
    this.initialDescription = '',
    this.fixedSystemId,
  });

  final String title;
  final String submitLabel;
  final String initialName;
  final String initialDescription;

  /// System of the campaign being edited; null when creating one.
  final String? fixedSystemId;

  @override
  ConsumerState<CampaignFormDialog> createState() => _CampaignFormDialogState();
}

class _CampaignFormDialogState extends ConsumerState<CampaignFormDialog> {
  final _formKey = GlobalKey<FormState>();
  late final _nameController = TextEditingController(text: widget.initialName);
  late final _descriptionController = TextEditingController(text: widget.initialDescription);

  /// System picked in the dropdown; null keeps the server's default.
  String? _pickedSystemId;

  @override
  void dispose() {
    _nameController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  /// Systems of the server, or [fallbackGameSystems] while they are unknown.
  List<GameSystem> _systems() {
    final loaded = ref.watch(systemsProvider).value;
    return (loaded == null || loaded.isEmpty) ? fallbackGameSystems : loaded;
  }

  String _systemId(List<GameSystem> systems) {
    final fixed = widget.fixedSystemId;
    if (fixed != null) return fixed;
    final picked = _pickedSystemId;
    if (picked != null && systems.any((s) => s.id == picked)) return picked;
    return systems.firstWhere((s) => s.isDefault, orElse: () => systems.first).id;
  }

  void _submit(String systemId) {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop<CampaignFormData>((
      name: _nameController.text.trim(),
      description: _descriptionController.text.trim(),
      systemId: systemId,
    ));
  }

  Widget _systemField(List<GameSystem> systems, String systemId) {
    if (widget.fixedSystemId != null || systems.length < 2) {
      return Align(
        alignment: AlignmentDirectional.centerStart,
        child: Text(
          'Sistema de juego: ${gameSystemName(systems, systemId)}',
          key: const Key('campaign-system'),
          style: Theme.of(context).textTheme.bodyMedium,
        ),
      );
    }
    return DropdownButtonFormField<String>(
      key: const Key('campaign-system-select'),
      initialValue: systemId,
      isExpanded: true,
      decoration: const InputDecoration(labelText: 'Sistema de juego'),
      items: [
        for (final system in systems)
          DropdownMenuItem(
            value: system.id,
            child: Text(system.name, overflow: TextOverflow.ellipsis),
          ),
      ],
      onChanged: (value) => setState(() => _pickedSystemId = value),
    );
  }

  @override
  Widget build(BuildContext context) {
    final systems = _systems();
    final systemId = _systemId(systems);
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
              const SizedBox(height: 8),
              _systemField(systems, systemId),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('campaign-form-submit'),
          onPressed: () => _submit(systemId),
          child: Text(widget.submitLabel),
        ),
      ],
    );
  }
}
