import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/cache/stale_data.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/markdown_view.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/models.dart';
import '../data/sessions_controllers.dart';
import '../data/sessions_repository.dart';
import '../domain/sessions_format.dart';
import 'notify_dialog.dart';
import 'session_widgets.dart';

enum _SessionAction { edit, markDone, cancel, reactivate, notify, delete }

/// Detail of a session: when and where, attendance (answer, comment and the
/// answers of the other members), the reminders (DMs) and the journal summary.
class SessionPage extends ConsumerWidget {
  const SessionPage({super.key, required this.campaignId, required this.sessionId});

  final String campaignId;
  final String sessionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionControllerProvider(sessionId));
    final campaign = ref.watch(campaignDetailControllerProvider(campaignId)).value;
    final isDm = campaign?.myRole.isAtLeastDm ?? false;

    return Scaffold(
      appBar: AppBar(
        title: Text(
          session.hasValue && session.requireValue.number > 0
              ? 'Sesión ${session.requireValue.number}'
              : 'Sesión',
        ),
        actions: [
          if (isDm && session.hasValue)
            _ActionsMenu(campaignId: campaignId, session: session.requireValue),
        ],
      ),
      body: OfflineBannerLayout(
        scopes: [staleTree(SessionsRepository.sessionPath(sessionId))],
        child: session.when(
          skipLoadingOnReload: true,
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(describeSessionError(error), textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: () => ref.invalidate(sessionControllerProvider(sessionId)),
                    icon: const Icon(Icons.refresh),
                    label: const Text('Reintentar'),
                  ),
                ],
              ),
            ),
          ),
          data: (s) => RefreshIndicator(
            onRefresh: () async => ref.invalidate(sessionControllerProvider(sessionId)),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _Header(session: s),
                if (s.notes != null) ...[
                  const SizedBox(height: 16),
                  Text('Notas', style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 4),
                  MarkdownView(key: const Key('session-notes'), data: s.notes!),
                ],
                const SizedBox(height: 16),
                _RsvpSection(session: s),
                const SizedBox(height: 16),
                _ResponsesSection(session: s, members: campaign?.members ?? const []),
                if (isDm && s.reminders != null) ...[
                  const SizedBox(height: 16),
                  _RemindersSection(session: s),
                ],
                const SizedBox(height: 16),
                _SummarySection(campaignId: campaignId, session: s, isDm: isDm),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _ActionsMenu extends ConsumerWidget {
  const _ActionsMenu({required this.campaignId, required this.session});

  final String campaignId;
  final Session session;

  SessionController _controller(WidgetRef ref) =>
      ref.read(sessionControllerProvider(session.id).notifier);

  Future<void> _setStatus(
    BuildContext context,
    WidgetRef ref,
    SessionStatus status,
    String success,
  ) => runAction(
    context,
    () => _controller(ref).patch(SessionPatch(status: status)),
    success: success,
    describe: describeSessionError,
  );

  Future<void> _notify(BuildContext context, WidgetRef ref) async {
    final data = await showDialog<NoticeData>(
      context: context,
      builder: (_) => const NotifyDialog(),
    );
    if (data == null || !context.mounted) return;
    await runAction(
      context,
      () => _controller(ref).notify(subject: data.subject, message: data.message),
      success: 'Aviso enviado a los miembros.',
      describe: describeSessionError,
    );
  }

  Future<void> _delete(BuildContext context, WidgetRef ref) async {
    final confirmed = await confirmAction(
      context,
      title: 'Eliminar sesión',
      message:
          '¿Seguro que quieres eliminar "${session.title}"? Se borrarán sus respuestas y su resumen.',
      confirmLabel: 'Eliminar',
    );
    if (!confirmed || !context.mounted) return;
    final router = GoRouter.of(context);
    final done = await runAction(
      context,
      () => _controller(ref).delete(),
      success: 'Sesión eliminada.',
      describe: describeSessionError,
    );
    if (done && router.canPop()) router.pop();
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scheduled = session.status == SessionStatus.scheduled;
    return PopupMenuButton<_SessionAction>(
      key: const Key('session-menu'),
      tooltip: 'Acciones de la sesión',
      onSelected: (action) => switch (action) {
        _SessionAction.edit => context.push(AppRoutes.sessionEdit(campaignId, session.id)),
        _SessionAction.markDone => _setStatus(
          context,
          ref,
          SessionStatus.done,
          'Sesión marcada como hecha.',
        ),
        _SessionAction.cancel => _setStatus(
          context,
          ref,
          SessionStatus.cancelled,
          'Sesión cancelada.',
        ),
        _SessionAction.reactivate => _setStatus(
          context,
          ref,
          SessionStatus.scheduled,
          'Sesión reprogramada.',
        ),
        _SessionAction.notify => _notify(context, ref),
        _SessionAction.delete => _delete(context, ref),
      },
      itemBuilder: (_) => [
        const PopupMenuItem(
          key: Key('session-edit'),
          value: _SessionAction.edit,
          child: Text('Editar'),
        ),
        if (scheduled)
          const PopupMenuItem(
            key: Key('session-mark-done'),
            value: _SessionAction.markDone,
            child: Text('Marcar como hecha'),
          ),
        if (scheduled)
          const PopupMenuItem(
            key: Key('session-cancel'),
            value: _SessionAction.cancel,
            child: Text('Cancelar sesión'),
          ),
        if (!scheduled)
          const PopupMenuItem(
            key: Key('session-reactivate'),
            value: _SessionAction.reactivate,
            child: Text('Volver a programar'),
          ),
        const PopupMenuItem(
          key: Key('session-notify'),
          value: _SessionAction.notify,
          child: Text('Enviar aviso'),
        ),
        const PopupMenuItem(
          key: Key('session-delete'),
          value: _SessionAction.delete,
          child: Text('Eliminar'),
        ),
      ],
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.session});

  final Session session;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final s = session;
    final offset = zoneOffsetLabel(s.timeZoneId, s.localStart);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                s.title,
                key: const Key('session-title'),
                style: theme.textTheme.headlineSmall,
              ),
            ),
            SessionStatusBadge(status: s.status),
          ],
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            const Icon(Icons.schedule, size: 18),
            const SizedBox(width: 6),
            Expanded(child: Text(formatLongDateTime(s.localStart), key: const Key('session-when'))),
          ],
        ),
        Padding(
          padding: const EdgeInsets.only(left: 24, top: 2),
          child: Text(
            'Zona horaria: ${s.timeZoneId}${offset == null ? '' : ' ($offset)'}'
            '${s.durationMinutes == null ? '' : ' · Duración: ${formatMinutes(s.durationMinutes!)}'}',
            style: theme.textTheme.bodySmall,
          ),
        ),
        if (s.location != null) ...[
          const SizedBox(height: 6),
          Row(
            children: [
              const Icon(Icons.place_outlined, size: 18),
              const SizedBox(width: 6),
              Expanded(child: Text(s.location!, key: const Key('session-location'))),
            ],
          ),
        ],
      ],
    );
  }
}

