import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/server/app_session_epoch.dart';
import '../domain/campaign_models.dart';
import 'campaigns_repository.dart';

/// Campaigns of the signed-in user, ordered by name by the server.
class CampaignsController extends AsyncNotifier<List<CampaignSummary>> {
  CampaignsRepository get _repository => ref.read(campaignsRepositoryProvider);

  @override
  Future<List<CampaignSummary>> build() {
    // Reload from scratch after a server switch.
    ref.watch(appSessionEpochProvider);
    return _repository.list();
  }

  Future<void> reload() async {
    state = await AsyncValue.guard(_repository.list);
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
  Future<CampaignDetail> build() => _repository.get(id);

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

  /// Changes the time zone and the reminder offsets (at least DM).
  Future<void> updateSettings({String? timeZoneId, List<int>? reminderOffsetsMinutes}) async {
    final updated = await _repository.updateSettings(
      id,
      timeZoneId: timeZoneId,
      reminderOffsetsMinutes: reminderOffsetsMinutes,
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
      await _repository.transferOwnership(
        id,
        toUserId: toUserId,
        previousOwnerRole: previousOwnerRole,
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
