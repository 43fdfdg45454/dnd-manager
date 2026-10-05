/// Coin denominations in copper pieces, largest first.
const _coins = <(String, int)>[('pp', 1000), ('gp', 100), ('ep', 50), ('sp', 10), ('cp', 1)];

/// Formats a price in copper pieces, e.g. 100 -> "1 gp", 50 -> "5 sp",
/// 250 -> "2 gp 5 sp".
///
/// By default only gp/sp/cp are used, which is how SRD prices are quoted
/// (1500 gp stays "1500 gp"); pass [allCoins] to also break down into pp and
/// ep. Returns "—" when [costCp] is null and "0 cp" when it is zero.
String formatCostCp(int? costCp, {bool allCoins = false}) {
  if (costCp == null) return '—';
  if (costCp <= 0) return '0 cp';
  final denominations = allCoins
      ? _coins
      : [
          for (final c in _coins)
            if (c.$1 == 'gp' || c.$1 == 'sp' || c.$1 == 'cp') c,
        ];
  var rest = costCp;
  final parts = <String>[];
  for (final (name, value) in denominations) {
    final amount = rest ~/ value;
    if (amount > 0) parts.add('$amount $name');
    rest -= amount * value;
  }
  return parts.join(' ');
}

/// "3" -> "3", "0.5" -> "0.5", "2.0" -> "2".
String formatWeightLb(double? weight) {
  if (weight == null) return '—';
  final text = weight == weight.roundToDouble() ? weight.toInt().toString() : weight.toString();
  return '$text lb';
}

/// Spanish name of an ability slug ("str", "Strength", ...). Unknown values are
/// returned unchanged.
String abilityLabel(String ability) {
  final key = ability.toLowerCase();
  if (key.startsWith('str')) return 'Fuerza';
  if (key.startsWith('dex')) return 'Destreza';
  if (key.startsWith('con')) return 'Constitución';
  if (key.startsWith('int')) return 'Inteligencia';
  if (key.startsWith('wis')) return 'Sabiduría';
  if (key.startsWith('cha')) return 'Carisma';
  return ability;
}

String _normalize(String value) => value.toLowerCase().replaceAll(RegExp(r'[^a-z]'), '');

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
  return _itemCategoryNames[_normalize(category)] ?? category;
}

const _rarities = <String, String>{
  'common': 'Común',
  'uncommon': 'Poco común',
  'rare': 'Rara',
  'veryrare': 'Muy rara',
  'legendary': 'Legendaria',
  'artifact': 'Artefacto',
  'varies': 'Variable',
};

String rarityLabel(String? rarity) {
  if (rarity == null) return '';
  return _rarities[_normalize(rarity)] ?? rarity;
}

/// "Truco" for level 0, "Nivel N" otherwise.
String spellLevelLabel(int level) => level == 0 ? 'Truco' : 'Nivel $level';

/// Drops the Markdown emphasis and heading marks that the SRD dataset keeps in
/// descriptions.
String cleanText(String text) => text
    .replaceAll('**', '')
    .replaceAll(RegExp(r'^#+\s*', multiLine: true), '')
    .replaceAll(RegExp(r'(?<![A-Za-z0-9])_(.+?)_(?![A-Za-z0-9])'), r'$1')
    .trim();
