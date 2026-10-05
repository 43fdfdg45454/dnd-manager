import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../../catalog/data/models.dart' show Page;
import 'models.dart';

/// Shop, trade and transaction endpoints under `/api/v1`.
class ShopsRepository {
  ShopsRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1';

  // -- Shops ------------------------------------------------------------------

  /// Players only get the open shops.
  Future<List<ShopSummary>> list(String campaignId) async => (await _client.getCached(
    '$_api/campaigns/$campaignId/shops',
    parse: parseList(ShopSummary.fromJson),
  )).data;

  /// Root of everything cached for the shop [shopId].
  static String shopPath(String shopId) => '$_api/shops/$shopId';

  Future<ShopSummary> create(
    String campaignId, {
    required String name,
    String? description,
    int? buybackPercent,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/campaigns/$campaignId/shops',
      data: {'name': name, 'description': ?description, 'buybackPercent': ?buybackPercent},
    );
    return ShopSummary.fromJson(response.data!);
  }

  /// 404 when a player asks for a closed shop.
  Future<Shop> get(String shopId) async =>
      (await _client.getCached('$_api/shops/$shopId', parse: parseObject(Shop.fromJson))).data;

  Future<ShopSummary> update(String shopId, ShopPatch patch) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/shops/$shopId',
      data: patch.toJson(),
    );
    return ShopSummary.fromJson(response.data!);
  }

  Future<void> delete(String shopId) async {
    await _client.dio.delete<void>('$_api/shops/$shopId');
  }

  // -- Shop items ---------------------------------------------------------------

  Future<ShopItem> addItem(String shopId, ShopItemInput input) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/shops/$shopId/items',
      data: input.toJson(),
    );
    return ShopItem.fromJson(response.data!);
  }

  Future<ShopItem> updateItem(String shopId, String shopItemId, ShopItemPatch patch) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/shops/$shopId/items/$shopItemId',
      data: patch.toJson(),
    );
    return ShopItem.fromJson(response.data!);
  }

  Future<void> removeItem(String shopId, String shopItemId) async {
    await _client.dio.delete<void>('$_api/shops/$shopId/items/$shopItemId');
  }

  // -- Trading ------------------------------------------------------------------

  /// 400 without money or stock, 403 for someone else's character, 409 when
  /// the shop is closed.
  Future<TradeResult> buy(
    String shopId, {
    required String characterId,
    required String shopItemId,
    int quantity = 1,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/shops/$shopId/buy',
      data: {'characterId': characterId, 'shopItemId': shopItemId, 'quantity': quantity},
    );
    return TradeResult.fromJson(response.data!);
  }

  /// 400 for an attuned item or a bad quantity.
  Future<TradeResult> sell(
    String shopId, {
    required String characterId,
    required String itemId,
    int quantity = 1,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/shops/$shopId/sell',
      data: {'characterId': characterId, 'itemId': itemId, 'quantity': quantity},
    );
    return TradeResult.fromJson(response.data!);
  }

  // -- History ------------------------------------------------------------------

  /// DMs get every transaction, players those of their own characters.
  Future<Page<Transaction>> transactions(
    String campaignId, {
    String? characterId,
    int page = 1,
    int pageSize = 30,
  }) async {
    final result = await _client.getCached(
      '$_api/campaigns/$campaignId/transactions',
      query: {'characterId': characterId, 'page': page, 'pageSize': pageSize},
      parse: (json) => Page.fromJson(json as Map<String, dynamic>, Transaction.fromJson),
    );
    return result.data;
  }
}

final shopsRepositoryProvider = Provider<ShopsRepository>(
  (ref) => ShopsRepository(ref.watch(apiClientProvider)),
);
