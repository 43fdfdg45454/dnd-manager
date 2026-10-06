import 'dart:convert';

// Hand-written catalog models for `/api/v1/catalog`. Every parser is tolerant:
// missing or null fields fall back to empty values and a few fields accept more
// than one wire shape (for example a string or an object with a `name`).

Map<String, dynamic>? _map(Object? value) => value is Map ? Map<String, dynamic>.from(value) : null;

String _str(Object? value, [String fallback = '']) {
  if (value == null) return fallback;
  return value is String ? value : value.toString();
}

String? _strOrNull(Object? value) {
  final text = _str(value);
  return text.isEmpty ? null : text;
}

int? _int(Object? value) {
  if (value is num) return value.toInt();
  if (value is String) return int.tryParse(value);
  return null;
}

double? _double(Object? value) {
  if (value is num) return value.toDouble();
  if (value is String) return double.tryParse(value);
  return null;
}

bool _bool(Object? value) => value is bool ? value : value == 'true';

/// A string list. A lone string becomes a one-element list.
List<String> _strList(Object? value) {
  if (value is List) {
    return [
      for (final e in value)
        if (e != null) _str(e),
    ];
  }
  if (value is String && value.isNotEmpty) return [value];
  return const [];
}

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) {
  if (value is! List) return const [];
  return [for (final e in value) ?_map(e).let(parse)];
}

extension<T> on T? {
  R? let<R>(R Function(T) f) {
    final self = this;
    return self == null ? null : f(self);
  }
}

/// A name that may arrive as a plain string or as `{index, name}`.
String _nameOf(Object? value) {
  if (value is Map) return _str(value['name'] ?? value['index']);
  return _str(value);
}

/// Display name derived from a slug: "wizard" -> "Wizard".
String titleFromIndex(String index) =>
    index.isEmpty ? index : '${index[0].toUpperCase()}${index.substring(1)}';

List<String> _nameList(Object? value) => value is List
    ? [
        for (final e in value)
          if (e != null) _nameOf(e),
      ]
    : const [];

/// Generic `{ items, total, page, pageSize }` envelope.
class Page<T> {
  const Page({
    required this.items,
    required this.total,
    required this.page,
    required this.pageSize,
  });

  factory Page.fromJson(Map<String, dynamic> json, T Function(Map<String, dynamic>) parse) {
    final items = _objects(json['items'], parse);
    return Page(
      items: items,
      total: _int(json['total']) ?? items.length,
      page: _int(json['page']) ?? 1,
      pageSize: _int(json['pageSize']) ?? items.length,
    );
  }

  final List<T> items;
  final int total;
  final int page;
  final int pageSize;

  bool get hasMore => items.length < total;

  Page<T> copyWith({List<T>? items, int? total, int? page}) => Page(
    items: items ?? this.items,
    total: total ?? this.total,
    page: page ?? this.page,
    pageSize: pageSize,
  );
}

/// `GET /attribution`.
class Attribution {
  const Attribution({required this.ruleset, required this.license, required this.text});

  factory Attribution.fromJson(Map<String, dynamic> json) => Attribution(
    ruleset: _str(json['ruleset']),
    license: _str(json['license']),
    text: _str(json['text']),
  );

  final String ruleset;
  final String license;
  final String text;
}

class AbilityBonus {
  const AbilityBonus({required this.ability, required this.bonus});

  factory AbilityBonus.fromJson(Map<String, dynamic> json) =>
      AbilityBonus(ability: _nameOf(json['ability']), bonus: _int(json['bonus']) ?? 0);

  /// Ability slug ("str", "dex", ...).
  final String ability;
  final int bonus;
}

class Feature {
  const Feature({
    required this.index,
    required this.name,
    this.classIndex,
    this.subclassIndex,
    this.level = 0,
    this.description = const [],
  });

  factory Feature.fromJson(Map<String, dynamic> json) => Feature(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    classIndex: _strOrNull(json['classIndex']),
    subclassIndex: _strOrNull(json['subclassIndex']),
    level: _int(json['level']) ?? 0,
    description: _strList(json['description']),
  );

  final String index;
  final String name;
  final String? classIndex;
  final String? subclassIndex;
  final int level;
  final List<String> description;

