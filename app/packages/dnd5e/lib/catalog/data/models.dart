import 'dart:convert';

import 'package:opentrpg_core/features/items/data/models.dart' show ItemSummary;

// Hand-written catalog models for `/api/v1/systems/dnd5e/catalog`. Every parser is tolerant:
// missing or null fields fall back to empty values and a few fields accept more
// than one wire shape (for example a string or an object with a `name`).

export 'package:opentrpg_core/core/catalog/catalog_models.dart';
export 'package:opentrpg_core/features/items/data/models.dart' show ItemSummary;

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
    this.source,
  });

  factory ClassSummary.fromJson(Map<String, dynamic> json) => ClassSummary(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    hitDie: _hitDie(json['hitDie']),
    isSpellcaster: _bool(json['isSpellcaster']) || json['spellcastingAbility'] != null,
    spellcastingAbility: _strOrNull(json['spellcastingAbility']),
    source: _strOrNull(json['source']),
  );

  final String index;
  final String name;
  final int? hitDie;
  final bool isSpellcaster;
  final String? spellcastingAbility;

  /// "srd" or the id of the content pack that added it; null when not sent.
  final String? source;
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
    this.source,
  });

  factory Subclass.fromJson(Map<String, dynamic> json) => Subclass(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    flavor: _strOrNull(json['flavor']),
    description: _strList(json['description']),
    features: _subclassFeatures(json),
    source: _strOrNull(json['source']),
  );

  final String index;
  final String name;

  /// "srd", "homebrew" or the id of a content pack; null when not sent.
  final String? source;
  final String? flavor;
  final List<String> description;
  final List<Feature> features;
}

/// Level 1 skill proficiency choice of a class: pick [choose] of [from] (skill
/// indexes such as "arcana").
class SkillChoices {
  const SkillChoices({this.choose = 0, this.from = const []});

  factory SkillChoices.fromJson(Map<String, dynamic> json) =>
      SkillChoices(choose: _int(json['choose']) ?? 0, from: _strList(json['from']));

  final int choose;
  final List<String> from;
}

/// How a class of a content pack casts spells (`ClassSpellcastingDto`).
class ClassSpellcasting {
  const ClassSpellcasting({
    this.progression = '',
    this.preparation = '',
    this.ritual = false,
    this.focus,
  });

  factory ClassSpellcasting.fromJson(Map<String, dynamic> json) => ClassSpellcasting(
    progression: _str(json['progression']),
    preparation: _str(json['preparation']),
    ritual: _bool(json['ritual']),
    focus: _strOrNull(json['focus']),
  );

  /// "full", "half", "third", "pact" or "table" (own slots table).
  final String progression;

  /// "prepared" or "known".
  final String preparation;
  final bool ritual;

  /// Index of the spellcasting focus ("alchemists-supplies"), if any.
  final String? focus;
}

/// What multiclassing into a class of a content pack asks and gives
/// (`ClassMulticlassingDto`).
class ClassMulticlassing {
  const ClassMulticlassing({
    this.prerequisites = const {},
    this.armor = const [],
    this.weapons = const [],
    this.tools = const [],
    this.skills = 0,
  });

  factory ClassMulticlassing.fromJson(Map<String, dynamic> json) => ClassMulticlassing(
    prerequisites: {
      for (final e in (_map(json['prerequisites']) ?? const <String, dynamic>{}).entries)
        if (_int(e.value) case final int v) e.key: v,
    },
    armor: _strList(json['armor']),
    weapons: _strList(json['weapons']),
    tools: _strList(json['tools']),
    skills: _int(json['skills']) ?? 0,
  );

  /// Minimum score by ability index ("int": 13).
  final Map<String, int> prerequisites;
  final List<String> armor;
  final List<String> weapons;
  final List<String> tools;

  /// Skills of the class list gained when multiclassing into it.
  final int skills;
}

/// A resource of a class of a content pack (`ClassResourceDto`).
class ClassResource {
  const ClassResource({
    required this.key,
    required this.name,
    this.max = '',
    this.maxByLevel,
    this.recharge = '',
  });

  factory ClassResource.fromJson(Map<String, dynamic> json) {
    final byLevel = _map(json['maxByLevel']);
    return ClassResource(
      key: _str(json['key']),
      name: _str(json['name'], _str(json['key'])),
      max: _str(json['max']),
      maxByLevel: byLevel == null
          ? null
          : {
              for (final e in byLevel.entries)
                if ((int.tryParse(e.key), _int(e.value)) case (final int level, final int v))
                  level: v,
            },
      recharge: _str(json['recharge']),
    );
  }

