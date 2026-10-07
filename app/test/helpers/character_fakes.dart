import 'dart:convert';

import 'package:dnd_companion/features/characters/data/characters_repository.dart';
import 'package:dnd_companion/features/characters/data/models.dart';

import 'fakes.dart';

/// A character detail as the server would send it. Defaults: a level 3 human
/// Fighter with Str 16 (modifier +3), owned by `u1`.
Map<String, dynamic> makeCharacterJson({
  String id = 'ch1',
  String campaignId = 'c1',
  String? ownerUserId = 'u1',
  String ownerDisplayName = 'Usuario Demo',
  String name = 'Thorin',
  String status = 'Draft',
  List<Map<String, dynamic>> overrides = const [],
  List<String> overriddenFields = const [],
  List<Map<String, dynamic>> pending = const [],
  List<Map<String, dynamic>> proficiencies = const [],
  List<Map<String, dynamic>> spells = const [],
  String notes = '',
  List<Map<String, dynamic>>? classes,
  List<Map<String, dynamic>> spellSlots = const [],
  int hitPointsCurrent = 20,
  int temporaryHitPoints = 3,
  Map<String, dynamic>? combat,
  List<Map<String, dynamic>> itemEffects = const [],
  Map<String, dynamic> breakdowns = const {},
  Map<String, dynamic>? pendingRest,
  int? pendingLevelUpTo,
  bool spellPreparationPending = false,
  String? spellPreparationReason,
  List<Map<String, dynamic>> invalidChoices = const [],
  bool restRollsPending = false,
  List<Map<String, dynamic>> resistances = const [],
  Map<String, dynamic>? breathWeapon,
  List<Map<String, dynamic>> resources = const [],
  String? concentratingOnSpellIndex,
}) => {
  'id': id,
  'campaignId': campaignId,
  'ownerUserId': ownerUserId,
  'ownerDisplayName': ownerDisplayName,
  'name': name,
  'status': status,
  'raceIndex': 'human',
  'raceName': 'Human',
  'applyRacialBonuses': true,
  'hpMode': 'Average',
  'baseStr': 15,
  'baseDex': 14,
  'baseCon': 13,
  'baseInt': 10,
  'baseWis': 12,
  'baseCha': 8,
  'hitPointsCurrent': hitPointsCurrent,
  'temporaryHitPoints': temporaryHitPoints,
  'deathSaveSuccesses': 0,
  'deathSaveFailures': 0,
  'exhaustionLevel': 0,
  'conditions': <Object>[],
  'inspiration': true,
  'copperPieces': 1550,
  'notes': notes,
  'backstory': '',
  'classes':
      classes ??
      [
        {'classIndex': 'fighter', 'className': 'Fighter', 'level': 3},
      ],
  'proficiencies': proficiencies,
  'spells': spells,
  'overrides': overrides,
  'resources': resources,
  'concentratingOnSpellIndex': concentratingOnSpellIndex,
  'spellSlots': spellSlots,
  'combat': ?combat,
  'sheet': {
    'abilities': {
      'str': {'score': 16, 'modifier': 3, 'overridden': false},
      'dex': {'score': 15, 'modifier': 2, 'overridden': false},
      'con': {'score': 14, 'modifier': 2, 'overridden': false},
      'int': {'score': 11, 'modifier': 0, 'overridden': false},
      'wis': {'score': 13, 'modifier': 1, 'overridden': false},
      'cha': {'score': 9, 'modifier': -1, 'overridden': false},
    },
    'proficiencyBonus': 2,
    'savingThrows': {
      'str': {'value': 5, 'proficient': true},
      'dex': {'value': 2, 'proficient': false},
      'con': {'value': 4, 'proficient': true},
      'int': {'value': 0, 'proficient': false},
      'wis': {'value': 1, 'proficient': false},
      'cha': {'value': -1, 'proficient': false},
    },
    'skills': [
      {
        'index': 'athletics',
        'name': 'Athletics',
        'ability': 'str',
        'value': 5,
        'proficient': true,
        'expertise': false,
      },
      {
        'index': 'stealth',
        'name': 'Stealth',
        'ability': 'dex',
        'value': 2,
        'proficient': false,
        'expertise': false,
      },
    ],
    'passivePerception': 11,
    'initiative': 2,
    'armorClass': 17,
    'speed': 30,
    'hitPointsMax': 28,
    'hitDice': [
      {'classIndex': 'fighter', 'die': 10, 'total': 3, 'remaining': 3},
    ],
    'spellcasting': <Object>[],
    'overriddenFields': overriddenFields,
    'itemEffects': itemEffects,
    'breakdowns': breakdowns,
    'resistances': resistances,
    'breathWeapon': ?breathWeapon,
  },
  'pendingChangeRequests': pending,
  'pendingRest': pendingRest,
  'pendingLevelUpTo': pendingLevelUpTo,
  'spellPreparationPending': spellPreparationPending,
  'spellPreparationReason': spellPreparationReason,
  'invalidChoices': invalidChoices,
  'restRollsPending': restRollsPending,
};

