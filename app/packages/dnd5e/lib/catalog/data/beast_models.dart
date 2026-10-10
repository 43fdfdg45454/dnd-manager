// Creatures of the catalog (`/api/v1/systems/dnd5e/catalog/beasts`): the SRD beasts
// (the wild shapes of a druid) and the creatures of the content packs.

Map<String, dynamic> _map(Object? value) =>
    value is Map ? Map<String, dynamic>.from(value) : const {};

String _str(Object? value, [String fallback = '']) => value is String ? value : fallback;

String? _strOrNull(Object? value) => value is String && value.isNotEmpty ? value : null;

int? _int(Object? value) => switch (value) {
  final int v => v,
  final num v => v.round(),
  final String v => int.tryParse(v),
  _ => null,
};

double _double(Object? value) => switch (value) {
  final num v => v.toDouble(),
  final String v => double.tryParse(v) ?? 0,
  _ => 0,
};

Map<String, int> _intMap(Object? value) => {
  for (final e in _map(value).entries)
    if (_int(e.value) case final int v) e.key: v,
};

List<String> _strList(Object? value) => [
  if (value is List)
    for (final e in value)
      if (e is String) e,
];

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) => [
  if (value is List)
    for (final e in value)
      if (e is Map) parse(Map<String, dynamic>.from(e)),
];

/// Speed kinds in the order a statblock prints them.
const beastSpeedOrder = ['walk', 'burrow', 'climb', 'fly', 'swim'];

/// "30 ft., nadar 40 ft." style labels of the speed kinds.
const beastSpeedLabels = {
  'walk': 'caminar',
  'burrow': 'excavar',
  'climb': 'trepar',
  'fly': 'volar',
  'swim': 'nadar',
};

/// "40 pies, trepar 30 pies" from a speed map.
String formatBeastSpeeds(Map<String, int> speeds) => [
  for (final kind in beastSpeedOrder)
    if (speeds[kind] case final int feet)
      kind == 'walk' ? '$feet pies' : '${beastSpeedLabels[kind]} $feet pies',
].join(', ');

/// A beast in a list (`BeastSummaryDto`).
class BeastSummary {
  const BeastSummary({
    required this.index,
    required this.name,
    this.size = '',
    this.challengeRating = 0,
    this.challengeRatingText = '0',
    this.armorClass = 10,
    this.hitPoints = 1,
    this.speeds = const {},
    this.type = 'beast',
    this.source,
  });

  factory BeastSummary.fromJson(Map<String, dynamic> json) => BeastSummary(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    size: _str(json['size']),
    challengeRating: _double(json['challengeRating']),
    challengeRatingText: _str(json['challengeRatingText'], '0'),
    armorClass: _int(json['armorClass']) ?? 10,
    hitPoints: _int(json['hitPoints']) ?? 1,
    speeds: _intMap(json['speeds']),
    type: _str(json['type'], 'beast'),
    source: _strOrNull(json['source']),
  );

  final String index;
  final String name;
  final String size;
  final double challengeRating;
  final String challengeRatingText;
  final int armorClass;
  final int hitPoints;

  /// Feet by kind ("walk", "fly", "swim", "climb", "burrow").
  final Map<String, int> speeds;

  /// Creature type in lowercase ("beast", "monstrosity"...).
  final String type;

  /// "srd" or the content pack of the creature; null when not sent.
  final String? source;

  bool get isBeast => type.toLowerCase() == 'beast';

  bool get flies => (speeds['fly'] ?? 0) > 0;

  bool get swims => (speeds['swim'] ?? 0) > 0;
}

class BeastDamage {
  const BeastDamage({required this.dice, this.type});

  factory BeastDamage.fromJson(Map<String, dynamic> json) =>
      BeastDamage(dice: _str(json['dice']), type: _strOrNull(json['type']));

  /// "2d4+2".
  final String dice;
  final String? type;
}

class BeastSave {
  const BeastSave({required this.dc, required this.ability});

  factory BeastSave.fromJson(Map<String, dynamic> json) =>
      BeastSave(dc: _int(json['dc']) ?? 10, ability: _str(json['ability']));

  final int dc;

  /// Ability index ("str".."cha").
  final String ability;
}

class BeastTrait {
  const BeastTrait({required this.name, this.description = '', this.save});

  factory BeastTrait.fromJson(Map<String, dynamic> json) => BeastTrait(
    name: _str(json['name']),
    description: _str(json['description']),
    save: json['save'] is Map ? BeastSave.fromJson(_map(json['save'])) : null,
  );

  final String name;
  final String description;
  final BeastSave? save;
}

class BeastAction {
  const BeastAction({
    required this.name,
    this.description = '',
    this.attackBonus,
    this.damage = const [],
    this.save,
    this.isMultiattack = false,
  });

