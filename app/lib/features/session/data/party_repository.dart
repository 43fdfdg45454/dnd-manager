import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../../characters/data/models.dart' show DamageOutcome;
import 'models.dart';

/// DM tools for the table under `/api/v1/campaigns/{id}/party` (at least DM).
class PartyRepository {
  PartyRepository(this._client);

  final ApiClient _client;

  static String partyPath(String campaignId) => '/api/v1/campaigns/$campaignId/party';

  static List<PartyMember> _parse(Object? json) =>
      parseList(PartyMember.fromJson)((json as Map<String, dynamic>)['characters']);

  /// The active characters of the campaign, sorted by name.
  Future<List<PartyMember>> party(String campaignId) async =>
      (await _client.getCached(partyPath(campaignId), parse: _parse)).data;

  /// Forced short or long rest for every active character, or only [characterIds].
  Future<List<PartyMember>> rest(
    String campaignId,
    PartyRestKind kind, {
    List<String>? characterIds,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '${partyPath(campaignId)}/rest',
      data: {'kind': kind.apiValue, 'characterIds': ?characterIds},
    );
    return _parse(response.data);
  }

  /// Grants the next level to every active character, or only [characterIds].
  Future<List<PartyMember>> grantLevel(String campaignId, {List<String>? characterIds}) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '${partyPath(campaignId)}/grant-level',
      data: {'characterIds': ?characterIds},
    );
    return _parse(response.data);
  }

  /// Withdraws the level granted and not taken yet (everyone, or [characterIds]).
  Future<List<PartyMember>> revokeLevel(String campaignId, {List<String>? characterIds}) async {
    final response = await _client.dio.delete<Map<String, dynamic>>(
      '${partyPath(campaignId)}/grant-level',
      data: {'characterIds': ?characterIds},
    );
    return _parse(response.data);
  }

  /// Damage or healing, temporary hit points, conditions and maximum hit points
  /// of several characters at once. The result also carries the damage
  /// outcomes (concentration saves to make or concentrations that ended).
  Future<PartyAdjustResult> adjust(String campaignId, List<PartyAdjustment> adjustments) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '${partyPath(campaignId)}/adjust',
      data: [for (final a in adjustments) a.toJson()],
    );
    final damage = response.data?['damage'];
    return PartyAdjustResult(
      members: _parse(response.data),
      damage: [
        if (damage is List)
          for (final d in damage)
            if (d is Map) DamageOutcome.fromJson(Map<String, dynamic>.from(d)),
      ],
    );
  }
}

final partyRepositoryProvider = Provider<PartyRepository>(
  (ref) => PartyRepository(ref.watch(apiClientProvider)),
);
