import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'auth_response.dart';

/// Persists the session tokens in the platform secure storage.
///
/// The access token is also cached in memory so the HTTP interceptor does not
/// hit the keystore on every request.
class TokenStorage {
  TokenStorage([FlutterSecureStorage? storage])
    : _storage = storage ?? const FlutterSecureStorage();

  static const _accessKey = 'auth.access_token';
  static const _refreshKey = 'auth.refresh_token';
  static const _expiryKey = 'auth.access_expires_at';

  final FlutterSecureStorage _storage;

  bool _accessLoaded = false;
  String? _accessCache;

  Future<String?> readAccessToken() async {
    if (!_accessLoaded) {
      _accessCache = await _storage.read(key: _accessKey);
      _accessLoaded = true;
    }
    return _accessCache;
  }

  Future<String?> readRefreshToken() => _storage.read(key: _refreshKey);

  Future<DateTime?> readAccessTokenExpiry() async {
    final raw = await _storage.read(key: _expiryKey);
    return raw == null ? null : DateTime.tryParse(raw);
  }

  Future<void> save(AuthResponse response) async {
    _accessCache = response.accessToken;
    _accessLoaded = true;
    await _storage.write(key: _accessKey, value: response.accessToken);
    await _storage.write(key: _refreshKey, value: response.refreshToken);
    await _storage.write(key: _expiryKey, value: response.accessTokenExpiresAt.toIso8601String());
  }

  Future<void> clear() async {
    _accessCache = null;
    _accessLoaded = true;
    await _storage.delete(key: _accessKey);
    await _storage.delete(key: _refreshKey);
    await _storage.delete(key: _expiryKey);
  }
}

final tokenStorageProvider = Provider<TokenStorage>((ref) => TokenStorage());
