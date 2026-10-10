import 'package:flutter/material.dart';

import '../../catalog/data/models.dart' show ItemModifier, itemModifierKinds;
import '../../catalog/domain/catalog_format.dart';
import '../../catalog/domain/item_modifier_format.dart';
import '../../characters/data/models.dart' show abilityKeys;
import '../../characters/domain/character_format.dart' show copperToGoldText, skillLabel;
import '../domain/item_form_data.dart';
import '../domain/items_format.dart';
import '../../../systems/dnd5e/items/dnd5e_item.dart';

/// Form with every item field. It is used for homebrew templates
/// ([templateMode]) and for the overrides of an inventory or shop item.
///
/// The parent keeps a `GlobalKey<ItemFieldsFormState>` to validate the form
/// and read its [ItemFormData].
class ItemFieldsForm extends StatefulWidget {
  const ItemFieldsForm({super.key, required this.initial, this.templateMode = false});

  final ItemFormData initial;

  /// Shows the template-only fields (subcategory, cost).
  final bool templateMode;

  @override
  State<ItemFieldsForm> createState() => ItemFieldsFormState();
}

/// Maximum number of modifiers per item (server limit).
const maxItemModifiers = 10;

/// Editable state of one modifier row.
class _ModifierRow {
  _ModifierRow(this.kind, this.target, String value) : value = TextEditingController(text: value);

  String kind;
  String? target;
  final TextEditingController value;

  void dispose() => value.dispose();
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
    _description = TextEditingController(text: d.description.join('\n'));
    _effects = TextEditingController(text: d.effects.join('\n'));
    _category = itemCategories.containsKey(d.category) ? d.category : 'Other';
    _rarity = itemRarities.containsKey(d.rarity) ? d.rarity : null;
    _attunement = d.requiresAttunement;
    _addDex = d.addDexModifier;
    _stealth = d.stealthDisadvantage;
    for (final m in d.modifiers) {
      _modifiers.add(_ModifierRow(m.kind, m.target, '${m.value}'));
    }
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
      _description,
      _effects,
    ]) {
      c.dispose();
    }
    for (final row in _modifiers) {
      row.dispose();
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
      modifiers: [
        for (final row in _modifiers)
          ItemModifier(
            kind: row.kind,
            target: row.target,
            value: int.tryParse(row.value.text.trim()) ?? 0,
          ),
      ],
    );
  }

  final List<_ModifierRow> _modifiers = [];

  void _addModifier() =>
      setState(() => _modifiers.add(_ModifierRow('AbilityBonus', abilityKeys.first, '1')));

  void _removeModifier(int index) => setState(() => _modifiers.removeAt(index).dispose());

  void _setModifierKind(_ModifierRow row, String kind) => setState(() {
    row.kind = kind;
    // Keep the target only while it still makes sense for the new kind.
    if (modifierTargetsAbility(kind)) {
      row.target = abilityKeys.contains(row.target) ? row.target : abilityKeys.first;
    } else if (modifierTargetsSave(kind)) {
      row.target = abilityKeys.contains(row.target) ? row.target : null;
    } else if (modifierTargetsSkill(kind)) {
      row.target = modifierSkillIndexes.contains(row.target) ? row.target : null;
    } else {
      row.target = null;
    }
  });

  String? _modifierValue(String kind, String? value) {
    final parsed = int.tryParse(value?.trim() ?? '');
    final min = kind == 'AbilitySet' ? 1 : -10;
    if (parsed == null || parsed < min || parsed > 30) return 'Entre $min y 30';
    return null;
  }

  Widget _modifierRow(BuildContext context, int index, _ModifierRow row) {
    final targetItems = <DropdownMenuItem<String?>>[
      if (!modifierTargetsAbility(row.kind))
        DropdownMenuItem<String?>(
          value: null,
          child: Text(modifierTargetsSkill(row.kind) ? 'Todas las habilidades' : 'Todas'),
        ),
      if (modifierTargetsSkill(row.kind))
        for (final skill in modifierSkillIndexes)
          DropdownMenuItem<String?>(value: skill, child: Text(skillLabel(skill)))
      else
        for (final ability in abilityKeys)
          DropdownMenuItem<String?>(value: ability, child: Text(abilityLabel(ability))),
    ];
    return Card(
      key: ObjectKey(row),
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 12, 4, 4),
        child: Column(
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: DropdownButtonFormField<String>(
                      key: Key('modifier-kind-$index'),
                      initialValue: row.kind,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'Tipo',
                        border: OutlineInputBorder(),
                      ),
                      items: [
                        for (final kind in itemModifierKinds)
                          DropdownMenuItem(value: kind, child: Text(itemModifierKindLabel(kind))),
                      ],
                      onChanged: (value) {
                        if (value != null) _setModifierKind(row, value);
                      },
                    ),
                  ),
                ),
                IconButton(
                  key: Key('modifier-remove-$index'),
                  tooltip: 'Quitar modificador',
                  onPressed: () => _removeModifier(index),
                  icon: const Icon(Icons.delete_outline),
                ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (modifierHasTarget(row.kind)) ...[
                    Expanded(
                      flex: 3,
                      child: Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        // Rebuilt when the kind changes: the dropdown keeps its own value.
                        child: KeyedSubtree(
                          key: ValueKey('${row.kind}-target'),
                          child: DropdownButtonFormField<String?>(
                            key: Key('modifier-target-$index'),
                            initialValue: row.target,
                            isExpanded: true,
                            decoration: InputDecoration(
                              labelText: modifierTargetsSkill(row.kind)
                                  ? 'Habilidad'
                                  : modifierTargetsSave(row.kind)
                                  ? 'Salvación'
                                  : 'Característica',
                              border: const OutlineInputBorder(),
                            ),
                            items: targetItems,
                            onChanged: (value) => setState(() => row.target = value),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                  ],
                  Expanded(
                    flex: 2,
                    child: Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: TextFormField(
                        key: Key('modifier-value-$index'),
                        onChanged: (_) => setState(() {}),
                        controller: row.value,
                        keyboardType: const TextInputType.numberWithOptions(signed: true),
                        validator: (v) => _modifierValue(row.kind, v),
                        decoration: InputDecoration(
                          labelText: row.kind == 'AbilitySet' ? 'Puntuación' : 'Valor',
                          border: const OutlineInputBorder(),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            Align(
              alignment: Alignment.centerLeft,
              child: Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Text(
                  describeItemModifier(
                    ItemModifier(
                      kind: row.kind,
                      target: row.target,
                      value: int.tryParse(row.value.text.trim()) ?? 0,
                    ),
                  ),
                  key: Key('modifier-summary-$index'),
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
            ),
          ],
        ),
      ),
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
          Text('Modificadores', style: theme.textTheme.titleSmall),
          const SizedBox(height: 4),
          Text(
            'Se aplican al personaje mientras el objeto está equipado '
            '(y sintonizado, si lo requiere).',
            style: theme.textTheme.bodySmall,
          ),
          const SizedBox(height: 8),
          for (var i = 0; i < _modifiers.length; i++) _modifierRow(context, i, _modifiers[i]),
          Align(
            alignment: Alignment.centerLeft,
            child: OutlinedButton.icon(
              key: const Key('modifier-add'),
              onPressed: _modifiers.length >= maxItemModifiers ? null : _addModifier,
              icon: const Icon(Icons.add),
              label: const Text('Añadir modificador'),
            ),
          ),
          const SizedBox(height: 16),
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
