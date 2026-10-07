import 'package:dnd_companion/features/characters/data/models.dart';
import 'package:dnd_companion/features/items/data/models.dart';
import 'package:dnd_companion/features/session/data/messages_repository.dart';
import 'package:dnd_companion/features/session/data/models.dart';
import 'package:dnd_companion/features/session/data/party_repository.dart';
import 'package:dnd_companion/features/session/data/rest_requests_repository.dart';
import 'package:dnd_companion/features/session/data/stash_repository.dart';

import 'fakes.dart';
import 'item_fakes.dart';

/// A party member as the server would send it: a level 3 Fighter at 20/28 HP.
PartyMember makePartyMember({
  String id = 'ch1',
  String name = 'Thorin',
  String? ownerUserId = 'u1',
  String classIndex = 'fighter',
  int level = 3,
  int hitPointsCurrent = 20,
  int hitPointsMax = 28,
  int temporaryHitPoints = 0,
  List<CharacterCondition> conditions = const [],
  String? concentratingOnSpellIndex,
  int deathSaveSuccesses = 0,
  int deathSaveFailures = 0,
  PendingRest? pendingRest,
  int? pendingLevelUpTo,
}) => PartyMember(
  id: id,
  name: name,
  ownerUserId: ownerUserId,
  classes: [
    CharacterClass(
      classIndex: classIndex,
      className: classIndex[0].toUpperCase() + classIndex.substring(1),
      level: level,
    ),
  ],
  level: level,
  hitPointsCurrent: hitPointsCurrent,
  hitPointsMax: hitPointsMax,
  temporaryHitPoints: temporaryHitPoints,
  armorClass: 17,
  initiative: 2,
  passivePerception: 11,
  conditions: conditions,
  concentratingOnSpellIndex: concentratingOnSpellIndex,
  deathSaveSuccesses: deathSaveSuccesses,
  deathSaveFailures: deathSaveFailures,
  pendingRest: pendingRest,
  pendingLevelUpTo: pendingLevelUpTo,
);

PartyMember _copy(
  PartyMember m, {
  int? hitPointsCurrent,
  int? hitPointsMax,
  int? temporaryHitPoints,
  List<CharacterCondition>? conditions,
  int? pendingLevelUpTo,
}) => PartyMember(
  id: m.id,
  name: m.name,
  ownerUserId: m.ownerUserId,
  classes: m.classes,
  level: m.level,
  hitPointsCurrent: hitPointsCurrent ?? m.hitPointsCurrent,
  hitPointsMax: hitPointsMax ?? m.hitPointsMax,
  temporaryHitPoints: temporaryHitPoints ?? m.temporaryHitPoints,
  armorClass: m.armorClass,
  initiative: m.initiative,
  passivePerception: m.passivePerception,
  conditions: conditions ?? m.conditions,
  concentratingOnSpellIndex: m.concentratingOnSpellIndex,
  deathSaveSuccesses: m.deathSaveSuccesses,
  deathSaveFailures: m.deathSaveFailures,
  pendingRest: m.pendingRest,
  pendingLevelUpTo: pendingLevelUpTo ?? m.pendingLevelUpTo,
);

/// In-memory party of a campaign. Adjustments change the members like the
/// server (damage takes the temporary hit points first).
class FakePartyRepository implements PartyRepository {
  FakePartyRepository({List<PartyMember> members = const []}) : members = [...members];

  final List<PartyMember> members;
  Object? error;
  final List<({PartyRestKind kind, List<String>? characterIds})> rests = [];
  final List<List<PartyAdjustment>> adjustments = [];
  final List<List<String>?> grants = [];
  final List<List<String>?> revokes = [];

  @override
  Future<List<PartyMember>> party(String campaignId) async {
    if (error != null) throw error!;
    return [...members];
  }

