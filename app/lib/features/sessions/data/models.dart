// Hand-written models for the sessions endpoints (`/campaigns/{id}/sessions`,
// `/sessions/{id}`, `/campaigns/{id}/journal`, `/me/sessions`). Parsers are
// tolerant: missing optional fields fall back to empty values.

/// State of a session, as sent by the API.
enum SessionStatus {
  scheduled('Scheduled', 'Programada'),
  cancelled('Cancelled', 'Cancelada'),
  done('Done', 'Hecha');

  const SessionStatus(this.apiValue, this.label);

  final String apiValue;

  /// Spanish label shown in the UI.
  final String label;

  static SessionStatus fromApi(String? value) => SessionStatus.values.firstWhere(
    (s) => s.apiValue == value,
    orElse: () => SessionStatus.scheduled,
  );
}

/// Attendance answer of a member.
enum RsvpStatus {
  yes('Yes', 'Sí'),
  no('No', 'No'),
  maybe('Maybe', 'Quizá');

  const RsvpStatus(this.apiValue, this.label);

  final String apiValue;

  /// Spanish label shown in the UI.
  final String label;

  static RsvpStatus? fromApi(String? value) {
    for (final s in RsvpStatus.values) {
      if (s.apiValue == value) return s;
    }
    return null;
  }
}

DateTime? _date(Object? value) => value is String ? DateTime.tryParse(value) : null;

String? _nonEmpty(Object? value) {
  if (value is! String) return null;
  return value.trim().isEmpty ? null : value;
}

class SessionRsvp {
  const SessionRsvp({
    required this.userId,
    required this.displayName,
    required this.status,
    this.comment,
  });

  factory SessionRsvp.fromJson(Map<String, dynamic> json) => SessionRsvp(
    userId: json['userId'] as String,
    displayName: json['displayName'] as String? ?? '',
    status: RsvpStatus.fromApi(json['status'] as String?) ?? RsvpStatus.maybe,
    comment: _nonEmpty(json['comment']),
  );

  final String userId;
  final String displayName;
  final RsvpStatus status;
  final String? comment;
}

class RsvpCounts {
  const RsvpCounts({this.yes = 0, this.no = 0, this.maybe = 0, this.pending = 0});

  factory RsvpCounts.fromJson(Map<String, dynamic>? json) => RsvpCounts(
    yes: (json?['yes'] as num?)?.toInt() ?? 0,
    no: (json?['no'] as num?)?.toInt() ?? 0,
    maybe: (json?['maybe'] as num?)?.toInt() ?? 0,
    pending: (json?['pending'] as num?)?.toInt() ?? 0,
  );

  final int yes;
  final int no;
  final int maybe;
  final int pending;

  int of(RsvpStatus status) => switch (status) {
    RsvpStatus.yes => yes,
    RsvpStatus.no => no,
    RsvpStatus.maybe => maybe,
  };
}

/// Delivery state of a scheduled reminder.
enum ReminderState {
  pending('Pendiente'),
  sent('Enviado'),
  failed('Fallido');

  const ReminderState(this.label);

  final String label;
}

/// A reminder email scheduled for a session (only DMs receive them).
class SessionReminder {
  const SessionReminder({
    required this.offsetMinutes,
    required this.sendAt,
    this.sentAt,
    this.failedAt,
  });

  factory SessionReminder.fromJson(Map<String, dynamic> json) => SessionReminder(
    offsetMinutes: (json['offsetMinutes'] as num).toInt(),
    sendAt: DateTime.parse(json['sendAt'] as String),
    sentAt: _date(json['sentAt']),
    failedAt: _date(json['failedAt']),
  );

  final int offsetMinutes;
  final DateTime sendAt;
  final DateTime? sentAt;
  final DateTime? failedAt;

  ReminderState get state => sentAt != null
      ? ReminderState.sent
      : failedAt != null
      ? ReminderState.failed
      : ReminderState.pending;
}

/// Wall-clock fields of an ISO 8601 string with offset, ignoring the offset.
final _wallClock = RegExp(r'^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})');

/// The date and time shown on the clocks of the campaign's time zone, taken
/// from [startsAtLocal] (ISO with the campaign offset). Falls back to the
/// device's local time when the text does not parse.
DateTime wallClockOf(String startsAtLocal, DateTime startsAt) {
  final m = _wallClock.firstMatch(startsAtLocal);
  if (m == null) return startsAt.toLocal();
  return DateTime(
    int.parse(m.group(1)!),
    int.parse(m.group(2)!),
    int.parse(m.group(3)!),
    int.parse(m.group(4)!),
    int.parse(m.group(5)!),
  );
}

/// Minutes assumed for a session without duration (same as the server).
const assumedSessionMinutes = 240;

class Session {
  const Session({
    required this.id,
    required this.number,
    required this.campaignId,
    required this.campaignName,
    required this.title,
    required this.startsAt,
    required this.startsAtLocal,
    required this.timeZoneId,
    required this.status,
    this.durationMinutes,
    this.location,
    this.notes,
    this.summaryMarkdown,
    this.summaryUpdatedAt,
    this.myRsvp,
    this.rsvps = const [],
    this.counts = const RsvpCounts(),
    this.reminders,
  });