  final String key;
  final String name;

  /// Formula of the maximum when it is not a table ("proficiencyBonus",
  /// "mod:wis"...).
  final String max;

  /// Maximum by class level when it comes from the level table.
  final Map<int, int>? maxByLevel;

  /// "ShortRest", "LongRest"... as the server spells it.
  final String recharge;
}

class ClassDetail extends ClassSummary {
  const ClassDetail({
    required super.index,
    required super.name,
    super.hitDie,
    super.isSpellcaster,
    super.spellcastingAbility,
    super.source,
    this.description = const [],
    this.subclassLevel = 0,
    this.spellcasting,
    this.multiclassing,
    this.resources = const [],
    this.spellcastingLevel = 0,
    this.savingThrows = const [],
    this.proficiencies = const [],
    this.subclassFlavor,
    this.startingEquipmentText,
    this.startingEquipment,
    this.skillChoices = const SkillChoices(),
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
      source: summary.source,
      description: _strList(json['description']),
      subclassLevel: _int(json['subclassLevel']) ?? 0,
      spellcasting: _map(json['spellcasting']).let(ClassSpellcasting.fromJson),
      multiclassing: _map(json['multiclassing']).let(ClassMulticlassing.fromJson),
      resources: _objects(json['resources'], ClassResource.fromJson),
      spellcastingLevel: _int(json['spellcastingLevel']) ?? 0,
      savingThrows: _nameList(json['savingThrows']),
      proficiencies: _nameList(json['proficiencyNames'] ?? json['proficiencies']),
      subclassFlavor: _strOrNull(json['subclassFlavor']),
      startingEquipmentText: _strOrNull(json['startingEquipmentText']),
      startingEquipment: _map(json['startingEquipment']).let(StartingEquipment.fromJson),
      skillChoices: _map(json['skillChoices']).let(SkillChoices.fromJson) ?? const SkillChoices(),
      levels: levels,
      subclasses: _objects(json['subclasses'], Subclass.fromJson),
      features: features,
    );
  }

  final int spellcastingLevel;

  /// Description paragraphs (classes of content packs; empty for the SRD).
  final List<String> description;

  /// Level at which the subclass is chosen (0 when the class does not say).
  final int subclassLevel;

  /// Spellcasting of a class of a content pack (null for the SRD classes).
  final ClassSpellcasting? spellcasting;

  /// Multiclassing of a class of a content pack (null for the SRD classes).
  final ClassMulticlassing? multiclassing;

  /// Resources of a class of a content pack (bombs, grit...).
  final List<ClassResource> resources;

  /// Ability slugs ("str", "con", ...).
  final List<String> savingThrows;
  final List<String> proficiencies;
  final String? subclassFlavor;
  final String? startingEquipmentText;

  /// Structured equipment; null when the server has none (use the text).
  final StartingEquipment? startingEquipment;
  final SkillChoices skillChoices;
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
  const RaceSummary({
    required this.index,
    required this.name,
    this.speed,
    this.size,
    this.abilityBonuses = const [],
    this.subraceIndexes = const [],
    this.source,
  });

  factory RaceSummary.fromJson(Map<String, dynamic> json) => RaceSummary(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    speed: _int(json['speed']),
    size: _strOrNull(json['size']),
    abilityBonuses: _objects(json['abilityBonuses'], AbilityBonus.fromJson),
    subraceIndexes: _nameList(json['subraceIndexes']),
    source: _strOrNull(json['source']),
  );

  final String index;
  final String name;

  /// "srd", "homebrew" or the id of a content pack; null when not sent.
  final String? source;
  final int? speed;
  final String? size;
  final List<AbilityBonus> abilityBonuses;
  final List<String> subraceIndexes;
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

/// Random height and weight table of a race or subrace (`HeightWeightDto`,
/// PHB chapter 4): height = base + height roll (inches); weight = base +
/// height roll × weight roll (pounds). The modifiers are dice expressions
/// ("2d10") or a whole number ("1"). No mechanical effect.
class HeightWeightTable {
  const HeightWeightTable({
    required this.baseHeightInches,
    required this.heightModifier,
    required this.baseWeightPounds,
    required this.weightModifier,
  });

