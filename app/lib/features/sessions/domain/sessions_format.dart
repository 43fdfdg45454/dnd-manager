import 'package:intl/date_symbol_data_local.dart';
import 'package:intl/intl.dart';
import 'package:timezone/data/latest.dart' as tzdata;
import 'package:timezone/timezone.dart' as tz;

import '../data/models.dart';

bool _datesReady = false;
bool _zonesReady = false;

/// Loads the Spanish date symbols (idempotent). The app localizations do it
/// too, but screens may be shown without them.
void ensureSpanishDates() {
  if (_datesReady) return;
  initializeDateFormatting('es');
  _datesReady = true;
}

void _ensureZones() {
  if (_zonesReady) return;
  tzdata.initializeTimeZones();
  _zonesReady = true;
}

String _capitalize(String text) =>
    text.isEmpty ? text : '${text[0].toUpperCase()}${text.substring(1)}';

/// "sáb 10 oct 2026 · 20:00".
String formatShortDateTime(DateTime wallClock) {
  ensureSpanishDates();
  return DateFormat("EEE d MMM y · HH:mm", 'es').format(wallClock);
}

/// "Sábado 10 de octubre de 2026, 20:00".
String formatLongDateTime(DateTime wallClock) {
  ensureSpanishDates();
  return _capitalize(DateFormat("EEEE d 'de' MMMM 'de' y, HH:mm", 'es').format(wallClock));
}

/// "10 oct 2026".
String formatDate(DateTime wallClock) {
  ensureSpanishDates();
  return DateFormat('d MMM y', 'es').format(wallClock);
}

/// "Octubre 2026".
String formatMonth(DateTime day) {
  ensureSpanishDates();
  return _capitalize(DateFormat('MMMM y', 'es').format(day));
}

/// "20:00".
String formatTime(DateTime wallClock) => DateFormat('HH:mm').format(wallClock);

/// "1 h 30 min", "24 h", "3 días", "45 min".
String formatMinutes(int minutes) {
  if (minutes < 60) return '$minutes min';
  final h = minutes ~/ 60;
  final m = minutes % 60;
  if (h >= 48 && m == 0 && h % 24 == 0) {
    return '${h ~/ 24} días';
  }
  return m == 0 ? '$h h' : '$h h $m min';
}

/// "24 h antes" style label of a reminder offset.
String formatOffsetBefore(int minutes) => '${formatMinutes(minutes)} antes';

/// Calendar day key (time and zone dropped).
DateTime dayOf(DateTime wallClock) => DateTime.utc(wallClock.year, wallClock.month, wallClock.day);

/// Whole-day key of a date picked in the calendar widget.
bool sameDay(DateTime a, DateTime b) =>
    a.year == b.year && a.month == b.month && a.day == b.day;

/// Sessions grouped by the calendar day (in the campaign's zone) they start on.
Map<DateTime, List<Session>> sessionsByDay(Iterable<Session> sessions) {
  final map = <DateTime, List<Session>>{};
  for (final s in sessions) {
    map.putIfAbsent(dayOf(s.localStart), () => []).add(s);
  }
  return map;
}

/// Sessions split into the ones still to come (soonest first) and the ones
/// already over (latest first).
({List<Session> upcoming, List<Session> past}) splitSessions(
  Iterable<Session> sessions,
  DateTime now,
) {
  final upcoming = <Session>[];
  final past = <Session>[];
  for (final s in sessions) {
    (s.hasEnded(now) ? past : upcoming).add(s);
  }
  upcoming.sort((a, b) => a.startsAt.compareTo(b.startsAt));
  past.sort((a, b) => b.startsAt.compareTo(a.startsAt));
  return (upcoming: upcoming, past: past);
}

/// Time zones offered in the settings; any other IANA identifier can be typed.
const commonTimeZones = <String>[
  'Europe/Madrid',
  'Europe/London',
  'Europe/Lisbon',
  'Europe/Paris',
  'Europe/Berlin',
  'Europe/Rome',
  'Atlantic/Canary',
  'America/Argentina/Buenos_Aires',
  'America/Bogota',
  'America/Caracas',
  'America/Santiago',
  'America/Lima',
  'America/Mexico_City',
  'America/New_York',
  'America/Chicago',
  'America/Denver',
  'America/Los_Angeles',
  'America/Sao_Paulo',
  'UTC',
];

/// True when [zoneId] is a time zone known to the local database.
bool isKnownTimeZone(String zoneId) {
  _ensureZones();
  try {
    tz.getLocation(zoneId);
    return true;
  } catch (_) {
    return false;
  }
}

/// The instant (UTC) at which the clocks of [zoneId] show the given date and
/// time, or null when the zone is unknown. A time skipped by a clock change is
/// moved forward, as the platform does.
DateTime? zonedToUtc(String zoneId, DateTime wallClock) {
  _ensureZones();
  try {
    final location = tz.getLocation(zoneId);
    final zoned = tz.TZDateTime(
      location,
      wallClock.year,
      wallClock.month,
      wallClock.day,
      wallClock.hour,
      wallClock.minute,
    );
    // A plain UTC DateTime, so it compares equal to the ones parsed from the API.
    return DateTime.fromMillisecondsSinceEpoch(zoned.millisecondsSinceEpoch, isUtc: true);
  } catch (_) {
    return null;
  }
}

/// "UTC+2" / "UTC-3:30" offset of [zoneId] at the given wall-clock time, or null.
String? zoneOffsetLabel(String zoneId, DateTime wallClock) {
  _ensureZones();
  try {
    final location = tz.getLocation(zoneId);
    final zoned = tz.TZDateTime(
      location,
      wallClock.year,
      wallClock.month,
      wallClock.day,
      wallClock.hour,
      wallClock.minute,
    );
    final total = zoned.timeZoneOffset.inMinutes;
    final sign = total < 0 ? '-' : '+';
    final h = total.abs() ~/ 60;
    final m = total.abs() % 60;
    return 'UTC$sign$h${m == 0 ? '' : ':${m.toString().padLeft(2, '0')}'}';
  } catch (_) {
    return null;
  }
}

/// The clocks of [zoneId] at the instant [utc]; falls back to the device's
/// local time when the zone is unknown.
DateTime utcToWallClock(String zoneId, DateTime utc) {
  _ensureZones();
  try {
    final z = tz.TZDateTime.from(utc, tz.getLocation(zoneId));
    return DateTime(z.year, z.month, z.day, z.hour, z.minute);
  } catch (_) {
    return utc.toLocal();
  }
}
