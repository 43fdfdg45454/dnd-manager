// Item enhancements and grafts (phase 35): the feature that lets a character
// enhance concrete inventory entries (`ItemEnhancementFeatureDto`), the
// enhanced entries as the sheet shows them (`CharacterItemEnhancementDto`)
// and the grafts with their body slot (`CharacterGraftDto`).

import 'package:opentrpg_core/core/ui/breakdown.dart' show ValueBreakdown;

Map<String, dynamic> _map(Object? value) =>
    value is Map ? Map<String, dynamic>.from(value) : const {};

String _str(Object? value, [String fallback = '']) => value is String ? value : fallback;

String? _strOrNull(Object? value) => value is String && value.isNotEmpty ? value : null;

int? _int(Object? value) => switch (value) {
  final num v => v.toInt(),
  final String v => int.tryParse(v),
  _ => null,
};

List<String> _strings(Object? value) => [
  if (value is List)
    for (final e in value)
      if (e is String) e,
];

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) => [
  if (value is List)
    for (final e in value)
      if (e is Map) parse(Map<String, dynamic>.from(e)),
];

/// What an enhancement option does to an item (`ItemEffectsDto`).
class ItemEffects {
  const ItemEffects({
    this.appliesTo = const [],
    this.attackBonus = 0,
    this.damageBonus = 0,
    this.armorClassBonus = 0,
    this.addProperties = const [],
    this.damageStep = 0,
    this.damageType,
  });

  factory ItemEffects.fromJson(Map<String, dynamic> json) => ItemEffects(
    appliesTo: _strings(json['appliesTo']),
    attackBonus: _int(json['attackBonus']) ?? 0,
    damageBonus: _int(json['damageBonus']) ?? 0,
    armorClassBonus: _int(json['armorClassBonus']) ?? 0,
    addProperties: _strings(json['addProperties']),
    damageStep: _int(json['damageStep']) ?? 0,
    damageType: _strOrNull(json['damageType']),
  );

  /// "weapon", "armor", "shield".
  final List<String> appliesTo;
  final int attackBonus;
  final int damageBonus;
  final int armorClassBonus;
  final List<String> addProperties;
  final int damageStep;
  final String? damageType;

  /// Whether the option fits an item of [kind] ("weapon", "armor", "shield").
  bool appliesToKind(String kind) => appliesTo.isEmpty || appliesTo.contains(kind);
}

/// An option the feature can apply (`ItemEnhancementOptionDto`).
class ItemEnhancementOption {
  const ItemEnhancementOption({
    required this.index,
    required this.name,
    this.description = const [],
    this.minLevel,
    this.requiresOptions = const [],
    this.available = true,
    this.effects = const ItemEffects(),
  });

  factory ItemEnhancementOption.fromJson(Map<String, dynamic> json) => ItemEnhancementOption(
    index: _str(json['index']),
    name: _str(json['name'], _str(json['index'])),
    description: _strings(json['description']),
    minLevel: _int(json['minLevel']),
    requiresOptions: _strings(json['requiresOptions']),
    available: json['available'] != false,
    effects: ItemEffects.fromJson(_map(json['effects'])),
  );

  final String index;
  final String name;
  final List<String> description;
  final int? minLevel;
  final List<String> requiresOptions;

  /// The class level reaches [minLevel].
  final bool available;
  final ItemEffects effects;
}

/// The item enhancement feature the character has reached.
class ItemEnhancementFeature {
  const ItemEnhancementFeature({
    required this.featureIndex,
    required this.featureName,
    this.classIndex = '',
    this.classLevel = 0,
    this.maxPerItem = 0,
    this.options = const [],
  });

  factory ItemEnhancementFeature.fromJson(Map<String, dynamic> json) => ItemEnhancementFeature(
    featureIndex: _str(json['featureIndex']),
    featureName: _str(json['featureName']),
    classIndex: _str(json['classIndex']),
    classLevel: _int(json['classLevel']) ?? 0,
    maxPerItem: _int(json['maxPerItem']) ?? 0,
    options: _objects(json['options'], ItemEnhancementOption.fromJson),
  );

  final String featureIndex;
  final String featureName;
  final String classIndex;
  final int classLevel;

  /// How many options one item may carry at the class level.
  final int maxPerItem;
  final List<ItemEnhancementOption> options;
}

/// An option applied to an item.
class AppliedEnhancement {
  const AppliedEnhancement({required this.index, required this.name});

  factory AppliedEnhancement.fromJson(Map<String, dynamic> json) =>
      AppliedEnhancement(index: _str(json['index']), name: _str(json['name'], _str(json['index'])));

  final String index;
  final String name;
}

/// An enhanced inventory entry: its options, the fields after them and the
/// breakdowns of what they add (`attack`, `damage`, `armorClass`).
class CharacterItemEnhancement {
  const CharacterItemEnhancement({
    required this.itemId,
    required this.itemName,
    this.options = const [],
    this.damageDice,
    this.damageType,
    this.properties = const [],
    this.breakdowns = const {},
  });

  factory CharacterItemEnhancement.fromJson(Map<String, dynamic> json) => CharacterItemEnhancement(
    itemId: _str(json['itemId']),
    itemName: _str(json['itemName']),
    options: _objects(json['options'], AppliedEnhancement.fromJson),
    damageDice: _strOrNull(json['damageDice']),
    damageType: _strOrNull(json['damageType']),
    properties: _strings(json['properties']),
    breakdowns: {
      for (final e in _map(json['breakdowns']).entries)
        if (ValueBreakdown.maybeFromJson(e.value) case final ValueBreakdown b) e.key: b,
    },
  );

  final String itemId;
  final String itemName;
  final List<AppliedEnhancement> options;
  final String? damageDice;
  final String? damageType;
  final List<String> properties;
  final Map<String, ValueBreakdown> breakdowns;
}

/// A graft of the inventory with its body slot (one equipped per slot).
class CharacterGraft {
  const CharacterGraft({
    required this.itemId,
    required this.name,
    required this.slot,
    this.equipped = false,
  });

  factory CharacterGraft.fromJson(Map<String, dynamic> json) => CharacterGraft(
    itemId: _str(json['itemId']),
    name: _str(json['name']),
    slot: _str(json['slot']),
    equipped: json['equipped'] == true,
  );

  final String itemId;
  final String name;
  final String slot;
  final bool equipped;
}
