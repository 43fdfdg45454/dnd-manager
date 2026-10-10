import 'dart:math' as math;

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
import '../../lore/data/lore_controllers.dart';
import '../../lore/data/models.dart';
import '../../lore/ui/lore_entry_page.dart' show findLoreBySlug;
import '../data/maps_controllers.dart';
import '../data/maps_repository.dart';
import '../data/models.dart';
import 'pin_form_dialog.dart';

/// Size of the pin glyph on screen, whatever the zoom.
const _pinSize = 40.0;

/// A map image with zoom and pan and its pins, placed by relative x and y so
/// they follow the rendered size. Tapping a pin opens a sheet with its note and
/// lore link. DMs add pins (long press, or the app bar button at the centre of
/// the view), drag them to move them, and edit or delete them.
class MapViewerPage extends ConsumerStatefulWidget {
  const MapViewerPage({super.key, required this.campaignId, required this.mapId});

  final String campaignId;
  final String mapId;

  @override
  ConsumerState<MapViewerPage> createState() => _MapViewerPageState();
}

class _MapViewerPageState extends ConsumerState<MapViewerPage> {
  final _transform = TransformationController();

  /// Pin being dragged and its current relative position.
  String? _draggingId;
  Offset _dragPosition = Offset.zero;

  @override
  void dispose() {
    _transform.dispose();
    super.dispose();
  }

  MapDetailController get _controller =>
      ref.read(mapDetailControllerProvider(widget.mapId).notifier);

  Future<void> _addPin(double x, double y) async {
    final lore = ref.read(loreControllerProvider(widget.campaignId)).value ?? const [];
    final draft = await showDialog<PinDraft>(
      context: context,
      builder: (_) => PinFormDialog(title: 'Nuevo pin', loreEntries: lore),
    );
    if (draft == null || !mounted) return;
    await runAction(
      context,
      () => _controller.addPin(x: x, y: y, draft: draft),
      success: 'Pin añadido.',
      describe: describeContentError,
    );
  }

  Future<void> _editPin(MapPin pin) async {
    final lore = ref.read(loreControllerProvider(widget.campaignId)).value ?? const [];
    final draft = await showDialog<PinDraft>(
      context: context,
      builder: (_) =>
          PinFormDialog(title: 'Editar pin', initial: PinDraft.of(pin), loreEntries: lore),
    );
    if (draft == null || !mounted) return;
    await runAction(
      context,
      () => _controller.editPin(pin.id, draft),
      success: 'Pin actualizado.',
      describe: describeContentError,
    );
  }

  Future<void> _deletePin(MapPin pin) async {
    final confirmed = await confirmAction(
      context,
      title: 'Eliminar pin',
      message: '¿Seguro que quieres eliminar "${pin.title}"?',
      confirmLabel: 'Eliminar',
    );
    if (!confirmed || !mounted) return;
    await runAction(
      context,
      () => _controller.deletePin(pin.id),
      success: 'Pin eliminado.',
      describe: describeContentError,
    );
  }

  Future<void> _endDrag(MapPin pin) async {
    final target = _dragPosition;
    setState(() => _draggingId = null);
    await runAction(
      context,
      () => _controller.movePin(pin.id, x: target.dx, y: target.dy),
      describe: describeContentError,
    );
  }

  void _openLore(String entryId) => context.push(AppRoutes.loreEntry(widget.campaignId, entryId));

