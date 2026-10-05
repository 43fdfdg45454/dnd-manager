import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/cached_result.dart';
import '../../../core/network/api_client.dart';
import '../../catalog/data/models.dart' show Page;
import 'models.dart';

/// Session endpoints: `/campaigns/{id}/sessions`, `/campaigns/{id}/journal`,
/// `/sessions/{id}` and `/me/sessions`.
class SessionsRepository {
  SessionsRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1';

  /// Root of everything cached for the session [id].
  static String sessionPath(String id) => '$_api/sessions/$id';

  /// Cache key of the upcoming sessions of the signed-in user.
  static const mySessionsPath = '$_api/me/sessions';

  /// Sessions of the campaign ordered by date. With [includePast] the ones that
  /// already took place are included; [to] is exclusive.
  Future<List<Session>> list(
    String campaignId, {
    DateTime? from,
    DateTime? to,
    bool includePast = true,
  }) async {
    final result = await _client.getCached(
      '$_api/campaigns/$campaignId/sessions',
      query: {
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
        'includePast': includePast,
      },
      parse: parseList(Session.fromJson),
    );
    return result.data;
  }

  Future<Session> create(String campaignId, SessionDraft draft) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/campaigns/$campaignId/sessions',
      data: draft.toJson(),
    );
    return Session.fromJson(response.data!);
  }

  Future<Session> get(String id) async =>
      (await _client.getCached('$_api/sessions/$id', parse: parseObject(Session.fromJson))).data;

  Future<Session> patch(String id, SessionPatch patch) async {
    final response = await _client.dio.patch<Map<String, dynamic>>(
      '$_api/sessions/$id',
      data: patch.toJson(),
    );
    return Session.fromJson(response.data!);
  }

  Future<void> delete(String id) async {
    await _client.dio.delete<void>('$_api/sessions/$id');
  }

  /// Answers attendance for the signed-in user.
  Future<Session> rsvp(String id, RsvpStatus status, {String? comment}) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '$_api/sessions/$id/rsvp',
      data: {
        'status': status.apiValue,
        if (comment != null && comment.trim().isNotEmpty) 'comment': comment.trim(),
      },
    );
    return Session.fromJson(response.data!);
  }

  /// Sends an email now to the members of the campaign (at least DM).
  Future<void> notify(String id, {required String subject, required String message}) async {
    await _client.dio.post<void>(
      '$_api/sessions/$id/notify',
      data: {'subject': subject, 'message': message},
    );
  }

  /// Sets the summary of the session; a blank text removes it.
  Future<Session> setSummary(String id, String summaryMarkdown) async {
    final response = await _client.dio.put<Map<String, dynamic>>(
      '$_api/sessions/$id/summary',
      data: {'summaryMarkdown': summaryMarkdown},
    );
    return Session.fromJson(response.data!);
  }

  /// One page of the journal: sessions with a summary, oldest first.
  Future<Page<SessionSummary>> journal(
    String campaignId, {
    int page = 1,
    int pageSize = 100,
  }) async {
    final result = await _client.getCached(
      '$_api/campaigns/$campaignId/journal',
      query: {'page': page, 'pageSize': pageSize},
      parse: (json) => Page.fromJson(json as Map<String, dynamic>, SessionSummary.fromJson),
    );
    return result.data;
  }

  /// Upcoming scheduled sessions of every campaign of the signed-in user.
  Future<List<Session>> mySessions({DateTime? from, DateTime? to}) async {
    final result = await _client.getCached(
      mySessionsPath,
      query: {
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
      },
      parse: parseList(Session.fromJson),
    );
    return result.data;
  }
}

final sessionsRepositoryProvider = Provider<SessionsRepository>(
  (ref) => SessionsRepository(ref.watch(apiClientProvider)),
);
