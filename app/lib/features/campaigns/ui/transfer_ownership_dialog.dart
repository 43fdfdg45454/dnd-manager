import 'package:flutter/material.dart';

import '../domain/campaign_models.dart';

typedef TransferData = ({Member to, CampaignRole previousOwnerRole});

/// Asks which member becomes the Owner and which role the current Owner keeps.
/// Pops a [TransferData], or null if cancelled.
class TransferOwnershipDialog extends StatefulWidget {
  const TransferOwnershipDialog({super.key, required this.candidates});

  /// Members that can receive the ownership (everyone but the current Owner).
  final List<Member> candidates;

  @override
  State<TransferOwnershipDialog> createState() => _TransferOwnershipDialogState();
}

class _TransferOwnershipDialogState extends State<TransferOwnershipDialog> {
  Member? _selected;
  CampaignRole _keptRole = CampaignRole.dm;

  @override
  void initState() {
    super.initState();
    if (widget.candidates.isNotEmpty) _selected = widget.candidates.first;
  }

  @override
  Widget build(BuildContext context) {
    if (widget.candidates.isEmpty) {
      return AlertDialog(
        title: const Text('Transferir propiedad'),
        content: const Text(
          'Añade antes a otro miembro de la campaña para poder transferirle la propiedad.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cerrar')),
        ],
      );
    }
    return AlertDialog(
      title: const Text('Transferir propiedad'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            DropdownButtonFormField<Member>(
              key: const Key('transfer-member'),
              initialValue: _selected,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Nuevo dueño'),
              items: [
                for (final m in widget.candidates)
                  DropdownMenuItem(
                    value: m,
                    child: Text(
                      '${m.displayName} (${m.role.label})',
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
              ],
              onChanged: (value) => setState(() => _selected = value),
            ),
            const SizedBox(height: 16),
            const Text('Tu rol después de transferir'),
            const SizedBox(height: 8),
            SegmentedButton<CampaignRole>(
              key: const Key('transfer-role'),
              showSelectedIcon: false,
              segments: const [
                ButtonSegment(value: CampaignRole.dm, label: Text('DM')),
                ButtonSegment(value: CampaignRole.player, label: Text('Jugador')),
              ],
              selected: {_keptRole},
              onSelectionChanged: (selection) => setState(() => _keptRole = selection.first),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('transfer-submit'),
          onPressed: _selected == null
              ? null
              : () =>
                    Navigator.of(context)
                        .pop<TransferData>((to: _selected!, previousOwnerRole: _keptRole)),
          child: const Text('Transferir'),
        ),
      ],
    );
  }
}
