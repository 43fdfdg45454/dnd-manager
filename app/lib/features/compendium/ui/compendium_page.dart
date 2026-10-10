import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/stale_data.dart';
import '../../../core/catalog/compendium_search.dart';
import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/ui/offline_widgets.dart';

const searchDebounce = Duration(milliseconds: 300);

/// Compendium of the rules content of the default game system (D&D 5e:
/// spells, items, classes, races, beasts, conditions and tables) behind one
/// search box in the app bar. The tabs come from
/// [GameSystemUi.compendiumTabs]; each one reads [compendiumSearchProvider].
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
    final system = ref.watch(defaultGameSystemUiProvider);
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
            Expanded(
              child: TabBarView(
                children: [
                  for (final tab in tabs) _KeepAlive(child: Builder(builder: tab.builder)),
                ],
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
