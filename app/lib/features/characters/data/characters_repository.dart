import 'package:dio/dio.dart' show Options;
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
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

  /// DM only: hands the character to the player [ownerUserId], or makes it an
  /// NPC with null. Applied directly, without approval.
  Future<CharacterDetail> setOwner(String id, String? ownerUserId) async =>
      CharacterDetail.fromJson(
        await _json('PUT', '$_api/characters/$id/owner', data: {'ownerUserId': ownerUserId}),
      );

  // -- Origin choices (phase 19) ----------------------------------------------

  /// `GET /characters/{id}/origin-choices`: the decisions of the race,
  /// subrace and background of the character with their current answers.
  Future<OriginChoices> originChoices(String id) async =>
      OriginChoices.fromJson(await _json('GET', '$_api/characters/$id/origin-choices'));

  /// `PUT /characters/{id}/origin-choices`: answers some choices (the others
  /// keep theirs). A feat answer is `LevelUpChoiceAnswer.feat`.
  Future<OriginChoices> saveOriginChoices(String id, List<LevelUpChoiceAnswer> answers) async =>
      OriginChoices.fromJson(
        await _json(
          'PUT',
          '$_api/characters/$id/origin-choices',
          data: {
            'choices': [for (final a in answers) a.toJson()],
          },
        ),
      );

  // -- Invalid choices (phase 19) ---------------------------------------------

  /// `GET /characters/{id}/invalid-choices`.
  Future<InvalidChoices> invalidChoices(String id) async =>
      InvalidChoices.fromJson(await _json('GET', '$_api/characters/$id/invalid-choices'));

  /// `POST /characters/{id}/invalid-choices`: one answer per replacement
  /// (key `replace.<index>`). Responds with the updated character.
  Future<CharacterDetail> replaceInvalidChoices(
    String id,
    List<LevelUpChoiceAnswer> answers,
  ) async => CharacterDetail.fromJson(
    await _json(
      'POST',
      '$_api/characters/$id/invalid-choices',
      data: {
        'choices': [for (final a in answers) a.toJson()],
      },
    ),
  );

  // -- Combat tracking (no approval) ---------------------------------------

  /// `POST /characters/{id}/damage`: applies [amount] of damage (temporary
  /// hit points first) and says what it meant for the concentration.
  Future<DamageResult> applyDamage(String id, int amount) async {
    final json = await _json('POST', '$_api/characters/$id/damage', data: {'amount': amount});
    final character = json['character'];
    final outcome = json['outcome'];
    return DamageResult(
      character: CharacterDetail.fromJson(
        character is Map ? Map<String, dynamic>.from(character) : json,
      ),
      outcome: DamageOutcome.fromJson(outcome is Map ? Map<String, dynamic>.from(outcome) : {}),
    );
  }

  /// `POST /characters/{id}/resources/{resourceId}/rolls`: the dice rolled
  /// after a rest for a resource that asks for them.
  Future<CharacterDetail> saveResourceRolls(String id, String resourceId, List<int> values) async =>
      CharacterDetail.fromJson(
        await _json(
          'POST',
          '$_api/characters/$id/resources/$resourceId/rolls',
          data: {'values': values},
        ),
      );

  // -- Animal companion (phase 25, block 6) ----------------------------------

  /// `PUT /characters/{id}/companion`: chooses or renames the companion.
  /// [Saved] when applied; [PendingApproval] when a player changes the beast.
  Future<SheetSaveResult> setCompanion(
    String id, {
    required String beastIndex,
    required String name,
  }) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '$_api/characters/$id/companion',
      data: {'beastIndex': beastIndex, 'name': name},
    );
    if (response.statusCode == 202) {
      return PendingApproval(ChangeRequest.fromJson(response.data!));
    }
    return Saved(CharacterDetail.fromJson(response.data!));
  }

  /// `POST /characters/{id}/companion/hp`: [delta] (negative for damage) or
  /// [current], without approval.
  Future<CharacterDetail> trackCompanionHp(String id, {int? delta, int? current}) async =>
      CharacterDetail.fromJson(
        await _json(
          'POST',
          '$_api/characters/$id/companion/hp',
          data: {'delta': ?delta, 'current': ?current},
        ),
      );

  /// `DELETE /characters/{id}/companion` (DM only).
  Future<void> deleteCompanion(String id) async {
    await _client.dio.delete<void>('$_api/characters/$id/companion');
  }

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

  // -- Level-up wizard (phase 16c) -------------------------------------------

  /// `GET /characters/{id}/level-up`: what the next level in [classIndex]
  /// (the main class when null) brings. 409 when no level was granted.
  Future<LevelUpPlan> levelUpPlan(String id, {String? classIndex}) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '$_api/characters/$id/level-up',
      queryParameters: {'classIndex': ?classIndex},
    );
    return LevelUpPlan.fromJson(response.data!);
  }

  /// `POST /characters/{id}/level-up`: applies the level and returns the
  /// updated character. 400 with a Spanish message on an invalid answer.
  Future<CharacterDetail> applyLevelUp(String id, LevelUpRequest request) async =>
      CharacterDetail.fromJson(
        await _json('POST', '$_api/characters/$id/level-up', data: request.toJson()),
      );

  // -- Spell preparation (phase 18, no approval) -----------------------------

  /// `GET /characters/{id}/spell-preparation`: what each preparing class can
  /// prepare and whether the preparation is pending.
  Future<SpellPreparation> spellPreparation(String id) async =>
      SpellPreparation.fromJson(await _json('GET', '$_api/characters/$id/spell-preparation'));

  /// `POST /characters/{id}/spell-preparation`: [classes] maps each preparing
  /// class index to the spells prepared (always every class that prepares).
  Future<CharacterDetail> prepareSpells(String id, Map<String, List<String>> classes) async =>
      CharacterDetail.fromJson(
        await _json(
          'POST',
          '$_api/characters/$id/spell-preparation',
          data: {
            'classes': [
              for (final e in classes.entries) {'classIndex': e.key, 'spells': e.value},
            ],
          },
        ),
      );

  /// `POST /characters/{id}/spell-preparation/keep`: keeps the current ones.
  Future<CharacterDetail> keepSpellPreparation(String id) async =>
      CharacterDetail.fromJson(await _json('POST', '$_api/characters/$id/spell-preparation/keep'));

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
