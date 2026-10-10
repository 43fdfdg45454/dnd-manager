import 'package:opentrpg_core/features/items/data/models.dart';

import '../../catalog/data/models.dart' show ItemDetail, ItemModifier;
import '../dnd5e_item.dart';

bool _sameList<T>(List<T> a, List<T> b) {
  if (a.length != b.length) return false;
  for (var i = 0; i < a.length; i++) {
    if (a[i] != b[i]) return false;
  }
  return true;
}

/// Plain values of the item form. Empty strings and null mean "not set".
///
/// The form edits a template (homebrew, via [toInput]) or the overrides of an
/// inventory or shop item (via [toOverrides]).
class ItemFormData {
  const ItemFormData({
    this.name = '',
    this.category = 'AdventuringGear',
    this.subcategory = '',
    this.rarity,
    this.requiresAttunement = false,
    this.costCp,
    this.weightLb,
    this.damageDice = '',
    this.damageType = '',
    this.versatileDice = '',
    this.properties = const [],
    this.rangeNormal,
    this.rangeLong,
    this.armorClassBase,
    this.addDexModifier = false,
    this.maxDexBonus,
    this.strengthMinimum,
    this.stealthDisadvantage = false,
    this.description = const [],
    this.effects = const [],
    this.modifiers = const [],
  });

  /// Values of a catalog template.
  factory ItemFormData.fromDetail(ItemDetail d) => ItemFormData(
    name: d.name,
    category: d.category ?? 'AdventuringGear',
    subcategory: d.subcategory ?? '',
    rarity: d.rarity,
    requiresAttunement: d.requiresAttunement,
    costCp: d.costCp,
    weightLb: d.weightLb,
    damageDice: d.damage?.dice ?? '',
    damageType: d.damage?.type ?? '',
    versatileDice: d.damage?.versatileDice ?? '',
    properties: d.properties,
    rangeNormal: d.rangeNormal,
    rangeLong: d.rangeLong,
    armorClassBase: d.armor?.baseAc,
    addDexModifier: d.armor?.addDexModifier ?? false,
    maxDexBonus: d.armor?.maxDexBonus,
    strengthMinimum: d.armor?.strengthMinimum,
    stealthDisadvantage: d.armor?.stealthDisadvantage ?? false,
    description: d.description,
    effects: d.effects,
    modifiers: d.modifiers,
  );

  final String name;
  final String category;
  final String subcategory;
  final String? rarity;
  final bool requiresAttunement;
  final int? costCp;
  final double? weightLb;
  final String damageDice;
  final String damageType;
  final String versatileDice;
  final List<String> properties;
  final int? rangeNormal;
  final int? rangeLong;
  final int? armorClassBase;
  final bool addDexModifier;
  final int? maxDexBonus;
  final int? strengthMinimum;
  final bool stealthDisadvantage;
  final List<String> description;
  final List<String> effects;

  /// Structured modifiers (ability, save, AC, attack bonuses...). The loose
  /// attack and damage bonuses of old overrides arrive here as modifiers.
  final List<ItemModifier> modifiers;

  ItemTemplateInput toInput() => ItemTemplateInput(
    name: name.trim(),
    category: category,
    subcategory: subcategory.trim().isEmpty ? null : subcategory.trim(),
    rarity: rarity,
    requiresAttunement: requiresAttunement,
    costCp: costCp,
    weightLb: weightLb,
    damageDice: damageDice.trim().isEmpty ? null : damageDice.trim(),
    damageType: damageType.trim().isEmpty ? null : damageType.trim(),
    versatileDice: versatileDice.trim().isEmpty ? null : versatileDice.trim(),
    properties: properties,
    rangeNormal: rangeNormal,
    rangeLong: rangeLong,
    armorClassBase: armorClassBase,
    addDexModifier: armorClassBase == null ? null : addDexModifier,
    maxDexBonus: maxDexBonus,
    strengthMinimum: strengthMinimum,
    stealthDisadvantage: stealthDisadvantage,
    description: description,
    effects: effects,
    modifiers: modifiers,
  );

  /// The overrides this form represents.
  ///
  /// With a [base] (the chosen template) only the fields that differ from it
  /// are defined; without one every field that carries a value is.
  ItemOverrides toOverrides({ItemFormData? base}) {
    final b = base;

    String? text(String value, String? baseValue) {
      final v = value.trim();
      if (v.isEmpty) return null;
      return b != null && v == (baseValue ?? '').trim() ? null : v;
    }

    T? number<T extends num>(T? value, T? baseValue) {
      if (value == null) return null;
      return b != null && value == baseValue ? null : value;
    }

    bool? flag(bool value, bool baseValue, {bool onlyIfTrue = true}) {
      if (b != null) return value == baseValue ? null : value;
      return !onlyIfTrue || value ? value : null;
    }

    List<String>? list(List<String> value, List<String> baseValue) {
      if (b != null) return _sameList(value, baseValue) ? null : value;
      return value.isEmpty ? null : value;
    }

    return dnd5eItemOverrides(
      name: text(name, b?.name),
      description: list(description, b?.description ?? const []),
      category: b != null && category == b.category ? null : category,
      damageDice: text(damageDice, b?.damageDice),
      damageType: text(damageType, b?.damageType),
      versatileDice: text(versatileDice, b?.versatileDice),
      properties: list(properties, b?.properties ?? const []),
      rangeNormal: number(rangeNormal, b?.rangeNormal),
      rangeLong: number(rangeLong, b?.rangeLong),
      armorClassBase: number(armorClassBase, b?.armorClassBase),
      addDexModifier: armorClassBase == null
          ? null
          : flag(addDexModifier, b?.addDexModifier ?? false, onlyIfTrue: false),
      maxDexBonus: number(maxDexBonus, b?.maxDexBonus),
      strengthMinimum: number(strengthMinimum, b?.strengthMinimum),
      stealthDisadvantage: flag(stealthDisadvantage, b?.stealthDisadvantage ?? false),
      weightLb: number(weightLb, b?.weightLb),
      rarity: rarity == null || (b != null && rarity == b.rarity) ? null : rarity,
      requiresAttunement: flag(requiresAttunement, b?.requiresAttunement ?? false),
      effects: list(effects, b?.effects ?? const []),
      // Null keeps the template's modifiers; an empty list removes them.
      modifiers: b != null
          ? (_sameList(modifiers, b.modifiers) ? null : modifiers)
          : (modifiers.isEmpty ? null : modifiers),
    );
  }
}
