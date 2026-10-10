import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import 'models.dart';

/// Secret messages from a DM to the players of some characters.
class MessagesRepository {
  MessagesRepository(this._client);

  final ApiClient _client;

  static String messagesPath(String campaignId) => '/api/v1/campaigns/$campaignId/messages';

  /// Sends [body] (markdown) to the player of each character (at least DM).
  Future<List<DirectMessage>> send(
    String campaignId, {
    required List<String> characterIds,
    required String body,
  }) async {
    final response = await _client.dio.post<List<dynamic>>(
      messagesPath(campaignId),
      data: {'characterIds': characterIds, 'body': body},
    );
    return parseList(DirectMessage.fromJson)(response.data);
  }

  /// Most recent first: the DM gets the ones sent, a player the ones received.
  Future<List<DirectMessage>> list(String campaignId, {bool? unreadOnly, int? limit}) async =>
      (await _client.getCached(
        messagesPath(campaignId),
        query: {'unreadOnly': unreadOnly, 'limit': limit},
        parse: parseList(DirectMessage.fromJson),
      )).data;

  /// Unread messages of the user in the campaign (0 for DMs).
  Future<int> unreadCount(String campaignId) async => (await _client.getCached(
    '${messagesPath(campaignId)}/unread-count',
    parse: (json) => ((json as Map<String, dynamic>)['count'] as num?)?.toInt() ?? 0,
  )).data;

  /// Marks a received message as read.
  Future<DirectMessage> markRead(String messageId) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '/api/v1/messages/$messageId/read',
    );
    return DirectMessage.fromJson(response.data!);
  }
}

final messagesRepositoryProvider = Provider<MessagesRepository>(
  (ref) => MessagesRepository(ref.watch(apiClientProvider)),
);
