import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:opentrpg_core/core/catalog/catalog_sources.dart';
import 'package:opentrpg_core/core/catalog/compendium_search.dart';

import 'beast_models.dart';
import 'catalog_repository.dart';
import 'models.dart';

export 'package:opentrpg_core/core/catalog/catalog_sources.dart';
export 'package:opentrpg_core/core/catalog/compendium_search.dart';

const catalogPageSize = 50;

/// Never retry silently: errors are shown with a retry button.
Duration? _noRetry(int retryCount, Object error) => null;

class SpellFilters {
  const SpellFilters({this.level, this.classIndex});

  /// 0 (cantrips) to 9; null means every level.
  final int? level;
  final String? classIndex;
}

/// Spell filters of the compendium opened from a campaign (the argument;
/// null: the compendium of the main menu).
class SpellFiltersController extends Notifier<SpellFilters> {
  SpellFiltersController(this.compendium);

  final String? compendium;

  @override
  SpellFilters build() => const SpellFilters();

  void setLevel(int? level) => state = SpellFilters(level: level, classIndex: state.classIndex);

  void setClass(String? classIndex) =>
      state = SpellFilters(level: state.level, classIndex: classIndex);
}

final spellFiltersProvider = NotifierProvider.autoDispose
    .family<SpellFiltersController, SpellFilters, String?>(SpellFiltersController.new);

class ItemFiltersController extends Notifier<String?> {
  ItemFiltersController(this.compendium);

  final String? compendium;

  @override
  String? build() => null;

  void setCategory(String? category) => state = category;
}

/// Selected item category (API value) or null for all, per compendium.
final itemCategoryFilterProvider = NotifierProvider.autoDispose
    .family<ItemFiltersController, String?, String?>(ItemFiltersController.new);

/// Makes [ref] reload when the catalog of the instance changes or, with
/// [campaignId], when the packs that campaign enables change.
void watchCatalogScope(Ref ref, String? campaignId) {
  ref.watch(catalogRevisionProvider);
  if (campaignId != null) ref.watch(campaignCatalogRevisionProvider(campaignId));
}

/// A paged list of the compendium opened from [compendium] (a campaign or
/// null), for the current search, filters and scope. With a source picked,
/// only its entries are kept, so more pages are loaded until one matches.
abstract class _CompendiumPagedController<T> extends AsyncNotifier<Page<T>> {
  _CompendiumPagedController(this.compendium);

  /// The campaign the compendium was opened from (null: the main menu).
  final String? compendium;

  CatalogRepository get repository => ref.read(catalogRepositoryProvider);

  /// Last page of the server answers loaded and their total.
  int _serverPage = 0;
  int _serverTotal = 0;

  /// Fetches the server page [page] with the current search and filters.
  Future<Page<T>> fetch(int page, {required String? campaignId});

  /// Watches the filters of the list; called once per build.
  void watchFilters();

  String? sourceOf(T item);

  @override
  Future<Page<T>> build() async {
    ref.watch(compendiumSearchProvider(compendium));
    watchFilters();
    final campaignId = ref.watch(compendiumCatalogCampaignProvider(compendium));
    final filter = ref.watch(compendiumFilterProvider(compendium));
    watchCatalogScope(ref, campaignId);
    _serverPage = 0;
    _serverTotal = 0;
    return _loadMatching(const [], filter, campaignId);
  }

  /// Loads server pages after the current one until a page brings an entry
  /// of the picked source (or there are no more pages).
  Future<Page<T>> _loadMatching(
    List<T> current,
    CompendiumFilter filter,
    String? campaignId,
  ) async {
    final items = [...current];
    final start = items.length;
    do {
      final next = await fetch(_serverPage + 1, campaignId: campaignId);
      _serverPage = next.page;
      _serverTotal = next.total;
      items.addAll(next.items.where((e) => filter.matchesSource(sourceOf(e))));
      if (next.items.isEmpty) break;
    } while (items.length == start && _serverPage * catalogPageSize < _serverTotal);
    final more = _serverPage * catalogPageSize < _serverTotal;
    // `hasMore` is items.length < total: one more than the shown entries while
    // the server has pages left.
    return Page(
      items: items,
      total: more ? items.length + 1 : items.length,
      page: _serverPage,
      pageSize: catalogPageSize,
    );
  }

