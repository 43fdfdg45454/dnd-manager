// Animal companion of a character (phase 25, block 6): the feature that
// grants it (`CompanionFeatureDto`) and the companion with its recalculated
// statblock (`CharacterCompanionDto`).

import 'models.dart' show ValueBreakdown;

Map<String, dynamic> _map(Object? value) =>
    value is Map ? Map<String, dynamic>.from(value) : const {};

String _str(Object? value, [String fallback = '']) => value is String ? value : fallback;

String? _strOrNull(Object? value) => value is String && value.isNotEmpty ? value : null;

int? _int(Object? value) => switch (value) {
  final num v => v.toInt(),
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

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) => [
  if (value is List)
    for (final e in value)
      if (e is Map) parse(Map<String, dynamic>.from(e)),
];

/// Spanish names of the creature sizes the companion filter uses.
const companionSizeLabels = {
  'Tiny': 'Diminuta',
  'Small': 'Pequeña',
  'Medium': 'Mediana',
  'Large': 'Grande',
  'Huge': 'Enorme',
  'Gargantuan': 'Gargantuesca',
};

/// The subclass feature that grants the companion: which beasts qualify.
class CompanionFeature {
  const CompanionFeature({
    required this.featureIndex,
    required this.featureName,
    this.classIndex = '',
    this.classLevel = 0,
    this.maxChallengeRating = 0,
    this.maxChallengeRatingText = '0',
    this.sizes = const [],
    this.hitPoints = 'beast',
    this.proficiencyBonusFromCharacter = false,
    this.attackBonusFromCharacter = false,
  });

  factory CompanionFeature.fromJson(Map<String, dynamic> json) => CompanionFeature(
    featureIndex: _str(json['featureIndex']),
    featureName: _str(json['featureName']),
    classIndex: _str(json['classIndex']),
    classLevel: _int(json['classLevel']) ?? 0,
    maxChallengeRating: _double(json['maxChallengeRating']),
    maxChallengeRatingText: _str(json['maxChallengeRatingText'], '0'),
    sizes: [
      if (json['sizes'] is List)
        for (final s in json['sizes'] as List)
          if (s is String) s,
    ],
    hitPoints: _str(json['hitPoints'], 'beast'),
    proficiencyBonusFromCharacter: json['proficiencyBonusFromCharacter'] == true,
    attackBonusFromCharacter: json['attackBonusFromCharacter'] == true,
  );

  final String featureIndex;
  final String featureName;
  final String classIndex;
  final int classLevel;
  final double maxChallengeRating;
  final String maxChallengeRatingText;

  /// Allowed sizes ("Medium"); empty: any.
  final List<String> sizes;

  /// "beast" or "max(beast, N*classLevel)".
  final String hitPoints;
  final bool proficiencyBonusFromCharacter;
  final bool attackBonusFromCharacter;

  /// Whether a beast of [challengeRating] and [size] qualifies.
  bool allows(double challengeRating, String size) =>
      challengeRating <= maxChallengeRating &&
      (sizes.isEmpty || sizes.any((s) => s.toLowerCase() == size.toLowerCase()));

  /// "VD 1/4 o menos, tamaño mediana o pequeña".
  String get filterText => [
    'VD $maxChallengeRatingText o menos',
    if (sizes.isNotEmpty)
      'tamaño ${sizes.map((s) => (companionSizeLabels[s] ?? s).toLowerCase()).join(' o ')}',
  ].join(', ');
}

/// A damage component of a companion attack, the bonus already in the dice.
class CompanionDamage {
  const CompanionDamage({required this.dice, this.type});

  factory CompanionDamage.fromJson(Map<String, dynamic> json) =>
      CompanionDamage(dice: _str(json['dice']), type: _strOrNull(json['type']));

  final String dice;
  final String? type;
}

/// An action of the companion with the breakdowns of its bonuses.
class CompanionAttack {
  const CompanionAttack({
    required this.name,
    this.description = '',
    this.attackBonus,
    this.attackBreakdown,
    this.damage = const [],
    this.damageBreakdown,
    this.isMultiattack = false,
  });

