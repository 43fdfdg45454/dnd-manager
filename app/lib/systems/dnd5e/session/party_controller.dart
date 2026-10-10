import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../features/characters/data/character_refresh.dart';
import '../../../features/characters/data/characters_controller.dart';
import '../../../features/characters/data/characters_repository.dart';
import '../../../features/session/data/party_repository.dart';
import '../characters/models.dart' show DamageOutcome;
import 'party_models.dart';

/// Errors are shown with a retry button instead of being retried silently.
Duration? _noRetry(int retryCount, Object error) => null;

// ---------------------------------------------------------------------------
// Party
// ---------------------------------------------------------------------------

/// The active characters of a campaign as the DM sees them. Every mutation
/// rethrows errors for the UI and refreshes the sheets it touched.
class PartyController extends AsyncNotifier<List<PartyMember>> {
  PartyController(this.campaignId);

  final String campaignId;

  PartyRepository get _repository => ref.read(partyRepositoryProvider);

  @override
  Future<List<PartyMember>> build() {
    // Refreshed with the characters of the campaign (realtime events,
    // resolved rest requests...).
    ref.watch(campaignCharactersChangedProvider(campaignId));
    return _repository.party(campaignId);
  }

  Future<void> reload() async {
    state = AsyncData(await _repository.party(campaignId));
  }

  void _refreshSheets() {
    ref.invalidate(characterControllerProvider);
    ref.invalidate(campaignCharactersControllerProvider(campaignId));
  }

  /// Forced rest for every active character, or only [characterIds] when given.
  Future<void> rest(PartyRestKind kind, {List<String>? characterIds}) async {
    state = AsyncData(
      await _repository.rest(
        campaignId,
        kind,
        characterIds: characterIds == null || characterIds.isEmpty ? null : characterIds,
      ),
    );
    _refreshSheets();
  }

  /// Applies the adjustments and returns the damage outcomes (what each
  /// damage meant for the concentration of its character).
  Future<List<DamageOutcome>> adjust(List<PartyAdjustment> adjustments) async {
    final result = await _repository.adjust(campaignId, adjustments);
    state = AsyncData(result.members);
    _refreshSheets();
    return result.damage;
  }

  /// Hands [characterId] to the player [ownerUserId], or makes it an NPC with null.
  Future<void> setOwner(String characterId, String? ownerUserId) async {
    await ref.read(charactersRepositoryProvider).setOwner(characterId, ownerUserId);
    _refreshSheets();
    await reload();
  }

  /// Grants the next level to every active character, or only [characterIds].
  Future<void> grantLevel({List<String>? characterIds}) async {
    state = AsyncData(
      await _repository.grantLevel(
        campaignId,
        characterIds: characterIds == null || characterIds.isEmpty ? null : characterIds,
      ),
    );
    _refreshSheets();
  }

  /// Withdraws the level granted and not taken yet.
  Future<void> revokeLevel({List<String>? characterIds}) async {
    state = AsyncData(
      await _repository.revokeLevel(
        campaignId,
        characterIds: characterIds == null || characterIds.isEmpty ? null : characterIds,
      ),
    );
    _refreshSheets();
  }
}

final partyControllerProvider = AsyncNotifierProvider.autoDispose
    .family<PartyController, List<PartyMember>, String>(PartyController.new, retry: _noRetry);
