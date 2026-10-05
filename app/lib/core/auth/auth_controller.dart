import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../cache/cache_maintenance.dart';
import '../network/api_error.dart';
import '../network/connectivity.dart';
import 'auth_repository.dart';
import 'auth_state.dart';
import 'cached_user_store.dart';
import 'token_storage.dart';
import 'user_dto.dart';

class AuthController extends Notifier<AuthState> {
  AuthRepository get _repository => ref.read(authRepositoryProvider);

  CachedUserStore get _cachedUser => ref.read(cachedUserStoreProvider);

  @override
  AuthState build() {
    // A session restored offline is checked again once the server answers.
    ref.listen<bool>(connectivityProvider.select((status) => status.isOffline), (
      wasOffline,
      offline,
    ) {
      if (wasOffline == true && !offline) unawaited(_revalidate());
    });
    Future.microtask(_restore);
    return const AuthUnknown();
  }

  /// Restores the session on startup: needs a stored refresh token and a
  /// successful `/auth/me` (the interceptor refreshes the access token if
  /// needed). When the server cannot be reached and a user was saved on this
  /// device, the session starts offline with that user (ADR 0002).
  Future<void> _restore() async {
    AuthState next = const AuthSignedOut();
    try {
      if (await ref.read(tokenStorageProvider).readRefreshToken() != null) {
        try {
          final user = await _repository.me();
          await _cachedUser.save(user);
          next = AuthSignedIn(user);
        } catch (error) {
          final cached = _cachedUser.read();
          if (!isNetworkFailure(error) || cached == null) rethrow;
          if (ref.mounted) ref.read(connectivityProvider.notifier).reportRequestFailed();
          next = AuthSignedIn(cached, isOffline: true);
        }
      }
    } catch (_) {
      // Rejected or unreachable without a saved user: fall back to the login
      // screen. Tokens are only cleared by the interceptor when the server
      // rejects them.
    }
    if (ref.mounted && state is AuthUnknown) state = next;
  }

  /// Confirms an offline session with the server and refreshes the user.
  Future<void> _revalidate() async {
    final current = state;
    if (current is! AuthSignedIn || !current.isOffline) return;
    try {
      final user = await _repository.me();
      await _cachedUser.save(user);
      if (ref.mounted && state is AuthSignedIn) state = AuthSignedIn(user);
    } catch (_) {
      // Still unreachable, or rejected (the interceptor ends the session).
    }
  }

  /// Throws the underlying error (a `DioException`) when login fails.
  Future<void> login(String email, String password) async {
    final auth = await _repository.login(email, password);
    await _cachedUser.save(auth.user);
    state = AuthSignedIn(auth.user);
  }

  Future<void> logout() async {
    await _repository.logout();
    await _forgetSessionData();
    if (ref.mounted) state = const AuthSignedOut();
  }

  /// Ends the session on this device only: no request is made (used when the
  /// server changes, because the tokens belong to the previous one).
  Future<void> signOutLocally() async {
    await ref.read(tokenStorageProvider).clear();
    await _forgetSessionData();
    if (ref.mounted) state = const AuthSignedOut();
  }

  /// Called by the HTTP layer once the session has been cleared.
  void onSessionExpired() {
    unawaited(_forgetSessionData());
    if (ref.mounted) state = const AuthSignedOut();
  }

  /// The cached user and the offline data belong to the closed session.
  Future<void> _forgetSessionData() async {
    if (!ref.mounted) return;
    final maintenance = ref.read(cacheMaintenanceProvider);
    await _cachedUser.clear();
    unawaited(maintenance.clearAll());
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
    if (!ref.mounted || state is! AuthSignedIn) return;
    state = AuthSignedIn(user);
    unawaited(_cachedUser.save(user));
  }
}

final authControllerProvider = NotifierProvider<AuthController, AuthState>(AuthController.new);