  void _openSlug(String slug) {
    final lore = ref.read(loreControllerProvider(widget.campaignId)).value ?? const [];
    final target = findLoreBySlug(lore, slug);
    if (target == null) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text('No existe la entrada "$slug".')));
      return;
    }
    _openLore(target.id);
  }

  void _showPin(MapPin pin, {required bool isDm}) {
    final lore = ref.read(loreControllerProvider(widget.campaignId)).value ?? const <LoreSummary>[];
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) {
        final theme = Theme.of(sheetContext);
        void closeThen(VoidCallback action) {
          Navigator.of(sheetContext).pop();
          action();
        }

        final linked = lore.where((e) => e.id == pin.loreEntryId).firstOrNull;
        return SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
            child: Column(
              key: const Key('pin-sheet'),
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(pin.icon.icon, color: parsePinColor(pin.color)),
                    const SizedBox(width: 8),
                    Expanded(child: Text(pin.title, style: theme.textTheme.titleLarge)),
                    if (isDm && pin.visibility.isDmOnly) const DmOnlyBadge(),
                  ],
                ),
                const SizedBox(height: 8),
                if ((pin.note ?? '').trim().isEmpty)
                  Text(
                    'Sin nota.',
                    style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic),
                  )
                else
                  MarkdownView(
                    data: pin.note!,
                    titles: {for (final e in lore) e.slug: e.title},
                    onLoreLink: (slug) => closeThen(() => _openSlug(slug)),
                  ),
                if (pin.loreEntryId != null) ...[
                  const SizedBox(height: 8),
                  OutlinedButton.icon(
                    key: const Key('pin-lore-link'),
                    onPressed: () => closeThen(() => _openLore(pin.loreEntryId!)),
                    icon: const Icon(Icons.menu_book_outlined),
                    label: Text(linked == null ? 'Ver entrada de lore' : 'Ver "${linked.title}"'),
                  ),
                ],
                if (isDm) ...[
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      OfflineAware(
                        builder: (context, canWrite) => FilledButton.tonalIcon(
                          key: const Key('pin-edit'),
                          onPressed: !canWrite ? null : () => closeThen(() => _editPin(pin)),
                          icon: const Icon(Icons.edit_outlined),
                          label: const Text('Editar'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      OutlinedButton.icon(
                        key: const Key('pin-delete'),
                        style: OutlinedButton.styleFrom(foregroundColor: theme.colorScheme.error),
                        onPressed: () => closeThen(() => _deletePin(pin)),
                        icon: const Icon(Icons.delete_outline),
                        label: const Text('Eliminar'),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final map = ref.watch(mapDetailControllerProvider(widget.mapId));
    final isDm =
        ref.watch(campaignDetailControllerProvider(widget.campaignId)).value?.myRole.isAtLeastDm ??
        false;
    // Loaded here so `[[slug]]` links in notes resolve and the pin form can list entries.
    ref.watch(loreControllerProvider(widget.campaignId));

    return Scaffold(
      appBar: AppBar(title: Text(map.value?.name ?? 'Mapa')),
      body: OfflineBannerLayout(
        scopes: [staleTree(MapsRepository.mapPath(widget.mapId))],
        child: map.when(
          skipLoadingOnReload: true,
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ContentErrorView(
            error: error,
            onRetry: () => ref.invalidate(mapDetailControllerProvider(widget.mapId)),
          ),
          data: (map) => LayoutBuilder(
            builder: (context, constraints) {
              final viewport = constraints.biggest;
              final image = _fit(viewport, map.aspectRatio);
              return Stack(
                children: [
                  InteractiveViewer(
                    transformationController: _transform,
                    minScale: 1,
                    maxScale: 8,
                    panEnabled: _draggingId == null,
                    scaleEnabled: _draggingId == null,
                    child: SizedBox(
                      width: viewport.width,
                      height: viewport.height,
                      child: Center(
                        child: _MapCanvas(
                          map: map,
                          size: image,
                          isDm: isDm,
                          transform: _transform,
                          draggingId: _draggingId,
                          dragPosition: _dragPosition,
                          onLongPress: (x, y) => _addPin(x, y),
                          onTapPin: (pin) => _showPin(pin, isDm: isDm),
                          onDragStart: (pin) => setState(() {
                            _draggingId = pin.id;
                            _dragPosition = Offset(pin.x, pin.y);
                          }),
                          onDragUpdate: (delta) => setState(() {
                            _dragPosition = Offset(
                              (_dragPosition.dx + delta.dx / image.width).clamp(0.0, 1.0),
                              (_dragPosition.dy + delta.dy / image.height).clamp(0.0, 1.0),
                            );
                          }),
                          onDragEnd: _endDrag,
                        ),
                      ),
                    ),
                  ),
                  if (isDm)
                    Positioned(
                      right: 16,
                      bottom: 16,
                      child: OfflineAwareFab(
                        fabKey: const Key('map-add-pin'),
                        onPressed: () {
                          final center = _transform.toScene(
                            Offset(viewport.width / 2, viewport.height / 2),
                          );
                          final x = (center.dx - (viewport.width - image.width) / 2) / image.width;
                          final y =
                              (center.dy - (viewport.height - image.height) / 2) / image.height;
                          _addPin(x.clamp(0.0, 1.0), y.clamp(0.0, 1.0));
                        },
                        icon: const Icon(Icons.add_location_alt_outlined),
                        label: const Text('Añadir pin'),
                      ),
                    ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }

  /// Largest size with [aspect] that fits in [viewport].
  static Size _fit(Size viewport, double aspect) {
    if (viewport.width <= 0 || viewport.height <= 0) return Size.zero;
    final width = math.min(viewport.width, viewport.height * aspect);
    return Size(width, width / aspect);
  }
}

class _MapCanvas extends StatelessWidget {
  const _MapCanvas({
    required this.map,
    required this.size,
    required this.isDm,
    required this.transform,
    required this.draggingId,
    required this.dragPosition,
    required this.onLongPress,
    required this.onTapPin,
    required this.onDragStart,
    required this.onDragUpdate,
    required this.onDragEnd,
  });

  final MapDetail map;
  final Size size;
  final bool isDm;
  final TransformationController transform;
  final String? draggingId;
  final Offset dragPosition;
  final void Function(double x, double y) onLongPress;
  final ValueChanged<MapPin> onTapPin;
  final ValueChanged<MapPin> onDragStart;
  final ValueChanged<Offset> onDragUpdate;
  final ValueChanged<MapPin> onDragEnd;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onLongPressStart: isDm
          ? (details) => onLongPress(
              (details.localPosition.dx / size.width).clamp(0.0, 1.0),
              (details.localPosition.dy / size.height).clamp(0.0, 1.0),
            )
          : null,
      child: SizedBox(
        key: const Key('map-canvas'),
        width: size.width,
        height: size.height,
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            Positioned.fill(
              child: AuthenticatedImage(
                key: const Key('map-image'),
                url: map.url,
                fit: BoxFit.fill,
              ),
            ),
            // Pins keep their on-screen size when zooming, so the layer is
            // rebuilt with the scale of the viewer.
            ListenableBuilder(
              listenable: transform,
              builder: (context, _) {
                final scale = transform.value.getMaxScaleOnAxis();
                final box = _pinSize / scale;
                return Stack(
                  clipBehavior: Clip.none,
                  children: [
                    for (final pin in map.pins)
                      Positioned(
                        key: Key('map-pin-${pin.id}'),
                        left: _x(pin) * size.width - box / 2,
                        top: _y(pin) * size.height - box,
                        width: box,
                        height: box,
                        child: GestureDetector(
                          behavior: HitTestBehavior.opaque,
                          onTap: () => onTapPin(pin),
                          onPanStart: isDm ? (_) => onDragStart(pin) : null,
                          onPanUpdate: isDm ? (details) => onDragUpdate(details.delta) : null,
                          onPanEnd: isDm ? (_) => onDragEnd(pin) : null,
                          onPanCancel: isDm && draggingId == pin.id ? () => onDragEnd(pin) : null,
                          child: FittedBox(
                            child: SizedBox(
                              width: _pinSize,
                              height: _pinSize,
                              child: _PinGlyph(
                                pin: pin,
                                showHidden: isDm && pin.visibility.isDmOnly,
                                lifted: draggingId == pin.id,
                              ),
                            ),
                          ),
                        ),
                      ),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  double _x(MapPin pin) => draggingId == pin.id ? dragPosition.dx : pin.x;

  double _y(MapPin pin) => draggingId == pin.id ? dragPosition.dy : pin.y;
}

class _PinGlyph extends StatelessWidget {
  const _PinGlyph({required this.pin, required this.showHidden, required this.lifted});

  final MapPin pin;
  final bool showHidden;
  final bool lifted;

  @override
  Widget build(BuildContext context) {
    final color = parsePinColor(pin.color) ?? Theme.of(context).colorScheme.primary;
    return Tooltip(
      message: pin.title,
      child: Stack(
        alignment: Alignment.topCenter,
        children: [
          Icon(
            pin.icon.icon,
            size: lifted ? 40 : 36,
            color: color,
            shadows: const [Shadow(blurRadius: 4, color: Colors.black54)],
          ),
          if (showHidden)
            const Positioned(
              right: 2,
              top: 0,
              child: Icon(
                Icons.visibility_off,
                size: 14,
                color: Colors.white,
                shadows: [Shadow(blurRadius: 3, color: Colors.black)],
              ),
            ),
        ],
      ),
    );
  }
}
