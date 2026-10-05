import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import 'models.dart';

/// Library endpoints: `/library` and `/campaigns/{id}/library`.
class LibraryRepository {
  LibraryRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1';

  Future<List<LibraryDocument>> list() async {
    final response = await _client.dio.get<List<dynamic>>('$_api/library');
    return response.data!.map((e) => LibraryDocument.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// Admin only: publishes an uploaded `LibraryDocument` PDF.
  Future<LibraryDocument> create({
    required String title,
    String? description,
    required LibraryCategory category,
    required String fileId,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/library',
      data: {
        'title': title,
        'description': ?description,
        'category': category.apiValue,
        'fileId': fileId,
      },
    );
    return LibraryDocument.fromJson(response.data!);
  }

  /// Admin only; system documents answer 400.
  Future<void> delete(String id) async {
    await _client.dio.delete<void>('$_api/library/$id');
  }

  /// Documents the DMs recommend in a campaign.
  Future<List<LibraryDocument>> campaignDocuments(String campaignId) async {
    final response = await _client.dio.get<List<dynamic>>('$_api/campaigns/$campaignId/library');
    return response.data!.map((e) => LibraryDocument.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> recommend(String campaignId, String documentId, {String? note}) async {
    await _client.dio.put<void>(
      '$_api/campaigns/$campaignId/library/$documentId',
      data: {'note': ?note},
    );
  }

  Future<void> unrecommend(String campaignId, String documentId) async {
    await _client.dio.delete<void>('$_api/campaigns/$campaignId/library/$documentId');
  }
}

final libraryRepositoryProvider = Provider<LibraryRepository>(
  (ref) => LibraryRepository(ref.watch(apiClientProvider)),
);