class _RsvpSection extends ConsumerStatefulWidget {
  const _RsvpSection({required this.session});

  final Session session;

  @override
  ConsumerState<_RsvpSection> createState() => _RsvpSectionState();
}

class _RsvpSectionState extends ConsumerState<_RsvpSection> {
  late final TextEditingController _comment;
  bool _busy = false;

  String get _myUserId {
    final auth = ref.read(authControllerProvider);
    return auth is AuthSignedIn ? auth.user.id : '';
  }

  @override
  void initState() {
    super.initState();
    _comment = TextEditingController(text: widget.session.commentOf(_myUserId) ?? '');
  }

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _answer(RsvpStatus status) async {
    setState(() => _busy = true);
    await runAction(
      context,
      () => ref
          .read(sessionControllerProvider(widget.session.id).notifier)
          .rsvp(status, comment: _comment.text),
      success: 'Respuesta guardada.',
      errors: const {409: 'La sesión está cancelada.'},
      describe: describeSessionError,
    );
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final session = widget.session;
    final cancelled = session.status == SessionStatus.cancelled;
    final mine = session.myRsvp;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('¿Vienes?', style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            MyRsvpLabel(key: const Key('session-my-rsvp'), rsvp: mine),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              children: [
                for (final status in RsvpStatus.values)
                  ChoiceChip(
                    key: Key('rsvp-${status.apiValue.toLowerCase()}'),
                    label: Text(status.label),
                    avatar: Icon(rsvpIcon(status), size: 18),
                    selected: mine == status,
                    onSelected: cancelled || _busy ? null : (_) => _answer(status),
                  ),
              ],
            ),
            const SizedBox(height: 8),
            TextField(
              key: const Key('rsvp-comment'),
              controller: _comment,
              enabled: !cancelled,
              maxLength: 500,
              decoration: const InputDecoration(
                labelText: 'Comentario (opcional)',
                helperText: 'Se envía junto con tu respuesta.',
              ),
            ),
            if (mine != null && !cancelled)
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  key: const Key('rsvp-save-comment'),
                  onPressed: _busy ? null : () => _answer(mine),
                  child: const Text('Guardar comentario'),
                ),
              ),
            if (cancelled) Text('La sesión está cancelada.', style: theme.textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}