  /// Null when [json] is missing or incomplete.
  static HeightWeightTable? fromJson(Object? json) {
    final map = _map(json);
    if (map == null) return null;
    final baseHeight = _int(map['baseHeightInches']);
    final baseWeight = _int(map['baseWeightPounds']);
    final heightModifier = _strOrNull(map['heightModifier']);
    final weightModifier = _strOrNull(map['weightModifier']);
    if (baseHeight == null ||
        baseWeight == null ||
        heightModifier == null ||
        weightModifier == null) {
      return null;
    }
    return HeightWeightTable(
      baseHeightInches: baseHeight,
      heightModifier: heightModifier,
      baseWeightPounds: baseWeight,
      weightModifier: weightModifier,
    );
  }

  final int baseHeightInches;
  final String heightModifier;
  final int baseWeightPounds;
  final String weightModifier;
}

class Subrace {
  const Subrace({
    required this.index,
    required this.name,
    this.description = const [],
    this.abilityBonuses = const [],
    this.traits = const [],
    this.choices = const OriginChoiceSpec(),
    this.resistances = const [],
    this.languages = const [],
    this.heightWeight,
  });

  factory Subrace.fromJson(Map<String, dynamic> json) => Subrace(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    description: _strList(json['description']),
    abilityBonuses: _objects(json['abilityBonuses'], AbilityBonus.fromJson),
    traits: _objects(json['traits'], Trait.fromJson),
    choices: OriginChoiceSpec.fromJson(json['choices']),
    resistances: _strList(json['resistances']),
    languages: _nameList(json['languages']),
    heightWeight: HeightWeightTable.fromJson(json['heightWeight']),
  );

  final String index;
  final String name;
  final List<String> description;
  final List<AbilityBonus> abilityBonuses;
  final List<Trait> traits;

  /// Languages the subrace adds to those of the race (none in the SRD).
  final List<String> languages;

  /// Decisions the subrace asks for at creation (phase 19).
  final OriginChoiceSpec choices;

  /// Damage types the subrace always resists.
  final List<String> resistances;

  /// The subrace's own height and weight table (it replaces the race's), or null.
  final HeightWeightTable? heightWeight;
}

/// Which decisions a race, subrace or background asks for at creation
/// (`RaceChoicesDto`). The wizard only needs to know whether there is
/// something to ask, plus the language picks (asked in its own step); the other
/// options come from `GET /characters/{id}/origin-choices`.
class OriginChoiceSpec {
  const OriginChoiceSpec({this.kinds = const {}, this.languages});

  factory OriginChoiceSpec.fromJson(Object? json) {
    final map = _map(json);
    if (map == null) return const OriginChoiceSpec();
    return OriginChoiceSpec(
      kinds: {
        for (final key in const [
          'abilityBonuses',
          'skills',
          'languages',
          'tools',
          'cantrip',
          'feats',
        ])
          if (map[key] != null) key,
        if ((map['traitOptions'] as List? ?? const []).isNotEmpty) 'traitOptions',
      },
      languages: _map(map['languages']).let(LanguagePick.fromJson),
    );
  }

  /// Names of the parts present (`abilityBonuses`, `skills`, `languages`,
  /// `tools`, `cantrip`, `feats`, `traitOptions`).
  final Set<String> kinds;

  /// Languages to choose, or null when none are offered.
  final LanguagePick? languages;

  bool get isEmpty => kinds.isEmpty;

  /// True when there is a decision other than languages (the wizard asks for
  /// languages in their own step).
  bool get asksBesidesLanguages => kinds.any((k) => k != 'languages');
}

/// `choices.languages`: [choose] languages, from [from] (empty = any).
class LanguagePick {
  const LanguagePick({required this.choose, this.from = const []});

  factory LanguagePick.fromJson(Map<String, dynamic> json) => LanguagePick(
    choose: _int(json['choose']) ?? 0,
    from: json['from'] is List
        ? [
            for (final e in json['from'] as List)
              if (e is Map) _str(e['index'] ?? e['name']) else if (e != null) _str(e),
          ]
        : const [],
  );

  final int choose;

