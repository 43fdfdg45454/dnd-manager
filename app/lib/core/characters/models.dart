// Core models of the characters API: what every game system shares. The
// server sends one flat JSON per character (core fields and the fields of the
// game system at the same level), so the core models keep the whole JSON in
// `raw` and each game system reads its own part from there (for D&D 5e,
// `Dnd5eCharacter.fromDetail`). Parsers are tolerant: missing fields fall back
// to neutral values so a slightly different server shape does not break.

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

DateTime? _date(Object? value) => value is String ? DateTime.tryParse(value) : null;

List<T> _objects<T>(Object? value, T Function(Map<String, dynamic>) parse) {
  if (value is! List) return const [];
  return [
    for (final e in value)
      if (e is Map) parse(Map<String, dynamic>.from(e)),
  ];
}

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

enum ChangeRequestType {
  activate('Activate', 'Activación'),
  editSheet('EditSheet', 'Edición de hoja'),
  addItem('AddItem', 'Añadir objeto'),
  removeItem('RemoveItem', 'Quitar objeto'),
  customItem('CustomItem', 'Objeto personalizado'),
  adjustMoney('AdjustMoney', 'Ajuste de dinero'),
  other('Other', 'Otro'),
  companion('Companion', 'Compañero animal');

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

/// One character of a campaign list (`CharacterSummaryDto`). The roster line
/// of the game system (race, classes, level, hit points) stays in [raw].
class CharacterSummary {
  const CharacterSummary({
    required this.id,
    required this.campaignId,
    this.ownerUserId,
    this.ownerDisplayName,
    required this.name,
    required this.status,
    this.portraitUrl,
    this.raw = const {},
  });

  factory CharacterSummary.fromJson(Map<String, dynamic> json) => CharacterSummary(
    id: _str(json['id']),
    campaignId: _str(json['campaignId']),
    ownerUserId: _strOrNull(json['ownerUserId']),
    ownerDisplayName: _strOrNull(json['ownerDisplayName']),
    name: _str(json['name']),
    status: CharacterStatus.fromApi(json['status']),
    portraitUrl: _strOrNull(json['portraitUrl']),
    raw: json,
  );

  final String id;
  final String campaignId;

  /// Null for NPCs.
  final String? ownerUserId;
  final String? ownerDisplayName;
  final String name;
  final CharacterStatus status;
  final String? portraitUrl;

  /// The JSON as it arrived, with the fields of the game system.
  final Map<String, dynamic> raw;
}

// ---------------------------------------------------------------------------
// Rests
// ---------------------------------------------------------------------------

/// Kind of rest a player asks the DM for.
enum RestKind {
  short('short', 'Short', 'corto'),
  long('long', 'Long', 'largo');

  const RestKind(this.requestValue, this.apiValue, this.label);

  /// Value of `kind` in the request body.
  final String requestValue;

  /// Value the server answers with.
  final String apiValue;

  /// "corto" / "largo", to complete "descanso ...".
  final String label;

  static RestKind fromApi(Object? value) =>
      _enumFromApi(values, (e) => e.apiValue, value, RestKind.short);
}

Map<String, int> _hitDice(Object? value) {
  final dice = _map(value) ?? const {};
  return {
    for (final e in dice.entries)
      if ((_int(e.value) ?? 0) > 0) e.key: _int(e.value)!,
  };
}

/// The rest a player asked for and the DM has not resolved yet (`PendingRestDto`).
class PendingRest {
  const PendingRest({
    required this.id,
    required this.kind,
    this.hitDice = const {},
    this.requestedAt,
  });

  factory PendingRest.fromJson(Map<String, dynamic> json) => PendingRest(
    id: _str(json['id']),
    kind: RestKind.fromApi(json['kind']),
    hitDice: _hitDice(json['hitDice']),
    requestedAt: _date(json['requestedAt']),
  );

  final String id;
  final RestKind kind;

  /// Hit dice to spend per class index (short rest only).
  final Map<String, int> hitDice;
  final DateTime? requestedAt;

  /// Total hit dice to spend.
  int get diceCount => hitDice.values.fold(0, (sum, n) => sum + n);

  /// "descanso corto (2 dados)" / "descanso largo".
  String get description {
    final base = 'descanso ${kind.label}';
    if (kind == RestKind.long || diceCount == 0) return base;
    return '$base ($diceCount ${diceCount == 1 ? 'dado' : 'dados'})';
  }
}

/// A rest request with the names of the people involved (`RestRequestDto`).
class RestRequest {
  const RestRequest({
    required this.id,
    this.campaignId = '',
    required this.characterId,
    this.characterName = '',
    this.requestedByDisplayName = '',
    required this.kind,
    this.hitDice = const {},
    this.status = 'Pending',
    this.requestedAt,
    this.comment,
  });

  factory RestRequest.fromJson(Map<String, dynamic> json) => RestRequest(
    id: _str(json['id']),
    campaignId: _str(json['campaignId']),
    characterId: _str(json['characterId']),
    characterName: _str(json['characterName']),
    requestedByDisplayName: _str(json['requestedByDisplayName']),
    kind: RestKind.fromApi(json['kind']),
    hitDice: _hitDice(json['hitDice']),
    status: _str(json['status'], 'Pending'),
    requestedAt: _date(json['requestedAt']),
    comment: _strOrNull(json['comment']),
  );

