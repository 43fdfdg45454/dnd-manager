import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/ui/markdown_view.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/models.dart';
import '../data/sessions_controllers.dart';
import '../domain/sessions_format.dart';
import 'session_widgets.dart';

/// Schedules a new session (no [sessionId]) or edits one. Only DMs reach it.
///
/// The date and time are entered as they appear on the clocks of the campaign's
/// time zone (shown on screen) and sent to the server as a UTC instant.
class SessionFormPage extends ConsumerWidget {
  const SessionFormPage({super.key, required this.campaignId, this.sessionId});

  final String campaignId;
  final String? sessionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final title = sessionId == null ? 'Nueva sesión' : 'Editar sesión';
    final campaign = ref.watch(campaignDetailControllerProvider(campaignId));

    Widget shell(Widget body) => Scaffold(appBar: AppBar(title: Text(title)), body: body);

    if (campaign.hasValue && !campaign.value!.myRole.isAtLeastDm) {
      return shell(const Center(child: Text('Solo el DM puede programar sesiones.')));
    }
    if (campaign.isLoading && !campaign.hasValue) {
      return shell(const Center(child: CircularProgressIndicator()));
    }
    if (campaign.hasError && !campaign.hasValue) {
      return shell(Center(child: Text(describeSessionError(campaign.error!))));
    }
    final zoneId = campaign.requireValue.timeZoneId;
    if (sessionId == null) return _SessionForm(campaignId: campaignId, timeZoneId: zoneId);

    final session = ref.watch(sessionControllerProvider(sessionId!));
    return session.when(
      loading: () => shell(const Center(child: CircularProgressIndicator())),
      error: (error, _) => shell(
        Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(describeSessionError(error), textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: () => ref.invalidate(sessionControllerProvider(sessionId!)),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Reintentar'),
                ),
              ],
            ),
          ),
        ),
      ),
      data: (s) => _SessionForm(campaignId: campaignId, timeZoneId: s.timeZoneId, session: s),
    );
  }
}

class _SessionForm extends ConsumerStatefulWidget {
  const _SessionForm({required this.campaignId, required this.timeZoneId, this.session});

  final String campaignId;
  final String timeZoneId;
  final Session? session;

  @override
  ConsumerState<_SessionForm> createState() => _SessionFormState();
}

