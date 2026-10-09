import 'package:dio/dio.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_repository.dart';
import 'package:dnd_companion/core/auth/auth_response.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/auth/token_storage.dart';
import 'package:dnd_companion/core/auth/user_dto.dart';
import 'package:dnd_companion/core/server/server_config.dart';
import 'package:dnd_companion/core/server/server_config_repository.dart';
import 'package:dnd_companion/core/server/server_probe.dart';
import 'package:dnd_companion/features/admin/data/admin_users_repository.dart';
import 'package:dnd_companion/features/admin/domain/paged_users.dart';
import 'package:dnd_companion/features/campaigns/data/campaigns_repository.dart';
import 'package:dnd_companion/features/campaigns/domain/campaign_models.dart';
import 'package:dnd_companion/features/home/data/server_info.dart';
import 'package:dnd_companion/features/home/data/server_info_repository.dart';

UserDto makeUser({
  String id = 'u1',
  String email = 'user@example.com',
  String displayName = 'Usuario Demo',
  UserRole role = UserRole.user,
  bool isActive = true,
  bool hasPassword = true,
  bool notificationsEnabled = true,
}) => UserDto(
  id: id,
  email: email,
  displayName: displayName,
  role: role,
  isActive: isActive,
  hasPassword: hasPassword,
  createdAt: DateTime.utc(2026, 1, 1),
  notificationsEnabled: notificationsEnabled,
);

AuthResponse makeAuthResponse(UserDto user, {String suffix = '1'}) => AuthResponse(
  accessToken: 'access-$suffix',
  accessTokenExpiresAt: DateTime.utc(2030, 1, 1),
  refreshToken: 'refresh-$suffix',
  user: user,
);

DioException dioError(int? status, {DioExceptionType? type, Object? data}) {
  final options = RequestOptions(path: '/test');
  return DioException(
    requestOptions: options,
    type:
        type ?? (status == null ? DioExceptionType.connectionError : DioExceptionType.badResponse),
    response: status == null
        ? null
        : Response<dynamic>(requestOptions: options, statusCode: status, data: data),
  );
}

/// In-memory replacement for the secure storage.
class FakeTokenStorage implements TokenStorage {
  String? access;
  String? refresh;
  DateTime? expiry;

  @override
  Future<String?> readAccessToken() async => access;

  @override
  Future<String?> readRefreshToken() async => refresh;

  @override
  Future<DateTime?> readAccessTokenExpiry() async => expiry;

  @override
  Future<void> save(AuthResponse response) async {
    access = response.accessToken;
    refresh = response.refreshToken;
    expiry = response.accessTokenExpiresAt;
  }

  @override
  Future<void> clear() async {
    access = null;
    refresh = null;
    expiry = null;
  }
}

class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({
    required this.storage,
    this.loginUser,
    this.meUser,
    this.loginError,
    this.meError,
  });

  final FakeTokenStorage storage;
  UserDto? loginUser;
  UserDto? meUser;
  Object? loginError;

  /// Thrown by [me] when set (e.g. `dioError(null)` for no connection).
  Object? meError;
  int meCalls = 0;
  final List<String> forgotRequests = [];
  int logoutCalls = 0;

  @override
  Future<AuthResponse> login(String email, String password) async {
    if (loginError != null) throw loginError!;
    final auth = makeAuthResponse(loginUser!);
    await storage.save(auth);
    return auth;
  }

  @override
  Future<UserDto> me() async {
    meCalls++;
    if (meError != null) throw meError!;
    return meUser!;
  }

  @override
  Future<void> logout() async {
    logoutCalls++;
    await storage.clear();
  }

  Object? profileError;
  final List<({String? displayName, bool? notificationsEnabled})> profileUpdates = [];

  @override
  Future<UserDto> updateProfile({String? displayName, bool? notificationsEnabled}) async {
    if (profileError != null) throw profileError!;
    profileUpdates.add((displayName: displayName, notificationsEnabled: notificationsEnabled));
    meUser = (meUser ?? loginUser ?? makeUser()).copyWith(
      displayName: displayName,
      notificationsEnabled: notificationsEnabled,
    );
    return meUser!;
  }

  @override
  Future<void> forgotPassword(String email) async => forgotRequests.add(email);

  @override
  Future<AuthResponse> refresh() => throw UnimplementedError();

  @override
  Future<void> setPassword(String token, String password) => throw UnimplementedError();
}