  final String id;
  final String campaignId;
  final String characterId;
  final String characterName;
  final String requestedByDisplayName;
  final RestKind kind;

  /// Hit dice to spend per class index (short rest only).
  final Map<String, int> hitDice;

  /// Pending, Approved, Rejected or Cancelled.
  final String status;
  final DateTime? requestedAt;
  final String? comment;

  bool get isPending => status == 'Pending';

  /// Total hit dice to spend.
  int get diceCount => hitDice.values.fold(0, (sum, n) => sum + n);

  /// "descanso corto (2 dados)" / "descanso largo".
  String get description =>
      PendingRest(id: id, kind: kind, hitDice: hitDice, requestedAt: requestedAt).description;
}

// ---------------------------------------------------------------------------
// Detail
// ---------------------------------------------------------------------------

/// One character as the core sees it (`CharacterCoreDto` fields of the
/// detail): identity, owner, state, portrait, free texts, height and weight,
/// money and the pending requests. The sheet of the game system stays in
/// [raw]; D&D 5e reads it with `Dnd5eCharacter.fromDetail`.
class CharacterDetail {
  const CharacterDetail({
    required this.id,
    required this.campaignId,
    this.ownerUserId,
    this.ownerDisplayName,
    required this.name,
    required this.status,
    this.copperPieces = 0,
    this.notes = '',
    this.backstory = '',
    this.personalityTraits = '',
    this.ideals = '',
    this.bonds = '',
    this.flaws = '',
    this.backgroundDetail = '',
    this.heightInches,
    this.weightPounds,
    this.portraitUrl,
    this.pendingChangeRequests = const [],
    this.pendingRest,
    this.createdAt,
    this.updatedAt,
    this.raw = const {},
  });

  factory CharacterDetail.fromJson(Map<String, dynamic> json) {
    final pendingRest = _map(json['pendingRest']);
    return CharacterDetail(
      id: _str(json['id']),
      campaignId: _str(json['campaignId']),
      ownerUserId: _strOrNull(json['ownerUserId']),
      ownerDisplayName: _strOrNull(json['ownerDisplayName']),
      name: _str(json['name']),
      status: CharacterStatus.fromApi(json['status']),
      copperPieces: _int(json['copperPieces']) ?? 0,
      notes: _str(json['notes']),
      backstory: _str(json['backstory']),
      personalityTraits: _str(json['personalityTraits']),
      ideals: _str(json['ideals']),
      bonds: _str(json['bonds']),
      flaws: _str(json['flaws']),
      backgroundDetail: _str(json['backgroundDetail']),
      heightInches: _int(json['heightInches']),
      weightPounds: _int(json['weightPounds']),
      portraitUrl: _strOrNull(json['portraitUrl']),
      pendingChangeRequests: _objects(json['pendingChangeRequests'], ChangeRequest.fromJson),
      pendingRest: pendingRest == null ? null : PendingRest.fromJson(pendingRest),
      createdAt: _date(json['createdAt']),
      updatedAt: _date(json['updatedAt']),
      raw: json,
    );
  }

  final String id;
  final String campaignId;
  final String? ownerUserId;
  final String? ownerDisplayName;
  final String name;
  final CharacterStatus status;

  /// Total money in the smallest unit of the game system (copper in D&D 5e).
  final int copperPieces;
  final String notes;
  final String backstory;

  /// Personality (phase 22): traits (one per line), ideal, bond, flaw and the
  /// result of the optional table of the background ("Especialidad: …").
  final String personalityTraits;
  final String ideals;
  final String bonds;
  final String flaws;
  final String backgroundDetail;

  /// True when any personality field has text.
  bool get hasPersonality =>
      [personalityTraits, ideals, bonds, flaws, backgroundDetail].any((t) => t.trim().isNotEmpty);

  /// Height in inches and weight in pounds (phase 29); null when not given.
  /// Free data without mechanical effect.
  final int? heightInches;
  final int? weightPounds;
  final String? portraitUrl;
  final List<ChangeRequest> pendingChangeRequests;

  /// Rest the owner asked the DM for and is waiting on, or null.
  final PendingRest? pendingRest;
  final DateTime? createdAt;
  final DateTime? updatedAt;

  /// The JSON as it arrived, with the fields of the game system (the sheet).
  final Map<String, dynamic> raw;

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
    this.before,
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
    before: _map(json['before']),
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

  /// What would change; its shape depends on [type] (and, for sheet edits,
  /// on the game system).
  final Map<String, dynamic> payload;

  /// What [payload] changes as it was when the request was made (same shape
  /// as the payload for sheet edits; `quantity`, `copperPieces` or the catalog
  /// `template` for inventory requests). Null for old requests.
  final Map<String, dynamic>? before;
  final ChangeRequestStatus status;
  final String? resolvedByDisplayName;
  final DateTime? resolvedAt;
  final String? comment;
  final DateTime? createdAt;

  bool get isPending => status == ChangeRequestStatus.pending;
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
