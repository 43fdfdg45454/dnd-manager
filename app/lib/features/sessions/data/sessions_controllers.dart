import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/server/app_session_epoch.dart';
import 'models.dart';
import 'sessions_repository.dart';

/// Errors are shown with a retry button instead of being retried silently.
Duration? _noRetry(int retryCount, Object error) => null;

/// Current time; tests replace it to fix the "upcoming" and "past" split.
final sessionsClockProvider = Provider<DateTime Function()>((ref) => DateTime.now);

/// Every session of one campaign (upcoming and past), ordered by date.
/// Mutations rethrow errors for the UI.
class SessionsController extends AsyncNotifier<List<Session>> {
  SessionsController(this.campaignId);

  final String campaignId;

  SessionsRepository get _repository => ref.read(sessionsRepositoryProvider);

  @override
  Future<List<Session>> build() {
    ref.watch(appSessionEpochProvider);
    return _repository.list(campaignId, includePast: true);
  }

  Future<void> reload() async {
    state = await AsyncValue.guard(() => _repository.list(campaignId, includePast: true));
  }

  Future<Session> create(SessionDraft draft) async {
    final created = await _repository.create(campaignId, draft);
    ref.invalidate(mySessionsControllerProvider);
    await reload();
    return created;
  }
}

final sessionsControllerProvider = AsyncNotifierProvider.autoDispose
    .family<SessionsController, List<Session>, String>(SessionsController.new, retry: _noRetry);

/// One session with its answers. Every mutation updates the state with the
/// server's answer, refreshes the dependent lists and rethrows errors.
class SessionController extends AsyncNotifier<Session> {
  SessionController(this.id);

  final String id;

  SessionsRepository get _repository => ref.read(sessionsRepositoryProvider);

  @override
  Future<Session> build() {
    ref.watch(appSessionEpochProvider);
    return _repository.get(id);
  }

  /// Lists that show data of this session must be fetched again.
  void _refreshDependents(String campaignId) {
    ref.invalidate(sessionsControllerProvider(campaignId));
    ref.invalidate(journalControllerProvider(campaignId));
    ref.invalidate(mySessionsControllerProvider);
  }

  Future<void> _apply(Session updated) async {
    state = AsyncData(updated);
    _refreshDependents(updated.campaignId);
  }

  Future<void> rsvp(RsvpStatus status, {String? comment}) async =>
      _apply(await _repository.rsvp(id, status, comment: comment));

  Future<void> patch(SessionPatch patch) async => _apply(await _repository.patch(id, patch));

  /// Sets the journal summary; a blank [markdown] removes it.
  Future<void> setSummary(String markdown) async =>
      _apply(await _repository.setSummary(id, markdown.trim().isEmpty ? '' : markdown));

  Future<void> notify({required String subject, required String message}) =>
      _repository.notify(id, subject: subject, message: message);

  Future<void> delete() async {
    final campaignId = state.value?.campaignId;
    await _repository.delete(id);
    if (campaignId != null) _refreshDependents(campaignId);
  }
}

final sessionControllerProvider = AsyncNotifierProvider.autoDispose
    .family<SessionController, Session, String>(SessionController.new, retry: _noRetry);

/// All the journal summaries of a campaign (the pages are fetched until the
/// last one) so the search can run locally.
class JournalController extends AsyncNotifier<List<SessionSummary>> {
  JournalController(this.campaignId);

  final String campaignId;

  static const _pageSize = 100;

  @override
  Future<List<SessionSummary>> build() async {
    ref.watch(appSessionEpochProvider);
    final repository = ref.read(sessionsRepositoryProvider);
    final all = <SessionSummary>[];
    var page = 1;
    while (true) {
      final result = await repository.journal(campaignId, page: page, pageSize: _pageSize);
      all.addAll(result.items);
      if (result.items.isEmpty || all.length >= result.total) break;
      page++;
    }
    return all;
  }
}

final journalControllerProvider = AsyncNotifierProvider.autoDispose
    .family<JournalController, List<SessionSummary>, String>(JournalController.new, retry: _noRetry);

/// Upcoming sessions of every campaign of the signed-in user, soonest first.
class MySessionsController extends AsyncNotifier<List<Session>> {
  @override
  Future<List<Session>> build() async {
    ref.watch(appSessionEpochProvider);
    final sessions = await ref.read(sessionsRepositoryProvider).mySessions();
    return [...sessions]..sort((a, b) => a.startsAt.compareTo(b.startsAt));
  }
}

final mySessionsControllerProvider = AsyncNotifierProvider<MySessionsController, List<Session>>(
  MySessionsController.new,
  retry: _noRetry,
);
