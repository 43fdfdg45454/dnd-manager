import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/server/app_session_epoch.dart';
import '../../catalog/data/catalog_controllers.dart';
import '../domain/content_pack.dart';
import 'content_packs_repository.dart';

/// The content packs imported into the instance.
class ContentPacksController extends AsyncNotifier<List<ContentPack>> {
  ContentPacksRepository get _repository => ref.read(contentPacksRepositoryProvider);

  @override
  Future<List<ContentPack>> build() {
    // Reload from scratch after a server switch.
    ref.watch(appSessionEpochProvider);
    return _repository.list();
  }

  Future<void> reload() async {
    state = await AsyncValue.guard(_repository.list);
  }

  /// Imports (or replaces) the pack in [filePath] and refreshes the list and
  /// the catalog sources. Errors are rethrown.
  Future<ContentPackImportResult> import({
    required String filePath,
    required String fileName,
    void Function(int sent, int total)? onProgress,
  }) async {
    final result = await _repository.import(
      filePath: filePath,
      fileName: fileName,
      onProgress: onProgress,
    );
    await _refresh();
    return result;
  }

  Future<void> delete(String id) async {
    await _repository.delete(id);
    await _refresh();
  }

  Future<void> _refresh() async {
    ref
      ..invalidate(catalogSourcesProvider)
      ..invalidate(classesProvider)
      ..invalidate(racesProvider)
      ..invalidate(backgroundsProvider);
    await reload();
  }
}

final contentPacksControllerProvider =
    AsyncNotifierProvider.autoDispose<ContentPacksController, List<ContentPack>>(
      ContentPacksController.new,
      retry: (retryCount, error) => null,
    );

/// The validation errors of a rejected import: the ProblemDetails `errors` as a
/// list of "path: message" strings, or (for other 400 responses) as the usual
/// field map. Empty when [error] carries none.
List<String> contentPackErrors(Object error) {
  if (error is! DioException || error.response?.statusCode != 400) return const [];
  final data = error.response?.data;
  if (data is! Map) return const [];
  final errors = data['errors'];
  if (errors is List) {
    return [
      for (final e in errors)
        if (e != null && e.toString().trim().isNotEmpty) e.toString().trim(),
    ];
  }
  if (errors is Map) {
    return [
      for (final entry in errors.entries)
        if (entry.value is List)
          for (final message in entry.value as List) '${entry.key}: $message'
        else if (entry.value != null)
          '${entry.key}: ${entry.value}',
    ];
  }
  return const [];
}
