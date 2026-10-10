import 'package:dio/dio.dart';
import 'package:opentrpg_core/features/admin/data/content_packs_repository.dart';
import 'package:opentrpg_core/features/admin/domain/content_pack.dart';

ContentPack makeContentPack({
  String id = 'reinos-ejemplo',
  String name = 'Reinos de Ejemplo',
  String version = '1.0.0',
  Map<String, int> counts = const {'subclasses': 1, 'items': 2, 'spells': 1, 'races': 0},
}) => ContentPack(
  id: id,
  name: name,
  version: version,
  importedAt: DateTime.utc(2026, 3, 14, 12),
  counts: counts,
);

/// A 400 like the server's: ProblemDetails with `errors` as a list of
/// "path: message" strings.
DioException contentPackInvalid(List<String> errors, {String? detail}) {
  final options = RequestOptions(path: '/api/v1/admin/content-packs');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response<dynamic>(
      requestOptions: options,
      statusCode: 400,
      data: {
        'title': 'El paquete de contenido no es válido.',
        'detail': detail ?? 'El paquete de contenido tiene ${errors.length} errores.',
        'errors': errors,
      },
    ),
  );
}

/// In-memory content packs: an import adds (or replaces) the pack named after
/// the file and records the call.
class FakeContentPacksRepository implements ContentPacksRepository {
  FakeContentPacksRepository(List<ContentPack> packs) : packs = [...packs];

  final List<ContentPack> packs;
  final List<({String path, String name})> imported = [];
  final List<String> deleted = [];

  /// Thrown by [import] when set.
  Object? importError;

  /// Thrown by [delete] when set.
  Object? deleteError;

  @override
  Future<List<ContentPack>> list() async => [...packs];

  @override
  Future<ContentPackImportResult> import({
    required String filePath,
    required String fileName,
    void Function(int sent, int total)? onProgress,
    CancelToken? cancelToken,
  }) async {
    if (importError != null) throw importError!;
    imported.add((path: filePath, name: fileName));
    onProgress?.call(10, 10);
    final id = fileName.replaceAll('.json', '');
    final pack = makeContentPack(id: id, name: 'Paquete $id', counts: const {'items': 3});
    packs
      ..removeWhere((p) => p.id == id)
      ..add(pack);
    return ContentPackImportResult(
      id: pack.id,
      name: pack.name,
      version: pack.version,
      counts: pack.counts,
    );
  }

  @override
  Future<void> delete(String id) async {
    if (deleteError != null) throw deleteError!;
    deleted.add(id);
    packs.removeWhere((p) => p.id == id);
  }
}

/// A picker that always returns [file] (null simulates cancelling).
Future<PickedPackFile?> Function() fakePackPicker(PickedPackFile? file) =>
    () async => file;