  /// Allowed language names (SRD spelling); empty for any language.
  final List<String> from;
}

class RaceDetail extends RaceSummary {
  const RaceDetail({
    required super.index,
    required super.name,
    super.speed,
    super.size,
    super.abilityBonuses,
    super.subraceIndexes,
    super.source,
    this.traits = const [],
    this.languages = const [],
    this.age,
    this.alignment,
    this.sizeDescription,
    this.subraces = const [],
    this.choices = const OriginChoiceSpec(),
    this.resistances = const [],
    this.heightWeight,
  });

  factory RaceDetail.fromJson(Map<String, dynamic> json) {
    final summary = RaceSummary.fromJson(json);
    return RaceDetail(
      index: summary.index,
      name: summary.name,
      speed: summary.speed,
      size: summary.size,
      abilityBonuses: _objects(json['abilityBonuses'], AbilityBonus.fromJson),
      source: summary.source,
      traits: _objects(json['traits'], Trait.fromJson),
      languages: _nameList(json['languages']),
      age: _strOrNull(json['age']),
      alignment: _strOrNull(json['alignment']),
      sizeDescription: _strOrNull(json['sizeDescription']),
      subraces: _objects(json['subraces'], Subrace.fromJson),
      choices: OriginChoiceSpec.fromJson(json['choices']),
      resistances: _strList(json['resistances']),
      heightWeight: HeightWeightTable.fromJson(json['heightWeight']),
    );
  }

  final List<Trait> traits;
  final List<String> languages;
  final String? age;
  final String? alignment;
  final String? sizeDescription;
  final List<Subrace> subraces;

  /// Decisions the race asks for at creation (phase 19); subraces carry their own.
  final OriginChoiceSpec choices;

  /// Damage types the race always resists.
  final List<String> resistances;

  /// Height and weight table of the race (null when none, as in the SRD).
  final HeightWeightTable? heightWeight;

  /// The table that applies to [subraceIndex]: the subrace's own, or the race's.
  HeightWeightTable? heightWeightFor(String? subraceIndex) {
    for (final subrace in subraces) {
      if (subrace.index == subraceIndex && subrace.heightWeight != null) {
        return subrace.heightWeight;
      }
    }
    return heightWeight;
  }
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
    this.source,
    this.category,
    this.expandedBy = const [],
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
    source: _strOrNull(json['source']),
    category: _strOrNull(json['category']),
    expandedBy: [
      for (final e in json['expandedBy'] is List ? json['expandedBy'] as List : const [])
        if (e is Map<String, dynamic>) SpellExpansion.fromJson(e),
    ],
  );

  final String index;
  final String name;

  /// `SpellCategory` name (Healing, Damage, Control, Buff, Defense, Utility,
  /// Summoning); null when not sent.
  final String? category;

  /// "srd", "homebrew" or the id of a content pack; null when not sent.
  final String? source;

  /// 0 for cantrips, 1..9 otherwise.
  final int level;
  final String? school;
  final String? castingTime;
  final bool concentration;
  final bool ritual;

  /// Display names of the classes that can cast the spell.
  final List<String> classes;

  /// Subclasses whose expanded spell list (content packs) adds the spell to
  /// their class's list.
  final List<SpellExpansion> expandedBy;
}

/// A subclass whose expanded spell list adds a spell to its class's list for
/// the characters with that subclass (`expandedBy` of the catalog spells).
class SpellExpansion {
  const SpellExpansion({
    required this.subclassIndex,
    required this.subclassName,
    required this.classIndex,
  });

  factory SpellExpansion.fromJson(Map<String, dynamic> json) => SpellExpansion(
    subclassIndex: _str(json['subclassIndex']),
    subclassName: _str(json['subclassName'], _str(json['subclassIndex'])),
    classIndex: _str(json['classIndex']),
  );

  final String subclassIndex;
  final String subclassName;
  final String classIndex;

