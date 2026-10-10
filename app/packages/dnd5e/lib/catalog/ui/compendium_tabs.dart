import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:opentrpg_core/core/ui/infinite_scroll_list.dart';
import 'package:opentrpg_core/core/ui/source_chip.dart';

import '../../dnd5e_routes.dart';
import '../../ui/spell_category.dart';
import '../data/beast_models.dart';
import '../data/catalog_controllers.dart';
import '../data/models.dart';
import '../domain/catalog_format.dart';
import 'beast_page.dart';
import 'condition_sheet.dart';
import 'detail_widgets.dart';
import 'roll_table_widgets.dart';

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

/// The campaign the compendium of [context] was opened from (null: the main
/// menu) and the campaign whose catalog its lists ask for (null: the global
/// one).
(String?, String?) _scopeOf(BuildContext context, WidgetRef ref) {
  final page = CompendiumScope.of(context);
  return (page, ref.watch(compendiumCatalogCampaignProvider(page)));
}

class CompendiumSpellsTab extends ConsumerWidget {
  const CompendiumSpellsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final (page, campaign) = _scopeOf(context, ref);
    final filters = ref.watch(spellFiltersProvider(page));
    final filtersController = ref.read(spellFiltersProvider(page).notifier);
    final classes = ref.watch(classesProvider(campaign)).value ?? const <ClassSummary>[];
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
            value: ref.watch(spellsControllerProvider(page)),
            onRetry: () => ref.invalidate(spellsControllerProvider(page)),
            loadMore: ref.read(spellsControllerProvider(page).notifier).loadMore,
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
    final page = CompendiumScope.of(context);
    final category = ref.watch(itemCategoryFilterProvider(page));
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
              onChanged: ref.read(itemCategoryFilterProvider(page).notifier).setCategory,
            ),
          ],
        ),
        Expanded(
          child: _PagedList<ItemSummary>(
            listKey: const Key('item-list'),
            value: ref.watch(itemsControllerProvider(page)),
            onRetry: () => ref.invalidate(itemsControllerProvider(page)),
            loadMore: ref.read(itemsControllerProvider(page).notifier).loadMore,
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

/// List of a small reference collection filtered locally by the shared search
/// and the source picked in the compendium.
class _LocalList<T> extends ConsumerWidget {
  const _LocalList({
    required this.value,
    required this.onRetry,
    required this.nameOf,
    required this.sourceOf,
    required this.itemBuilder,
    required this.emptyText,
  });

  final AsyncValue<List<T>> value;
  final VoidCallback onRetry;
  final String Function(T item) nameOf;
  final String? Function(T item) sourceOf;
  final Widget Function(BuildContext context, T item) itemBuilder;
  final String emptyText;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final page = CompendiumScope.of(context);
    final search = ref.watch(compendiumSearchProvider(page));
    final filter = ref.watch(compendiumFilterProvider(page));
    return CatalogAsyncBody<List<T>>(
      value: value,
      onRetry: onRetry,
      builder: (all) {
        final items = [
          for (final e in all)
            if (_matches(nameOf(e), search) && filter.matchesSource(sourceOf(e))) e,
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
    final (_, campaign) = _scopeOf(context, ref);
    return _LocalList<ClassSummary>(
      value: ref.watch(classesProvider(campaign)),
      onRetry: () => ref.invalidate(classesProvider(campaign)),
      nameOf: (c) => c.name,
      sourceOf: (c) => c.source,
      emptyText: 'No se encontraron clases.',
      itemBuilder: (context, c) => ListTile(
        key: Key('class-${c.index}'),
        title: NameWithSource(c.name, c.source),
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
    final (_, campaign) = _scopeOf(context, ref);
    return _LocalList<RaceSummary>(
      value: ref.watch(racesProvider(campaign)),
      onRetry: () => ref.invalidate(racesProvider(campaign)),
      nameOf: (r) => r.name,
      sourceOf: (r) => r.source,
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

/// Creatures of the catalog (the SRD beasts and the creatures of the content
/// packs), ordered by challenge rating.
class CompendiumBeastsTab extends ConsumerWidget {
  const CompendiumBeastsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final (_, campaign) = _scopeOf(context, ref);
    return _LocalList<BeastSummary>(
      value: ref.watch(creaturesProvider(campaign)),
      onRetry: () => ref.invalidate(creaturesProvider(campaign)),
      nameOf: (b) => b.name,
      sourceOf: (b) => b.source,
      emptyText: 'No se encontraron criaturas.',
      itemBuilder: (context, b) => BeastTile(beast: b),
    );
  }
}

class CompendiumConditionsTab extends ConsumerWidget {
  const CompendiumConditionsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final (_, campaign) = _scopeOf(context, ref);
    return _LocalList<Condition>(
      value: ref.watch(conditionsProvider(campaign)),
      onRetry: () => ref.invalidate(conditionsProvider(campaign)),
      nameOf: (c) => c.name,
      sourceOf: (c) => c.source,
      emptyText: 'No se encontraron condiciones.',
      itemBuilder: (context, c) => ListTile(
        key: Key('condition-${c.index}'),
        title: NameWithSource(c.name, c.source),
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
      sourceOf: (t) => t.source,
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

/// Rules documents of the content packs (variant rules, firearms...): a
/// category picker over the list; empty with the SRD only.
class CompendiumRulesTab extends ConsumerStatefulWidget {
  const CompendiumRulesTab({super.key});

  @override
  ConsumerState<CompendiumRulesTab> createState() => _CompendiumRulesTabState();
}

class _CompendiumRulesTabState extends ConsumerState<CompendiumRulesTab> {
  String? _category;

  @override
  Widget build(BuildContext context) {
    final (_, campaign) = _scopeOf(context, ref);
    final rules = ref.watch(rulesProvider(campaign));
    final categories = {
      for (final r in rules.value ?? const <RuleSummary>[])
        if (r.category.isNotEmpty) r.category,
    }.toList()..sort((a, b) => ruleCategoryLabel(a).compareTo(ruleCategoryLabel(b)));
    final category = categories.contains(_category) ? _category : null;
    return Column(
      children: [
        if (categories.length > 1)
          _FilterBar(
            children: [
              DropdownButtonFormField<String?>(
                key: const Key('filter-rule-category'),
                initialValue: category,
                isExpanded: true,
                decoration: _filterDecoration.copyWith(labelText: 'Categoría'),
                items: [
                  const DropdownMenuItem<String?>(value: null, child: Text('Todas')),
                  for (final c in categories)
                    DropdownMenuItem<String?>(value: c, child: Text(ruleCategoryLabel(c))),
                ],
                onChanged: (value) => setState(() => _category = value),
              ),
            ],
          ),
        Expanded(
          child: _LocalList<RuleSummary>(
            value: rules.whenData(
              (list) => [
                for (final r in list)
                  if (category == null || r.category == category) r,
              ],
            ),
            onRetry: () => ref.invalidate(rulesProvider(campaign)),
            nameOf: (r) => '${r.title} ${r.tags.join(' ')}',
            sourceOf: (r) => r.source,
            emptyText: 'No hay reglas. Llegan con los paquetes de contenido.',
            itemBuilder: (context, r) => ListTile(
              key: Key('rule-${r.index}'),
              title: NameWithSource(r.title, r.source),
              subtitle: Text(
                [if (r.category.isNotEmpty) ruleCategoryLabel(r.category), ...r.tags].join(' · '),
              ),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push(Dnd5eRoutes.rule(r.index)),
            ),
          ),
        ),
      ],
    );
  }
}