  Feature withLevel(int level) => Feature(
    index: index,
    name: name,
    classIndex: classIndex,
    subclassIndex: subclassIndex,
    level: level,
    description: description,
  );
}

// ---------------------------------------------------------------------------
// Classes
// ---------------------------------------------------------------------------

int? _hitDie(Object? value) {
  if (value is num) return value.toInt();
  final match = RegExp(r'\d+').firstMatch(_str(value));
  return match == null ? null : int.parse(match.group(0)!);
}

class ClassSummary {
  const ClassSummary({
    required this.index,
    required this.name,
    this.hitDie,
    this.isSpellcaster = false,
    this.spellcastingAbility,
  });

  factory ClassSummary.fromJson(Map<String, dynamic> json) => ClassSummary(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    hitDie: _hitDie(json['hitDie']),
    isSpellcaster: _bool(json['isSpellcaster']) || json['spellcastingAbility'] != null,
    spellcastingAbility: _strOrNull(json['spellcastingAbility']),
  );

  final String index;
  final String name;
  final int? hitDie;
  final bool isSpellcaster;
  final String? spellcastingAbility;
}

class ClassLevel {
  const ClassLevel({
    required this.level,
    this.profBonus,
    this.abilityScoreBonuses = 0,
    this.features = const [],
    this.cantripsKnown,
    this.spellsKnown,
    this.spellSlots = const [0, 0, 0, 0, 0, 0, 0, 0, 0],
    this.classSpecific = const {},
  });

  /// [resolve] maps a feature index to its full definition when the level only
  /// lists indexes.
  factory ClassLevel.fromJson(
    Map<String, dynamic> json, {
    Map<String, Feature> resolve = const {},
  }) {
    final features = <Feature>[];
    final inline = json['features'];
    if (inline is List) {
      for (final e in inline) {
        if (e is Map) {
          features.add(Feature.fromJson(Map<String, dynamic>.from(e)));
        } else if (e != null) {
          features.add(resolve[_str(e)] ?? Feature(index: _str(e), name: titleFromIndex(_str(e))));
        }
      }
    }
    if (features.isEmpty) {
      for (final index in _strList(json['featureIndexes'])) {
        features.add(resolve[index] ?? Feature(index: index, name: titleFromIndex(index)));
      }
    }
    final slots = [for (final e in (json['spellSlots'] as List? ?? const [])) _int(e) ?? 0];
    while (slots.length < 9) {
      slots.add(0);
    }
    var classSpecific = json['classSpecific'];
    if (classSpecific is String) {
      try {
        classSpecific = jsonDecode(classSpecific);
      } on FormatException {
        classSpecific = null;
      }
    }
    return ClassLevel(
      level: _int(json['level']) ?? 0,
      profBonus: _int(json['profBonus']),
      abilityScoreBonuses: _int(json['abilityScoreBonuses']) ?? 0,
      features: features,
      cantripsKnown: _int(json['cantripsKnown']),
      spellsKnown: _int(json['spellsKnown']),
      spellSlots: slots.take(9).toList(),
      classSpecific: _map(classSpecific) ?? const {},
    );
  }

  final int level;
  final int? profBonus;
  final int abilityScoreBonuses;
  final List<Feature> features;
  final int? cantripsKnown;
  final int? spellsKnown;

  /// Slots for spell levels 1..9 (always 9 entries).
  final List<int> spellSlots;
  final Map<String, dynamic> classSpecific;

  ClassLevel withFeatures(List<Feature> features) => ClassLevel(
    level: level,
    profBonus: profBonus,
    abilityScoreBonuses: abilityScoreBonuses,
    features: features,
    cantripsKnown: cantripsKnown,
    spellsKnown: spellsKnown,
    spellSlots: spellSlots,
    classSpecific: classSpecific,
  );
}

