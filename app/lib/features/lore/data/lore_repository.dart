import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import 'models.dart';

/// Lore endpoints: `/campaigns/{id}/lore` and `/lore/{id}`.
class LoreRepository {
  LoreRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1';

  /// The flat tree of the campaign (players only receive the `Players` entries).
  Future<List<LoreSummary>> list(String campaignId) async {
    final response = await _client.dio.get<List<dynamic>>('$_api/campaigns/$campaignId/lore');
    return response.data!.map((e) => LoreSummary.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<LoreEntry> create(String campaignId, LoreDraft draft) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/campaigns/$campaignId/lore',
      data: draft.toJson(),
    );
    return LoreEntry.fromJson(response.data!);
  }

  Future<LoreEntry> get(String id) async {
    final response = await _client.dio.get<Map<String, dynamic>>('$_api/lore/$id');
    return LoreEntry.fromJson(response.data!);
  }

  Future<LoreEntry> update(String id, LoreDraft draft) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/lore/$id',
      data: draft.toJson(),
    );
    return LoreEntry.fromJson(response.data!);
  }

  Future<void> delete(String id) async {
    await _client.dio.delete<void>('$_api/lore/$id');
  }

  Future<LoreAttachment> addAttachment(String id, {required String fileId, String? caption}) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/lore/$id/attachments',
      data: {'fileId': fileId, 'caption': ?caption},
    );
    return LoreAttachment.fromJson(response.data!);
  }

  Future<void> removeAttachment(String id, String attachmentId) async {
    await _client.dio.delete<void>('$_api/lore/$id/attachments/$attachmentId');
  }
}

final loreRepositoryProvider = Provider<LoreRepository>(
  (ref) => LoreRepository(ref.watch(apiClientProvider)),
);
