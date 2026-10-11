import 'package:dio/dio.dart' show Options;
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:opentrpg_core/core/network/api_client.dart';

import '../session/campaign_rules.dart';
import 'models.dart';

/// D&D 5e character routes under `/api/v1/systems/dnd5e`: the sheet, origin
/// choices, combat tracking, companion, resources, rests, class actions,
/// level-up and spell preparation.
class Dnd5eCharactersRepository {
  Dnd5eCharactersRepository(this._client);

  final ApiClient _client;

  /// Prefix of the D&D 5e routes of the server (`/api/v1/systems/dnd5e`).
  static const _dnd5e = '/api/v1/systems/dnd5e';

  Future<Map<String, dynamic>> _json(String method, String path, {Object? data}) async {
    final response = await _client.dio.request<Map<String, dynamic>>(
      path,
      data: data,
      options: Options(method: method),
    );
    return response.data!;
  }

  /// 200 -> [Saved] with the updated sheet; 202 -> [PendingApproval] with the
  /// change request the DM must approve.
  Future<SheetSaveResult> patchSheet(String id, SheetPatch patch) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_dnd5e/characters/$id/sheet',
      data: patch.toJson(),
    );
    if (response.statusCode == 202) {
      return PendingApproval(ChangeRequest.fromJson(response.data!));
    }
    return Saved(CharacterDetail.fromJson(response.data!));
  }

  // -- Origin choices (phase 19) ----------------------------------------------

  /// `GET /characters/{id}/origin-choices`: the decisions of the race,
  /// subrace and background of the character with their current answers.
  Future<OriginChoices> originChoices(String id) async =>
      OriginChoices.fromJson(await _json('GET', '$_dnd5e/characters/$id/origin-choices'));

  /// `PUT /characters/{id}/origin-choices`: answers some choices (the others
  /// keep theirs). A feat answer is `LevelUpChoiceAnswer.feat`.
  Future<OriginChoices> saveOriginChoices(String id, List<LevelUpChoiceAnswer> answers) async =>
      OriginChoices.fromJson(
        await _json(
          'PUT',
          '$_dnd5e/characters/$id/origin-choices',
          data: {
            'choices': [for (final a in answers) a.toJson()],
          },
        ),
      );

  // -- Invalid choices (phase 19) ---------------------------------------------

  /// `GET /characters/{id}/invalid-choices`.
  Future<InvalidChoices> invalidChoices(String id) async =>
      InvalidChoices.fromJson(await _json('GET', '$_dnd5e/characters/$id/invalid-choices'));

  /// `POST /characters/{id}/invalid-choices`: one answer per replacement
  /// (key `replace.<index>`). Responds with the updated character.
  Future<CharacterDetail> replaceInvalidChoices(
    String id,
    List<LevelUpChoiceAnswer> answers,
  ) async => CharacterDetail.fromJson(
    await _json(
      'POST',
      '$_dnd5e/characters/$id/invalid-choices',
      data: {
        'choices': [for (final a in answers) a.toJson()],
      },
    ),
  );

  // -- Combat tracking (no approval) ---------------------------------------

  /// `POST /characters/{id}/damage`: applies [amount] of damage (temporary
  /// hit points first) and says what it meant for the concentration.
  Future<DamageResult> applyDamage(String id, int amount) async {
    final json = await _json('POST', '$_dnd5e/characters/$id/damage', data: {'amount': amount});
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
          '$_dnd5e/characters/$id/resources/$resourceId/rolls',
          data: {'values': values},
        ),
      );

  /// `POST /characters/{id}/resources/initiative`: the resources that come
  /// back when initiative is rolled.
  Future<CharacterDetail> rechargeOnInitiative(String id) async =>
      CharacterDetail.fromJson(await _json('POST', '$_dnd5e/characters/$id/resources/initiative'));

  /// `POST /characters/{id}/resources/{resourceId}/recharge-roll`: the value
  /// rolled on the resource's recharge die; whether it came back.
  Future<({CharacterDetail character, bool recharged})> rechargeRoll(
    String id,
    String resourceId,
    int value,
  ) async {
    final json = await _json(
      'POST',
      '$_dnd5e/characters/$id/resources/$resourceId/recharge-roll',
      data: {'value': value},
    );
    return (
      character: CharacterDetail.fromJson(Map<String, dynamic>.from(json['character'] as Map)),
      recharged: json['recharged'] == true,
    );
  }

  // -- Item enhancements (phase 35) ------------------------------------------

  /// `PUT /characters/{id}/items/{itemId}/enhancements`: the options the item
  /// carries (empty removes them). [Saved] when applied; [PendingApproval]
  /// when the DM has to approve it.
  Future<SheetSaveResult> setItemEnhancements(
    String id,
    String itemId,
    List<String> options,
  ) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '$_dnd5e/characters/$id/items/$itemId/enhancements',
      data: {'options': options},
    );
    if (response.statusCode == 202) {
      return PendingApproval(ChangeRequest.fromJson(response.data!));
    }
    return Saved(CharacterDetail.fromJson(response.data!));
  }

  // -- Animal companion (phase 25, block 6) ----------------------------------

  /// `PUT /characters/{id}/companion` (or `/companions/{companionId}`):
  /// chooses or renames the companion. [Saved] when applied;
  /// [PendingApproval] when a player changes the beast.
  Future<SheetSaveResult> setCompanion(
    String id, {
    required String beastIndex,
    required String name,
    String? companionId,
  }) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      _companionPath(id, companionId),
      data: {'beastIndex': beastIndex, 'name': name},
    );
    if (response.statusCode == 202) {
      return PendingApproval(ChangeRequest.fromJson(response.data!));
    }
    return Saved(CharacterDetail.fromJson(response.data!));
  }

  /// `POST /characters/{id}/companions`: adds one more companion, within the
  /// feature's maximum and challenge budget, without approval.
  Future<CharacterDetail> addCompanion(
    String id, {
    required String beastIndex,
    required String name,
  }) async => CharacterDetail.fromJson(
    await _json(
      'POST',
      '$_dnd5e/characters/$id/companions',
      data: {'beastIndex': beastIndex, 'name': name},
    ),
  );

  /// `POST /characters/{id}/companion/hp` (or `/companions/{companionId}/hp`):
  /// [delta] (negative for damage) or [current], without approval.
  Future<CharacterDetail> trackCompanionHp(
    String id, {
    int? delta,
    int? current,
    String? companionId,
  }) async => CharacterDetail.fromJson(
    await _json(
      'POST',
      '${_companionPath(id, companionId)}/hp',
      data: {'delta': ?delta, 'current': ?current},
    ),
  );

  /// `DELETE /characters/{id}/companion` (DM only) or
  /// `/companions/{companionId}` (the owner or a DM).
  Future<void> deleteCompanion(String id, {String? companionId}) async {
    await _client.dio.delete<void>(_companionPath(id, companionId));
  }

  String _companionPath(String id, String? companionId) => companionId == null
      ? '$_dnd5e/characters/$id/companion'
      : '$_dnd5e/characters/$id/companions/$companionId';

  Future<CharacterDetail> patchCombat(String id, CombatPatch patch) async =>
      CharacterDetail.fromJson(
        await _json('PATCH', '$_dnd5e/characters/$id/combat', data: patch.toJson()),
      );

  Future<void> setConcentration(String id, String? spellIndex) async {
    await _client.dio.post<void>(
      '$_dnd5e/characters/$id/concentration',
      data: {'spellIndex': spellIndex},
    );
  }

  Future<void> spendSpellSlot(String id, int level, {int amount = 1}) async {
    await _client.dio.post<void>(
      '$_dnd5e/characters/$id/spell-slots/$level/spend',
      data: {'amount': amount},
    );
  }

  Future<void> restoreSpellSlot(String id, int level, {int amount = 1}) async {
    await _client.dio.post<void>(
      '$_dnd5e/characters/$id/spell-slots/$level/restore',
      data: {'amount': amount},
    );
  }

  Future<void> spendResource(String id, String resourceId, {int amount = 1}) async {
    await _client.dio.post<void>(
      '$_dnd5e/characters/$id/resources/$resourceId/spend',
      data: {'amount': amount},
    );
  }

  Future<void> restoreResource(String id, String resourceId, {int amount = 1}) async {
    await _client.dio.post<void>(
      '$_dnd5e/characters/$id/resources/$resourceId/restore',
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
      '$_dnd5e/characters/$id/resources',
      data: {'name': name, 'max': max, 'recharge': recharge.apiValue},
    ),
  );

  Future<void> deleteResource(String id, String resourceId) async {
    await _client.dio.delete<void>('$_dnd5e/characters/$id/resources/$resourceId');
  }

  /// [hitDice] maps a class index to the number of hit dice spent.
  Future<CharacterDetail> shortRest(String id, {Map<String, int> hitDice = const {}}) async =>
      CharacterDetail.fromJson(
        await _json('POST', '$_dnd5e/characters/$id/rest/short', data: {'hitDice': hitDice}),
      );

  Future<CharacterDetail> longRest(String id) async =>
      CharacterDetail.fromJson(await _json('POST', '$_dnd5e/characters/$id/rest/long'));

  // -- Class actions (phase 6, no approval) ----------------------------------

  /// `POST /characters/{id}/class-actions/{action}`: `rage` (`{}`),
  /// `lay-on-hands` (`{amount, targetSelf}`) or `arcane-recovery`
  /// (`{slotLevels: [..]}`). Responds with the updated character.
  Future<CharacterDetail> classAction(
    String id,
    String action, [
    Map<String, dynamic> body = const {},
  ]) async => CharacterDetail.fromJson(
    await _json('POST', '$_dnd5e/characters/$id/class-actions/$action', data: body),
  );

  /// `divine-smite`: spends the slot and answers with the extra damage dice.
  Future<DivineSmiteResult> divineSmite(String id, int slotLevel) async {
    final json = await _json(
      'POST',
      '$_dnd5e/characters/$id/class-actions/divine-smite',
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

  /// `hit-point-cast`: casts a spell of [spellLevel] of the class
  /// [classIndex] paying hit points instead of a slot. Answers with the
  /// updated character and the hit points lost.
  Future<HitPointCastResult> hitPointCast(
    String id, {
    required String classIndex,
    required int spellLevel,
  }) async {
    final json = await _json(
      'POST',
      '$_dnd5e/characters/$id/class-actions/hit-point-cast',
      data: {'classIndex': classIndex, 'spellLevel': spellLevel},
    );
    return HitPointCastResult(
      character: CharacterDetail.fromJson(Map<String, dynamic>.from(json['character'] as Map)),
      hitPointsLost: (json['hitPointsLost'] as num?)?.toInt() ?? 0,
    );
  }

  // -- Campaign rules (phase 35) ----------------------------------------------

  /// `GET /campaigns/{id}/rules`: the D&D 5e rules of the campaign.
  Future<CampaignRules> campaignRules(String campaignId) async =>
      CampaignRules.fromJson(await _json('GET', '$_dnd5e/campaigns/$campaignId/rules'));

  /// `PUT /campaigns/{id}/rules` (Owner or DM).
  Future<CampaignRules> saveCampaignRules(
    String campaignId, {
    required bool starterFeatsEnabled,
  }) async => CampaignRules.fromJson(
    await _json(
      'PUT',
      '$_dnd5e/campaigns/$campaignId/rules',
      data: {'starterFeatsEnabled': starterFeatsEnabled},
    ),
  );

  // -- Level-up wizard (phase 16c) -------------------------------------------

  /// `GET /characters/{id}/level-up`: what the next level in [classIndex]
  /// (the main class when null) brings. 409 when no level was granted.
  Future<LevelUpPlan> levelUpPlan(String id, {String? classIndex}) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '$_dnd5e/characters/$id/level-up',
      queryParameters: {'classIndex': ?classIndex},
    );
    return LevelUpPlan.fromJson(response.data!);
  }

  /// `POST /characters/{id}/level-up`: applies the level and returns the
  /// updated character. 400 with a Spanish message on an invalid answer.
  Future<CharacterDetail> applyLevelUp(String id, LevelUpRequest request) async =>
      CharacterDetail.fromJson(
        await _json('POST', '$_dnd5e/characters/$id/level-up', data: request.toJson()),
      );

  // -- Spell preparation (phase 18, no approval) -----------------------------

  /// `GET /characters/{id}/spell-preparation`: what each preparing class can
  /// prepare and whether the preparation is pending.
  Future<SpellPreparation> spellPreparation(String id) async =>
      SpellPreparation.fromJson(await _json('GET', '$_dnd5e/characters/$id/spell-preparation'));

  /// `POST /characters/{id}/spell-preparation`: [classes] maps each preparing
  /// class index to the spells prepared (always every class that prepares).
  Future<CharacterDetail> prepareSpells(String id, Map<String, List<String>> classes) async =>
      CharacterDetail.fromJson(
        await _json(
          'POST',
          '$_dnd5e/characters/$id/spell-preparation',
          data: {
            'classes': [
              for (final e in classes.entries) {'classIndex': e.key, 'spells': e.value},
            ],
          },
        ),
      );

  /// `POST /characters/{id}/spell-preparation/keep`: keeps the current ones.
  Future<CharacterDetail> keepSpellPreparation(String id) async => CharacterDetail.fromJson(
    await _json('POST', '$_dnd5e/characters/$id/spell-preparation/keep'),
  );
}

final dnd5eCharactersRepositoryProvider = Provider<Dnd5eCharactersRepository>(
  (ref) => Dnd5eCharactersRepository(ref.watch(apiClientProvider)),
);
