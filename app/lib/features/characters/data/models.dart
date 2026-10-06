// Hand-written models for the characters API (phase 4). Parsers are tolerant:
// missing fields fall back to neutral values so a slightly different server
// shape does not break the sheet.

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

DateTime? _date(Object? value) => value is String ? DateTime.tryParse(value) : null;

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

enum CharacterStatus {
  draft('Draft', 'Borrador'),
  active('Active', 'Activo');

  const CharacterStatus(this.apiValue, this.label);

  final String apiValue;
  final String label;

  static CharacterStatus fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, CharacterStatus.draft);
}

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

enum ChangeRequestType {
  activate('Activate', 'Activación'),
  editSheet('EditSheet', 'Edición de hoja'),
  addItem('AddItem', 'Añadir objeto'),
  removeItem('RemoveItem', 'Quitar objeto'),
  customItem('CustomItem', 'Objeto personalizado'),
  adjustMoney('AdjustMoney', 'Ajuste de dinero'),
  other('Other', 'Otro');

  const ChangeRequestType(this.apiValue, this.label);

  final String apiValue;
  final String label;

  static ChangeRequestType fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, ChangeRequestType.other);
}

enum ChangeRequestStatus {
  pending('Pending', 'Pendiente'),
  approved('Approved', 'Aprobada'),
  rejected('Rejected', 'Rechazada'),
  cancelled('Cancelled', 'Cancelada');

  const ChangeRequestStatus(this.apiValue, this.label);

  final String apiValue;
  final String label;

  static ChangeRequestStatus fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, ChangeRequestStatus.pending);
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
  });

  factory CharacterClass.fromJson(Map<String, dynamic> json) {
    final classIndex = _str(json['classIndex']);
    return CharacterClass(
      classIndex: classIndex,
      className: _str(json['className'], _titleFromIndex(classIndex)),
      subclassIndex: _strOrNull(json['subclassIndex']),
      subclassName: _strOrNull(json['subclassName']),
      level: _int(json['level']) ?? 1,
    );
  }

  final String classIndex;
  final String className;
  final String? subclassIndex;
  final String? subclassName;
  final int level;

  /// "Wizard 3".
  String get label => '$className $level';
}

/// "Fighter 3 / Wizard 2"; empty when the character has no class yet.
String classesLabel(List<CharacterClass> classes) => classes.map((c) => c.label).join(' / ');

class CharacterSummary {
  const CharacterSummary({
    required this.id,
    required this.campaignId,
    this.ownerUserId,
    this.ownerDisplayName,
    required this.name,
    required this.status,
    this.raceName,
    this.classes = const [],
    required this.level,
    this.hitPointsCurrent,
    this.hitPointsMax,
    this.portraitUrl,
  });

  factory CharacterSummary.fromJson(Map<String, dynamic> json) {
    final classes = _objects(json['classes'], CharacterClass.fromJson);
    return CharacterSummary(
      id: _str(json['id']),
      campaignId: _str(json['campaignId']),
      ownerUserId: _strOrNull(json['ownerUserId']),
      ownerDisplayName: _strOrNull(json['ownerDisplayName']),
      name: _str(json['name']),
      status: CharacterStatus.fromApi(json['status']),
      raceName: _strOrNull(json['raceName']),
      classes: classes,
      level: _int(json['level']) ?? classes.fold<int>(0, (sum, c) => sum + c.level),
      hitPointsCurrent: _int(json['hitPointsCurrent']),
      hitPointsMax: _int(json['hitPointsMax']),
      portraitUrl: _strOrNull(json['portraitUrl']),
    );
  }

  final String id;
  final String campaignId;

  /// Null for NPCs.
  final String? ownerUserId;
  final String? ownerDisplayName;
  final String name;
  final CharacterStatus status;
  final String? raceName;
  final List<CharacterClass> classes;
  final int level;

