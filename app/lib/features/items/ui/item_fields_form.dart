import 'package:flutter/material.dart';

import '../../catalog/domain/catalog_format.dart';
import '../../characters/domain/character_format.dart' show copperToGoldText;
import '../domain/item_form_data.dart';
import '../domain/items_format.dart';

/// Form with every item field. It is used for homebrew templates
/// ([templateMode]) and for the overrides of an inventory or shop item.
///
/// The parent keeps a `GlobalKey<ItemFieldsFormState>` to validate the form
/// and read its [ItemFormData].
class ItemFieldsForm extends StatefulWidget {
  const ItemFieldsForm({super.key, required this.initial, this.templateMode = false});

  final ItemFormData initial;

  /// Shows the template-only fields (subcategory, cost) and hides the bonuses
  /// that only exist as overrides.
  final bool templateMode;

  @override
  State<ItemFieldsForm> createState() => ItemFieldsFormState();
}

class ItemFieldsFormState extends State<ItemFieldsForm> {
  final _formKey = GlobalKey<FormState>();

  late final TextEditingController _name;
  late final TextEditingController _subcategory;
  late final TextEditingController _cost;
  late final TextEditingController _weight;
  late final TextEditingController _damageDice;
  late final TextEditingController _damageType;
  late final TextEditingController _versatile;
  late final TextEditingController _properties;
  late final TextEditingController _rangeNormal;
  late final TextEditingController _rangeLong;
  late final TextEditingController _armorClass;
  late final TextEditingController _maxDex;
  late final TextEditingController _strength;
  late final TextEditingController _attackBonus;
  late final TextEditingController _damageBonus;
  late final TextEditingController _description;
  late final TextEditingController _effects;

  late String _category;
  String? _rarity;
  late bool _attunement;
  late bool _addDex;
  late bool _stealth;

  @override
  void initState() {
    super.initState();
    final d = widget.initial;
    String text(num? value) => value == null ? '' : value.toString();
    _name = TextEditingController(text: d.name);
    _subcategory = TextEditingController(text: d.subcategory);
    _cost = TextEditingController(text: d.costCp == null ? '' : copperToGoldText(d.costCp!));
    _weight = TextEditingController(text: d.weightLb == null ? '' : formatPlainNumber(d.weightLb!));
    _damageDice = TextEditingController(text: d.damageDice);
    _damageType = TextEditingController(text: d.damageType);
    _versatile = TextEditingController(text: d.versatileDice);
    _properties = TextEditingController(text: d.properties.join(', '));
    _rangeNormal = TextEditingController(text: text(d.rangeNormal));
    _rangeLong = TextEditingController(text: text(d.rangeLong));
    _armorClass = TextEditingController(text: text(d.armorClassBase));
    _maxDex = TextEditingController(text: text(d.maxDexBonus));
    _strength = TextEditingController(text: text(d.strengthMinimum));
    _attackBonus = TextEditingController(text: d.attackBonus == 0 ? '' : '${d.attackBonus}');
    _damageBonus = TextEditingController(text: d.damageBonus == 0 ? '' : '${d.damageBonus}');
    _description = TextEditingController(text: d.description.join('\n'));
    _effects = TextEditingController(text: d.effects.join('\n'));
    _category = itemCategories.containsKey(d.category) ? d.category : 'Other';
    _rarity = itemRarities.containsKey(d.rarity) ? d.rarity : null;
    _attunement = d.requiresAttunement;
    _addDex = d.addDexModifier;
    _stealth = d.stealthDisadvantage;
  }

