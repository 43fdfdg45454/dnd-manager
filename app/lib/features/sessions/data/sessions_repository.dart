import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../catalog/data/models.dart' show Page;
import 'models.dart';

/// Session endpoints: `/campaigns/{id}/sessions`, `/campaigns/{id}/journal`,
/// `/sessions/{id}` and `/me/sessions`.
class SessionsRepository {
  SessionsRepository(this._client);

  final ApiClient _client;

  static const _api = '/api/v1';

  List<T> _list<T>(List<dynamic> data, T Function(Map<String, dynamic>) parse) => [
    for (final e in data) parse(e as Map<String, dynamic>),
  ];

  /// Sessions of the campaign ordered by date. With [includePast] the ones that
  /// already took place are included; [to] is exclusive.
  Future<List<Session>> list(
    String campaignId, {
    DateTime? from,
    DateTime? to,
    bool includePast = true,
  }) async {
    final response = await _client.dio.get<List<dynamic>>(
      '$_api/campaigns/$campaignId/sessions',
      queryParameters: {
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
        'includePast': includePast,
      },
    );
    return _list(response.data!, Session.fromJson);
  }

  Future<Session> create(String campaignId, SessionDraft draft) async {
    final response = await _client.dio.post<Map<String, dynamic>>(
      '$_api/campaigns/$campaignId/sessions',
      data: draft.toJson(),
    );
    return Session.fromJson(response.data!);
  }

  Future<Session> get(String id) async {
    final response = await _client.dio.get<Map<String, dynamic>>('$_api/sessions/$id');
    return Session.fromJson(response.data!);
  }

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
  Future<Page<SessionSummary>> journal(String campaignId, {int page = 1, int pageSize = 100}) async {
    final response = await _client.dio.get<Map<String, dynamic>>(
      '$_api/campaigns/$campaignId/journal',
      queryParameters: {'page': page, 'pageSize': pageSize},
    );
    return Page.fromJson(response.data!, SessionSummary.fromJson);
  }

  /// Upcoming scheduled sessions of every campaign of the signed-in user.
  Future<List<Session>> mySessions({DateTime? from, DateTime? to}) async {
    final response = await _client.dio.get<List<dynamic>>(
      '$_api/me/sessions',
      queryParameters: {
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
      },
    );
    return _list(response.data!, Session.fromJson);
  }
}

final sessionsRepositoryProvider = Provider<SessionsRepository>(
  (ref) => SessionsRepository(ref.watch(apiClientProvider)),
);
