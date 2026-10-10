import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../data/models.dart';
import 'item_feedback.dart';
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
/// preload the item form of the game system ([GameSystemUi.itemFormSection]),
/// or a from-scratch item. Only the fields that differ from the template are
/// sent as overrides.
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
  var _formKey = GlobalKey();
  final _quantity = TextEditingController(text: '1');
  final _price = TextEditingController();
  final _stock = TextEditingController();
  ItemSummary? _template;
  bool _busy = false;

  GameSystemUi get _system => ref.read(campaignSystemUiProvider(widget.campaignId));

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
      _formKey = GlobalKey();
      // Shops start from the list price of the template.
      if (widget.mode == ComposerMode.shop && picked.costCp != null) {
        _price.text = _system.moneyInputText(picked.costCp!);
      }
    });
  }

  void _clear() => setState(() {
    _template = null;
    _formKey = GlobalKey();
  });

  Future<void> _submit() async {
    final extrasValid = _extrasKey.currentState?.validate() ?? false;
    final form = _formKey.currentState;
    if (form is! ItemFormReader || !(form as ItemFormReader).validate() || !extrasValid) return;
    final system = _system;
    final template = _template;
    final base = template == null ? null : ref.read(system.itemTemplate(template.id)).requireValue;
    final stockText = _stock.text.trim();
    final item = ComposedItem(
      templateId: template?.id,
      overrides: (form as ItemFormReader).readOverrides(template: base),
      quantity: int.tryParse(_quantity.text.trim()) ?? 1,
      priceCp: system.parseMoney(_price.text) ?? 0,
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
    final system = ref.watch(campaignSystemUiProvider(widget.campaignId));
    final templateProvider = template == null ? null : system.itemTemplate(template.id);
    final detail = templateProvider == null ? null : ref.watch(templateProvider);

    Widget form;
    if (templateProvider == null) {
      form = system.itemFormSection(ItemFormScope(formKey: _formKey)) ?? const SizedBox.shrink();
    } else {
      form = detail!.when(
        loading: () => const Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: CircularProgressIndicator()),
        ),
        error: (error, _) =>
            ItemsErrorView(error: error, onRetry: () => ref.invalidate(templateProvider)),
        data: (d) =>
            system.itemFormSection(ItemFormScope(formKey: _formKey, template: d)) ??
            const SizedBox.shrink(),
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
                            system.parseMoney(v ?? '') == null ? 'Precio no válido' : null,
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
