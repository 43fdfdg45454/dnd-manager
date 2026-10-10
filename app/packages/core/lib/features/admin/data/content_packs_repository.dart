import 'package:dio/dio.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../domain/content_pack.dart';

/// Admin endpoints under `/api/v1/admin/content-packs` (require the `Admin` role).
class ContentPacksRepository {
  ContentPacksRepository(this._client);

  final ApiClient _client;

  static const _base = '/api/v1/admin/content-packs';

  Future<List<ContentPack>> list() async {
    final response = await _client.dio.get<List<dynamic>>(_base);
    return [
      for (final json in response.data ?? const [])
        if (json is Map) ContentPack.fromJson(Map<String, dynamic>.from(json)),
    ];
  }

  /// Uploads the pack JSON at [filePath] (multipart field `file`), creating the
  /// pack or replacing the one with the same id. A 400 carries the validation
  /// errors (see `contentPackErrors`).
  Future<ContentPackImportResult> import({
    required String filePath,
    required String fileName,
    void Function(int sent, int total)? onProgress,
    CancelToken? cancelToken,
  }) async {
    // A multipart body can be sent only once, so it is rebuilt per attempt.
    Future<Response<Map<String, dynamic>>> send() async => _client.dio.post<Map<String, dynamic>>(
      _base,
      data: FormData.fromMap({
        'file': await MultipartFile.fromFile(
          filePath,
          filename: fileName,
          contentType: DioMediaType.parse('application/json'),
        ),
      }),
      onSendProgress: onProgress,
      cancelToken: cancelToken,
    );

    Response<Map<String, dynamic>> response;
    try {
      response = await send();
    } on DioException catch (error) {
      // The interceptor refreshes the token on a 401 but cannot replay a
      // consumed multipart body; one more attempt uses the new token.
      final replayFailed = error.error is StateError;
      if (error.response?.statusCode != 401 && !replayFailed) rethrow;
      response = await send();
    }
    return ContentPackImportResult.fromJson(response.data!);
  }

  Future<void> delete(String id) async {
    await _client.dio.delete<void>('$_base/${Uri.encodeComponent(id)}');
  }
}

final contentPacksRepositoryProvider = Provider<ContentPacksRepository>(
  (ref) => ContentPacksRepository(ref.watch(apiClientProvider)),
);

/// The file the administrator picked to import.
typedef PickedPackFile = ({String path, String name});

/// Opens the system file picker restricted to JSON; a provider so tests can
/// replace it. Resolves to null when the user cancels.
final contentPackPickerProvider = Provider<Future<PickedPackFile?> Function()>(
  (ref) => () async {
    final picked = await FilePicker.pickFile(
      type: FileType.custom,
      allowedExtensions: const ['json'],
    );
    final path = picked?.path;
    if (picked == null || path == null) return null;
    return (path: path, name: picked.name);
  },
);
