import '../../characters/data/models.dart'
    show CharacterClass, CharacterCondition, PendingRest, SpellSlot;
import '../../items/data/models.dart' show EffectiveItem;

// Hand-written models of the table endpoints (phase 12): the party seen by the
// DM, the party stash and the secret messages. Parsers are tolerant: missing
// fields fall back to neutral values.

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

bool _bool(Object? value) => value == true || value == 'true';

DateTime? _date(Object? value) => value is String ? DateTime.tryParse(value) : null;

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) {
  if (value is! List) return const [];
  return [
    for (final e in value)
      if (e is Map) parse(Map<String, dynamic>.from(e)),
  ];
}

// ---------------------------------------------------------------------------
// Party
// ---------------------------------------------------------------------------

/// One active character as the DM sees it at the table (`PartyMemberDto`).
class PartyMember {
  const PartyMember({
    required this.id,
    required this.name,
    this.ownerUserId,
    this.ownerDisplayName,
    this.portraitUrl,
    this.classes = const [],
    this.level = 0,
    this.hitPointsCurrent = 0,
    this.hitPointsMax = 0,
    this.temporaryHitPoints = 0,
    this.armorClass = 10,
    this.initiative = 0,
    this.passivePerception = 10,
    this.speed = 30,
    this.conditions = const [],
    this.exhaustionLevel = 0,
    this.deathSaveSuccesses = 0,
    this.deathSaveFailures = 0,
    this.concentratingOnSpellIndex,
    this.inspiration = false,
    this.spellSlots = const [],
    this.pactSlots,
    this.pendingRest,
    this.pendingLevelUpTo,
  });

  factory PartyMember.fromJson(Map<String, dynamic> json) {
    final classes = _objects(json['classes'], CharacterClass.fromJson);
    final pact = _map(json['pactSlots']);
    return PartyMember(
      id: _str(json['id']),
      name: _str(json['name']),
      ownerUserId: _strOrNull(json['ownerUserId']),
      ownerDisplayName: _strOrNull(json['ownerDisplayName']),
      portraitUrl: _strOrNull(json['portraitUrl']),
      classes: classes,
      level: _int(json['level']) ?? classes.fold<int>(0, (sum, c) => sum + c.level),
      hitPointsCurrent: _int(json['hitPointsCurrent']) ?? 0,
      hitPointsMax: _int(json['hitPointsMax']) ?? 0,
      temporaryHitPoints: _int(json['temporaryHitPoints']) ?? 0,
      armorClass: _int(json['armorClass']) ?? 10,
      initiative: _int(json['initiative']) ?? 0,
      passivePerception: _int(json['passivePerception']) ?? 10,
      speed: _int(json['speed']) ?? 30,
      conditions: _objects(json['conditions'], CharacterCondition.fromJson),
      exhaustionLevel: _int(json['exhaustionLevel']) ?? 0,
      deathSaveSuccesses: _int(json['deathSaveSuccesses']) ?? 0,
      deathSaveFailures: _int(json['deathSaveFailures']) ?? 0,
      concentratingOnSpellIndex: _strOrNull(json['concentratingOnSpellIndex']),
      inspiration: _bool(json['inspiration']),
      spellSlots: _objects(json['spellSlots'], SpellSlot.fromJson),
      pactSlots: pact == null ? null : SpellSlot.fromJson(pact),
      pendingRest: _map(json['pendingRest']) == null
          ? null
          : PendingRest.fromJson(_map(json['pendingRest'])!),
      pendingLevelUpTo: _int(json['pendingLevelUpTo']),
    );
  }

  final String id;
  final String name;

  /// Null for NPCs.
  final String? ownerUserId;
  final String? ownerDisplayName;
  final String? portraitUrl;
  final List<CharacterClass> classes;
  final int level;
  final int hitPointsCurrent;
  final int hitPointsMax;
  final int temporaryHitPoints;
  final int armorClass;
  final int initiative;
  final int passivePerception;
  final int speed;
  final List<CharacterCondition> conditions;
  final int exhaustionLevel;
  final int deathSaveSuccesses;
  final int deathSaveFailures;
  final String? concentratingOnSpellIndex;
  final bool inspiration;
  final List<SpellSlot> spellSlots;
  final SpellSlot? pactSlots;

  /// Rest the player is asking for, or null.
  final PendingRest? pendingRest;

  /// Level granted by a DM and not taken yet, or null.
  final int? pendingLevelUpTo;

