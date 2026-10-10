// D&D 5e models of the characters API (phase 4): the sheet the server sends
// next to the core fields of a character (`CharacterDetail.raw`), the sheet
// and combat requests, level-up, spell preparation and origin choices.
// Parsers are tolerant: missing fields fall back to neutral values so a
// slightly different server shape does not break the sheet.

import 'package:opentrpg_core/core/characters/models.dart';
import 'package:opentrpg_core/core/ui/breakdown.dart';

import 'data/companion_models.dart';

export 'package:opentrpg_core/core/characters/models.dart';
export 'package:opentrpg_core/core/ui/breakdown.dart' show BreakdownPart, ValueBreakdown;

export 'data/companion_models.dart';

/// Ability keys in display order.
const abilityKeys = <String>['str', 'dex', 'con', 'int', 'wis', 'cha'];

const _defaultBase = <String, int>{
  'str': 10,
  'dex': 10,
  'con': 10,
  'int': 10,
  'wis': 10,
  'cha': 10,
};

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

bool _bool(Object? value, [bool fallback = false]) {
  if (value is bool) return value;
  if (value == 'true') return true;
  if (value == 'false') return false;
  return fallback;
}

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) {
  if (value is! List) return const [];
  return [
    for (final e in value)
      if (e is Map) parse(Map<String, dynamic>.from(e)),
  ];
}

String _titleFromIndex(String index) =>
    index.isEmpty ? index : '${index[0].toUpperCase()}${index.substring(1)}';

T _enumFromApi<T extends Enum>(List<T> values, String Function(T) apiOf, Object? raw, T fallback) {
  final text = _str(raw).toLowerCase();
  for (final v in values) {
    if (apiOf(v).toLowerCase() == text) return v;
  }
  return fallback;
}

// ---------------------------------------------------------------------------
// Enums
// ---------------------------------------------------------------------------

enum HpMode {
  average('Average', 'Promedio'),
  manual('Manual', 'Manual');

  const HpMode(this.apiValue, this.label);

  final String apiValue;
  final String label;

  static HpMode fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, HpMode.average);
}

enum ProficiencyType {
  skill('Skill', 'Habilidad'),
  savingThrow('SavingThrow', 'Salvación'),
  armor('Armor', 'Armadura'),
  weapon('Weapon', 'Arma'),
  tool('Tool', 'Herramienta'),
  language('Language', 'Idioma');

  const ProficiencyType(this.apiValue, this.label);

  final String apiValue;
  final String label;

  static ProficiencyType fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, ProficiencyType.skill);
}

enum ProficiencySource {
  classSource('Class', 'Clase'),
  race('Race', 'Raza'),
  background('Background', 'Trasfondo'),
  manual('Manual', 'Manual');

  const ProficiencySource(this.apiValue, this.label);

  final String apiValue;
  final String label;

  static ProficiencySource fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, ProficiencySource.manual);
}

enum Recharge {
  shortRest('ShortRest', 'Descanso corto'),
  longRest('LongRest', 'Descanso largo'),
  dawn('Dawn', 'Amanecer'),
  manual('Manual', 'Manual');

  const Recharge(this.apiValue, this.label);

  final String apiValue;
  final String label;

  static Recharge fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, Recharge.manual);
}

// ---------------------------------------------------------------------------
// Summary
// ---------------------------------------------------------------------------

/// One class of a character: `{ classIndex, className, subclassName?, level }`
/// in the summary, plus `subclassIndex` in the detail.
class CharacterClass {
  const CharacterClass({
    required this.classIndex,
    required this.className,
    this.subclassIndex,
    this.subclassName,
    required this.level,
    this.catalogMissing = false,
  });

  factory CharacterClass.fromJson(Map<String, dynamic> json) {
    final classIndex = _str(json['classIndex']);
    return CharacterClass(
      classIndex: classIndex,
      className: _str(json['className'], _titleFromIndex(classIndex)),
      subclassIndex: _strOrNull(json['subclassIndex']),
      subclassName: _strOrNull(json['subclassName']),
      level: _int(json['level']) ?? 1,
      catalogMissing: _bool(json['catalogMissing']),
    );
  }

  final String classIndex;
  final String className;
  final String? subclassIndex;
  final String? subclassName;
  final int level;

  /// The class or subclass definition is gone from the catalog (a deleted content pack).
  final bool catalogMissing;

  /// "Wizard 3".
  String get label => '$className $level';
}

/// "Fighter 3 / Wizard 2"; empty when the character has no class yet.
String classesLabel(List<CharacterClass> classes) => classes.map((c) => c.label).join(' / ');

// ---------------------------------------------------------------------------
// Detail
// ---------------------------------------------------------------------------

class CharacterProficiency {
  const CharacterProficiency({
    required this.type,
    required this.key,
    this.expertise = false,
    this.source = ProficiencySource.manual,
  });

  factory CharacterProficiency.fromJson(Map<String, dynamic> json) => CharacterProficiency(
    type: ProficiencyType.fromApi(json['type']),
    key: _str(json['key']),
    expertise: _bool(json['expertise']),
    source: ProficiencySource.fromApi(json['source']),
  );

  final ProficiencyType type;

  /// Catalog index (skills, saving throws) or free text.
  final String key;
  final bool expertise;
  final ProficiencySource source;

  Map<String, dynamic> toPatchJson() => {'type': type.apiValue, 'key': key, 'expertise': expertise};
}

class CharacterSpell {
  const CharacterSpell({
    required this.spellIndex,
    required this.classIndex,
    this.isPrepared = false,
    this.alwaysPrepared = false,
    this.name,
    this.level,
    this.catalogMissing = false,
    this.category,
  });

  /// [name] and [level] are optional extras: when the server does not send them
  /// the UI resolves them from the catalog.
  factory CharacterSpell.fromJson(Map<String, dynamic> json) => CharacterSpell(
    spellIndex: _str(json['spellIndex']),
    classIndex: _str(json['classIndex']),
    isPrepared: _bool(json['isPrepared']),
    alwaysPrepared: _bool(json['alwaysPrepared']),
    name: _strOrNull(json['spellName'] ?? json['name']),
    level: _int(json['spellLevel'] ?? json['level']),
    catalogMissing: _bool(json['catalogMissing']),
    category: _strOrNull(json['category']),
  );

  final String spellIndex;
  final String classIndex;
  final bool isPrepared;
  final bool alwaysPrepared;
  final String? name;
  final int? level;

  /// `SpellCategory` name (Healing, Damage...); null when the spell is not in
  /// the catalog or the server did not send it.
  final String? category;

  /// The spell is gone from the catalog (a deleted content pack).
  final bool catalogMissing;

  Map<String, dynamic> toPatchJson() => {
    'spellIndex': spellIndex,
    'classIndex': classIndex,
    'isPrepared': isPrepared,
    'alwaysPrepared': alwaysPrepared,
  };
}

class CharacterOverride {
  const CharacterOverride({required this.field, required this.value, this.note});

  factory CharacterOverride.fromJson(Map<String, dynamic> json) => CharacterOverride(
    field: _str(json['field']),
    value: _int(json['value']) ?? 0,
    note: _strOrNull(json['note']),
  );

  final String field;
  final int value;
  final String? note;

  Map<String, dynamic> toPatchJson() => {'field': field, 'value': value, 'note': ?note};
}

class CharacterResource {
  const CharacterResource({
    required this.id,
    this.key,
    required this.name,
    required this.max,
    required this.used,
    required this.recharge,
    this.isAuto = false,
    this.rollOnRest,
    this.rolls = const [],
    this.rollsPending = false,
    this.dice,
    this.source,
    this.breakdown,
    this.options = const [],
  });

  factory CharacterResource.fromJson(Map<String, dynamic> json) {
    final roll = _map(json['rollOnRest']);
    return CharacterResource(
      id: _str(json['id']),
      key: _strOrNull(json['key']),
      name: _str(json['name']),
      max: _int(json['max']) ?? 0,
      used: _int(json['used']) ?? 0,
      recharge: Recharge.fromApi(json['recharge']),
      isAuto: _bool(json['isAuto']),
      rollOnRest: roll == null ? null : RollOnRest.fromJson(roll),
      rolls: [for (final r in (json['rolls'] as List? ?? const [])) ?_int(r)],
      rollsPending: _bool(json['rollsPending']),
      dice: _strOrNull(json['dice']),
      source: _strOrNull(json['source']),
      breakdown: ValueBreakdown.maybeFromJson(json['breakdown']),
      options: _objects(json['options'], CharacterOptionCost.fromJson),
    );
  }

  /// Chosen options that spend this resource ("Golpe sereno", 2): the
  /// "Usar" buttons of the Combat tab.
  final List<CharacterOptionCost> options;

  /// Die rolled with each use ("d8": tactics dice, Bardic Inspiration), or null.
  final String? dice;

  /// Feature or option that grants it, with its level ("Ventaja táctica (nivel 3)").
  final String? source;

  /// How [max] was obtained (pack resources), or null.
  final ValueBreakdown? breakdown;

  /// Dice the player rolls after a rest (Portent), or null.
  final RollOnRest? rollOnRest;

  /// Values rolled after the last rest.
  final List<int> rolls;

  /// The player still has to write the dice ([rollOnRest]).
  final bool rollsPending;

  final String id;
  final String? key;
  final String name;
  final int max;
  final int used;
  final Recharge recharge;
  final bool isAuto;
}

