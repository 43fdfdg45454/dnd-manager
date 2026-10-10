import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../features/catalog/data/catalog_repository.dart';
import '../../../features/catalog/data/models.dart' show SpellSummary;
import '../../../features/characters/data/characters_controller.dart';
import 'dnd5e_characters_repository.dart';
import 'models.dart';

/// Errors are shown with a retry button instead of being retried silently.
Duration? _noRetry(int retryCount, Object error) => null;

/// The D&D 5e sheet of a character, derived from [characterControllerProvider]
/// (the same detail, read through [Dnd5eCharacter.fromDetail]).
final dnd5eCharacterProvider = Provider.autoDispose.family<AsyncValue<Dnd5eCharacter>, String>(
  (ref, id) => ref.watch(characterControllerProvider(id)).whenData(Dnd5eCharacter.fromDetail),
);

/// The D&D 5e writes of one character (sheet, combat tracking, companion,
/// resources, rests, class actions, spell preparation). Each one goes through
/// [Dnd5eCharactersRepository] and hands the answer to the core
/// [CharacterController] ([CharacterController.apply] or a reload), so the
/// sheet on screen is always the one of [characterControllerProvider]. Errors
/// are rethrown for the UI.
class Dnd5eCharacterController extends Notifier<void> {
  Dnd5eCharacterController(this.id);

  final String id;

  Dnd5eCharactersRepository get _repository => ref.read(dnd5eCharactersRepositoryProvider);

  CharacterController get _core => ref.read(characterControllerProvider(id).notifier);

  @override
  void build() {}

  /// Saves a sheet edit: [Saved] when applied, [PendingApproval] when the DM
  /// has to approve it.
  Future<SheetSaveResult> saveSheet(SheetPatch patch) async {
    final result = await _repository.patchSheet(id, patch);
    switch (result) {
      case Saved(:final detail):
        _core.apply(detail);
      case PendingApproval():
        await _core.reload();
    }
    return result;
  }

  /// Prepares the spells ([classes]: class index -> spell indexes) and clears
  /// the pending preparation. Errors are rethrown for the UI.
  Future<void> prepareSpells(Map<String, List<String>> classes) async =>
      _core.apply(await _repository.prepareSpells(id, classes));

  /// Keeps the previous preparation.
  Future<void> keepSpellPreparation() async => _core.apply(await _repository.keepSpellPreparation(id));

  // Combat tracking (no approval).

  Future<void> patchCombat(CombatPatch patch) async =>
      _core.apply(await _repository.patchCombat(id, patch));

  /// Applies [amount] of damage through the server and reloads the sheet.
  /// Returns what it meant for the concentration (a Constitution save to make,
  /// or the concentration ended by itself).
  Future<DamageOutcome> applyDamage(int amount) async {
    final result = await _repository.applyDamage(id, amount);
    _core.apply(result.character);
    return result.outcome;
  }

  /// Chooses or renames the animal companion: [Saved] when applied,
  /// [PendingApproval] when the DM has to approve a change of beast.
  Future<SheetSaveResult> setCompanion({required String beastIndex, required String name}) async {
    final result = await _repository.setCompanion(id, beastIndex: beastIndex, name: name);
    switch (result) {
      case Saved(:final detail):
        _core.apply(detail);
      case PendingApproval():
        await _core.reload();
    }
    return result;
  }

  /// Moves the companion's hit points by [delta] or sets them to [current].
  Future<void> trackCompanionHp({int? delta, int? current}) async =>
      _core.apply(await _repository.trackCompanionHp(id, delta: delta, current: current));

  /// DM only: removes the companion.
  Future<void> deleteCompanion() async {
    await _repository.deleteCompanion(id);
    await _core.reload();
  }

  /// Writes the dice rolled after a rest for the resource [resourceId].
  Future<void> saveResourceRolls(String resourceId, List<int> values) async =>
      _core.apply(await _repository.saveResourceRolls(id, resourceId, values));

