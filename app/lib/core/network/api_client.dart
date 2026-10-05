import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../config/app_config.dart';

/// Cliente HTTP compartido. La autenticación (bearer + refresh) se añade aquí
/// como interceptor en la fase de auth.
class ApiClient {
  ApiClient({required String baseUrl, Dio? dio})
      : dio = dio ??
            Dio(
              BaseOptions(
                baseUrl: baseUrl,
                connectTimeout: const Duration(seconds: 10),
                receiveTimeout: const Duration(seconds: 30),
                headers: const {'Accept': 'application/json'},
              ),
            );

  final Dio dio;
}

final apiClientProvider = Provider<ApiClient>(
  (ref) => ApiClient(baseUrl: AppConfig.apiBaseUrl),
);
