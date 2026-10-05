import 'package:dnd_companion/features/catalog/data/models.dart' show Page;
import 'package:dnd_companion/features/sessions/data/models.dart';
import 'package:dnd_companion/features/sessions/data/sessions_repository.dart';
import 'package:flutter_riverpod/misc.dart' show Override;

import 'fakes.dart';

/// "Now" of the session tests: Monday 5 October 2026, 12:00 UTC.
final sessionsTestNow = DateTime.utc(2026, 10, 5, 12);

/// A session of the campaign `c1` in `Europe/Madrid` (UTC+2 in October).
Session makeSession({
  String id = 's1',
  int number = 1,
  String title = 'El Valle Oscuro',
  DateTime? startsAt,
  SessionStatus status = SessionStatus.scheduled,
  String campaignId = 'c1',
  String campaignName = 'La Mina Perdida',
  String? location,
  String? notes,
  String? summary,
  int? durationMinutes,
  RsvpStatus? myRsvp,
  List<SessionRsvp> rsvps = const [],
  RsvpCounts counts = const RsvpCounts(pending: 3),
  List<SessionReminder>? reminders,
}) {
  final start = startsAt ?? DateTime.utc(2026, 10, 10, 18);
  final local = start.add(const Duration(hours: 2));
  String two(int n) => n.toString().padLeft(2, '0');
  return Session(
    id: id,
    number: number,
    campaignId: campaignId,
    campaignName: campaignName,
    title: title,
    startsAt: start,
    startsAtLocal:
        '${local.year}-${two(local.month)}-${two(local.day)}T${two(local.hour)}:${two(local.minute)}:00+02:00',
    timeZoneId: 'Europe/Madrid',
    status: status,
    durationMinutes: durationMinutes,
    location: location,
    notes: notes,
    summaryMarkdown: summary,
    summaryUpdatedAt: summary == null ? null : DateTime.utc(2026, 9, 1),
    myRsvp: myRsvp,
    rsvps: rsvps,
    counts: counts,
    reminders: reminders,
  );
}

/// In-memory sessions backend. [currentUserId] is the signed-in user; [isDm]
/// decides what the journal and the reminders expose, like the server.
class FakeSessionsRepository implements SessionsRepository {
  FakeSessionsRepository({
    List<Session> sessions = const [],
    this.isDm = false,
    this.currentUserId = 'u1',
    DateTime? now,
  }) : sessions = [...sessions],
       now = now ?? sessionsTestNow;

  final List<Session> sessions;
  final bool isDm;
  final String currentUserId;
  final DateTime now;
  Object? error;

  final List<({String id, RsvpStatus status, String? comment})> rsvpCalls = [];
  final List<({String id, String markdown})> summaryCalls = [];
  final List<({String id, SessionPatch patch})> patches = [];
  final List<({String id, String subject, String message})> notices = [];
  final List<SessionDraft> created = [];
  final List<String> deleted = [];

  void _fail() {
    if (error != null) throw error!;
  }

  int _index(String id) {
    final i = sessions.indexWhere((s) => s.id == id);
    if (i < 0) throw dioError(404);
    return i;
  }

  Session _view(Session s) => Session(
    id: s.id,
    number: s.number,
    campaignId: s.campaignId,
    campaignName: s.campaignName,
    title: s.title,
    startsAt: s.startsAt,
    startsAtLocal: s.startsAtLocal,
    timeZoneId: s.timeZoneId,
    status: s.status,
    durationMinutes: s.durationMinutes,
    location: s.location,
    notes: s.notes,
    summaryMarkdown: s.summaryMarkdown,
    summaryUpdatedAt: s.summaryUpdatedAt,
    myRsvp: s.myRsvp,
    rsvps: s.rsvps,
    counts: s.counts,
    reminders: isDm ? s.reminders ?? const [] : null,
  );

  @override
  Future<List<Session>> list(
    String campaignId, {
    DateTime? from,
    DateTime? to,
    bool includePast = true,
  }) async {
    _fail();
    final result = sessions.where((s) => s.campaignId == campaignId).toList()
      ..sort((a, b) => a.startsAt.compareTo(b.startsAt));
    return [for (final s in result) _view(s)];
  }

  @override
  Future<Session> create(String campaignId, SessionDraft draft) async {
    _fail();
    created.add(draft);
    final made = makeSession(
      id: 'new${created.length}',
      number: sessions.length + 1,
      title: draft.title,
      startsAt: draft.startsAt,
      campaignId: campaignId,
      location: draft.location,
      notes: draft.notes,
      durationMinutes: draft.durationMinutes,
    );
    sessions.add(made);
    return _view(made);
  }

  @override
  Future<Session> get(String id) async {
    _fail();
    return _view(sessions[_index(id)]);
  }