/// What using an option costs (`OptionCostDto`): [amount] uses of the
/// resource [resource] (its key), named [resourceName].
class OptionCost {
  const OptionCost({required this.resource, required this.resourceName, required this.amount});

  factory OptionCost.fromJson(Map<String, dynamic> json) {
    final resource = _str(json['resource']);
    return OptionCost(
      resource: resource,
      resourceName: _str(json['resourceName'], resource),
      amount: _int(json['amount']) ?? 1,
    );
  }

  final String resource;
  final String resourceName;
  final int amount;

  /// "2 Ki".
  String get label => '$amount $resourceName';
}

/// A chosen option with a cost (`CharacterOptionCostDto`).
class CharacterOptionCost {
  const CharacterOptionCost({
    required this.index,
    required this.name,
    required this.resource,
    required this.resourceName,
    required this.amount,
  });

  factory CharacterOptionCost.fromJson(Map<String, dynamic> json) {
    final index = _str(json['index']);
    final resource = _str(json['resource']);
    return CharacterOptionCost(
      index: index,
      name: _str(json['name'], index),
      resource: resource,
      resourceName: _str(json['resourceName'], resource),
      amount: _int(json['amount']) ?? 1,
    );
  }

  final String index;
  final String name;
  final String resource;
  final String resourceName;
  final int amount;

  /// "2 Ki".
  String get label => '$amount $resourceName';
}

/// Dice a resource asks the player to roll after a rest (`RollOnRestDto`):
/// [count] x [dice] ("d20") after a [rest] ("short" or "long") rest.
class RollOnRest {
  const RollOnRest({required this.dice, this.count = 1, this.rest = 'long'});

  factory RollOnRest.fromJson(Map<String, dynamic> json) => RollOnRest(
    dice: _str(json['dice'], 'd20'),
    count: _int(json['count']) ?? 1,
    rest: _str(json['rest'], 'long'),
  );

  final String dice;
  final int count;
  final String rest;

  /// Faces of [dice] ("d20" -> 20); 20 when it cannot be read.
  int get sides => int.tryParse(dice.toLowerCase().replaceAll('d', '')) ?? 20;
}

class SpellSlot {
  const SpellSlot({required this.level, required this.max, required this.used});

  factory SpellSlot.fromJson(Map<String, dynamic> json) => SpellSlot(
    level: _int(json['level']) ?? 0,
    max: _int(json['max']) ?? 0,
    used: _int(json['used']) ?? 0,
  );

  /// 1..9, or 0 for the Warlock's pact slots.
  final int level;
  final int max;
  final int used;
}

class CharacterCondition {
  const CharacterCondition({required this.index, this.note});

  factory CharacterCondition.fromJson(Map<String, dynamic> json) =>
      CharacterCondition(index: _str(json['index']), note: _strOrNull(json['note']));

  final String index;
  final String? note;

  Map<String, dynamic> toJson() => {'index': index, 'note': ?note};
}

// -- Sheet ------------------------------------------------------------------

class AbilityScore {
  const AbilityScore({required this.score, required this.modifier, this.overridden = false});

  factory AbilityScore.fromJson(Map<String, dynamic> json) => AbilityScore(
    score: _int(json['score']) ?? 10,
    modifier: _int(json['modifier']) ?? 0,
    overridden: _bool(json['overridden']),
  );

  final int score;
  final int modifier;
  final bool overridden;
}

class SavingThrow {
  const SavingThrow({required this.value, this.proficient = false});

  factory SavingThrow.fromJson(Map<String, dynamic> json) =>
      SavingThrow(value: _int(json['value']) ?? 0, proficient: _bool(json['proficient']));

  final int value;
  final bool proficient;
}

class SheetSkill {
  const SheetSkill({
    required this.index,
    required this.name,
    required this.ability,
    required this.value,
    this.proficient = false,
    this.expertise = false,
  });

  factory SheetSkill.fromJson(Map<String, dynamic> json) => SheetSkill(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    ability: _str(json['ability']),
    value: _int(json['value']) ?? 0,
    proficient: _bool(json['proficient']),
    expertise: _bool(json['expertise']),
  );

  final String index;
  final String name;

  /// Ability key ("str", "dex", ...).
  final String ability;
  final int value;
  final bool proficient;
  final bool expertise;
}

class HitDice {
  const HitDice({
    required this.classIndex,
    required this.die,
    required this.total,
    required this.remaining,
  });

  factory HitDice.fromJson(Map<String, dynamic> json) => HitDice(
    classIndex: _str(json['classIndex']),
    die: _int(json['die']) ?? 0,
    total: _int(json['total']) ?? 0,
    remaining: _int(json['remaining']) ?? 0,
  );

  final String classIndex;
  final int die;
  final int total;
  final int remaining;
}

class Spellcasting {
  const Spellcasting({
    required this.classIndex,
    required this.ability,
    required this.saveDc,
    required this.attackBonus,
    this.preparedMax,
    this.spellsKnownMax,
    this.cantripsKnownMax,
  });

  factory Spellcasting.fromJson(Map<String, dynamic> json) => Spellcasting(
    classIndex: _str(json['classIndex']),
    ability: _str(json['ability']),
    saveDc: _int(json['saveDc']) ?? 0,
    attackBonus: _int(json['attackBonus']) ?? 0,
    preparedMax: _int(json['preparedMax']),
    spellsKnownMax: _int(json['spellsKnownMax']),
    cantripsKnownMax: _int(json['cantripsKnownMax']),
  );

  final String classIndex;
  final String ability;
  final int saveDc;
  final int attackBonus;
  final int? preparedMax;

  /// Spells known allowed at the class level (classes that cast through a
  /// subclass of a content pack); null when the class has no fixed number.
  final int? spellsKnownMax;

  /// Cantrips known allowed at the class level, like [spellsKnownMax].
  final int? cantripsKnownMax;
}

/// An item modifier applied to the sheet (`ItemEffectDto`).
class ItemEffect {
  const ItemEffect({required this.itemName, required this.kind, this.target, this.value = 0});

  factory ItemEffect.fromJson(Map<String, dynamic> json) => ItemEffect(
    itemName: _str(json['itemName']),
    kind: _str(json['kind']),
    target: _strOrNull(json['target']),
    value: _int(json['value']) ?? 0,
  );

  final String itemName;
  final String kind;
  final String? target;
  final int value;
}

/// A resisted damage type (`ResistanceDto`): [source] is "race", "subrace" or
/// "background" and [label] what grants it.
class Resistance {
  const Resistance({required this.damageType, this.source = '', this.label = ''});

  factory Resistance.fromJson(Map<String, dynamic> json) => Resistance(
    damageType: _str(json['damageType']),
    source: _str(json['source']),
    label: _str(json['label']),
  );

  final String damageType;
  final String source;
  final String label;
}

/// Breath weapon of a draconic ancestry (`BreathWeaponValueDto`).
class BreathWeapon {
  const BreathWeapon({
    required this.name,
    this.source = '',
    this.damageType = '',
    this.dice = '',
    this.saveAbility = '',
    this.area = '',
    this.dc = 0,
  });

  factory BreathWeapon.fromJson(Map<String, dynamic> json) => BreathWeapon(
    name: _str(json['name']),
    source: _str(json['source']),
    damageType: _str(json['damageType']),
    dice: _str(json['dice']),
    saveAbility: _str(json['saveAbility']),
    area: _str(json['area']),
    dc: _int(json['dc']) ?? 0,
  );

  final String name;
  final String source;
  final String damageType;
  final String dice;
  final String saveAbility;
  final String area;
  final int dc;
}

/// The computed sheet (`CharacterSheetDto`). The client never recomputes it.
class CharacterSheet {
  const CharacterSheet({
    this.abilities = const {},
    this.proficiencyBonus = 2,
    this.savingThrows = const {},
    this.skills = const [],
    this.passivePerception = 10,
    this.initiative = 0,
    this.armorClass = 10,
    this.speed = 30,
    this.hitPointsMax = 0,
    this.hitDice = const [],
    this.spellcasting = const [],
    this.overriddenFields = const [],
    this.itemEffects = const [],
    this.breakdowns = const {},
    this.resistances = const [],
    this.breathWeapon,
  });

  factory CharacterSheet.fromJson(Map<String, dynamic> json) {
    final abilities = _map(json['abilities']) ?? const {};
    final saves = _map(json['savingThrows']) ?? const {};
    return CharacterSheet(
      abilities: {
        for (final e in abilities.entries)
          if (e.value is Map)
            e.key: AbilityScore.fromJson(Map<String, dynamic>.from(e.value as Map)),
      },
      proficiencyBonus: _int(json['proficiencyBonus']) ?? 2,
      savingThrows: {
        for (final e in saves.entries)
          if (e.value is Map)
            e.key: SavingThrow.fromJson(Map<String, dynamic>.from(e.value as Map)),
      },
      skills: _objects(json['skills'], SheetSkill.fromJson),
      passivePerception: _int(json['passivePerception']) ?? 10,
      initiative: _int(json['initiative']) ?? 0,
      armorClass: _int(json['armorClass']) ?? 10,
      speed: _int(json['speed']) ?? 30,
      hitPointsMax: _int(json['hitPointsMax']) ?? 0,
      hitDice: _objects(json['hitDice'], HitDice.fromJson),
      spellcasting: _objects(json['spellcasting'], Spellcasting.fromJson),
      overriddenFields: [for (final f in (json['overriddenFields'] as List? ?? const [])) _str(f)],
      itemEffects: _objects(json['itemEffects'], ItemEffect.fromJson),
      breakdowns: {
        for (final e in (_map(json['breakdowns']) ?? const {}).entries)
          if (e.value is Map) e.key: ValueBreakdown.fromJson(e.value),
      },
      resistances: _objects(json['resistances'], Resistance.fromJson),
      breathWeapon: _map(json['breathWeapon']) == null
          ? null
          : BreathWeapon.fromJson(_map(json['breathWeapon'])!),
    );
  }

