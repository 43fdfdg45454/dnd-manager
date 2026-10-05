import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../network/api_client.dart';
import 'app_release.dart';

/// `GET /api/v1/app/latest` (anonymous).
class AppUpdateRepository {
  AppUpdateRepository(this._client);

  final ApiClient _client;

  /// The latest published release, or null (204) when there is none. The
  /// download URL is made absolute with the configured server.
  Future<AppRelease?> latest() async {
    final response = await _client.dio.get<Object?>('/api/v1/app/latest');
    final data = response.data;
    if (response.statusCode == 204 || data is! Map) return null;
    final release = AppRelease.fromJson(Map<String, dynamic>.from(data));
    return release.withDownloadUrl(_client.absoluteUrl(release.downloadUrl));
  }
}

final appUpdateRepositoryProvider = Provider<AppUpdateRepository>(
  (ref) => AppUpdateRepository(ref.watch(apiClientProvider)),
);
