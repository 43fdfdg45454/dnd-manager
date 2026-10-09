import '../../catalog/data/models.dart' show ItemArmor, ItemDamage, ItemModifier;
import '../../characters/data/models.dart' show ChangeRequest;

// Hand-written models for the items, inventory and shops API (phase 5).
// Parsers are tolerant: missing fields fall back to neutral values. Enums travel
// as text ("Weapon", "VeryRare", "Purchase") and are kept as strings, except the
// transaction type.

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

bool _bool(Object? value, [bool fallback = false]) {
  if (value is bool) return value;
  if (value == 'true') return true;
  if (value == 'false') return false;
  return fallback;
}

bool? _boolOrNull(Object? value) => value == null ? null : _bool(value);

DateTime? _date(Object? value) => value is String ? DateTime.tryParse(value) : null;

List<String> _strList(Object? value) => value is List
    ? [
        for (final e in value)
          if (e != null) _str(e),
      ]
    : const [];

List<String>? _strListOrNull(Object? value) => value is List ? _strList(value) : null;

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) {
  if (value is! List) return const [];
  return [
    for (final e in value)
      if (e is Map) parse(Map<String, dynamic>.from(e)),
  ];
}

// ---------------------------------------------------------------------------
// Overrides and effective item
// ---------------------------------------------------------------------------

/// Item fields that replace the ones of the template. Only the defined (non
/// null) fields are sent and received.
class ItemOverrides {
  const ItemOverrides({
    this.name,
    this.description,
    this.category,
    this.damageDice,
    this.damageType,
    this.versatileDice,
    this.properties,
    this.rangeNormal,
    this.rangeLong,
    this.armorClassBase,
    this.addDexModifier,
    this.maxDexBonus,
    this.strengthMinimum,
    this.stealthDisadvantage,
    this.weightLb,
    this.rarity,
    this.requiresAttunement,
    this.attackBonus,
    this.damageBonus,
    this.effects,
    this.modifiers,
  });

  factory ItemOverrides.fromJson(Object? raw) {
    final json = _map(raw) ?? const {};
    return ItemOverrides(
      name: _strOrNull(json['name']),
      description: _strListOrNull(json['description']),
      category: _strOrNull(json['category']),
      damageDice: _strOrNull(json['damageDice']),
      damageType: _strOrNull(json['damageType']),
      versatileDice: _strOrNull(json['versatileDice']),
      properties: _strListOrNull(json['properties']),
      rangeNormal: _int(json['rangeNormal']),
      rangeLong: _int(json['rangeLong']),
      armorClassBase: _int(json['armorClassBase']),
      addDexModifier: _boolOrNull(json['addDexModifier']),
      maxDexBonus: _int(json['maxDexBonus']),
      strengthMinimum: _int(json['strengthMinimum']),
      stealthDisadvantage: _boolOrNull(json['stealthDisadvantage']),
      weightLb: _double(json['weightLb']),
      rarity: _strOrNull(json['rarity']),
      requiresAttunement: _boolOrNull(json['requiresAttunement']),
      attackBonus: _int(json['attackBonus']),
      damageBonus: _int(json['damageBonus']),
      effects: _strListOrNull(json['effects']),
      modifiers: json['modifiers'] is List ? ItemModifier.listFromJson(json['modifiers']) : null,
    );
  }

  final String? name;
  final List<String>? description;
  final String? category;
  final String? damageDice;
  final String? damageType;
  final String? versatileDice;
  final List<String>? properties;
  final int? rangeNormal;
  final int? rangeLong;
  final int? armorClassBase;
  final bool? addDexModifier;
  final int? maxDexBonus;
  final int? strengthMinimum;
  final bool? stealthDisadvantage;
  final double? weightLb;
  final String? rarity;
  final bool? requiresAttunement;
  final int? attackBonus;
  final int? damageBonus;
  final List<String>? effects;

  /// Null keeps the template's modifiers; an empty list removes them all.
  final List<ItemModifier>? modifiers;

  /// Only the defined fields. A defined empty `modifiers` list is kept: it
  /// means "remove the template's modifiers".
  Map<String, dynamic> toJson() => {
    'name': ?name,
    'description': ?description,
    'category': ?category,
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
    'weightLb': ?weightLb,
    'rarity': ?rarity,
    'requiresAttunement': ?requiresAttunement,
    'attackBonus': ?attackBonus,
    'damageBonus': ?damageBonus,
    'effects': ?effects,
    if (modifiers != null) 'modifiers': [for (final m in modifiers!) m.toJson()],
  };

  bool get isEmpty => toJson().isEmpty;

  /// Names of the defined fields (the JSON keys).
  Set<String> get definedFields => toJson().keys.toSet();
}

