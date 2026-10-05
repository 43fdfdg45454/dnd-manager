import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/user_dto.dart';
import '../domain/paged_users.dart';
import 'admin_users_repository.dart';

/// Holds the (searchable, paged) user list for the admin screen.
class AdminUsersController extends AsyncNotifier<PagedUsers> {
  String _search = '';

  AdminUsersRepository get _repository => ref.read(adminUsersRepositoryProvider);

  @override
  Future<PagedUsers> build() => _repository.list();

  /// Runs a new search from the first page, keeping the old list while loading.
  Future<void> search(String query) {
    _search = query.trim();
    return reload();
  }

  Future<void> reload() async {
    state = await AsyncValue.guard(() => _repository.list(search: _search));
  }

  /// Appends the next page. Errors are rethrown so the UI can report them.
  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore) return;
    final next = await _repository.list(search: _search, page: current.page + 1);
    state = AsyncData(
      next.copyWith(items: [...current.items, ...next.items], page: current.page + 1),
    );
  }

  Future<UserDto> create({
    required String email,
    required String displayName,
    required UserRole role,
  }) async {
    final user = await _repository.create(email: email, displayName: displayName, role: role);
    await reload();
    return user;
  }

  Future<void> updateUser(String id, {String? displayName, UserRole? role, bool? isActive}) async {
    final updated = await _repository.update(
      id,
      displayName: displayName,
      role: role,
      isActive: isActive,
    );
    final current = state.value;
    if (current == null) return;
    state = AsyncData(
      current.copyWith(items: [for (final u in current.items) u.id == id ? updated : u]),
    );
  }

  Future<void> resendSetupEmail(String id) => _repository.resendSetupEmail(id);
}

final adminUsersControllerProvider = AsyncNotifierProvider<AdminUsersController, PagedUsers>(
  AdminUsersController.new,
  // Errors are shown with a retry button instead of being retried silently.
  retry: (retryCount, error) => null,
);
