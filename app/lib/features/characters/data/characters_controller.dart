import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../catalog/data/catalog_repository.dart';
import '../../catalog/data/models.dart' show SpellSummary;
import 'characters_repository.dart';
import 'models.dart';

/// Errors are shown with a retry button instead of being retried silently.
Duration? _noRetry(int retryCount, Object error) => null;

/// Characters of one campaign (summaries). Mutations rethrow errors for the UI.
class CampaignCharactersController extends AsyncNotifier<List<CharacterSummary>> {
  CampaignCharactersController(this.campaignId);

  final String campaignId;

  CharactersRepository get _repository => ref.read(charactersRepositoryProvider);

  @override
  Future<List<CharacterSummary>> build() => _repository.listByCampaign(campaignId);

  Future<void> reload() async {
    state = await AsyncValue.guard(() => _repository.listByCampaign(campaignId));
  }

  /// Creates a character and refreshes the list. See
  /// [CharactersRepository.create] for [owner].
  Future<CharacterDetail> create(String name, {({String? userId})? owner}) async {
    final created = await _repository.create(campaignId, name: name, owner: owner);
    await reload();
    return created;
  }
}

final campaignCharactersControllerProvider = AsyncNotifierProvider.autoDispose
    .family<CampaignCharactersController, List<CharacterSummary>, String>(
      CampaignCharactersController.new,
      retry: _noRetry,
    );

/// One character with its computed sheet.
class CharacterController extends AsyncNotifier<CharacterDetail> {
  CharacterController(this.id);

  final String id;

  CharactersRepository get _repository => ref.read(charactersRepositoryProvider);

  @override
  Future<CharacterDetail> build() => _repository.get(id);

  void _invalidateLists(String campaignId) {
    ref.invalidate(campaignCharactersControllerProvider(campaignId));
    ref.invalidate(changeRequestsControllerProvider);
  }

  Future<void> reload() async {
    final detail = await _repository.get(id);
    state = AsyncData(detail);
    _invalidateLists(detail.campaignId);
  }

  void _apply(CharacterDetail detail) {
    state = AsyncData(detail);
    _invalidateLists(detail.campaignId);
  }

  /// Saves a sheet edit: [Saved] when applied, [PendingApproval] when the DM
  /// has to approve it.
  Future<SheetSaveResult> saveSheet(SheetPatch patch) async {
    final result = await _repository.patchSheet(id, patch);
    switch (result) {
      case Saved(:final detail):
        _apply(detail);
      case PendingApproval():
        await reload();
    }
    return result;
  }

  /// Asks the DM to activate the character (owner, Draft).
  Future<ChangeRequest> submit() async {
    final request = await _repository.submit(id);
    await reload();
    return request;
  }

  Future<void> activate() async => _apply(await _repository.activate(id));

  Future<void> delete() async {
    final campaignId = state.value?.campaignId;
    await _repository.delete(id);
    if (campaignId != null) _invalidateLists(campaignId);
  }

  // Combat tracking (no approval). The combat view itself is phase 6.

  Future<void> patchCombat(CombatPatch patch) async =>
      _apply(await _repository.patchCombat(id, patch));

  Future<void> setConcentration(String? spellIndex) async {
    await _repository.setConcentration(id, spellIndex);
    await reload();
  }

  Future<void> spendSpellSlot(int level, {int amount = 1}) async {
    await _repository.spendSpellSlot(id, level, amount: amount);
    await reload();
  }

  Future<void> restoreSpellSlot(int level, {int amount = 1}) async {
    await _repository.restoreSpellSlot(id, level, amount: amount);
    await reload();
  }

  Future<void> spendResource(String resourceId, {int amount = 1}) async {
    await _repository.spendResource(id, resourceId, amount: amount);
    await reload();
  }

  Future<void> restoreResource(String resourceId, {int amount = 1}) async {
    await _repository.restoreResource(id, resourceId, amount: amount);
    await reload();
  }

  Future<void> shortRest({Map<String, int> hitDice = const {}}) async =>
      _apply(await _repository.shortRest(id, hitDice: hitDice));

  Future<void> longRest() async => _apply(await _repository.longRest(id));
}

final characterControllerProvider = AsyncNotifierProvider.autoDispose
    .family<CharacterController, CharacterDetail, String>(CharacterController.new, retry: _noRetry);

/// Selector of the change-request list: a campaign and an optional status.
typedef ChangeRequestsKey = ({String campaignId, ChangeRequestStatus? status});

/// Change requests of a campaign filtered by status (null = every status).
class ChangeRequestsController extends AsyncNotifier<List<ChangeRequest>> {
  ChangeRequestsController(this.key);

  final ChangeRequestsKey key;

  CharactersRepository get _repository => ref.read(charactersRepositoryProvider);

  @override
  Future<List<ChangeRequest>> build() =>
      _repository.changeRequests(key.campaignId, status: key.status);

  /// A resolved request changes the lists of every status and, when approved,
  /// the character it targets.
  void _refreshAll() {
    ref.invalidate(changeRequestsControllerProvider);
    ref.invalidate(characterControllerProvider);
    ref.invalidate(campaignCharactersControllerProvider(key.campaignId));
  }

  Future<void> approve(String id, {String? comment}) async {
    await _repository.approve(id, comment: comment);
    _refreshAll();
  }

  Future<void> reject(String id, {required String comment}) async {
    await _repository.reject(id, comment: comment);
    _refreshAll();
  }

  Future<void> cancel(String id) async {
    await _repository.cancel(id);
    _refreshAll();
  }
}

final changeRequestsControllerProvider = AsyncNotifierProvider.autoDispose
    .family<ChangeRequestsController, List<ChangeRequest>, ChangeRequestsKey>(
      ChangeRequestsController.new,
      retry: _noRetry,
    );

/// Number of pending requests of a campaign (the badge for DMs).
final pendingChangeRequestCountProvider = Provider.autoDispose.family<int, String>((
  ref,
  campaignId,
) {
  final pending = ref.watch(
    changeRequestsControllerProvider((campaignId: campaignId, status: ChangeRequestStatus.pending)),
  );
  return pending.value?.length ?? 0;
});

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
