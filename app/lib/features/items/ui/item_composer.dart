import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../catalog/data/catalog_controllers.dart';
import '../../catalog/data/models.dart' show ItemSummary;
import '../data/models.dart';
import '../domain/item_form_data.dart';
import '../domain/items_format.dart';
import 'item_feedback.dart';
import 'item_fields_form.dart';
import 'item_search_list.dart';

/// Where the composed item goes, which decides the extra fields.
enum ComposerMode {
  /// A character's inventory: asks for a quantity.
  inventory,

  /// A shop: asks for a price in gp and an optional stock.
  shop,
}

/// What the composer collected.
class ComposedItem {
  const ComposedItem({
    this.templateId,
    required this.overrides,
    this.quantity = 1,
    this.priceCp = 0,
    this.stock,
  });

  final String? templateId;
  final ItemOverrides overrides;
  final int quantity;
  final int priceCp;

  /// Null means unlimited.
  final int? stock;
}

/// The "advanced" way to add an item: an optional template whose values
/// preload the form, or a from-scratch item. Only the fields that differ from
/// the template are sent as overrides.
class ItemComposer extends ConsumerStatefulWidget {
  const ItemComposer({
    super.key,
    required this.campaignId,
    required this.mode,
    required this.submitLabel,
    required this.onSubmit,
  });

  final String campaignId;
  final ComposerMode mode;
  final String submitLabel;

  /// Returns true when the item was saved.
  final Future<bool> Function(ComposedItem item) onSubmit;

  @override
  ConsumerState<ItemComposer> createState() => _ItemComposerState();
}

class _ItemComposerState extends ConsumerState<ItemComposer> {
  final _extrasKey = GlobalKey<FormState>();
  var _formKey = GlobalKey<ItemFieldsFormState>();
  final _quantity = TextEditingController(text: '1');
  final _price = TextEditingController();
  final _stock = TextEditingController();
  ItemSummary? _template;
  bool _busy = false;

  @override
  void dispose() {
    _quantity.dispose();
    _price.dispose();
    _stock.dispose();
    super.dispose();
  }

  Future<void> _pick() async {
    final picked = await Navigator.of(context).push<ItemSummary>(
      MaterialPageRoute(builder: (_) => TemplatePickerPage(campaignId: widget.campaignId)),
    );
    if (picked == null || !mounted) return;
    setState(() {
      _template = picked;
      _formKey = GlobalKey<ItemFieldsFormState>();
    });
  }

  void _clear() => setState(() {
    _template = null;
    _formKey = GlobalKey<ItemFieldsFormState>();
  });

  Future<void> _submit() async {
    final extrasValid = _extrasKey.currentState?.validate() ?? false;
    final form = _formKey.currentState;
    if (form == null || !form.validate() || !extrasValid) return;
    final data = form.read();
    final template = _template;
    final ItemFormData? base = template == null
        ? null
        : ItemFormData.fromDetail(ref.read(itemDetailProvider(template.id)).requireValue);
    final stockText = _stock.text.trim();
    final item = ComposedItem(
      templateId: template?.id,
      overrides: data.toOverrides(base: base),
      quantity: int.tryParse(_quantity.text.trim()) ?? 1,
      priceCp: parseGoldToCp(_price.text) ?? 0,
      stock: stockText.isEmpty ? null : int.tryParse(stockText),
    );
    setState(() => _busy = true);
    try {
      await widget.onSubmit(item);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String? _positiveInt(String? value) {
    final parsed = int.tryParse(value?.trim() ?? '');
    return parsed == null || parsed < 1 ? 'Indica una cantidad válida' : null;
  }

  @override
  Widget build(BuildContext context) {
    final template = _template;
    final theme = Theme.of(context);
    final detail = template == null ? null : ref.watch(itemDetailProvider(template.id));

    Widget form;
    if (template == null) {
      form = ItemFieldsForm(key: _formKey, initial: const ItemFormData());
    } else {
      form = detail!.when(
        loading: () => const Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: CircularProgressIndicator()),
        ),
        error: (error, _) => ItemsErrorView(
          error: error,
          onRetry: () => ref.invalidate(itemDetailProvider(template.id)),
        ),
        data: (d) => ItemFieldsForm(key: _formKey, initial: ItemFormData.fromDetail(d)),
      );
    }

    final canSubmit = !_busy && (template == null || (detail?.hasValue ?? false));

    return ListView(
      padding: EdgeInsets.fromLTRB(16, 12, 16, 24 + MediaQuery.paddingOf(context).bottom),
      children: [
        Card(
          child: ListTile(
            key: const Key('composer-template'),
            leading: const Icon(Icons.inventory_2_outlined),
            title: Text(template?.name ?? 'Desde cero'),
            subtitle: Text(
              template == null
                  ? 'Rellena todos los campos o elige una plantilla.'
                  : 'Los campos que cambies se guardan como modificaciones.',
            ),
            trailing: Wrap(
              spacing: 4,
              children: [
                if (template != null)
                  IconButton(
                    key: const Key('composer-clear'),
                    tooltip: 'Quitar plantilla',
                    onPressed: _clear,
                    icon: const Icon(Icons.close),
                  ),
                FilledButton.tonal(
                  key: const Key('composer-pick'),
                  onPressed: _pick,
                  child: const Text('Plantilla'),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        form,
        const SizedBox(height: 8),
        Form(
          key: _extrasKey,
          child: widget.mode == ComposerMode.inventory
              ? TextFormField(
                  key: const Key('composer-quantity'),
                  controller: _quantity,
                  keyboardType: TextInputType.number,
                  validator: _positiveInt,
                  decoration: const InputDecoration(
                    labelText: 'Cantidad',
                    border: OutlineInputBorder(),
                  ),
                )
              : Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: TextFormField(
                        key: const Key('composer-price'),
                        controller: _price,
                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                        validator: (v) =>
                            parseGoldToCp(v ?? '') == null ? 'Precio no válido' : null,
                        decoration: const InputDecoration(
                          labelText: 'Precio (gp)',
                          border: OutlineInputBorder(),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: TextFormField(
                        key: const Key('composer-stock'),
                        controller: _stock,
                        keyboardType: TextInputType.number,
                        validator: (v) {
                          final text = v?.trim() ?? '';
                          if (text.isEmpty) return null;
                          final parsed = int.tryParse(text);
                          return parsed == null || parsed < 0 ? 'Stock no válido' : null;
                        },
                        decoration: const InputDecoration(
                          labelText: 'Stock',
                          helperText: 'Vacío = ilimitado',
                          border: OutlineInputBorder(),
                        ),
                      ),
                    ),
                  ],
                ),
        ),
        const SizedBox(height: 16),
        FilledButton.icon(
          key: const Key('composer-submit'),
          onPressed: canSubmit ? _submit : null,
          icon: _busy
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.check),
          label: Text(widget.submitLabel),
        ),
        const SizedBox(height: 8),
        Text(
          'Los datos de arriba parten de la plantilla elegida; si no eliges ninguna, se crea un objeto personalizado.',
          style: theme.textTheme.bodySmall,
        ),
      ],
    );
  }
}
