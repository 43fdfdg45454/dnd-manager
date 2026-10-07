import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/content/content_visibility.dart';
import '../../../core/files/authenticated_image.dart';
import '../../../core/files/image_upload.dart';
import '../../../core/files/stored_file.dart';
import '../../../core/network/api_error.dart';
import '../../../core/ui/content_widgets.dart';
import '../../../core/ui/markdown_view.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/lore_controllers.dart';
import '../data/models.dart';

/// Creates (no [entryId]) or edits a lore entry. Only DMs reach it: any other
/// role sees a notice. The content is edited as markdown with a preview tab,
/// and images can be attached from the gallery.
class LoreEditorPage extends ConsumerWidget {
  const LoreEditorPage({super.key, required this.campaignId, this.entryId, this.initialParentId});

  final String campaignId;
  final String? entryId;
  final String? initialParentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final title = entryId == null ? 'Nueva entrada' : 'Editar entrada';
    final campaign = ref.watch(campaignDetailControllerProvider(campaignId));
    if (campaign.hasValue && !campaign.value!.myRole.isAtLeastDm) {
      return Scaffold(
        appBar: AppBar(title: Text(title)),
        body: const Center(child: Text('Solo el DM puede editar el lore.')),
      );
    }
    if (entryId == null) {
      return _EditorForm(campaignId: campaignId, initialParentId: initialParentId);
    }
    final entry = ref.watch(loreEntryControllerProvider(entryId!));
    return entry.when(
      loading: () => Scaffold(
        appBar: AppBar(title: Text(title)),
        body: const Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => Scaffold(
        appBar: AppBar(title: Text(title)),
        body: ContentErrorView(
          error: error,
          onRetry: () => ref.invalidate(loreEntryControllerProvider(entryId!)),
        ),
      ),
      data: (entry) => _EditorForm(campaignId: campaignId, entry: entry),
    );
  }
}

class _EditorForm extends ConsumerStatefulWidget {
  const _EditorForm({required this.campaignId, this.entry, this.initialParentId});

  final String campaignId;
  final LoreEntry? entry;
  final String? initialParentId;

  @override
  ConsumerState<_EditorForm> createState() => _EditorFormState();
}

