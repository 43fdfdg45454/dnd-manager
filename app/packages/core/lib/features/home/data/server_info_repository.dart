import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import 'server_info.dart';

class ServerInfoRepository {
  ServerInfoRepository(this._client);

  final ApiClient _client;

  Future<ServerInfo> fetch() async {
    final response = await _client.dio.get<Map<String, dynamic>>('/api/v1/app/info');
    return ServerInfo.fromJson(response.data!);
  }
}

final serverInfoRepositoryProvider = Provider<ServerInfoRepository>(
  (ref) => ServerInfoRepository(ref.watch(apiClientProvider)),
);

final serverInfoProvider = FutureProvider<ServerInfo>(
  (ref) => ref.watch(serverInfoRepositoryProvider).fetch(),
);
