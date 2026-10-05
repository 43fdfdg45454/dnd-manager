import 'package:dio/dio.dart' show Options;
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import 'models.dart';

/// Character and change-request endpoints under `/api/v1`.
class CharactersRepository {
  CharactersRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1';

  Future<Map<String, dynamic>> _json(String method, String path, {Object? data}) async {
    final response = await _client.dio.request<Map<String, dynamic>>(
      path,
      data: data,
      options: Options(method: method),
    );
    return response.data!;
  }

  // -- Characters -----------------------------------------------------------

  Future<List<CharacterSummary>> listByCampaign(String campaignId) async {
    final response = await _client.dio.get<List<dynamic>>('$_api/campaigns/$campaignId/characters');
    return response.data!.map((e) => CharacterSummary.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// [owner] is omitted from the body when null; `(userId: null)` sends an
  /// explicit `ownerUserId: null` (NPC without an owner).
  Future<CharacterDetail> create(
    String campaignId, {
    required String name,
    ({String? userId})? owner,
  }) async {
    final json = await _json(
      'POST',
      '$_api/campaigns/$campaignId/characters',
      data: {'name': name, if (owner != null) 'ownerUserId': owner.userId},
    );
    return CharacterDetail.fromJson(json);
  }

  Future<CharacterDetail> get(String id) async =>
      CharacterDetail.fromJson(await _json('GET', '$_api/characters/$id'));

  /// 200 -> [Saved] with the updated sheet; 202 -> [PendingApproval] with the
  /// change request the DM must approve.
  Future<SheetSaveResult> patchSheet(String id, SheetPatch patch) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/characters/$id/sheet',
      data: patch.toJson(),
    );
    if (response.statusCode == 202) {
      return PendingApproval(ChangeRequest.fromJson(response.data!));
    }
    return Saved(CharacterDetail.fromJson(response.data!));
  }

  /// Owner in Draft asks the DM to activate the character.
  Future<ChangeRequest> submit(String id) async =>
      ChangeRequest.fromJson(await _json('POST', '$_api/characters/$id/submit'));

  Future<CharacterDetail> activate(String id) async =>
      CharacterDetail.fromJson(await _json('POST', '$_api/characters/$id/activate'));

  /// Sets (or, with a null [fileId], removes) the portrait: [fileId] is a
  /// `Portrait` file uploaded for this character's campaign.
  Future<CharacterDetail> setPortrait(String id, String? fileId) async => CharacterDetail.fromJson(
    await _json('PATCH', '$_api/characters/$id/portrait', data: {'fileId': fileId}),
  );

  Future<void> delete(String id) async {
    await _client.dio.delete<void>('$_api/characters/$id');
  }

  // -- Combat tracking (no approval) ---------------------------------------

  Future<CharacterDetail> patchCombat(String id, CombatPatch patch) async =>
      CharacterDetail.fromJson(
        await _json('PATCH', '$_api/characters/$id/combat', data: patch.toJson()),
      );

  Future<void> setConcentration(String id, String? spellIndex) async {
    await _client.dio.post<void>(
      '$_api/characters/$id/concentration',
      data: {'spellIndex': spellIndex},
    );
  }

  Future<void> spendSpellSlot(String id, int level, {int amount = 1}) async {
    await _client.dio.post<void>(
      '$_api/characters/$id/spell-slots/$level/spend',
      data: {'amount': amount},
    );
  }

  Future<void> restoreSpellSlot(String id, int level, {int amount = 1}) async {
    await _client.dio.post<void>(
      '$_api/characters/$id/spell-slots/$level/restore',
      data: {'amount': amount},
    );
  }

  Future<void> spendResource(String id, String resourceId, {int amount = 1}) async {
    await _client.dio.post<void>(
      '$_api/characters/$id/resources/$resourceId/spend',
      data: {'amount': amount},
    );
  }

  Future<void> restoreResource(String id, String resourceId, {int amount = 1}) async {
    await _client.dio.post<void>(
      '$_api/characters/$id/resources/$resourceId/restore',
      data: {'amount': amount},
    );
  }

  Future<CharacterResource> createResource(
    String id, {
    required String name,
    required int max,
    required Recharge recharge,
  }) async => CharacterResource.fromJson(
    await _json(
      'POST',
      '$_api/characters/$id/resources',
      data: {'name': name, 'max': max, 'recharge': recharge.apiValue},
    ),
  );

  Future<void> deleteResource(String id, String resourceId) async {
    await _client.dio.delete<void>('$_api/characters/$id/resources/$resourceId');
  }

  /// [hitDice] maps a class index to the number of hit dice spent.
  Future<CharacterDetail> shortRest(String id, {Map<String, int> hitDice = const {}}) async =>
      CharacterDetail.fromJson(
        await _json('POST', '$_api/characters/$id/rest/short', data: {'hitDice': hitDice}),
      );

  Future<CharacterDetail> longRest(String id) async =>
      CharacterDetail.fromJson(await _json('POST', '$_api/characters/$id/rest/long'));

  // -- Class actions (phase 6, no approval) ----------------------------------

  /// `POST /characters/{id}/class-actions/{action}`: `rage` (`{}`),
  /// `lay-on-hands` (`{amount, targetSelf}`) or `arcane-recovery`
  /// (`{slotLevels: [..]}`). Responds with the updated character.
  Future<CharacterDetail> classAction(
    String id,
    String action, [
    Map<String, dynamic> body = const {},
  ]) async => CharacterDetail.fromJson(
    await _json('POST', '$_api/characters/$id/class-actions/$action', data: body),
  );

  /// `divine-smite`: spends the slot and answers with the extra damage dice.
  Future<DivineSmiteResult> divineSmite(String id, int slotLevel) async {
    final json = await _json(
      'POST',
      '$_api/characters/$id/class-actions/divine-smite',
      data: {'slotLevel': slotLevel},
    );
    final character = json['character'];
    return DivineSmiteResult(
      character: CharacterDetail.fromJson(
        character is Map ? Map<String, dynamic>.from(character) : json,
      ),
      damageDice: json['damageDice'] is String ? json['damageDice'] as String : '',
    );
  }

  // -- Change requests ------------------------------------------------------

  /// DMs get every request of the campaign, players only their own. A null
  /// [status] returns every status.
  Future<List<ChangeRequest>> changeRequests(
    String campaignId, {
    ChangeRequestStatus? status,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '$_api/campaigns/$campaignId/change-requests',
      queryParameters: {'status': ?status?.apiValue},
    );
    return response.data!.map((e) => ChangeRequest.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<ChangeRequest> changeRequest(String id) async =>
      ChangeRequest.fromJson(await _json('GET', '$_api/change-requests/$id'));

  Future<ChangeRequest> approve(String id, {String? comment}) async => ChangeRequest.fromJson(
    await _json('POST', '$_api/change-requests/$id/approve', data: {'comment': ?comment}),
  );

  Future<ChangeRequest> reject(String id, {required String comment}) async =>
      ChangeRequest.fromJson(
        await _json('POST', '$_api/change-requests/$id/reject', data: {'comment': comment}),
      );

  Future<ChangeRequest> cancel(String id) async =>
      ChangeRequest.fromJson(await _json('POST', '$_api/change-requests/$id/cancel'));
}

final charactersRepositoryProvider = Provider<CharactersRepository>(
  (ref) => CharactersRepository(ref.watch(apiClientProvider)),
);