  bool get isDown => hitPointsCurrent <= 0;
}

/// Forced rest declared by the DM.
enum PartyRestKind {
  short('short', 'Descanso corto'),
  long('long', 'Descanso largo');

  const PartyRestKind(this.apiValue, this.label);

  final String apiValue;
  final String label;
}

/// Quick change of the DM to one character (`PartyAdjustment`). Null fields do
/// not change.
class PartyAdjustment {
  const PartyAdjustment({
    required this.characterId,
    this.hitPointsDelta,
    this.temporaryHitPoints,
    this.addConditions,
    this.removeConditions,
    this.hitPointsMax,
  });

  final String characterId;

  /// Negative = damage (temporary hit points first), positive = healing.
  final int? hitPointsDelta;

  /// Replaces the current temporary hit points.
  final int? temporaryHitPoints;
  final List<CharacterCondition>? addConditions;
  final List<String>? removeConditions;

  /// Overrides the maximum hit points; 0 removes the override.
  final int? hitPointsMax;

  Map<String, dynamic> toJson() => {
    'characterId': characterId,
    'hitPointsDelta': ?hitPointsDelta,
    'temporaryHitPoints': ?temporaryHitPoints,
    if (addConditions != null) 'addConditions': [for (final c in addConditions!) c.toJson()],
    'removeConditions': ?removeConditions,
    'hitPointsMax': ?hitPointsMax,
  };
}

// ---------------------------------------------------------------------------
// Party stash
// ---------------------------------------------------------------------------

/// One item of the party stash (`PartyStashItemDto`).
class StashItem {
  const StashItem({
    required this.id,
    this.templateId,
    required this.item,
    this.quantity = 1,
    this.notes,
    this.charges,
    this.chargesMax,
    this.addedAt,
    this.addedByDisplayName,
  });

  factory StashItem.fromJson(Map<String, dynamic> json) => StashItem(
    id: _str(json['id']),
    templateId: _strOrNull(json['templateId']),
    item: EffectiveItem.fromJson(json['item']),
    quantity: _int(json['quantity']) ?? 1,
    notes: _strOrNull(json['notes']),
    charges: _int(json['charges']),
    chargesMax: _int(json['chargesMax']),
    addedAt: _date(json['addedAt']),
    addedByDisplayName: _strOrNull(json['addedByDisplayName']),
  );

  final String id;
  final String? templateId;
  final EffectiveItem item;
  final int quantity;
  final String? notes;
  final int? charges;
  final int? chargesMax;
  final DateTime? addedAt;
  final String? addedByDisplayName;
}

/// Shared gold and loot of a campaign (`PartyStashDto`).
class PartyStash {
  const PartyStash({
    this.copperPieces = 0,
    this.playersCanTakeFromStash = false,
    this.items = const [],
  });

  factory PartyStash.fromJson(Map<String, dynamic> json) => PartyStash(
    copperPieces: _int(json['copperPieces']) ?? 0,
    playersCanTakeFromStash: _bool(json['playersCanTakeFromStash']),
    items: _objects(json['items'], StashItem.fromJson),
  );

  final int copperPieces;
  final bool playersCanTakeFromStash;
  final List<StashItem> items;
}

// ---------------------------------------------------------------------------
// Secret messages
// ---------------------------------------------------------------------------

/// A secret message from a DM to the player of a character (`MessageDto`).
class DirectMessage {
  const DirectMessage({
    required this.id,
    required this.campaignId,
    this.senderUserId = '',
    this.senderDisplayName = '',
    this.recipientUserId = '',
    required this.characterId,
    this.characterName = '',
    required this.body,
    this.sentAt,
    this.readAt,
  });

  factory DirectMessage.fromJson(Map<String, dynamic> json) => DirectMessage(
    id: _str(json['id']),
    campaignId: _str(json['campaignId']),
    senderUserId: _str(json['senderUserId']),
    senderDisplayName: _str(json['senderDisplayName']),
    recipientUserId: _str(json['recipientUserId']),
    characterId: _str(json['characterId']),
    characterName: _str(json['characterName']),
    body: _str(json['body']),
    sentAt: _date(json['sentAt']),
    readAt: _date(json['readAt']),
  );

  final String id;
  final String campaignId;
  final String senderUserId;
  final String senderDisplayName;
  final String recipientUserId;
  final String characterId;
  final String characterName;

  /// Markdown text.
  final String body;
  final DateTime? sentAt;
  final DateTime? readAt;

  bool get isRead => readAt != null;
}
