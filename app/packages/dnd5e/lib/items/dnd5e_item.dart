// D&D 5e fields of the items of the core (`EffectiveItem.raw`,
// `ItemOverrides.system`): damage, armor class, properties, range, rarity,
// attunement and modifiers, plus the homebrew template body.

import 'package:opentrpg_core/features/items/data/models.dart';

import '../catalog/data/models.dart' show ItemArmor, ItemDamage, ItemModifier;

export '../catalog/data/models.dart' show ItemArmor, ItemDamage, ItemModifier;

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

bool? _boolOrNull(Object? value) => value == null ? null : _bool(value);

List<String> _strList(Object? value) => value is List
    ? [
        for (final e in value)
          if (e != null) _str(e),
      ]
    : const [];

List<String>? _strListOrNull(Object? value) => value is List ? _strList(value) : null;

/// Maximum number of attuned items (SRD rule).
const maxAttunedItems = 3;

/// The D&D 5e part of an [EffectiveItem] (`EffectiveItemDto`), read from its
/// [EffectiveItem.raw] once and cached ([of]).
class Dnd5eItem {
  const Dnd5eItem({
    this.rarity,
    this.requiresAttunement = false,
    this.damage,
    this.properties = const [],
    this.rangeNormal,
    this.rangeLong,
    this.armor,
    this.attackBonus = 0,
    this.damageBonus = 0,
    this.modifiers = const [],
  });

  factory Dnd5eItem.fromJson(Map<String, dynamic> json) {
    final damageJson = _map(json['damage']);
    final dice = _strOrNull(damageJson?['dice']);
    final armorJson = _map(json['armor']);
    final base = _int(armorJson?['base']);
    final rangeJson = _map(json['range']);
    return Dnd5eItem(
      rarity: _strOrNull(json['rarity']),
      requiresAttunement: _bool(json['requiresAttunement']),
      damage: dice == null
          ? null
          : ItemDamage(
              dice: dice,
              type: _strOrNull(damageJson?['type']),
              versatileDice: _strOrNull(damageJson?['versatile'] ?? damageJson?['versatileDice']),
            ),
      properties: _strList(json['properties']),
      rangeNormal: _int(rangeJson?['normal']),
      rangeLong: _int(rangeJson?['long']),
      armor: base == null
          ? null
          : ItemArmor(
              baseAc: base,
              addDexModifier: _boolOrNull(armorJson?['addDex']),
              maxDexBonus: _int(armorJson?['maxDex']),
              strengthMinimum: _int(armorJson?['strengthMinimum']),
              stealthDisadvantage: _bool(armorJson?['stealthDisadvantage']),
            ),
      attackBonus: _int(json['attackBonus']) ?? 0,
      damageBonus: _int(json['damageBonus']) ?? 0,
      modifiers: ItemModifier.listFromJson(json['modifiers']),
    );
  }

  static final _cache = Expando<Dnd5eItem>('dnd5eItem');

  /// The D&D 5e fields of [item], parsed once per item.
  static Dnd5eItem of(EffectiveItem item) => _cache[item] ??= Dnd5eItem.fromJson(item.raw);

  final String? rarity;
  final bool requiresAttunement;
  final ItemDamage? damage;
  final List<String> properties;
  final int? rangeNormal;
  final int? rangeLong;
  final ItemArmor? armor;
  final int attackBonus;
  final int damageBonus;

  /// Structured modifiers (the legacy attack/damage bonuses are already in).
  final List<ItemModifier> modifiers;

  /// The fields in the wire shape of `EffectiveItemDto`.
  Map<String, dynamic> toJson() => {
    'rarity': ?rarity,
    'requiresAttunement': requiresAttunement,
    if (damage != null)
      'damage': {'dice': damage!.dice, 'type': ?damage!.type, 'versatile': ?damage!.versatileDice},
    'properties': properties,
    if (rangeNormal != null || rangeLong != null)
      'range': {'normal': ?rangeNormal, 'long': ?rangeLong},
    if (armor != null)
      'armor': {
        'base': ?armor!.baseAc,
        'addDex': ?armor!.addDexModifier,
        'maxDex': ?armor!.maxDexBonus,
        'strengthMinimum': ?armor!.strengthMinimum,
        'stealthDisadvantage': armor!.stealthDisadvantage,
      },
    'attackBonus': attackBonus,
    'damageBonus': damageBonus,
    'modifiers': [for (final m in modifiers) m.toJson()],
  };
}