  factory BeastAction.fromJson(Map<String, dynamic> json) => BeastAction(
    name: _str(json['name']),
    description: _str(json['description']),
    attackBonus: _int(json['attackBonus']),
    damage: _objects(json['damage'], BeastDamage.fromJson),
    save: json['save'] is Map ? BeastSave.fromJson(_map(json['save'])) : null,
    isMultiattack: json['isMultiattack'] == true,
  );

  final String name;
  final String description;
  final int? attackBonus;
  final List<BeastDamage> damage;
  final BeastSave? save;
  final bool isMultiattack;

  /// Every damage die of the action in one expression ("2d4+2+1d6").
  String? get damageExpression =>
      damage.isEmpty ? null : damage.map((d) => d.dice).join('+').replaceAll('+-', '-');
}

/// Full statblock of a beast (`BeastDto`).
class Beast extends BeastSummary {
  const Beast({
    required super.index,
    required super.name,
    super.size,
    super.challengeRating,
    super.challengeRatingText,
    super.armorClass,
    super.hitPoints,
    super.speeds,
    super.type,
    super.source,
    this.subtype,
    this.reactions = const [],
    this.legendaryActions = const [],
    this.alignment = '',
    this.xp = 0,
    this.proficiencyBonus = 2,
    this.armorClassType,
    this.hitDice = '',
    this.hitPointsRoll,
    this.abilities = const {},
    this.savingThrows = const {},
    this.skills = const {},
    this.senses = const {},
    this.passivePerception = 10,
    this.languages = '',
    this.damageVulnerabilities = const [],
    this.damageResistances = const [],
    this.damageImmunities = const [],
    this.conditionImmunities = const [],
    this.traits = const [],
    this.actions = const [],
    this.description,
  });

  factory Beast.fromJson(Map<String, dynamic> json) {
    final summary = BeastSummary.fromJson(json);
    return Beast(
      index: summary.index,
      name: summary.name,
      size: summary.size,
      challengeRating: summary.challengeRating,
      challengeRatingText: summary.challengeRatingText,
      armorClass: summary.armorClass,
      hitPoints: summary.hitPoints,
      speeds: summary.speeds,
      type: summary.type,
      source: summary.source,
      subtype: _strOrNull(json['subtype']),
      reactions: _objects(json['reactions'], BeastAction.fromJson),
      legendaryActions: _objects(json['legendaryActions'], BeastAction.fromJson),
      alignment: _str(json['alignment']),
      xp: _int(json['xp']) ?? 0,
      proficiencyBonus: _int(json['proficiencyBonus']) ?? 2,
      armorClassType: _strOrNull(json['armorClassType']),
      hitDice: _str(json['hitDice']),
      hitPointsRoll: _strOrNull(json['hitPointsRoll']),
      abilities: _intMap(json['abilities']),
      savingThrows: _intMap(json['savingThrows']),
      skills: _intMap(json['skills']),
      senses: {
        for (final e in _map(json['senses']).entries)
          if (e.value != null) e.key: '${e.value}',
      },
      passivePerception: _int(json['passivePerception']) ?? 10,
      languages: _str(json['languages']),
      damageVulnerabilities: _strList(json['damageVulnerabilities']),
      damageResistances: _strList(json['damageResistances']),
      damageImmunities: _strList(json['damageImmunities']),
      conditionImmunities: _strList(json['conditionImmunities']),
      traits: _objects(json['traits'], BeastTrait.fromJson),
      actions: _objects(json['actions'], BeastAction.fromJson),
      description: _strOrNull(json['description']),
    );
  }

  /// "goblinoid", "shapechanger"... when the creature has one.
  final String? subtype;
  final List<BeastAction> reactions;
  final List<BeastAction> legendaryActions;
  final String alignment;
  final int xp;
  final int proficiencyBonus;
  final String? armorClassType;
  final String hitDice;
  final String? hitPointsRoll;

  /// Scores by ability index ("str".."cha").
  final Map<String, int> abilities;
  final Map<String, int> savingThrows;

  /// Bonuses by skill index ("perception").
  final Map<String, int> skills;
  final Map<String, String> senses;
  final int passivePerception;
  final String languages;
  final List<String> damageVulnerabilities;
  final List<String> damageResistances;
  final List<String> damageImmunities;
  final List<String> conditionImmunities;
  final List<BeastTrait> traits;
  final List<BeastAction> actions;
  final String? description;
}

/// Wild shape limits of a druid: highest challenge rating and whether flying
/// and swimming forms are allowed.
typedef WildShapeLimits = ({double maxCr, bool fly, bool swim});

/// Query of `GET /catalog/beasts`.
typedef BeastQuery = ({double? maxCr, bool? fly, bool? swim});
