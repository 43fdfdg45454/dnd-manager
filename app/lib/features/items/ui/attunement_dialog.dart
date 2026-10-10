import 'package:flutter/material.dart';

import '../../../core/network/api_error.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/models.dart';
import '../../../systems/dnd5e/items/dnd5e_item.dart';

/// Code the server sends (409) when the character is already attuned to three
/// items.
const attunementLimitCode = 'attunement-limit';

/// True when [error] is the server's "already attuned to three items" conflict.
bool isAttunementLimit(Object error) => problemCode(error) == attunementLimitCode;

/// "Elige cuál dejar": the attuned items of the character, one of which has to
/// stop being attuned for [newItemName] to take its place. Resolves to the
/// chosen item, or null when cancelled.
Future<CharacterItem?> showAttunementReplaceDialog(
  BuildContext context, {
  required List<CharacterItem> attuned,
  required String newItemName,
}) => showDialog<CharacterItem>(
  context: context,
  builder: (dialogContext) => AlertDialog(
    key: const Key('attunement-dialog'),
    title: const Text('Elige cuál dejar'),
    content: SingleChildScrollView(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Solo puedes estar sintonizado con $maxAttunedItems objetos a la vez. '
            'Deja uno para sintonizar "$newItemName".',
          ),
          const SizedBox(height: 8),
          for (final item in attuned)
            ListTile(
              key: Key('attunement-drop-${item.id}'),
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.link_off),
              title: Text(item.effective.name),
              onTap: () => Navigator.of(dialogContext).pop(item),
            ),
        ],
      ),
    ),
    actions: [
      TextButton(
        key: const Key('attunement-cancel'),
        onPressed: () => Navigator.of(dialogContext).pop(),
        child: const Text('Cancelar'),
      ),
    ],
  ),
);

/// Attunes to [item]. When the limit is reached it opens [showAttunementReplaceDialog]
/// with [attunedItems] and retries the request dropping the chosen one.
/// [write] sends the patch; returns true once the item is attuned.
Future<bool> attuneWithReplacement(
  BuildContext context, {
  required CharacterItem item,
  required List<CharacterItem> attunedItems,
  required Future<void> Function(InventoryPatch patch) write,
  String success = 'Objeto sintonizado.',
}) async {
  final messenger = ScaffoldMessenger.of(context);
  void show(String message) => messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
  try {
    await write(const InventoryPatch(attuned: true));
    show(success);
    return true;
  } catch (error) {
    if (!isAttunementLimit(error)) {
      show(describeItemError(error));
      return false;
    }
  }
  if (!context.mounted) return false;
  final dropped = await showAttunementReplaceDialog(
    context,
    attuned: [
      for (final a in attunedItems)
        if (a.id != item.id && a.attuned) a,
    ],
    newItemName: item.effective.name,
  );
  if (dropped == null || !context.mounted) return false;
  return runAction(
    context,
    () => write(InventoryPatch(attuned: true, replaceAttunedItemId: dropped.id)),
    success: '${item.effective.name} sintonizado; has dejado ${dropped.effective.name}.',
    describe: describeItemError,
  );
}
