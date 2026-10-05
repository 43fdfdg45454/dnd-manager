import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../catalog/data/models.dart' show ItemDetail, ItemSummary, Page;
import 'models.dart';

/// Which templates a campaign item search covers.
enum ItemSource {
  all('all', 'Todos'),
  srd('srd', 'SRD'),
  homebrew('homebrew', 'Campaña');

  const ItemSource(this.apiValue, this.label);

  final String apiValue;
  final String label;
}

/// Item catalog of a campaign (SRD plus homebrew) under
/// `/api/v1/campaigns/{id}/items`.
class CampaignItemsRepository {
  CampaignItemsRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1/campaigns';

  Future<Page<ItemSummary>> list(
    String campaignId, {
    String? search,
    String? category,
    String? rarity,
    ItemSource source = ItemSource.all,
    int page = 1,
    int pageSize = 30,
  }) async {
    final text = search?.trim();
    final response = await _client.dio.get<Map<String, dynamic>>(
      '$_api/$campaignId/items',
      queryParameters: {
        if (text != null && text.isNotEmpty) 'search': text,
        'category': ?category,
        'rarity': ?rarity,
        'source': source.apiValue,
        'page': page,
        'pageSize': pageSize,
      },
    );
    return Page.fromJson(response.data!, ItemSummary.fromJson);
  }

  Future<ItemDetail> create(String campaignId, ItemTemplateInput input) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/$campaignId/items',
      data: input.toJson(),
    );
    return ItemDetail.fromJson(response.data!);
  }

  Future<ItemDetail> update(String campaignId, String templateId, ItemTemplateInput input) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/$campaignId/items/$templateId',
      data: input.toJson(),
    );
    return ItemDetail.fromJson(response.data!);
  }

  /// 409 when an item or a shop still uses the template.
  Future<void> delete(String campaignId, String templateId) async {
    await _client.dio.delete<void>('$_api/$campaignId/items/$templateId');
  }
}

final campaignItemsRepositoryProvider = Provider<CampaignItemsRepository>(
  (ref) => CampaignItemsRepository(ref.watch(apiClientProvider)),
);
