import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../domain/game_system.dart';

/// Game systems registered in the server (`GET /api/v1/systems`).
class SystemsRepository {
  SystemsRepository(this._client);

  final ApiClient _client;

  /// Cache key of the list of systems.
  static const path = '/api/v1/systems';

  Future<List<GameSystem>> list() async =>
      (await _client.getCached(path, parse: parseList(GameSystem.fromJson))).data;
}

final systemsRepositoryProvider = Provider<SystemsRepository>(
  (ref) => SystemsRepository(ref.watch(apiClientProvider)),
);

/// Systems of the server, kept like the rest of the catalog (cached for offline
/// use, reloaded with the API client when the server changes).
final systemsProvider = FutureProvider<List<GameSystem>>(
  (ref) => ref.watch(systemsRepositoryProvider).list(),
  // A failure falls back to [fallbackGameSystems] in the UI; no silent retries.
  retry: (retryCount, error) => null,
);