  final Map<String, AbilityScore> abilities;
  final int proficiencyBonus;
  final Map<String, SavingThrow> savingThrows;
  final List<SheetSkill> skills;
  final int passivePerception;
  final int initiative;
  final int armorClass;
  final int speed;
  final int hitPointsMax;
  final List<HitDice> hitDice;
  final List<Spellcasting> spellcasting;

  /// Override field names such as `armorClass` or `ability.str`.
  final List<String> overriddenFields;

  /// Item modifiers currently applied to the sheet.
  final List<ItemEffect> itemEffects;

  /// How each value was obtained, by key: `ability.dex`, `save.wis`,
  /// `skill.stealth`, `armorClass`, `initiative`, `speed`, `hitPointsMax`,
  /// `passivePerception`, `proficiencyBonus`, `spellSaveDc.<class>`,
  /// `spellAttackBonus.<class>`. Empty when the server does not send them.
  final Map<String, ValueBreakdown> breakdowns;

  /// Damage types the character resists (race, subrace and chosen options).
  final List<Resistance> resistances;

  /// Breath weapon of a draconic ancestry (DC breakdown in
  /// `breakdowns['breathWeapon.dc']`), or null.
  final BreathWeapon? breathWeapon;

  ValueBreakdown? breakdown(String key) => breakdowns[key];

  AbilityScore ability(String key) => abilities[key] ?? const AbilityScore(score: 10, modifier: 0);

  bool isOverridden(String field) => overriddenFields.contains(field);
}

// -- Combat summary (phase 6) -------------------------------------------------

/// One entry of `combat.attacks` (`AttackDto`): a weapon or the unarmed strike
/// with its precomputed bonus and damage.
class CombatAttack {
  const CombatAttack({
    this.itemId,
    required this.name,
    this.attackBonus = 0,
    this.damage = '',
    this.damageType = '',
    this.versatileDamage,
    this.range,
    this.properties = const [],
    this.notes,
    this.attackBreakdown,
    this.damageBreakdown,
  });

  factory CombatAttack.fromJson(Map<String, dynamic> json) => CombatAttack(
    itemId: _strOrNull(json['itemId']),
    name: _str(json['name']),
    attackBonus: _int(json['attackBonus']) ?? 0,
    damage: _str(json['damage']),
    damageType: _str(json['damageType']),
    versatileDamage: _strOrNull(json['versatileDamage']),
    range: _strOrNull(json['range']),
    properties: [for (final p in (json['properties'] as List? ?? const [])) _str(p)],
    notes: _strOrNull(json['notes']),
    attackBreakdown: ValueBreakdown.maybeFromJson(json['attackBreakdown']),
    damageBreakdown: ValueBreakdown.maybeFromJson(json['damageBreakdown']),
  );

  final String? itemId;
  final String name;
  final int attackBonus;

  /// Dice with the damage bonus included: "1d8+3".
  final String damage;
  final String damageType;
  final String? versatileDamage;
  final String? range;
  final List<String> properties;
  final String? notes;

  /// How the attack bonus and the flat damage bonus were obtained; null when
  /// the server does not send them.
  final ValueBreakdown? attackBreakdown;
  final ValueBreakdown? damageBreakdown;

  bool hasProperty(String property) =>
      properties.any((p) => p.toLowerCase() == property.toLowerCase());

  /// Ammunition weapons and weapons with a range that are not thrown are
  /// ranged; the rest are melee (a rage damage bonus applies to those).
  bool get isRanged =>
      hasProperty('ammunition') || ((range?.contains('/') ?? false) && !hasProperty('thrown'));
}

/// One entry of `combat.quickConsumables`: a potion or similar item.
class QuickConsumable {
  const QuickConsumable({
    required this.itemId,
    required this.name,
    this.quantity = 1,
    this.charges,
  });

  factory QuickConsumable.fromJson(Map<String, dynamic> json) => QuickConsumable(
    itemId: _str(json['itemId']),
    name: _str(json['name']),
    quantity: _int(json['quantity']) ?? 1,
    charges: _int(json['charges']),
  );

  final String itemId;
  final String name;
  final int quantity;
  final int? charges;
}

/// One entry of `combat.classPanels`: per-class data for the combat view.
/// [data] depends on the class (see `docs/specs/fase-6-combate-dados.md`).
class ClassPanel {
  const ClassPanel({required this.classIndex, this.level = 1, this.data = const {}});

  factory ClassPanel.fromJson(Map<String, dynamic> json) => ClassPanel(
    classIndex: _str(json['classIndex']),
    level: _int(json['level']) ?? 1,
    data: _map(json['data']) ?? const {},
  );

  final String classIndex;
  final int level;
  final Map<String, dynamic> data;
}

/// A feature usable once between long rests (`combat.onceSinceLongRest`).
class OnceSinceLongRest {
  const OnceSinceLongRest({required this.key, required this.name, this.used = false});

  factory OnceSinceLongRest.fromJson(Map<String, dynamic> json) => OnceSinceLongRest(
    key: _str(json['key']),
    name: _str(json['name'], _str(json['key'])),
    used: _bool(json['used']),
  );

  final String key;
  final String name;
  final bool used;
}

/// The precomputed combat data of a character (`CombatSummaryDto`). Missing
/// from older servers, in which case every list is empty.
class CombatSummary {
  const CombatSummary({
    this.attacks = const [],
    this.spellSlots = const [],
    this.pactSlots,
    this.resources = const [],
    this.quickConsumables = const [],
    this.classPanels = const [],
    this.onceSinceLongRest = const [],
  });

  factory CombatSummary.fromJson(Object? raw) {
    final json = _map(raw);
    if (json == null) return const CombatSummary();
    final pact = _map(json['pactSlots']);
    return CombatSummary(
      attacks: _objects(json['attacks'], CombatAttack.fromJson),
      spellSlots: _objects(json['spellSlots'], SpellSlot.fromJson),
      pactSlots: pact == null ? null : SpellSlot.fromJson(pact),
      resources: _objects(json['resources'], CharacterResource.fromJson),
      quickConsumables: _objects(json['quickConsumables'], QuickConsumable.fromJson),
      classPanels: _objects(json['classPanels'], ClassPanel.fromJson),
      onceSinceLongRest: _objects(json['onceSinceLongRest'], OnceSinceLongRest.fromJson),
    );
  }

  final List<CombatAttack> attacks;
  final List<SpellSlot> spellSlots;

  /// Warlock pact slots (all of one level); null for other classes.
  final SpellSlot? pactSlots;
  final List<CharacterResource> resources;
  final List<QuickConsumable> quickConsumables;
  final List<ClassPanel> classPanels;
  final List<OnceSinceLongRest> onceSinceLongRest;

  /// The panel of [classIndex], or null.
  ClassPanel? panelOf(String classIndex) {
    for (final p in classPanels) {
      if (p.classIndex == classIndex) return p;
    }
    return null;
  }
}

/// Response of `divine-smite`: the updated character and the extra damage dice.
class DivineSmiteResult {
  const DivineSmiteResult({required this.character, required this.damageDice});

  final CharacterDetail character;

  /// "2d8".
  final String damageDice;
}

/// The D&D 5e part of a character detail: the sheet fields the server sends
/// next to the core ones in the same flat JSON (`CharacterDetail.raw`).
/// [fromDetail] builds it once per detail and caches it; the
/// [Dnd5eCharacterDetail] extension exposes its fields on [CharacterDetail].
class Dnd5eCharacter {
  const Dnd5eCharacter({
    this.raceIndex,
    this.raceName,
    this.subraceIndex,
    this.subraceName,
    this.backgroundIndex,
    this.backgroundName,
    this.alignment,
    this.applyRacialBonuses = true,
    this.hpMode = HpMode.average,
    this.baseAbilities = _defaultBase,
    this.hitPointsCurrent = 0,
    this.temporaryHitPoints = 0,
    this.deathSaveSuccesses = 0,
    this.deathSaveFailures = 0,
    this.exhaustionLevel = 0,
    this.conditions = const [],
    this.concentratingOnSpellIndex,
    this.inspiration = false,
    this.hitDiceUsed = const {},
    this.classes = const [],
    this.proficiencies = const [],
    this.spells = const [],
    this.overrides = const [],
    this.resources = const [],
    this.spellSlots = const [],
    this.combat = const CombatSummary(),
    this.sheet = const CharacterSheet(),
    this.pendingLevelUpTo,
    this.spellPreparationPending = false,
    this.spellPreparationReason,
    this.invalidChoices = const [],
    this.restRollsPending = false,
    this.choices = const [],
    this.feats = const [],
    this.optionCosts = const [],
    this.companion,
    this.companionFeature,
    this.companionPending = false,
    this.raceCatalogMissing = false,
    this.backgroundCatalogMissing = false,
    this.catalogMissing = false,
  });

