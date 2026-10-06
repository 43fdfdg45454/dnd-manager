import 'dart:async';

import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../catalog/data/models.dart' show ItemSummary;
import '../../catalog/domain/catalog_format.dart';
import '../data/campaign_items_repository.dart';
import '../data/items_controllers.dart';
import 'item_feedback.dart';

const _searchDebounce = Duration(milliseconds: 350);

/// Search box, source filter (all / SRD / campaign) and the matching items of
/// the campaign catalog. [onSelected] fires when a row is tapped.
class ItemSearchList extends ConsumerStatefulWidget {
  const ItemSearchList({
    super.key,
    required this.campaignId,
    required this.onSelected,
    this.selectedId,
    this.initialSource = ItemSource.all,
    this.showSourceFilter = true,
    this.trailingBuilder,
  });

  final String campaignId;
  final ValueChanged<ItemSummary> onSelected;
  final String? selectedId;
  final ItemSource initialSource;
  final bool showSourceFilter;

  /// Optional widget at the end of each row (for example a menu).
  final Widget? Function(BuildContext context, ItemSummary item)? trailingBuilder;

  @override
  ConsumerState<ItemSearchList> createState() => _ItemSearchListState();
}

class _ItemSearchListState extends ConsumerState<ItemSearchList> {
  Timer? _debounce;
  String _search = '';
  late ItemSource _source = widget.initialSource;

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  void _onChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(_searchDebounce, () {
      if (mounted) setState(() => _search = value.trim());
    });
  }

  @override
  Widget build(BuildContext context) {
    final key = (campaignId: widget.campaignId, source: _source, search: _search);
    final items = ref.watch(campaignItemsControllerProvider(key));
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
          child: TextField(
            key: const Key('item-search'),
            onChanged: _onChanged,
            textInputAction: TextInputAction.search,
            decoration: const InputDecoration(
              prefixIcon: Icon(Icons.search),
              hintText: 'Buscar objeto',
              isDense: true,
              border: OutlineInputBorder(),
            ),
          ),
        ),
        if (widget.showSourceFilter)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 4, 16, 4),
            child: SegmentedButton<ItemSource>(
              key: const Key('item-source'),
              showSelectedIcon: false,
              segments: [
                for (final s in ItemSource.values) ButtonSegment(value: s, label: Text(s.label)),
              ],
              selected: {_source},
              onSelectionChanged: (value) => setState(() => _source = value.first),
            ),
          ),
        Expanded(
          child: items.when(
            skipLoadingOnReload: true,
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => ItemsErrorView(
              error: error,
              onRetry: () => ref.invalidate(campaignItemsControllerProvider(key)),
            ),
            data: (page) => page.items.isEmpty
                ? const Center(child: Text('No hay objetos que coincidan.'))
                : ListView.builder(
                    key: const Key('item-results'),
                    itemCount: page.items.length + (page.hasMore ? 1 : 0),
                    itemBuilder: (context, index) {
                      if (index >= page.items.length) {
                        return Padding(
                          padding: const EdgeInsets.all(8),
                          child: Center(
                            child: TextButton(
                              key: const Key('item-load-more'),
                              onPressed: () => ref
                                  .read(campaignItemsControllerProvider(key).notifier)
                                  .loadMore(),
                              child: const Text('Cargar más'),
                            ),
                          ),
                        );
                      }
                      final item = page.items[index];
                      return ListTile(
                        key: Key('item-${item.id}'),
                        selected: item.id == widget.selectedId,
                        title: Text(item.name),
                        subtitle: Text(
                          [
                            itemCategoryLabel(item.category),
                            if (item.rarity != null) rarityLabel(item.rarity),
                            if (item.costCp != null) formatCostCp(item.costCp),
                          ].where((e) => e.isNotEmpty).join(' · '),
                        ),
                        trailing: item.id == widget.selectedId
                            ? const Icon(Icons.check_circle)
                            : widget.trailingBuilder?.call(context, item),
                        onTap: () => widget.onSelected(item),
                      );
                    },
                  ),
          ),
        ),
      ],
    );
  }
}

/// Full-screen picker of a template; pops with the chosen [ItemSummary].
class TemplatePickerPage extends StatelessWidget {
  const TemplatePickerPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Elegir plantilla')),
      body: ItemSearchList(
        campaignId: campaignId,
        onSelected: (item) => Navigator.of(context).pop(item),
      ),
    );
  }
}
