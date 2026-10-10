import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/content/content_visibility.dart';
import '../../../core/files/authenticated_image.dart';
import '../../../core/files/image_upload.dart';
import '../../../core/files/stored_file.dart';
import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/content_widgets.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/maps_controllers.dart';
import '../data/models.dart';

/// "Mapas" tab of a campaign: the maps with a thumbnail. DMs also see the
/// hidden ones and can upload, rename, hide and delete maps.
class MapsTab extends ConsumerWidget {
  const MapsTab({super.key, required this.campaign});

  final CampaignDetail campaign;

  Future<void> _upload(BuildContext context, WidgetRef ref) async {
    final stored = await pickAndUploadImage(
      context,
      ref,
      kind: FileKind.mapImage,
      campaignId: campaign.id,
      maxWidth: 4096,
      quality: 90,
    );
    if (stored == null || !context.mounted) return;
    final data = await showDialog<MapFormData>(
      context: context,
      builder: (_) => MapFormDialog(
        title: 'Nuevo mapa',
        initialName: stored.fileName.replaceFirst(RegExp(r'\.[^.]*$'), ''),
      ),
    );
    if (data == null || !context.mounted) return;
    await runAction(
      context,
      () => ref
          .read(mapsControllerProvider(campaign.id).notifier)
          .create(name: data.name, fileId: stored.id, visibility: data.visibility),
      success: 'Mapa creado.',
      describe: describeContentError,
    );
  }

  Future<void> _edit(BuildContext context, WidgetRef ref, MapSummary map) async {
    final data = await showDialog<MapFormData>(
      context: context,
      builder: (_) => MapFormDialog(
        title: 'Editar mapa',
        initialName: map.name,
        initialVisibility: map.visibility,
      ),
    );
    if (data == null || !context.mounted) return;
    await runAction(
      context,
      () => ref
          .read(mapsControllerProvider(campaign.id).notifier)
          .edit(map.id, name: data.name, visibility: data.visibility),
      success: 'Mapa actualizado.',
      describe: describeContentError,
    );
  }

  Future<void> _delete(BuildContext context, WidgetRef ref, MapSummary map) async {
    final confirmed = await confirmAction(
      context,
      title: 'Eliminar mapa',
      message: '¿Seguro que quieres eliminar "${map.name}" y todos sus pines?',
      confirmLabel: 'Eliminar',
    );
    if (!confirmed || !context.mounted) return;
    await runAction(
      context,
      () => ref.read(mapsControllerProvider(campaign.id).notifier).delete(map.id),
      success: 'Mapa eliminado.',
      describe: describeContentError,
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isDm = campaign.myRole.isAtLeastDm;
    final maps = ref.watch(mapsControllerProvider(campaign.id));

    return Scaffold(
      backgroundColor: Colors.transparent,
      floatingActionButton: isDm
          ? OfflineAwareFab(
              fabKey: const Key('maps-new'),
              onPressed: () => _upload(context, ref),
              icon: const Icon(Icons.add_photo_alternate_outlined),
              label: const Text('Subir mapa'),
            )
          : null,
      body: maps.when(
        skipLoadingOnReload: true,
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ContentErrorView(
          error: error,
          onRetry: () => ref.invalidate(mapsControllerProvider(campaign.id)),
        ),
        data: (list) => RefreshIndicator(
          onRefresh: () => ref.read(mapsControllerProvider(campaign.id).notifier).reload(),
          child: list.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: [
                    const SizedBox(height: 96),
                    Center(
                      child: Text(
                        isDm
                            ? 'Aún no hay mapas en esta campaña'
                            : 'El DM aún no ha publicado mapas',
                        key: const Key('maps-empty'),
                      ),
                    ),
                  ],
                )
              : ListView(
                  padding: const EdgeInsets.only(top: 8, bottom: 96),
                  children: [
                    for (final map in list)
                      _MapTile(
                        map: map,
                        isDm: isDm,
                        onOpen: () => context.push(AppRoutes.map(campaign.id, map.id)),
                        onEdit: () => _edit(context, ref, map),
                        onDelete: () => _delete(context, ref, map),
                      ),
                  ],
                ),
        ),
      ),
    );
  }
}

enum _MapAction { edit, delete }

class _MapTile extends StatelessWidget {
  const _MapTile({
    required this.map,
    required this.isDm,
    required this.onOpen,
    required this.onEdit,
    required this.onDelete,
  });

  final MapSummary map;
  final bool isDm;
  final VoidCallback onOpen;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    return Card(
      key: Key('map-${map.id}'),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onOpen,
        child: Row(
          children: [
            SizedBox(width: 96, height: 72, child: AuthenticatedImage(url: map.url, compact: true)),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    map.name,
                    style: Theme.of(context).textTheme.titleMedium,
                    overflow: TextOverflow.ellipsis,
                  ),
                  if (isDm && map.visibility.isDmOnly)
                    const Padding(
                      padding: EdgeInsets.only(top: 4),
                      child: Align(alignment: Alignment.centerLeft, child: DmOnlyBadge()),
                    ),
                ],
              ),
            ),
            if (isDm)
              PopupMenuButton<_MapAction>(
                key: Key('map-menu-${map.id}'),
                onSelected: (action) => switch (action) {
                  _MapAction.edit => onEdit(),
                  _MapAction.delete => onDelete(),
                },
                itemBuilder: (_) => const [
                  PopupMenuItem(value: _MapAction.edit, child: Text('Editar')),
                  PopupMenuItem(value: _MapAction.delete, child: Text('Eliminar')),
                ],
              )
            else
              const SizedBox(width: 12),
          ],
        ),
      ),
    );
  }
}

class MapFormData {
  const MapFormData({required this.name, required this.visibility});

  final String name;
  final ContentVisibility visibility;
}

/// Name and visibility of a map.
class MapFormDialog extends StatefulWidget {
  const MapFormDialog({
    super.key,
    required this.title,
    this.initialName = '',
    this.initialVisibility = ContentVisibility.players,
  });

  final String title;
  final String initialName;
  final ContentVisibility initialVisibility;

  @override
  State<MapFormDialog> createState() => _MapFormDialogState();
}

class _MapFormDialogState extends State<MapFormDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _name = TextEditingController(text: widget.initialName);
  late ContentVisibility _visibility = widget.initialVisibility;

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop(MapFormData(name: _name.text.trim(), visibility: _visibility));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextFormField(
              key: const Key('map-field-name'),
              controller: _name,
              autofocus: true,
              maxLength: 100,
              decoration: const InputDecoration(labelText: 'Nombre'),
              validator: (v) => (v ?? '').trim().isEmpty ? 'Escribe un nombre.' : null,
              onFieldSubmitted: (_) => _submit(),
            ),
            DropdownButtonFormField<ContentVisibility>(
              key: const Key('map-field-visibility'),
              initialValue: _visibility,
              decoration: const InputDecoration(labelText: 'Visibilidad'),
              items: [
                for (final v in ContentVisibility.values)
                  DropdownMenuItem(value: v, child: Text(v.label)),
              ],
              onChanged: (v) => setState(() => _visibility = v ?? _visibility),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('map-form-submit'),
          onPressed: _submit,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}
