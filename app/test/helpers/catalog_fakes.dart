import 'package:opentrpg_dnd5e/catalog/data/beast_models.dart';
import 'package:opentrpg_dnd5e/catalog/data/catalog_repository.dart';
import 'package:opentrpg_dnd5e/catalog/data/models.dart';

import 'fakes.dart';

const fakeAttributionText =
    'Este trabajo incluye material del SRD 5.1 bajo licencia Creative Commons Attribution 4.0.';

SpellSummary makeSpell({
  String index = 'fireball',
  String name = 'Fireball',
  int level = 3,
  String school = 'Evocation',
  bool concentration = false,
  bool ritual = false,
  String? source,
  String? category,
}) => SpellSummary(
  index: index,
  name: name,
  level: level,
  school: school,
  concentration: concentration,
  ritual: ritual,
  source: source,
  category: category,
);

ItemSummary makeItem({
  String id = 'i1',
  String name = 'Longsword',
  String category = 'Weapon',
  int? costCp = 1500,
}) => ItemSummary(id: id, name: name, category: category, costCp: costCp);

/// One recorded call to [FakeCatalogRepository.spells].
typedef SpellCall = ({String? search, int? level, String? classIndex, int page, int pageSize});

/// In-memory catalog. Spells and items are filtered and paged like the server.
class FakeCatalogRepository implements CatalogRepository {
  FakeCatalogRepository({
    this.spellList = const [],
    this.itemList = const [],
    this.classList = const [],
    this.classDetails = const {},
    this.spellDetails = const {},
    this.itemDetails = const {},
    this.raceList = const [],
    this.raceDetails = const {},
    this.conditionList = const [],
    this.backgroundList = const [],
    this.equipmentCategories = const {},
    this.sourceList = const [CatalogSource(id: 'srd', name: 'SRD 5.1')],
    this.trinketList = const [],
    this.rollTableList = const [],
    this.beastList = const [],
    this.featureDetails = const {},
    this.ruleList = const [],
    this.referenceEntries = const [],
    Map<String, Set<String>>? enabledPacks,
  }) : enabledPacks = enabledPacks ?? {};

  final List<SpellSummary> spellList;
  final List<ItemSummary> itemList;
  final List<ClassSummary> classList;
  final Map<String, ClassDetail> classDetails;
  final Map<String, SpellDetail> spellDetails;
  final Map<String, ItemDetail> itemDetails;
  final List<RaceSummary> raceList;
  final Map<String, RaceDetail> raceDetails;
  final List<Condition> conditionList;
  final List<Background> backgroundList;
  final Map<String, EquipmentCategory> equipmentCategories;
  final List<CatalogSource> sourceList;
  final List<Trinket> trinketList;
  final List<RollTable> rollTableList;

  /// Class features by index (`feature`); a missing one fails like a 404.
  final Map<String, Feature> featureDetails;

  /// Full statblocks; the list endpoint answers their summaries.
  final List<Beast> beastList;
  final List<BeastQuery> beastCalls = [];

  /// Rules documents (the list answers them without body).
  final List<Rule> ruleList;
  final List<ReferenceEntry> referenceEntries;

  /// Packs each campaign enables: asked with a `campaignId` found here, the
  /// lists keep only the base content ("srd" or no source), the homebrew and
  /// the content of these packs, like the server. Campaigns not in the map
  /// see everything.
  final Map<String, Set<String>> enabledPacks;

  /// The `campaignId` of every list call, in order (null: global catalog).
  final List<String?> campaignCalls = [];
  final List<({String? type, String? campaignId})> beastScopes = [];

  bool _visible(String? source, String? campaignId) {
    final enabled = campaignId == null ? null : enabledPacks[campaignId];
    if (enabled == null || source == null || source == 'srd' || source == 'homebrew') return true;
    return enabled.contains(source);
  }

  List<T> _scoped<T>(List<T> all, String? Function(T) sourceOf, String? campaignId) {
    campaignCalls.add(campaignId);
    return [
      for (final e in all)
        if (_visible(sourceOf(e), campaignId)) e,
    ];
  }

  Object? error;
  final List<SpellCall> spellCalls = [];
  final List<({String? search, String? category, int page})> itemCalls = [];

  void _fail() {
    if (error != null) throw error!;
  }

  @override
  Future<List<BeastSummary>> beasts({
    double? maxCr,
    bool? fly,
    bool? swim,
    String? search,
    String? type,
    String? campaignId,
  }) async {
    _fail();
    beastCalls.add((maxCr: maxCr, fly: fly, swim: swim));
    beastScopes.add((type: type, campaignId: campaignId));
    return [
      for (final b in _scoped(beastList, (b) => b.source, campaignId))
        if ((maxCr == null || b.challengeRating <= maxCr) &&
            (fly == null || b.flies == fly) &&
            (swim == null || b.swims == swim) &&
            (type == null || b.type == type))
          b,
    ];
  }

  @override
  Future<Beast> beast(String index) async {
    _fail();
    return beastList.firstWhere((b) => b.index == index, orElse: () => throw dioError(404));
  }

