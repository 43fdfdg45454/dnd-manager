import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/stale_data.dart';
import '../../../core/files/files_repository.dart';
import '../../../core/network/api_error.dart';
import '../../../core/server/app_session_epoch.dart';
import '../../../core/storage/local_preferences.dart';
import 'library_repository.dart';
import 'library_storage.dart';
import 'models.dart';

/// Errors are shown with a retry button instead of being retried silently.
Duration? _noRetry(int retryCount, Object error) => null;

/// The library list and whether it comes from the device (no connection).
class LibraryListState {
  const LibraryListState(this.documents, {this.offline = false});

  final List<LibraryDocument> documents;

  /// True when the server was unreachable and [documents] is the list saved
  /// by the last successful load (only the downloaded ones can be opened).
  final bool offline;
}

const _cacheKey = 'library.cache';

/// The instance library. The last list is kept in the preferences so the
/// downloaded documents stay reachable without a connection.
class LibraryController extends AsyncNotifier<LibraryListState> {
  LibraryRepository get _repository => ref.read(libraryRepositoryProvider);

  @override
  Future<LibraryListState> build() async {
    ref.watch(appSessionEpochProvider);
    return _load();
  }

  Future<LibraryListState> _load() async {
    final prefs = ref.read(localPreferencesProvider);
    try {
      final documents = await _repository.list();
      // The response cache answered for an unreachable server.
      final stale = ref.read(staleSinceProvider(staleExact(LibraryRepository.libraryPath))) != null;
      if (stale) return LibraryListState(documents, offline: true);
      await prefs?.setString(_cacheKey, jsonEncode([for (final d in documents) d.toJson()]));
      return LibraryListState(documents);
    } catch (error) {
      final cached = prefs?.getString(_cacheKey);
      if (isNetworkFailure(error) && cached != null) {
        try {
          return LibraryListState([
            for (final e in jsonDecode(cached) as List)
              LibraryDocument.fromJson(Map<String, dynamic>.from(e as Map)),
          ], offline: true);
        } catch (_) {
          // A damaged cache is as good as none.
        }
      }
      rethrow;
    }
  }

  Future<void> reload() async {
    state = await AsyncValue.guard(_load);
  }

  /// Publishes an uploaded PDF and refreshes the list.
  Future<void> create({
    required String title,
    String? description,
    required LibraryCategory category,
    required String fileId,
  }) async {
    await _repository.create(
      title: title,
      description: description,
      category: category,
      fileId: fileId,
    );
    await reload();
  }

  /// Deletes a document of the instance (and its download on this device).
  Future<void> delete(String id) async {
    await _repository.delete(id);
    await ref.read(libraryDownloadsProvider.notifier).remove(id);
    await reload();
  }
}

final libraryControllerProvider = AsyncNotifierProvider<LibraryController, LibraryListState>(
  LibraryController.new,
  retry: _noRetry,
);

/// Documents recommended in one campaign.
class CampaignLibraryController extends AsyncNotifier<List<LibraryDocument>> {
  CampaignLibraryController(this.campaignId);

  final String campaignId;

  LibraryRepository get _repository => ref.read(libraryRepositoryProvider);

  @override
  Future<List<LibraryDocument>> build() => _repository.campaignDocuments(campaignId);

  Future<void> setRecommended(String documentId, bool recommended) async {
    if (recommended) {
      await _repository.recommend(campaignId, documentId);
    } else {
      await _repository.unrecommend(campaignId, documentId);
    }
    state = AsyncData(await _repository.campaignDocuments(campaignId));
  }
}

final campaignLibraryControllerProvider = AsyncNotifierProvider.autoDispose
    .family<CampaignLibraryController, List<LibraryDocument>, String>(
      CampaignLibraryController.new,
      retry: _noRetry,
    );

/// Download state of the documents on this device, by document id. It lives
/// as long as the app, so downloads go on while the user leaves the screen.
class LibraryDownloadsController extends Notifier<Map<String, DocumentDownload>> {
  final _cancelTokens = <String, CancelToken>{};

  LibraryStorage get _storage => ref.read(libraryStorageProvider);

  @override
  Map<String, DocumentDownload> build() {
    Future.microtask(_scan);
    return const {};
  }

  Future<void> _scan() async {
    final ids = await _storage.downloadedIds();
    if (!ref.mounted) return;
    state = {
      for (final id in ids) id: DocumentDownload.available,
      // A download started before the scan finished keeps its progress.
      for (final e in state.entries)
        if (e.value.status == DownloadStatus.downloading) e.key: e.value,
    };
  }

  /// Downloads the PDF of [document]; errors are rethrown for the UI.
  Future<void> download(LibraryDocument document) async {
    if (state[document.id]?.status == DownloadStatus.downloading) return;
    final token = CancelToken();
    _cancelTokens[document.id] = token;
    state = {...state, document.id: const DocumentDownload(DownloadStatus.downloading, 0)};
    try {
      await ref
          .read(filesRepositoryProvider)
          .download(
            document.url,
            await _storage.fileFor(document.id),
            cancelToken: token,
            onProgress: (progress) {
              if (!ref.mounted) return;
              state = {
                ...state,
                document.id: DocumentDownload(DownloadStatus.downloading, progress),
              };
            },
          );
      if (ref.mounted) state = {...state, document.id: DocumentDownload.available};
    } catch (error) {
      if (ref.mounted) state = {...state}..remove(document.id);
      if (error is DioException && CancelToken.isCancel(error)) return;
      rethrow;
    } finally {
      _cancelTokens.remove(document.id);
    }
  }

  void cancel(String documentId) => _cancelTokens[documentId]?.cancel();

  /// Deletes the local copy.
  Future<void> remove(String documentId) async {
    await _storage.delete(documentId);
    if (ref.mounted) state = {...state}..remove(documentId);
  }
}

final libraryDownloadsProvider =
    NotifierProvider<LibraryDownloadsController, Map<String, DocumentDownload>>(
      LibraryDownloadsController.new,
    );