  factory Dnd5eCharacter.fromJson(Map<String, dynamic> json) {
    // Base scores arrive as `baseStr`... (stored fields) or as `baseAbilities`.
    final nested = _map(json['baseAbilities']) ?? const {};
    int base(String key) => _int(json['base${_titleFromIndex(key)}']) ?? _int(nested[key]) ?? 10;
    final used = _map(json['hitDiceUsed']) ?? const {};
    return Dnd5eCharacter(
      raceIndex: _strOrNull(json['raceIndex']),
      raceName: _strOrNull(json['raceName']),
      subraceIndex: _strOrNull(json['subraceIndex']),
      subraceName: _strOrNull(json['subraceName']),
      backgroundIndex: _strOrNull(json['backgroundIndex']),
      backgroundName: _strOrNull(json['backgroundName']),
      alignment: _strOrNull(json['alignment']),
      applyRacialBonuses: _bool(json['applyRacialBonuses'], true),
      hpMode: HpMode.fromApi(json['hpMode']),
      baseAbilities: {for (final k in abilityKeys) k: base(k)},
      hitPointsCurrent: _int(json['hitPointsCurrent']) ?? 0,
      temporaryHitPoints: _int(json['temporaryHitPoints']) ?? 0,
      deathSaveSuccesses: _int(json['deathSaveSuccesses']) ?? 0,
      deathSaveFailures: _int(json['deathSaveFailures']) ?? 0,
      exhaustionLevel: _int(json['exhaustionLevel']) ?? 0,
      conditions: _objects(json['conditions'], CharacterCondition.fromJson),
      concentratingOnSpellIndex: _strOrNull(json['concentratingOnSpellIndex']),
      inspiration: _bool(json['inspiration']),
      hitDiceUsed: {for (final e in used.entries) e.key: _int(e.value) ?? 0},
      classes: _objects(json['classes'], CharacterClass.fromJson),
      proficiencies: _objects(json['proficiencies'], CharacterProficiency.fromJson),
      spells: _objects(json['spells'], CharacterSpell.fromJson),
      overrides: _objects(json['overrides'], CharacterOverride.fromJson),
      resources: _objects(json['resources'], CharacterResource.fromJson),
      spellSlots: _objects(json['spellSlots'], SpellSlot.fromJson),
      combat: CombatSummary.fromJson(json['combat']),
      sheet: CharacterSheet.fromJson(_map(json['sheet']) ?? const {}),
      pendingLevelUpTo: _int(json['pendingLevelUpTo']),
      spellPreparationPending: _bool(json['spellPreparationPending']),
      spellPreparationReason: SpellPreparationReason.fromApi(json['spellPreparationReason']),
      invalidChoices: _objects(json['invalidChoices'], InvalidChoice.fromJson),
      restRollsPending: _bool(json['restRollsPending']),
      choices: _objects(json['choices'], CharacterChoice.fromJson),
      feats: _objects(json['feats'], CharacterFeat.fromJson),
      optionCosts: _objects(json['optionCosts'], CharacterOptionCost.fromJson),
      companion: _map(json['companion']) == null
          ? null
          : CharacterCompanion.fromJson(_map(json['companion'])!),
      companionFeature: _map(json['companionFeature']) == null
          ? null
          : CompanionFeature.fromJson(_map(json['companionFeature'])!),
      companionPending: _bool(json['companionPending']),
      raceCatalogMissing: _bool(json['raceCatalogMissing']),
      backgroundCatalogMissing: _bool(json['backgroundCatalogMissing']),
      catalogMissing: _bool(json['catalogMissing']),
    );
  }

  static final _cache = Expando<Dnd5eCharacter>('dnd5e');

  /// The 5e sheet of [detail], parsed from its [CharacterDetail.raw] the
  /// first time and cached for that detail.
  static Dnd5eCharacter fromDetail(CharacterDetail detail) =>
      _cache[detail] ??= Dnd5eCharacter.fromJson(detail.raw);

  final String? raceIndex;
  final String? raceName;
  final String? subraceIndex;
  final String? subraceName;
  final String? backgroundIndex;
  final String? backgroundName;
  final String? alignment;
  final bool applyRacialBonuses;
  final HpMode hpMode;

  /// Base ability scores by key ("str", ...), before racial bonuses.
  final Map<String, int> baseAbilities;
  final int hitPointsCurrent;
  final int temporaryHitPoints;
  final int deathSaveSuccesses;
  final int deathSaveFailures;
  final int exhaustionLevel;
  final List<CharacterCondition> conditions;
  final String? concentratingOnSpellIndex;
  final bool inspiration;
  final Map<String, int> hitDiceUsed;
  final List<CharacterClass> classes;
  final List<CharacterProficiency> proficiencies;
  final List<CharacterSpell> spells;
  final List<CharacterOverride> overrides;
  final List<CharacterResource> resources;
  final List<SpellSlot> spellSlots;

  /// Precomputed combat data; empty when the server does not send it.
  final CombatSummary combat;
  final CharacterSheet sheet;

  /// Level a DM granted and the player has not taken yet, or null.
  final int? pendingLevelUpTo;

  /// The player must prepare spells before playing (after a long rest, a level
  /// or the first time). [spellPreparationReason] says why.
  final bool spellPreparationPending;
  final SpellPreparationReason? spellPreparationReason;

  /// Options and feats whose prerequisites no longer hold; the player must
  /// replace them (forced step of "Mi sesión").
  final List<InvalidChoice> invalidChoices;

  /// Some resource asks for its dice to be rolled after the last rest.
  final bool restRollsPending;

  /// Choices made when levelling up (subclass, fighting style, ASI...).
  final List<CharacterChoice> choices;

  /// Feats the character has, with their catalog text (`CharacterFeatDto`).
  final List<CharacterFeat> feats;

  /// Chosen options whose use spends a resource ("2 Ki").
  final List<CharacterOptionCost> optionCosts;

  /// The animal companion, or null (phase 25, block 6).
  final CharacterCompanion? companion;

  /// The companion feature the character has reached, or null.
  final CompanionFeature? companionFeature;

  /// The companion feature is reached and no beast is chosen yet.
  final bool companionPending;

  /// The race (or subrace) is gone from the catalog (a deleted content pack).
  final bool raceCatalogMissing;

  /// The background is gone from the catalog.
  final bool backgroundCatalogMissing;

  /// Some of the character's content is gone from the catalog; the specific
  /// flags above and on [classes] and [spells] say which.
  final bool catalogMissing;

  /// Cost of the chosen option [index], or null.
  CharacterOptionCost? optionCost(String index) {
    for (final cost in optionCosts) {
      if (cost.index == index) return cost;
    }
    return null;
  }

  /// What of the character is gone from the catalog, in Spanish ("clase o
  /// subclase", "raza", "conjuros", "trasfondo"); empty when nothing is. When
  /// the server only says [catalogMissing] the list is a generic "contenido".
  List<String> get missingContent {
    final missing = [
      if (classes.any((c) => c.catalogMissing)) 'clase o subclase',
      if (raceCatalogMissing) 'raza',
      if (spells.any((s) => s.catalogMissing)) 'conjuros',
      if (backgroundCatalogMissing) 'trasfondo',
    ];
    return missing.isEmpty && catalogMissing ? const ['contenido'] : missing;
  }

  int get totalLevel => classes.fold<int>(0, (sum, c) => sum + c.level);

  /// The override of [field], or null.
  CharacterOverride? overrideOf(String field) {
    for (final o in overrides) {
      if (o.field == field) return o;
    }
    return null;
  }
}

/// The D&D 5e sheet fields of a [CharacterDetail], read from its
/// [Dnd5eCharacter] (`detail.dnd5e`).
extension Dnd5eCharacterDetail on CharacterDetail {
  Dnd5eCharacter get dnd5e => Dnd5eCharacter.fromDetail(this);

  String? get raceIndex => dnd5e.raceIndex;
  String? get raceName => dnd5e.raceName;
  String? get subraceIndex => dnd5e.subraceIndex;
  String? get subraceName => dnd5e.subraceName;
  String? get backgroundIndex => dnd5e.backgroundIndex;
  String? get backgroundName => dnd5e.backgroundName;
  String? get alignment => dnd5e.alignment;
  bool get applyRacialBonuses => dnd5e.applyRacialBonuses;
  HpMode get hpMode => dnd5e.hpMode;
  Map<String, int> get baseAbilities => dnd5e.baseAbilities;
  int get hitPointsCurrent => dnd5e.hitPointsCurrent;
  int get temporaryHitPoints => dnd5e.temporaryHitPoints;
  int get deathSaveSuccesses => dnd5e.deathSaveSuccesses;
  int get deathSaveFailures => dnd5e.deathSaveFailures;
  int get exhaustionLevel => dnd5e.exhaustionLevel;
  List<CharacterCondition> get conditions => dnd5e.conditions;
  String? get concentratingOnSpellIndex => dnd5e.concentratingOnSpellIndex;
  bool get inspiration => dnd5e.inspiration;
  Map<String, int> get hitDiceUsed => dnd5e.hitDiceUsed;
  List<CharacterClass> get classes => dnd5e.classes;
  List<CharacterProficiency> get proficiencies => dnd5e.proficiencies;
  List<CharacterSpell> get spells => dnd5e.spells;
  List<CharacterOverride> get overrides => dnd5e.overrides;
  List<CharacterResource> get resources => dnd5e.resources;
  List<SpellSlot> get spellSlots => dnd5e.spellSlots;
  CombatSummary get combat => dnd5e.combat;
  CharacterSheet get sheet => dnd5e.sheet;
  int? get pendingLevelUpTo => dnd5e.pendingLevelUpTo;
  bool get spellPreparationPending => dnd5e.spellPreparationPending;
  SpellPreparationReason? get spellPreparationReason => dnd5e.spellPreparationReason;
  List<InvalidChoice> get invalidChoices => dnd5e.invalidChoices;
  bool get restRollsPending => dnd5e.restRollsPending;
  List<CharacterChoice> get choices => dnd5e.choices;
  List<CharacterFeat> get feats => dnd5e.feats;
  List<CharacterOptionCost> get optionCosts => dnd5e.optionCosts;
  CharacterCompanion? get companion => dnd5e.companion;
  CompanionFeature? get companionFeature => dnd5e.companionFeature;
  bool get companionPending => dnd5e.companionPending;
  bool get raceCatalogMissing => dnd5e.raceCatalogMissing;
  bool get backgroundCatalogMissing => dnd5e.backgroundCatalogMissing;
  bool get catalogMissing => dnd5e.catalogMissing;
  CharacterOptionCost? optionCost(String index) => dnd5e.optionCost(index);
  List<String> get missingContent => dnd5e.missingContent;
  int get totalLevel => dnd5e.totalLevel;
  CharacterOverride? overrideOf(String field) => dnd5e.overrideOf(field);
}