/// An `OriginChoiceDto` as JSON: a required one-pick skill choice by default.
Map<String, dynamic> makeOriginChoiceJson({
  String key = 'race.skills',
  String name = 'Habilidades (raza)',
  String kind = 'Skill',
  String source = 'race',
  int choose = 1,
  int? required,
  int? amount,
  bool freeText = false,
  String note = '',
  List<Map<String, dynamic>>? options,
  List<Map<String, dynamic>> selected = const [],
}) => {
  'key': key,
  'name': name,
  'kind': kind,
  'source': source,
  'choose': choose,
  'required': required ?? choose,
  'amount': ?amount,
  'freeText': freeText,
  'note': note,
  'options':
      options ??
      [
        {'index': 'insight', 'name': 'Perspicacia', 'eligible': true, 'description': <String>[]},
        {'index': 'perception', 'name': 'Percepción', 'eligible': true, 'description': <String>[]},
      ],
  'selected': selected,
};

/// An `OriginChoicesDto` as JSON.
Map<String, dynamic> makeOriginChoicesJson(
  List<Map<String, dynamic>> choices, {
  String characterId = 'new1',
  bool complete = false,
}) => {'characterId': characterId, 'complete': complete, 'choices': choices};

/// A `PreparationSpellDto` as JSON.
Map<String, dynamic> makePreparationSpellJson(
  String index,
  String name, {
  int level = 1,
  String category = 'Utility',
  String school = 'Evocation',
}) => {
  'index': index,
  'name': name,
  'level': level,
  'school': school,
  'category': category,
  'concentration': false,
  'ritual': false,
  'castingTime': '1 action',
  'source': 'srd',
};

/// A `SpellPreparationDto` as JSON: one class (a cleric by default) with
/// [candidates] (index, name, level, category) and [max] spells to prepare.
Map<String, dynamic> makePreparationJson({
  bool pending = true,
  String? reason = 'LongRest',
  bool canKeep = true,
  String? keepProblem,
  String classIndex = 'cleric',
  String className = 'Cleric',
  int max = 2,
  List<String> prepared = const [],
  List<Map<String, dynamic>>? candidates,
  List<Map<String, dynamic>> alwaysPrepared = const [],
}) => {
  'pending': pending,
  'reason': reason,
  'canKeep': canKeep,
  'keepProblem': keepProblem,
  'classes': [
    {
      'classIndex': classIndex,
      'className': className,
      'max': max,
      'maxSpellLevel': 1,
      'alwaysPrepared': alwaysPrepared,
      'prepared': prepared,
      'candidates':
          candidates ??
          [
            makePreparationSpellJson('cure-wounds', 'Cure Wounds', category: 'Healing'),
            makePreparationSpellJson('bless', 'Bless', category: 'Buff'),
            makePreparationSpellJson('guiding-bolt', 'Guiding Bolt', category: 'Damage'),
            makePreparationSpellJson('shield-of-faith', 'Shield of Faith', category: 'Defense'),
          ],
    },
  ],
};

