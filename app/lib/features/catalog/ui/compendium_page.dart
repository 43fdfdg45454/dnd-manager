import 'dart:async';

import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/cache/stale_data.dart';
import '../../../core/ui/infinite_scroll_list.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../../core/ui/source_chip.dart';
import '../../../systems/dnd5e/ui/spell_category.dart';
import '../data/beast_models.dart';
import '../data/catalog_controllers.dart';
import '../data/catalog_repository.dart';
import '../data/models.dart';
import '../domain/catalog_format.dart';
import 'beast_page.dart';
import 'condition_sheet.dart';
import 'detail_widgets.dart';
import 'roll_table_widgets.dart';
import '../../../systems/dnd5e/dnd5e_routes.dart';

const searchDebounce = Duration(milliseconds: 300);

const _fallbackAttribution =
    'Contenido del System Reference Document 5.1, bajo licencia Creative Commons '
    'Attribution 4.0 International (CC-BY 4.0).';

/// Compendium of SRD content: spells, items, classes, races and conditions
/// behind one search box in the app bar.
class CompendiumPage extends ConsumerStatefulWidget {
  const CompendiumPage({super.key});

  @override
  ConsumerState<CompendiumPage> createState() => _CompendiumPageState();
}

class _CompendiumPageState extends ConsumerState<CompendiumPage> {
  final _searchController = TextEditingController();
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    setState(() {}); // Refreshes the clear button.
    _debounce?.cancel();
    _debounce = Timer(searchDebounce, () {
      ref.read(compendiumSearchProvider.notifier).set(value);
    });
  }

  void _clearSearch() {
    _debounce?.cancel();
    _searchController.clear();
    ref.read(compendiumSearchProvider.notifier).set('');
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 7,
      child: Scaffold(
        appBar: AppBar(
          title: TextField(
            key: const Key('compendium-search'),
            controller: _searchController,
            onChanged: _onSearchChanged,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: 'Buscar en el compendio',
              border: InputBorder.none,
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _searchController.text.isEmpty
                  ? null
                  : IconButton(
                      key: const Key('compendium-search-clear'),
                      tooltip: 'Borrar búsqueda',
                      icon: const Icon(Icons.close),
                      onPressed: _clearSearch,
                    ),
            ),
          ),
          bottom: const TabBar(
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            tabs: [
              Tab(key: Key('tab-spells'), text: 'Hechizos'),
              Tab(key: Key('tab-items'), text: 'Objetos'),
              Tab(key: Key('tab-classes'), text: 'Clases'),
              Tab(key: Key('tab-races'), text: 'Razas'),
              Tab(key: Key('tab-beasts'), text: 'Bestias'),
              Tab(key: Key('tab-conditions'), text: 'Condiciones'),
              Tab(key: Key('tab-tables'), text: 'Tablas'),
            ],
          ),
        ),
        body: Column(
          children: [
            OfflineBanner(scopes: [staleTree(CatalogRepository.rootPath)]),
            const Expanded(
              child: TabBarView(
                children: [
                  _KeepAlive(child: CompendiumSpellsTab()),
                  _KeepAlive(child: CompendiumItemsTab()),
                  _KeepAlive(child: CompendiumClassesTab()),
                  _KeepAlive(child: CompendiumRacesTab()),
                  _KeepAlive(child: CompendiumBeastsTab()),
                  _KeepAlive(child: CompendiumConditionsTab()),
                  _KeepAlive(child: CompendiumTablesTab()),
                ],
              ),
            ),
            const _AttributionFooter(),
          ],
        ),
      ),
    );
  }
}

/// Keeps a tab (and its scroll position and loaded pages) alive while another
/// tab is shown.
class _KeepAlive extends StatefulWidget {
  const _KeepAlive({required this.child});

  final Widget child;

  @override
  State<_KeepAlive> createState() => _KeepAliveState();
}

class _KeepAliveState extends State<_KeepAlive> with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return widget.child;
  }
}