/// The D&D 5e roster line of a [CharacterSummary] (race, classes, level and
/// hit points), read from its [CharacterSummary.raw].
class Dnd5eRosterLine {
  const Dnd5eRosterLine({
    this.raceName,
    this.classes = const [],
    this.level = 0,
    this.hitPointsCurrent,
    this.hitPointsMax,
  });

  factory Dnd5eRosterLine.fromJson(Map<String, dynamic> json) {
    final classes = _objects(json['classes'], CharacterClass.fromJson);
    return Dnd5eRosterLine(
      raceName: _strOrNull(json['raceName']),
      classes: classes,
      level: _int(json['level']) ?? classes.fold<int>(0, (sum, c) => sum + c.level),
      hitPointsCurrent: _int(json['hitPointsCurrent']),
      hitPointsMax: _int(json['hitPointsMax']),
    );
  }

  static final _cache = Expando<Dnd5eRosterLine>('dnd5eRoster');

  /// The roster line of [summary], parsed once and cached.
  static Dnd5eRosterLine of(CharacterSummary summary) =>
      _cache[summary] ??= Dnd5eRosterLine.fromJson(summary.raw);

  final String? raceName;
  final List<CharacterClass> classes;
  final int level;

  /// Only sent to the owner and the DMs.
  final int? hitPointsCurrent;
  final int? hitPointsMax;
}

/// The D&D 5e fields of a [CharacterSummary].
extension Dnd5eCharacterSummary on CharacterSummary {
  Dnd5eRosterLine get dnd5e => Dnd5eRosterLine.of(this);

  String? get raceName => dnd5e.raceName;
  List<CharacterClass> get classes => dnd5e.classes;
  int get level => dnd5e.level;
  int? get hitPointsCurrent => dnd5e.hitPointsCurrent;
  int? get hitPointsMax => dnd5e.hitPointsMax;
}

// ---------------------------------------------------------------------------
// Requests
// ---------------------------------------------------------------------------

/// Body of `PATCH /characters/{id}/sheet`. Absent fields do not change; every
/// list sent replaces the existing one. Keys in [clear] are sent as an explicit
/// `null` (to unset a nullable field).
class SheetPatch {
  const SheetPatch({
    this.name,
    this.raceIndex,
    this.subraceIndex,
    this.backgroundIndex,
    this.alignment,
    this.applyRacialBonuses,
    this.hpMode,
    this.baseAbilities,
    this.classes,
    this.proficiencies,
    this.spells,
    this.overrides,
    this.notes,
    this.backstory,
    this.personalityTraits,
    this.ideals,
    this.bonds,
    this.flaws,
    this.backgroundDetail,
    this.copperPieces,
    this.heightInches,
    this.weightPounds,
    this.clear = const {},
  });

  final String? name;
  final String? raceIndex;
  final String? subraceIndex;
  final String? backgroundIndex;
  final String? alignment;
  final bool? applyRacialBonuses;
  final HpMode? hpMode;
  final Map<String, int>? baseAbilities;
  final List<SheetPatchClass>? classes;
  final List<CharacterProficiency>? proficiencies;
  final List<CharacterSpell>? spells;
  final List<CharacterOverride>? overrides;
  final String? notes;
  final String? backstory;
  final String? personalityTraits;
  final String? ideals;
  final String? bonds;
  final String? flaws;
  final String? backgroundDetail;
  final int? copperPieces;

  /// Height in inches and weight in pounds; the owner changes them without
  /// approval. Clear them with `clear: {'heightInches'}` / `{'weightPounds'}`.
  final int? heightInches;
  final int? weightPounds;

  /// Nullable keys to send as `null`: raceIndex, subraceIndex, backgroundIndex,
  /// alignment, heightInches, weightPounds.
  final Set<String> clear;

  bool get isEmpty => toJson().isEmpty;

  Map<String, dynamic> toJson() => {
    'name': ?name,
    'raceIndex': ?raceIndex,
    'subraceIndex': ?subraceIndex,
    'backgroundIndex': ?backgroundIndex,
    'alignment': ?alignment,
    'applyRacialBonuses': ?applyRacialBonuses,
    'hpMode': ?hpMode?.apiValue,
    if (baseAbilities != null) 'baseAbilities': {for (final k in abilityKeys) k: baseAbilities![k]},
    if (classes != null) 'classes': [for (final c in classes!) c.toJson()],
    if (proficiencies != null) 'proficiencies': [for (final p in proficiencies!) p.toPatchJson()],
    if (spells != null) 'spells': [for (final s in spells!) s.toPatchJson()],
    if (overrides != null) 'overrides': [for (final o in overrides!) o.toPatchJson()],
    'notes': ?notes,
    'backstory': ?backstory,
    'personalityTraits': ?personalityTraits,
    'ideals': ?ideals,
    'bonds': ?bonds,
    'flaws': ?flaws,
    'backgroundDetail': ?backgroundDetail,
    'copperPieces': ?copperPieces,
    'heightInches': ?heightInches,
    'weightPounds': ?weightPounds,
    for (final key in clear) key: null,
  };
}

class SheetPatchClass {
  const SheetPatchClass({required this.classIndex, this.subclassIndex, required this.level});

  final String classIndex;
  final String? subclassIndex;
  final int level;

  Map<String, dynamic> toJson() => {
    'classIndex': classIndex,
    'subclassIndex': ?subclassIndex,
    'level': level,
  };
}

/// Body of `PATCH /characters/{id}/combat`; only present fields are sent.
class CombatPatch {
  const CombatPatch({
    this.hitPointsCurrent,
    this.temporaryHitPoints,
    this.deathSaveSuccesses,
    this.deathSaveFailures,
    this.exhaustionLevel,
    this.conditions,
    this.inspiration,
  });

  final int? hitPointsCurrent;
  final int? temporaryHitPoints;
  final int? deathSaveSuccesses;
  final int? deathSaveFailures;
  final int? exhaustionLevel;
  final List<CharacterCondition>? conditions;
  final bool? inspiration;

  Map<String, dynamic> toJson() => {
    'hitPointsCurrent': ?hitPointsCurrent,
    'temporaryHitPoints': ?temporaryHitPoints,
    'deathSaveSuccesses': ?deathSaveSuccesses,
    'deathSaveFailures': ?deathSaveFailures,
    'exhaustionLevel': ?exhaustionLevel,
    if (conditions != null) 'conditions': [for (final c in conditions!) c.toJson()],
    'inspiration': ?inspiration,
  };
}

// ---------------------------------------------------------------------------
// Level choices and level-up plan (phase 16c)
// ---------------------------------------------------------------------------

Map<String, int> _intMap(Object? value) {
  final json = _map(value);
  if (json == null) return const {};
  return {
    for (final e in json.entries)
      if (_int(e.value) != null) e.key: _int(e.value)!,
  };
}

List<String> _strings(Object? value) =>
    value is List ? [for (final e in value) _str(e)] : const <String>[];

/// A picked option: catalog index (or free text) and its display name (`ChoiceItemDto`).
class ChoiceItem {
  const ChoiceItem({required this.index, required this.name});

  factory ChoiceItem.fromJson(Map<String, dynamic> json) {
    final index = _str(json['index']);
    return ChoiceItem(index: index, name: _str(json['name'], index));
  }

  final String index;
  final String name;
}

/// A choice the character made when levelling up (`CharacterChoiceDto`):
/// picks ([selected], [replaced]), an Ability Score Improvement ([asi]) or a
/// [feat] (with the [ability] it raised).
class CharacterChoice {
  const CharacterChoice({
    this.id = '',
    required this.level,
    required this.classIndex,
    required this.key,
    required this.name,
    this.kind = '',
    this.selected = const [],
    this.replaced = const [],
    this.asi = const {},
    this.feat,
    this.ability,
  });

  factory CharacterChoice.fromJson(Map<String, dynamic> json) {
    final feat = _map(json['feat']);
    return CharacterChoice(
      id: _str(json['id']),
      level: _int(json['level']) ?? 1,
      classIndex: _str(json['classIndex']),
      key: _str(json['key']),
      name: _str(json['name'], _str(json['key'])),
      kind: _str(json['kind']),
      selected: _objects(json['selected'], ChoiceItem.fromJson),
      replaced: _objects(json['replaced'], ChoiceItem.fromJson),
      asi: _intMap(json['asi']),
      feat: feat == null ? null : ChoiceItem.fromJson(feat),
      ability: _strOrNull(json['ability']),
    );
  }