class _EditorFormState extends ConsumerState<_EditorForm> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title;
  late final TextEditingController _content;
  late LoreCategory _category;
  late ContentVisibility _visibility;
  String? _parentId;
  String? _coverFileId;

  /// Attachments already stored on the entry, minus the ones marked for removal.
  late List<LoreAttachment> _existing;
  final _removed = <String>[];

  /// Files uploaded in this session, attached when the entry is saved.
  final _added = <StoredFile>[];
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final entry = widget.entry;
    _title = TextEditingController(text: entry?.title ?? '');
    _content = TextEditingController(text: entry?.contentMarkdown ?? '');
    _category = entry?.category ?? LoreCategory.note;
    _visibility = entry?.visibility ?? ContentVisibility.players;
    _parentId = entry?.parentId ?? widget.initialParentId;
    _coverFileId = entry?.coverFileId;
    _existing = [...?entry?.attachments];
  }

  @override
  void dispose() {
    _title.dispose();
    _content.dispose();
    super.dispose();
  }

  Future<void> _attachImage() async {
    final stored = await pickAndUploadImage(
      context,
      ref,
      kind: FileKind.loreAttachment,
      campaignId: widget.campaignId,
    );
    if (stored == null || !mounted) return;
    setState(() => _added.add(stored));
  }

  void _removeExisting(LoreAttachment attachment) => setState(() {
    _existing.remove(attachment);
    _removed.add(attachment.id);
    if (_coverFileId == attachment.fileId) _coverFileId = null;
  });

  void _removeAdded(StoredFile file) => setState(() {
    _added.remove(file);
    if (_coverFileId == file.id) _coverFileId = null;
  });

  void _toggleCover(String fileId) =>
      setState(() => _coverFileId = _coverFileId == fileId ? null : fileId);

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    final controller = ref.read(loreControllerProvider(widget.campaignId).notifier);
    final draft = LoreDraft(
      title: _title.text.trim(),
      category: _category,
      visibility: _visibility,
      contentMarkdown: _content.text,
      parentId: _parentId,
      coverFileId: _coverFileId,
    );
    final entry = widget.entry;
    final router = GoRouter.of(context);
    setState(() => _saving = true);
    final done = await runAction(
      context,
      () async {
        if (entry == null) {
          await controller.create(draft, attachFileIds: [for (final f in _added) f.id]);
        } else {
          await controller.save(
            entry.id,
            draft,
            addFileIds: [for (final f in _added) f.id],
            removeAttachmentIds: _removed,
          );
        }
      },
      success: entry == null ? 'Entrada creada.' : 'Entrada guardada.',
      describe: describeContentError,
    );
    if (!mounted) return;
    setState(() => _saving = false);
    if (done && router.canPop()) router.pop();
  }

  @override
  Widget build(BuildContext context) {
    final entry = widget.entry;
    final all = ref.watch(loreControllerProvider(widget.campaignId)).value ?? const [];
    final excluded = entry == null ? const <String>{} : descendantsOf(entry.id, all);
    final parents = all.where((e) => !excluded.contains(e.id)).toList()
      ..sort((a, b) => a.title.toLowerCase().compareTo(b.title.toLowerCase()));
    final parentValue = parents.any((e) => e.id == _parentId) ? _parentId : null;

    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: Text(entry == null ? 'Nueva entrada' : 'Editar entrada'),
          actions: [
            OfflineAware(
              builder: (context, canWrite) => TextButton(
                key: const Key('lore-save'),
                onPressed: _saving || !canWrite ? null : _save,
                child: const Text('Guardar'),
              ),
            ),
          ],
          bottom: const TabBar(
            tabs: [
              Tab(key: Key('lore-tab-edit'), text: 'Editar'),
              Tab(key: Key('lore-tab-preview'), text: 'Vista previa'),
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
                    key: const Key('lore-field-title'),
                    controller: _title,
                    maxLength: 200,
                    decoration: const InputDecoration(labelText: 'Título'),
                    validator: (value) =>
                        (value ?? '').trim().isEmpty ? 'Escribe un título.' : null,
                  ),
                  const SizedBox(height: 8),
                  DropdownButtonFormField<LoreCategory>(
                    key: const Key('lore-field-category'),
                    initialValue: _category,
                    decoration: const InputDecoration(labelText: 'Categoría'),
                    items: [
                      for (final c in LoreCategory.values)
                        DropdownMenuItem(value: c, child: Text(c.label)),
                    ],
                    onChanged: (value) => setState(() => _category = value ?? _category),
                  ),
                  const SizedBox(height: 16),
                  DropdownButtonFormField<ContentVisibility>(
                    key: const Key('lore-field-visibility'),
                    initialValue: _visibility,
                    decoration: const InputDecoration(labelText: 'Visibilidad'),
                    items: [
                      for (final v in ContentVisibility.values)
                        DropdownMenuItem(value: v, child: Text(v.label)),
                    ],
                    onChanged: (value) => setState(() => _visibility = value ?? _visibility),
                  ),
                  const SizedBox(height: 16),
                  // Rebuilt when the entries load so a stored parent is preselected.
                  KeyedSubtree(
                    key: ValueKey(parents.length),
                    child: DropdownButtonFormField<String?>(
                      key: const Key('lore-field-parent'),
                      initialValue: parentValue,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Entrada padre'),
                      items: [
                        const DropdownMenuItem<String?>(value: null, child: Text('Ninguna (raíz)')),
                        for (final p in parents)
                          DropdownMenuItem<String?>(
                            value: p.id,
                            child: Text(p.title, overflow: TextOverflow.ellipsis),
                          ),
                      ],
                      onChanged: (value) => setState(() => _parentId = value),
                    ),
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    key: const Key('lore-field-content'),
                    controller: _content,
                    minLines: 8,
                    maxLines: null,
                    maxLength: 100000,
                    keyboardType: TextInputType.multiline,
                    decoration: const InputDecoration(
                      labelText: 'Contenido (markdown)',
                      helperText: 'Enlaza otras entradas con [[slug]].',
                      alignLabelWithHint: true,
                      border: OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(height: 16),
                  _AttachmentsEditor(
                    existing: _existing,
                    added: _added,
                    coverFileId: _coverFileId,
                    onAdd: _attachImage,
                    onRemoveExisting: _removeExisting,
                    onRemoveAdded: _removeAdded,
                    onToggleCover: _toggleCover,
                  ),
                ],
              ),
            ),
            AnimatedBuilder(
              animation: Listenable.merge([_title, _content]),
              builder: (context, _) => ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  Text(
                    _title.text.trim().isEmpty ? 'Sin título' : _title.text.trim(),
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  const SizedBox(height: 12),
                  MarkdownView(
                    key: const Key('lore-preview'),
                    data: _content.text,
                    titles: {for (final e in all) e.slug: e.title},
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _AttachmentsEditor extends StatelessWidget {
  const _AttachmentsEditor({
    required this.existing,
    required this.added,
    required this.coverFileId,
    required this.onAdd,
    required this.onRemoveExisting,
    required this.onRemoveAdded,
    required this.onToggleCover,
  });

  final List<LoreAttachment> existing;
  final List<StoredFile> added;
  final String? coverFileId;
  final VoidCallback onAdd;
  final ValueChanged<LoreAttachment> onRemoveExisting;
  final ValueChanged<StoredFile> onRemoveAdded;
  final ValueChanged<String> onToggleCover;

  @override
  Widget build(BuildContext context) {
    final tiles = <Widget>[
      for (final a in existing)
        _AttachmentThumb(
          key: Key('lore-existing-${a.id}'),
          url: a.url,
          isCover: coverFileId == a.fileId,
          onCover: () => onToggleCover(a.fileId),
          onRemove: () => onRemoveExisting(a),
        ),
      for (final f in added)
        _AttachmentThumb(
          key: Key('lore-added-${f.id}'),
          url: f.url,
          isCover: coverFileId == f.id,
          onCover: () => onToggleCover(f.id),
          onRemove: () => onRemoveAdded(f),
        ),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Imágenes adjuntas', style: Theme.of(context).textTheme.titleSmall),
        const SizedBox(height: 4),
        Text(
          'Toca la estrella de una imagen para usarla como portada.',
          style: Theme.of(context).textTheme.bodySmall,
        ),
        const SizedBox(height: 8),
        Wrap(spacing: 8, runSpacing: 8, children: tiles),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          key: const Key('lore-attach'),
          onPressed: onAdd,
          icon: const Icon(Icons.add_photo_alternate_outlined),
          label: const Text('Adjuntar imagen'),
        ),
      ],
    );
  }
}

class _AttachmentThumb extends StatelessWidget {
  const _AttachmentThumb({
    super.key,
    required this.url,
    required this.isCover,
    required this.onCover,
    required this.onRemove,
  });

  final String url;
  final bool isCover;
  final VoidCallback onCover;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 96,
      height: 96,
      child: Stack(
        fit: StackFit.expand,
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: AuthenticatedImage(url: url, compact: true),
          ),
          Positioned(
            left: 0,
            top: 0,
            child: IconButton(
              tooltip: isCover ? 'Quitar portada' : 'Usar como portada',
              visualDensity: VisualDensity.compact,
              style: IconButton.styleFrom(backgroundColor: Colors.black45),
              icon: Icon(isCover ? Icons.star : Icons.star_border, color: Colors.white, size: 20),
              onPressed: onCover,
            ),
          ),
          Positioned(
            right: 0,
            top: 0,
            child: IconButton(
              tooltip: 'Quitar imagen',
              visualDensity: VisualDensity.compact,
              style: IconButton.styleFrom(backgroundColor: Colors.black45),
              icon: const Icon(Icons.close, color: Colors.white, size: 18),
              onPressed: onRemove,
            ),
          ),
        ],
      ),
    );
  }
}
