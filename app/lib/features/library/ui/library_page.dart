import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/files/image_upload.dart';
import '../../../core/files/stored_file.dart';
import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/content_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/library_controllers.dart';
import '../data/models.dart';

/// The PDF library of the instance, with search, category filter and a
/// download state per document. With a [campaignId] it also marks the
/// documents recommended in that campaign (DMs can change them).
class LibraryPage extends ConsumerStatefulWidget {
  const LibraryPage({super.key, this.campaignId});

  final String? campaignId;

  @override
  ConsumerState<LibraryPage> createState() => _LibraryPageState();
}

class _LibraryPageState extends ConsumerState<LibraryPage> {
  String _query = '';
  LibraryCategory? _category;
  bool _onlyRecommended = false;

  Future<void> _upload() async {
    final picked = await FilePicker.pickFile(
      type: FileType.custom,
      allowedExtensions: const ['pdf'],
    );
    final path = picked?.path;
    if (picked == null || path == null || !mounted) return;
    final data = await showDialog<LibraryFormData>(
      context: context,
      builder: (_) => LibraryFormDialog(
        initialTitle: picked.name.replaceFirst(RegExp(r'\.pdf$', caseSensitive: false), ''),
      ),
    );
    if (data == null || !mounted) return;
    final stored = await uploadWithProgress(
      context,
      ref,
      filePath: path,
      fileName: picked.name,
      kind: FileKind.libraryDocument,
      contentType: 'application/pdf',
    );
    if (stored == null || !mounted) return;
    await runAction(
      context,
      () => ref
          .read(libraryControllerProvider.notifier)
          .create(
            title: data.title,
            description: data.description,
            category: data.category,
            fileId: stored.id,
          ),
      success: 'Documento añadido a la biblioteca.',
      describe: describeContentError,
    );
  }

  Future<void> _delete(LibraryDocument document) async {
    final confirmed = await confirmAction(
      context,
      title: 'Eliminar documento',
      message: '¿Seguro que quieres eliminar "${document.title}" de la biblioteca para todos?',
      confirmLabel: 'Eliminar',
    );
    if (!confirmed || !mounted) return;
    await runAction(
      context,
      () => ref.read(libraryControllerProvider.notifier).delete(document.id),
      success: 'Documento eliminado.',
      describe: describeContentError,
      errors: const {400: 'Este documento es del sistema y no se puede eliminar.'},
    );
  }

  Future<void> _download(LibraryDocument document) => runAction(
    context,
    () => ref.read(libraryDownloadsProvider.notifier).download(document),
    success: '"${document.title}" ya está disponible sin conexión.',
    describe: describeContentError,
  );

  Future<void> _removeDownload(LibraryDocument document) => runAction(
    context,
    () => ref.read(libraryDownloadsProvider.notifier).remove(document.id),
    success: 'Descarga eliminada.',
    describe: describeContentError,
  );

  Future<void> _toggleRecommended(LibraryDocument document, bool recommended) => runAction(
    context,
    () => ref
        .read(campaignLibraryControllerProvider(widget.campaignId!).notifier)
        .setRecommended(document.id, recommended),
    success: recommended ? 'Documento recomendado.' : 'Ya no está recomendado.',
    describe: describeContentError,
  );

