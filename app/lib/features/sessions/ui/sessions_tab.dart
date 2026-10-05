import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:table_calendar/table_calendar.dart';

import '../../../core/router/app_router.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../data/models.dart';
import '../data/sessions_controllers.dart';
import '../domain/sessions_format.dart';
import 'session_widgets.dart';

/// "Sesiones" tab of a campaign: upcoming and past sessions as a list, or a
/// monthly calendar with the days that have a session marked. DMs can schedule
/// new sessions.
class SessionsTab extends ConsumerStatefulWidget {
  const SessionsTab({super.key, required this.campaign});

  final CampaignDetail campaign;

  @override
  ConsumerState<SessionsTab> createState() => _SessionsTabState();
}

class _SessionsTabState extends ConsumerState<SessionsTab> {
  bool _calendar = false;
  DateTime _focused = DateTime.now();
  DateTime? _selected;

  @override
  Widget build(BuildContext context) {
    final campaign = widget.campaign;
    final isDm = campaign.myRole.isAtLeastDm;
    final sessions = ref.watch(sessionsControllerProvider(campaign.id));

    return Scaffold(
      backgroundColor: Colors.transparent,
      floatingActionButton: isDm
          ? FloatingActionButton.extended(
              key: const Key('sessions-new'),
              onPressed: () => context.push(AppRoutes.sessionNew(campaign.id)),
              icon: const Icon(Icons.event_available_outlined),
              label: const Text('Nueva sesión'),
            )
          : null,
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
            child: SegmentedButton<bool>(
              key: const Key('sessions-view-toggle'),
              showSelectedIcon: false,
              segments: const [
                ButtonSegment(
                  value: false,
                  icon: Icon(Icons.view_list_outlined),
                  label: Text('Lista', key: Key('sessions-view-list')),
                ),
                ButtonSegment(
                  value: true,
                  icon: Icon(Icons.calendar_month_outlined),
                  label: Text('Calendario', key: Key('sessions-view-calendar')),
                ),
              ],
              selected: {_calendar},
              onSelectionChanged: (value) => setState(() => _calendar = value.first),
            ),
          ),
          Expanded(
            child: sessions.when(
              skipLoadingOnReload: true,
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _SessionsError(
                error: error,
                onRetry: () => ref.invalidate(sessionsControllerProvider(campaign.id)),
              ),
              data: (list) => RefreshIndicator(
                onRefresh: () => ref.read(sessionsControllerProvider(campaign.id).notifier).reload(),
                child: _calendar
                    ? _CalendarView(
                        campaign: campaign,
                        sessions: list,
                        focused: _focused,
                        selected: _selected,
                        onSelected: (selected, focused) => setState(() {
                          _selected = selected;
                          _focused = focused;
                        }),
                        onPageChanged: (focused) => _focused = focused,
                      )
                    : _ListView(campaign: campaign, sessions: list),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _SessionsError extends StatelessWidget {
  const _SessionsError({required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(describeSessionError(error), textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Reintentar'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ListView extends ConsumerWidget {
  const _ListView({required this.campaign, required this.sessions});

  final CampaignDetail campaign;
  final List<Session> sessions;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final now = ref.watch(sessionsClockProvider)();
    final split = splitSessions(sessions, now);
    final theme = Theme.of(context);

    Widget header(String text, Key key) => Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      child: Text(text, key: key, style: theme.textTheme.titleMedium),
    );

    Widget empty(String text) => Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Text(text, style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic)),
    );

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.only(bottom: 88),
      children: [
        header('Próximas', const Key('sessions-upcoming-header')),
        if (split.upcoming.isEmpty)
          empty('No hay sesiones programadas.')
        else
          for (final s in split.upcoming) SessionTile(campaignId: campaign.id, session: s),
        header('Pasadas', const Key('sessions-past-header')),
        if (split.past.isEmpty)
          empty('Todavía no hay sesiones pasadas.')
        else
          for (final s in split.past) SessionTile(campaignId: campaign.id, session: s),
      ],
    );
  }
}

class _CalendarView extends StatelessWidget {
  const _CalendarView({
    required this.campaign,
    required this.sessions,
    required this.focused,
    required this.selected,
    required this.onSelected,
    required this.onPageChanged,
  });

  final CampaignDetail campaign;
  final List<Session> sessions;
  final DateTime focused;
  final DateTime? selected;
  final void Function(DateTime selected, DateTime focused) onSelected;
  final ValueChanged<DateTime> onPageChanged;

  @override
  Widget build(BuildContext context) {
    ensureSpanishDates();
    final byDay = sessionsByDay(sessions);
    final selectedDay = selected;
    final daySessions = selectedDay == null ? const <Session>[] : byDay[dayOf(selectedDay)] ?? [];
    final theme = Theme.of(context);

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.only(bottom: 88),
      children: [
        TableCalendar<Session>(
          key: const Key('sessions-calendar'),
          locale: 'es',
          firstDay: DateTime.utc(2020),
          lastDay: DateTime.utc(2100),
          focusedDay: focused,
          startingDayOfWeek: StartingDayOfWeek.monday,
          calendarFormat: CalendarFormat.month,
          availableCalendarFormats: const {CalendarFormat.month: 'Mes'},
          headerStyle: const HeaderStyle(formatButtonVisible: false, titleCentered: true),
          selectedDayPredicate: (day) => selectedDay != null && isSameDay(selectedDay, day),
          eventLoader: (day) => byDay[dayOf(day)] ?? const [],
          onDaySelected: onSelected,
          onPageChanged: onPageChanged,
        ),
        const Divider(),
        if (selectedDay == null)
          Padding(
            padding: const EdgeInsets.all(16),
            child: Text(
              'Toca un día para ver sus sesiones. Zona horaria: ${campaign.timeZoneId}.',
              style: theme.textTheme.bodyMedium,
            ),
          )
        else if (daySessions.isEmpty)
          Padding(
            key: const Key('sessions-day-empty'),
            padding: const EdgeInsets.all(16),
            child: Text(
              'No hay sesiones el ${formatDate(selectedDay)}.',
              style: theme.textTheme.bodyMedium,
            ),
          )
        else
          for (final s in daySessions) SessionTile(campaignId: campaign.id, session: s),
      ],
    );
  }
}

/// One session of a list: number, title, local date, place, answer counters
/// and the answer of the signed-in user.
class SessionTile extends StatelessWidget {
  const SessionTile({super.key, required this.campaignId, required this.session});

  final String campaignId;
  final Session session;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final s = session;
    return Card(
      key: Key('session-tile-${s.id}'),
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => context.push(AppRoutes.session(campaignId, s.id)),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      sessionHeading(s.number, s.title),
                      style: theme.textTheme.titleSmall,
                    ),
                  ),
                  SessionStatusBadge(status: s.status),
                ],
              ),
              const SizedBox(height: 2),
              Text(sessionWhen(s), key: Key('session-when-${s.id}')),
              if (s.location != null)
                Row(
                  children: [
                    const Icon(Icons.place_outlined, size: 16),
                    const SizedBox(width: 4),
                    Expanded(child: Text(s.location!)),
                  ],
                ),
              const SizedBox(height: 6),
              RsvpCountChips(counts: s.counts),
              const SizedBox(height: 4),
              if (s.status != SessionStatus.cancelled)
                KeyedSubtree(key: Key('session-my-rsvp-${s.id}'), child: MyRsvpLabel(rsvp: s.myRsvp)),
            ],
          ),
        ),
      ),
    );
  }
}
