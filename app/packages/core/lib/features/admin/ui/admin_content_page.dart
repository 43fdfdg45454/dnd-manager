import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/network/api_error.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../content_packs/ui/base_pack_chip.dart';
import '../data/content_packs_controller.dart';
import '../data/content_packs_repository.dart';
import '../domain/content_pack.dart';

const _importErrors = <int, String>{
  403: 'Solo un administrador puede gestionar los paquetes de contenido.',
  413: 'El paquete supera el tamaño máximo permitido (20 MB).',
};

/// Administration of the private content packs of the instance: list, import
/// (JSON file) and delete.
class AdminContentPage extends ConsumerStatefulWidget {
  const AdminContentPage({super.key});

  @override
  ConsumerState<AdminContentPage> createState() => _AdminContentPageState();
}

class _AdminContentPageState extends ConsumerState<AdminContentPage> {
  ContentPacksController get _controller => ref.read(contentPacksControllerProvider.notifier);

  void _showMessage(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _import() async {
    final picked = await ref.read(contentPackPickerProvider)();
    if (picked == null || !mounted) return;

    final progress = ValueNotifier<double?>(null);
    final navigator = Navigator.of(context, rootNavigator: true);
    var dialogOpen = true;
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (_) => PopScope(
        canPop: false,
        child: AlertDialog(
          key: const Key('content-import-progress'),
          title: const Text('Importando paquete'),
          content: ValueListenableBuilder<double?>(
            valueListenable: progress,
            builder: (_, value, _) => LinearProgressIndicator(value: value),
          ),
        ),
      ),
    ).whenComplete(() => dialogOpen = false);

    ContentPackImportResult? result;
    Object? failure;
    try {
      result = await _controller.import(
        filePath: picked.path,
        fileName: picked.name,
        onProgress: (sent, total) => progress.value = total > 0 ? sent / total : null,
      );
    } catch (error) {
      failure = error;
    } finally {
      if (dialogOpen) navigator.pop();
      progress.dispose();
    }
    if (!mounted) return;

    if (result != null) {
      final summary = result.countsSummary;
      _showMessage('Paquete "${result.name}" importado${summary.isEmpty ? '' : ': $summary'}.');
      return;
    }
    final errors = contentPackErrors(failure!);
    if (errors.isNotEmpty) {
      await showDialog<void>(
        context: context,
        builder: (_) => _ImportErrorsDialog(
          errors: errors,
          detail: errors.length > 1 ? problemDetail(failure!) : null,
        ),
      );
    } else {
      _showMessage(describeContentError(failure, byStatus: _importErrors));
    }
  }

  Future<void> _delete(ContentPack pack) async {
    final confirmed = await confirmAction(
      context,
      title: 'Eliminar paquete',
      message:
          '¿Seguro que quieres eliminar "${pack.name}"? Su contenido dejará de estar disponible; '
          'los personajes que lo usan lo marcarán como no disponible.',
      confirmLabel: 'Eliminar',
    );
    if (!confirmed || !mounted) return;
    try {
      await _controller.delete(pack.id);
      if (mounted) _showMessage('Paquete eliminado.');
    } catch (error) {
      if (mounted) _showMessage(_deleteError(error));
    }
  }

  /// A 409 says why the pack cannot go: it is the base pack (`base-pack`) or
  /// other packs require it (`required-by`, with their names in the detail).
  static String _deleteError(Object error) {
    if (error is DioException && error.response?.statusCode == 409) {
      return switch (problemCode(error)) {
        'base-pack' => 'El paquete base del sistema no se puede borrar.',
        _ => problemDetail(error) ?? 'Otros paquetes lo requieren: bórralos antes.',
      };
    }
    return describeContentError(error, byStatus: const {404: 'El paquete ya no existe.'});
  }

  @override
  Widget build(BuildContext context) {
    final packs = ref.watch(contentPacksControllerProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Contenido')),
      floatingActionButton: OfflineAwareFab(
        fabKey: const Key('content-import'),
        onPressed: _import,
        icon: const Icon(Icons.upload_file),
        label: const Text('Importar paquete'),
      ),
      body: packs.when(
        skipLoadingOnReload: true,
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(describeApiError(error), textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: () => ref.invalidate(contentPacksControllerProvider),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Reintentar'),
                ),
              ],
            ),
          ),
        ),
        data: (list) {
          if (list.isEmpty) {
            return const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text(
                  'No hay paquetes de contenido. El contenido del SRD '
                  'siempre está disponible.',
                  key: Key('content-empty'),
                  textAlign: TextAlign.center,
                ),
              ),
            );
          }
          return ListView(
            padding: const EdgeInsets.only(bottom: 88),
            children: [for (final pack in list) _PackTile(pack: pack, onDelete: _delete)],
          );
        },
      ),
    );
  }
}

class _PackTile extends StatelessWidget {
  const _PackTile({required this.pack, required this.onDelete});

  final ContentPack pack;
  final ValueChanged<ContentPack> onDelete;

  @override
  Widget build(BuildContext context) {
    final imported = pack.importedAt;
    final details = [
      if (pack.systemId.isNotEmpty) 'Sistema ${pack.systemId}',
      'Versión ${pack.version}',
      pack.formatVersion > 0 ? 'Formato ${pack.formatVersion}' : 'Integrado',
      if (imported != null) 'Importado el ${DateFormat('dd/MM/yyyy').format(imported.toLocal())}',
    ].join(' · ');
    final counts = pack.countsSummary;
    final requires = pack.requires.isEmpty ? null : 'Requiere: ${pack.requires.join(', ')}';
    final lines = [details, ?requires, if (counts.isNotEmpty) counts];
    return ListTile(
      key: Key('content-pack-${pack.id}'),
      leading: Icon(pack.isBase ? Icons.verified_outlined : Icons.inventory_2_outlined),
      title: Wrap(
        spacing: 8,
        runSpacing: 2,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text(pack.name),
          if (pack.isBase) BasePackChip(key: Key('content-base-${pack.id}')),
        ],
      ),
      subtitle: Text(lines.join('\n')),
      isThreeLine: lines.length > 1,
      // The base pack of a system is always there: it cannot be deleted.
      trailing: pack.isBase
          ? null
          : IconButton(
              key: Key('content-delete-${pack.id}'),
              icon: const Icon(Icons.delete_outline),
              tooltip: 'Eliminar paquete',
              onPressed: () => onDelete(pack),
            ),
    );
  }
}

class _ImportErrorsDialog extends StatelessWidget {
  const _ImportErrorsDialog({required this.errors, this.detail});

  final List<String> errors;
  final String? detail;

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      key: const Key('content-import-errors'),
      title: const Text('El paquete no es válido'),
      content: SizedBox(
        width: double.maxFinite,
        child: ListView(
          shrinkWrap: true,
          children: [
            if (detail != null)
              Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(detail!)),
            for (final error in errors)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 2),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('• '),
                    Expanded(child: SelectableText(error)),
                  ],
                ),
              ),
          ],
        ),
      ),
      actions: [
        TextButton(
          key: const Key('content-import-errors-close'),
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cerrar'),
        ),
      ],
    );
  }
}
