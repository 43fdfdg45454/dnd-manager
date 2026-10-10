import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/server/app_session_epoch.dart';
import '../../sessions/data/sessions_controllers.dart';
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

  Future<CampaignInvitation> invite(String userId, CampaignRole role) async {
    final invitation = await _repository.invite(id, userId: userId, role: role);
    ref.invalidate(campaignInvitationsProvider(id));
    return invitation;
  }

  Future<void> cancelInvitation(String invitationId) async {
    await _repository.cancelInvitation(id, invitationId);
    ref.invalidate(campaignInvitationsProvider(id));
  }

  /// Members changed on the server (someone accepted an invitation, for example).
  Future<void> refreshMembers() => _reloadMembers();

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
    // The next session of the home page may belong to this campaign.
    ref.invalidate(mySessionsControllerProvider);
  }

  Future<void> delete() async {
    await _repository.delete(id);
    _refreshList();
    ref.invalidate(mySessionsControllerProvider);
  }
}

final campaignDetailControllerProvider = AsyncNotifierProvider.autoDispose
    .family<CampaignDetailController, CampaignDetail, String>(
      CampaignDetailController.new,
      retry: (retryCount, error) => null,
    );

/// Pending invitations of a campaign, for its DMs.
final campaignInvitationsProvider = FutureProvider.autoDispose.family<List<CampaignInvitation>, String>(
  (ref, campaignId) => ref.watch(campaignsRepositoryProvider).invitations(campaignId),
);

/// Invitations the signed-in user has not answered yet. Accepting one refreshes
/// the campaign list; the campaign is then opened by the caller.
class MyInvitationsController extends AsyncNotifier<List<MyInvitation>> {
  CampaignsRepository get _repository => ref.read(campaignsRepositoryProvider);

  @override
  Future<List<MyInvitation>> build() async {
    ref.watch(appSessionEpochProvider);
    return _repository.myInvitations();
  }

  Future<void> reload() async {
    state = await AsyncValue.guard(_repository.myInvitations);
  }

  Future<Member> accept(MyInvitation invitation) async {
    final member = await _repository.acceptInvitation(invitation.id);
    _remove(invitation.id);
    // So the campaign opens in the view of the role straight away.
    ref.read(campaignRoleCacheProvider.notifier).remember(invitation.campaignId, invitation.role);
    ref.invalidate(campaignsControllerProvider);
    return member;
  }

  Future<void> decline(MyInvitation invitation) async {
    await _repository.declineInvitation(invitation.id);
    _remove(invitation.id);
  }

  void _remove(String id) {
    final current = state.value;
    if (current != null) state = AsyncData([for (final i in current) if (i.id != id) i]);
  }
}

final myInvitationsControllerProvider = AsyncNotifierProvider<MyInvitationsController, List<MyInvitation>>(
  MyInvitationsController.new,
  retry: (retryCount, error) => null,
);