  /// Only sent to the owner and the DMs.
  final int? hitPointsCurrent;
  final int? hitPointsMax;
  final String? portraitUrl;
}

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
  );

  final String spellIndex;
  final String classIndex;
  final bool isPrepared;
  final bool alwaysPrepared;
  final String? name;
  final int? level;

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
  });

  factory CharacterResource.fromJson(Map<String, dynamic> json) => CharacterResource(
    id: _str(json['id']),
    key: _strOrNull(json['key']),
    name: _str(json['name']),
    max: _int(json['max']) ?? 0,
    used: _int(json['used']) ?? 0,
    recharge: Recharge.fromApi(json['recharge']),
    isAuto: _bool(json['isAuto']),
  );

  final String id;
  final String? key;
  final String name;
  final int max;
  final int used;
  final Recharge recharge;
  final bool isAuto;
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
  });

  factory Spellcasting.fromJson(Map<String, dynamic> json) => Spellcasting(
    classIndex: _str(json['classIndex']),
    ability: _str(json['ability']),
    saveDc: _int(json['saveDc']) ?? 0,
    attackBonus: _int(json['attackBonus']) ?? 0,
    preparedMax: _int(json['preparedMax']),
  );

  final String classIndex;
  final String ability;
  final int saveDc;
  final int attackBonus;
  final int? preparedMax;
}

/// One term of a calculated value (`BreakdownPartDto`). [source] is one of
/// base, race, subrace, ability, proficiency, expertise, class, armor, shield,
/// item, override or feature.
class BreakdownPart {
  const BreakdownPart({required this.source, required this.label, required this.value});

  factory BreakdownPart.fromJson(Map<String, dynamic> json) => BreakdownPart(
    source: _str(json['source']),
    label: _str(json['label']),
    value: _int(json['value']) ?? 0,
  );

  final String source;
  final String label;
  final int value;
}

/// A calculated value explained point by point (`ValueBreakdownDto`): the
/// [parts] add up to [total].
class ValueBreakdown {
  const ValueBreakdown({this.total = 0, this.parts = const []});

  factory ValueBreakdown.fromJson(Object? raw) {
    final json = _map(raw);
    if (json == null) return const ValueBreakdown();
    return ValueBreakdown(
      total: _int(json['total']) ?? 0,
      parts: _objects(json['parts'], BreakdownPart.fromJson),
    );
  }

  /// Null when [raw] is not an object (older servers).
  static ValueBreakdown? maybeFromJson(Object? raw) =>
      raw is Map ? ValueBreakdown.fromJson(raw) : null;

  final int total;
  final List<BreakdownPart> parts;

  /// True when an item or a manual override contributes to the value.
  bool get hasItemOrOverride => parts.any((p) => p.source == 'item' || p.source == 'override');
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

class CharacterDetail {
  const CharacterDetail({
    required this.id,
    required this.campaignId,
    this.ownerUserId,
    this.ownerDisplayName,
    required this.name,
    required this.status,
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
    this.copperPieces = 0,
    this.hitDiceUsed = const {},
    this.notes = '',
    this.backstory = '',
    this.portraitUrl,
    this.classes = const [],
    this.proficiencies = const [],
    this.spells = const [],
    this.overrides = const [],
    this.resources = const [],
    this.spellSlots = const [],
    this.combat = const CombatSummary(),
    this.sheet = const CharacterSheet(),
    this.pendingChangeRequests = const [],
    this.createdAt,
    this.updatedAt,
  });

  factory CharacterDetail.fromJson(Map<String, dynamic> json) {
    // Base scores arrive as `baseStr`... (stored fields) or as `baseAbilities`.
    final nested = _map(json['baseAbilities']) ?? const {};
    int base(String key) => _int(json['base${_titleFromIndex(key)}']) ?? _int(nested[key]) ?? 10;
    final used = _map(json['hitDiceUsed']) ?? const {};
    return CharacterDetail(
      id: _str(json['id']),
      campaignId: _str(json['campaignId']),
      ownerUserId: _strOrNull(json['ownerUserId']),
      ownerDisplayName: _strOrNull(json['ownerDisplayName']),
      name: _str(json['name']),
      status: CharacterStatus.fromApi(json['status']),
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
      copperPieces: _int(json['copperPieces']) ?? 0,
      hitDiceUsed: {for (final e in used.entries) e.key: _int(e.value) ?? 0},
      notes: _str(json['notes']),
      backstory: _str(json['backstory']),
      portraitUrl: _strOrNull(json['portraitUrl']),
      classes: _objects(json['classes'], CharacterClass.fromJson),
      proficiencies: _objects(json['proficiencies'], CharacterProficiency.fromJson),
      spells: _objects(json['spells'], CharacterSpell.fromJson),
      overrides: _objects(json['overrides'], CharacterOverride.fromJson),
      resources: _objects(json['resources'], CharacterResource.fromJson),
      spellSlots: _objects(json['spellSlots'], SpellSlot.fromJson),
      combat: CombatSummary.fromJson(json['combat']),
      sheet: CharacterSheet.fromJson(_map(json['sheet']) ?? const {}),
      pendingChangeRequests: _objects(json['pendingChangeRequests'], ChangeRequest.fromJson),
      createdAt: _date(json['createdAt']),
      updatedAt: _date(json['updatedAt']),
    );
  }