class FakeAdminUsersRepository implements AdminUsersRepository {
  FakeAdminUsersRepository(this.users);

  final List<UserDto> users;
  Object? createError;
  final List<String> resent = [];

  @override
  Future<PagedUsers> list({String search = '', int page = 1, int pageSize = 50}) async {
    final q = search.toLowerCase();
    final items = users
        .where((u) => u.displayName.toLowerCase().contains(q) || u.email.contains(q))
        .toList();
    return PagedUsers(items: items, total: items.length, page: 1, pageSize: pageSize);
  }

  @override
  Future<UserDto> create({
    required String email,
    required String displayName,
    required UserRole role,
  }) async {
    if (createError != null) throw createError!;
    final user = makeUser(
      id: 'u${users.length + 1}',
      email: email,
      displayName: displayName,
      role: role,
      hasPassword: false,
    );
    users.add(user);
    return user;
  }

  @override
  Future<UserDto> update(String id, {String? displayName, UserRole? role, bool? isActive}) async {
    final index = users.indexWhere((u) => u.id == id);
    users[index] = users[index].copyWith(displayName: displayName, role: role, isActive: isActive);
    return users[index];
  }

  @override
  Future<void> resendSetupEmail(String id) async => resent.add(id);
}

/// Notifier whose state is fixed, for pages that only need a session.
class FixedAuthController extends AuthController {
  FixedAuthController(this.fixed);

  final AuthState fixed;

  @override
  AuthState build() => fixed;
}

/// In-memory replacement for the `shared_preferences` backed repository.
class FakeServerConfigRepository implements ServerConfigRepository {
  FakeServerConfigRepository([ServerConfig? initial]) : stored = initial ?? ServerConfig();

  ServerConfig stored;

  @override
  ServerConfig load() => stored;

  @override
  Future<void> save(ServerConfig config) async => stored = config;
}

/// Server used by the tests that need one configured.
const testServerUrl = 'http://dnd.example.com:8080';

/// Provides a configured server so the router does not send the app to `/server`.
Override fakeServerConfigOverride({String baseUrl = testServerUrl}) =>
    serverConfigRepositoryProvider.overrideWithValue(
      FakeServerConfigRepository(ServerConfig(baseUrl: baseUrl)),
    );

/// Probe whose answer is scripted by the test.
class FakeServerProbe implements ServerProbe {
  FakeServerProbe({this.result, this.failure});

  ServerProbeResult? result;
  ServerProbeException? failure;
  final List<({String url, String? pinned})> calls = [];

  @override
  HttpClientAdapter Function()? get adapterFactory => null;

  @override
  Future<ServerProbeResult> check(String baseUrl, {String? pinnedFingerprint}) async {
    calls.add((url: baseUrl, pinned: pinnedFingerprint));
    if (failure != null) {
      // A trusted fingerprint makes the scripted certificate error go away.
      final f = failure!;
      if (f.failure == ServerProbeFailure.certificate &&
          pinnedFingerprint != null &&
          pinnedFingerprint == f.fingerprint) {
        return result!;
      }
      throw f;
    }
    return result!;
  }
}

final fakeServerInfoOverride = serverInfoProvider.overrideWith(
  (ref) async => const ServerInfo(name: 'dnd-companion-api', version: '0.1.0'),
);

Member makeMember({
  String userId = 'u1',
  String displayName = 'Usuario Demo',
  String? email,
  CampaignRole role = CampaignRole.player,
}) => Member(
  userId: userId,
  displayName: displayName,
  email: email ?? '$userId@example.com',
  role: role,
  joinedAt: DateTime.utc(2026, 1, 1),
);

