import '../data/models.dart';

/// A row of the journal: a session, with its summary when it has one.
class JournalEntry {
  const JournalEntry({
    required this.id,
    required this.number,
    required this.title,
    required this.localStart,
    required this.startsAt,
    required this.status,
    this.summaryMarkdown,
  });

  factory JournalEntry.fromSummary(SessionSummary s) => JournalEntry(
    id: s.id,
    number: s.number,
    title: s.title,
    localStart: s.localStart,
    startsAt: s.startsAt,
    status: s.status,
    summaryMarkdown: s.summaryMarkdown,
  );

  factory JournalEntry.fromSession(Session s) => JournalEntry(
    id: s.id,
    number: s.number,
    title: s.title,
    localStart: s.localStart,
    startsAt: s.startsAt,
    status: s.status,
    summaryMarkdown: s.summaryMarkdown,
  );

  final String id;
  final int number;
  final String title;
  final DateTime localStart;
  final DateTime startsAt;
  final SessionStatus status;
  final String? summaryMarkdown;

  bool get hasSummary => summaryMarkdown != null && summaryMarkdown!.trim().isNotEmpty;
}

/// Journal rows in chronological order (oldest first).
///
/// The server only returns the sessions that have a summary. A DM also sees the
/// sessions that already started (or are done) and are still missing one, so
/// they can write it: pass them in [dmSessions]. Cancelled sessions without a
/// summary never appear.
List<JournalEntry> buildJournalEntries(
  List<SessionSummary> summaries, {
  List<Session>? dmSessions,
  required DateTime now,
}) {
  final entries = [for (final s in summaries) JournalEntry.fromSummary(s)];
  if (dmSessions != null) {
    final known = {for (final s in summaries) s.id};
    for (final s in dmSessions) {
      if (known.contains(s.id) || s.status == SessionStatus.cancelled) continue;
      if (s.status == SessionStatus.done || !s.startsAt.isAfter(now)) {
        entries.add(JournalEntry.fromSession(s));
      }
    }
  }
  entries.sort((a, b) {
    final byDate = a.startsAt.compareTo(b.startsAt);
    return byDate != 0 ? byDate : a.number.compareTo(b.number);
  });
  return entries;
}

/// Entries whose title or summary contain [query] (case-insensitive).
List<JournalEntry> filterJournal(List<JournalEntry> entries, String query) {
  final q = query.trim().toLowerCase();
  if (q.isEmpty) return entries;
  return [
    for (final e in entries)
      if (e.title.toLowerCase().contains(q) ||
          (e.summaryMarkdown ?? '').toLowerCase().contains(q) ||
          'sesión ${e.number}'.contains(q))
        e,
  ];
}