  /// "Lista ampliada: Archfey".
  String get label => 'Lista ampliada: $subclassName';
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

/// `{"3": "8d6"}` (JSON keys are strings) as `{3: "8d6"}`; empty when absent.
Map<int, String> _levelMap(Object? value) {
  if (value is! Map) return const {};
  final map = <int, String>{};
  for (final e in value.entries) {
    final level = int.tryParse('${e.key}');
    final dice = '${e.value}'.trim();
    if (level != null && dice.isNotEmpty) map[level] = dice;
  }
  return map;
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
    super.source,
    super.category,
    super.expandedBy,
    this.range,
    this.components = const [],
    this.material,
    this.duration,
    this.description = const [],
    this.higherLevel = const [],
    this.attackType,
    this.damage,
    this.damageType,
    this.damageAtSlotLevel = const {},
    this.damageAtCharacterLevel = const {},
    this.healAtSlotLevel = const {},
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
      source: summary.source,
      category: summary.category,
      expandedBy: summary.expandedBy,
      range: _strOrNull(json['range']),
      components: _strList(json['components']),
      material: _strOrNull(json['material']),
      duration: _strOrNull(json['duration']),
      description: _strList(json['description']),
      higherLevel: _strList(json['higherLevel']),
      attackType: _strOrNull(json['attackType']),
      damage: damageText(json['damage']),
      damageType: json['damage'] is Map ? _strOrNull(_nameOf(json['damage']['type'])) : null,
      damageAtSlotLevel: _levelMap(_map(json['damage'])?['atSlotLevel']),
      damageAtCharacterLevel: _levelMap(_map(json['damage'])?['atCharacterLevel']),
      healAtSlotLevel: _levelMap(json['healAtSlotLevel']),
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

  /// Damage type(s) as sent ("Fire", "Acid + Fire"); null without damage.
  final String? damageType;

  /// Damage dice by slot level (`damage.atSlotLevel`), e.g. `{3: "8d6", 4: "9d6"}`.
  final Map<int, String> damageAtSlotLevel;

  /// Damage dice of a cantrip by character level (`damage.atCharacterLevel`).
  final Map<int, String> damageAtCharacterLevel;

  /// Healing by slot level (`healAtSlotLevel`); `MOD` stands for the
  /// spellcasting ability modifier ("1d8 + MOD").
  final Map<int, String> healAtSlotLevel;

  final String? dcAbility;
}

// ---------------------------------------------------------------------------
// Items
// ---------------------------------------------------------------------------

/// The D&D 5e fields of a catalog [ItemSummary], read from its raw JSON.
extension Dnd5eItemSummary on ItemSummary {
  String? get rarity => _strOrNull(raw['rarity']);
  bool get requiresAttunement => _bool(raw['requiresAttunement']);
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
    this.rarity,
    this.requiresAttunement = false,
    super.costCp,
    super.weightLb,
    super.source,
    super.raw,
    this.damage,
    this.armor,
    this.rangeNormal,
    this.rangeLong,
    this.properties = const [],
    this.description = const [],
    this.effects = const [],
    this.modifiers = const [],
    this.systemData = const {},
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
      rarity: _strOrNull(json['rarity']),
      requiresAttunement: _bool(json['requiresAttunement']),
      costCp: summary.costCp,
      weightLb: summary.weightLb,
      source: summary.source,
      raw: json,
      damage: damage,
      armor: armor,
      rangeNormal: _int(rangeJson?['normal'] ?? json['rangeNormal']),
      rangeLong: _int(rangeJson?['long'] ?? json['rangeLong']),
      properties: _nameList(json['properties']),
      description: _strList(json['description']),
      effects: _strList(json['effects']),
      modifiers: ItemModifier.listFromJson(json['modifiers']),
      systemData: _map(json['systemData']) ?? const {},
    );
  }

  final String? rarity;

  /// Rules data of the item without a column of its own (`systemData`):
  /// `special`, `tool`, `ammunition` and `firearm: { reload, misfire }`.
  final Map<String, dynamic> systemData;

  /// Text of the special property of a weapon of a content pack.
  String? get special => _strOrNull(systemData['special']);

  bool get isTool => _bool(systemData['tool']);

  bool get isAmmunition => _bool(systemData['ammunition']);

  /// Shots between reloads of a firearm (null: not a firearm or no reload).
  int? get firearmReload => _int(_map(systemData['firearm'])?['reload']);

  /// Highest d20 result that jams a firearm (null: not a firearm).
  int? get firearmMisfire => _int(_map(systemData['firearm'])?['misfire']);

  final bool requiresAttunement;

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
  const Condition({
    required this.index,
    required this.name,
    this.description = const [],
    this.source,
  });

  factory Condition.fromJson(Map<String, dynamic> json) => Condition(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    description: _strList(json['description']),
    source: _strOrNull(json['source']),
  );

  final String index;
  final String name;
  final List<String> description;

  /// "srd" or the content pack of the condition; null when not sent.
  final String? source;
}

/// A rules document of a content pack in the list (`RuleSummaryDto`).
class RuleSummary {
  const RuleSummary({
    required this.index,
    required this.title,
    this.category = '',
    this.tags = const [],
    this.source,
  });

