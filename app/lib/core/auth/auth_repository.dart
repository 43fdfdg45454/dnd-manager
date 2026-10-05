import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../network/api_client.dart';
import 'auth_response.dart';
import 'token_storage.dart';
import 'user_dto.dart';

/// Thrown by [AuthRepository.refresh] when there is no refresh token to use.
class SessionExpiredException implements Exception {
  const SessionExpiredException();

  @override
  String toString() => 'SessionExpiredException';
}

/// Auth endpoints under `/api/v1/auth`. Login and refresh persist the tokens.
class AuthRepository {
  AuthRepository(this._client, this._storage);

  final ApiClient _client;
  final TokenStorage _storage;

  Dio get _dio => _client.dio;

  Future<AuthResponse> login(String email, String password) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/v1/auth/login',
      data: {'email': email, 'password': password},
    );
    final auth = AuthResponse.fromJson(response.data!);
    await _storage.save(auth);
    return auth;
  }

  /// Exchanges the stored refresh token for a new token pair (rotating).
  Future<AuthResponse> refresh() async {
    final refreshToken = await _storage.readRefreshToken();
    if (refreshToken == null) throw const SessionExpiredException();
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/v1/auth/refresh',
      data: {'refreshToken': refreshToken},
    );
    final auth = AuthResponse.fromJson(response.data!);
    await _storage.save(auth);
    return auth;
  }

  /// Revokes the refresh token on the server (best effort) and always clears
  /// the local session.
  Future<void> logout() async {
    try {
      final refreshToken = await _storage.readRefreshToken();
      if (refreshToken != null) {
        await _dio.post<void>('/api/v1/auth/logout', data: {'refreshToken': refreshToken});
      }
    } on DioException {
      // Offline or already expired: the local session is dropped anyway.
    } finally {
      await _storage.clear();
    }
  }

  Future<UserDto> me() async {
    final response = await _dio.get<Map<String, dynamic>>('/api/v1/auth/me');
    return UserDto.fromJson(response.data!);
  }

  Future<void> forgotPassword(String email) async {
    await _dio.post<void>('/api/v1/auth/password/forgot', data: {'email': email});
  }

  Future<void> setPassword(String token, String password) async {
    await _dio.post<void>(
      '/api/v1/auth/password/set',
      data: {'token': token, 'password': password},
    );
  }
}

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepository(ref.watch(apiClientProvider), ref.watch(tokenStorageProvider)),
);
