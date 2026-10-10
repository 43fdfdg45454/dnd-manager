import '../../../core/characters/models.dart' show ChangeRequest;

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
// Catalog item (template) summary
// ---------------------------------------------------------------------------

/// One item template of the catalog (SRD, content pack or campaign homebrew)
/// as the lists show it. The fields of the game system (D&D 5e: rarity,
/// attunement) stay in [raw].
class ItemSummary {
  const ItemSummary({
    required this.id,
    required this.name,
    this.index,
    this.category,
    this.subcategory,
    this.costCp,
    this.weightLb,
    this.source,
    this.raw = const {},
  });

  factory ItemSummary.fromJson(Map<String, dynamic> json) => ItemSummary(
    id: _str(json['id']),
    index: _strOrNull(json['index']),
    name: _str(json['name'], _str(json['index'])),
    category: _strOrNull(json['category']),
    subcategory: _strOrNull(json['subcategory']),
    costCp: _int(json['costCp']),
    weightLb: _double(json['weightLb']),
    source: _strOrNull(json['source']),
    raw: json,
  );

  final String id;
  final String? index;
  final String name;
  final String? category;
  final String? subcategory;

  /// "srd", "homebrew" (campaign item) or the id of a content pack; null when
  /// the server does not say.
  final String? source;

  /// Whether the item belongs to the campaign (and can be edited), as opposed
  /// to coming from the SRD or a content pack.
  bool get isHomebrew => source == null || source == 'homebrew';

  /// Cost in the smallest unit of money; null when unknown.
  final int? costCp;
  final double? weightLb;

  /// The JSON as it arrived, with the fields of the game system.
  final Map<String, dynamic> raw;
}

// ---------------------------------------------------------------------------
// Overrides and effective item
// ---------------------------------------------------------------------------

/// Item fields that replace the ones of the template. Only the defined (non
/// null) fields are sent and received. The core knows the fields every game
/// system shares; the fields of the game system (D&D 5e: damage, armor,
/// properties, rarity, attunement, modifiers...) travel in [system] with
/// their wire names, and the module reads them from there.
class ItemOverrides {
  const ItemOverrides({
    this.name,
    this.description,
    this.category,
    this.weightLb,
    this.effects,
    this.system = const {},
  });

  /// JSON keys of the core fields.
  static const coreFields = {'name', 'description', 'category', 'weightLb', 'effects'};

  factory ItemOverrides.fromJson(Object? raw) {
    final json = _map(raw) ?? const {};
    return ItemOverrides(
      name: _strOrNull(json['name']),
      description: _strListOrNull(json['description']),
      category: _strOrNull(json['category']),
      weightLb: _double(json['weightLb']),
      effects: _strListOrNull(json['effects']),
      system: {
        for (final e in json.entries)
          if (!coreFields.contains(e.key) && e.value != null) e.key: e.value,
      },
    );
  }

  final String? name;
  final List<String>? description;
  final String? category;
  final double? weightLb;
  final List<String>? effects;

  /// The defined fields of the game system, as JSON (`damageDice`,
  /// `modifiers`...). A defined empty `modifiers` list means "remove the
  /// template's modifiers".
  final Map<String, dynamic> system;

  /// Only the defined fields.
  Map<String, dynamic> toJson() => {
    'name': ?name,
    'description': ?description,
    'category': ?category,
    'weightLb': ?weightLb,
    'effects': ?effects,
    for (final e in system.entries)
      if (e.value != null) e.key: e.value,
  };

  bool get isEmpty => toJson().isEmpty;

  /// Names of the defined fields (the JSON keys).
  Set<String> get definedFields => toJson().keys.toSet();
}

/// The item as the character sees it: template plus overrides, already
/// resolved by the server (`EffectiveItemDto`). The fields of the game system
/// stay in [raw] (D&D 5e reads them with `Dnd5eItem.of`).
class EffectiveItem {
  const EffectiveItem({
    required this.name,
    this.category = '',
    this.subcategory,
    this.weightLb,
    this.costCp,
    this.effects = const [],
    this.description = const [],
    this.raw = const {},
  });

  factory EffectiveItem.fromJson(Object? raw) {
    final json = _map(raw) ?? const {};
    return EffectiveItem(
      name: _str(json['name']),
      category: _str(json['category']),
      subcategory: _strOrNull(json['subcategory']),
      weightLb: _double(json['weightLb']),
      costCp: _int(json['costCp']),
      effects: _strList(json['effects']),
      description: _strList(json['description']),
      raw: json,
    );
  }

  final String name;

  /// Category as the API spells it ("Weapon", "AdventuringGear", ...).
  final String category;
  final String? subcategory;
  final double? weightLb;

  /// Not part of the contract: kept when the server happens to send it.
  final int? costCp;
  final List<String> effects;
  final List<String> description;

  /// The JSON as it arrived, with the fields of the game system.
  final Map<String, dynamic> raw;

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