  @override
  void dispose() {
    for (final c in [
      _name,
      _subcategory,
      _cost,
      _weight,
      _damageDice,
      _damageType,
      _versatile,
      _properties,
      _rangeNormal,
      _rangeLong,
      _armorClass,
      _maxDex,
      _strength,
      _attackBonus,
      _damageBonus,
      _description,
      _effects,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  /// True when every field is valid (and shows the errors otherwise).
  bool validate() => _formKey.currentState?.validate() ?? false;

  /// Current values of the form.
  ItemFormData read() {
    int? integer(TextEditingController c) => int.tryParse(c.text.trim());
    return ItemFormData(
      name: _name.text.trim(),
      category: _category,
      subcategory: widget.templateMode ? _subcategory.text.trim() : '',
      rarity: _rarity,
      requiresAttunement: _attunement,
      costCp: widget.templateMode ? parseGoldToCp(_cost.text) : null,
      weightLb: double.tryParse(_weight.text.trim().replaceAll(',', '.')),
      damageDice: _damageDice.text.trim(),
      damageType: _damageType.text.trim(),
      versatileDice: _versatile.text.trim(),
      properties: splitCommas(_properties.text),
      rangeNormal: integer(_rangeNormal),
      rangeLong: integer(_rangeLong),
      armorClassBase: integer(_armorClass),
      addDexModifier: _addDex,
      maxDexBonus: integer(_maxDex),
      strengthMinimum: integer(_strength),
      stealthDisadvantage: _stealth,
      description: splitLines(_description.text),
      effects: splitLines(_effects.text),
      attackBonus: widget.templateMode ? 0 : (integer(_attackBonus) ?? 0),
      damageBonus: widget.templateMode ? 0 : (integer(_damageBonus) ?? 0),
    );
  }

  String? _optionalInt(String? value, {int min = 0}) {
    final text = value?.trim() ?? '';
    if (text.isEmpty) return null;
    final parsed = int.tryParse(text);
    return parsed == null || parsed < min ? 'Número no válido' : null;
  }

  String? _signedInt(String? value) {
    final text = value?.trim() ?? '';
    if (text.isEmpty) return null;
    return int.tryParse(text) == null ? 'Número no válido' : null;
  }

  String? _optionalDecimal(String? value) {
    final text = value?.trim() ?? '';
    if (text.isEmpty) return null;
    final parsed = double.tryParse(text.replaceAll(',', '.'));
    return parsed == null || parsed < 0 ? 'Número no válido' : null;
  }

  Widget _field(
    String key,
    TextEditingController controller,
    String label, {
    String? Function(String?)? validator,
    TextInputType? keyboardType,
    int? maxLines = 1,
    String? hint,
  }) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: TextFormField(
      key: Key(key),
      controller: controller,
      keyboardType: keyboardType,
      maxLines: maxLines,
      minLines: maxLines == 1 ? 1 : 2,
      validator: validator,
      decoration: InputDecoration(
        labelText: label,
        hintText: hint,
        border: const OutlineInputBorder(),
      ),
    ),
  );

  Widget _number(String key, TextEditingController c, String label, {bool signed = false}) =>
      Expanded(
        child: Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: TextFormField(
            key: Key(key),
            controller: c,
            keyboardType: TextInputType.numberWithOptions(signed: signed),
            validator: signed ? _signedInt : _optionalInt,
            decoration: InputDecoration(labelText: label, border: const OutlineInputBorder()),
          ),
        ),
      );

  @override
  Widget build(BuildContext context) {
    const decimal = TextInputType.numberWithOptions(decimal: true);
    final theme = Theme.of(context);
    return Form(
      key: _formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _field(
            'item-form-name',
            _name,
            'Nombre',
            validator: (v) => (v ?? '').trim().isEmpty ? 'Escribe un nombre' : null,
          ),
          Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: DropdownButtonFormField<String>(
              key: const Key('item-form-category'),
              initialValue: _category,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'Categoría',
                border: OutlineInputBorder(),
              ),
              items: [
                for (final e in itemCategories.entries)
                  DropdownMenuItem(value: e.key, child: Text(e.value)),
              ],
              onChanged: (value) => setState(() => _category = value ?? _category),
            ),
          ),
          if (widget.templateMode)
            _field('item-form-subcategory', _subcategory, 'Subcategoría (opcional)'),
          Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: DropdownButtonFormField<String?>(
              key: const Key('item-form-rarity'),
              initialValue: _rarity,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Rareza', border: OutlineInputBorder()),
              items: [
                const DropdownMenuItem<String?>(value: null, child: Text('Sin rareza')),
                for (final e in itemRarities.entries)
                  DropdownMenuItem<String?>(value: e.key, child: Text(e.value)),
              ],
              onChanged: (value) => setState(() => _rarity = value),
            ),
          ),
          SwitchListTile(
            key: const Key('item-form-attunement'),
            contentPadding: EdgeInsets.zero,
            title: const Text('Requiere sintonización'),
            value: _attunement,
            onChanged: (value) => setState(() => _attunement = value),
          ),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: _field(
                  'item-form-weight',
                  _weight,
                  'Peso (lb)',
                  keyboardType: decimal,
                  validator: _optionalDecimal,
                ),
              ),
              if (widget.templateMode) ...[
                const SizedBox(width: 12),
                Expanded(
                  child: _field(
                    'item-form-cost',
                    _cost,
                    'Coste (gp)',
                    keyboardType: decimal,
                    validator: _optionalDecimal,
                  ),
                ),
              ],
            ],
          ),
          Text('Combate', style: theme.textTheme.titleSmall),
          const SizedBox(height: 8),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(child: _field('item-form-damage-dice', _damageDice, 'Daño', hint: '1d8')),
              const SizedBox(width: 12),
              Expanded(
                child: _field('item-form-damage-type', _damageType, 'Tipo', hint: 'Slashing'),
              ),
            ],
          ),
          _field('item-form-versatile', _versatile, 'Daño versátil', hint: '1d10'),
          _field(
            'item-form-properties',
            _properties,
            'Propiedades (separadas por comas)',
            hint: 'Finesse, Light',
          ),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _number('item-form-range-normal', _rangeNormal, 'Alcance (pies)'),
              const SizedBox(width: 12),
              _number('item-form-range-long', _rangeLong, 'Alcance largo'),
            ],
          ),
          if (!widget.templateMode)
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _number('item-form-attack-bonus', _attackBonus, 'Bono de ataque', signed: true),
                const SizedBox(width: 12),
                _number('item-form-damage-bonus', _damageBonus, 'Bono de daño', signed: true),
              ],
            ),
          Text('Armadura', style: theme.textTheme.titleSmall),
          const SizedBox(height: 8),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _number('item-form-ac', _armorClass, 'CA base'),
              const SizedBox(width: 12),
              _number('item-form-max-dex', _maxDex, 'Máx. Des'),
              const SizedBox(width: 12),
              _number('item-form-strength', _strength, 'Fuerza mín.'),
            ],
          ),
          SwitchListTile(
            key: const Key('item-form-add-dex'),
            contentPadding: EdgeInsets.zero,
            title: const Text('Suma el modificador de Destreza'),
            value: _addDex,
            onChanged: (value) => setState(() => _addDex = value),
          ),
          SwitchListTile(
            key: const Key('item-form-stealth'),
            contentPadding: EdgeInsets.zero,
            title: const Text('Desventaja en Sigilo'),
            value: _stealth,
            onChanged: (value) => setState(() => _stealth = value),
          ),
          const SizedBox(height: 8),
          Text('Texto', style: theme.textTheme.titleSmall),
          const SizedBox(height: 8),
          _field(
            'item-form-effects',
            _effects,
            'Efectos (uno por línea)',
            maxLines: 4,
            hint: '+1 a ataque y daño',
          ),
          _field(
            'item-form-description',
            _description,
            'Descripción (un párrafo por línea)',
            maxLines: 6,
          ),
        ],
      ),
    );
  }
}