/// The server nests a subclass' features under `levels[].features`; a flat
/// `features` list is still accepted. Flattened and sorted by level, each
/// feature carrying the level it is gained at.
List<Feature> _subclassFeatures(Map<String, dynamic> json) {
  final features = <Feature>[];
  final levels = json['levels'];
  if (levels is List) {
    for (final entry in levels) {
      final level = _map(entry);
      if (level == null) continue;
      final levelNumber = _int(level['level']) ?? 0;
      for (final feature in _objects(level['features'], Feature.fromJson)) {
        features.add(feature.level == 0 ? feature.withLevel(levelNumber) : feature);
      }
    }
  }
  if (features.isEmpty) features.addAll(_objects(json['features'], Feature.fromJson));
  // Stable sort: List.sort is not guaranteed stable, so keep the original order
  // as a tie breaker.
  final order = {for (var i = 0; i < features.length; i++) features[i]: i};
  features.sort((a, b) {
    final byLevel = a.level.compareTo(b.level);
    return byLevel != 0 ? byLevel : order[a]!.compareTo(order[b]!);
  });
  return features;
}

class Subclass {
  const Subclass({
    required this.index,
    required this.name,
    this.flavor,
    this.description = const [],
    this.features = const [],
  });

  factory Subclass.fromJson(Map<String, dynamic> json) => Subclass(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    flavor: _strOrNull(json['flavor']),
    description: _strList(json['description']),
    features: _subclassFeatures(json),
  );

  final String index;
  final String name;
  final String? flavor;
  final List<String> description;
  final List<Feature> features;
}

class ClassDetail extends ClassSummary {
  const ClassDetail({
    required super.index,
    required super.name,
    super.hitDie,
    super.isSpellcaster,
    super.spellcastingAbility,
    this.spellcastingLevel = 0,
    this.savingThrows = const [],
    this.proficiencies = const [],
    this.subclassFlavor,
    this.startingEquipmentText,
    this.levels = const [],
    this.subclasses = const [],
    this.features = const [],
  });

  factory ClassDetail.fromJson(Map<String, dynamic> json) {
    final summary = ClassSummary.fromJson(json);
    final features = _objects(json['features'], Feature.fromJson);
    final byIndex = {for (final f in features) f.index: f};
    var levels = _objects(json['levels'], (e) => ClassLevel.fromJson(e, resolve: byIndex));
    levels.sort((a, b) => a.level.compareTo(b.level));
    // Levels without their own feature list fall back to the class feature
    // list grouped by level.
    if (levels.every((l) => l.features.isEmpty) && features.isNotEmpty) {
      levels = [
        for (final l in levels)
          l.withFeatures([
            for (final f in features)
              if (f.level == l.level) f,
          ]),
      ];
    }
    return ClassDetail(
      index: summary.index,
      name: summary.name,
      hitDie: summary.hitDie,
      isSpellcaster: summary.isSpellcaster,
      spellcastingAbility: summary.spellcastingAbility,
      spellcastingLevel: _int(json['spellcastingLevel']) ?? 0,
      savingThrows: _nameList(json['savingThrows']),
      proficiencies: _nameList(json['proficiencyNames'] ?? json['proficiencies']),
      subclassFlavor: _strOrNull(json['subclassFlavor']),
      startingEquipmentText: _strOrNull(json['startingEquipmentText']),
      levels: levels,
      subclasses: _objects(json['subclasses'], Subclass.fromJson),
      features: features,
    );
  }

  final int spellcastingLevel;

  /// Ability slugs ("str", "con", ...).
  final List<String> savingThrows;
  final List<String> proficiencies;
  final String? subclassFlavor;
  final String? startingEquipmentText;
  final List<ClassLevel> levels;
  final List<Subclass> subclasses;
  final List<Feature> features;

  /// True when any level has spell slots or the class is flagged as caster.
  bool get hasSpellSlots => isSpellcaster || levels.any((l) => l.spellSlots.any((s) => s > 0));
}

// ---------------------------------------------------------------------------
// Races
// ---------------------------------------------------------------------------

class RaceSummary {
  const RaceSummary({required this.index, required this.name, this.speed, this.size});

  factory RaceSummary.fromJson(Map<String, dynamic> json) => RaceSummary(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    speed: _int(json['speed']),
    size: _strOrNull(json['size']),
  );

  final String index;
  final String name;
  final int? speed;
  final String? size;
}

class Trait {
  const Trait({required this.index, required this.name, this.description = const []});

  factory Trait.fromJson(Map<String, dynamic> json) => Trait(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    description: _strList(json['description']),
  );

  final String index;
  final String name;
  final List<String> description;
}

class Subrace {
  const Subrace({
    required this.index,
    required this.name,
    this.description = const [],
    this.abilityBonuses = const [],
    this.traits = const [],
  });