/// A `ValueBreakdownDto` as JSON: [parts] are `(source, label, value)`; the
/// total is their sum, like the server's.
Map<String, dynamic> makeBreakdownJson(List<(String, String, int)> parts) => {
  'total': parts.fold<int>(0, (sum, p) => sum + p.$3),
  'parts': [
    for (final (source, label, value) in parts) {'source': source, 'label': label, 'value': value},
  ],
};

/// A `combat` block as the server would send it: a longsword attack, two level
/// 1 slots and a short-rest resource. Pass the lists to override the defaults.
Map<String, dynamic> makeCombatJson({
  List<Map<String, dynamic>>? attacks,
  List<Map<String, dynamic>>? spellSlots,
  Map<String, dynamic>? pactSlots,
  List<Map<String, dynamic>>? resources,
  List<Map<String, dynamic>> quickConsumables = const [],
  List<Map<String, dynamic>> classPanels = const [],
  List<Map<String, dynamic>> onceSinceLongRest = const [],
}) => {
  'attacks':
      attacks ??
      [
        {
          'itemId': 'it1',
          'name': 'Longsword',
          'attackBonus': 5,
          'damage': '1d8+3',
          'damageType': 'Slashing',
          'versatileDamage': '1d10+3',
          'properties': ['Versatile'],
        },
      ],
  'spellSlots':
      spellSlots ??
      [
        {'level': 1, 'max': 2, 'used': 0},
      ],
  'pactSlots': ?pactSlots,
  'resources':
      resources ??
      [
        {
          'id': 'r1',
          'key': 'second-wind',
          'name': 'Second Wind',
          'max': 1,
          'used': 0,
          'recharge': 'ShortRest',
          'isAuto': true,
        },
      ],
  'quickConsumables': quickConsumables,
  'classPanels': classPanels,
  'onceSinceLongRest': onceSinceLongRest,
};

Map<String, dynamic> makeChangeRequestJson({
  String id = 'cr1',
  String campaignId = 'c1',
  String characterId = 'ch1',
  String characterName = 'Thorin',
  String requestedByUserId = 'p2',
  String requestedByDisplayName = 'Beto',
  String type = 'EditSheet',
  Map<String, dynamic> payload = const {'name': 'Thorin II'},
  String status = 'Pending',
}) => {
  'id': id,
  'campaignId': campaignId,
  'characterId': characterId,
  'characterName': characterName,
  'requestedByUserId': requestedByUserId,
  'requestedByDisplayName': requestedByDisplayName,
  'type': type,
  'payload': payload,
  'status': status,
  'createdAt': '2026-10-01T10:00:00Z',
};

ChangeRequest makeChangeRequest({
  String id = 'cr1',
  String requestedByUserId = 'p2',
  String type = 'EditSheet',
  Map<String, dynamic> payload = const {'name': 'Thorin II'},
  String status = 'Pending',
}) => ChangeRequest.fromJson(
  makeChangeRequestJson(
    id: id,
    requestedByUserId: requestedByUserId,
    type: type,
    payload: payload,
    status: status,
  ),
);

/// In-memory characters backend. [isDm] decides whether a sheet edit on an
/// Active character is applied (200) or becomes a change request (202).
class FakeCharactersRepository implements CharactersRepository {
  FakeCharactersRepository({
    List<Map<String, dynamic>> characters = const [],
    List<ChangeRequest> requests = const [],
    this.currentUserId = 'u1',
    this.isDm = false,
  }) : _characters = {for (final c in characters) c['id'] as String: Map.of(c)},
       requests = [...requests];

  final Map<String, Map<String, dynamic>> _characters;
  final List<ChangeRequest> requests;
  final String currentUserId;
  final bool isDm;
  Object? error;

