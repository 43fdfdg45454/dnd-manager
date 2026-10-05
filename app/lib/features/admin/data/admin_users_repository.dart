import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/user_dto.dart';
import '../../../core/network/api_client.dart';
import '../domain/paged_users.dart';

/// Admin endpoints under `/api/v1/admin/users` (require the `Admin` role).
class AdminUsersRepository {
  AdminUsersRepository(this._client);

  final ApiClient _client;

  Future<PagedUsers> list({String search = '', int page = 1, int pageSize = 50}) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '/api/v1/admin/users',
      queryParameters: {'search': search, 'page': page, 'pageSize': pageSize},
    );
    return PagedUsers.fromJson(response.data!);
  }

  Future<UserDto> create({
    required String email,
    required String displayName,
    required UserRole role,
  }) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/v1/admin/users',
      data: {'email': email, 'displayName': displayName, 'role': role.apiValue},
    );
    return UserDto.fromJson(response.data!);
  }

  /// Only the non-null fields are sent.
  Future<UserDto> update(String id, {String? displayName, UserRole? role, bool? isActive}) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '/api/v1/admin/users/$id',
      data: {
        'displayName': ?displayName,
        if (role != null) 'role': role.apiValue,
        'isActive': ?isActive,
      },
    );
    return UserDto.fromJson(response.data!);
  }

  Future<void> resendSetupEmail(String id) async {
    await _client.dio.post<void>('/api/v1/admin/users/$id/setup-email');
  }
}

final adminUsersRepositoryProvider = Provider<AdminUsersRepository>(
  (ref) => AdminUsersRepository(ref.watch(apiClientProvider)),
);
