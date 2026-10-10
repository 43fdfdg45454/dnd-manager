import 'package:flutter/material.dart';

import '../domain/campaign_models.dart';
import 'member_error.dart';

typedef TransferData = ({Member to, CampaignRole previousOwnerRole});

/// Asks which member becomes the Owner and which role the current Owner keeps,
/// and runs [onSubmit] without closing: an error (such as the 409 of a member
/// who still owns characters) is shown inline. Pops the [TransferData] once
/// done, or null if cancelled.
class TransferOwnershipDialog extends StatefulWidget {
  const TransferOwnershipDialog({super.key, required this.candidates, required this.onSubmit});

  /// Members that can receive the ownership (everyone but the current Owner).
  final List<Member> candidates;

  final Future<void> Function(TransferData data) onSubmit;

  @override
  State<TransferOwnershipDialog> createState() => _TransferOwnershipDialogState();
}

class _TransferOwnershipDialogState extends State<TransferOwnershipDialog> {
  Member? _selected;
  CampaignRole _keptRole = CampaignRole.dm;
  bool _busy = false;
  String? _error;

  Future<void> _submit() async {
    final data = (to: _selected!, previousOwnerRole: _keptRole);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.onSubmit(data);
      if (mounted) Navigator.of(context).pop<TransferData>(data);
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = describeMemberError(
          error,
          byStatus: const {400: 'El destino debe ser otro miembro de la campaña.'},
        );
      });
    }
  }

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
              onChanged: (value) => setState(() {
                _selected = value;
                _error = null;
              }),
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
            if (_error != null) ...[
              const SizedBox(height: 16),
              InlineMemberError(key: const Key('transfer-error'), message: _error!),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('transfer-submit'),
          onPressed: _selected == null || _busy ? null : _submit,
          child: const Text('Transferir'),
        ),
      ],
    );
  }
}