  /// Appends the next page. Errors are rethrown so the UI can report them.
  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || state.isLoading) return;
    final next = await _loadMatching(
      current.items,
      ref.read(compendiumFilterProvider(compendium)),
      ref.read(compendiumCatalogCampaignProvider(compendium)),
    );
    // The list was rebuilt (new search or filter) while this page was loading.
    if (!identical(state.value, current)) return;
    state = AsyncData(next);
  }
}

/// Spell list of a compendium for the current search and filters, loaded
/// page by page. A change in the search or the filters rebuilds it from page 1.
class SpellsController extends _CompendiumPagedController<SpellSummary> {
  SpellsController(super.compendium);

  @override
  void watchFilters() => ref.watch(spellFiltersProvider(compendium));

  @override
  String? sourceOf(SpellSummary item) => item.source;

  @override
  Future<Page<SpellSummary>> fetch(int page, {required String? campaignId}) {
    final filters = ref.read(spellFiltersProvider(compendium));
    return repository.spells(
      search: ref.read(compendiumSearchProvider(compendium)),
      level: filters.level,
      classIndex: filters.classIndex,
      page: page,
      pageSize: catalogPageSize,
      campaignId: campaignId,
    );
  }
}

final spellsControllerProvider = AsyncNotifierProvider.autoDispose
    .family<SpellsController, Page<SpellSummary>, String?>(SpellsController.new, retry: _noRetry);

/// Item list of a compendium for the current search and category, loaded
/// page by page.
class ItemsController extends _CompendiumPagedController<ItemSummary> {
  ItemsController(super.compendium);

  @override
  void watchFilters() => ref.watch(itemCategoryFilterProvider(compendium));

  @override
  String? sourceOf(ItemSummary item) => item.source;

  @override
  Future<Page<ItemSummary>> fetch(int page, {required String? campaignId}) => repository.items(
    search: ref.read(compendiumSearchProvider(compendium)),
    category: ref.read(itemCategoryFilterProvider(compendium)),
    page: page,
    pageSize: catalogPageSize,
    campaignId: campaignId,
  );
}

final itemsControllerProvider = AsyncNotifierProvider.autoDispose
    .family<ItemsController, Page<ItemSummary>, String?>(ItemsController.new, retry: _noRetry);

// Small reference lists: loaded once and filtered locally.

// They depend on the content packs: the argument is the campaign whose
// catalog they show (null: the global one, every pack), and a new revision of
// the catalog or of the packs of the campaign reloads them. The server sends
// them already filtered; the app does not filter by pack.

final classesProvider = FutureProvider.autoDispose.family<List<ClassSummary>, String?>((
  ref,
  campaignId,
) {
  watchCatalogScope(ref, campaignId);
  return ref.watch(catalogRepositoryProvider).classes(campaignId: campaignId);
}, retry: _noRetry);

final racesProvider = FutureProvider.autoDispose.family<List<RaceSummary>, String?>((
  ref,
  campaignId,
) {
  watchCatalogScope(ref, campaignId);
  return ref.watch(catalogRepositoryProvider).races(campaignId: campaignId);
}, retry: _noRetry);

final backgroundsProvider = FutureProvider.autoDispose.family<List<Background>, String?>((
  ref,
  campaignId,
) {
  watchCatalogScope(ref, campaignId);
  return ref.watch(catalogRepositoryProvider).backgrounds(campaignId: campaignId);
}, retry: _noRetry);

/// Rules documents of the content packs (pestaña Reglas).
final rulesProvider = FutureProvider.autoDispose.family<List<RuleSummary>, String?>((
  ref,
  campaignId,
) {
  watchCatalogScope(ref, campaignId);
  return ref.watch(catalogRepositoryProvider).rules(campaignId: campaignId);
}, retry: _noRetry);

final ruleDetailProvider = FutureProvider.autoDispose.family<Rule, String>(
  (ref, index) => ref.watch(catalogRepositoryProvider).rule(index),
  retry: _noRetry,
);