  @override
  Future<List<PartyMember>> rest(
    String campaignId,
    PartyRestKind kind, {
    List<String>? characterIds,
  }) async {
    if (error != null) throw error!;
    rests.add((kind: kind, characterIds: characterIds));
    if (kind == PartyRestKind.long) {
      for (var i = 0; i < members.length; i++) {
        final m = members[i];
        if (characterIds == null || characterIds.contains(m.id)) {
          members[i] = _copy(m, hitPointsCurrent: m.hitPointsMax, temporaryHitPoints: 0);
        }
      }
    }
    return [...members];
  }

  @override
  Future<List<PartyMember>> grantLevel(String campaignId, {List<String>? characterIds}) async {
    if (error != null) throw error!;
    grants.add(characterIds);
    for (var i = 0; i < members.length; i++) {
      final m = members[i];
      if ((characterIds == null || characterIds.contains(m.id)) && m.pendingLevelUpTo == null) {
        members[i] = _copy(m, pendingLevelUpTo: m.level + 1);
      }
    }
    return [...members];
  }

  @override
  Future<List<PartyMember>> revokeLevel(String campaignId, {List<String>? characterIds}) async {
    if (error != null) throw error!;
    revokes.add(characterIds);
    return [...members];
  }

  @override
  Future<PartyAdjustResult> adjust(String campaignId, List<PartyAdjustment> list) async {
    if (error != null) throw error!;
    adjustments.add(list);
    for (final a in list) {
      final i = members.indexWhere((m) => m.id == a.characterId);
      if (i < 0) throw dioError(404);
      var m = members[i];
      if (a.temporaryHitPoints != null) m = _copy(m, temporaryHitPoints: a.temporaryHitPoints);
      final delta = a.hitPointsDelta;
      if (delta != null && delta < 0) {
        final absorbed = (-delta).clamp(0, m.temporaryHitPoints);
        final rest = -delta - absorbed;
        m = _copy(
          m,
          temporaryHitPoints: m.temporaryHitPoints - absorbed,
          hitPointsCurrent: (m.hitPointsCurrent - rest).clamp(0, m.hitPointsMax),
        );
      } else if (delta != null) {
        m = _copy(m, hitPointsCurrent: (m.hitPointsCurrent + delta).clamp(0, m.hitPointsMax));
      }
      if (a.hitPointsMax != null && a.hitPointsMax! > 0) m = _copy(m, hitPointsMax: a.hitPointsMax);
      final removed = a.removeConditions ?? const [];
      m = _copy(
        m,
        conditions: [
          for (final c in m.conditions)
            if (!removed.contains(c.index)) c,
          for (final c in a.addConditions ?? const <CharacterCondition>[])
            if (!m.conditions.any((e) => e.index == c.index)) c,
        ],
      );
      members[i] = m;
    }
    return PartyAdjustResult(members: [...members], damage: [...nextDamage]);
  }

  /// Damage outcomes the next adjustments report (concentration saves).
  List<DamageOutcome> nextDamage = [];
}

StashItem makeStashItem({
  String id = 'st1',
  String? templateId = 't-sword',
  String name = 'Longsword',
  String category = 'Weapon',
  int quantity = 1,
  String? notes,
}) => StashItem(
  id: id,
  templateId: templateId,
  item: makeEffective(
    name: name,
    category: category,
    damageDice: category == 'Weapon' ? '1d8' : null,
  ),
  quantity: quantity,
  notes: notes,
);

/// In-memory party stash. Taking and giving back move items between the stash
/// and [inventory] when one is given.
class FakeStashRepository implements StashRepository {
  FakeStashRepository({
    List<StashItem> items = const [],
    this.copperPieces = 0,
    this.playersCanTake = false,
    this.inventory,
  }) : items = [...items];

  final List<StashItem> items;
  int copperPieces;
  bool playersCanTake;
  final FakeInventoryRepository? inventory;
  Object? error;