  @override
  Widget build(BuildContext context) {
    final campaignId = widget.campaignId;
    final auth = ref.watch(authControllerProvider);
    final isAdmin = auth is AuthSignedIn && auth.user.isAdmin;
    final isDm =
        campaignId != null &&
        (ref.watch(campaignDetailControllerProvider(campaignId)).value?.myRole.isAtLeastDm ??
            false);
    final library = ref.watch(libraryControllerProvider);
    final downloads = ref.watch(libraryDownloadsProvider);
    final recommended = campaignId == null
        ? null
        : ref.watch(campaignLibraryControllerProvider(campaignId)).value;
    final recommendedIds = {for (final d in recommended ?? const <LibraryDocument>[]) d.id};
    final notes = {for (final d in recommended ?? const <LibraryDocument>[]) d.id: d.note};

    return Scaffold(
      appBar: AppBar(title: Text(campaignId == null ? 'Biblioteca' : 'Documentos de la campaña')),
      floatingActionButton: isAdmin
          ? FloatingActionButton.extended(
              key: const Key('library-upload'),
              onPressed: _upload,
              icon: const Icon(Icons.upload_file),
              label: const Text('Subir PDF'),
            )
          : null,
      body: library.when(
        skipLoadingOnReload: true,
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ContentErrorView(
          error: error,
          onRetry: () => ref.invalidate(libraryControllerProvider),
        ),
        data: (state) {
          var documents = filterLibrary(state.documents, query: _query, category: _category);
          if (state.offline) {
            documents = documents
                .where((d) => downloads[d.id]?.status == DownloadStatus.available)
                .toList();
          }
          if (_onlyRecommended) {
            documents = documents.where((d) => recommendedIds.contains(d.id)).toList();
          }
          return RefreshIndicator(
            onRefresh: () => ref.read(libraryControllerProvider.notifier).reload(),
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.only(bottom: 96),
              children: [
                if (state.offline)
                  const MaterialBanner(
                    key: Key('library-offline'),
                    content: Text('Sin conexión: solo se muestran los documentos descargados.'),
                    actions: [SizedBox.shrink()],
                  ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                  child: TextField(
                    key: const Key('library-search'),
                    decoration: const InputDecoration(
                      prefixIcon: Icon(Icons.search),
                      hintText: 'Buscar documentos',
                      border: OutlineInputBorder(),
                      isDense: true,
                    ),
                    onChanged: (value) => setState(() => _query = value),
                  ),
                ),
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  child: Row(
                    children: [
                      if (campaignId != null)
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 4),
                          child: FilterChip(
                            key: const Key('library-filter-recommended'),
                            avatar: const Icon(Icons.star_outline, size: 18),
                            label: const Text('Recomendados'),
                            selected: _onlyRecommended,
                            onSelected: (v) => setState(() => _onlyRecommended = v),
                          ),
                        ),
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 4),
                        child: ChoiceChip(
                          key: const Key('library-category-all'),
                          label: const Text('Todas'),
                          selected: _category == null,
                          onSelected: (_) => setState(() => _category = null),
                        ),
                      ),
                      for (final c in LibraryCategory.values)
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 4),
                          child: ChoiceChip(
                            key: Key('library-category-${c.apiValue}'),
                            label: Text(c.label),
                            selected: _category == c,
                            onSelected: (_) => setState(() => _category = c),
                          ),
                        ),
                    ],
                  ),
                ),
                if (documents.isEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 72),
                    child: Center(
                      child: Text(
                        state.documents.isEmpty
                            ? 'La biblioteca aún no tiene documentos'
                            : 'Ningún documento coincide con el filtro',
                        key: const Key('library-empty'),
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ),
                for (final document in documents)
                  _DocumentTile(
                    document: document,
                    download: downloads[document.id] ?? DocumentDownload.idle,
                    recommended: recommendedIds.contains(document.id),
                    note: notes[document.id],
                    showRecommend: isDm && !state.offline,
                    canDelete: isAdmin && !document.isSystem && !state.offline,
                    onOpen: () => context.push(AppRoutes.libraryDocument(document.id)),
                    onDownload: () => _download(document),
                    onCancel: () => ref.read(libraryDownloadsProvider.notifier).cancel(document.id),
                    onRemoveDownload: () => _removeDownload(document),
                    onToggleRecommended: (value) => _toggleRecommended(document, value),
                    onDelete: () => _delete(document),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _DocumentTile extends StatelessWidget {
  const _DocumentTile({
    required this.document,
    required this.download,
    required this.recommended,
    required this.note,
    required this.showRecommend,
    required this.canDelete,
    required this.onOpen,
    required this.onDownload,
    required this.onCancel,
    required this.onRemoveDownload,
    required this.onToggleRecommended,
    required this.onDelete,
  });

  final LibraryDocument document;
  final DocumentDownload download;
  final bool recommended;
  final String? note;
  final bool showRecommend;
  final bool canDelete;
  final VoidCallback onOpen;
  final VoidCallback onDownload;
  final VoidCallback onCancel;
  final VoidCallback onRemoveDownload;
  final ValueChanged<bool> onToggleRecommended;
  final VoidCallback onDelete;

  String get _details => [
    document.category.label,
    if (document.pageCount != null) '${document.pageCount} págs.',
    if (document.sizeBytes > 0) formatFileSize(document.sizeBytes),
  ].join(' · ');

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final id = document.id;
    final status = switch (download.status) {
      DownloadStatus.idle => 'No descargado',
      DownloadStatus.downloading => 'Descargando…',
      DownloadStatus.available => 'Disponible sin conexión',
    };
    return ListTile(
      key: Key('library-doc-$id'),
      isThreeLine: true,
      leading: const CircleAvatar(child: Icon(Icons.picture_as_pdf_outlined)),
      title: Text(document.title),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(_details),
          if (note != null && recommended)
            Text('Nota del DM: $note', style: theme.textTheme.bodySmall),
          Text(
            status,
            key: Key('library-status-$id'),
            style: theme.textTheme.bodySmall?.copyWith(
              color: download.status == DownloadStatus.available ? theme.colorScheme.primary : null,
            ),
          ),
        ],
      ),
      onTap: onOpen,
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (showRecommend)
            IconButton(
              key: Key('library-recommend-$id'),
              tooltip: recommended ? 'Quitar de recomendados' : 'Recomendar en la campaña',
              icon: Icon(recommended ? Icons.star : Icons.star_border),
              onPressed: () => onToggleRecommended(!recommended),
            )
          else if (recommended)
            Icon(Icons.star, color: theme.colorScheme.primary, size: 20),
          switch (download.status) {
            DownloadStatus.idle => IconButton(
              key: Key('library-download-$id'),
              tooltip: 'Descargar',
              icon: const Icon(Icons.download_outlined),
              onPressed: onDownload,
            ),
            DownloadStatus.downloading => IconButton(
              key: Key('library-progress-$id'),
              tooltip: 'Cancelar descarga',
              onPressed: onCancel,
              icon: Stack(
                alignment: Alignment.center,
                children: [
                  SizedBox(
                    width: 24,
                    height: 24,
                    child: CircularProgressIndicator(value: download.progress, strokeWidth: 2.5),
                  ),
                  const Icon(Icons.close, size: 14),
                ],
              ),
            ),
            DownloadStatus.available => IconButton(
              key: Key('library-remove-$id'),
              tooltip: 'Eliminar descarga',
              icon: const Icon(Icons.download_done),
              onPressed: onRemoveDownload,
            ),
          },
          if (canDelete)
            PopupMenuButton<String>(
              key: Key('library-menu-$id'),
              onSelected: (_) => onDelete(),
              itemBuilder: (_) => const [PopupMenuItem(value: 'delete', child: Text('Eliminar'))],
            ),
        ],
      ),
    );
  }
}

