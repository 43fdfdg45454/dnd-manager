import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_error.dart';
import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/systems/unsupported_system_ui.dart';
import '../data/characters_controller.dart';

/// Manual sheet editor of a character: loads it and shows the editor of its
/// game system ([GameSystemUi.sheetEditorSection]).
class SheetEditorPage extends ConsumerWidget {
  const SheetEditorPage({super.key, required this.characterId});

  final String characterId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(characterControllerProvider(characterId));
    return detail.when(
      skipLoadingOnReload: true,
      loading: () => Scaffold(
        appBar: AppBar(title: const Text('Editar hoja')),
        body: const Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => Scaffold(
        appBar: AppBar(title: const Text('Editar hoja')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(describeCharacterError(error), textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: () => ref.invalidate(characterControllerProvider(characterId)),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Reintentar'),
                ),
              ],
            ),
          ),
        ),
      ),
      data: (character) {
        final system = ref.watch(campaignSystemUiProvider(character.campaignId));
        return system.sheetEditorSection(SheetEditorScope(character: character)) ??
            Scaffold(
              appBar: AppBar(title: const Text('Editar hoja')),
              body: UnsupportedSystemNotice(systemId: system.id),
            );
      },
    );
  }
}