class _AttributionFooter extends ConsumerWidget {
  const _AttributionFooter();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final text = ref.watch(attributionProvider).value?.text;
    final theme = Theme.of(context);
    return Material(
      color: theme.colorScheme.surfaceContainer,
      child: SafeArea(
        top: false,
        child: Container(
          width: double.infinity,
          constraints: const BoxConstraints(maxHeight: 96),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: SingleChildScrollView(
            child: Text(
              text == null || text.isEmpty ? _fallbackAttribution : text,
              key: const Key('compendium-attribution'),
              style: theme.textTheme.bodySmall,
            ),
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Paged tabs (spells, items)
// ---------------------------------------------------------------------------

class _FilterBar extends StatelessWidget {
  const _FilterBar({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: Row(
        children: [
          for (var i = 0; i < children.length; i++) ...[
            if (i > 0) const SizedBox(width: 12),
            Expanded(child: children[i]),
          ],
        ],
      ),
    );
  }
}

const _filterDecoration = InputDecoration(
  isDense: true,
  border: OutlineInputBorder(),
  contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
);

class CompendiumSpellsTab extends ConsumerWidget {
  const CompendiumSpellsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final filters = ref.watch(spellFiltersProvider);
    final filtersController = ref.read(spellFiltersProvider.notifier);
    final classes = ref.watch(classesProvider).value ?? const <ClassSummary>[];
    // Only casters can have spells; fall back to all if the flag is missing.
    final casters = classes.where((c) => c.isSpellcaster).toList();
    final classOptions = casters.isEmpty ? classes : casters;

    return Column(
      children: [
        _FilterBar(
          children: [
            DropdownButtonFormField<int?>(
              key: const Key('filter-spell-level'),
              initialValue: filters.level,
              isExpanded: true,
              decoration: _filterDecoration.copyWith(labelText: 'Nivel'),
              items: [
                const DropdownMenuItem<int?>(value: null, child: Text('Todos')),
                for (var level = 0; level <= 9; level++)
                  DropdownMenuItem<int?>(value: level, child: Text(spellLevelLabel(level))),
              ],
              onChanged: filtersController.setLevel,
            ),
            DropdownButtonFormField<String?>(
              key: const Key('filter-spell-class'),
              initialValue: filters.classIndex,
              isExpanded: true,
              decoration: _filterDecoration.copyWith(labelText: 'Clase'),
              items: [
                const DropdownMenuItem<String?>(value: null, child: Text('Todas')),
                for (final c in classOptions)
                  DropdownMenuItem<String?>(value: c.index, child: Text(c.name)),
              ],
              onChanged: filtersController.setClass,
            ),
          ],
        ),
        Expanded(
          child: _PagedList<SpellSummary>(
            listKey: const Key('spell-list'),
            value: ref.watch(spellsControllerProvider),
            onRetry: () => ref.invalidate(spellsControllerProvider),
            loadMore: ref.read(spellsControllerProvider.notifier).loadMore,
            emptyText: 'No se encontraron hechizos.',
            itemBuilder: (context, spell) => ListTile(
              key: Key('spell-${spell.index}'),
              leading: SpellCategoryIcon(spell.category),
              title: NameWithSource(spell.name, spell.source),
              subtitle: Text(
                [
                  spellLevelLabel(spell.level),
                  ?spell.school,
                  if (spell.concentration) 'Concentración',
                  if (spell.ritual) 'Ritual',
                  for (final e in spell.expandedBy) e.label,
                ].join(' · '),
              ),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push(Dnd5eRoutes.spell(spell.index)),
            ),
          ),
        ),
      ],
    );
  }
}

class CompendiumItemsTab extends ConsumerWidget {
  const CompendiumItemsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final category = ref.watch(itemCategoryFilterProvider);
    return Column(
      children: [
        _FilterBar(
          children: [
            DropdownButtonFormField<String?>(
              key: const Key('filter-item-category'),
              initialValue: category,
              isExpanded: true,
              decoration: _filterDecoration.copyWith(labelText: 'Categoría'),
              items: [
                const DropdownMenuItem<String?>(value: null, child: Text('Todas')),
                for (final entry in itemCategories.entries)
                  DropdownMenuItem<String?>(value: entry.key, child: Text(entry.value)),
              ],
              onChanged: ref.read(itemCategoryFilterProvider.notifier).setCategory,
            ),
          ],
        ),
        Expanded(
          child: _PagedList<ItemSummary>(
            listKey: const Key('item-list'),
            value: ref.watch(itemsControllerProvider),
            onRetry: () => ref.invalidate(itemsControllerProvider),
            loadMore: ref.read(itemsControllerProvider.notifier).loadMore,
            emptyText: 'No se encontraron objetos.',
            itemBuilder: (context, item) => ListTile(
              key: Key('item-${item.id}'),
              title: NameWithSource(item.name, item.source),
              subtitle: Text(
                [
                  itemCategoryLabel(item.category),
                  if (item.costCp != null) formatCostCp(item.costCp),
                  if (item.rarity != null) rarityLabel(item.rarity),
                ].where((e) => e.isNotEmpty).join(' · '),
              ),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push(Dnd5eRoutes.item(item.id)),
            ),
          ),
        ),
      ],
    );
  }
}

/// Infinite-scroll list over a paged [AsyncValue]. Keeps the previous results
/// visible while a new search or filter loads.
class _PagedList<T> extends StatelessWidget {
  const _PagedList({
    required this.listKey,
    required this.value,
    required this.onRetry,
    required this.loadMore,
    required this.itemBuilder,
    required this.emptyText,
  });

  final Key listKey;
  final AsyncValue<Page<T>> value;
  final VoidCallback onRetry;
  final Future<void> Function() loadMore;
  final Widget Function(BuildContext context, T item) itemBuilder;
  final String emptyText;

  @override
  Widget build(BuildContext context) {
    return value.when(
      skipLoadingOnReload: true,
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => CatalogErrorView(error: error, onRetry: onRetry),
      data: (page) {
        if (page.items.isEmpty) {
          return Center(child: Text(emptyText));
        }
        return Column(
          children: [
            if (value.isLoading) const LinearProgressIndicator(),
            Expanded(
              child: InfiniteScrollList(
                listKey: listKey,
                itemCount: page.items.length,
                // No new pages while a new search or filter replaces the list.
                hasMore: page.hasMore && !value.isLoading,
                onLoadMore: loadMore,
                itemBuilder: (context, i) => itemBuilder(context, page.items[i]),
              ),
            ),
          ],
        );
      },
    );
  }
}

// ---------------------------------------------------------------------------
// Local lists (classes, races, conditions)
// ---------------------------------------------------------------------------

bool _matches(String name, String search) => name.toLowerCase().contains(search.toLowerCase());

/// List of a small reference collection filtered locally by the shared search.
class _LocalList<T> extends ConsumerWidget {
  const _LocalList({
    required this.value,
    required this.onRetry,
    required this.nameOf,
    required this.itemBuilder,
    required this.emptyText,
  });

  final AsyncValue<List<T>> value;
  final VoidCallback onRetry;
  final String Function(T item) nameOf;
  final Widget Function(BuildContext context, T item) itemBuilder;
  final String emptyText;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final search = ref.watch(compendiumSearchProvider);
    return CatalogAsyncBody<List<T>>(
      value: value,
      onRetry: onRetry,
      builder: (all) {
        final items = [
          for (final e in all)
            if (_matches(nameOf(e), search)) e,
        ];
        if (items.isEmpty) return Center(child: Text(emptyText));
        return ListView.builder(
          itemCount: items.length,
          itemBuilder: (context, i) => itemBuilder(context, items[i]),
        );
      },
    );
  }
}

class CompendiumClassesTab extends ConsumerWidget {
  const CompendiumClassesTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return _LocalList<ClassSummary>(
      value: ref.watch(classesProvider),
      onRetry: () => ref.invalidate(classesProvider),
      nameOf: (c) => c.name,
      emptyText: 'No se encontraron clases.',
      itemBuilder: (context, c) => ListTile(
        key: Key('class-${c.index}'),
        title: Text(c.name),
        subtitle: Text(
          [
            if (c.hitDie != null) 'Dado de golpe d${c.hitDie}',
            if (c.isSpellcaster) 'Lanzador de conjuros',
          ].join(' · '),
        ),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => context.push(Dnd5eRoutes.dndClass(c.index)),
      ),
    );
  }
}

class CompendiumRacesTab extends ConsumerWidget {
  const CompendiumRacesTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return _LocalList<RaceSummary>(
      value: ref.watch(racesProvider),
      onRetry: () => ref.invalidate(racesProvider),
      nameOf: (r) => r.name,
      emptyText: 'No se encontraron razas.',
      itemBuilder: (context, r) => ListTile(
        key: Key('race-${r.index}'),
        title: NameWithSource(r.name, r.source),
        subtitle: Text([if (r.speed != null) 'Velocidad ${r.speed} pies', ?r.size].join(' · ')),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => context.push(Dnd5eRoutes.race(r.index)),
      ),
    );
  }
}

