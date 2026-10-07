import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../characters/data/characters_controller.dart';
import '../../characters/data/characters_repository.dart';
import '../../characters/data/models.dart' show DamageOutcome, RestRequest;
import '../../items/data/items_controllers.dart';
import '../../items/data/models.dart' show ItemOverrides;
import 'messages_repository.dart';
import 'models.dart';
import 'party_repository.dart';
import 'rest_requests_repository.dart';
import 'stash_repository.dart';

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
  Future<List<PartyMember>> build() => _repository.party(campaignId);

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

// ---------------------------------------------------------------------------
// Rest requests
// ---------------------------------------------------------------------------

/// The rest requests of a campaign waiting for the DM. Resolving one refreshes
/// the party and the sheet of its character.
class RestRequestsController extends AsyncNotifier<List<RestRequest>> {
  RestRequestsController(this.campaignId);

  final String campaignId;

  RestRequestsRepository get _repository => ref.read(restRequestsRepositoryProvider);

  @override
  Future<List<RestRequest>> build() => _repository.pending(campaignId);

  Future<void> reload() async {
    state = AsyncData(await _repository.pending(campaignId));
  }

  void _refresh(RestRequest request) {
    ref.invalidate(characterControllerProvider(request.characterId));
    ref.invalidate(campaignCharactersControllerProvider(campaignId));
    ref.invalidate(partyControllerProvider(campaignId));
  }

  Future<void> approve(RestRequest request) async {
    await _repository.approve(request.id);
    _refresh(request);
    await reload();
  }

  Future<void> reject(RestRequest request, {String? comment}) async {
    await _repository.reject(request.id, comment: comment);
    _refresh(request);
    await reload();
  }
}

final restRequestsControllerProvider = AsyncNotifierProvider.autoDispose
    .family<RestRequestsController, List<RestRequest>, String>(
      RestRequestsController.new,
      retry: _noRetry,
    );

// ---------------------------------------------------------------------------
// Party stash
// ---------------------------------------------------------------------------

/// Party stash of a campaign. Movements to or from a character refresh its
/// inventory, its sheet (money) and the transaction history.
class StashController extends AsyncNotifier<PartyStash> {
  StashController(this.campaignId);

  final String campaignId;

  StashRepository get _repository => ref.read(stashRepositoryProvider);

  @override
  Future<PartyStash> build() => _repository.get(campaignId);

  Future<void> reload() async {
    state = AsyncData(await _repository.get(campaignId));
    ref.invalidate(transactionsControllerProvider(campaignId));
  }

  void _refreshCharacter(String? characterId) {
    if (characterId == null) {
      ref.invalidate(inventoryControllerProvider);
      ref.invalidate(characterControllerProvider);
    } else {
      ref.invalidate(inventoryControllerProvider(characterId));
      ref.invalidate(characterControllerProvider(characterId));
    }
    ref.invalidate(transactionsControllerProvider(campaignId));
  }

  Future<void> addItem({
    String? templateId,
    ItemOverrides overrides = const ItemOverrides(),
    int quantity = 1,
    String? notes,
  }) async {
    await _repository.addItem(
      campaignId,
      templateId: templateId,
      overrides: overrides,
      quantity: quantity,
      notes: notes,
    );
    await reload();
  }

  Future<void> updateItem(String itemId, {required int quantity, required String notes}) async {
    final trimmed = notes.trim();
    await _repository.updateItem(
      campaignId,
      itemId,
      quantity: quantity,
      notes: trimmed.isEmpty ? null : trimmed,
      clearNotes: trimmed.isEmpty,
    );
    await reload();
  }

  Future<void> removeItem(String itemId) async {
    await _repository.removeItem(campaignId, itemId);
    await reload();
  }

  /// Moves units of a stash item to the inventory of [characterId].
  Future<void> take(String itemId, {required String characterId, int quantity = 1}) async {
    state = AsyncData(
      await _repository.take(campaignId, itemId, characterId: characterId, quantity: quantity),
    );
    _refreshCharacter(characterId);
  }

  /// Gives units of an inventory item back to the stash.
  Future<void> giveBack({
    required String characterId,
    required String characterItemId,
    int quantity = 1,
  }) async {
    state = AsyncData(
      await _repository.giveBack(
        campaignId,
        characterId: characterId,
        characterItemId: characterItemId,
        quantity: quantity,
      ),
    );
    _refreshCharacter(characterId);
  }

  Future<void> adjustGold(int deltaCp) async {
    state = AsyncData(await _repository.adjustGold(campaignId, deltaCp));
    ref.invalidate(transactionsControllerProvider(campaignId));
  }

  /// Splits the shared gold among every active character, or [characterIds].
  Future<void> splitGold({List<String>? characterIds}) async {
    state = AsyncData(
      await _repository.splitGold(
        campaignId,
        characterIds: characterIds == null || characterIds.isEmpty ? null : characterIds,
      ),
    );
    _refreshCharacter(null);
  }
}

final stashControllerProvider = AsyncNotifierProvider.autoDispose
    .family<StashController, PartyStash, String>(StashController.new, retry: _noRetry);

// ---------------------------------------------------------------------------
// Secret messages
// ---------------------------------------------------------------------------

/// How many messages the inbox loads.
const messagesPageSize = 50;

/// Secret messages of a campaign: the ones sent (DM) or received (player).
class MessagesController extends AsyncNotifier<List<DirectMessage>> {
  MessagesController(this.campaignId);

  final String campaignId;

  MessagesRepository get _repository => ref.read(messagesRepositoryProvider);

  @override
  Future<List<DirectMessage>> build() => _repository.list(campaignId, limit: messagesPageSize);

  Future<void> reload() async {
    state = AsyncData(await _repository.list(campaignId, limit: messagesPageSize));
  }

  /// Sends [body] to the players of [characterIds] (at least DM).
  Future<void> send({required List<String> characterIds, required String body}) async {
    await _repository.send(campaignId, characterIds: characterIds, body: body);
    try {
      await reload();
    } catch (_) {
      // The message was sent; a failed refresh keeps the previous list.
    }
  }

  /// Marks a received message as read and updates the unread counter.
  Future<void> markRead(String messageId) async {
    final read = await _repository.markRead(messageId);
    final current = state.value;
    if (current != null) {
      state = AsyncData([for (final m in current) m.id == read.id ? read : m]);
    }
    ref.invalidate(unreadMessagesCountProvider(campaignId));
  }
}

final messagesControllerProvider = AsyncNotifierProvider.autoDispose
    .family<MessagesController, List<DirectMessage>, String>(
      MessagesController.new,
      retry: _noRetry,
    );

/// Unread secret messages of the user in a campaign (0 while loading, on
/// error and for DMs).
final unreadMessagesCountProvider = FutureProvider.autoDispose.family<int, String>(
  (ref, campaignId) => ref.watch(messagesRepositoryProvider).unreadCount(campaignId),
  retry: _noRetry,
);
