import 'package:dnd_companion/features/catalog/data/beast_models.dart';
import 'package:dnd_companion/features/catalog/data/catalog_repository.dart';
import 'package:dnd_companion/features/catalog/data/models.dart';

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
  });

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

  /// Full statblocks; the list endpoint answers their summaries.
  final List<Beast> beastList;
  final List<BeastQuery> beastCalls = [];
  Object? error;
  final List<SpellCall> spellCalls = [];
  final List<({String? search, String? category, int page})> itemCalls = [];

  void _fail() {
    if (error != null) throw error!;
  }

  @override
  Future<List<BeastSummary>> beasts({double? maxCr, bool? fly, bool? swim, String? search}) async {
    _fail();
    beastCalls.add((maxCr: maxCr, fly: fly, swim: swim));
    return [
      for (final b in beastList)
        if ((maxCr == null || b.challengeRating <= maxCr) &&
            (fly == null || b.flies == fly) &&
            (swim == null || b.swims == swim))
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
  Future<List<CatalogSource>> sources() async {
    _fail();
    return sourceList;
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
    final filtered = spellList
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
  }) async {
    _fail();
    itemCalls.add((search: search, category: category, page: page));
    final q = (search ?? '').toLowerCase();
    final filtered = itemList
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
  Future<List<ClassSummary>> classes() async {
    _fail();
    return classList;
  }

  @override
  Future<ClassDetail> classDetail(String index) async {
    _fail();
    return classDetails[index] ?? (throw dioError(404));
  }

  @override
  Future<List<RaceSummary>> races() async {
    _fail();
    return raceList;
  }

  @override
  Future<RaceDetail> raceDetail(String index) async {
    _fail();
    return raceDetails[index] ?? (throw dioError(404));
  }

  @override
  Future<List<Condition>> conditions() async {
    _fail();
    return conditionList;
  }

  @override
  Future<List<Skill>> skills() async => const [];

  @override
  Future<List<Background>> backgrounds() async {
    _fail();
    return backgroundList;
  }

  @override
  Future<EquipmentCategory> equipmentCategory(String index) async {
    _fail();
    return equipmentCategories[index] ?? (throw dioError(404));
  }

  @override
  Future<List<Trinket>> trinkets() async {
    _fail();
    return trinketList;
  }

  @override
  Future<List<RollTable>> rollTables({String? subclass}) async {
    _fail();
    return [
      for (final t in rollTableList)
        if (subclass == null || t.subclassIndex == subclass) t,
    ];
  }

  @override
  Future<Feature> feature(String index) => throw UnimplementedError();
}