  @override
  Future<Session> patch(String id, SessionPatch patch) async {
    _fail();
    patches.add((id: id, patch: patch));
    final i = _index(id);
    final s = sessions[i];
    final updated = makeSession(
      id: s.id,
      number: s.number,
      title: patch.title ?? s.title,
      startsAt: patch.startsAt ?? s.startsAt,
      status: patch.status ?? s.status,
      campaignId: s.campaignId,
      campaignName: s.campaignName,
      location: patch.location == null ? s.location : patch.location!.value,
      notes: patch.notes == null ? s.notes : patch.notes!.value,
      summary: s.summaryMarkdown,
      durationMinutes: patch.durationMinutes == null
          ? s.durationMinutes
          : patch.durationMinutes!.value,
      myRsvp: s.myRsvp,
      rsvps: s.rsvps,
      counts: s.counts,
      reminders: s.reminders,
    );
    sessions[i] = updated;
    return _view(updated);
  }

  @override
  Future<void> delete(String id) async {
    _fail();
    sessions.removeAt(_index(id));
    deleted.add(id);
  }

  @override
  Future<Session> rsvp(String id, RsvpStatus status, {String? comment}) async {
    _fail();
    rsvpCalls.add((id: id, status: status, comment: comment));
    final i = _index(id);
    final s = sessions[i];
    if (s.status == SessionStatus.cancelled) throw dioError(409);
    final rsvps = [
      for (final r in s.rsvps)
        if (r.userId != currentUserId) r,
      SessionRsvp(
        userId: currentUserId,
        displayName: 'Usuario Demo',
        status: status,
        comment: comment == null || comment.trim().isEmpty ? null : comment.trim(),
      ),
    ];
    int count(RsvpStatus x) => rsvps.where((r) => r.status == x).length;
    final updated = Session(
      id: s.id,
      number: s.number,
      campaignId: s.campaignId,
      campaignName: s.campaignName,
      title: s.title,
      startsAt: s.startsAt,
      startsAtLocal: s.startsAtLocal,
      timeZoneId: s.timeZoneId,
      status: s.status,
      durationMinutes: s.durationMinutes,
      location: s.location,
      notes: s.notes,
      summaryMarkdown: s.summaryMarkdown,
      summaryUpdatedAt: s.summaryUpdatedAt,
      myRsvp: status,
      rsvps: rsvps,
      counts: RsvpCounts(
        yes: count(RsvpStatus.yes),
        no: count(RsvpStatus.no),
        maybe: count(RsvpStatus.maybe),
        pending: (s.counts.yes + s.counts.no + s.counts.maybe + s.counts.pending) - rsvps.length,
      ),
      reminders: s.reminders,
    );
    sessions[i] = updated;
    return _view(updated);
  }

  @override
  Future<void> notify(String id, {required String subject, required String message}) async {
    _fail();
    notices.add((id: id, subject: subject, message: message));
  }

  @override
  Future<Session> setSummary(String id, String summaryMarkdown) async {
    _fail();
    summaryCalls.add((id: id, markdown: summaryMarkdown));
    final i = _index(id);
    final s = sessions[i];
    final blank = summaryMarkdown.trim().isEmpty;
    final updated = makeSession(
      id: s.id,
      number: s.number,
      title: s.title,
      startsAt: s.startsAt,
      status: s.status,
      campaignId: s.campaignId,
      campaignName: s.campaignName,
      location: s.location,
      notes: s.notes,
      summary: blank ? null : summaryMarkdown,
      durationMinutes: s.durationMinutes,
      myRsvp: s.myRsvp,
      rsvps: s.rsvps,
      counts: s.counts,
      reminders: s.reminders,
    );
    sessions[i] = updated;
    return _view(updated);
  }

  @override
  Future<Page<SessionSummary>> journal(String campaignId, {int page = 1, int pageSize = 100}) async {
    _fail();
    final all =
        sessions
            .where(
              (s) =>
                  s.campaignId == campaignId &&
                  s.hasSummary &&
                  (isDm || s.status != SessionStatus.cancelled),
            )
            .toList()
          ..sort((a, b) => a.startsAt.compareTo(b.startsAt));
    final items = all.skip((page - 1) * pageSize).take(pageSize).toList();
    return Page(
      items: [
        for (final s in items)
          SessionSummary(
            id: s.id,
            number: s.number,
            title: s.title,
            startsAt: s.startsAt,
            startsAtLocal: s.startsAtLocal,
            status: s.status,
            summaryMarkdown: s.summaryMarkdown!,
            summaryUpdatedAt: s.summaryUpdatedAt,
          ),
      ],
      total: all.length,
      page: page,
      pageSize: pageSize,
    );
  }

  @override
  Future<List<Session>> mySessions({DateTime? from, DateTime? to}) async {
    _fail();
    return [
      for (final s in sessions)
        if (s.status == SessionStatus.scheduled && !s.hasEnded(now)) _view(s),
    ];
  }
}

/// Overrides the sessions backend.
Override sessionsOverride(FakeSessionsRepository repository) =>
    sessionsRepositoryProvider.overrideWithValue(repository);
