import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/storage/local_preferences.dart';

/// The two main views of a character page.
enum CharacterView { combat, detail }

/// Preference key of the "Detalle" sub-tab last shown for a character.
String characterTabKey(String characterId) => 'character.$characterId.tab';

/// Preference key of the main view (Combate or Detalle) last shown.
String characterMainViewKey(String characterId) => 'character.$characterId.mainView';

/// Key of the old Detallado / Combate switch: read once so that a character
/// left in combat still opens on the Combate view.
String legacyCharacterViewKey(String characterId) => 'character.$characterId.view';

/// "Detalle" sub-tab of one character (the id of the tab), remembered per
/// character in `shared_preferences`. Null when nothing is stored or there is
/// no storage (then the choice lasts for the session only); the page then
/// opens on its first sub-tab, as it does for an id it does not know (an old
/// flat "combat" tab, or a tab of another version). The sub-tabs themselves
/// come from the game system (`GameSystemUi.detailTabs`) and the core.
class CharacterTabController extends Notifier<String?> {
  CharacterTabController(this.characterId);

  final String characterId;

  @override
  String? build() => ref.read(localPreferencesProvider)?.getString(characterTabKey(characterId));

  /// Remembers the "Detalle" sub-tab [tabId].
  void select(String tabId) {
    if (tabId == state) return;
    state = tabId;
    ref.read(localPreferencesProvider)?.setString(characterTabKey(characterId), tabId).ignore();
  }
}

final characterTabProvider = NotifierProvider.family<CharacterTabController, String?, String>(
  CharacterTabController.new,
);

/// Main view (Combate or Detalle) of one character, remembered per character.
/// Without a stored view it is derived from older preferences: the flat
/// "combat" tab or the old Combate switch open on Combate; anything else on
/// Detalle.
class CharacterViewController extends Notifier<CharacterView> {
  CharacterViewController(this.characterId);

  final String characterId;

  @override
  CharacterView build() {
    final prefs = ref.read(localPreferencesProvider);
    final stored = prefs?.getString(characterMainViewKey(characterId));
    for (final view in CharacterView.values) {
      if (view.name == stored) return view;
    }
    final oldTab = prefs?.getString(characterTabKey(characterId));
    if (oldTab == 'combat') return CharacterView.combat;
    if (oldTab != null) return CharacterView.detail;
    return prefs?.getString(legacyCharacterViewKey(characterId)) == 'combat'
        ? CharacterView.combat
        : CharacterView.detail;
  }

  void select(CharacterView view) {
    if (view == state) return;
    state = view;
    ref
        .read(localPreferencesProvider)
        ?.setString(characterMainViewKey(characterId), view.name)
        .ignore();
  }
}

final characterViewProvider =
    NotifierProvider.family<CharacterViewController, CharacterView, String>(
      CharacterViewController.new,
    );

/// Whether "Detalle" of the player's session shows its "Sesión" sub-tab
/// (true, the default) or the remembered sub-tab of the sheet. Kept in memory
/// per character.
class PlayerSessionTabController extends Notifier<bool> {
  PlayerSessionTabController(this.characterId);

  final String characterId;

  @override
  bool build() => true;

  void select(bool onSession) => state = onSession;
}

final playerSessionTabProvider = NotifierProvider.family<PlayerSessionTabController, bool, String>(
  PlayerSessionTabController.new,
);
