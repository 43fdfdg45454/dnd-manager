import '../data/models.dart';

/// Modifier kinds that change attack or damage rolls.
const _attackKinds = {'attackbonus', 'damagebonus'};

/// Whether [item] matters during a fight: weapons, shields, armor, consumables,
/// items with charges ([hasCharges]) and items that add to attack or damage.
bool isCombatUsable(EffectiveItem item, {bool hasCharges = false}) {
  const combatCategories = {'weapon', 'shield', 'armor', 'consumable'};
  return combatCategories.contains(item.category.toLowerCase()) ||
      hasCharges ||
      item.damage != null ||
      item.armor != null ||
      item.attackBonus != 0 ||
      item.damageBonus != 0 ||
      item.modifiers.any((m) => _attackKinds.contains(m.kind.toLowerCase()) && m.value != 0);
}

/// [isCombatUsable] for an inventory item (its charges count).
bool isCombatUsableItem(CharacterItem item) =>
    isCombatUsable(item.effective, hasCharges: item.charges != null);