class _SessionFormState extends ConsumerState<_SessionForm> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title;
  late final TextEditingController _duration;
  late final TextEditingController _location;
  late final TextEditingController _notes;
  DateTime? _date;
  TimeOfDay? _time;
  bool _dateMissing = false;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final s = widget.session;
    _title = TextEditingController(text: s?.title ?? '');
    _duration = TextEditingController(text: s?.durationMinutes?.toString() ?? '');
    _location = TextEditingController(text: s?.location ?? '');
    _notes = TextEditingController(text: s?.notes ?? '');
    if (s != null) {
      final local = s.localStart;
      _date = DateTime(local.year, local.month, local.day);
      _time = TimeOfDay(hour: local.hour, minute: local.minute);
    }
  }

  @override
  void dispose() {
    _title.dispose();
    _duration.dispose();
    _location.dispose();
    _notes.dispose();
    super.dispose();
  }

  /// Date and time as typed, i.e. on the clocks of the campaign's zone.
  DateTime? get _wallClock => _date == null || _time == null
      ? null
      : DateTime(_date!.year, _date!.month, _date!.day, _time!.hour, _time!.minute);

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _date ?? now.add(const Duration(days: 1)),
      firstDate: DateTime(2020),
      lastDate: DateTime(2100),
      helpText: 'Fecha de la sesión',
    );
    if (picked != null) setState(() => _date = picked);
  }

  Future<void> _pickTime() async {
    final picked = await showTimePicker(
      context: context,
      initialTime: _time ?? const TimeOfDay(hour: 20, minute: 0),
      helpText: 'Hora de la sesión (${widget.timeZoneId})',
    );
    if (picked != null) setState(() => _time = picked);
  }

  Future<void> _save() async {
    final valid = _formKey.currentState!.validate();
    final wall = _wallClock;
    setState(() => _dateMissing = wall == null);
    if (!valid || wall == null) return;

    final startsAt = zonedToUtc(widget.timeZoneId, wall);
    if (startsAt == null) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(content: Text('No se reconoce la zona horaria ${widget.timeZoneId}.')),
        );
      return;
    }
    final duration = int.tryParse(_duration.text.trim());
    final location = _location.text.trim();
    final notes = _notes.text.trim();
    final original = widget.session;
    final router = GoRouter.of(context);
    setState(() => _saving = true);

    final bool done;
    if (original == null) {
      final draft = SessionDraft(
        title: _title.text.trim(),
        startsAt: startsAt,
        durationMinutes: duration,
        location: location,
        notes: notes.isEmpty ? null : _notes.text,
      );
      done = await runAction(
        context,
        () async {
          await ref.read(sessionsControllerProvider(widget.campaignId).notifier).create(draft);
        },
        success: 'Sesión programada.',
        describe: describeSessionError,
      );
    } else {
      final patch = SessionPatch(
        title: _title.text.trim() == original.title ? null : _title.text.trim(),
        startsAt: startsAt.isAtSameMomentAs(original.startsAt) ? null : startsAt,
        durationMinutes: duration == original.durationMinutes ? null : Clearable(duration),
        location: location == (original.location ?? '')
            ? null
            : Clearable(location.isEmpty ? null : location),
        notes: notes == (original.notes?.trim() ?? '')
            ? null
            : Clearable(notes.isEmpty ? null : _notes.text),
      );
      if (patch.isEmpty) {
        setState(() => _saving = false);
        if (router.canPop()) router.pop();
        return;
      }
      done = await runAction(
        context,
        () => ref.read(sessionControllerProvider(original.id).notifier).patch(patch),
        success: 'Sesión guardada.',
        describe: describeSessionError,
      );
    }
    if (!mounted) return;
    setState(() => _saving = false);
    if (done && router.canPop()) router.pop();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final wall = _wallClock;
    final offset = zoneOffsetLabel(widget.timeZoneId, wall ?? DateTime.now());
    final editing = widget.session != null;

    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: Text(editing ? 'Editar sesión' : 'Nueva sesión'),
          actions: [
            TextButton(
              key: const Key('session-save'),
              onPressed: _saving ? null : _save,
              child: const Text('Guardar'),
            ),
          ],
          bottom: const TabBar(
            tabs: [
              Tab(key: Key('session-tab-edit'), text: 'Editar'),
              Tab(key: Key('session-tab-preview'), text: 'Vista previa'),
            ],
          ),
        ),
        body: TabBarView(
          children: [
            Form(
              key: _formKey,
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  TextFormField(
                    key: const Key('session-field-title'),
                    controller: _title,
                    maxLength: 200,
                    decoration: const InputDecoration(labelText: 'Título'),
                    validator: (v) => (v ?? '').trim().isEmpty ? 'Escribe un título.' : null,
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton.icon(
                          key: const Key('session-pick-date'),
                          onPressed: _pickDate,
                          icon: const Icon(Icons.calendar_today_outlined),
                          label: Text(_date == null ? 'Elegir fecha' : formatDate(_date!)),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: OutlinedButton.icon(
                          key: const Key('session-pick-time'),
                          onPressed: _pickTime,
                          icon: const Icon(Icons.schedule),
                          label: Text(
                            _time == null
                                ? 'Elegir hora'
                                : '${_time!.hour.toString().padLeft(2, '0')}:'
                                      '${_time!.minute.toString().padLeft(2, '0')}',
                          ),
                        ),
                      ),
                    ],
                  ),
                  Padding(
                    padding: const EdgeInsets.only(top: 6, left: 4),
                    child: Text(
                      'Zona horaria de la campaña: ${widget.timeZoneId}'
                      '${offset == null ? '' : ' ($offset)'}',
                      key: const Key('session-zone'),
                      style: theme.textTheme.bodySmall,
                    ),
                  ),
                  if (_dateMissing && wall == null)
                    Padding(
                      padding: const EdgeInsets.only(top: 6, left: 4),
                      child: Text(
                        'Elige la fecha y la hora.',
                        key: const Key('session-date-error'),
                        style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
                      ),
                    ),
                  const SizedBox(height: 8),
                  TextFormField(
                    key: const Key('session-field-duration'),
                    controller: _duration,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(
                      labelText: 'Duración en minutos (opcional)',
                    ),
                    validator: (v) {
                      final text = (v ?? '').trim();
                      if (text.isEmpty) return null;
                      final n = int.tryParse(text);
                      return n == null || n < 1 || n > 1440 ? 'Entre 1 y 1440 minutos.' : null;
                    },
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    key: const Key('session-field-location'),
                    controller: _location,
                    maxLength: 200,
                    decoration: const InputDecoration(labelText: 'Lugar (opcional)'),
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    key: const Key('session-field-notes'),
                    controller: _notes,
                    minLines: 5,
                    maxLines: null,
                    maxLength: 10000,
                    keyboardType: TextInputType.multiline,
                    decoration: const InputDecoration(
                      labelText: 'Notas previas (markdown)',
                      alignLabelWithHint: true,
                      border: OutlineInputBorder(),
                    ),
                  ),
                ],
              ),
            ),
            AnimatedBuilder(
              animation: Listenable.merge([_title, _notes]),
              builder: (context, _) => ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  Text(
                    _title.text.trim().isEmpty ? 'Sin título' : _title.text.trim(),
                    style: theme.textTheme.headlineSmall,
                  ),
                  if (wall != null) Text(formatLongDateTime(wall)),
                  const SizedBox(height: 12),
                  MarkdownView(key: const Key('session-notes-preview'), data: _notes.text),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
