import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../../core/ui/markdown_view.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../data/models.dart';
import '../data/sessions_controllers.dart';
import '../domain/journal_entries.dart';
import '../domain/sessions_format.dart';
import 'session_widgets.dart';

/// "Diario" tab: the sessions of the campaign in chronological order with the
/// summary written by the DM, and a local text search. Players only read; DMs
/// also see the sessions that are missing a summary and can edit any of them.
class JournalTab extends ConsumerStatefulWidget {
  const JournalTab({super.key, required this.campaign});

  final CampaignDetail campaign;

  @override
  ConsumerState<JournalTab> createState() => _JournalTabState();
}

class _JournalTabState extends ConsumerState<JournalTab> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final campaign = widget.campaign;
    final isDm = campaign.myRole.isAtLeastDm;
    final journal = ref.watch(journalControllerProvider(campaign.id));
    // The DM also needs the sessions without summary; a failure there only hides them.
    final dmSessions = isDm ? ref.watch(sessionsControllerProvider(campaign.id)).value : null;
    final now = ref.watch(sessionsClockProvider)();

    return journal.when(
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
                onPressed: () => ref.invalidate(journalControllerProvider(campaign.id)),
                icon: const Icon(Icons.refresh),
                label: const Text('Reintentar'),
              ),
            ],
          ),
        ),
      ),
      data: (summaries) {
        final all = buildJournalEntries(summaries, dmSessions: dmSessions, now: now);
        final entries = filterJournal(all, _query);
        return RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(journalControllerProvider(campaign.id));
            ref.invalidate(sessionsControllerProvider(campaign.id));
            await ref.read(journalControllerProvider(campaign.id).future);
          },
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.only(bottom: 24),
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
                child: TextField(
                  key: const Key('journal-search'),
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    hintText: 'Buscar en el diario',
                    border: OutlineInputBorder(),
                    isDense: true,
                  ),
                  onChanged: (value) => setState(() => _query = value),
                ),
              ),
              if (all.isEmpty)
                const Padding(
                  key: Key('journal-empty'),
                  padding: EdgeInsets.all(24),
                  child: Text('El diario está vacío: aún no hay resúmenes de sesiones.'),
                )
              else if (entries.isEmpty)
                const Padding(
                  key: Key('journal-no-results'),
                  padding: EdgeInsets.all(24),
                  child: Text('Ninguna sesión coincide con la búsqueda.'),
                )
              else
                for (final entry in entries)
                  _JournalEntryTile(campaignId: campaign.id, entry: entry, isDm: isDm),
            ],
          ),
        );
      },
    );
  }
}

class _JournalEntryTile extends StatelessWidget {
  const _JournalEntryTile({required this.campaignId, required this.entry, required this.isDm});

  final String campaignId;
  final JournalEntry entry;
  final bool isDm;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final heading = '${sessionHeading(entry.number, entry.title)} · ${formatDate(entry.localStart)}';
    return Card(
      key: Key('journal-entry-${entry.id}'),
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      child: ExpansionTile(
        key: PageStorageKey('journal-${entry.id}'),
        initiallyExpanded: true,
        shape: const Border(),
        collapsedShape: const Border(),
        title: Text(heading, style: theme.textTheme.titleSmall),
        subtitle: entry.status == SessionStatus.scheduled ? null : Text(entry.status.label),
        childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
        expandedCrossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (entry.hasSummary)
            MarkdownView(key: Key('journal-summary-${entry.id}'), data: entry.summaryMarkdown!)
          else
            Text(
              'Sin resumen todavía',
              key: Key('journal-empty-${entry.id}'),
              style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic),
            ),
          if (isDm)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                key: Key('journal-edit-${entry.id}'),
                onPressed: () => context.push(AppRoutes.sessionSummary(campaignId, entry.id)),
                icon: const Icon(Icons.edit_outlined),
                label: Text(entry.hasSummary ? 'Editar resumen' : 'Escribir resumen'),
              ),
            ),
        ],
      ),
    );
  }
}