/// SRD beasts (wild shapes), ordered by challenge rating.
class CompendiumBeastsTab extends ConsumerWidget {
  const CompendiumBeastsTab({super.key});

  static const BeastQuery _all = (maxCr: null, fly: null, swim: null);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return _LocalList<BeastSummary>(
      value: ref.watch(beastsProvider(_all)),
      onRetry: () => ref.invalidate(beastsProvider(_all)),
      nameOf: (b) => b.name,
      emptyText: 'No se encontraron bestias.',
      itemBuilder: (context, b) => BeastTile(beast: b),
    );
  }
}

class CompendiumConditionsTab extends ConsumerWidget {
  const CompendiumConditionsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return _LocalList<Condition>(
      value: ref.watch(conditionsProvider),
      onRetry: () => ref.invalidate(conditionsProvider),
      nameOf: (c) => c.name,
      emptyText: 'No se encontraron condiciones.',
      itemBuilder: (context, c) => ListTile(
        key: Key('condition-${c.index}'),
        title: Text(c.name),
        trailing: const Icon(Icons.info_outline),
        onTap: () => showConditionSheet(context, c),
      ),
    );
  }
}

/// Roll tables of the content packs (wild magic surge…); empty with the SRD only.
class CompendiumTablesTab extends ConsumerWidget {
  const CompendiumTablesTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return _LocalList<RollTable>(
      value: ref.watch(rollTablesProvider(null)),
      onRetry: () => ref.invalidate(rollTablesProvider(null)),
      nameOf: (t) => t.name,
      emptyText: 'No hay tablas de tirada. Llegan con los paquetes de contenido.',
      itemBuilder: (context, t) => ListTile(
        key: Key('roll-table-${t.key}'),
        title: NameWithSource(t.name, t.source),
        subtitle: Text(
          [
            '1${t.dice}',
            '${t.entries.length} ${t.entries.length == 1 ? 'entrada' : 'entradas'}',
          ].join(' · '),
        ),
        trailing: const Icon(Icons.chevron_right),
        onTap: () =>
            Navigator.of(context)
                .push(MaterialPageRoute<void>(builder: (_) => RollTablePage(table: t))),
      ),
    );
  }
}