/// A campaign where the signed-in user ([myRole]) is a member. [members] must
/// include that user; by default it is the user `u1` plus an Owner `owner`.
CampaignDetail makeCampaign({
  String id = 'c1',
  String name = 'La Mina Perdida',
  String description = 'Una aventura para niveles 1 a 3.',
  CampaignRole myRole = CampaignRole.owner,
  List<Member>? members,
  String timeZoneId = 'Europe/Madrid',
  List<int> reminderOffsetsMinutes = const [1440, 120],
  bool playersCanTakeFromStash = false,
}) {
  final list =
      members ??
      [
        makeMember(userId: 'u1', displayName: 'Usuario Demo', role: myRole),
        if (myRole != CampaignRole.owner)
          makeMember(userId: 'owner', displayName: 'Dueña Demo', role: CampaignRole.owner),
        makeMember(userId: 'p2', displayName: 'Beto', role: CampaignRole.player),
      ];
  final owner = list.firstWhere((m) => m.role.isOwner);
  return CampaignDetail(
    id: id,
    name: name,
    description: description,
    ownerId: owner.userId,
    ownerDisplayName: owner.displayName,
    myRole: myRole,
    members: list,
    createdAt: DateTime.utc(2026, 1, 1),
    updatedAt: DateTime.utc(2026, 1, 1),
    timeZoneId: timeZoneId,
    reminderOffsetsMinutes: reminderOffsetsMinutes,
    playersCanTakeFromStash: playersCanTakeFromStash,
  );
}

/// In-memory campaigns backend. [currentUserId] is the signed-in user.
class FakeCampaignsRepository implements CampaignsRepository {
  FakeCampaignsRepository({
    List<CampaignDetail>? campaigns,
    this.directory = const [],
    this.currentUserId = 'u1',
  }) : campaigns = [...?campaigns];

  final List<CampaignDetail> campaigns;

  /// Users returned by the search (filtered by name or email).
  final List<UserSummary> directory;
  final String currentUserId;
  Object? error;
  final List<String> searches = [];

  void _fail() {
    if (error != null) throw error!;
  }

  int _index(String id) => campaigns.indexWhere((c) => c.id == id);

  CampaignDetail _byId(String id) {
    final index = _index(id);
    if (index < 0) throw dioError(404);
    return campaigns[index];
  }

  @override
  Future<List<CampaignSummary>> list() async {
    _fail();
    return [
      for (final c in campaigns)
        CampaignSummary(
          id: c.id,
          name: c.name,
          description: c.description,
          ownerId: c.ownerId,
          ownerDisplayName: c.ownerDisplayName,
          myRole: c.myRole,
          memberCount: c.members.length,
          createdAt: c.createdAt,
        ),
    ];
  }

  @override
  Future<CampaignDetail> create({required String name, required String description}) async {
    _fail();
    final created = makeCampaign(
      id: 'c${campaigns.length + 1}',
      name: name,
      description: description,
      members: [makeMember(userId: currentUserId, role: CampaignRole.owner)],
    );
    campaigns.add(created);
    return created;
  }

  @override
  Future<CampaignDetail> get(String id) async {
    _fail();
    return _byId(id);
  }

  @override
  Future<CampaignDetail> update(String id, {String? name, String? description}) async {
    _fail();
    final updated = _byId(id).copyWith(name: name, description: description);
    campaigns[_index(id)] = updated;
    return updated;
  }

  @override
  Future<CampaignDetail> updateSettings(
    String id, {
    String? timeZoneId,
    List<int>? reminderOffsetsMinutes,
    bool? playersCanTakeFromStash,
  }) async {
    _fail();
    final updated = _byId(id).copyWith(
      timeZoneId: timeZoneId,
      reminderOffsetsMinutes: reminderOffsetsMinutes,
      playersCanTakeFromStash: playersCanTakeFromStash,
    );
    campaigns[_index(id)] = updated;
    return updated;
  }

  @override
  Future<void> delete(String id) async {
    _fail();
    campaigns.removeAt(_index(id));
  }

  @override
  Future<List<Member>> members(String id) async {
    _fail();
    return _byId(id).members;
  }

  /// Pending invitations per campaign id; [myInvitations] are the current user's.
  final Map<String, List<CampaignInvitation>> invitationsByCampaign = {};
  final List<MyInvitation> pendingInvitations = [];

