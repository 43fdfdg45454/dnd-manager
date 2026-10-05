import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'auth_repository.dart';
import 'auth_state.dart';
import 'token_storage.dart';
import 'user_dto.dart';

class AuthController extends Notifier<AuthState> {
  AuthRepository get _repository => ref.read(authRepositoryProvider);

  @override
  AuthState build() {
    Future.microtask(_restore);
    return const AuthUnknown();
  }

  /// Restores the session on startup: needs a stored refresh token and a
  /// successful `/auth/me` (the interceptor refreshes the access token if needed).
  Future<void> _restore() async {
    AuthState next = const AuthSignedOut();
    try {
      if (await ref.read(tokenStorageProvider).readRefreshToken() != null) {
        next = AuthSignedIn(await _repository.me());
      }
    } catch (_) {
      // Rejected or unreachable: fall back to the login screen. Tokens are only
      // cleared by the interceptor when the server rejects them.
    }
    if (ref.mounted && state is AuthUnknown) state = next;
  }

  /// Throws the underlying error (a `DioException`) when login fails.
  Future<void> login(String email, String password) async {
    final auth = await _repository.login(email, password);
    state = AuthSignedIn(auth.user);
  }

  Future<void> logout() async {
    await _repository.logout();
    if (ref.mounted) state = const AuthSignedOut();
  }

  /// Ends the session on this device only: no request is made (used when the
  /// server changes, because the tokens belong to the previous one).
  Future<void> signOutLocally() async {
    await ref.read(tokenStorageProvider).clear();
    if (ref.mounted) state = const AuthSignedOut();
  }

  /// Called by the HTTP layer once the session has been cleared.
  void onSessionExpired() {
    if (ref.mounted) state = const AuthSignedOut();
  }

  /// Edits the display name and/or the email preference of the signed-in user.
  /// Errors are rethrown for the UI.
  Future<void> updateProfile({String? displayName, bool? notificationsEnabled}) async {
    final user = await _repository.updateProfile(
      displayName: displayName,
      notificationsEnabled: notificationsEnabled,
    );
    updateUser(user);
  }

  /// Refreshes the cached user (e.g. after a token refresh returned a newer one).
  void updateUser(UserDto user) {
    if (ref.mounted && state is AuthSignedIn) state = AuthSignedIn(user);
  }
}

final authControllerProvider = NotifierProvider<AuthController, AuthState>(AuthController.new);
