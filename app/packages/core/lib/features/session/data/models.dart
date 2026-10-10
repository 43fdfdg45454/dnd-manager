import '../../items/data/models.dart' show EffectiveItem;

// Hand-written models of the table endpoints (phase 12): the party stash and
// the secret messages (the party seen by the DM is D&D 5e, in
// `systems/dnd5e/session/party_models.dart`). Parsers are tolerant: missing
// fields fall back to neutral values.

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
