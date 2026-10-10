import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:opentrpg_core/core/cache/cached_result.dart';
import 'package:opentrpg_core/core/network/api_client.dart';

import 'beast_models.dart';
import 'models.dart';

/// Catalog endpoints under `/api/v1/systems/dnd5e/catalog`.
class CatalogRepository {
  CatalogRepository(this._client);

  final ApiClient _client;

  static const _base = '/api/v1/systems/dnd5e/catalog';

  /// Root of every cached catalog answer (see `staleSinceProvider`).
  static const rootPath = _base;

  /// [campaignId] asks for the catalog of that campaign (the base pack, the
  /// packs it enables and its homebrew); without it, the global catalog.
  Future<List<T>> _list<T>(
    String path,
    T Function(Map<String, dynamic>) parse, {
    String? campaignId,
    Map<String, Object?> query = const {},
  }) async => (await _client.getCached(
    '$_base/$path',
    query: {
      for (final e in query.entries)
        if (e.value != null && e.value != '') e.key: e.value,
      'campaignId': ?campaignId,
    },
    parse: parseList(parse),
  )).data;

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

  /// "srd" first, then every imported content pack; with [campaignId] each
  /// one says whether the campaign enables it.
  Future<List<CatalogSource>> sources({String? campaignId}) =>
      _list('sources', CatalogSource.fromJson, campaignId: campaignId);

  Future<Attribution> attribution() => _one('attribution', Attribution.fromJson);

  Future<List<ClassSummary>> classes({String? campaignId}) =>
      _list('classes', ClassSummary.fromJson, campaignId: campaignId);

  Future<ClassDetail> classDetail(String index) =>
      _one('classes/${Uri.encodeComponent(index)}', ClassDetail.fromJson);

  Future<List<RaceSummary>> races({String? campaignId}) =>
      _list('races', RaceSummary.fromJson, campaignId: campaignId);

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
    String? campaignId,
  }) => _page('spells', {
    'campaignId': campaignId,
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
    String? campaignId,
  }) => _page('items', {
    'campaignId': campaignId,
    'search': search?.trim(),
    'category': category,
    'rarity': rarity,
    'page': page,
    'pageSize': pageSize,
  }, ItemSummary.fromJson);

  Future<ItemDetail> itemDetail(String id) =>
      _one('items/${Uri.encodeComponent(id)}', ItemDetail.fromJson);

  /// Creatures ordered by challenge rating: [fly]/[swim] false leave out the
  /// ones with that speed (wild shape limits), true keep only those; [type]
  /// keeps one creature type ("beast" for wild shapes and companions).
  Future<List<BeastSummary>> beasts({
    double? maxCr,
    bool? fly,
    bool? swim,
    String? search,
    String? type,
    String? campaignId,
  }) async {
    final text = search?.trim();
    final result = await _client.getCached(
      '$_base/beasts',
      query: {
        'maxCr': ?maxCr,
        'fly': ?fly,
        'swim': ?swim,
        if (text != null && text.isNotEmpty) 'q': text,
        'type': ?type,
        'campaignId': ?campaignId,
      },
      parse: parseList(BeastSummary.fromJson),
    );
    return result.data;
  }

  Future<Beast> beast(String index) => _one('beasts/${Uri.encodeComponent(index)}', Beast.fromJson);

  Future<List<Condition>> conditions({String? campaignId}) =>
      _list('conditions', Condition.fromJson, campaignId: campaignId);

  /// Rules documents of the content packs ordered by title (empty with the
  /// SRD only); [search] matches the title and [category] keeps one category.
  Future<List<RuleSummary>> rules({String? search, String? category, String? campaignId}) => _list(
    'rules',
    RuleSummary.fromJson,
    campaignId: campaignId,
    query: {'q': search?.trim(), 'category': category},
  );

  Future<Rule> rule(String index) => _one('rules/${Uri.encodeComponent(index)}', Rule.fromJson);

  /// A vocabulary of the catalog ([ReferenceKinds]): the SRD entries and those
  /// of the content packs.
  Future<List<ReferenceEntry>> reference(String kind, {String? campaignId}) => _list(
    'reference/${Uri.encodeComponent(kind)}',
    ReferenceEntry.fromJson,
    campaignId: campaignId,
  );

  Future<List<Skill>> skills() => _list('skills', Skill.fromJson);

  Future<List<Background>> backgrounds({String? campaignId}) =>
      _list('backgrounds', Background.fromJson, campaignId: campaignId);

  Future<EquipmentCategory> equipmentCategory(String index, {String? campaignId}) async =>
      (await _client.getCached(
        '$_base/equipment-categories/${Uri.encodeComponent(index)}',
        query: {'campaignId': ?campaignId},
        parse: parseObject(EquipmentCategory.fromJson),
      )).data;

  /// Trinket table of the content packs, ordered by roll (empty with the SRD only).
  Future<List<Trinket>> trinkets({String? campaignId}) =>
      _list('trinkets', Trinket.fromJson, campaignId: campaignId);

  /// Roll tables of the content packs (empty with the SRD only); [subclass]
  /// keeps only the tables of that subclass.
  Future<List<RollTable>> rollTables({String? subclass, String? campaignId}) => _list(
    'roll-tables',
    RollTable.fromJson,
    campaignId: campaignId,
    query: {'subclass': subclass},
  );

  Future<Feature> feature(String index) =>
      _one('features/${Uri.encodeComponent(index)}', Feature.fromJson);
}

final catalogRepositoryProvider = Provider<CatalogRepository>(
  (ref) => CatalogRepository(ref.watch(apiClientProvider)),
);
