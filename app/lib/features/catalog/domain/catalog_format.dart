export '../../../core/ui/text_format.dart';
export '../../items/domain/items_format.dart'
    show formatWeightLb, itemCategories, itemCategoryLabel;

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