/// A vocabulary of the catalog (weapon properties, languages...) in the
/// global scope, by index.
final referenceProvider = FutureProvider.autoDispose.family<Map<String, ReferenceEntry>, String>((
  ref,
  kind,
) async {
  ref.watch(catalogRevisionProvider);
  final entries = await ref.watch(catalogRepositoryProvider).reference(kind);
  return {for (final e in entries) e.index: e};
}, retry: _noRetry);

/// Every creature of the catalog (any type) for the compendium.
final creaturesProvider = FutureProvider.autoDispose.family<List<BeastSummary>, String?>((
  ref,
  campaignId,
) {
  watchCatalogScope(ref, campaignId);
  return ref.watch(catalogRepositoryProvider).beasts(campaignId: campaignId);
}, retry: _noRetry);

/// Items of an equipment category (starting equipment picker) in the catalog
/// of a campaign (null: the global one).
final equipmentCategoryProvider = FutureProvider.autoDispose
    .family<EquipmentCategory, ({String index, String? campaignId})>(
      (ref, key) => ref
          .watch(catalogRepositoryProvider)
          .equipmentCategory(key.index, campaignId: key.campaignId),
      retry: _noRetry,
    );

/// Roll tables of the content packs; the argument keeps only the tables of
/// that subclass (null: every table).
final rollTablesProvider = FutureProvider.autoDispose.family<List<RollTable>, String?>(
  (ref, subclass) => ref.watch(catalogRepositoryProvider).rollTables(subclass: subclass),
  retry: _noRetry,
);

/// Beasts (type "beast") matching a wild shape or companion query.
final beastsProvider = FutureProvider.autoDispose.family<List<BeastSummary>, BeastQuery>(
  (ref, q) => ref
      .watch(catalogRepositoryProvider)
      .beasts(maxCr: q.maxCr, fly: q.fly, swim: q.swim, type: 'beast'),
  retry: _noRetry,
);

final beastDetailProvider = FutureProvider.autoDispose.family<Beast, String>(
  (ref, index) => ref.watch(catalogRepositoryProvider).beast(index),
  retry: _noRetry,
);

final conditionsProvider = FutureProvider.autoDispose.family<List<Condition>, String?>((
  ref,
  campaignId,
) {
  watchCatalogScope(ref, campaignId);
  return ref.watch(catalogRepositoryProvider).conditions(campaignId: campaignId);
}, retry: _noRetry);

final attributionProvider = FutureProvider.autoDispose<Attribution>(
  (ref) => ref.watch(catalogRepositoryProvider).attribution(),
  retry: _noRetry,
);

/// The attribution text the server sends (empty when it sends none), for the
/// attributions page (`SystemAttribution.serverText`).
final attributionTextProvider = FutureProvider.autoDispose<String>(
  (ref) async => (await ref.watch(attributionProvider.future)).text,
  retry: _noRetry,
);

// Detail pages.

final spellDetailProvider = FutureProvider.autoDispose.family<SpellDetail, String>(
  (ref, index) => ref.watch(catalogRepositoryProvider).spellDetail(index),
  retry: _noRetry,
);

/// A class or subclass feature (`GET /catalog/features/{index}`).
final featureDetailProvider = FutureProvider.autoDispose.family<Feature, String>(
  (ref, index) => ref.watch(catalogRepositoryProvider).feature(index),
  retry: _noRetry,
);

final itemDetailProvider = FutureProvider.autoDispose.family<ItemDetail, String>(
  (ref, id) => ref.watch(catalogRepositoryProvider).itemDetail(id),
  retry: _noRetry,
);

final classDetailProvider = FutureProvider.autoDispose.family<ClassDetail, String>(
  (ref, index) => ref.watch(catalogRepositoryProvider).classDetail(index),
  retry: _noRetry,
);

final raceDetailProvider = FutureProvider.autoDispose.family<RaceDetail, String>(
  (ref, index) => ref.watch(catalogRepositoryProvider).raceDetail(index),
  retry: _noRetry,
);