  /// Replaces the invalid picks (`replace.<index>` answers).
  Future<void> replaceInvalidChoices(List<LevelUpChoiceAnswer> answers) async =>
      _core.apply(await _repository.replaceInvalidChoices(id, answers));

  Future<void> setConcentration(String? spellIndex) async {
    await _repository.setConcentration(id, spellIndex);
    await _core.reload();
  }

  Future<void> spendSpellSlot(int level, {int amount = 1}) async {
    await _repository.spendSpellSlot(id, level, amount: amount);
    await _core.reload();
  }

  Future<void> restoreSpellSlot(int level, {int amount = 1}) async {
    await _repository.restoreSpellSlot(id, level, amount: amount);
    await _core.reload();
  }

  Future<void> spendResource(String resourceId, {int amount = 1}) async {
    await _repository.spendResource(id, resourceId, amount: amount);
    await _core.reload();
  }

  Future<void> restoreResource(String resourceId, {int amount = 1}) async {
    await _repository.restoreResource(id, resourceId, amount: amount);
    await _core.reload();
  }

  Future<void> shortRest({Map<String, int> hitDice = const {}}) async =>
      _core.apply(await _repository.shortRest(id, hitDice: hitDice));

  Future<void> longRest() async => _core.apply(await _repository.longRest(id));

  // Class actions (phase 6).

  Future<void> classAction(String action, [Map<String, dynamic> body = const {}]) async =>
      _core.apply(await _repository.classAction(id, action, body));

  Future<void> rage() => classAction('rage');

  /// [note] (e.g. "Curar a Fulano") is recorded with the action when healing
  /// someone else.
  Future<void> layOnHands(int amount, {bool targetSelf = true, String? note}) =>
      classAction('lay-on-hands', {'amount': amount, 'targetSelf': targetSelf, 'note': ?note});

  Future<void> arcaneRecovery(List<int> slotLevels) =>
      classAction('arcane-recovery', {'slotLevels': slotLevels});

  /// Circle of the Land druid: same body and rules as the arcane recovery.
  Future<void> naturalRecovery(List<int> slotLevels) =>
      classAction('natural-recovery', {'slotLevels': slotLevels});

  /// Spends a slot of [slotLevel] and returns the extra damage dice ("2d8").
  Future<String> divineSmite(int slotLevel) async {
    final result = await _repository.divineSmite(id, slotLevel);
    _core.apply(result.character);
    return result.damageDice;
  }
}

final dnd5eCharacterControllerProvider =
    NotifierProvider.family<Dnd5eCharacterController, void, String>(Dnd5eCharacterController.new);

/// Name and level of spells by index, resolved from the catalog. The key is the
/// indexes joined by commas (see [spellInfoKey]). Spells that cannot be loaded
/// are left out.
final spellInfoProvider = FutureProvider.autoDispose.family<Map<String, SpellSummary>, String>((
  ref,
  key,
) async {
  final catalog = ref.watch(catalogRepositoryProvider);
  final indexes = key.isEmpty ? const <String>[] : key.split(',');
  final found = await Future.wait(
    indexes.map((index) async {
      try {
        return await catalog.spellDetail(index);
      } catch (_) {
        return null;
      }
    }),
  );
  return {for (final spell in found) ?spell?.index: spell!};
}, retry: _noRetry);

/// Stable family key for [spellInfoProvider].
String spellInfoKey(Iterable<String> indexes) => (indexes.toSet().toList()..sort()).join(',');

/// The invalid picks of a character and their replacement choices.
final invalidChoicesProvider = FutureProvider.autoDispose.family<InvalidChoices, String>(
  (ref, id) => ref.watch(dnd5eCharactersRepositoryProvider).invalidChoices(id),
  retry: _noRetry,
);

/// What a character can prepare (`GET /characters/{id}/spell-preparation`).
final spellPreparationProvider = FutureProvider.autoDispose.family<SpellPreparation, String>(
  (ref, id) => ref.watch(dnd5eCharactersRepositoryProvider).spellPreparation(id),
  retry: _noRetry,
);
