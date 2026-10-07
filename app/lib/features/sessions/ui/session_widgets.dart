import 'package:dio/dio.dart';
import 'package:flutter/material.dart';

import '../../../core/network/api_error.dart';
import '../../../core/theme/tokens.dart';
import '../data/models.dart';
import '../domain/sessions_format.dart';

/// Status-specific messages for the session endpoints.
const sessionErrorMessages = <int, String>{
  400: 'Los datos de la sesión no son válidos.',
  403: 'No tienes permiso para hacer eso en esta sesión.',
  404: 'La sesión no existe o no tienes acceso.',
  409: 'Hubo un conflicto al guardar. Inténtalo de nuevo.',
};

/// Like [describeApiError] with the session texts. A 400 shows the server's
/// ProblemDetails `detail` (already in Spanish) when it has one.
String describeSessionError(Object error, {Map<int, String> byStatus = const {}}) {
  if (error is DioException && error.response?.statusCode == 400 && !byStatus.containsKey(400)) {
    final detail = problemDetail(error);
    if (detail != null) return detail;
  }
  return describeApiError(error, byStatus: {...sessionErrorMessages, ...byStatus});
}

/// Colour of an answer: healing green for yes, error for no and the palette
/// highlight for maybe, all readable as text.
Color rsvpColor(BuildContext context, RsvpStatus status) => switch (status) {
  RsvpStatus.yes => context.tokens.mossText,
  RsvpStatus.no => Theme.of(context).colorScheme.error,
  RsvpStatus.maybe => context.tokens.oldGoldText,
};

IconData rsvpIcon(RsvpStatus status) => switch (status) {
  RsvpStatus.yes => Icons.check_circle_outline,
  RsvpStatus.no => Icons.cancel_outlined,
  RsvpStatus.maybe => Icons.help_outline,
};

/// "Sí 3", "No 1", "Quizá 0" counters of a session.
class RsvpCountChips extends StatelessWidget {
  const RsvpCountChips({super.key, required this.counts, this.showPending = false});

  final RsvpCounts counts;
  final bool showPending;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 6,
      runSpacing: 4,
      children: [
        for (final status in RsvpStatus.values)
          Chip(
            key: Key('count-${status.apiValue.toLowerCase()}'),
            visualDensity: VisualDensity.compact,
            padding: EdgeInsets.zero,
            avatar: Icon(rsvpIcon(status), size: 16, color: rsvpColor(context, status)),
            label: Text('${status.label} ${counts.of(status)}'),
          ),
        if (showPending)
          Chip(
            key: const Key('count-pending'),
            visualDensity: VisualDensity.compact,
            padding: EdgeInsets.zero,
            label: Text('Pendientes ${counts.pending}'),
          ),
      ],
    );
  }
}

/// "Tu respuesta: Sí" / "Sin responder".
class MyRsvpLabel extends StatelessWidget {
  const MyRsvpLabel({super.key, required this.rsvp, this.style});

  final RsvpStatus? rsvp;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final base = style ?? Theme.of(context).textTheme.bodySmall;
    if (rsvp == null) {
      return Text('Sin responder', style: base?.copyWith(color: scheme.onSurfaceVariant));
    }
    return Text(
      'Tu respuesta: ${rsvp!.label}',
      style: base?.copyWith(color: rsvpColor(context, rsvp!), fontWeight: FontWeight.w600),
    );
  }
}

/// Small badge for sessions that are not simply scheduled.
class SessionStatusBadge extends StatelessWidget {
  const SessionStatusBadge({super.key, required this.status});

  final SessionStatus status;

  @override
  Widget build(BuildContext context) {
    if (status == SessionStatus.scheduled) return const SizedBox.shrink();
    final scheme = Theme.of(context).colorScheme;
    final cancelled = status == SessionStatus.cancelled;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: cancelled ? scheme.errorContainer : scheme.secondaryContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(
        status.label,
        style: Theme.of(context).textTheme.labelSmall
            ?.copyWith(color: cancelled ? scheme.onErrorContainer : scheme.onSecondaryContainer),
      ),
    );
  }
}

/// "Sesión 3 · El Valle" heading used by lists.
String sessionHeading(int number, String title) => number > 0 ? 'Sesión $number · $title' : title;

/// Local date of a session as "sáb 10 oct 2026 · 20:00".
String sessionWhen(Session s) => formatShortDateTime(s.localStart);
