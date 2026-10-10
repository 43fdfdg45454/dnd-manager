import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../domain/campaign_content_pack.dart';

/// `/api/v1/campaigns/{id}/content-packs`: the packs a campaign enables.
class CampaignContentPacksRepository {
  CampaignContentPacksRepository(this._client);

  final ApiClient _client;

  static String path(String campaignId) => '/api/v1/campaigns/$campaignId/content-packs';

  /// The packs of the system of the campaign, the base one first (members).
  Future<List<CampaignContentPack>> list(String campaignId) async => (await _client.getCached(
    path(campaignId),
    parse: parseList(CampaignContentPack.fromJson),
  )).data;

  /// Replaces the enabled packs of the campaign (Owner/DM); the base pack is
  /// ignored. A 400 carries the code `unknown-pack` or `missing-requirement`.
  Future<List<CampaignContentPack>> setEnabled(String campaignId, Iterable<String> packIds) async {
    final response = await _client.dio.put<List<dynamic>>(
      path(campaignId),
      data: {'packIds': packIds.toList()},
    );
    return parseList(CampaignContentPack.fromJson)(response.data);
  }
}

final campaignContentPacksRepositoryProvider = Provider<CampaignContentPacksRepository>(
  (ref) => CampaignContentPacksRepository(ref.watch(apiClientProvider)),
);