  factory Subrace.fromJson(Map<String, dynamic> json) => Subrace(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    description: _strList(json['description']),
    abilityBonuses: _objects(json['abilityBonuses'], AbilityBonus.fromJson),
    traits: _objects(json['traits'], Trait.fromJson),
  );

  final String index;
  final String name;
  final List<String> description;
  final List<AbilityBonus> abilityBonuses;
  final List<Trait> traits;
}

class RaceDetail extends RaceSummary {
  const RaceDetail({
    required super.index,
    required super.name,
    super.speed,
    super.size,
    this.abilityBonuses = const [],
    this.traits = const [],
    this.languages = const [],
    this.age,
    this.alignment,
    this.sizeDescription,
    this.subraces = const [],
  });

  factory RaceDetail.fromJson(Map<String, dynamic> json) {
    final summary = RaceSummary.fromJson(json);
    return RaceDetail(
      index: summary.index,
      name: summary.name,
      speed: summary.speed,
      size: summary.size,
      abilityBonuses: _objects(json['abilityBonuses'], AbilityBonus.fromJson),
      traits: _objects(json['traits'], Trait.fromJson),
      languages: _nameList(json['languages']),
      age: _strOrNull(json['age']),
      alignment: _strOrNull(json['alignment']),
      sizeDescription: _strOrNull(json['sizeDescription']),
      subraces: _objects(json['subraces'], Subrace.fromJson),
    );
  }

  final List<AbilityBonus> abilityBonuses;
  final List<Trait> traits;
  final List<String> languages;
  final String? age;
  final String? alignment;
  final String? sizeDescription;
  final List<Subrace> subraces;
}

// ---------------------------------------------------------------------------
// Spells
// ---------------------------------------------------------------------------

class SpellSummary {
  const SpellSummary({
    required this.index,
    required this.name,
    required this.level,
    this.school,
    this.castingTime,
    this.concentration = false,
    this.ritual = false,
    this.classes = const [],
  });

  factory SpellSummary.fromJson(Map<String, dynamic> json) => SpellSummary(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    level: _int(json['level']) ?? 0,
    school: _strOrNull(_nameOf(json['school'])),
    castingTime: _strOrNull(json['castingTime']),
    concentration: _bool(json['concentration']),
    ritual: _bool(json['ritual']),
    classes: _spellClasses(json),
  );

  final String index;
  final String name;

  /// 0 for cantrips, 1..9 otherwise.
  final int level;
  final String? school;
  final String? castingTime;
  final bool concentration;
  final bool ritual;

  /// Display names of the classes that can cast the spell.
  final List<String> classes;
}

List<String> _spellClasses(Map<String, dynamic> json) {
  final named = _nameList(json['classes']);
  if (named.isNotEmpty) return named;
  return [for (final index in _strList(json['classIndexes'])) titleFromIndex(index)];
}

/// `damage` as `{dice, type}` or plain text, flattened for display.
String? damageText(Object? value) {
  if (value is Map) {
    final dice = _str(value['dice']);
    final type = _nameOf(value['type']);
    final text = [dice, type].where((e) => e.isNotEmpty).join(' ');
    return text.isEmpty ? null : text;
  }
  return _strOrNull(value);
}

class SpellDetail extends SpellSummary {
  const SpellDetail({
    required super.index,
    required super.name,
    required super.level,
    super.school,
    super.castingTime,
    super.concentration,
    super.ritual,
    super.classes,
    this.range,
    this.components = const [],
    this.material,
    this.duration,
    this.description = const [],
    this.higherLevel = const [],
    this.attackType,
    this.damage,
    this.dcAbility,
  });

  factory SpellDetail.fromJson(Map<String, dynamic> json) {
    final summary = SpellSummary.fromJson(json);
    return SpellDetail(
      index: summary.index,
      name: summary.name,
      level: summary.level,
      school: summary.school,
      castingTime: summary.castingTime,
      concentration: summary.concentration,
      ritual: summary.ritual,
      classes: summary.classes,
      range: _strOrNull(json['range']),
      components: _strList(json['components']),
      material: _strOrNull(json['material']),
      duration: _strOrNull(json['duration']),
      description: _strList(json['description']),
      higherLevel: _strList(json['higherLevel']),
      attackType: _strOrNull(json['attackType']),
      damage: damageText(json['damage']),
      dcAbility: _strOrNull(_nameOf(json['dcAbility'])),
    );
  }