  final List<({String name, ({String? userId})? owner})> created = [];
  final List<SheetPatch> patches = [];
  final List<String> activated = [];
  final List<String> submitted = [];
  final List<String> deleted = [];
  final List<String?> portraits = [];

  void _fail() {
    if (error != null) throw error!;
  }

  Map<String, dynamic> _json(String id) => _characters[id] ?? (throw dioError(404));

  @override
  Future<List<CharacterSummary>> listByCampaign(String campaignId) async {
    _fail();
    return [
      for (final json in _characters.values)
        if (json['campaignId'] == campaignId)
          CharacterSummary.fromJson({
            ...json,
            'hitPointsMax': (json['sheet'] as Map)['hitPointsMax'],
            'hitPointsCurrent': json['hitPointsCurrent'],
          }),
    ];
  }

  @override
  Future<CharacterDetail> create(
    String campaignId, {
    required String name,
    ({String? userId})? owner,
  }) async {
    _fail();
    created.add((name: name, owner: owner));
    final id = 'new${created.length}';
    _characters[id] = makeCharacterJson(
      id: id,
      campaignId: campaignId,
      name: name,
      ownerUserId: owner == null ? currentUserId : owner.userId,
    );
    return CharacterDetail.fromJson(_characters[id]!);
  }

  @override
  Future<CharacterDetail> get(String id) async {
    _fail();
    final json = _json(id);
    return CharacterDetail.fromJson({
      ...json,
      'pendingChangeRequests': [
        for (final r in requests)
          if (r.characterId == id && r.isPending)
            makeChangeRequestJson(
              id: r.id,
              characterId: id,
              type: r.type.apiValue,
              payload: r.payload,
            ),
      ],
    });
  }

  @override
  Future<SheetSaveResult> patchSheet(String id, SheetPatch patch) async {
    _fail();
    patches.add(patch);
    final json = _json(id);
    if (json['status'] == 'Active' && !isDm) {
      final request = makeChangeRequest(
        id: 'cr${requests.length + 1}',
        requestedByUserId: currentUserId,
        payload: patch.toJson(),
      );
      requests.add(request);
      return PendingApproval(request);
    }
    if (patch.name != null) json['name'] = patch.name;
    return Saved(CharacterDetail.fromJson(json));
  }

  @override
  Future<ChangeRequest> submit(String id) async {
    _fail();
    submitted.add(id);
    final request = makeChangeRequest(
      id: 'cr${requests.length + 1}',
      requestedByUserId: currentUserId,
      type: 'Activate',
      payload: const {},
    );
    requests.add(request);
    return request;
  }

  @override
  Future<CharacterDetail> activate(String id) async {
    _fail();
    activated.add(id);
    _json(id)['status'] = 'Active';
    return get(id);
  }

  @override
  Future<CharacterDetail> setPortrait(String id, String? fileId) async {
    _fail();
    portraits.add(fileId);
    _json(id)['portraitUrl'] = fileId == null ? null : '/api/v1/files/$fileId';
    return get(id);
  }

  @override
  Future<void> delete(String id) async {
    _fail();
    deleted.add(id);
    _characters.remove(id);
  }

  /// Owner changes requested with [setOwner] (character id, new owner).
  final List<({String id, String? ownerUserId})> ownerChanges = [];

  @override
  Future<CharacterDetail> setOwner(String id, String? ownerUserId) async {
    _fail();
    ownerChanges.add((id: id, ownerUserId: ownerUserId));
    _json(id)['ownerUserId'] = ownerUserId;
    return get(id);
  }

  // -- Origin choices (phase 19) ----------------------------------------------

  /// Plan answered by `GET /origin-choices` (without `selected`: the answers
  /// saved with the PUT are merged in).
  Map<String, dynamic>? originPlan;

  /// Bodies of every `PUT /origin-choices`, as JSON.
  final List<List<Map<String, dynamic>>> originSaves = [];

  /// Thrown by [saveOriginChoices] when set.
  Object? originSaveError;
  final Map<String, Map<String, dynamic>> _originAnswers = {};