  factory RuleSummary.fromJson(Map<String, dynamic> json) => RuleSummary(
    index: _str(json['index']),
    title: _str(json['title'], _str(json['index'])),
    category: _str(json['category']),
    tags: _strList(json['tags']),
    source: _strOrNull(json['source']),
  );

  final String index;
  final String title;

  /// "variant", "multiclassing", "equipment", "general"...
  final String category;
  final List<String> tags;
  final String? source;
}

/// A rules document with its body (`RuleDto`): paragraphs in light Markdown.
class Rule extends RuleSummary {
  const Rule({
    required super.index,
    required super.title,
    super.category,
    super.tags,
    super.source,
    this.body = const [],
  });

  factory Rule.fromJson(Map<String, dynamic> json) {
    final summary = RuleSummary.fromJson(json);
    return Rule(
      index: summary.index,
      title: summary.title,
      category: summary.category,
      tags: summary.tags,
      source: summary.source,
      body: _strList(json['body']),
    );
  }

  final List<String> body;
}

/// Kinds of vocabulary of `GET /catalog/reference/{kind}`.
abstract final class ReferenceKinds {
  static const languages = 'languages';
  static const weaponProperties = 'weaponProperties';
  static const equipmentCategories = 'equipmentCategories';
  static const damageTypes = 'damageTypes';
  static const magicSchools = 'magicSchools';
  static const tools = 'tools';
}

/// An entry of a vocabulary of the catalog (`ReferenceEntryDto`): a
/// language, weapon property, equipment category, damage type, magic school
/// or tool, from the SRD or a content pack.
class ReferenceEntry {
  const ReferenceEntry({
    required this.kind,
    required this.index,
    required this.name,
    this.description = const [],
    this.source,
  });

  factory ReferenceEntry.fromJson(Map<String, dynamic> json) => ReferenceEntry(
    kind: _str(json['kind']),
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    description: _strList(json['description']),
    source: _strOrNull(json['source']),
  );

  final String kind;
  final String index;
  final String name;
  final List<String> description;
  final String? source;
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
    this.startingEquipment,
    this.source,
    this.choices = const OriginChoiceSpec(),
    this.personality,
    this.optionalTables = const [],
  });

  factory Background.fromJson(Map<String, dynamic> json) => Background(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    featureName: _strOrNull(json['featureName']),
    featureDescription: _strList(json['featureDescription']),
    skillProficiencies: _nameList(json['skillProficiencies']),
    startingEquipmentText: _strOrNull(json['startingEquipmentText']),
    startingEquipment: _map(json['startingEquipment']).let(StartingEquipment.fromJson),
    source: _strOrNull(json['source']),
    choices: OriginChoiceSpec.fromJson(json['choices']),
    personality: _map(json['personality']).let(BackgroundPersonality.fromJson),
    optionalTables: _objects(json['optionalTables'], BackgroundTable.fromJson),
  );

  final String index;
  final String name;

  /// "srd", "homebrew" or the id of a content pack; null when not sent.
  final String? source;

  /// Decisions the background asks for at creation (phase 19).
  final OriginChoiceSpec choices;

  /// Tables of traits, ideals, bonds and flaws (phase 22); null without them.
  final BackgroundPersonality? personality;

  /// Optional tables (specialty, scheme…); the player keeps one entry.
  final List<BackgroundTable> optionalTables;
  final String? featureName;
  final List<String> featureDescription;
  final List<String> skillProficiencies;
  final String? startingEquipmentText;
  final StartingEquipment? startingEquipment;
}

/// An ideal of a background table with the alignment it points to.
class BackgroundIdeal {
  const BackgroundIdeal({required this.text, this.alignment});

  factory BackgroundIdeal.fromJson(Map<String, dynamic> json) =>
      BackgroundIdeal(text: _str(json['text']), alignment: _strOrNull(json['alignment']));

  final String text;

  /// "Lawful", "Any"… (SRD) or free text of a content pack; null without one.
  final String? alignment;
}

/// Personality tables of a background: the die of each one is the number of
/// its entries (d8 for eight traits).
class BackgroundPersonality {
  const BackgroundPersonality({
    this.traits = const [],
    this.ideals = const [],
    this.bonds = const [],
    this.flaws = const [],
  });

