export 'package:opentrpg_core/core/ui/text_format.dart';
export 'package:opentrpg_core/features/items/domain/items_format.dart'
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

const _ruleCategories = <String, String>{
  'variant': 'Variante',
  'multiclassing': 'Multiclase',
  'equipment': 'Equipo',
  'general': 'General',
  'combat': 'Combate',
  'spellcasting': 'Magia',
  'adventuring': 'Aventura',
};

/// Spanish name of the category of a rules document ("variant" ->
/// "Variante"); an unknown category is shown capitalised as it comes.
String ruleCategoryLabel(String category) {
  final known = _ruleCategories[category.toLowerCase()];
  if (known != null) return known;
  return category.isEmpty ? category : '${category[0].toUpperCase()}${category.substring(1)}';
}

const _spellProgressions = <String, String>{
  'full': 'Completo',
  'half': 'Medio',
  'third': 'Un tercio',
  'pact': 'Magia de pacto',
  'table': 'Tabla propia',
};

/// Spanish name of the spell slot progression of a class ("full" ->
/// "Completo").
String spellProgressionLabel(String progression) =>
    _spellProgressions[progression.toLowerCase()] ?? progression;

/// "Preparados" or "Conocidos" for the way a class readies its spells.
String spellPreparationLabel(String preparation) => switch (preparation.toLowerCase()) {
  'prepared' => 'Preparados',
  'known' => 'Conocidos',
  _ => preparation,
};

/// When a resource of a class recharges ("LongRest" -> "Descanso largo").
String rechargeLabel(String recharge) => switch (recharge.toLowerCase()) {
  'longrest' => 'Descanso largo',
  'shortrest' => 'Descanso corto',
  'dawn' => 'Al amanecer',
  'none' || '' => 'No se recupera',
  _ => recharge,
};

/// The maximum of a resource whose formula is not a table ("mod:wis" ->
/// "Mod. de Sabiduría"); an unknown formula is shown as it comes.
String resourceMaxLabel(String max) {
  final formula = max.trim();
  if (formula == 'proficiencyBonus') return 'Bonificador de competencia';
  if (formula.startsWith('mod:')) return 'Mod. de ${abilityLabel(formula.substring(4))}';
  if (formula.startsWith('level')) return 'Nivel de la clase';
  return formula;
}
