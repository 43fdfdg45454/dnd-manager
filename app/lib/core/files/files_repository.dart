import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../network/api_client.dart';
import 'stored_file.dart';

/// Upload and download of stored files (`/api/v1/files`). Every request goes
/// through the authenticated client, so the bearer token is refreshed and
/// pinned certificates are honoured.
class FilesRepository {
  FilesRepository(this._client);

  final ApiClient _client;

  static const _files = '/api/v1/files';

  /// Uploads the local file at [filePath] (multipart). [campaignId] is required
  /// for map images, portraits and lore attachments; [characterId] for
  /// portraits.
  Future<StoredFile> upload({
    required String filePath,
    required String fileName,
    required FileKind kind,
    String? campaignId,
    String? characterId,
    String? contentType,
    void Function(int sent, int total)? onProgress,
    CancelToken? cancelToken,
  }) async {
    // A multipart body can be sent only once, so it is rebuilt per attempt.
    Future<Response<Map<String, dynamic>>> send() async => _client.dio.post<Map<String, dynamic>>(
      _files,
      data: FormData.fromMap({
        'file': await MultipartFile.fromFile(
          filePath,
          filename: fileName,
          contentType: DioMediaType.parse(contentType ?? contentTypeForFileName(fileName)),
        ),
        'kind': kind.apiValue,
        'campaignId': ?campaignId,
        'characterId': ?characterId,
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
    return StoredFile.fromJson(response.data!);
  }

  /// Downloads [url] (relative to the server) into [target], reporting the
  /// fraction received. The file is written next to [target] and renamed when
  /// complete, so a cancelled or failed download leaves nothing behind.
  Future<void> download(
    String url,
    File target, {
    void Function(double progress)? onProgress,
    CancelToken? cancelToken,
  }) async {
    await target.parent.create(recursive: true);
    final partial = File('${target.path}.part');
    try {
      await _client.dio.download(
        url,
        partial.path,
        cancelToken: cancelToken,
        options: Options(headers: const {'Accept': '*/*'}),
        onReceiveProgress: (received, total) {
          if (total > 0) onProgress?.call(received / total);
        },
      );
      await partial.rename(target.path);
    } catch (_) {
      if (partial.existsSync()) await partial.delete();
      rethrow;
    }
  }

  /// The content of [url] (relative to the server) in memory.
  Future<Uint8List> fetchBytes(String url) async {
    final response = await _client.dio.get<List<int>>(
      url,
      options: Options(responseType: ResponseType.bytes, headers: const {'Accept': '*/*'}),
    );
    final data = response.data!;
    return data is Uint8List ? data : Uint8List.fromList(data);
  }
}

final filesRepositoryProvider = Provider<FilesRepository>(
  (ref) => FilesRepository(ref.watch(apiClientProvider)),
);
