import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/ui/markdown_view.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/models.dart';
import '../data/sessions_controllers.dart';
import 'session_widgets.dart';

/// Edits the journal summary of a session as markdown, with a preview tab.
/// Only DMs reach it. Saving an empty text removes the summary.
class SummaryEditorPage extends ConsumerWidget {
  const SummaryEditorPage({super.key, required this.campaignId, required this.sessionId});

  final String campaignId;
  final String sessionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    const title = 'Resumen de la sesión';
    final campaign = ref.watch(campaignDetailControllerProvider(campaignId));
    if (campaign.hasValue && !campaign.value!.myRole.isAtLeastDm) {
      return Scaffold(
        appBar: AppBar(title: const Text(title)),
        body: const Center(child: Text('Solo el DM puede editar el resumen.')),
      );
    }
    final session = ref.watch(sessionControllerProvider(sessionId));
    return session.when(
      loading: () => Scaffold(
        appBar: AppBar(title: const Text(title)),
        body: const Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => Scaffold(
        appBar: AppBar(title: const Text(title)),
        body: Center(
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
      ),
      data: (s) => _Editor(session: s),
    );
  }
}

class _Editor extends ConsumerStatefulWidget {
  const _Editor({required this.session});

  final Session session;

  @override
  ConsumerState<_Editor> createState() => _EditorState();
}

class _EditorState extends ConsumerState<_Editor> {
  late final TextEditingController _text;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _text = TextEditingController(text: widget.session.summaryMarkdown ?? '');
  }

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final router = GoRouter.of(context);
    final removing = _text.text.trim().isEmpty;
    setState(() => _saving = true);
    final done = await runAction(
      context,
      () => ref.read(sessionControllerProvider(widget.session.id).notifier).setSummary(_text.text),
      success: removing ? 'Resumen eliminado.' : 'Resumen guardado.',
      describe: describeSessionError,
    );
    if (!mounted) return;
    setState(() => _saving = false);
    if (done && router.canPop()) router.pop();
  }

  @override
  Widget build(BuildContext context) {
    final session = widget.session;
    final heading = sessionHeading(session.number, session.title);
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Resumen de la sesión'),
          actions: [
            OfflineAware(
              builder: (context, canWrite) => TextButton(
                key: const Key('summary-save'),
                onPressed: _saving || !canWrite ? null : _save,
                child: const Text('Guardar'),
              ),
            ),
          ],
          bottom: const TabBar(
            tabs: [
              Tab(key: Key('summary-tab-edit'), text: 'Editar'),
              Tab(key: Key('summary-tab-preview'), text: 'Vista previa'),
            ],
          ),
        ),
        body: TabBarView(
          children: [
            ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Text(heading, style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 12),
                TextField(
                  key: const Key('summary-field'),
                  controller: _text,
                  minLines: 12,
                  maxLines: null,
                  maxLength: 100000,
                  keyboardType: TextInputType.multiline,
                  decoration: const InputDecoration(
                    labelText: 'Resumen (markdown)',
                    helperText: 'Lo leen todos los miembros. Déjalo vacío para borrarlo.',
                    alignLabelWithHint: true,
                    border: OutlineInputBorder(),
                  ),
                ),
              ],
            ),
            AnimatedBuilder(
              animation: _text,
              builder: (context, _) => ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  Text(heading, style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 12),
                  if (_text.text.trim().isEmpty)
                    const Text('Sin contenido.')
                  else
                    MarkdownView(key: const Key('summary-preview'), data: _text.text),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