  final String? range;

  /// "V", "S", "M".
  final List<String> components;
  final String? material;
  final String? duration;
  final List<String> description;
  final List<String> higherLevel;
  final String? attackType;
  final String? damage;
  final String? dcAbility;
}

// ---------------------------------------------------------------------------
// Items
// ---------------------------------------------------------------------------

class ItemSummary {
  const ItemSummary({
    required this.id,
    required this.name,
    this.index,
    this.category,
    this.subcategory,
    this.rarity,
    this.requiresAttunement = false,
    this.costCp,
    this.weightLb,
    this.source,
  });

  factory ItemSummary.fromJson(Map<String, dynamic> json) => ItemSummary(
    id: _str(json['id']),
    index: _strOrNull(json['index']),
    name: _str(json['name'], _str(json['index'])),
    category: _strOrNull(json['category']),
    subcategory: _strOrNull(json['subcategory']),
    rarity: _strOrNull(json['rarity']),
    requiresAttunement: _bool(json['requiresAttunement']),
    costCp: _int(json['costCp']),
    weightLb: _double(json['weightLb']),
    source: _strOrNull(json['source']),
  );

  final String id;
  final String? index;
  final String name;
  final String? category;
  final String? subcategory;
  final String? rarity;
  final bool requiresAttunement;

  /// "srd" or "homebrew" (campaign item); null when the server does not say.
  final String? source;

  /// Whether the item belongs to the campaign (and can be edited), as opposed
  /// to coming from the SRD.
  bool get isHomebrew => source != 'srd';

  /// Cost in copper pieces; null when unknown.
  final int? costCp;
  final double? weightLb;
}

class ItemDamage {
  const ItemDamage({required this.dice, this.type, this.versatileDice});

  final String dice;
  final String? type;
  final String? versatileDice;
}

class ItemArmor {
  const ItemArmor({
    this.baseAc,
    this.addDexModifier,
    this.maxDexBonus,
    this.strengthMinimum,
    this.stealthDisadvantage = false,
  });

  final int? baseAc;
  final bool? addDexModifier;
  final int? maxDexBonus;
  final int? strengthMinimum;
  final bool stealthDisadvantage;
}

class ItemDetail extends ItemSummary {
  const ItemDetail({
    required super.id,
    required super.name,
    super.index,
    super.category,
    super.subcategory,
    super.rarity,
    super.requiresAttunement,
    super.costCp,
    super.weightLb,
    this.damage,
    this.armor,
    this.rangeNormal,
    this.rangeLong,
    this.properties = const [],
    this.description = const [],
    this.effects = const [],
    this.modifiers = const [],
  });

  factory ItemDetail.fromJson(Map<String, dynamic> json) {
    final summary = ItemSummary.fromJson(json);

    final damageJson = _map(json['damage']);
    final dice = _strOrNull(damageJson?['dice'] ?? json['damageDice']);
    final versatile = _strOrNull(damageJson?['versatileDice'] ?? json['versatileDice']);
    final damage = dice == null
        ? null
        : ItemDamage(
            dice: dice,
            type: _strOrNull(_nameOf(damageJson?['type'] ?? json['damageType'])),
            versatileDice: versatile,
          );

    // The armor block may be `armor`, `armorClass` (object) or flat fields.
    final armorJson = _map(json['armor']) ?? _map(json['armorClass']) ?? const {};
    final baseAc = _int(
      armorJson['base'] ??
          armorJson['armorClassBase'] ??
          json['armorClassBase'] ??
          (json['armorClass'] is Map ? null : json['armorClass']),
    );
    final armor = baseAc == null
        ? null
        : ItemArmor(
            baseAc: baseAc,
            addDexModifier: _boolOrNull(armorJson['addDexModifier'] ?? json['addDexModifier']),
            maxDexBonus: _int(armorJson['maxDexBonus'] ?? json['maxDexBonus']),
            strengthMinimum: _int(armorJson['strengthMinimum'] ?? json['strengthMinimum']),
            stealthDisadvantage: _bool(
              armorJson['stealthDisadvantage'] ?? json['stealthDisadvantage'],
            ),
          );

    final rangeJson = _map(json['range']);
    return ItemDetail(
      id: summary.id,
      index: summary.index,
      name: summary.name,
      category: summary.category,
      subcategory: summary.subcategory,
      rarity: summary.rarity,
      requiresAttunement: summary.requiresAttunement,
      costCp: summary.costCp,
      weightLb: summary.weightLb,
      damage: damage,
      armor: armor,
      rangeNormal: _int(rangeJson?['normal'] ?? json['rangeNormal']),
      rangeLong: _int(rangeJson?['long'] ?? json['rangeLong']),
      properties: _nameList(json['properties']),
      description: _strList(json['description']),
      effects: _strList(json['effects']),
      modifiers: ItemModifier.listFromJson(json['modifiers']),
    );
  }