class LibraryFormData {
  const LibraryFormData({required this.title, this.description, required this.category});

  final String title;
  final String? description;
  final LibraryCategory category;
}

/// Title, description and category of a PDF about to be published.
class LibraryFormDialog extends StatefulWidget {
  const LibraryFormDialog({super.key, this.initialTitle = ''});

  final String initialTitle;

  @override
  State<LibraryFormDialog> createState() => _LibraryFormDialogState();
}

class _LibraryFormDialogState extends State<LibraryFormDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title = TextEditingController(text: widget.initialTitle);
  final _description = TextEditingController();
  LibraryCategory _category = LibraryCategory.other;

  @override
  void dispose() {
    _title.dispose();
    _description.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    final description = _description.text.trim();
    Navigator.of(context).pop(
      LibraryFormData(
        title: _title.text.trim(),
        description: description.isEmpty ? null : description,
        category: _category,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Nuevo documento'),
      content: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                key: const Key('library-field-title'),
                controller: _title,
                autofocus: true,
                maxLength: 200,
                decoration: const InputDecoration(labelText: 'Título'),
                validator: (v) => (v ?? '').trim().isEmpty ? 'Escribe un título.' : null,
              ),
              TextFormField(
                key: const Key('library-field-description'),
                controller: _description,
                maxLength: 2000,
                minLines: 1,
                maxLines: 4,
                decoration: const InputDecoration(labelText: 'Descripción (opcional)'),
              ),
              DropdownButtonFormField<LibraryCategory>(
                key: const Key('library-field-category'),
                initialValue: _category,
                decoration: const InputDecoration(labelText: 'Categoría'),
                items: [
                  for (final c in LibraryCategory.values)
                    DropdownMenuItem(value: c, child: Text(c.label)),
                ],
                onChanged: (v) => setState(() => _category = v ?? _category),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('library-form-submit'),
          onPressed: _submit,
          child: const Text('Subir'),
        ),
      ],
    );
  }
}
