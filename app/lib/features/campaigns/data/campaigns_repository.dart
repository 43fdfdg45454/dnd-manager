import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../domain/campaign_models.dart';

/// Campaign endpoints under `/api/v1/campaigns` plus `/api/v1/users/search`.
class CampaignsRepository {
  CampaignsRepository(this._client);

  final ApiClient _client;

  static const _base = '/api/v1/campaigns';

  /// Cache key of the campaign list (exact) and root of everything cached for
  /// the campaign [id] (see `staleSinceProvider`).
  static const listPath = _base;
  static String campaignPath(String id) => '$_base/$id';

  Future<List<CampaignSummary>> list() async =>
      (await _client.getCached(_base, parse: parseList(CampaignSummary.fromJson))).data;

  Future<CampaignDetail> create({required String name, required String description}) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      _base,
      data: {'name': name, 'description': description},
    );
    return CampaignDetail.fromJson(response.data!);
  }

  Future<CampaignDetail> get(String id) async =>
      (await _client.getCached('$_base/$id', parse: parseObject(CampaignDetail.fromJson))).data;

  /// Only the non-null fields are sent.
  Future<CampaignDetail> update(String id, {String? name, String? description}) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_base/$id',
      data: {'name': ?name, 'description': ?description},
    );
    return CampaignDetail.fromJson(response.data!);
  }

  /// Changes the calendar settings; only the non-null fields are sent.
  Future<CampaignDetail> updateSettings(
    String id, {
    String? timeZoneId,
    List<int>? reminderOffsetsMinutes,
  }) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_base/$id/settings',
      data: {'timeZoneId': ?timeZoneId, 'reminderOffsetsMinutes': ?reminderOffsetsMinutes},
    );
    return CampaignDetail.fromJson(response.data!);
  }

  Future<void> delete(String id) async {
    await _client.dio.delete<void>('$_base/$id');
  }

  Future<List<Member>> members(String id) async =>
      (await _client.getCached('$_base/$id/members', parse: parseList(Member.fromJson))).data;

  Future<Member> addMember(String id, {required String userId, required CampaignRole role}) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_base/$id/members',
      data: {'userId': userId, 'role': role.apiValue},
    );
    return Member.fromJson(response.data!);
  }

  Future<Member> changeMemberRole(String id, String userId, {required CampaignRole role}) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_base/$id/members/$userId',
      data: {'role': role.apiValue},
    );
    return Member.fromJson(response.data!);
  }

  Future<void> removeMember(String id, String userId) async {
    await _client.dio.delete<void>('$_base/$id/members/$userId');
  }

  Future<void> leave(String id) async {
    await _client.dio.post<void>('$_base/$id/leave');
  }

  Future<CampaignDetail> transferOwnership(
    String id, {
    required String toUserId,
    required CampaignRole previousOwnerRole,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_base/$id/transfer-ownership',
      data: {'toUserId': toUserId, 'previousOwnerRole': previousOwnerRole.apiValue},
    );
    return CampaignDetail.fromJson(response.data!);
  }

  /// Active users whose email or name contains [query] (the server needs 2+ characters).
  Future<List<UserSummary>> searchUsers(String query, {int limit = 10}) async {
    final response = await _client.dio.get<List<dynamic>>(
      '/api/v1/users/search',
      queryParameters: {'q': query, 'limit': limit},
    );
    return response.data!.map((e) => UserSummary.fromJson(e as Map<String, dynamic>)).toList();
  }
}

final campaignsRepositoryProvider = Provider<CampaignsRepository>(
  (ref) => CampaignsRepository(ref.watch(apiClientProvider)),
);