  final String id;

  /// Level of the class at which it was chosen.
  final int level;
  final String classIndex;
  final String key;

  /// Name of the choice ("Fighting Style").
  final String name;

  /// A [LevelChoiceKind] api value.
  final String kind;
  final List<ChoiceItem> selected;
  final List<ChoiceItem> replaced;

  /// Ability key -> increase, for an Ability Score Improvement.
  final Map<String, int> asi;
  final ChoiceItem? feat;
  final String? ability;
}

/// A feat of the character with its catalog text, for the "Rasgos" tab
/// (`CharacterFeatDto`). [description] is empty when the feat is gone from
/// the catalog; [ability] is the key it raised, if any.
class CharacterFeat {
  const CharacterFeat({
    required this.index,
    required this.name,
    this.description = const [],
    this.prerequisitesText,
    this.ability,
    this.level = 0,
    this.classIndex,
  });

  factory CharacterFeat.fromJson(Map<String, dynamic> json) {
    final index = _str(json['index']);
    return CharacterFeat(
      index: index,
      name: _str(json['name'], index),
      description: _strings(json['description']),
      prerequisitesText: _strOrNull(json['prerequisitesText']),
      ability: _strOrNull(json['ability']),
      level: _int(json['level']) ?? 0,
      classIndex: _strOrNull(json['classIndex']),
    );
  }

  final String index;
  final String name;
  final List<String> description;
  final String? prerequisitesText;
  final String? ability;

  /// Level of the class at which it was taken; 0 for origin choices.
  final int level;
  final String? classIndex;
}

/// Kinds of level choices (`LevelChoiceKind`).
enum LevelChoiceKind {
  subclass('Subclass'),
  optionSet('OptionSet'),
  asiOrFeat('AsiOrFeat'),
  expertise('Expertise'),
  skill('Skill'),
  language('Language'),
  tool('Tool'),
  cantripsKnown('CantripsKnown'),
  spellsKnown('SpellsKnown'),
  spellbookSpells('SpellbookSpells'),
  custom('Custom');

  const LevelChoiceKind(this.apiValue);

  final String apiValue;

  /// Cantrips, spells known and spellbook spells.
  bool get isSpells => this == cantripsKnown || this == spellsKnown || this == spellbookSpells;

  static LevelChoiceKind fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, LevelChoiceKind.custom);
}

/// A class the character could gain the level in (`LevelUpClassDto`).
class LevelUpClassOption {
  const LevelUpClassOption({
    required this.classIndex,
    required this.name,
    this.allowed = true,
    this.reason,
    this.hitDie = 8,
    this.isNew = false,
    this.currentLevel = 0,
    this.subclassIndex,
  });

  factory LevelUpClassOption.fromJson(Map<String, dynamic> json) {
    final classIndex = _str(json['classIndex']);
    return LevelUpClassOption(
      classIndex: classIndex,
      name: _str(json['name'], _titleFromIndex(classIndex)),
      allowed: _bool(json['allowed'], true),
      reason: _strOrNull(json['reason']),
      hitDie: _int(json['hitDie']) ?? 8,
      isNew: _bool(json['isNew']),
      currentLevel: _int(json['currentLevel']) ?? 0,
      subclassIndex: _strOrNull(json['subclassIndex']),
    );
  }

  final String classIndex;
  final String name;

  /// False when a new class does not meet the multiclassing prerequisites ([reason]).
  final bool allowed;
  final String? reason;
  final int hitDie;

  /// The character does not have the class yet (multiclassing).
  final bool isNew;

  /// Level in the class now (0 for a new class).
  final int currentLevel;

  /// Current subclass in the class, if any.
  final String? subclassIndex;
}

/// A feature gained automatically at the new level (`LevelUpFeatureDto`).
class LevelUpFeature {
  const LevelUpFeature({
    required this.classIndex,
    this.subclassIndex,
    required this.level,
    required this.index,
    required this.name,
    this.description = const [],
  });

  factory LevelUpFeature.fromJson(Map<String, dynamic> json) {
    final feature = _map(json['feature']) ?? const {};
    final index = _str(feature['index']);
    return LevelUpFeature(
      classIndex: _str(json['classIndex']),
      subclassIndex: _strOrNull(json['subclassIndex']),
      level: _int(json['level']) ?? 1,
      index: index,
      name: _str(feature['name'], index),
      description: _strings(feature['description']),
    );
  }

  final String classIndex;

  /// Set for subclass features.
  final String? subclassIndex;
  final int level;
  final String index;
  final String name;
  final List<String> description;
}

/// A numeric effect of an option (`EffectPreviewDto`): "CA 16 → 17".
class EffectPreview {
  const EffectPreview({
    this.source = 'feature',
    required this.label,
    this.value = 0,
    this.field,
    this.before,
    this.after,
    this.condition,
  });

  factory EffectPreview.fromJson(Map<String, dynamic> json) => EffectPreview(
    source: _str(json['source'], 'feature'),
    label: _str(json['label']),
    value: _int(json['value']) ?? 0,
    field: _strOrNull(json['field']),
    before: _int(json['before']),
    after: _int(json['after']),
    condition: _strOrNull(json['condition']),
  );

  final String source;

  /// The value it changes ("CA").
  final String label;
  final int value;
  final String? field;
  final int? before;
  final int? after;

  /// When the effect applies (Spanish), for conditional effects.
  final String? condition;

  /// "CA 16 → 17", or "Ataque a distancia +2" without before/after; the
  /// [condition] goes in brackets.
  String get text {
    final sign = value >= 0 ? '+$value' : '$value';
    final base = before != null && after != null ? '$label $before → $after' : '$label $sign';
    return condition == null ? base : '$base ($condition)';
  }
}

/// A feat raises one ability of [from] (empty: any) by [amount] (`AbilityIncreaseDto`).
class AbilityIncrease {
  const AbilityIncrease({this.amount = 1, this.from = const []});

  factory AbilityIncrease.fromJson(Map<String, dynamic> json) =>
      AbilityIncrease(amount: _int(json['amount']) ?? 1, from: _strings(json['from']));

  final int amount;
  final List<String> from;

  /// The abilities that can be raised.
  List<String> get options => from.isEmpty ? abilityKeys : from;

  /// The player has to pick the ability (more than one candidate).
  bool get needsPick => options.length > 1;
}

/// An option of a level choice (`LevelUpOptionDto`).
class LevelUpOption {
  const LevelUpOption({
    required this.index,
    required this.name,
    this.description = const [],
    this.prerequisitesText,
    this.eligible = true,
    this.reason,
    this.spellLevel,
    this.spellCategory,
    this.effectsPreview = const [],
    this.abilityIncrease,
    this.damageType,
    this.cost,
    this.requires,
  });

  factory LevelUpOption.fromJson(Map<String, dynamic> json) {
    final index = _str(json['index']);
    final increase = _map(json['abilityIncrease']);
    final cost = _map(json['cost']);
    final requires = _map(json['requires']);
    return LevelUpOption(
      index: index,
      name: _str(json['name'], index),
      description: _strings(json['description']),
      prerequisitesText: _strOrNull(json['prerequisitesText']),
      eligible: _bool(json['eligible'], true),
      reason: _strOrNull(json['reason']),
      spellLevel: _int(json['spellLevel']),
      spellCategory: _strOrNull(json['spellCategory']),
      effectsPreview: _objects(json['effectsPreview'], EffectPreview.fromJson),
      abilityIncrease: increase == null ? null : AbilityIncrease.fromJson(increase),
      damageType: _strOrNull(json['damageType']),
      cost: cost == null ? null : OptionCost.fromJson(cost),
      requires: requires == null
          ? null
          : (choiceKey: _str(requires['choiceKey']), index: _str(requires['index'])),
    );
  }

  final String index;
  final String name;
  final List<String> description;
  final String? prerequisitesText;

  /// What using the option costs ("2 Ki"), or null.
  final OptionCost? cost;

  /// The option only applies when [index] is picked in the choice
  /// [choiceKey] of the same level-up (expertise in a skill gained now).
  final ({String choiceKey, String index})? requires;

  /// Damage type resisted by a draconic ancestry option ("fire").
  final String? damageType;

  /// False when the prerequisites are not met ([reason]).
  final bool eligible;
  final String? reason;

  /// Level of a spell option (0 for cantrips).
  final int? spellLevel;

  /// `SpellCategory` name of a spell option.
  final String? spellCategory;
  final List<EffectPreview> effectsPreview;

  /// For feats that raise an ability.
  final AbilityIncrease? abilityIncrease;
}

/// Prefix of the key of a forced replacement choice.
const replacementKeyPrefix = 'replace.';

/// A choice to make at the new level (`LevelUpChoiceDto`). [required] picks
/// are needed; with [replaces] one of [known] may be swapped (one more pick).
/// With a [subclassIndex] it only applies when that subclass is chosen.
class LevelUpChoice {
  const LevelUpChoice({
    required this.key,
    required this.name,
    required this.kind,
    this.choose = 0,
    this.required = 0,
    this.replaces = false,
    this.cumulative = false,
    this.note = '',
    this.subclassIndex,
    this.setId,
    this.freeText = false,
    this.options = const [],
    this.known = const [],
    this.warning,
  });