  final List<({String? templateId, ItemOverrides overrides, int quantity})> added = [];
  final List<({String itemId, int? quantity, String? notes})> updated = [];
  final List<String> removed = [];
  final List<({String itemId, String characterId, int quantity})> taken = [];
  final List<({String characterId, String characterItemId, int quantity})> returned = [];
  final List<int> goldChanges = [];
  final List<List<String>?> splits = [];

  PartyStash get snapshot => PartyStash(
    copperPieces: copperPieces,
    playersCanTakeFromStash: playersCanTake,
    items: [...items],
  );

  void _fail() {
    if (error != null) throw error!;
  }

  StashItem _with(StashItem item, {int? quantity, String? notes}) => StashItem(
    id: item.id,
    templateId: item.templateId,
    item: item.item,
    quantity: quantity ?? item.quantity,
    notes: notes ?? item.notes,
  );

  @override
  Future<PartyStash> get(String campaignId) async {
    _fail();
    return snapshot;
  }

  @override
  Future<StashItem> addItem(
    String campaignId, {
    String? templateId,
    ItemOverrides overrides = const ItemOverrides(),
    int quantity = 1,
    String? notes,
  }) async {
    _fail();
    added.add((templateId: templateId, overrides: overrides, quantity: quantity));
    final item = StashItem(
      id: 'st${items.length + 1}',
      templateId: templateId,
      item: EffectiveItem(name: overrides.name ?? templateId ?? 'Objeto'),
      quantity: quantity,
      notes: notes,
    );
    items.add(item);
    return item;
  }

  @override
  Future<StashItem> updateItem(
    String campaignId,
    String itemId, {
    int? quantity,
    String? notes,
    bool clearNotes = false,
  }) async {
    _fail();
    updated.add((itemId: itemId, quantity: quantity, notes: notes));
    final i = items.indexWhere((e) => e.id == itemId);
    items[i] = _with(items[i], quantity: quantity, notes: notes);
    return items[i];
  }

  @override
  Future<void> removeItem(String campaignId, String itemId) async {
    _fail();
    removed.add(itemId);
    items.removeWhere((e) => e.id == itemId);
  }

  @override
  Future<PartyStash> take(
    String campaignId,
    String itemId, {
    required String characterId,
    int quantity = 1,
  }) async {
    _fail();
    taken.add((itemId: itemId, characterId: characterId, quantity: quantity));
    final i = items.indexWhere((e) => e.id == itemId);
    final item = items[i];
    if (item.quantity <= quantity) {
      items.removeAt(i);
    } else {
      items[i] = _with(item, quantity: item.quantity - quantity);
    }
    inventory?.give(
      characterId,
      makeCharacterItem(
        id: 'from-$itemId',
        templateId: item.templateId,
        quantity: quantity,
        effective: item.item,
      ),
    );
    return snapshot;
  }

  @override
  Future<PartyStash> giveBack(
    String campaignId, {
    required String characterId,
    required String characterItemId,
    int quantity = 1,
  }) async {
    _fail();
    returned.add((characterId: characterId, characterItemId: characterItemId, quantity: quantity));
    final list = inventory?.items[characterId];
    final index = list?.indexWhere((e) => e.id == characterItemId) ?? -1;
    if (list != null && index >= 0) {
      final item = list[index];
      list.removeAt(index);
      items.add(
        StashItem(
          id: 'back-$characterItemId',
          templateId: item.templateId,
          item: item.effective,
          quantity: quantity,
        ),
      );
    }
    return snapshot;
  }

  @override
  Future<PartyStash> adjustGold(String campaignId, int deltaCp) async {
    _fail();
    goldChanges.add(deltaCp);
    copperPieces += deltaCp;
    return snapshot;
  }

  @override
  Future<PartyStash> splitGold(String campaignId, {List<String>? characterIds}) async {
    _fail();
    splits.add(characterIds);
    copperPieces = 0;
    return snapshot;
  }
}