  OriginChoices _originPlanFor(String id) {
    final plan = jsonDecode(jsonEncode(originPlan ?? makeOriginChoicesJson(const [])));
    final choices = [
      for (final c in (plan['choices'] as List))
        {
          ...(c as Map<String, dynamic>),
          if (_originAnswers[c['key']] != null) ..._originAnswers[c['key']]!,
        },
    ];
    return OriginChoices.fromJson({
      ...(plan as Map<String, dynamic>),
      'characterId': id,
      'choices': choices,
    });
  }

  @override
  Future<OriginChoices> originChoices(String id) async {
    _fail();
    _json(id);
    return _originPlanFor(id);
  }

  @override
  Future<OriginChoices> saveOriginChoices(String id, List<LevelUpChoiceAnswer> answers) async {
    _fail();
    originSaves.add([
      for (final a in answers) jsonDecode(jsonEncode(a.toJson())) as Map<String, dynamic>,
    ]);
    if (originSaveError != null) throw originSaveError!;
    for (final a in answers) {
      _originAnswers[a.key] = {
        'selected': [
          for (final i in a.selected) {'index': i, 'name': i},
        ],
        'feat': a.feat == null ? null : {'index': a.feat, 'name': a.feat},
        'ability': a.ability,
      };
    }
    return _originPlanFor(id);
  }

  // -- Invalid choices, damage and rest rolls (phase 19) -------------------------

  /// Plan answered by `GET /invalid-choices` by character id.
  final Map<String, Map<String, dynamic>> invalidPlans = {};

  /// Bodies of `POST /invalid-choices`.
  final List<List<Map<String, dynamic>>> invalidReplacements = [];

  @override
  Future<InvalidChoices> invalidChoices(String id) async {
    _fail();
    final json = invalidPlans[id];
    if (json == null) return InvalidChoices(characterId: id);
    return InvalidChoices.fromJson(jsonDecode(jsonEncode(json)) as Map<String, dynamic>);
  }

  @override
  Future<CharacterDetail> replaceInvalidChoices(
    String id,
    List<LevelUpChoiceAnswer> answers,
  ) async {
    _fail();
    invalidReplacements.add([
      for (final a in answers) jsonDecode(jsonEncode(a.toJson())) as Map<String, dynamic>,
    ]);
    _json(id)['invalidChoices'] = <Object>[];
    invalidPlans.remove(id);
    return get(id);
  }

  /// Amounts of `POST /damage`.
  final List<int> damageCalls = [];

  /// Concentration outcome the next damage reports.
  int? nextConcentrationDc;
  bool nextConcentrationEnded = false;

  @override
  Future<DamageResult> applyDamage(String id, int amount) async {
    _fail();
    damageCalls.add(amount);
    final json = _json(id);
    var temp = json['temporaryHitPoints'] as int? ?? 0;
    var hp = json['hitPointsCurrent'] as int? ?? 0;
    final concentrating = json['concentratingOnSpellIndex'] as String?;
    var rest = amount;
    final absorbed = rest < temp ? rest : temp;
    temp -= absorbed;
    rest -= absorbed;
    hp = (hp - rest).clamp(0, 9999);
    json['temporaryHitPoints'] = temp;
    json['hitPointsCurrent'] = hp;
    if (nextConcentrationEnded) json['concentratingOnSpellIndex'] = null;
    final outcome = DamageOutcome(
      characterId: id,
      damage: amount,
      hitPointsCurrent: hp,
      concentratingOn: concentrating,
      concentrationCheckDc: concentrating == null ? null : nextConcentrationDc,
      concentrationEnded: concentrating != null && nextConcentrationEnded,
    );
    return DamageResult(character: await get(id), outcome: outcome);
  }

  /// Bodies of `POST /resources/{id}/rolls`.
  final List<({String resourceId, List<int> values})> rollSaves = [];

