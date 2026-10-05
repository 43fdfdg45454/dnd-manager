import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../storage/local_preferences.dart';
import 'user_dto.dart';

/// Last known signed-in user, so the app can start without a connection
/// (ADR 0002). Saved on every login, refresh and `/auth/me`.
class CachedUserStore {
  CachedUserStore(this._prefs);

  static const key = 'auth.cached_user';

  /// Null when there is no persistence (most tests): nothing is remembered.
  final SharedPreferences? _prefs;

  UserDto? read() {
    final raw = _prefs?.getString(key);
    if (raw == null) return null;
    try {
      return UserDto.fromJson(Map<String, dynamic>.from(jsonDecode(raw) as Map));
    } catch (_) {
      return null;
    }
  }

  Future<void> save(UserDto user) async {
    await _prefs?.setString(key, jsonEncode(user.toJson()));
  }

  Future<void> clear() async {
    await _prefs?.remove(key);
  }
}

final cachedUserStoreProvider = Provider<CachedUserStore>(
  (ref) => CachedUserStore(ref.watch(localPreferencesProvider)),
);
