import '../data/models.dart';

/// What a shop pays for [quantity] units worth [unitCp] each at
/// [buybackPercent] percent (rounded down).
int sellPayoutCp({required int unitCp, required int quantity, required int buybackPercent}) =>
    unitCp * quantity * buybackPercent ~/ 100;

/// "38.5" or "40" (no trailing zeros).
String formatPlainNumber(double value) =>
    value == value.roundToDouble() ? value.toInt().toString() : value.toString();

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

/// "3" -> "3", "0.5" -> "0.5", "2.0" -> "2".
String formatWeightLb(double? weight) {
  if (weight == null) return '—';
  final text = weight == weight.roundToDouble() ? weight.toInt().toString() : weight.toString();
  return '$text lb';
}

String _normalizeCategory(String value) => value.toLowerCase().replaceAll(RegExp(r'[^a-z]'), '');

/// Item categories as the API spells them, with their Spanish plural (filter
/// menu).
const itemCategories = <String, String>{
  'Weapon': 'Armas',
  'Armor': 'Armaduras',
  'Shield': 'Escudos',
  'AdventuringGear': 'Equipo de aventura',
  'Tool': 'Herramientas',
  'Mount': 'Monturas y vehículos',
  'Consumable': 'Consumibles',
  'MagicItem': 'Objetos mágicos',
  'Other': 'Otros',
};

const _itemCategoryNames = <String, String>{
  'weapon': 'Arma',
  'armor': 'Armadura',
  'shield': 'Escudo',
  'adventuringgear': 'Equipo de aventura',
  'tool': 'Herramienta',
  'mount': 'Montura o vehículo',
  'consumable': 'Consumible',
  'magicitem': 'Objeto mágico',
  'other': 'Otro',
};

/// Spanish singular name of an item category, for lists and detail pages.
String itemCategoryLabel(String? category) {
  if (category == null) return '';
  return _itemCategoryNames[_normalizeCategory(category)] ?? category;
}