  @override
  Future<CharacterDetail> saveResourceRolls(String id, String resourceId, List<int> values) async {
    _fail();
    rollSaves.add((resourceId: resourceId, values: values));
    final json = _json(id);
    void update(List<dynamic> resources) {
      for (final r in resources) {
        if ((r as Map)['id'] == resourceId) {
          r['rolls'] = values;
          r['rollsPending'] = false;
        }
      }
    }

    update(json['resources'] as List? ?? const []);
    update((json['combat'] as Map?)?['resources'] as List? ?? const []);
    final anyPending = [
      ...(json['resources'] as List? ?? const []),
      ...((json['combat'] as Map?)?['resources'] as List? ?? const []),
    ].any((r) => (r as Map)['rollsPending'] == true);
    json['restRollsPending'] = anyPending;
    return get(id);
  }

  // -- Combat tracking ------------------------------------------------------

  final List<CombatPatch> combatPatches = [];
  final List<String?> concentrationCalls = [];
  final List<({int level, int amount})> slotSpends = [];
  final List<({int level, int amount})> slotRestores = [];
  final List<({String id, int amount})> resourceSpends = [];
  final List<({String id, int amount})> resourceRestores = [];
  final List<Map<String, int>> shortRests = [];
  int longRests = 0;
  final List<({String action, Map<String, dynamic> body})> classActions = [];
  final List<int> smites = [];

  /// Extra dice answered by `divine-smite`: level + 1 d8 unless set.
  String? smiteDice;

  /// Applies [change] to a deep copy of the character's `combat` block.
  void _combat(String id, void Function(Map<String, dynamic> combat) change) {
    final json = _json(id);
    final combat = jsonDecode(jsonEncode(json['combat'] ?? <String, dynamic>{}));
    change(combat as Map<String, dynamic>);
    json['combat'] = combat;
  }

  @override
  Future<CharacterDetail> patchCombat(String id, CombatPatch patch) async {
    _fail();
    combatPatches.add(patch);
    final json = _json(id);
    final body = patch.toJson();
    for (final key in body.keys) {
      json[key] = body[key];
    }
    return get(id);
  }

  @override
  Future<void> setConcentration(String id, String? spellIndex) async {
    _fail();
    concentrationCalls.add(spellIndex);
    _json(id)['concentratingOnSpellIndex'] = spellIndex;
  }

  @override
  Future<void> spendSpellSlot(String id, int level, {int amount = 1}) async {
    _fail();
    slotSpends.add((level: level, amount: amount));
    _combat(id, (combat) {
      for (final slot in (combat['spellSlots'] as List? ?? const [])) {
        if (slot['level'] == level) slot['used'] = (slot['used'] as int) + amount;
      }
    });
  }

  @override
  Future<void> restoreSpellSlot(String id, int level, {int amount = 1}) async {
    _fail();
    slotRestores.add((level: level, amount: amount));
    _combat(id, (combat) {
      for (final slot in (combat['spellSlots'] as List? ?? const [])) {
        if (slot['level'] == level) slot['used'] = (slot['used'] as int) - amount;
      }
    });
  }

  @override
  Future<void> spendResource(String id, String resourceId, {int amount = 1}) async {
    _fail();
    resourceSpends.add((id: resourceId, amount: amount));
    _combat(id, (combat) {
      for (final r in (combat['resources'] as List? ?? const [])) {
        if (r['id'] == resourceId) r['used'] = (r['used'] as int) + amount;
      }
    });
  }

  @override
  Future<void> restoreResource(String id, String resourceId, {int amount = 1}) async {
    _fail();
    resourceRestores.add((id: resourceId, amount: amount));
    _combat(id, (combat) {
      for (final r in (combat['resources'] as List? ?? const [])) {
        if (r['id'] == resourceId) r['used'] = (r['used'] as int) - amount;
      }
    });
  }

  @override
  Future<CharacterDetail> shortRest(String id, {Map<String, int> hitDice = const {}}) async {
    _fail();
    shortRests.add(hitDice);
    return get(id);
  }

  @override
  Future<CharacterDetail> longRest(String id) async {
    _fail();
    longRests++;
    return get(id);
  }

