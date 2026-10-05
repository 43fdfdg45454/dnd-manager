import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/content/content_visibility.dart';
import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import 'models.dart';

/// Map endpoints: `/campaigns/{id}/maps` and `/maps/{id}`.
class MapsRepository {
  MapsRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1';

  Future<List<MapSummary>> list(String campaignId) async => (await _client.getCached(
    '$_api/campaigns/$campaignId/maps',
    parse: parseList(MapSummary.fromJson),
  )).data;

  /// Root of everything cached for the map [id].
  static String mapPath(String id) => '$_api/maps/$id';

  /// Creates a map from an image uploaded as `MapImage`.
  Future<MapDetail> create(
    String campaignId, {
    required String name,
    required String fileId,
    required ContentVisibility visibility,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/campaigns/$campaignId/maps',
      data: {'name': name, 'fileId': fileId, 'visibility': visibility.apiValue},
    );
    return MapDetail.fromJson(response.data!);
  }

  Future<MapDetail> get(String id) async =>
      (await _client.getCached('$_api/maps/$id', parse: parseObject(MapDetail.fromJson))).data;

  /// Only the non-null fields are sent.
  Future<MapDetail> update(String id, {String? name, ContentVisibility? visibility}) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/maps/$id',
      data: {'name': ?name, 'visibility': ?visibility?.apiValue},
    );
    return MapDetail.fromJson(response.data!);
  }

  Future<void> delete(String id) async {
    await _client.dio.delete<void>('$_api/maps/$id');
  }

  Future<MapPin> createPin(
    String mapId, {
    required double x,
    required double y,
    required PinDraft draft,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/maps/$mapId/pins',
      data: {'x': x, 'y': y, ...draft.toJson()},
    );
    return MapPin.fromJson(response.data!);
  }

  /// Moves a pin: only x and y are sent.
  Future<MapPin> movePin(String mapId, String pinId, {required double x, required double y}) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/maps/$mapId/pins/$pinId',
      data: {'x': x, 'y': y},
    );
    return MapPin.fromJson(response.data!);
  }

  Future<MapPin> updatePin(String mapId, String pinId, PinDraft draft) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/maps/$mapId/pins/$pinId',
      data: draft.toJson(),
    );
    return MapPin.fromJson(response.data!);
  }

  Future<void> deletePin(String mapId, String pinId) async {
    await _client.dio.delete<void>('$_api/maps/$mapId/pins/$pinId');
  }
}

final mapsRepositoryProvider = Provider<MapsRepository>(
  (ref) => MapsRepository(ref.watch(apiClientProvider)),
);