  @override
  Future<CampaignInvitation> invite(String id, {required String userId, required CampaignRole role}) async {
    _fail();
    final campaign = _byId(id);
    if (campaign.members.any((m) => m.userId == userId)) throw dioError(409);
    final list = invitationsByCampaign.putIfAbsent(id, () => []);
    if (list.any((i) => i.userId == userId)) throw dioError(409);
    final user = directory.firstWhere((u) => u.id == userId);
    final invitation = CampaignInvitation(
      id: 'inv-${user.id}',
      userId: user.id,
      displayName: user.displayName,
      email: user.email,
      role: role,
      invitedByDisplayName: 'Yo',
      createdAt: DateTime(2026, 1, 1),
    );
    list.add(invitation);
    return invitation;
  }

  @override
  Future<List<CampaignInvitation>> invitations(String id) async {
    _fail();
    return [...?invitationsByCampaign[id]];
  }

  @override
  Future<void> cancelInvitation(String id, String invitationId) async {
    _fail();
    invitationsByCampaign[id]?.removeWhere((i) => i.id == invitationId);
  }

  @override
  Future<List<MyInvitation>> myInvitations() async {
    _fail();
    return [...pendingInvitations];
  }

  @override
  Future<Member> acceptInvitation(String invitationId) async {
    _fail();
    final index = pendingInvitations.indexWhere((i) => i.id == invitationId);
    if (index < 0) throw dioError(404);
    final invitation = pendingInvitations.removeAt(index);
    final member = makeMember(userId: currentUserId, role: invitation.role);
    final campaignIndex = _index(invitation.campaignId);
    if (campaignIndex >= 0) {
      final campaign = campaigns[campaignIndex];
      campaigns[campaignIndex] = campaign.copyWith(members: [...campaign.members, member]);
    }
    return member;
  }

  @override
  Future<void> declineInvitation(String invitationId) async {
    _fail();
    if (!pendingInvitations.any((i) => i.id == invitationId)) throw dioError(404);
    pendingInvitations.removeWhere((i) => i.id == invitationId);
  }

  @override
  Future<Member> changeMemberRole(String id, String userId, {required CampaignRole role}) async {
    _fail();
    final campaign = _byId(id);
    final members = [
      for (final m in campaign.members) m.userId == userId ? m.copyWith(role: role) : m,
    ];
    campaigns[_index(id)] = campaign.copyWith(members: members);
    return members.firstWhere((m) => m.userId == userId);
  }

  @override
  Future<void> removeMember(String id, String userId) async {
    _fail();
    final campaign = _byId(id);
    campaigns[_index(id)] = campaign.copyWith(
      members: campaign.members.where((m) => m.userId != userId).toList(),
    );
  }

  @override
  Future<void> leave(String id) async {
    _fail();
    campaigns.removeAt(_index(id));
  }

  @override
  Future<CampaignDetail> transferOwnership(
    String id, {
    required String toUserId,
    required CampaignRole previousOwnerRole,
  }) async {
    _fail();
    final campaign = _byId(id);
    final target = campaign.members.firstWhere((m) => m.userId == toUserId);
    final members = [
      for (final m in campaign.members)
        if (m.userId == toUserId)
          m.copyWith(role: CampaignRole.owner)
        else if (m.userId == currentUserId)
          m.copyWith(role: previousOwnerRole)
        else
          m,
    ];
    final updated = campaign.copyWith(
      ownerId: target.userId,
      ownerDisplayName: target.displayName,
      myRole: previousOwnerRole,
      members: members,
    );
    campaigns[_index(id)] = updated;
    return updated;
  }

  @override
  Future<List<UserSummary>> searchUsers(String query, {int limit = 10}) async {
    _fail();
    searches.add(query);
    final q = query.toLowerCase();
    return directory
        .where((u) => u.displayName.toLowerCase().contains(q) || u.email.toLowerCase().contains(q))
        .take(limit)
        .toList();
  }
}

/// Overrides the campaigns backend with an empty in-memory one.
final fakeCampaignsOverride = campaignsRepositoryProvider.overrideWithValue(
  FakeCampaignsRepository(),
);