  /// Rest requests of the player, in order, and how many were withdrawn.
  final List<({RestKind kind, Map<String, int> hitDice})> restRequests = [];
  int restCancellations = 0;

  @override
  Future<RestRequest> requestRest(
    String id,
    RestKind kind, {
    Map<String, int> hitDice = const {},
  }) async {
    _fail();
    restRequests.add((kind: kind, hitDice: Map<String, int>.of(hitDice)));
    final json = _json(id);
    if (json['pendingRest'] != null) throw dioError(409);
    final request = RestRequest(
      id: 'rr${restRequests.length}',
      campaignId: json['campaignId'] as String,
      characterId: id,
      kind: kind,
      hitDice: hitDice,
    );
    json['pendingRest'] = {
      'id': request.id,
      'kind': kind.apiValue,
      'hitDice': hitDice,
      'requestedAt': DateTime.utc(2026, 10, 1, 20).toIso8601String(),
    };
    return request;
  }

  @override
  Future<void> cancelRestRequest(String id) async {
    _fail();
    restCancellations++;
    final json = _json(id);
    if (json['pendingRest'] == null) throw dioError(404);
    json['pendingRest'] = null;
  }

  // -- Level-up wizard ------------------------------------------------------

  /// Plans answered by `GET /level-up`, by class index; the entry under ''
  /// is the default (no `classIndex`). Without a plan the request fails with 409.
  final Map<String, Map<String, dynamic>> levelUpPlans = {};

  /// Class asked for in each plan request (null for the default one).
  final List<String?> levelUpPlanRequests = [];

  /// Bodies of `POST /level-up`, as JSON.
  final List<Map<String, dynamic>> levelUpBodies = [];

  /// Thrown by [levelUpPlan] / [applyLevelUp] when set.
  Object? levelUpPlanError;
  Object? levelUpError;

  @override
  Future<LevelUpPlan> levelUpPlan(String id, {String? classIndex}) async {
    _fail();
    levelUpPlanRequests.add(classIndex);
    if (levelUpPlanError != null) throw levelUpPlanError!;
    final json = levelUpPlans[classIndex ?? ''] ?? levelUpPlans[''];
    if (json == null) throw dioError(409);
    return LevelUpPlan.fromJson(jsonDecode(jsonEncode(json)) as Map<String, dynamic>);
  }

  @override
  Future<CharacterDetail> applyLevelUp(String id, LevelUpRequest request) async {
    _fail();
    levelUpBodies.add(jsonDecode(jsonEncode(request.toJson())) as Map<String, dynamic>);
    if (levelUpError != null) throw levelUpError!;
    final json = _json(id);
    final classes = [
      for (final c in (json['classes'] as List? ?? const [])) Map<String, dynamic>.from(c as Map),
    ];
    final existing = classes.where((c) => c['classIndex'] == request.classIndex).firstOrNull;
    if (existing != null) {
      existing['level'] = (existing['level'] as int) + 1;
    } else if (request.classIndex != null) {
      classes.add({'classIndex': request.classIndex, 'className': request.classIndex, 'level': 1});
    }
    json['classes'] = classes;
    json['pendingLevelUpTo'] = null;
    return get(id);
  }

  // -- Spell preparation ----------------------------------------------------

  /// Answers of `GET /spell-preparation` by character id (409 when absent).
  final Map<String, Map<String, dynamic>> preparations = {};

  /// Bodies of `POST /spell-preparation` (class index -> spells) and the
  /// number of `POST .../keep` calls.
  final List<Map<String, List<String>>> prepareBodies = [];
  int keepCalls = 0;

  /// Thrown by [prepareSpells] / [keepSpellPreparation] when set.
  Object? prepareError;

  @override
  Future<SpellPreparation> spellPreparation(String id) async {
    _fail();
    final json = preparations[id];
    if (json == null) throw dioError(409);
    return SpellPreparation.fromJson(jsonDecode(jsonEncode(json)) as Map<String, dynamic>);
  }

