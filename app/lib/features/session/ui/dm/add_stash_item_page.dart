import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/data/models.dart' show ItemSummary;
import '../../../items/data/models.dart';
import '../../../items/ui/item_composer.dart';
import '../../../items/ui/item_search_list.dart';
import '../../data/session_controllers.dart';
import '../session_feedback.dart';

/// Adds loot to the party stash: "Catálogo" (an item of the campaign catalog
/// and a quantity) or "Personalizado" (template plus overrides, or from
/// scratch, with the item form). Pops with true when it was added.
class AddStashItemPage extends ConsumerStatefulWidget {
  const AddStashItemPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  ConsumerState<AddStashItemPage> createState() => _AddStashItemPageState();
}

class _AddStashItemPageState extends ConsumerState<AddStashItemPage> {
  ItemSummary? _selected;
  final _quantity = TextEditingController(text: '1');
  bool _busy = false;

  @override
  void dispose() {
    _quantity.dispose();
    super.dispose();
  }

  Future<bool> _add({
    String? templateId,
    required int quantity,
    ItemOverrides overrides = const ItemOverrides(),
  }) async {
    final done = await runTableAction(
      context,
      () => ref
          .read(stashControllerProvider(widget.campaignId).notifier)
          .addItem(templateId: templateId, overrides: overrides, quantity: quantity),
      success: 'Botín añadido.',
    );
    if (done && mounted) Navigator.of(context).pop(true);
    return done;
  }

  Future<void> _quickAdd() async {
    final item = _selected;
    final quantity = int.tryParse(_quantity.text.trim());
    if (item == null || quantity == null || quantity < 1) return;
    setState(() => _busy = true);
    try {
      await _add(templateId: item.id, quantity: quantity);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final quantity = int.tryParse(_quantity.text.trim());
    final canAdd = _selected != null && quantity != null && quantity >= 1 && !_busy;
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Añadir botín'),
          bottom: const TabBar(
            tabs: [
              Tab(key: Key('stash-add-catalog'), text: 'Catálogo'),
              Tab(key: Key('stash-add-custom'), text: 'Personalizado'),
            ],
          ),
        ),
        body: TabBarView(
          children: [
            Column(
              children: [
                Expanded(
                  child: ItemSearchList(
                    campaignId: widget.campaignId,
                    selectedId: _selected?.id,
                    onSelected: (item) => setState(() => _selected = item),
                  ),
                ),
                SafeArea(
                  top: false,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 96,
                          child: TextField(
                            key: const Key('stash-add-quantity'),
                            controller: _quantity,
                            keyboardType: TextInputType.number,
                            onChanged: (_) => setState(() {}),
                            decoration: const InputDecoration(
                              labelText: 'Cantidad',
                              isDense: true,
                              border: OutlineInputBorder(),
                            ),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: FilledButton.icon(
                            key: const Key('stash-add-submit'),
                            onPressed: canAdd ? _quickAdd : null,
                            icon: const Icon(Icons.add),
                            label: Text(
                              _selected == null ? 'Elige un objeto' : 'Añadir ${_selected!.name}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
            ItemComposer(
              campaignId: widget.campaignId,
              mode: ComposerMode.inventory,
              submitLabel: 'Añadir al botín',
              onSubmit: (item) => _add(
                templateId: item.templateId,
                quantity: item.quantity,
                overrides: item.overrides,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
