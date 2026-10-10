import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../items/data/items_controllers.dart';
import 'characters_controller.dart';

/// Changes whenever the characters of the campaign may have changed (a
/// realtime event, a resolved rest request...). Views of a game system that
/// derive from the characters (D&D 5e: the party of the DM table) watch it so
/// the core refreshes them without knowing them.
final campaignCharactersChangedProvider = Provider.autoDispose.family<Object, String>(
  (ref, campaignId) => Object(),
);

/// Refreshes the sheet and inventory of [characterId] (every character of the
/// campaign when null), the character list of [campaignId] and whatever
/// watches [campaignCharactersChangedProvider].
void refreshCampaignCharacters(Ref ref, String campaignId, String? characterId) {
  if (characterId == null) {
    ref.invalidate(characterControllerProvider);
    ref.invalidate(inventoryControllerProvider);
  } else {
    ref.invalidate(characterControllerProvider(characterId));
    ref.invalidate(inventoryControllerProvider(characterId));
  }
  ref.invalidate(campaignCharactersControllerProvider(campaignId));
  ref.invalidate(campaignCharactersChangedProvider(campaignId));
}