  final ItemDamage? damage;
  final ItemArmor? armor;
  final int? rangeNormal;
  final int? rangeLong;

  /// Weapon property names ("Finesse", "Light", ...).
  final List<String> properties;
  final List<String> description;

  /// Free-text effects ("+1 a ataque y daño"); only homebrew and magic items
  /// have them.
  final List<String> effects;

  /// Structured modifiers applied to the character while the item is worn.
  final List<ItemModifier> modifiers;
}

/// Modifier kinds as the API spells them (`ItemModifierKind`).
const itemModifierKinds = <String>[
  'AbilityBonus',
  'AbilitySet',
  'SaveBonus',
  'SkillBonus',
  'ArmorClassBonus',
  'AttackBonus',
  'DamageBonus',
  'SpeedBonus',
  'HitPointsMaxBonus',
  'InitiativeBonus',
];

/// A structured item modifier (`ItemModifierDto`): [kind] is an
/// `ItemModifierKind` name; [target] an ability index ("str".."cha") for the
/// ability and save kinds, a skill index for `SkillBonus`, or null (all saves
/// or skills, or a kind without target).
class ItemModifier {
  const ItemModifier({required this.kind, this.target, required this.value});

  factory ItemModifier.fromJson(Map<String, dynamic> json) => ItemModifier(
    kind: _str(json['kind']),
    target: _strOrNull(json['target']),
    value: _int(json['value']) ?? 0,
  );

  /// Tolerant list parser: anything but a list gives an empty list.
  static List<ItemModifier> listFromJson(Object? value) => _objects(value, ItemModifier.fromJson);

  final String kind;
  final String? target;
  final int value;

  Map<String, dynamic> toJson() => {'kind': kind, 'target': ?target, 'value': value};

  @override
  bool operator ==(Object other) =>
      other is ItemModifier && other.kind == kind && other.target == target && other.value == value;

  @override
  int get hashCode => Object.hash(kind, target, value);

  @override
  String toString() => 'ItemModifier($kind, $target, $value)';
}

bool? _boolOrNull(Object? value) => value == null ? null : _bool(value);

// ---------------------------------------------------------------------------
// Reference lists
// ---------------------------------------------------------------------------

class Condition {
  const Condition({required this.index, required this.name, this.description = const []});

  factory Condition.fromJson(Map<String, dynamic> json) => Condition(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    description: _strList(json['description']),
  );

  final String index;
  final String name;
  final List<String> description;
}

class Skill {
  const Skill({required this.index, required this.name, this.ability, this.description = const []});

  factory Skill.fromJson(Map<String, dynamic> json) => Skill(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    ability: _strOrNull(_nameOf(json['abilityIndex'] ?? json['ability'])),
    description: _strList(json['description']),
  );

  final String index;
  final String name;
  final String? ability;
  final List<String> description;
}

class Background {
  const Background({
    required this.index,
    required this.name,
    this.featureName,
    this.featureDescription = const [],
    this.skillProficiencies = const [],
    this.startingEquipmentText,
  });

  factory Background.fromJson(Map<String, dynamic> json) => Background(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    featureName: _strOrNull(json['featureName']),
    featureDescription: _strList(json['featureDescription']),
    skillProficiencies: _nameList(json['skillProficiencies']),
    startingEquipmentText: _strOrNull(json['startingEquipmentText']),
  );

  final String index;
  final String name;
  final String? featureName;
  final List<String> featureDescription;
  final List<String> skillProficiencies;
  final String? startingEquipmentText;
}