  factory CompanionAttack.fromJson(Map<String, dynamic> json) => CompanionAttack(
    name: _str(json['name']),
    description: _str(json['description']),
    attackBonus: _int(json['attackBonus']),
    attackBreakdown: ValueBreakdown.maybeFromJson(json['attackBreakdown']),
    damage: _objects(json['damage'], CompanionDamage.fromJson),
    damageBreakdown: ValueBreakdown.maybeFromJson(json['damageBreakdown']),
    isMultiattack: json['isMultiattack'] == true,
  );

  final String name;
  final String description;
  final int? attackBonus;
  final ValueBreakdown? attackBreakdown;
  final List<CompanionDamage> damage;
  final ValueBreakdown? damageBreakdown;
  final bool isMultiattack;

  /// Every damage die of the attack in one expression ("2d4+4+1d6").
  String? get damageExpression =>
      damage.isEmpty ? null : damage.map((d) => d.dice).join('+').replaceAll('+-', '-');
}

/// The companion: stored beast, name and current hit points, and its statblock
/// recalculated with the character's proficiency bonus.
class CharacterCompanion {
  const CharacterCompanion({
    required this.id,
    required this.beastIndex,
    required this.beastName,
    required this.name,
    this.size = '',
    this.challengeRatingText = '0',
    this.hitPointsCurrent = 0,
    this.hitPointsMax = 0,
    this.hitPointsMaxOverride,
    this.armorClass = 10,
    this.speeds = const {},
    this.abilities = const {},
    this.savingThrows = const {},
    this.skills = const {},
    this.senses = const {},
    this.passivePerception = 10,
    this.attacks = const [],
    this.breakdowns = const {},
    this.beastMissing = false,
  });

  factory CharacterCompanion.fromJson(Map<String, dynamic> json) => CharacterCompanion(
    id: _str(json['id']),
    beastIndex: _str(json['beastIndex']),
    beastName: _str(json['beastName'], _str(json['beastIndex'])),
    name: _str(json['name']),
    size: _str(json['size']),
    challengeRatingText: _str(json['challengeRatingText'], '0'),
    hitPointsCurrent: _int(json['hitPointsCurrent']) ?? 0,
    hitPointsMax: _int(json['hitPointsMax']) ?? 0,
    hitPointsMaxOverride: _int(json['hitPointsMaxOverride']),
    armorClass: _int(json['armorClass']) ?? 10,
    speeds: _intMap(json['speeds']),
    abilities: _intMap(json['abilities']),
    savingThrows: _intMap(json['savingThrows']),
    skills: _intMap(json['skills']),
    senses: {
      for (final e in _map(json['senses']).entries)
        if (e.value != null) e.key: '${e.value}',
    },
    passivePerception: _int(json['passivePerception']) ?? 10,
    attacks: _objects(json['attacks'], CompanionAttack.fromJson),
    breakdowns: {
      for (final e in _map(json['breakdowns']).entries)
        if (ValueBreakdown.maybeFromJson(e.value) case final ValueBreakdown b) e.key: b,
    },
    beastMissing: json['beastMissing'] == true,
  );

  final String id;
  final String beastIndex;
  final String beastName;
  final String name;
  final String size;
  final String challengeRatingText;
  final int hitPointsCurrent;
  final int hitPointsMax;
  final int? hitPointsMaxOverride;
  final int armorClass;
  final Map<String, int> speeds;
  final Map<String, int> abilities;
  final Map<String, int> savingThrows;
  final Map<String, int> skills;
  final Map<String, String> senses;
  final int passivePerception;
  final List<CompanionAttack> attacks;

  /// "armorClass", "hitPointsMax", "save.dex", "skill.perception".
  final Map<String, ValueBreakdown> breakdowns;

  /// The beast is no longer in the catalog.
  final bool beastMissing;
}