DirectMessage makeMessage({
  String id = 'm1',
  String characterId = 'ch1',
  String characterName = 'Thorin',
  String body = 'Escuchas pasos tras la puerta.',
  bool read = false,
}) => DirectMessage(
  id: id,
  campaignId: 'c1',
  senderUserId: 'owner',
  senderDisplayName: 'Dueña Demo',
  recipientUserId: 'u1',
  characterId: characterId,
  characterName: characterName,
  body: body,
  sentAt: DateTime.utc(2026, 10, 1, 20),
  readAt: read ? DateTime.utc(2026, 10, 1, 21) : null,
);

/// In-memory secret messages of the signed-in user.
class FakeMessagesRepository implements MessagesRepository {
  FakeMessagesRepository({List<DirectMessage> messages = const []}) : messages = [...messages];

  final List<DirectMessage> messages;
  Object? error;
  final List<({List<String> characterIds, String body})> sent = [];
  final List<String> marked = [];

  @override
  Future<List<DirectMessage>> send(
    String campaignId, {
    required List<String> characterIds,
    required String body,
  }) async {
    if (error != null) throw error!;
    sent.add((characterIds: characterIds, body: body));
    return [
      for (final id in characterIds)
        DirectMessage(id: 'sent-$id', campaignId: campaignId, characterId: id, body: body),
    ];
  }

  @override
  Future<List<DirectMessage>> list(String campaignId, {bool? unreadOnly, int? limit}) async {
    if (error != null) throw error!;
    return [
      for (final m in messages)
        if (unreadOnly != true || !m.isRead) m,
    ];
  }

  @override
  Future<int> unreadCount(String campaignId) async => messages.where((m) => !m.isRead).length;

  @override
  Future<DirectMessage> markRead(String messageId) async {
    if (error != null) throw error!;
    marked.add(messageId);
    final i = messages.indexWhere((m) => m.id == messageId);
    final m = messages[i];
    final read = DirectMessage(
      id: m.id,
      campaignId: m.campaignId,
      senderUserId: m.senderUserId,
      senderDisplayName: m.senderDisplayName,
      recipientUserId: m.recipientUserId,
      characterId: m.characterId,
      characterName: m.characterName,
      body: m.body,
      sentAt: m.sentAt,
      readAt: DateTime.utc(2026, 10, 2),
    );
    messages[i] = read;
    return read;
  }
}

/// A pending rest request as the DM's list shows it.
RestRequest makeRestRequest({
  String id = 'rr1',
  String characterId = 'ch1',
  String characterName = 'Thorin',
  RestKind kind = RestKind.short,
  Map<String, int> hitDice = const {'fighter': 2},
  DateTime? requestedAt,
}) => RestRequest(
  id: id,
  campaignId: 'c1',
  characterId: characterId,
  characterName: characterName,
  requestedByDisplayName: 'Usuario Demo',
  kind: kind,
  hitDice: hitDice,
  requestedAt: requestedAt,
);

/// In-memory pending rest requests of a campaign. Approving or rejecting one
/// removes it from the list and records the call.
class FakeRestRequestsRepository implements RestRequestsRepository {
  FakeRestRequestsRepository({List<RestRequest> requests = const []}) : requests = [...requests];

  final List<RestRequest> requests;
  Object? error;
  final List<String> approved = [];
  final List<({String id, String? comment})> rejected = [];

  @override
  Future<List<RestRequest>> pending(String campaignId) async {
    if (error != null) throw error!;
    return [
      for (final r in requests)
        if (r.isPending) r,
    ];
  }

  RestRequest _take(String id) {
    final index = requests.indexWhere((r) => r.id == id);
    if (index < 0) throw dioError(404);
    return requests.removeAt(index);
  }

  @override
  Future<RestRequest> approve(String requestId) async {
    if (error != null) throw error!;
    approved.add(requestId);
    return _take(requestId);
  }

  @override
  Future<RestRequest> reject(String requestId, {String? comment}) async {
    if (error != null) throw error!;
    rejected.add((id: requestId, comment: comment));
    return _take(requestId);
  }
}