  factory BackgroundPersonality.fromJson(Map<String, dynamic> json) => BackgroundPersonality(
    traits: _strList(json['traits']),
    ideals: _objects(json['ideals'], BackgroundIdeal.fromJson),
    bonds: _strList(json['bonds']),
    flaws: _strList(json['flaws']),
  );

  final List<String> traits;
  final List<BackgroundIdeal> ideals;
  final List<String> bonds;
  final List<String> flaws;
}

/// Optional table of a background ("Especialidad"); one entry is kept.
class BackgroundTable {
  const BackgroundTable({required this.key, required this.name, this.entries = const []});

  factory BackgroundTable.fromJson(Map<String, dynamic> json) => BackgroundTable(
    key: _str(json['key']),
    name: _str(json['name'], _str(json['key'])),
    entries: _strList(json['entries']),
  );

  final String key;
  final String name;
  final List<String> entries;
}

// ---------------------------------------------------------------------------
// Structured starting equipment
// ---------------------------------------------------------------------------

/// An item of starting equipment. [templateId] is null when the item does not
/// resolve against the catalog (it cannot be added to the inventory).
class StartingItem {
  const StartingItem({
    required this.item,
    this.templateId,
    required this.name,
    this.quantity = 1,
    this.contents,
  });

  factory StartingItem.fromJson(Map<String, dynamic> json) {
    final contents = _objects(json['contents'], StartingItem.fromJson);
    return StartingItem(
      item: _str(json['item']),
      templateId: _strOrNull(json['templateId']),
      name: _str(json['name'], _str(json['item'])),
      quantity: _int(json['quantity']) ?? 1,
      contents: contents.isEmpty ? null : contents,
    );
  }

  final String item;
  final String? templateId;
  final String name;
  final int quantity;

  /// What an equipment pack contains, when known.
  final List<StartingItem>? contents;
}

/// "Any martial weapon": [choose] items of the equipment category [category].
class StartingCategoryPick {
  const StartingCategoryPick({required this.category, required this.name, this.choose = 1});

  factory StartingCategoryPick.fromJson(Map<String, dynamic> json) => StartingCategoryPick(
    category: _str(json['category']),
    name: _str(json['name'], _str(json['category'])),
    choose: _int(json['choose']) ?? 1,
  );

  final String category;
  final String name;
  final int choose;
}

class StartingEquipmentOption {
  const StartingEquipmentOption({
    required this.label,
    this.items = const [],
    this.categories = const [],
  });

  factory StartingEquipmentOption.fromJson(Map<String, dynamic> json) => StartingEquipmentOption(
    label: _str(json['label']),
    items: _objects(json['items'], StartingItem.fromJson),
    categories: _objects(json['categories'], StartingCategoryPick.fromJson),
  );

  final String label;
  final List<StartingItem> items;
  final List<StartingCategoryPick> categories;
}

/// Pick [choose] of [options].
class StartingEquipmentChoice {
  const StartingEquipmentChoice({
    required this.description,
    this.choose = 1,
    this.options = const [],
  });

  factory StartingEquipmentChoice.fromJson(Map<String, dynamic> json) => StartingEquipmentChoice(
    description: _str(json['description']),
    choose: _int(json['choose']) ?? 1,
    options: _objects(json['options'], StartingEquipmentOption.fromJson),
  );

  final String description;
  final int choose;
  final List<StartingEquipmentOption> options;
}

/// Alternative starting wealth: roll [dice] ("5d4") and multiply by [multiplier] gp.
class StartingGold {
  const StartingGold({required this.dice, this.multiplier = 1});

  factory StartingGold.fromJson(Map<String, dynamic> json) =>
      StartingGold(dice: _str(json['dice']), multiplier: _int(json['multiplier']) ?? 1);

  final String dice;
  final int multiplier;

  /// Number of dice and sides of [dice], or null when it is not "NdS".
  ({int count, int sides})? get parsed {
    final m = RegExp(r'^\s*(\d+)\s*d\s*(\d+)\s*$', caseSensitive: false).firstMatch(dice);
    if (m == null) return null;
    final count = int.parse(m.group(1)!);
    final sides = int.parse(m.group(2)!);
    return count > 0 && sides > 0 ? (count: count, sides: sides) : null;
  }
}

class StartingEquipment {
  const StartingEquipment({
    this.fixed = const [],
    this.choices = const [],
    this.gold,
    this.fixedGoldCp,
  });

