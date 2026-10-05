import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import 'models.dart';

/// Library endpoints: `/library` and `/campaigns/{id}/library`.
class LibraryRepository {
  LibraryRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1';

  /// Root of the cached library list.
  static const libraryPath = '$_api/library';

  Future<List<LibraryDocument>> list() async =>
      (await _client.getCached(libraryPath, parse: parseList(LibraryDocument.fromJson))).data;

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
  /// Cache key of the documents recommended in the campaign [campaignId].
  static String campaignDocumentsPath(String campaignId) => '$_api/campaigns/$campaignId/library';

  Future<List<LibraryDocument>> campaignDocuments(String campaignId) async =>
      (await _client.getCached(
        campaignDocumentsPath(campaignId),
        parse: parseList(LibraryDocument.fromJson),
      )).data;

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
