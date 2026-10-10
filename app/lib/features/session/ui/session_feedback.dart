import 'package:dio/dio.dart';
import 'package:flutter/material.dart';

import '../../../core/network/api_error.dart';
import '../../../core/systems/game_system_ui.dart' show TableCharacter;
import '../../campaigns/ui/feedback.dart';

export '../../../core/systems/game_system_ui.dart' show TableCharacter;

/// Spanish message for a failed table action (party, stash, messages): the
/// server's own reason for a 400 or 409 when it sends one, else the generic
/// texts.
String describeTableError(Object error, {Map<int, String> byStatus = const {}}) {
  final status = error is DioException ? error.response?.statusCode : null;
  if ((status == 400 || status == 409) && !byStatus.containsKey(status)) {
    final detail = problemDetail(error);
    if (detail != null) return detail;
  }
  return describeApiError(
    error,
    byStatus: {
      403: 'No tienes permiso para hacer eso en esta campaña.',
      404: 'El personaje o el objeto ya no existe, o no tienes acceso.',
      ...byStatus,
    },
  );
}

/// [runAction] with the table error texts. Returns true when the action completed.
Future<bool> runTableAction(
  BuildContext context,
  Future<void> Function() action, {
  String? success,
  Map<int, String> errors = const {},
}) => runAction(context, action, success: success, errors: errors, describe: describeTableError);

/// Lets the DM choose characters: several (checkboxes, [initial] checked) or,
/// with [single], exactly one. Pops with the chosen ids, or null.
Future<Set<String>?> pickCharacters(
  BuildContext context, {
  required String title,
  required List<TableCharacter> characters,
  required String confirmLabel,
  Set<String>? initial,
  bool single = false,
}) => showDialog<Set<String>>(
  context: context,
  builder: (_) => _CharacterPicker(
    title: title,
    characters: characters,
    confirmLabel: confirmLabel,
    initial: initial ?? (single ? const {} : {for (final c in characters) c.id}),
    single: single,
  ),
);

class _CharacterPicker extends StatefulWidget {
  const _CharacterPicker({
    required this.title,
    required this.characters,
    required this.confirmLabel,
    required this.initial,
    required this.single,
  });

  final String title;
  final List<TableCharacter> characters;
  final String confirmLabel;
  final Set<String> initial;
  final bool single;

  @override
  State<_CharacterPicker> createState() => _CharacterPickerState();
}

class _CharacterPickerState extends State<_CharacterPicker> {
  late final Set<String> _chosen = {...widget.initial};

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: SizedBox(
        width: double.maxFinite,
        child: widget.characters.isEmpty
            ? const Text('No hay personajes activos en la campaña.')
            : ListView(
                shrinkWrap: true,
                children: [
                  for (final c in widget.characters)
                    widget.single
                        ? ListTile(
                            key: Key('pick-character-${c.id}'),
                            leading: Icon(
                              _chosen.contains(c.id)
                                  ? Icons.radio_button_checked
                                  : Icons.radio_button_off,
                            ),
                            title: Text(c.name),
                            onTap: () => setState(
                              () => _chosen
                                ..clear()
                                ..add(c.id),
                            ),
                          )
                        : CheckboxListTile(
                            key: Key('pick-character-${c.id}'),
                            value: _chosen.contains(c.id),
                            title: Text(c.name),
                            onChanged: (checked) => setState(
                              () => checked == true ? _chosen.add(c.id) : _chosen.remove(c.id),
                            ),
                          ),
                ],
              ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('pick-characters-confirm'),
          onPressed: _chosen.isEmpty ? null : () => Navigator.of(context).pop({..._chosen}),
          child: Text(widget.confirmLabel),
        ),
      ],
    );
  }
}