  final String id;
  final String campaignId;
  final String? ownerUserId;
  final String? ownerDisplayName;
  final String name;
  final CharacterStatus status;
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

  /// Total money in copper pieces.
  final int copperPieces;
  final Map<String, int> hitDiceUsed;
  final String notes;
  final String backstory;
  final String? portraitUrl;
  final List<CharacterClass> classes;
  final List<CharacterProficiency> proficiencies;
  final List<CharacterSpell> spells;
  final List<CharacterOverride> overrides;
  final List<CharacterResource> resources;
  final List<SpellSlot> spellSlots;

  /// Precomputed combat data; empty when the server does not send it.
  final CombatSummary combat;
  final CharacterSheet sheet;
  final List<ChangeRequest> pendingChangeRequests;
  final DateTime? createdAt;
  final DateTime? updatedAt;

  int get totalLevel => classes.fold<int>(0, (sum, c) => sum + c.level);

  /// The override of [field], or null.
  CharacterOverride? overrideOf(String field) {
    for (final o in overrides) {
      if (o.field == field) return o;
    }
    return null;
  }

  bool get hasPendingActivation => pendingChangeRequests.any(
    (r) => r.type == ChangeRequestType.activate && r.status == ChangeRequestStatus.pending,
  );
}

// ---------------------------------------------------------------------------
// Change requests
// ---------------------------------------------------------------------------

class ChangeRequest {
  const ChangeRequest({
    required this.id,
    required this.campaignId,
    required this.characterId,
    this.characterName = '',
    required this.requestedByUserId,
    this.requestedByDisplayName = '',
    required this.type,
    this.payload = const {},
    required this.status,
    this.resolvedByDisplayName,
    this.resolvedAt,
    this.comment,
    this.createdAt,
  });

  factory ChangeRequest.fromJson(Map<String, dynamic> json) => ChangeRequest(
    id: _str(json['id']),
    campaignId: _str(json['campaignId']),
    characterId: _str(json['characterId']),
    characterName: _str(json['characterName']),
    requestedByUserId: _str(json['requestedByUserId']),
    requestedByDisplayName: _str(json['requestedByDisplayName']),
    type: ChangeRequestType.fromApi(json['type']),
    payload: _map(json['payload']) ?? const {},
    status: ChangeRequestStatus.fromApi(json['status']),
    resolvedByDisplayName: _strOrNull(json['resolvedByDisplayName']),
    resolvedAt: _date(json['resolvedAt']),
    comment: _strOrNull(json['comment']),
    createdAt: _date(json['createdAt']),
  );

  final String id;
  final String campaignId;
  final String characterId;
  final String characterName;
  final String requestedByUserId;
  final String requestedByDisplayName;
  final ChangeRequestType type;

  /// What would change; for `EditSheet` it has the shape of a [SheetPatch].
  final Map<String, dynamic> payload;
  final ChangeRequestStatus status;
  final String? resolvedByDisplayName;
  final DateTime? resolvedAt;
  final String? comment;
  final DateTime? createdAt;

  bool get isPending => status == ChangeRequestStatus.pending;
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
    this.copperPieces,
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
  final int? copperPieces;

  /// Nullable keys to send as `null`: raceIndex, subraceIndex, backgroundIndex, alignment.
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
    'copperPieces': ?copperPieces,
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

/// Outcome of saving a sheet: applied (200) or waiting for the DM (202).
sealed class SheetSaveResult {
  const SheetSaveResult();
}

class Saved extends SheetSaveResult {
  const Saved(this.detail);

  final CharacterDetail detail;
}

class PendingApproval extends SheetSaveResult {
  const PendingApproval(this.changeRequest);

  final ChangeRequest changeRequest;
}
