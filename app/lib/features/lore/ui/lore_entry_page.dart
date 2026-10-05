import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/cache/stale_data.dart';
import '../../../core/files/authenticated_image.dart';
import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/content_widgets.dart';
import '../../../core/ui/markdown_view.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/lore_controllers.dart';
import '../data/lore_repository.dart';
import '../data/models.dart';

/// The entry whose slug (or, failing that, title) is [slug], or null.
LoreSummary? findLoreBySlug(List<LoreSummary> entries, String slug) {
  for (final e in entries) {
    if (e.slug == slug) return e;
  }
  final lower = slug.toLowerCase();
  for (final e in entries) {
    if (e.slug.toLowerCase() == lower || e.title.toLowerCase() == lower) return e;
  }
  return null;
}

/// Reads one lore entry: cover, rendered markdown (with `[[slug]]` links that
/// open other entries), sub-entries and the attachment gallery. DMs can edit
/// and delete it.
class LoreEntryPage extends ConsumerWidget {
  const LoreEntryPage({super.key, required this.campaignId, required this.entryId});

  final String campaignId;
  final String entryId;

  Future<void> _delete(BuildContext context, WidgetRef ref, LoreEntry entry) async {
    final confirmed = await confirmAction(
      context,
      title: 'Eliminar entrada',
      message:
          '¿Seguro que quieres eliminar "${entry.title}"? Sus subentradas pasarán al nivel raíz.',
      confirmLabel: 'Eliminar',
    );
    if (!confirmed || !context.mounted) return;
    final router = GoRouter.of(context);
    final done = await runAction(
      context,
      () => ref.read(loreControllerProvider(campaignId).notifier).delete(entry.id),
      success: 'Entrada eliminada.',
      describe: describeContentError,
    );
    if (!done) return;
    if (router.canPop()) {
      router.pop();
    } else {
      router.go(AppRoutes.campaign(campaignId));
    }
  }

  void _openSlug(BuildContext context, List<LoreSummary> entries, String slug) {
    final target = findLoreBySlug(entries, slug);
    if (target == null) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text('No existe la entrada "$slug".')));
      return;
    }
    context.push(AppRoutes.loreEntry(campaignId, target.id));
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final entry = ref.watch(loreEntryControllerProvider(entryId));
    final role = ref.watch(campaignDetailControllerProvider(campaignId)).value?.myRole;
    final isDm = role?.isAtLeastDm ?? false;
    final all = ref.watch(loreControllerProvider(campaignId)).value ?? const <LoreSummary>[];

    return Scaffold(
      appBar: AppBar(
        title: Text(entry.value?.title ?? 'Lore'),
        actions: [
          if (isDm && entry.value != null) ...[
            OfflineAware(
              builder: (context, canWrite) => IconButton(
                key: const Key('lore-edit'),
                tooltip: 'Editar',
                icon: const Icon(Icons.edit_outlined),
                onPressed: !canWrite
                    ? null
                    : () => context.push(AppRoutes.loreEdit(campaignId, entryId)),
              ),
            ),
            IconButton(
              key: const Key('lore-delete'),
              tooltip: 'Eliminar',
              icon: const Icon(Icons.delete_outline),
              onPressed: () => _delete(context, ref, entry.value!),
            ),
          ],
        ],
      ),
      body: OfflineBannerLayout(
        scopes: [staleTree(LoreRepository.entryPath(entryId))],
        child: entry.when(
          skipLoadingOnReload: true,
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ContentErrorView(
            error: error,
            onRetry: () => ref.invalidate(loreEntryControllerProvider(entryId)),
          ),
          data: (entry) {
            final children = all.where((e) => e.parentId == entry.id).toList();
            final images = entry.attachments.where((a) => a.isImage).toList();
            final others = entry.attachments.where((a) => !a.isImage).toList();
            final theme = Theme.of(context);
            return ListView(
              padding: const EdgeInsets.only(bottom: 32),
              children: [
                if (entry.cover != null)
                  AspectRatio(
                    aspectRatio: 16 / 9,
                    child: AuthenticatedImage(key: const Key('lore-cover'), url: entry.cover!),
                  ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        entry.title,
                        key: const Key('lore-title'),
                        style: theme.textTheme.headlineSmall,
                      ),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 8,
                        runSpacing: 4,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        children: [
                          Chip(
                            label: Text(entry.category.label),
                            visualDensity: VisualDensity.compact,
                          ),
                          if (isDm && entry.visibility.isDmOnly)
                            const DmOnlyBadge(key: Key('lore-dm-badge')),
                        ],
                      ),
                      const SizedBox(height: 12),
                      if (entry.contentMarkdown.trim().isEmpty)
                        Text(
                          'Esta entrada no tiene contenido.',
                          style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic),
                        )
                      else
                        MarkdownView(
                          key: const Key('lore-content'),
                          data: entry.contentMarkdown,
                          titles: {for (final e in all) e.slug: e.title},
                          onLoreLink: (slug) => _openSlug(context, all, slug),
                        ),
                    ],
                  ),
                ),
                if (children.isNotEmpty) ...[
                  const _SectionTitle('Subentradas'),
                  for (final child in children)
                    ListTile(
                      key: Key('lore-child-${child.id}'),
                      leading: const Icon(Icons.subdirectory_arrow_right),
                      title: Text(child.title),
                      trailing: isDm && child.visibility.isDmOnly ? const DmOnlyBadge() : null,
                      onTap: () => context.push(AppRoutes.loreEntry(campaignId, child.id)),
                    ),
                ],
                if (images.isNotEmpty) ...[
                  const _SectionTitle('Galería'),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: GridView.count(
                      key: const Key('lore-gallery'),
                      crossAxisCount: 3,
                      mainAxisSpacing: 8,
                      crossAxisSpacing: 8,
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      children: [
                        for (final image in images)
                          InkWell(
                            key: Key('lore-attachment-${image.id}'),
                            onTap: () => _showImage(context, image),
                            child: ClipRRect(
                              borderRadius: BorderRadius.circular(8),
                              child: AuthenticatedImage(url: image.url, compact: true),
                            ),
                          ),
                      ],
                    ),
                  ),
                ],
                if (others.isNotEmpty) ...[
                  const _SectionTitle('Archivos adjuntos'),
                  for (final file in others)
                    ListTile(
                      leading: const Icon(Icons.attach_file),
                      title: Text(file.caption ?? file.fileName),
                    ),
                ],
              ],
            );
          },
        ),
      ),
    );
  }

  void _showImage(BuildContext context, LoreAttachment image) {
    showDialog<void>(
      context: context,
      builder: (dialogContext) => Dialog.fullscreen(
        child: Stack(
          children: [
            Positioned.fill(
              child: InteractiveViewer(
                maxScale: 6,
                child: AuthenticatedImage(url: image.url, fit: BoxFit.contain),
              ),
            ),
            if (image.caption != null)
              Positioned(
                left: 16,
                right: 16,
                bottom: 24,
                child: Text(
                  image.caption!,
                  textAlign: TextAlign.center,
                  style: Theme.of(dialogContext).textTheme.bodyMedium,
                ),
              ),
            Positioned(
              top: 8,
              right: 8,
              child: IconButton.filledTonal(
                tooltip: 'Cerrar',
                icon: const Icon(Icons.close),
                onPressed: () => Navigator.of(dialogContext).pop(),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(16, 20, 16, 8),
    child: Text(
      text,
      style: Theme.of(context).textTheme.titleSmall
          ?.copyWith(color: Theme.of(context).colorScheme.primary),
    ),
  );
}
