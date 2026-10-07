import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'catalog_repository.dart';
import 'models.dart';

const catalogPageSize = 50;

/// Never retry silently: errors are shown with a retry button.
Duration? _noRetry(int retryCount, Object error) => null;

/// Search text shared by every tab of the compendium (already debounced by the UI).
class CompendiumSearch extends Notifier<String> {
  @override
  String build() => '';

  void set(String value) => state = value.trim();
}

final compendiumSearchProvider = NotifierProvider.autoDispose<CompendiumSearch, String>(
  CompendiumSearch.new,
);

class SpellFilters {
  const SpellFilters({this.level, this.classIndex});

  /// 0 (cantrips) to 9; null means every level.
  final int? level;
  final String? classIndex;
}

class SpellFiltersController extends Notifier<SpellFilters> {
  @override
  SpellFilters build() => const SpellFilters();

  void setLevel(int? level) => state = SpellFilters(level: level, classIndex: state.classIndex);

  void setClass(String? classIndex) =>
      state = SpellFilters(level: state.level, classIndex: classIndex);
}

final spellFiltersProvider = NotifierProvider.autoDispose<SpellFiltersController, SpellFilters>(
  SpellFiltersController.new,
);

class ItemFiltersController extends Notifier<String?> {
  @override
  String? build() => null;

  void setCategory(String? category) => state = category;
}

/// Selected item category (API value) or null for all.
final itemCategoryFilterProvider = NotifierProvider.autoDispose<ItemFiltersController, String?>(
  ItemFiltersController.new,
);

/// Spell list for the current search and filters, loaded page by page. A change
/// in the search or the filters rebuilds it from page 1.
class SpellsController extends AsyncNotifier<Page<SpellSummary>> {
  CatalogRepository get _repository => ref.read(catalogRepositoryProvider);

  @override
  Future<Page<SpellSummary>> build() {
    final search = ref.watch(compendiumSearchProvider);
    final filters = ref.watch(spellFiltersProvider);
    return _repository.spells(
      search: search,
      level: filters.level,
      classIndex: filters.classIndex,
      pageSize: catalogPageSize,
    );
  }

  /// Appends the next page. Errors are rethrown so the UI can report them.
  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || state.isLoading) return;
    final filters = ref.read(spellFiltersProvider);
    final next = await _repository.spells(
      search: ref.read(compendiumSearchProvider),
      level: filters.level,
      classIndex: filters.classIndex,
      page: current.page + 1,
      pageSize: catalogPageSize,
    );
    // The list was rebuilt (new search or filter) while this page was loading.
    if (!identical(state.value, current)) return;
    state = AsyncData(
      next.copyWith(items: [...current.items, ...next.items], page: current.page + 1),
    );
  }
}

final spellsControllerProvider =
    AsyncNotifierProvider.autoDispose<SpellsController, Page<SpellSummary>>(
      SpellsController.new,
      retry: _noRetry,
    );

/// Item list for the current search and category, loaded page by page.
class ItemsController extends AsyncNotifier<Page<ItemSummary>> {
  CatalogRepository get _repository => ref.read(catalogRepositoryProvider);

  @override
  Future<Page<ItemSummary>> build() {
    final search = ref.watch(compendiumSearchProvider);
    final category = ref.watch(itemCategoryFilterProvider);
    return _repository.items(search: search, category: category, pageSize: catalogPageSize);
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || state.isLoading) return;
    final next = await _repository.items(
      search: ref.read(compendiumSearchProvider),
      category: ref.read(itemCategoryFilterProvider),
      page: current.page + 1,
      pageSize: catalogPageSize,
    );
    if (!identical(state.value, current)) return;
    state = AsyncData(
      next.copyWith(items: [...current.items, ...next.items], page: current.page + 1),
    );
  }
}

final itemsControllerProvider =
    AsyncNotifierProvider.autoDispose<ItemsController, Page<ItemSummary>>(
      ItemsController.new,
      retry: _noRetry,
    );

// Small reference lists: loaded once and filtered locally.

final classesProvider = FutureProvider.autoDispose<List<ClassSummary>>(
  (ref) => ref.watch(catalogRepositoryProvider).classes(),
  retry: _noRetry,
);

final racesProvider = FutureProvider.autoDispose<List<RaceSummary>>(
  (ref) => ref.watch(catalogRepositoryProvider).races(),
  retry: _noRetry,
);

final backgroundsProvider = FutureProvider.autoDispose<List<Background>>(
  (ref) => ref.watch(catalogRepositoryProvider).backgrounds(),
  retry: _noRetry,
);

/// Items of an equipment category (starting equipment picker).
final equipmentCategoryProvider = FutureProvider.autoDispose.family<EquipmentCategory, String>(
  (ref, index) => ref.watch(catalogRepositoryProvider).equipmentCategory(index),
  retry: _noRetry,
);

/// Roll tables of the content packs; the argument keeps only the tables of
/// that subclass (null: every table).
final rollTablesProvider = FutureProvider.autoDispose.family<List<RollTable>, String?>(
  (ref, subclass) => ref.watch(catalogRepositoryProvider).rollTables(subclass: subclass),
  retry: _noRetry,
);

final conditionsProvider = FutureProvider.autoDispose<List<Condition>>(
  (ref) => ref.watch(catalogRepositoryProvider).conditions(),
  retry: _noRetry,
);

/// Sources of the catalog (SRD and content packs), to name the pack a piece of
/// content comes from. Kept alive: it is tiny and every chip reads it. Errors
/// (offline without cache) leave the chips showing nothing rather than failing.
final catalogSourcesProvider = FutureProvider<List<CatalogSource>>(
  (ref) => ref.watch(catalogRepositoryProvider).sources(),
  retry: _noRetry,
);

final attributionProvider = FutureProvider.autoDispose<Attribution>(
  (ref) => ref.watch(catalogRepositoryProvider).attribution(),
  retry: _noRetry,
);

// Detail pages.

final spellDetailProvider = FutureProvider.autoDispose.family<SpellDetail, String>(
  (ref, index) => ref.watch(catalogRepositoryProvider).spellDetail(index),
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
