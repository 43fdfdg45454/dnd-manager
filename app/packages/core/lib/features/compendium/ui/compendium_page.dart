import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/stale_data.dart';
import '../../../core/catalog/catalog_models.dart';
import '../../../core/catalog/catalog_sources.dart';
import '../../../core/catalog/compendium_search.dart';
import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/ui/offline_widgets.dart';

const searchDebounce = Duration(milliseconds: 300);

/// Compendium of the rules content of a game system (D&D 5e: spells, items,
/// classes, races, beasts, conditions, rules and tables) behind one search box
/// in the app bar and a source picker. The tabs come from
/// [GameSystemUi.compendiumTabs]; each one reads [compendiumSearchProvider]
/// and [compendiumFilterProvider] of its page ([CompendiumScope]).
///
/// From the main menu ([campaignId] null) it shows the catalog of the default
/// system with every content pack. Opened from a campaign, it is the system of
/// the campaign and "Solo lo activo en la campaña" narrows it to the packs the
/// campaign enables.
class CompendiumPage extends ConsumerStatefulWidget {
  const CompendiumPage({super.key, this.campaignId});

  final String? campaignId;

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
      ref.read(compendiumSearchProvider(widget.campaignId).notifier).set(value);
    });
  }

  void _clearSearch() {
    _debounce?.cancel();
    _searchController.clear();
    ref.read(compendiumSearchProvider(widget.campaignId).notifier).set('');
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final campaignId = widget.campaignId;
    final system = campaignId == null
        ? ref.watch(defaultGameSystemUiProvider)
        : ref.watch(campaignSystemUiProvider(campaignId));
    // The search and the filters live while the page does.
    ref.watch(compendiumSearchProvider(campaignId));
    ref.watch(compendiumFilterProvider(campaignId));
    final tabs = system.compendiumTabs();
    return DefaultTabController(
      length: tabs.length,
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
          bottom: TabBar(
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            tabs: [for (final tab in tabs) Tab(key: tab.tabKey, text: tab.label)],
          ),
        ),
        body: Column(
          children: [
            // Every catalog answer of the system lives below this path.
            OfflineBanner(scopes: [staleTree('/api/v1/systems/${system.id}/catalog')]),
            _ScopeBar(campaignId: campaignId),
            Expanded(
              child: CompendiumScope(
                campaignId: campaignId,
                child: TabBarView(
                  children: [
                    for (final tab in tabs) _KeepAlive(child: Builder(builder: tab.builder)),
                  ],
                ),
              ),
            ),
            if (system.attributions.isNotEmpty)
              _AttributionFooter(credit: system.attributions.first),
          ],
        ),
      ),
    );
  }
}

/// The source picker ("Todo" or one pack) and, in the compendium of a
/// campaign, the "Solo lo activo en la campaña" switch.
class _ScopeBar extends ConsumerWidget {
  const _ScopeBar({required this.campaignId});

  final String? campaignId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final filter = ref.watch(compendiumFilterProvider(campaignId));
    final controller = ref.read(compendiumFilterProvider(campaignId).notifier);
    final all = ref.watch(compendiumSourcesProvider(campaignId)).value ?? const <CatalogSource>[];
    // With the campaign scope only its packs can be picked.
    final sources = [
      for (final s in all)
        if (!filter.campaignOnly || s.isBase || (s.enabled ?? true)) s,
    ];
    final selected = sources.any((s) => s.id == filter.source) ? filter.source : null;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: Wrap(
        spacing: 12,
        runSpacing: 8,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          ConstrainedBox(
            constraints: const BoxConstraints(minWidth: 160, maxWidth: 280),
            child: InputDecorator(
              decoration: const InputDecoration(
                labelText: 'Fuente',
                isDense: true,
                border: OutlineInputBorder(),
                contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              ),
              child: DropdownButtonHideUnderline(
                child: DropdownButton<String?>(
                  key: const Key('compendium-source'),
                  value: selected,
                  isExpanded: true,
                  isDense: true,
                  items: [
                    const DropdownMenuItem<String?>(
                      key: Key('compendium-source-all'),
                      value: null,
                      child: Text('Todo'),
                    ),
                    for (final s in sources)
                      DropdownMenuItem<String?>(
                        key: Key('compendium-source-${s.id}'),
                        value: s.id,
                        child: Text(s.name, overflow: TextOverflow.ellipsis),
                      ),
                  ],
                  onChanged: controller.setSource,
                ),
              ),
            ),
          ),
          if (campaignId != null)
            FilterChip(
              key: const Key('compendium-campaign-only'),
              label: const Text('Solo lo activo en la campaña'),
              selected: filter.campaignOnly,
              onSelected: controller.setCampaignOnly,
            ),
        ],
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

/// The credit of the rules content, under the tabs.
class _AttributionFooter extends ConsumerWidget {
  const _AttributionFooter({required this.credit});

  final SystemAttribution credit;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final serverText = credit.serverText;
    final text = serverText == null ? null : ref.watch(serverText).value;
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
              text == null || text.isEmpty ? credit.text : text,
              key: const Key('compendium-attribution'),
              style: theme.textTheme.bodySmall,
            ),
          ),
        ),
      ),
    );
  }
}