  factory Session.fromJson(Map<String, dynamic> json) {
    final startsAt = DateTime.parse(json['startsAt'] as String).toUtc();
    return Session(
      id: json['id'] as String,
      number: (json['number'] as num?)?.toInt() ?? 0,
      campaignId: json['campaignId'] as String,
      campaignName: json['campaignName'] as String? ?? '',
      title: json['title'] as String,
      startsAt: startsAt,
      startsAtLocal: json['startsAtLocal'] as String? ?? json['startsAt'] as String,
      timeZoneId: json['timeZoneId'] as String? ?? 'UTC',
      durationMinutes: (json['durationMinutes'] as num?)?.toInt(),
      location: _nonEmpty(json['location']),
      notes: _nonEmpty(json['notes']),
      summaryMarkdown: _nonEmpty(json['summaryMarkdown']),
      summaryUpdatedAt: _date(json['summaryUpdatedAt']),
      status: SessionStatus.fromApi(json['status'] as String?),
      myRsvp: RsvpStatus.fromApi(json['myRsvp'] as String?),
      rsvps: [
        for (final r in (json['rsvps'] as List<dynamic>? ?? const []))
          SessionRsvp.fromJson(r as Map<String, dynamic>),
      ],
      counts: RsvpCounts.fromJson(json['counts'] as Map<String, dynamic>?),
      reminders: json['reminders'] == null
          ? null
          : [
              for (final r in json['reminders'] as List<dynamic>)
                SessionReminder.fromJson(r as Map<String, dynamic>),
            ],
    );
  }

  final String id;

  /// Correlative number inside the campaign ("Sesión 12").
  final int number;
  final String campaignId;
  final String campaignName;
  final String title;

  /// Instant of the start, in UTC.
  final DateTime startsAt;

  /// ISO 8601 with the offset of the campaign's time zone.
  final String startsAtLocal;
  final String timeZoneId;
  final int? durationMinutes;
  final String? location;

  /// Notes written before the session (markdown).
  final String? notes;

  /// Story of the session for the journal (markdown).
  final String? summaryMarkdown;
  final DateTime? summaryUpdatedAt;
  final SessionStatus status;
  final RsvpStatus? myRsvp;
  final List<SessionRsvp> rsvps;
  final RsvpCounts counts;

  /// Only present for DMs.
  final List<SessionReminder>? reminders;

  /// Start as shown on the clocks of the campaign's time zone.
  DateTime get localStart => wallClockOf(startsAtLocal, startsAt);

  bool get hasSummary => summaryMarkdown != null;

  /// True once the session is over (it started and its duration elapsed).
  bool hasEnded(DateTime now) =>
      startsAt.add(Duration(minutes: durationMinutes ?? assumedSessionMinutes)).isBefore(now);

  /// The comment I left with my answer, if any.
  String? commentOf(String userId) {
    for (final r in rsvps) {
      if (r.userId == userId) return r.comment;
    }
    return null;
  }
}

/// A journal row: a session with a written summary.
class SessionSummary {
  const SessionSummary({
    required this.id,
    required this.number,
    required this.title,
    required this.startsAt,
    required this.startsAtLocal,
    required this.status,
    required this.summaryMarkdown,
    this.summaryUpdatedAt,
  });

  factory SessionSummary.fromJson(Map<String, dynamic> json) => SessionSummary(
    id: json['id'] as String,
    number: (json['number'] as num?)?.toInt() ?? 0,
    title: json['title'] as String,
    startsAt: DateTime.parse(json['startsAt'] as String).toUtc(),
    startsAtLocal: json['startsAtLocal'] as String? ?? json['startsAt'] as String,
    status: SessionStatus.fromApi(json['status'] as String?),
    summaryMarkdown: json['summaryMarkdown'] as String? ?? '',
    summaryUpdatedAt: _date(json['summaryUpdatedAt']),
  );

  final String id;
  final int number;
  final String title;
  final DateTime startsAt;
  final String startsAtLocal;
  final SessionStatus status;
  final String summaryMarkdown;
  final DateTime? summaryUpdatedAt;

  DateTime get localStart => wallClockOf(startsAtLocal, startsAt);
}

/// Body of `POST /campaigns/{id}/sessions`.
class SessionDraft {
  const SessionDraft({
    required this.title,
    required this.startsAt,
    this.durationMinutes,
    this.location,
    this.notes,
  });

  final String title;

  /// Instant of the start (sent in UTC).
  final DateTime startsAt;
  final int? durationMinutes;
  final String? location;
  final String? notes;

  Map<String, dynamic> toJson() => {
    'title': title,
    'startsAt': startsAt.toUtc().toIso8601String(),
    'durationMinutes': ?durationMinutes,
    if (location != null && location!.trim().isNotEmpty) 'location': location!.trim(),
    if (notes != null && notes!.trim().isNotEmpty) 'notes': notes,
  };
}

/// A value of a [SessionPatch] that may be an explicit `null` (which clears it
/// on the server) as opposed to an absent field (which leaves it unchanged).
class Clearable<T> {
  const Clearable(this.value);

  final T? value;
}

/// Body of `PATCH /sessions/{id}`: only the fields that are set are sent.
class SessionPatch {
  const SessionPatch({
    this.title,
    this.startsAt,
    this.status,
    this.durationMinutes,
    this.location,
    this.notes,
  });

  final String? title;
  final DateTime? startsAt;
  final SessionStatus? status;
  final Clearable<int>? durationMinutes;
  final Clearable<String>? location;
  final Clearable<String>? notes;

  bool get isEmpty =>
      title == null &&
      startsAt == null &&
      status == null &&
      durationMinutes == null &&
      location == null &&
      notes == null;

  Map<String, dynamic> toJson() => {
    'title': ?title,
    if (startsAt != null) 'startsAt': startsAt!.toUtc().toIso8601String(),
    if (status != null) 'status': status!.apiValue,
    if (durationMinutes != null) 'durationMinutes': durationMinutes!.value,
    if (location != null) 'location': location!.value,
    if (notes != null) 'notes': notes!.value,
  };
}
