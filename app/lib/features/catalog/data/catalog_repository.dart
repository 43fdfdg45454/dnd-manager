import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import 'models.dart';

/// Catalog endpoints under `/api/v1/catalog`.
class CatalogRepository {
  CatalogRepository(this._client);

  final ApiClient _client;

  static const _base = '/api/v1/catalog';

  /// Root of every cached catalog answer (see `staleSinceProvider`).
  static const rootPath = _base;

  Future<List<T>> _list<T>(String path, T Function(Map<String, dynamic>) parse) async =>
      (await _client.getCached('$_base/$path', parse: parseList(parse))).data;

  Future<T> _one<T>(String path, T Function(Map<String, dynamic>) parse) async =>
      (await _client.getCached('$_base/$path', parse: parseObject(parse))).data;

  Future<Page<T>> _page<T>(
    String path,
    Map<String, Object?> query,
    T Function(Map<String, dynamic>) parse,
  ) async {
    final result = await _client.getCached(
      '$_base/$path',
      // Unset and blank filters are not sent.
      query: {
        for (final e in query.entries)
          if (e.value != null && e.value != '') e.key: e.value,
      },
      parse: (json) => Page.fromJson(json as Map<String, dynamic>, parse),
    );
    return result.data;
  }

  /// "srd" first, then every imported content pack.
  Future<List<CatalogSource>> sources() => _list('sources', CatalogSource.fromJson);

  Future<Attribution> attribution() => _one('attribution', Attribution.fromJson);

  Future<List<ClassSummary>> classes() => _list('classes', ClassSummary.fromJson);

  Future<ClassDetail> classDetail(String index) =>
      _one('classes/${Uri.encodeComponent(index)}', ClassDetail.fromJson);

  Future<List<RaceSummary>> races() => _list('races', RaceSummary.fromJson);

  Future<RaceDetail> raceDetail(String index) =>
      _one('races/${Uri.encodeComponent(index)}', RaceDetail.fromJson);

  /// [classIndex] is sent as the `class` query parameter.
  Future<Page<SpellSummary>> spells({
    String? search,
    int? level,
    String? classIndex,
    String? school,
    bool? ritual,
    bool? concentration,
    int page = 1,
    int pageSize = 50,
  }) => _page('spells', {
    'search': search?.trim(),
    'level': level,
    'class': classIndex,
    'school': school,
    'ritual': ritual,
    'concentration': concentration,
    'page': page,
    'pageSize': pageSize,
  }, SpellSummary.fromJson);

  Future<SpellDetail> spellDetail(String index) =>
      _one('spells/${Uri.encodeComponent(index)}', SpellDetail.fromJson);

  Future<Page<ItemSummary>> items({
    String? search,
    String? category,
    String? rarity,
    int page = 1,
    int pageSize = 50,
  }) => _page('items', {
    'search': search?.trim(),
    'category': category,
    'rarity': rarity,
    'page': page,
    'pageSize': pageSize,
  }, ItemSummary.fromJson);

  Future<ItemDetail> itemDetail(String id) =>
      _one('items/${Uri.encodeComponent(id)}', ItemDetail.fromJson);

  Future<List<Condition>> conditions() => _list('conditions', Condition.fromJson);

  Future<List<Skill>> skills() => _list('skills', Skill.fromJson);

  Future<List<Background>> backgrounds() => _list('backgrounds', Background.fromJson);

  Future<Feature> feature(String index) =>
      _one('features/${Uri.encodeComponent(index)}', Feature.fromJson);
}

final catalogRepositoryProvider = Provider<CatalogRepository>(
  (ref) => CatalogRepository(ref.watch(apiClientProvider)),
);
