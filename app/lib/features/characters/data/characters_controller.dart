import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/characters/models.dart';
import 'characters_repository.dart';

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

/// One character (its core fields and, in `raw`, the sheet of its game system).
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

  /// Shows [detail] (the answer of a write) and refreshes the lists that
  /// derive from it. Game system modules call it after their own writes.
  void apply(CharacterDetail detail) {
    state = AsyncData(detail);
    _invalidateLists(detail.campaignId);
  }

  /// Asks the DM to activate the character (owner, Draft).
  Future<ChangeRequest> submit() async {
    final request = await _repository.submit(id);
    await reload();
    return request;
  }

  Future<void> activate() async => apply(await _repository.activate(id));

  /// Sets the portrait to the uploaded `Portrait` file [fileId] (null removes it).
  Future<void> setPortrait(String? fileId) async =>
      apply(await _repository.setPortrait(id, fileId));

  /// DM only: hands the character to the player [ownerUserId] (null: NPC).
  Future<void> setOwner(String? ownerUserId) async =>
      apply(await _repository.setOwner(id, ownerUserId));

  Future<void> delete() async {
    final campaignId = state.value?.campaignId;
    await _repository.delete(id);
    if (campaignId != null) _invalidateLists(campaignId);
  }

  /// Asks the DM for a rest (the owner) and refreshes the sheet, which then
  /// carries the pending request.
  Future<void> requestRest(RestKind kind, {Map<String, int> hitDice = const {}}) async {
    await _repository.requestRest(id, kind, hitDice: hitDice);
    await reload();
  }

  /// Withdraws the pending rest request.
  Future<void> cancelRestRequest() async {
    await _repository.cancelRestRequest(id);
    await reload();
  }

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
