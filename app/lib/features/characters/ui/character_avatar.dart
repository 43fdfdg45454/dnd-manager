import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/files/authenticated_image.dart';
import '../../../core/files/image_upload.dart';
import '../../../core/files/stored_file.dart';
import '../../../core/network/api_error.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/characters_controller.dart';
import '../data/models.dart';

enum _PortraitAction { pick, remove }

/// The portrait of a character, or its initial when it has none. When
/// [canEdit] (owner or DM) tapping it offers to pick a new portrait from the
/// gallery or remove the current one.
class CharacterAvatar extends ConsumerWidget {
  const CharacterAvatar({
    super.key,
    required this.character,
    this.radius = 36,
    this.canEdit = false,
  });

  final CharacterDetail character;
  final double radius;
  final bool canEdit;

  Future<void> _change(BuildContext context, WidgetRef ref) async {
    final action = await showModalBottomSheet<_PortraitAction>(
      context: context,
      showDragHandle: true,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              key: const Key('portrait-pick'),
              leading: const Icon(Icons.photo_library_outlined),
              title: const Text('Elegir de la galería'),
              onTap: () => Navigator.of(sheetContext).pop(_PortraitAction.pick),
            ),
            if (character.portraitUrl != null)
              ListTile(
                key: const Key('portrait-remove'),
                leading: const Icon(Icons.hide_image_outlined),
                title: const Text('Quitar retrato'),
                onTap: () => Navigator.of(sheetContext).pop(_PortraitAction.remove),
              ),
          ],
        ),
      ),
    );
    if (action == null || !context.mounted) return;
    final controller = ref.read(characterControllerProvider(character.id).notifier);
    switch (action) {
      case _PortraitAction.pick:
        final stored = await pickAndUploadImage(
          context,
          ref,
          kind: FileKind.portrait,
          campaignId: character.campaignId,
          characterId: character.id,
          maxWidth: 1024,
        );
        if (stored == null || !context.mounted) return;
        await runAction(
          context,
          () => controller.setPortrait(stored.id),
          success: 'Retrato actualizado.',
          describe: describeContentError,
        );
      case _PortraitAction.remove:
        await runAction(
          context,
          () => controller.setPortrait(null),
          success: 'Retrato eliminado.',
          describe: describeContentError,
        );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scheme = Theme.of(context).colorScheme;
    final url = character.portraitUrl;
    final initial = character.name.isEmpty ? '?' : character.name[0].toUpperCase();
    final avatar = SizedBox(
      width: radius * 2,
      height: radius * 2,
      child: ClipOval(
        child: url == null
            ? ColoredBox(
                color: scheme.primaryContainer,
                child: Center(
                  child: Text(
                    initial,
                    style: TextStyle(fontSize: radius * 0.9, color: scheme.onPrimaryContainer),
                  ),
                ),
              )
            : AuthenticatedImage(url: url, width: radius * 2, height: radius * 2, compact: true),
      ),
    );
    if (!canEdit) return KeyedSubtree(key: const Key('character-avatar'), child: avatar);
    return Tooltip(
      message: 'Cambiar retrato',
      child: InkResponse(
        key: const Key('character-avatar'),
        onTap: () => _change(context, ref),
        radius: radius + 6,
        child: Stack(
          children: [
            avatar,
            Positioned(
              right: 0,
              bottom: 0,
              child: CircleAvatar(
                radius: 11,
                backgroundColor: scheme.secondaryContainer,
                child: Icon(
                  Icons.photo_camera_outlined,
                  size: 13,
                  color: scheme.onSecondaryContainer,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
