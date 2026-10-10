import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../../items/data/models.dart' show ItemOverrides;
import 'models.dart';

/// Party stash of a campaign under `/api/v1/campaigns/{id}/stash`: shared loot
/// and gold. Adding, editing, removing and the gold are for DMs; taking and
/// giving back for any member with their own characters (if the campaign
/// allows it) and for DMs with any.
class StashRepository {
  StashRepository(this._client);

  final ApiClient _client;

  static String stashPath(String campaignId) => '/api/v1/campaigns/$campaignId/stash';

  Future<PartyStash> get(String campaignId) async => (await _client.getCached(
    stashPath(campaignId),
    parse: parseObject(PartyStash.fromJson),
  )).data;

  /// Loot from the campaign catalog ([templateId]) and/or custom ([overrides]).
  Future<StashItem> addItem(
    String campaignId, {
    String? templateId,
    ItemOverrides overrides = const ItemOverrides(),
    int quantity = 1,
    String? notes,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '${stashPath(campaignId)}/items',
      data: {
        'templateId': ?templateId,
        if (!overrides.isEmpty) 'overrides': overrides.toJson(),
        'quantity': quantity,
        'notes': ?notes,
      },
    );
    return StashItem.fromJson(response.data!);
  }

  /// Changes the quantity and/or the notes ([clearNotes] removes them).
  Future<StashItem> updateItem(
    String campaignId,
    String itemId, {
    int? quantity,
    String? notes,
    bool clearNotes = false,
  }) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '${stashPath(campaignId)}/items/$itemId',
      data: {'quantity': ?quantity, if (clearNotes) 'notes': null else 'notes': ?notes},
    );
    return StashItem.fromJson(response.data!);
  }

  Future<void> removeItem(String campaignId, String itemId) async {
    await _client.dio.delete<void>('${stashPath(campaignId)}/items/$itemId');
  }

  /// Moves [quantity] units of a stash item to the inventory of [characterId].
  Future<PartyStash> take(
    String campaignId,
    String itemId, {
    required String characterId,
    int quantity = 1,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '${stashPath(campaignId)}/items/$itemId/take',
      data: {'characterId': characterId, 'quantity': quantity},
    );
    return PartyStash.fromJson(response.data!);
  }

  /// Gives [quantity] units of an inventory item back to the stash.
  Future<PartyStash> giveBack(
    String campaignId, {
    required String characterId,
    required String characterItemId,
    int quantity = 1,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '${stashPath(campaignId)}/items/return',
      data: {'characterId': characterId, 'characterItemId': characterItemId, 'quantity': quantity},
    );
    return PartyStash.fromJson(response.data!);
  }

  /// Adds (positive) or withdraws (negative) shared gold, in copper pieces.
  Future<PartyStash> adjustGold(String campaignId, int deltaCp) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '${stashPath(campaignId)}/gold',
      data: {'deltaCp': deltaCp},
    );
    return PartyStash.fromJson(response.data!);
  }

  /// Splits the shared gold evenly among every active character, or [characterIds].
  Future<PartyStash> splitGold(String campaignId, {List<String>? characterIds}) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '${stashPath(campaignId)}/gold/split',
      data: {'characterIds': ?characterIds},
    );
    return PartyStash.fromJson(response.data!);
  }
}

final stashRepositoryProvider = Provider<StashRepository>(
  (ref) => StashRepository(ref.watch(apiClientProvider)),
);
