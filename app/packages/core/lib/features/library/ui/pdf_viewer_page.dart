import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:pdfrx/pdfrx.dart';

import '../../../core/auth/token_storage.dart';
import '../../../core/files/stored_file.dart';
import '../../../core/server/server_config_controller.dart';
import '../../../core/storage/local_preferences.dart';
import '../data/library_controllers.dart';
import '../data/library_storage.dart';
import '../data/models.dart';

/// Reads a library PDF: from the device when it has been downloaded, streamed
/// from the server (with the session token) otherwise. The last page read is
/// remembered per document and restored on the next visit.
class PdfViewerPage extends ConsumerStatefulWidget {
  const PdfViewerPage({super.key, required this.documentId});

  final String documentId;

  @override
  ConsumerState<PdfViewerPage> createState() => _PdfViewerPageState();
}

class _PdfViewerPageState extends ConsumerState<PdfViewerPage> {
  late final Future<_PdfSource> _source = _resolveSource();

  Future<_PdfSource> _resolveSource() async {
    final storage = ref.read(libraryStorageProvider);
    if (await storage.exists(widget.documentId)) {
      return _PdfSource.local((await storage.fileFor(widget.documentId)).path);
    }
    final document = (await ref.read(libraryControllerProvider.future)).documents
        .where((d) => d.id == widget.documentId)
        .firstOrNull;
    if (document == null) throw StateError('Document not found');
    final token = await ref.read(tokenStorageProvider).readAccessToken();
    final baseUrl = ref.read(serverConfigProvider).baseUrl;
    return _PdfSource.remote(Uri.parse(resolveFileUrl(baseUrl, document.url)), {
      if (token != null) 'Authorization': 'Bearer $token',
    });
  }

  int get _initialPage =>
      ref.read(localPreferencesProvider)?.getInt(libraryPageKey(widget.documentId)) ?? 1;

  void _rememberPage(int? page) {
    if (page == null) return;
    ref.read(localPreferencesProvider)?.setInt(libraryPageKey(widget.documentId), page);
  }

  @override
  Widget build(BuildContext context) {
    final document = ref
        .watch(libraryControllerProvider)
        .value
        ?.documents
        .where((d) => d.id == widget.documentId)
        .firstOrNull;
    final available =
        ref.watch(libraryDownloadsProvider)[widget.documentId]?.status == DownloadStatus.available;

    return Scaffold(
      appBar: AppBar(
        title: Text(document?.title ?? 'Documento'),
        actions: [
          if (available)
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 12),
              child: Tooltip(message: 'Disponible sin conexión', child: Icon(Icons.download_done)),
            ),
        ],
      ),
      body: FutureBuilder<_PdfSource>(
        future: _source,
        builder: (context, snapshot) {
          if (snapshot.hasError) {
            return const Center(child: Text('No se pudo abrir el documento.'));
          }
          final source = snapshot.data;
          if (source == null) return const Center(child: CircularProgressIndicator());
          final params = PdfViewerParams(onPageChanged: _rememberPage);
          return source.path != null
              ? PdfViewer.file(source.path!, initialPageNumber: _initialPage, params: params)
              : PdfViewer.uri(
                  source.uri!,
                  headers: source.headers,
                  initialPageNumber: _initialPage,
                  params: params,
                );
        },
      ),
    );
  }
}

class _PdfSource {
  _PdfSource.local(String this.path) : uri = null, headers = null;

  _PdfSource.remote(Uri this.uri, Map<String, String> this.headers) : path = null;

  final String? path;
  final Uri? uri;
  final Map<String, String>? headers;
}
