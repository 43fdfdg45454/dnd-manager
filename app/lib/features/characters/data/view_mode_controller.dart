import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/storage/local_preferences.dart';

/// The two views of a character page.
enum CharacterViewMode { detailed, combat }

/// Preference key of the view chosen for a character.
String characterViewKey(String characterId) => 'character.$characterId.view';

/// View of one character, remembered per character in `shared_preferences`.
/// Defaults to [CharacterViewMode.detailed] when nothing is stored or there is
/// no storage (then the choice lasts for the session only).
class CharacterViewModeController extends Notifier<CharacterViewMode> {
  CharacterViewModeController(this.characterId);

  final String characterId;

  @override
  CharacterViewMode build() {
    final stored = ref.read(localPreferencesProvider)?.getString(characterViewKey(characterId));
    return stored == 'combat' ? CharacterViewMode.combat : CharacterViewMode.detailed;
  }

  void select(CharacterViewMode mode) {
    state = mode;
    ref
        .read(localPreferencesProvider)
        ?.setString(characterViewKey(characterId), mode.name)
        .ignore();
  }
}

final characterViewModeProvider =
    NotifierProvider.family<CharacterViewModeController, CharacterViewMode, String>(
      CharacterViewModeController.new,
    );
