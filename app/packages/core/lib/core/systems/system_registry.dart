import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/campaigns/data/campaigns_controller.dart';
import '../../features/characters/data/characters_controller.dart';
import '../../features/systems/domain/game_system.dart';
import 'game_system_ui.dart';
import 'unsupported_system_ui.dart';

/// The game systems this build of the app brings. The host registers them
/// once, overriding this provider at the root `ProviderScope`
/// (`main.dart`: `gameSystemsProvider.overrideWithValue([Dnd5eUi()])`); the
/// core never imports a system. Empty by default.
final gameSystemsProvider = Provider<List<GameSystemUi>>((ref) => const []);

/// The registered system [systemId], or an [UnsupportedSystemUi] that says
/// the app does not include it.
final gameSystemUiProvider = Provider.family<GameSystemUi, String>((ref, systemId) {
  for (final system in ref.watch(gameSystemsProvider)) {
    if (system.id == systemId) return system;
  }
  return UnsupportedSystemUi(systemId);
});

/// The system of campaigns that do not say otherwise ([defaultGameSystemId]),
/// or the first registered one. Global screens (dice, compendium, the
/// breakdown sheet) use it.
final defaultGameSystemUiProvider = Provider<GameSystemUi>((ref) {
  final systems = ref.watch(gameSystemsProvider);
  for (final system in systems) {
    if (system.id == defaultGameSystemId) return system;
  }
  return systems.isEmpty ? const UnsupportedSystemUi(defaultGameSystemId) : systems.first;
});

/// The system of the campaign [campaignId]. While the campaign is loading
/// (or cannot be loaded) it is the default system, like the campaigns cached
/// before the server sent `systemId`.
final campaignSystemUiProvider = Provider.autoDispose.family<GameSystemUi, String>((
  ref,
  campaignId,
) {
  final systemId = ref.watch(
    campaignDetailControllerProvider(campaignId).select((campaign) => campaign.value?.systemId),
  );
  return systemId == null
      ? ref.watch(defaultGameSystemUiProvider)
      : ref.watch(gameSystemUiProvider(systemId));
});

/// The system of the campaign of the character [characterId] (the default
/// one while the character is loading).
final characterSystemUiProvider = Provider.autoDispose.family<GameSystemUi, String>((
  ref,
  characterId,
) {
  final campaignId = ref.watch(
    characterControllerProvider(characterId).select((character) => character.value?.campaignId),
  );
  return campaignId == null
      ? ref.watch(defaultGameSystemUiProvider)
      : ref.watch(campaignSystemUiProvider(campaignId));
});