class _ResponsesSection extends StatelessWidget {
  const _ResponsesSection({required this.session, required this.members});

  final Session session;
  final List<Member> members;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final answered = {for (final r in session.rsvps) r.userId};
    final pending = [
      for (final m in members)
        if (!answered.contains(m.userId)) m,
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Respuestas', style: theme.textTheme.titleMedium),
        const SizedBox(height: 6),
        RsvpCountChips(counts: session.counts, showPending: true),
        const SizedBox(height: 4),
        for (final r in session.rsvps)
          ListTile(
            key: Key('response-${r.userId}'),
            dense: true,
            contentPadding: EdgeInsets.zero,
            leading: Icon(rsvpIcon(r.status), color: rsvpColor(theme.colorScheme, r.status)),
            title: Text(r.displayName),
            subtitle: r.comment == null ? null : Text(r.comment!),
            trailing: Text(r.status.label),
          ),
        for (final m in pending)
          ListTile(
            key: Key('pending-${m.userId}'),
            dense: true,
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.hourglass_empty),
            title: Text(m.displayName),
            trailing: const Text('Pendiente'),
          ),
      ],
    );
  }
}

class _RemindersSection extends StatelessWidget {
  const _RemindersSection({required this.session});

  final Session session;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final reminders = session.reminders ?? const [];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Recordatorios por correo', style: theme.textTheme.titleMedium),
        const SizedBox(height: 4),
        if (reminders.isEmpty)
          Text(
            'No hay recordatorios pendientes.',
            key: const Key('reminders-empty'),
            style: theme.textTheme.bodyMedium,
          ),
        for (final r in reminders)
          ListTile(
            key: Key('reminder-${r.offsetMinutes}'),
            dense: true,
            contentPadding: EdgeInsets.zero,
            leading: Icon(switch (r.state) {
              ReminderState.sent => Icons.mark_email_read_outlined,
              ReminderState.failed => Icons.error_outline,
              ReminderState.pending => Icons.schedule_send_outlined,
            }),
            title: Text(formatOffsetBefore(r.offsetMinutes)),
            subtitle: Text(
              'Envío: ${formatShortDateTime(utcToWallClock(session.timeZoneId, r.sendAt))}',
            ),
            trailing: Text(
              r.state.label,
              style: TextStyle(
                color: r.state == ReminderState.failed ? theme.colorScheme.error : null,
              ),
            ),
          ),
      ],
    );
  }
}

class _SummarySection extends StatelessWidget {
  const _SummarySection({required this.campaignId, required this.session, required this.isDm});

  final String campaignId;
  final Session session;
  final bool isDm;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text('Resumen', style: theme.textTheme.titleMedium)),
            if (isDm)
              OfflineAware(
                builder: (context, canWrite) => TextButton.icon(
                  key: const Key('session-summary-edit'),
                  onPressed: !canWrite
                      ? null
                      : () => context.push(AppRoutes.sessionSummary(campaignId, session.id)),
                  icon: const Icon(Icons.edit_outlined),
                  label: Text(session.hasSummary ? 'Editar resumen' : 'Escribir resumen'),
                ),
              ),
          ],
        ),
        if (session.hasSummary)
          MarkdownView(key: const Key('session-summary'), data: session.summaryMarkdown!)
        else
          Text(
            isDm ? 'Sin resumen todavía.' : 'Aún no hay resumen de esta sesión.',
            key: const Key('session-summary-empty'),
            style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic),
          ),
      ],
    );
  }
}