/// An [EffectiveItem] with D&D 5e fields, as the server would send it (tests,
/// previews).
EffectiveItem dnd5eEffectiveItem({
  required String name,
  String category = '',
  String? subcategory,
  String? rarity,
  bool requiresAttunement = false,
  double? weightLb,
  int? costCp,
  ItemDamage? damage,
  List<String> properties = const [],
  int? rangeNormal,
  int? rangeLong,
  ItemArmor? armor,
  int attackBonus = 0,
  int damageBonus = 0,
  List<String> effects = const [],
  List<String> description = const [],
  List<ItemModifier> modifiers = const [],
}) {
  final system = Dnd5eItem(
    rarity: rarity,
    requiresAttunement: requiresAttunement,
    damage: damage,
    properties: properties,
    rangeNormal: rangeNormal,
    rangeLong: rangeLong,
    armor: armor,
    attackBonus: attackBonus,
    damageBonus: damageBonus,
    modifiers: modifiers,
  );
  final item = EffectiveItem(
    name: name,
    category: category,
    subcategory: subcategory,
    weightLb: weightLb,
    costCp: costCp,
    effects: effects,
    description: description,
    raw: {
      'name': name,
      'category': category,
      'subcategory': ?subcategory,
      'weightLb': ?weightLb,
      'costCp': ?costCp,
      'effects': effects,
      'description': description,
      ...system.toJson(),
    },
  );
  Dnd5eItem._cache[item] = system;
  return item;
}

/// The D&D 5e fields of an [EffectiveItem].
extension Dnd5eEffectiveItem on EffectiveItem {
  Dnd5eItem get dnd5e => Dnd5eItem.of(this);

  String? get rarity => dnd5e.rarity;
  bool get requiresAttunement => dnd5e.requiresAttunement;
  ItemDamage? get damage => dnd5e.damage;
  List<String> get properties => dnd5e.properties;
  int? get rangeNormal => dnd5e.rangeNormal;
  int? get rangeLong => dnd5e.rangeLong;
  ItemArmor? get armor => dnd5e.armor;
  int get attackBonus => dnd5e.attackBonus;
  int get damageBonus => dnd5e.damageBonus;
  List<ItemModifier> get modifiers => dnd5e.modifiers;
}

/// [ItemOverrides] with D&D 5e fields: the core ones go to their fields and
/// the rest to [ItemOverrides.system] with their wire names.
ItemOverrides dnd5eItemOverrides({
  String? name,
  List<String>? description,
  String? category,
  String? damageDice,
  String? damageType,
  String? versatileDice,
  List<String>? properties,
  int? rangeNormal,
  int? rangeLong,
  int? armorClassBase,
  bool? addDexModifier,
  int? maxDexBonus,
  int? strengthMinimum,
  bool? stealthDisadvantage,
  double? weightLb,
  String? rarity,
  bool? requiresAttunement,
  int? attackBonus,
  int? damageBonus,
  List<String>? effects,
  List<ItemModifier>? modifiers,
}) => ItemOverrides(
  name: name,
  description: description,
  category: category,
  weightLb: weightLb,
  effects: effects,
  system: {
    'damageDice': ?damageDice,
    'damageType': ?damageType,
    'versatileDice': ?versatileDice,
    'properties': ?properties,
    'rangeNormal': ?rangeNormal,
    'rangeLong': ?rangeLong,
    'armorClassBase': ?armorClassBase,
    'addDexModifier': ?addDexModifier,
    'maxDexBonus': ?maxDexBonus,
    'strengthMinimum': ?strengthMinimum,
    'stealthDisadvantage': ?stealthDisadvantage,
    'rarity': ?rarity,
    'requiresAttunement': ?requiresAttunement,
    'attackBonus': ?attackBonus,
    'damageBonus': ?damageBonus,
    if (modifiers != null) 'modifiers': [for (final m in modifiers) m.toJson()],
  },
);