/// The item as the character sees it: template plus overrides, already
/// resolved by the server (`EffectiveItemDto`).
class EffectiveItem {
  const EffectiveItem({
    required this.name,
    this.category = '',
    this.subcategory,
    this.rarity,
    this.requiresAttunement = false,
    this.weightLb,
    this.costCp,
    this.damage,
    this.properties = const [],
    this.rangeNormal,
    this.rangeLong,
    this.armor,
    this.attackBonus = 0,
    this.damageBonus = 0,
    this.effects = const [],
    this.description = const [],
    this.modifiers = const [],
  });

  factory EffectiveItem.fromJson(Object? raw) {
    final json = _map(raw) ?? const {};
    final damageJson = _map(json['damage']);
    final dice = _strOrNull(damageJson?['dice']);
    final armorJson = _map(json['armor']);
    final base = _int(armorJson?['base']);
    final rangeJson = _map(json['range']);
    return EffectiveItem(
      name: _str(json['name']),
      category: _str(json['category']),
      subcategory: _strOrNull(json['subcategory']),
      rarity: _strOrNull(json['rarity']),
      requiresAttunement: _bool(json['requiresAttunement']),
      weightLb: _double(json['weightLb']),
      costCp: _int(json['costCp']),
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
      effects: _strList(json['effects']),
      description: _strList(json['description']),
      modifiers: ItemModifier.listFromJson(json['modifiers']),
    );
  }

  final String name;

  /// Category as the API spells it ("Weapon", "AdventuringGear", ...).
  final String category;
  final String? subcategory;
  final String? rarity;
  final bool requiresAttunement;
  final double? weightLb;

  /// Not part of the contract: kept when the server happens to send it.
  final int? costCp;
  final ItemDamage? damage;
  final List<String> properties;
  final int? rangeNormal;
  final int? rangeLong;
  final ItemArmor? armor;
  final int attackBonus;
  final int damageBonus;
  final List<String> effects;
  final List<String> description;

  /// Structured modifiers (the legacy attack/damage bonuses are already in).
  final List<ItemModifier> modifiers;

  bool get isConsumable => category.toLowerCase() == 'consumable';
}

// ---------------------------------------------------------------------------
// Inventory
// ---------------------------------------------------------------------------

/// One item owned by a character (`CharacterItemDto`).
class CharacterItem {
  const CharacterItem({
    required this.id,
    this.templateId,
    this.templateName,
    this.quantity = 1,
    this.equipped = false,
    this.attuned = false,
    this.charges,
    this.chargesMax,
    this.notes,
    this.sortOrder = 0,
    this.overrides = const ItemOverrides(),
    required this.effective,
    this.isCustom = false,
  });

  factory CharacterItem.fromJson(Map<String, dynamic> json) {
    final overrides = ItemOverrides.fromJson(json['overrides']);
    final templateId = _strOrNull(json['templateId']);
    return CharacterItem(
      id: _str(json['id']),
      templateId: templateId,
      templateName: _strOrNull(json['templateName']),
      quantity: _int(json['quantity']) ?? 1,
      equipped: _bool(json['equipped']),
      attuned: _bool(json['attuned']),
      charges: _int(json['charges']),
      chargesMax: _int(json['chargesMax']),
      notes: _strOrNull(json['notes']),
      sortOrder: _int(json['sortOrder']) ?? 0,
      overrides: overrides,
      effective: EffectiveItem.fromJson(json['effective']),
      isCustom: _bool(json['isCustom'], templateId == null || !overrides.isEmpty),
    );
  }

  final String id;
  final String? templateId;
  final String? templateName;
  final int quantity;
  final bool equipped;
  final bool attuned;
  final int? charges;
  final int? chargesMax;
  final String? notes;
  final int sortOrder;

  /// Only the fields that differ from the template.
  final ItemOverrides overrides;
  final EffectiveItem effective;

  /// No template, or the template is partly replaced by overrides.
  final bool isCustom;

  bool get hasOverrides => !overrides.isEmpty;

  /// True when the list should flag the item as customised.
  bool get isMarked => isCustom || hasOverrides;

  CharacterItem copyWith({
    int? quantity,
    bool? equipped,
    bool? attuned,
    int? charges,
    String? notes,
  }) => CharacterItem(
    id: id,
    templateId: templateId,
    templateName: templateName,
    quantity: quantity ?? this.quantity,
    equipped: equipped ?? this.equipped,
    attuned: attuned ?? this.attuned,
    charges: charges ?? this.charges,
    chargesMax: chargesMax,
    notes: notes ?? this.notes,
    sortOrder: sortOrder,
    overrides: overrides,
    effective: effective,
    isCustom: isCustom,
  );
}

