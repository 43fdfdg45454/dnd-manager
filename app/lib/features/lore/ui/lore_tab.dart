import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/files/authenticated_image.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/content_widgets.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../data/lore_controllers.dart';
import '../data/models.dart';

/// "Lore" tab of a campaign: the entries grouped by category, with a local
/// search. DMs also see the hidden entries (marked "Solo DM") and can create
/// new ones.
class LoreTab extends ConsumerStatefulWidget {
  const LoreTab({super.key, required this.campaign});

  final CampaignDetail campaign;

  @override
  ConsumerState<LoreTab> createState() => _LoreTabState();
}

class _LoreTabState extends ConsumerState<LoreTab> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final campaign = widget.campaign;
    final isDm = campaign.myRole.isAtLeastDm;
    final lore = ref.watch(loreControllerProvider(campaign.id));

    return Scaffold(
      backgroundColor: Colors.transparent,
      floatingActionButton: isDm
          ? OfflineAwareFab(
              fabKey: const Key('lore-new'),
              onPressed: () => context.push(AppRoutes.loreNew(campaign.id)),
              icon: const Icon(Icons.post_add_outlined),
              label: const Text('Nueva entrada'),
            )
          : null,
      body: lore.when(
        skipLoadingOnReload: true,
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ContentErrorView(
          error: error,
          onRetry: () => ref.invalidate(loreControllerProvider(campaign.id)),
        ),
        data: (entries) {
          final grouped = groupLore(entries, _query);
          final titleById = {for (final e in entries) e.id: e.title};
          return RefreshIndicator(
            onRefresh: () => ref.read(loreControllerProvider(campaign.id).notifier).reload(),
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.only(bottom: 96),
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                  child: TextField(
                    key: const Key('lore-search'),
                    decoration: const InputDecoration(
                      prefixIcon: Icon(Icons.search),
                      hintText: 'Buscar en el lore',
                      border: OutlineInputBorder(),
                      isDense: true,
                    ),
                    onChanged: (value) => setState(() => _query = value),
                  ),
                ),
                if (grouped.isEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 72),
                    child: Center(
                      child: Text(
                        entries.isEmpty
                            ? (isDm
                                  ? 'Aún no hay entradas de lore en esta campaña'
                                  : 'El DM aún no ha publicado lore')
                            : 'Ninguna entrada coincide con la búsqueda',
                        key: const Key('lore-empty'),
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ),
                for (final group in grouped.entries) ...[
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
                    child: Text(
                      group.key.label,
                      key: Key('lore-group-${group.key.apiValue}'),
                      style: Theme.of(context).textTheme.titleSmall
                          ?.copyWith(color: Theme.of(context).colorScheme.primary),
                    ),
                  ),
                  for (final entry in group.value)
                    _LoreTile(
                      entry: entry,
                      parentTitle: titleById[entry.parentId],
                      showDmBadge: isDm && entry.visibility.isDmOnly,
                      onTap: () => context.push(AppRoutes.loreEntry(campaign.id, entry.id)),
                    ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}

class _LoreTile extends StatelessWidget {
  const _LoreTile({
    required this.entry,
    required this.parentTitle,
    required this.showDmBadge,
    required this.onTap,
  });

  final LoreSummary entry;
  final String? parentTitle;
  final bool showDmBadge;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final cover = entry.cover;
    return ListTile(
      key: Key('lore-${entry.id}'),
      leading: cover == null
          ? const CircleAvatar(child: Icon(Icons.menu_book_outlined))
          : ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: AuthenticatedImage(url: cover, width: 48, height: 48, compact: true),
            ),
      title: Text(entry.title),
      subtitle: parentTitle == null ? null : Text('En $parentTitle'),
      trailing: showDmBadge ? DmOnlyBadge(key: Key('lore-dm-badge-${entry.id}')) : null,
      onTap: onTap,
    );
  }
}
