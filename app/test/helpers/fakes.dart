import 'package:dio/dio.dart';
import 'package:dnd_companion/core/auth/auth_controller.dart';
import 'package:dnd_companion/core/auth/auth_repository.dart';
import 'package:dnd_companion/core/auth/auth_response.dart';
import 'package:dnd_companion/core/auth/auth_state.dart';
import 'package:dnd_companion/core/auth/token_storage.dart';
import 'package:dnd_companion/core/auth/user_dto.dart';
import 'package:dnd_companion/features/admin/data/admin_users_repository.dart';
import 'package:dnd_companion/features/admin/domain/paged_users.dart';
import 'package:dnd_companion/features/home/data/server_info.dart';
import 'package:dnd_companion/features/home/data/server_info_repository.dart';

UserDto makeUser({
  String id = 'u1',
  String email = 'user@example.com',
  String displayName = 'Usuario Demo',
  UserRole role = UserRole.user,
  bool isActive = true,
  bool hasPassword = true,
}) => UserDto(
  id: id,
  email: email,
  displayName: displayName,
  role: role,
  isActive: isActive,
  hasPassword: hasPassword,
  createdAt: DateTime.utc(2026, 1, 1),
);

AuthResponse makeAuthResponse(UserDto user, {String suffix = '1'}) => AuthResponse(
  accessToken: 'access-$suffix',
  accessTokenExpiresAt: DateTime.utc(2030, 1, 1),
  refreshToken: 'refresh-$suffix',
  user: user,
);

DioException dioError(int? status, {DioExceptionType? type}) {
  final options = RequestOptions(path: '/test');
  return DioException(
    requestOptions: options,
    type:
        type ?? (status == null ? DioExceptionType.connectionError : DioExceptionType.badResponse),
    response: status == null
        ? null
        : Response<dynamic>(requestOptions: options, statusCode: status),
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
  FakeAuthRepository({required this.storage, this.loginUser, this.meUser, this.loginError});

  final FakeTokenStorage storage;
  UserDto? loginUser;
  UserDto? meUser;
  Object? loginError;
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
  Future<UserDto> me() async => meUser!;

  @override
  Future<void> logout() async {
    logoutCalls++;
    await storage.clear();
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

final fakeServerInfoOverride = serverInfoProvider.overrideWith(
  (ref) async => const ServerInfo(name: 'dnd-companion-api', version: '0.1.0'),
);