/// The D&D 5e fields of [ItemOverrides] (null when not overridden).
extension Dnd5eItemOverrides on ItemOverrides {
  String? get damageDice => _strOrNull(system['damageDice']);
  String? get damageType => _strOrNull(system['damageType']);
  String? get versatileDice => _strOrNull(system['versatileDice']);
  List<String>? get properties => _strListOrNull(system['properties']);
  int? get rangeNormal => _int(system['rangeNormal']);
  int? get rangeLong => _int(system['rangeLong']);
  int? get armorClassBase => _int(system['armorClassBase']);
  bool? get addDexModifier => _boolOrNull(system['addDexModifier']);
  int? get maxDexBonus => _int(system['maxDexBonus']);
  int? get strengthMinimum => _int(system['strengthMinimum']);
  bool? get stealthDisadvantage => _boolOrNull(system['stealthDisadvantage']);
  String? get rarity => _strOrNull(system['rarity']);
  bool? get requiresAttunement => _boolOrNull(system['requiresAttunement']);
  int? get attackBonus => _int(system['attackBonus']);
  int? get damageBonus => _int(system['damageBonus']);

  /// Null keeps the template's modifiers; an empty list removes them all.
  List<ItemModifier>? get modifiers =>
      system['modifiers'] is List ? ItemModifier.listFromJson(system['modifiers']) : null;
}

// ---------------------------------------------------------------------------
// Templates (homebrew)
// ---------------------------------------------------------------------------

/// Body to create or edit an item template (`ItemTemplateInput`). Null fields
/// are not sent.
class ItemTemplateInput {
  const ItemTemplateInput({
    required this.name,
    required this.category,
    this.subcategory,
    this.rarity,
    this.requiresAttunement = false,
    this.costCp,
    this.weightLb,
    this.damageDice,
    this.damageType,
    this.versatileDice,
    this.properties = const [],
    this.rangeNormal,
    this.rangeLong,
    this.armorClassBase,
    this.addDexModifier,
    this.maxDexBonus,
    this.strengthMinimum,
    this.stealthDisadvantage = false,
    this.description = const [],
    this.effects = const [],
    this.modifiers = const [],
  });

  final String name;
  final String category;
  final String? subcategory;
  final String? rarity;
  final bool requiresAttunement;
  final int? costCp;
  final double? weightLb;
  final String? damageDice;
  final String? damageType;
  final String? versatileDice;
  final List<String> properties;
  final int? rangeNormal;
  final int? rangeLong;
  final int? armorClassBase;
  final bool? addDexModifier;
  final int? maxDexBonus;
  final int? strengthMinimum;
  final bool stealthDisadvantage;
  final List<String> description;
  final List<String> effects;

  /// Always sent: an empty list removes the modifiers of an edited template.
  final List<ItemModifier> modifiers;

  Map<String, dynamic> toJson() => {
    'name': name,
    'category': category,
    'subcategory': ?subcategory,
    'rarity': ?rarity,
    'requiresAttunement': requiresAttunement,
    'costCp': ?costCp,
    'weightLb': ?weightLb,
    'damageDice': ?damageDice,
    'damageType': ?damageType,
    'versatileDice': ?versatileDice,
    'properties': properties,
    'rangeNormal': ?rangeNormal,
    'rangeLong': ?rangeLong,
    'armorClassBase': ?armorClassBase,
    'addDexModifier': ?addDexModifier,
    'maxDexBonus': ?maxDexBonus,
    'strengthMinimum': ?strengthMinimum,
    'stealthDisadvantage': stealthDisadvantage,
    'description': description,
    'effects': effects,
    'modifiers': [for (final m in modifiers) m.toJson()],
  };
}

/// Rarities as the API spells them, with their Spanish label.
const itemRarities = <String, String>{
  'Common': 'Común',
  'Uncommon': 'Poco común',
  'Rare': 'Rara',
  'VeryRare': 'Muy rara',
  'Legendary': 'Legendaria',
  'Artifact': 'Artefacto',
  'Varies': 'Variable',
};

/// Whether the equip action makes sense for [item] (the server has the last
/// word and answers 400 otherwise): weapons, armor, shields and magic items,
/// plus any other non-consumable item that does something while worn
/// (modifiers, effects, armor, damage or attunement), such as custom items
/// without an equipment category.
bool canEquip(EffectiveItem item) {
  const equippable = {'weapon', 'armor', 'shield', 'magicitem'};
  if (equippable.contains(item.category.toLowerCase())) return true;
  if (item.isConsumable) return false;
  return item.modifiers.isNotEmpty ||
      item.effects.isNotEmpty ||
      item.armor != null ||
      item.damage != null ||
      item.requiresAttunement;
}
