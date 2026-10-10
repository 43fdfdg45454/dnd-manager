import 'package:dio/dio.dart' show Options;
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../../../core/characters/models.dart';

/// Character, rest-request and change-request endpoints under `/api/v1` (the
/// core of every game system). The D&D 5e sheet routes are in
/// `Dnd5eCharactersRepository`.
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

  /// Root of everything cached for the character [id] (sheet and inventory).
  static String characterPath(String id) => '$_api/characters/$id';

  Future<List<CharacterSummary>> listByCampaign(String campaignId) async =>
      (await _client.getCached(
        '$_api/campaigns/$campaignId/characters',
        parse: parseList(CharacterSummary.fromJson),
      )).data;

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

  Future<CharacterDetail> get(String id) async => (await _client.getCached(
    '$_api/characters/$id',
    parse: parseObject(CharacterDetail.fromJson),
  )).data;

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

  /// DM only: hands the character to the player [ownerUserId], or makes it an
  /// NPC with null. Applied directly, without approval.
  Future<CharacterDetail> setOwner(String id, String? ownerUserId) async =>
      CharacterDetail.fromJson(
        await _json('PUT', '$_api/characters/$id/owner', data: {'ownerUserId': ownerUserId}),
      );

  /// The owner asks the DM for a rest (`POST /characters/{id}/rest-requests`).
  /// [hitDice] maps a class index to the hit dice to spend (short rest only).
  Future<RestRequest> requestRest(
    String id,
    RestKind kind, {
    Map<String, int> hitDice = const {},
  }) async => RestRequest.fromJson(
    await _json(
      'POST',
      '$_api/characters/$id/rest-requests',
      data: {'kind': kind.requestValue, if (kind == RestKind.short) 'hitDice': hitDice},
    ),
  );

  /// Withdraws the pending rest request of the character.
  Future<void> cancelRestRequest(String id) async {
    await _client.dio.delete<void>('$_api/characters/$id/rest-requests');
  }

  // -- Change requests ------------------------------------------------------

  /// DMs get every request of the campaign, players only their own. A null
  /// [status] returns every status.
  Future<List<ChangeRequest>> changeRequests(
    String campaignId, {
    ChangeRequestStatus? status,
  }) async {
    final result = await _client.getCached(
      '$_api/campaigns/$campaignId/change-requests',
      query: {'status': status?.apiValue},
      parse: parseList(ChangeRequest.fromJson),
    );
    return result.data;
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