  factory LevelUpChoice.fromJson(Map<String, dynamic> json) => LevelUpChoice(
    key: _str(json['key']),
    name: _str(json['name'], _str(json['key'])),
    kind: LevelChoiceKind.fromApi(json['kind']),
    choose: _int(json['choose']) ?? 0,
    required: _int(json['required']) ?? _int(json['choose']) ?? 0,
    replaces: _bool(json['replaces']),
    cumulative: _bool(json['cumulative']),
    note: _str(json['note']),
    subclassIndex: _strOrNull(json['subclassIndex']),
    setId: _strOrNull(json['setId']),
    freeText: _bool(json['freeText']),
    options: _objects(json['options'], LevelUpOption.fromJson),
    known: _objects(json['known'], ChoiceItem.fromJson),
    warning: _strOrNull(json['warning']),
  );

  final String key;
  final String name;
  final LevelChoiceKind kind;
  final int choose;

  /// Picks needed (fewer than [choose] when not enough options are eligible).
  final int required;
  final bool replaces;
  final bool cumulative;
  final String note;
  final String? subclassIndex;
  final String? setId;

  /// No list: the player writes the values (languages, tools...).
  final bool freeText;
  final List<LevelUpOption> options;

  /// Earlier picks that may be replaced.
  final List<ChoiceItem> known;

  /// Why fewer picks than [choose] are required: not enough eligible options.
  final String? warning;

  /// The choice only offers replacements ([choose] 0).
  bool get replacementOnly => choose == 0;

  /// Forced replacement of an invalid pick (key `replace.<index>`).
  bool get isReplacement => key.startsWith(replacementKeyPrefix);

  LevelUpOption? option(String index) {
    for (final o in options) {
      if (o.index == index) return o;
    }
    return null;
  }
}

/// Spellcasting of the class at its new level (`LevelUpSpellcastingDto`).
class LevelUpSpellcasting {
  const LevelUpSpellcasting({
    required this.classIndex,
    this.ability,
    this.isPactCaster = false,
    this.cantripsKnown,
    this.spellsKnown,
    this.maxSpellLevel = 0,
    this.currentCantrips = 0,
    this.currentSpells = 0,
    this.spellSlots = const [],
    this.preparesSpells = false,
  });

  factory LevelUpSpellcasting.fromJson(Map<String, dynamic> json) => LevelUpSpellcasting(
    classIndex: _str(json['classIndex']),
    ability: _strOrNull(json['ability']),
    isPactCaster: _bool(json['isPactCaster']),
    cantripsKnown: _int(json['cantripsKnown']),
    spellsKnown: _int(json['spellsKnown']),
    maxSpellLevel: _int(json['maxSpellLevel']) ?? 0,
    currentCantrips: _int(json['currentCantrips']) ?? 0,
    currentSpells: _int(json['currentSpells']) ?? 0,
    spellSlots: [for (final s in (json['spellSlots'] as List? ?? const [])) _int(s) ?? 0],
    preparesSpells: _bool(json['preparesSpells']),
  );

  final String classIndex;
  final String? ability;
  final bool isPactCaster;
  final int? cantripsKnown;
  final int? spellsKnown;
  final int maxSpellLevel;
  final int currentCantrips;
  final int currentSpells;
  final List<int> spellSlots;

  /// The class prepares spells (cleric, druid, paladin, wizard) and already
  /// has slots: "Preparar conjuros" opens after the level-up.
  final bool preparesSpells;
}

/// A feature gained at the new level, for the review step
/// (`LevelUpNewFeatureDto`). [subclassIndex] is set for subclass features.
class LevelUpNewFeature {
  const LevelUpNewFeature({required this.name, this.description = const [], this.subclassIndex});

  factory LevelUpNewFeature.fromJson(Map<String, dynamic> json) => LevelUpNewFeature(
    name: _str(json['name']),
    description: _strings(json['description']),
    subclassIndex: _strOrNull(json['subclassIndex']),
  );

  final String name;
  final List<String> description;
  final String? subclassIndex;
}

/// What gaining the next level in [classIndex] means (`LevelUpPlanDto`).
class LevelUpPlan {
  const LevelUpPlan({
    this.characterId = '',
    this.currentLevel = 0,
    required this.targetLevel,
    required this.classIndex,
    this.classLevel = 1,
    this.hitDie = 8,
    this.conModifier = 0,
    this.classes = const [],
    this.automaticFeatures = const [],
    this.choices = const [],
    this.spellcasting,
    this.newFeatures = const [],
  });

  factory LevelUpPlan.fromJson(Map<String, dynamic> json) {
    final spellcasting = _map(json['spellcasting']);
    final currentLevel = _int(json['currentLevel']) ?? 0;
    return LevelUpPlan(
      characterId: _str(json['characterId']),
      currentLevel: currentLevel,
      targetLevel: _int(json['targetLevel']) ?? currentLevel + 1,
      classIndex: _str(json['classIndex']),
      classLevel: _int(json['classLevel']) ?? 1,
      hitDie: _int(json['hitDie']) ?? 8,
      conModifier: _int(json['conModifier']) ?? 0,
      classes: _objects(json['classes'], LevelUpClassOption.fromJson),
      automaticFeatures: _objects(json['automaticFeatures'], LevelUpFeature.fromJson),
      choices: _objects(json['choices'], LevelUpChoice.fromJson),
      spellcasting: spellcasting == null ? null : LevelUpSpellcasting.fromJson(spellcasting),
      newFeatures: _objects(json['newFeatures'], LevelUpNewFeature.fromJson),
    );
  }

  final String characterId;
  final int currentLevel;
  final int targetLevel;
  final String classIndex;

  /// Level of [classIndex] after the level-up.
  final int classLevel;
  final int hitDie;
  final int conModifier;
  final List<LevelUpClassOption> classes;
  final List<LevelUpFeature> automaticFeatures;
  final List<LevelUpChoice> choices;
  final LevelUpSpellcasting? spellcasting;

  /// Class and subclass features gained at the new level.
  final List<LevelUpNewFeature> newFeatures;

  /// The features of the class and of [subclassIndex] (null: only the class).
  List<LevelUpNewFeature> newFeaturesFor(String? subclassIndex) => [
    for (final f in newFeatures)
      if (f.subclassIndex == null || f.subclassIndex == subclassIndex) f,
  ];

  /// The entry of [classIndex] in [classes], or null.
  LevelUpClassOption? get selectedClass {
    for (final c in classes) {
      if (c.classIndex == classIndex) return c;
    }
    return null;
  }
}

/// The answer to one choice of the plan (`LevelUpChoiceAnswer`): picks
/// ([selected], with [replaced]), an ASI ([asi]) or a [feat] (and [ability]).
class LevelUpChoiceAnswer {
  const LevelUpChoiceAnswer.picks(this.key, this.selected, {this.replaced = const []})
    : asi = null,
      feat = null,
      ability = null;

  const LevelUpChoiceAnswer.asi(this.key, Map<String, int> this.asi)
    : selected = const [],
      replaced = const [],
      feat = null,
      ability = null;

  const LevelUpChoiceAnswer.feat(this.key, String this.feat, {this.ability})
    : selected = const [],
      replaced = const [],
      asi = null;

  final String key;
  final List<String> selected;
  final List<String> replaced;
  final Map<String, int>? asi;
  final String? feat;
  final String? ability;

  Map<String, dynamic> toJson() => {
    'key': key,
    'selected': asi != null
        ? {'asi': asi}
        : feat != null
        ? {'feat': feat, 'ability': ?ability}
        : selected,
    if (replaced.isNotEmpty) 'replaced': replaced,
  };
}

/// Body of `POST /characters/{id}/level-up`.
class LevelUpRequest {
  const LevelUpRequest({this.classIndex, required this.hitPointsRolled, this.choices = const []});

  final String? classIndex;

  /// Result of the hit die (1..die); the server adds Constitution.
  final int hitPointsRolled;
  final List<LevelUpChoiceAnswer> choices;

  Map<String, dynamic> toJson() => {
    'classIndex': classIndex,
    'hitPointsRolled': hitPointsRolled,
    'choices': [for (final c in choices) c.toJson()],
  };
}

// ---------------------------------------------------------------------------
// Spell preparation (phase 18)
// ---------------------------------------------------------------------------

/// Why the player has to prepare spells (`spellPreparationReason`).
enum SpellPreparationReason {
  creation('Creation', 'Primera preparación'),
  longRest('LongRest', 'Tras el descanso largo'),
  levelUp('LevelUp', 'Nuevo nivel');

  const SpellPreparationReason(this.apiValue, this.label);

  final String apiValue;

  /// Header of the "Prepara tus conjuros" page.
  final String label;

  static SpellPreparationReason? fromApi(Object? value) {
    for (final r in values) {
      if (r.apiValue == value) return r;
    }
    return null;
  }
}

/// A spell of the preparation page (`PreparationSpellDto`).
class PreparationSpell {
  const PreparationSpell({
    required this.index,
    required this.name,
    required this.level,
    this.school,
    this.category,
    this.concentration = false,
    this.ritual = false,
    this.castingTime,
    this.source,
  });

  factory PreparationSpell.fromJson(Map<String, dynamic> json) => PreparationSpell(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    level: _int(json['level']) ?? 1,
    school: _strOrNull(json['school']),
    category: _strOrNull(json['category']),
    concentration: _bool(json['concentration']),
    ritual: _bool(json['ritual']),
    castingTime: _strOrNull(json['castingTime']),
    source: _strOrNull(json['source']),
  );