/// `InventoryDto`: items, money and weight totals.
class Inventory {
  const Inventory({
    this.items = const [],
    this.copperPieces = 0,
    this.totalWeightLb = 0,
    this.carryCapacityLb = 0,
    this.attunedCount = 0,
  });

  factory Inventory.fromJson(Map<String, dynamic> json) {
    final items = _objects(json['items'], CharacterItem.fromJson);
    return Inventory(
      items: items,
      copperPieces: _int(json['copperPieces']) ?? 0,
      totalWeightLb: _double(json['totalWeightLb']) ?? 0,
      carryCapacityLb: _double(json['carryCapacityLb']) ?? 0,
      attunedCount: _int(json['attunedCount']) ?? items.where((i) => i.attuned).length,
    );
  }

  final List<CharacterItem> items;
  final int copperPieces;
  final double totalWeightLb;
  final double carryCapacityLb;
  final int attunedCount;
}

/// Maximum number of attuned items (SRD rule).
const maxAttunedItems = 3;

/// Body of `PATCH /characters/{id}/inventory/{itemId}`; only present fields are sent.
class InventoryPatch {
  const InventoryPatch({
    this.equipped,
    this.attuned,
    this.replaceAttunedItemId,
    this.notes,
    this.sortOrder,
    this.charges,
  });

  final bool? equipped;
  final bool? attuned;

  /// With `attuned: true` at the attunement limit: the attuned item to drop in
  /// the same operation.
  final String? replaceAttunedItemId;
  final String? notes;
  final int? sortOrder;
  final int? charges;

  Map<String, dynamic> toJson() => {
    'equipped': ?equipped,
    'attuned': ?attuned,
    'replaceAttunedItemId': ?replaceAttunedItemId,
    'notes': ?notes,
    'sortOrder': ?sortOrder,
    'charges': ?charges,
  };
}

/// Outcome of an inventory write: applied (200/201/204) or waiting for the DM (202).
sealed class InventoryWriteResult {
  const InventoryWriteResult();
}

class InventoryApplied extends InventoryWriteResult {
  const InventoryApplied();
}

class InventoryPending extends InventoryWriteResult {
  const InventoryPending(this.changeRequest);

  final ChangeRequest changeRequest;
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

// ---------------------------------------------------------------------------
// Shops
// ---------------------------------------------------------------------------

class ShopSummary {
  const ShopSummary({
    required this.id,
    required this.name,
    this.description,
    this.isOpen = false,
    this.buybackPercent = 50,
    this.itemCount,
  });

  factory ShopSummary.fromJson(Map<String, dynamic> json) => ShopSummary(
    id: _str(json['id']),
    name: _str(json['name']),
    description: _strOrNull(json['description']),
    isOpen: _bool(json['isOpen']),
    buybackPercent: _int(json['buybackPercent']) ?? 50,
    itemCount: _int(json['itemCount']),
  );

  final String id;
  final String name;
  final String? description;
  final bool isOpen;
  final int buybackPercent;

  /// Only when the server sends it.
  final int? itemCount;
}

class ShopItem {
  const ShopItem({
    required this.id,
    this.templateId,
    required this.priceCp,
    this.stock,
    required this.effective,
  });

  factory ShopItem.fromJson(Map<String, dynamic> json) => ShopItem(
    id: _str(json['id']),
    templateId: _strOrNull(json['templateId']),
    priceCp: _int(json['priceCp']) ?? 0,
    stock: _int(json['stock']),
    effective: EffectiveItem.fromJson(json['effective']),
  );

  final String id;
  final String? templateId;
  final int priceCp;

  /// Null means unlimited.
  final int? stock;
  final EffectiveItem effective;

  bool get isUnlimited => stock == null;
  bool get soldOut => stock != null && stock! <= 0;

  ShopItem copyWith({int? stock}) => ShopItem(
    id: id,
    templateId: templateId,
    priceCp: priceCp,
    stock: stock ?? this.stock,
    effective: effective,
  );
}

class Shop extends ShopSummary {
  const Shop({
    required super.id,
    required super.name,
    super.description,
    super.isOpen,
    super.buybackPercent,
    this.items = const [],
  }) : super(itemCount: null);

  factory Shop.fromJson(Map<String, dynamic> json) {
    final summary = ShopSummary.fromJson(json);
    return Shop(
      id: summary.id,
      name: summary.name,
      description: summary.description,
      isOpen: summary.isOpen,
      buybackPercent: summary.buybackPercent,
      items: _objects(json['items'], ShopItem.fromJson),
    );
  }

  final List<ShopItem> items;
}

/// Body of `PATCH /shops/{id}`; only present fields are sent.
class ShopPatch {
  const ShopPatch({this.name, this.description, this.isOpen, this.buybackPercent});

  final String? name;
  final String? description;
  final bool? isOpen;
  final int? buybackPercent;

