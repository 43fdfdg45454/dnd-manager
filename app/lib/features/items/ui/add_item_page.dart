import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../catalog/data/models.dart' show ItemSummary;
import '../data/items_controllers.dart';
import '../data/models.dart';
import 'item_composer.dart';
import 'item_feedback.dart';
import 'item_search_list.dart';

/// Adds an item to a character: "Rápido" (a catalog item and a quantity) or
/// "Avanzado" (template plus overrides, or from scratch). Pops with true when
/// the request went through, whether applied or sent to the DM.
class AddItemPage extends ConsumerStatefulWidget {
  const AddItemPage({super.key, required this.characterId, required this.campaignId});

  final String characterId;
  final String campaignId;

  @override
  ConsumerState<AddItemPage> createState() => _AddItemPageState();
}

class _AddItemPageState extends ConsumerState<AddItemPage> {
  ItemSummary? _selected;
  final _quantity = TextEditingController(text: '1');
  bool _busy = false;

  @override
  void dispose() {
    _quantity.dispose();
    super.dispose();
  }

  InventoryController get _inventory =>
      ref.read(inventoryControllerProvider(widget.characterId).notifier);

  Future<bool> _add({String? templateId, required int quantity, ComposedItem? composed}) async {
    final done = await runWrite(
      context,
      () => _inventory.add(
        templateId: templateId,
        quantity: quantity,
        overrides: composed?.overrides ?? const ItemOverrides(),
      ),
      applied: 'Objeto añadido al inventario.',
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
          title: const Text('Añadir objeto'),
          bottom: const TabBar(
            tabs: [
              Tab(key: Key('tab-add-quick'), text: 'Rápido'),
              Tab(key: Key('tab-add-advanced'), text: 'Avanzado'),
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
                            key: const Key('quick-quantity'),
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
                            key: const Key('quick-add'),
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
              submitLabel: 'Añadir',
              onSubmit: (item) =>
                  _add(templateId: item.templateId, quantity: item.quantity, composed: item),
            ),
          ],
        ),
      ),
    );
  }
}
