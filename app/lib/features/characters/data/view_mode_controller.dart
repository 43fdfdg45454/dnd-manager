import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/storage/local_preferences.dart';

/// The tabs of a character page, in their order on the tab bar.
enum CharacterTab { combat, summary, skills, traits, spells, inventory, notes }

/// Preference key of the tab last shown for a character.
String characterTabKey(String characterId) => 'character.$characterId.tab';

/// Key of the old Detallado / Combate switch: read once so that a character
/// left in combat still opens on the Combate tab.
String legacyCharacterViewKey(String characterId) => 'character.$characterId.view';

/// Tab of one character, remembered per character in `shared_preferences`.
/// Defaults to [CharacterTab.summary] when nothing is stored or there is no
/// storage (then the choice lasts for the session only).
class CharacterTabController extends Notifier<CharacterTab> {
  CharacterTabController(this.characterId);

  final String characterId;

  @override
  CharacterTab build() {
    final prefs = ref.read(localPreferencesProvider);
    final stored = prefs?.getString(characterTabKey(characterId));
    for (final tab in CharacterTab.values) {
      if (tab.name == stored) return tab;
    }
    return prefs?.getString(legacyCharacterViewKey(characterId)) == 'combat'
        ? CharacterTab.combat
        : CharacterTab.summary;
  }

  void select(CharacterTab tab) {
    if (tab == state) return;
    state = tab;
    ref.read(localPreferencesProvider)?.setString(characterTabKey(characterId), tab.name).ignore();
  }
}

final characterTabProvider = NotifierProvider.family<CharacterTabController, CharacterTab, String>(
  CharacterTabController.new,
);
