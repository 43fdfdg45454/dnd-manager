import 'package:flutter/material.dart';

import '../../campaigns/domain/campaign_models.dart';

/// Result of [NewCharacterDialog]. [owner] is null when the owner is not sent
/// (players always create their own character); `(userId: null)` is an NPC.
typedef NewCharacterData = ({String name, ({String? userId})? owner});

const _npcValue = '__npc__';

/// Asks for the name of a new character and, for DMs, which player it is for.
/// A DM has no characters of their own: an NPC by default, or a player's.
class NewCharacterDialog extends StatefulWidget {
  const NewCharacterDialog({
    super.key,
    required this.members,
    required this.myUserId,
    required this.canChooseOwner,
  });

  final List<Member> members;
  final String myUserId;

  /// True for Owner/DM of the campaign.
  final bool canChooseOwner;

  @override
  State<NewCharacterDialog> createState() => _NewCharacterDialogState();
}

class _NewCharacterDialogState extends State<NewCharacterDialog> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  String _owner = _npcValue;

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    ({String? userId})? owner;
    if (widget.canChooseOwner) {
      owner = _owner == _npcValue ? (userId: null) : (userId: _owner);
    }
    Navigator.of(context).pop<NewCharacterData>((name: _nameController.text.trim(), owner: owner));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Nuevo personaje'),
      content: SingleChildScrollView(
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                key: const Key('character-name'),
                controller: _nameController,
                maxLength: 100,
                autofocus: true,
                textCapitalization: TextCapitalization.words,
                decoration: const InputDecoration(labelText: 'Nombre'),
                validator: (value) =>
                    (value == null || value.trim().isEmpty) ? 'Introduce un nombre' : null,
              ),
              if (widget.canChooseOwner) ...[
                const SizedBox(height: 8),
                DropdownButtonFormField<String>(
                  key: const Key('character-owner'),
                  initialValue: _owner,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Para'),
                  items: [
                    const DropdownMenuItem(value: _npcValue, child: Text('PNJ (sin jugador)')),
                    for (final m in widget.members)
                      if (m.role == CampaignRole.player && m.userId != widget.myUserId)
                        DropdownMenuItem(
                          value: m.userId,
                          child: Text(m.displayName, overflow: TextOverflow.ellipsis),
                        ),
                  ],
                  onChanged: (value) => setState(() => _owner = value ?? _npcValue),
                ),
              ],
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('character-create-submit'),
          onPressed: _submit,
          child: const Text('Crear'),
        ),
      ],
    );
  }
}
