import 'package:opentrpg_core/core/systems/game_system_ui.dart';
import 'package:opentrpg_core/features/items/data/models.dart';

import '../catalog/domain/catalog_format.dart';
import '../catalog/ui/detail_widgets.dart' show ModifierLines;
import 'dnd5e_item.dart';

String? _damage(ItemDamage? damage) =>
    damage == null ? null : [damage.dice, ?damage.type].join(' ');

String? _range(EffectiveItem i) {
  final normal = i.rangeNormal;
  if (normal == null) return null;
  final long = i.rangeLong;
  return long == null ? '$normal pies' : '$normal/$long pies';
}

String? _armorClass(ItemArmor? armor) {
  final base = armor?.baseAc;
  if (armor == null || base == null) return null;
  if (armor.addDexModifier != true) return '$base';
  final max = armor.maxDexBonus;
  return max == null ? '$base + mod. Des' : '$base + mod. Des (máx. $max)';
}

/// The rows of the detail of an item in D&D 5e: rarity, attunement, weight,
/// damage, range, properties and armor.
List<ItemFact> dnd5eItemFacts(EffectiveItem i) => [
  (label: 'Rareza', value: i.rarity == null ? null : rarityLabel(i.rarity), fields: ['rarity']),
  (
    label: 'Sintonización',
    value: i.requiresAttunement ? 'Requerida' : null,
    fields: ['requiresAttunement'],
  ),
  (
    label: 'Peso',
    value: i.weightLb == null ? null : formatWeightLb(i.weightLb),
    fields: ['weightLb'],
  ),
  (label: 'Daño', value: _damage(i.damage), fields: ['damageDice', 'damageType']),
  (label: 'Versátil', value: i.damage?.versatileDice, fields: ['versatileDice']),
  (label: 'Alcance', value: _range(i), fields: ['rangeNormal', 'rangeLong']),
  (label: 'Propiedades', value: i.properties.join(', '), fields: ['properties']),
  (
    label: 'Clase de armadura',
    value: _armorClass(i.armor),
    fields: ['armorClassBase', 'addDexModifier', 'maxDexBonus'],
  ),
  (
    label: 'Fuerza mínima',
    value: i.armor?.strengthMinimum?.toString(),
    fields: ['strengthMinimum'],
  ),
  (
    label: 'Sigilo',
    value: (i.armor?.stealthDisadvantage ?? false) ? 'Desventaja' : null,
    fields: ['stealthDisadvantage'],
  ),
];

/// The modifiers of an item as readable lines. Servers that predate the
/// structured modifiers only send the attack and damage bonuses, which are
/// shown as modifiers too.
ItemModifiersView dnd5eItemModifiers(EffectiveItem i) {
  final modifiers = [
    ...i.modifiers,
    if (i.attackBonus != 0 && !i.modifiers.any((m) => m.kind == 'AttackBonus'))
      ItemModifier(kind: 'AttackBonus', value: i.attackBonus),
    if (i.damageBonus != 0 && !i.modifiers.any((m) => m.kind == 'DamageBonus'))
      ItemModifier(kind: 'DamageBonus', value: i.damageBonus),
  ];
  return (
    view: modifiers.isEmpty ? null : ModifierLines(modifiers),
    fields: const ['modifiers', 'attackBonus', 'damageBonus'],
  );
}