  Map<String, dynamic> toJson() => {
    'name': ?name,
    'description': ?description,
    'isOpen': ?isOpen,
    'buybackPercent': ?buybackPercent,
  };
}

/// Body of `POST /shops/{id}/items`.
class ShopItemInput {
  const ShopItemInput({
    this.templateId,
    this.overrides = const ItemOverrides(),
    required this.priceCp,
    this.stock,
  });

  final String? templateId;
  final ItemOverrides overrides;
  final int priceCp;

  /// Null means unlimited.
  final int? stock;

  Map<String, dynamic> toJson() => {
    'templateId': ?templateId,
    if (!overrides.isEmpty) 'overrides': overrides.toJson(),
    'priceCp': priceCp,
    'stock': ?stock,
  };
}

/// One entry of `POST /shops/{id}/items/bulk`: a catalog item, with the
/// template's list price when [priceCp] is null and unlimited stock when
/// [stock] is null.
class BulkShopItem {
  const BulkShopItem({required this.templateId, this.priceCp, this.stock});

  final String templateId;
  final int? priceCp;
  final int? stock;

  Map<String, dynamic> toJson() => {'templateId': templateId, 'priceCp': ?priceCp, 'stock': ?stock};
}

/// Body of `PATCH /shops/{id}/items/{shopItemId}`. [unlimitedStock] sends an
/// explicit `stock: null`.
class ShopItemPatch {
  const ShopItemPatch({this.priceCp, this.stock, this.unlimitedStock = false, this.sortOrder});

  final int? priceCp;
  final int? stock;
  final bool unlimitedStock;
  final int? sortOrder;

  Map<String, dynamic> toJson() => {
    'priceCp': ?priceCp,
    if (unlimitedStock) 'stock': null else 'stock': ?stock,
    'sortOrder': ?sortOrder,
  };
}

// ---------------------------------------------------------------------------
// Transactions
// ---------------------------------------------------------------------------

enum TransactionType {
  purchase('Purchase', 'Compra'),
  sale('Sale', 'Venta'),
  stashAdd('StashAdd', 'Botín añadido'),
  stashRemove('StashRemove', 'Botín retirado'),
  stashTake('StashTake', 'Tomado del botín'),
  stashReturn('StashReturn', 'Devuelto al botín'),
  stashGoldAdd('StashGoldAdd', 'Oro del grupo'),
  stashGoldSplit('StashGoldSplit', 'Reparto de oro');

  const TransactionType(this.apiValue, this.label);

  final String apiValue;
  final String label;

  static TransactionType fromApi(Object? value) => values.firstWhere(
    (t) => t.apiValue.toLowerCase() == _str(value).toLowerCase(),
    orElse: () => TransactionType.purchase,
  );
}

/// One movement of items or money: a purchase or sale between a character and
/// a shop, or a movement of the party stash. Shop and character are empty when
/// they do not apply (for example gold added to the stash by the DM).
class Transaction {
  const Transaction({
    required this.id,
    this.shopId,
    this.shopName = '',
    this.characterId,
    this.characterName = '',
    this.actorUserId,
    this.actorDisplayName = '',
    required this.type,
    required this.itemName,
    this.quantity = 1,
    this.totalCp = 0,
    this.at,
  });

  factory Transaction.fromJson(Map<String, dynamic> json) => Transaction(
    id: _str(json['id']),
    shopId: _strOrNull(json['shopId']),
    shopName: _str(json['shopName']),
    characterId: _strOrNull(json['characterId']),
    characterName: _str(json['characterName']),
    actorUserId: _strOrNull(json['actorUserId']),
    actorDisplayName: _str(json['actorDisplayName']),
    type: TransactionType.fromApi(json['type']),
    itemName: _str(json['itemName']),
    quantity: _int(json['quantity']) ?? 1,
    totalCp: _int(json['totalCp']) ?? 0,
    at: _date(json['at']),
  );

  final String id;
  final String? shopId;
  final String shopName;
  final String? characterId;
  final String characterName;

  /// Who did it (the buyer, the DM who moved the loot...).
  final String? actorUserId;
  final String actorDisplayName;
  final TransactionType type;
  final String itemName;
  final int quantity;
  final int totalCp;
  final DateTime? at;
}

/// `{ inventory, transaction }` returned by buy and sell.
class TradeResult {
  const TradeResult({required this.inventory, required this.transaction});

  factory TradeResult.fromJson(Map<String, dynamic> json) => TradeResult(
    inventory: Inventory.fromJson(_map(json['inventory']) ?? const {}),
    transaction: Transaction.fromJson(_map(json['transaction']) ?? const {}),
  );

  final Inventory inventory;
  final Transaction transaction;
}
