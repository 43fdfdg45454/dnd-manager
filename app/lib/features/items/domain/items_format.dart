import '../data/models.dart';

/// Coins shown for money, largest first (electrum is left out on purpose).
const _moneyCoins = <(String, int)>[('pp', 1000), ('gp', 100), ('sp', 10), ('cp', 1)];

/// Money in copper as "1 pp 5 gp 5 sp"; "0 cp" for zero and a leading "-" for
/// negative amounts.
String formatMoney(int copper) {
  if (copper == 0) return '0 cp';
  var rest = copper.abs();
  final parts = <String>[];
  for (final (name, value) in _moneyCoins) {
    final amount = rest ~/ value;
    if (amount > 0) parts.add('$amount $name');
    rest -= amount * value;
  }
  return '${copper < 0 ? '-' : ''}${parts.join(' ')}';
}

/// Parses a gold amount ("15", "15.5", "15,5", "-2") into copper; null when it
/// is not a number, is not finite or (unless [allowNegative]) is negative.
int? parseGoldToCp(String text, {bool allowNegative = false}) {
  final value = double.tryParse(text.trim().replaceAll(',', '.'));
  if (value == null || !value.isFinite) return null;
  if (value < 0 && !allowNegative) return null;
  return (value * 100).round();
}

/// What a shop pays for [quantity] units worth [unitCp] each at
/// [buybackPercent] percent (rounded down).
int sellPayoutCp({required int unitCp, required int quantity, required int buybackPercent}) =>
    unitCp * quantity * buybackPercent ~/ 100;

/// "38.5" or "40" (no trailing zeros).
String formatPlainNumber(double value) =>
    value == value.roundToDouble() ? value.toInt().toString() : value.toString();

/// Rarities as the API spells them, with their Spanish label.
const itemRarities = <String, String>{
  'Common': 'Común',
  'Uncommon': 'Poco común',
  'Rare': 'Rara',
  'VeryRare': 'Muy rara',
  'Legendary': 'Legendaria',
  'Artifact': 'Artefacto',
  'Varies': 'Variable',
};

/// Splits a multi-line text field into trimmed, non-empty entries.
List<String> splitLines(String text) => [
  for (final line in text.split('\n'))
    if (line.trim().isNotEmpty) line.trim(),
];

/// Splits a comma separated field into trimmed, non-empty entries.
List<String> splitCommas(String text) => [
  for (final part in text.split(','))
    if (part.trim().isNotEmpty) part.trim(),
];

/// Whether the equip action makes sense for [item] (the server has the last
/// word and answers 400 otherwise): weapons, armor, shields and magic items,
/// plus any other non-consumable item that does something while worn
/// (modifiers, effects, armor, damage or attunement), such as custom items
/// without an equipment category.
bool canEquip(EffectiveItem item) {
  const equippable = {'weapon', 'armor', 'shield', 'magicitem'};
  if (equippable.contains(item.category.toLowerCase())) return true;
  if (item.isConsumable) return false;
  return item.modifiers.isNotEmpty ||
      item.effects.isNotEmpty ||
      item.armor != null ||
      item.damage != null ||
      item.requiresAttunement;
}

/// Whether the item can be spent: consumables and anything with charges.
bool canUse(CharacterItem item) => item.effective.isConsumable || item.charges != null;

/// Inventory sections in display order.
enum InventoryGroup {
  equipped('Equipado'),
  backpack('Mochila'),
  consumables('Consumibles');

  const InventoryGroup(this.label);

  final String label;
}

InventoryGroup groupOf(CharacterItem item) {
  if (item.equipped) return InventoryGroup.equipped;
  if (item.effective.isConsumable) return InventoryGroup.consumables;
  return InventoryGroup.backpack;
}

/// Unit value used to estimate what a shop pays: the price the server sent for
/// the item, else the template cost, else the price the shop itself asks.
int? sellUnitCp({int? effectiveCostCp, int? templateCostCp, int? shopPriceCp}) =>
    effectiveCostCp ?? templateCostCp ?? shopPriceCp;