  final String index;
  final String name;
  final int level;
  final String? school;

  /// `SpellCategory` name.
  final String? category;
  final bool concentration;
  final bool ritual;
  final String? castingTime;
  final String? source;
}

/// One class that prepares spells (`SpellPreparationClassDto`): [max] spells
/// out of [candidates], besides the [alwaysPrepared] ones, which do not count.
class PreparationClass {
  const PreparationClass({
    required this.classIndex,
    required this.className,
    required this.max,
    this.maxSpellLevel = 0,
    this.alwaysPrepared = const [],
    this.prepared = const [],
    this.candidates = const [],
  });

  factory PreparationClass.fromJson(Map<String, dynamic> json) => PreparationClass(
    classIndex: _str(json['classIndex']),
    className: _str(json['className'], _str(json['classIndex'])),
    max: _int(json['max']) ?? 0,
    maxSpellLevel: _int(json['maxSpellLevel']) ?? 0,
    alwaysPrepared: _objects(json['alwaysPrepared'], PreparationSpell.fromJson),
    prepared: _strings(json['prepared']),
    candidates: _objects(json['candidates'], PreparationSpell.fromJson),
  );

  final String classIndex;
  final String className;
  final int max;
  final int maxSpellLevel;
  final List<PreparationSpell> alwaysPrepared;

  /// Indexes prepared now (always-prepared ones excluded).
  final List<String> prepared;
  final List<PreparationSpell> candidates;
}

/// `GET /characters/{id}/spell-preparation`.
class SpellPreparation {
  const SpellPreparation({
    required this.pending,
    this.reason,
    this.canKeep = false,
    this.keepProblem,
    this.classes = const [],
  });

  factory SpellPreparation.fromJson(Map<String, dynamic> json) => SpellPreparation(
    pending: _bool(json['pending']),
    reason: SpellPreparationReason.fromApi(json['reason']),
    canKeep: _bool(json['canKeep']),
    keepProblem: _strOrNull(json['keepProblem']),
    classes: _objects(json['classes'], PreparationClass.fromJson),
  );

  final bool pending;
  final SpellPreparationReason? reason;

  /// "Mantener los de ayer" is possible.
  final bool canKeep;

  /// Why it is not (Spanish), when [canKeep] is false.
  final String? keepProblem;
  final List<PreparationClass> classes;
}

// ---------------------------------------------------------------------------
// Origin choices, invalid choices, damage outcome (phase 19)
// ---------------------------------------------------------------------------

/// An option or feat whose prerequisites no longer hold (`InvalidChoiceDto`).
class InvalidChoice {
  const InvalidChoice({
    required this.replaceKey,
    this.classIndex,
    this.key = '',
    this.level = 1,
    this.setId = '',
    required this.item,
    this.reason = '',
    this.code = '',
  });

  factory InvalidChoice.fromJson(Map<String, dynamic> json) => InvalidChoice(
    replaceKey: _str(json['replaceKey']),
    classIndex: _strOrNull(json['classIndex']),
    key: _str(json['key']),
    level: _int(json['level']) ?? 1,
    setId: _str(json['setId']),
    item: ChoiceItem.fromJson(_map(json['item']) ?? const {}),
    reason: _str(json['reason']),
    code: _str(json['code']),
  );

  /// Code of [packDisabled] picks.
  static const packDisabledCode = 'pack-disabled';

  /// The reason the app shows for a pick of a disabled pack when the server
  /// sends none.
  static const packDisabledReason =
      'Este contenido pertenece a un paquete desactivado en la campaña.';

  /// Key to answer in the replacement (`replace.<index>`).
  final String replaceKey;

  /// Class of the choice that picked it; null for an origin feat.
  final String? classIndex;
  final String key;
  final int level;
  final String setId;
  final ChoiceItem item;
  final String reason;

  /// "prerequisites" or "pack-disabled" (its content pack is disabled in the
  /// campaign).
  final String code;

  bool get packDisabled => code == packDisabledCode;

  /// [reason], or the standard text of [code] when the server sent none.
  String get displayReason => reason.isNotEmpty
      ? reason
      : packDisabled
      ? packDisabledReason
      : 'Ya no cumples sus requisitos.';
}

/// `GET /characters/{id}/invalid-choices`: the invalid picks and one
/// replacement choice per pick (same shape as the level-up choices).
class InvalidChoices {
  const InvalidChoices({this.characterId = '', this.invalid = const [], this.choices = const []});

  factory InvalidChoices.fromJson(Map<String, dynamic> json) => InvalidChoices(
    characterId: _str(json['characterId']),
    invalid: _objects(json['invalid'], InvalidChoice.fromJson),
    choices: _objects(json['choices'], LevelUpChoice.fromJson),
  );

  final String characterId;
  final List<InvalidChoice> invalid;
  final List<LevelUpChoice> choices;
}

/// Kinds of origin choices (`OriginChoiceDto.kind`).
enum OriginChoiceKind {
  abilityBonus('AbilityBonus'),
  skill('Skill'),
  language('Language'),
  tool('Tool'),
  cantrip('Cantrip'),
  feat('Feat'),
  traitOption('TraitOption');

  const OriginChoiceKind(this.apiValue);

  final String apiValue;

  static OriginChoiceKind fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, OriginChoiceKind.traitOption);
}

/// A decision of the race, subrace or background (`OriginChoiceDto`).
/// [required] picks block the activation; optional ones (languages) have 0.
class OriginChoice {
  const OriginChoice({
    required this.key,
    required this.name,
    required this.kind,
    this.source = 'race',
    this.choose = 1,
    this.required = 0,
    this.amount,
    this.freeText = false,
    this.note = '',
    this.options = const [],
    this.selected = const [],
    this.feat,
    this.ability,
  });

  factory OriginChoice.fromJson(Map<String, dynamic> json) {
    final feat = _map(json['feat']);
    return OriginChoice(
      key: _str(json['key']),
      name: _str(json['name'], _str(json['key'])),
      kind: OriginChoiceKind.fromApi(json['kind']),
      source: _str(json['source'], 'race'),
      choose: _int(json['choose']) ?? 1,
      required: _int(json['required']) ?? 0,
      amount: _int(json['amount']),
      freeText: _bool(json['freeText']),
      note: _str(json['note']),
      options: _objects(json['options'], LevelUpOption.fromJson),
      selected: _objects(json['selected'], ChoiceItem.fromJson),
      feat: feat == null ? null : ChoiceItem.fromJson(feat),
      ability: _strOrNull(json['ability']),
    );
  }

  final String key;
  final String name;
  final OriginChoiceKind kind;

  /// "race", "subrace" or "background".
  final String source;
  final int choose;
  final int required;

  /// Bonus of each pick of an ability bonus choice (+1).
  final int? amount;

  /// No closed list: any text (tools of any kind).
  final bool freeText;
  final String note;
  final List<LevelUpOption> options;

  /// Current answer (empty when not answered).
  final List<ChoiceItem> selected;

  /// Current feat answer and the ability it raised.
  final ChoiceItem? feat;
  final String? ability;

  LevelUpOption? option(String index) {
    for (final o in options) {
      if (o.index == index) return o;
    }
    return null;
  }
}

/// `GET/PUT /characters/{id}/origin-choices` (`OriginChoicesDto`).
class OriginChoices {
  const OriginChoices({this.characterId = '', this.complete = true, this.choices = const []});

  factory OriginChoices.fromJson(Map<String, dynamic> json) => OriginChoices(
    characterId: _str(json['characterId']),
    complete: _bool(json['complete'], true),
    choices: _objects(json['choices'], OriginChoice.fromJson),
  );

  final String characterId;

  /// Every required choice is answered.
  final bool complete;
  final List<OriginChoice> choices;
}

/// Damage outcome of `POST /characters/{id}/damage` and of `party/adjust`
/// (`DamageOutcomeDto`): what the damage meant for the concentration.
class DamageOutcome {
  const DamageOutcome({
    this.characterId = '',
    this.damage = 0,
    this.hitPointsCurrent = 0,
    this.concentratingOn,
    this.concentrationCheckDc,
    this.concentrationEnded = false,
  });

  factory DamageOutcome.fromJson(Map<String, dynamic> json) => DamageOutcome(
    characterId: _str(json['characterId']),
    damage: _int(json['damage']) ?? 0,
    hitPointsCurrent: _int(json['hitPointsCurrent']) ?? 0,
    concentratingOn: _strOrNull(json['concentratingOn']),
    concentrationCheckDc: _int(json['concentrationCheckDc']),
    concentrationEnded: _bool(json['concentrationEnded']),
  );

  final String characterId;
  final int damage;
  final int hitPointsCurrent;

  /// Spell index the character was concentrating on before the damage.
  final String? concentratingOn;

  /// DC of the Constitution save to keep concentrating, when it applies.
  final int? concentrationCheckDc;

  /// The concentration ended by itself (0 hit points).
  final bool concentrationEnded;

  /// The damage asks for a concentration decision of any kind.
  bool get affectsConcentration => concentrationCheckDc != null || concentrationEnded;
}

/// Response of `POST /characters/{id}/damage`.
class DamageResult {
  const DamageResult({required this.character, required this.outcome});

  final CharacterDetail character;
  final DamageOutcome outcome;
}
