import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/server/app_session_epoch.dart';
import '../domain/campaign_models.dart';
import 'campaigns_repository.dart';

/// Role of the signed-in user in each campaign seen so far (from the list or a
/// detail). The router reads it synchronously to keep each user in the campaign
/// views of their role (see `campaignModeRedirect`).
class CampaignRoleCache extends Notifier<Map<String, CampaignRole>> {
  @override
  Map<String, CampaignRole> build() {
    // Roles belong to one server and one session.
    ref.watch(appSessionEpochProvider);
    return const {};
  }

  void remember(String campaignId, CampaignRole role) {
    if (state[campaignId] == role) return;
    state = {...state, campaignId: role};
  }

  void rememberAll(Iterable<CampaignSummary> campaigns) {
    final next = {...state, for (final c in campaigns) c.id: c.myRole};
    if (next.length == state.length && next.entries.every((e) => state[e.key] == e.value)) return;
    state = next;
  }
}

final campaignRoleCacheProvider = NotifierProvider<CampaignRoleCache, Map<String, CampaignRole>>(
  CampaignRoleCache.new,
);

/// Campaigns of the signed-in user, ordered by name by the server.
class CampaignsController extends AsyncNotifier<List<CampaignSummary>> {
  CampaignsRepository get _repository => ref.read(campaignsRepositoryProvider);

  @override
  Future<List<CampaignSummary>> build() async {
    // Reload from scratch after a server switch.
    ref.watch(appSessionEpochProvider);
    return _remember(await _repository.list());
  }

  List<CampaignSummary> _remember(List<CampaignSummary> list) {
    ref.read(campaignRoleCacheProvider.notifier).rememberAll(list);
    return list;
  }

  Future<void> reload() async {
    state = await AsyncValue.guard(() async => _remember(await _repository.list()));
  }

  /// Creates a campaign and refreshes the list. Errors are rethrown for the UI.
  Future<CampaignDetail> create({required String name, required String description}) async {
    final created = await _repository.create(name: name, description: description);
    await reload();
    return created;
  }
}

final campaignsControllerProvider =
    AsyncNotifierProvider<CampaignsController, List<CampaignSummary>>(
      CampaignsController.new,
      // Errors are shown with a retry button instead of being retried silently.
      retry: (retryCount, error) => null,
    );

/// One campaign with its members. Every mutation rethrows errors for the UI.
class CampaignDetailController extends AsyncNotifier<CampaignDetail> {
  CampaignDetailController(this.id);

  final String id;

  CampaignsRepository get _repository => ref.read(campaignsRepositoryProvider);

  @override
  Future<CampaignDetail> build() async => _remember(await _repository.get(id));

  /// Keeps the role cache of the router in sync with every loaded detail.
  CampaignDetail _remember(CampaignDetail detail) {
    ref.read(campaignRoleCacheProvider.notifier).remember(detail.id, detail.myRole);
    return detail;
  }

  /// The member count shown in the list changes with every membership mutation.
  void _refreshList() => ref.invalidate(campaignsControllerProvider);

  Future<void> _reloadMembers() async {
    final members = await _repository.members(id);
    final current = state.value;
    if (current != null) state = AsyncData(current.copyWith(members: members));
    _refreshList();
  }

  Future<void> edit({String? name, String? description}) async {
    state = AsyncData(await _repository.update(id, name: name, description: description));
    _refreshList();
  }

  /// Changes the time zone, the reminder offsets or whether players take items
  /// from the party stash (at least DM).
  Future<void> updateSettings({
    String? timeZoneId,
    List<int>? reminderOffsetsMinutes,
    bool? playersCanTakeFromStash,
  }) async {
    final updated = await _repository.updateSettings(
      id,
      timeZoneId: timeZoneId,
      reminderOffsetsMinutes: reminderOffsetsMinutes,
      playersCanTakeFromStash: playersCanTakeFromStash,
    );
    // The server answers with the whole campaign; keep the members already loaded
    // in case the answer omits them.
    state = AsyncData(updated);
  }

  Future<void> addMember(String userId, CampaignRole role) async {
    await _repository.addMember(id, userId: userId, role: role);
    await _reloadMembers();
  }

  Future<void> changeRole(String userId, CampaignRole role) async {
    await _repository.changeMemberRole(id, userId, role: role);
    await _reloadMembers();
  }

  Future<void> removeMember(String userId) async {
    await _repository.removeMember(id, userId);
    await _reloadMembers();
  }

  Future<void> transferOwnership(String toUserId, CampaignRole previousOwnerRole) async {
    state = AsyncData(
      _remember(
        await _repository.transferOwnership(
          id,
          toUserId: toUserId,
          previousOwnerRole: previousOwnerRole,
        ),
      ),
    );
    _refreshList();
  }

  /// Leaves the campaign (not allowed for the Owner).
  Future<void> leave() async {
    await _repository.leave(id);
    _refreshList();
  }

  Future<void> delete() async {
    await _repository.delete(id);
    _refreshList();
  }
}

final campaignDetailControllerProvider = AsyncNotifierProvider.autoDispose
    .family<CampaignDetailController, CampaignDetail, String>(
      CampaignDetailController.new,
      retry: (retryCount, error) => null,
    );