  @override
  Future<CharacterDetail> prepareSpells(String id, Map<String, List<String>> classes) async {
    _fail();
    prepareBodies.add({
      for (final e in classes.entries) e.key: [...e.value],
    });
    if (prepareError != null) throw prepareError!;
    _completePreparation(id);
    return get(id);
  }

  @override
  Future<CharacterDetail> keepSpellPreparation(String id) async {
    _fail();
    keepCalls++;
    if (prepareError != null) throw prepareError!;
    _completePreparation(id);
    return get(id);
  }

  void _completePreparation(String id) {
    final json = _json(id);
    json['spellPreparationPending'] = false;
    json['spellPreparationReason'] = null;
    preparations[id]?['pending'] = false;
  }

  /// What the server does when a long rest (or a level) makes [id] prepare.
  void setSpellPreparationPending(String id, {String reason = 'LongRest'}) {
    final json = _json(id);
    json['spellPreparationPending'] = true;
    json['spellPreparationReason'] = reason;
  }

  /// What the server does when a DM grants (or withdraws, with null) a level.
  void setPendingLevelUp(String id, int? level) => _json(id)['pendingLevelUpTo'] = level;

  /// What the server does when the DM approves the pending rest of [id]: the
  /// request disappears and the character heals to [hitPointsCurrent].
  void approvePendingRest(String id, {required int hitPointsCurrent}) {
    final json = _json(id);
    json['pendingRest'] = null;
    json['hitPointsCurrent'] = hitPointsCurrent;
  }

  @override
  Future<CharacterDetail> classAction(
    String id,
    String action, [
    Map<String, dynamic> body = const {},
  ]) async {
    _fail();
    classActions.add((action: action, body: body));
    return get(id);
  }

  @override
  Future<DivineSmiteResult> divineSmite(String id, int slotLevel) async {
    _fail();
    smites.add(slotLevel);
    return DivineSmiteResult(
      character: await get(id),
      damageDice: smiteDice ?? '${slotLevel + 1}d8',
    );
  }

  @override
  Future<List<ChangeRequest>> changeRequests(
    String campaignId, {
    ChangeRequestStatus? status,
  }) async {
    _fail();
    return [
      for (final r in requests)
        if (r.campaignId == campaignId &&
            (status == null || r.status == status) &&
            (isDm || r.requestedByUserId == currentUserId))
          r,
    ];
  }

  ChangeRequest _resolve(String id, ChangeRequestStatus status, String? comment) {
    final index = requests.indexWhere((r) => r.id == id);
    if (index < 0) throw dioError(404);
    final old = requests[index];
    if (!old.isPending) throw dioError(409);
    final json = makeChangeRequestJson(
      id: old.id,
      characterId: old.characterId,
      requestedByUserId: old.requestedByUserId,
      type: old.type.apiValue,
      payload: old.payload,
      status: status.apiValue,
    )..['comment'] = comment;
    requests[index] = ChangeRequest.fromJson(json);
    return requests[index];
  }

  final List<({String id, String? comment})> approvals = [];
  final List<({String id, String comment})> rejections = [];
  final List<String> cancellations = [];

  @override
  Future<ChangeRequest> approve(String id, {String? comment}) async {
    _fail();
    approvals.add((id: id, comment: comment));
    final request = _resolve(id, ChangeRequestStatus.approved, comment);
    final name = request.payload['name'];
    if (name is String && _characters.containsKey(request.characterId)) {
      _characters[request.characterId]!['name'] = name;
    }
    return request;
  }

  @override
  Future<ChangeRequest> reject(String id, {required String comment}) async {
    _fail();
    rejections.add((id: id, comment: comment));
    return _resolve(id, ChangeRequestStatus.rejected, comment);
  }

  @override
  Future<ChangeRequest> cancel(String id) async {
    _fail();
    cancellations.add(id);
    return _resolve(id, ChangeRequestStatus.cancelled, null);
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
