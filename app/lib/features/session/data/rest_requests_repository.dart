import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../../../core/characters/models.dart' show RestRequest;

/// Rest requests the DM resolves (phase 16b).
class RestRequestsRepository {
  RestRequestsRepository(this._client);

  final ApiClient _client;

  static String campaignPath(String campaignId) => '/api/v1/campaigns/$campaignId/rest-requests';

  /// The rest requests of the campaign still waiting for the DM, newest first.
  Future<List<RestRequest>> pending(String campaignId) async => (await _client.getCached(
    campaignPath(campaignId),
    query: {'status': 'Pending'},
    parse: parseList(RestRequest.fromJson),
  )).data;

  /// Approves the request: the server applies the rest to the character.
  Future<RestRequest> approve(String requestId) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/v1/rest-requests/$requestId/approve',
    );
    return RestRequest.fromJson(response.data!);
  }

  Future<RestRequest> reject(String requestId, {String? comment}) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/v1/rest-requests/$requestId/reject',
      data: {'comment': ?comment},
    );
    return RestRequest.fromJson(response.data!);
  }
}

final restRequestsRepositoryProvider = Provider<RestRequestsRepository>(
  (ref) => RestRequestsRepository(ref.watch(apiClientProvider)),
);