  factory StartingEquipment.fromJson(Map<String, dynamic> json) => StartingEquipment(
    fixed: _objects(json['fixed'], StartingItem.fromJson),
    choices: _objects(json['choices'], StartingEquipmentChoice.fromJson),
    gold: _map(json['gold']).let(StartingGold.fromJson),
    fixedGoldCp: _int(json['fixedGoldCp']),
  );

  final List<StartingItem> fixed;
  final List<StartingEquipmentChoice> choices;
  final StartingGold? gold;
  final int? fixedGoldCp;
}

class EquipmentCategoryItem {
  const EquipmentCategoryItem({required this.templateId, required this.index, required this.name});

  factory EquipmentCategoryItem.fromJson(Map<String, dynamic> json) => EquipmentCategoryItem(
    templateId: _str(json['templateId']),
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
  );

  final String templateId;
  final String index;
  final String name;
}

/// `GET /catalog/equipment-categories/{index}`.
class EquipmentCategory {
  const EquipmentCategory({required this.index, required this.name, this.items = const []});

  factory EquipmentCategory.fromJson(Map<String, dynamic> json) => EquipmentCategory(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    items: _objects(json['items'], EquipmentCategoryItem.fromJson),
  );

  final String index;
  final String name;
  final List<EquipmentCategoryItem> items;
}

// ---------------------------------------------------------------------------
// Trinkets
// ---------------------------------------------------------------------------

/// One result of the d100 trinket table (`GET /catalog/trinkets`); the table
/// only exists when a content pack defines it.
class Trinket {
  const Trinket({
    required this.roll,
    required this.templateId,
    required this.index,
    required this.name,
    this.description = '',
  });

  factory Trinket.fromJson(Map<String, dynamic> json) => Trinket(
    roll: _int(json['roll']) ?? 0,
    templateId: _str(json['templateId']),
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    description: _str(json['description']),
  );

  final int roll;
  final String templateId;
  final String index;
  final String name;
  final String description;
}

// ---------------------------------------------------------------------------
// Roll tables (phase 22)
// ---------------------------------------------------------------------------

/// One range of a [RollTable]: the results [from]..[to] give [text].
class RollTableEntry {
  const RollTableEntry({required this.from, required this.to, required this.text});

  factory RollTableEntry.fromJson(Map<String, dynamic> json) {
    final from = _int(json['from']) ?? 0;
    return RollTableEntry(from: from, to: _int(json['to']) ?? from, text: _str(json['text']));
  }

  final int from;
  final int to;
  final String text;

  /// "7" or "7–12".
  String get range => from == to ? '$from' : '$from–$to';
}

/// Generic roll table of the content packs (the Wild Magic Surge of a
/// sorcerer subclass, d100…).
class RollTable {
  const RollTable({
    required this.key,
    required this.name,
    required this.dice,
    this.classIndex,
    this.subclassIndex,
    this.source,
    this.entries = const [],
  });

  factory RollTable.fromJson(Map<String, dynamic> json) => RollTable(
    key: _str(json['key']),
    name: _str(json['name'], _str(json['key'])),
    dice: _str(json['dice'], 'd100'),
    classIndex: _strOrNull(json['classIndex']),
    subclassIndex: _strOrNull(json['subclassIndex']),
    source: _strOrNull(json['source']),
    entries: _objects(json['entries'], RollTableEntry.fromJson),
  );

  final String key;
  final String name;

  /// "d100", "d20"…
  final String dice;
  final String? classIndex;
  final String? subclassIndex;
  final String? source;
  final List<RollTableEntry> entries;

  /// Faces of [dice] ("d100" → 100); 0 when it cannot be read.
  int get faces => int.tryParse(dice.toLowerCase().replaceFirst('d', '')) ?? 0;

  /// Reads a typed result: a number 1..[faces]; on a d100 "00" (and "0") is 100.
  /// Null when it is not a valid result.
  int? parseRoll(String text) {
    final trimmed = text.trim();
    if (trimmed.isEmpty || int.tryParse(trimmed) == null) return null;
    var value = int.parse(trimmed);
    if (faces == 100 && value == 0) value = 100;
    return value >= 1 && value <= faces ? value : null;
  }

  /// Entry for the result [roll], or null.
  RollTableEntry? entryFor(int roll) =>
      entries.where((e) => roll >= e.from && roll <= e.to).firstOrNull;
}
