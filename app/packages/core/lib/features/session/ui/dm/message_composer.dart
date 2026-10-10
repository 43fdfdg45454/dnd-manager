import 'package:flutter/material.dart';

import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../session_feedback.dart';

/// What the DM wrote in the secret message composer.
typedef SecretMessage = ({List<String> characterIds, String body});

/// Composer of a secret message: the recipients (players of [characters],
/// [initial] checked) and the text. Pops with the message, or null.
Future<SecretMessage?> showMessageComposer(
  BuildContext context, {
  required List<TableCharacter> characters,
  Set<String> initial = const {},
}) => showDialog<SecretMessage>(
  context: context,
  builder: (_) => _MessageComposer(characters: characters, initial: initial),
);

class _MessageComposer extends StatefulWidget {
  const _MessageComposer({required this.characters, required this.initial});

  final List<TableCharacter> characters;
  final Set<String> initial;

  @override
  State<_MessageComposer> createState() => _MessageComposerState();
}

class _MessageComposerState extends State<_MessageComposer> {
  late final Set<String> _targets = {...widget.initial};
  final _body = TextEditingController();

  @override
  void dispose() {
    _body.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final canSend = _targets.isNotEmpty && _body.text.trim().isNotEmpty;
    return AlertDialog(
      title: const Text('Mensaje secreto'),
      content: SizedBox(
        width: double.maxFinite,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Solo lo verá el jugador de cada personaje elegido.'),
              const SizedBox(height: 8),
              if (widget.characters.isEmpty) const Text('No hay personajes activos.'),
              for (final c in widget.characters)
                CheckboxListTile(
                  key: Key('message-target-${c.id}'),
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                  value: _targets.contains(c.id),
                  title: Text(c.name),
                  onChanged: (checked) =>
                      setState(() => checked == true ? _targets.add(c.id) : _targets.remove(c.id)),
                ),
              const SizedBox(height: 8),
              TextField(
                key: const Key('message-body'),
                controller: _body,
                minLines: 3,
                maxLines: 8,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(
                  labelText: 'Mensaje',
                  helperText: 'Admite markdown',
                  border: OutlineInputBorder(),
                ),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton.icon(
          key: const Key('message-send'),
          onPressed: canSend
              ? () =>
                    Navigator.of(context)
                        .pop((characterIds: _targets.toList(), body: _body.text.trim()))
              : null,
          icon: const AppIcon(AppIcons.envelope, size: 20),
          label: const Text('Enviar'),
        ),
      ],
    );
  }
}
