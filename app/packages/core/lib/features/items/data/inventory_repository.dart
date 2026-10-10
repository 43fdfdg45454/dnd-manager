import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../../../core/characters/models.dart' show ChangeRequest;
import 'models.dart';

/// Inventory endpoints of a character under `/api/v1/characters/{id}`.
class InventoryRepository {
  InventoryRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1/characters';

  Future<Inventory> get(String characterId) async => (await _client.getCached(
    '$_api/$characterId/inventory',
    parse: parseObject(Inventory.fromJson),
  )).data;

  /// 201 -> [InventoryApplied]; 202 -> [InventoryPending] with the change
  /// request the DM must approve. [templateId] null creates a custom item from
  /// [overrides] alone.
  Future<InventoryWriteResult> add(
    String characterId, {
    String? templateId,
    int quantity = 1,
    ItemOverrides overrides = const ItemOverrides(),
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/$characterId/inventory',
      data: {
        'templateId': ?templateId,
        'quantity': quantity,
        if (!overrides.isEmpty) 'overrides': overrides.toJson(),
      },
    );
    return _result(response.statusCode, response.data);
  }

  Future<CharacterItem> patch(String characterId, String itemId, InventoryPatch patch) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/$characterId/inventory/$itemId',
      data: patch.toJson(),
    );
    return CharacterItem.fromJson(response.data!);
  }

  /// Spends [amount] units (or charges) of a consumable.
  Future<void> use(String characterId, String itemId, {int amount = 1}) async {
    await _client.dio.post<void>(
      '$_api/$characterId/inventory/$itemId/use',
      data: {'amount': amount},
    );
  }

  /// 204 -> [InventoryApplied]; 202 -> [InventoryPending]. A null [quantity]
  /// removes the whole stack.
  Future<InventoryWriteResult> remove(String characterId, String itemId, {int? quantity}) async {
    final response = await _client.dio.delete<Map<String, dynamic>>(
      '$_api/$characterId/inventory/$itemId',
      data: {'quantity': ?quantity},
    );
    return _result(response.statusCode, response.data);
  }

  /// 200 -> [InventoryApplied]; 202 -> [InventoryPending].
  Future<InventoryWriteResult> adjustMoney(
    String characterId, {
    required int deltaCp,
    required String reason,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/$characterId/money',
      data: {'deltaCp': deltaCp, 'reason': reason},
    );
    return _result(response.statusCode, response.data);
  }

  InventoryWriteResult _result(int? status, Map<String, dynamic>? body) {
    if (status == 202 && body != null) return InventoryPending(ChangeRequest.fromJson(body));
    return const InventoryApplied();
  }
}

final inventoryRepositoryProvider = Provider<InventoryRepository>(
  (ref) => InventoryRepository(ref.watch(apiClientProvider)),
);