  Page<T> _slice<T>(List<T> all, int page, int pageSize) {
    final start = (page - 1) * pageSize;
    final items = start >= all.length ? <T>[] : all.skip(start).take(pageSize).toList();
    return Page(items: items, total: all.length, page: page, pageSize: pageSize);
  }

  @override
  Future<List<CatalogSource>> sources({String? campaignId}) async {
    _fail();
    campaignCalls.add(campaignId);
    final enabled = campaignId == null ? null : enabledPacks[campaignId];
    return [
      for (final s in sourceList)
        CatalogSource(
          id: s.id,
          name: s.name,
          version: s.version,
          isBase: s.isBase || s.id == 'srd',
          enabled: campaignId == null
              ? s.enabled
              : s.id == 'srd' || s.isBase || (enabled?.contains(s.id) ?? s.enabled ?? false),
        ),
    ];
  }

  @override
  Future<Attribution> attribution() async =>
      const Attribution(ruleset: 'srd-5.1', license: 'CC-BY-4.0', text: fakeAttributionText);

  @override
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
  }) async {
    _fail();
    spellCalls.add((
      search: search,
      level: level,
      classIndex: classIndex,
      page: page,
      pageSize: pageSize,
    ));
    final q = (search ?? '').toLowerCase();
    final filtered = _scoped(spellList, (s) => s.source, campaignId)
        .where((s) => (level == null || s.level == level) && s.name.toLowerCase().contains(q))
        .toList();
    return _slice(filtered, page, pageSize);
  }

  @override
  Future<SpellDetail> spellDetail(String index) async {
    _fail();
    return spellDetails[index] ?? (throw dioError(404));
  }

  @override
  Future<Page<ItemSummary>> items({
    String? search,
    String? category,
    String? rarity,
    int page = 1,
    int pageSize = 50,
    String? campaignId,
  }) async {
    _fail();
    itemCalls.add((search: search, category: category, page: page));
    final q = (search ?? '').toLowerCase();
    final filtered = _scoped(itemList, (i) => i.source, campaignId)
        .where(
          (i) => (category == null || i.category == category) && i.name.toLowerCase().contains(q),
        )
        .toList();
    return _slice(filtered, page, pageSize);
  }

  @override
  Future<ItemDetail> itemDetail(String id) async {
    _fail();
    return itemDetails[id] ?? (throw dioError(404));
  }

  @override
  Future<List<ClassSummary>> classes({String? campaignId}) async {
    _fail();
    return _scoped(classList, (c) => c.source, campaignId);
  }

  @override
  Future<ClassDetail> classDetail(String index) async {
    _fail();
    return classDetails[index] ?? (throw dioError(404));
  }

  @override
  Future<List<RaceSummary>> races({String? campaignId}) async {
    _fail();
    return _scoped(raceList, (r) => r.source, campaignId);
  }

  @override
  Future<RaceDetail> raceDetail(String index) async {
    _fail();
    return raceDetails[index] ?? (throw dioError(404));
  }

  @override
  Future<List<Condition>> conditions({String? campaignId}) async {
    _fail();
    return _scoped(conditionList, (c) => c.source, campaignId);
  }

  @override
  Future<List<RuleSummary>> rules({String? search, String? category, String? campaignId}) async {
    _fail();
    final q = (search ?? '').toLowerCase();
    return [
      for (final r in _scoped(ruleList, (r) => r.source, campaignId))
        if ((category == null || r.category == category) && r.title.toLowerCase().contains(q))
          RuleSummary(
            index: r.index,
            title: r.title,
            category: r.category,
            tags: r.tags,
            source: r.source,
          ),
    ];
  }

  @override
  Future<Rule> rule(String index) async {
    _fail();
    return ruleList.firstWhere((r) => r.index == index, orElse: () => throw dioError(404));
  }

  @override
  Future<List<ReferenceEntry>> reference(String kind, {String? campaignId}) async {
    _fail();
    return [
      for (final e in _scoped(referenceEntries, (e) => e.source, campaignId))
        if (e.kind == kind) e,
    ];
  }

  @override
  Future<List<Skill>> skills() async => const [];

  @override
  Future<List<Background>> backgrounds({String? campaignId}) async {
    _fail();
    return _scoped(backgroundList, (b) => b.source, campaignId);
  }

  @override
  Future<EquipmentCategory> equipmentCategory(String index, {String? campaignId}) async {
    _fail();
    return equipmentCategories[index] ?? (throw dioError(404));
  }

  @override
  Future<List<Trinket>> trinkets({String? campaignId}) async {
    _fail();
    return trinketList;
  }

  @override
  Future<List<RollTable>> rollTables({String? subclass, String? campaignId}) async {
    _fail();
    return [
      for (final t in rollTableList)
        if (subclass == null || t.subclassIndex == subclass) t,
    ];
  }

  @override
  Future<Feature> feature(String index) async {
    _fail();
    final feature = featureDetails[index];
    if (feature == null) throw StateError('Rasgo no encontrado: $index');
    return feature;
  }
}
