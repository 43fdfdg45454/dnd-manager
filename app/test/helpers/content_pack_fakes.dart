import 'package:dio/dio.dart';
import 'package:opentrpg_core/features/admin/data/content_packs_repository.dart';
import 'package:opentrpg_core/features/admin/domain/content_pack.dart';
import 'package:opentrpg_core/features/content_packs/data/campaign_content_packs_repository.dart';
import 'package:opentrpg_core/features/content_packs/domain/campaign_content_pack.dart';

ContentPack makeContentPack({
  String id = 'reinos-ejemplo',
  String name = 'Reinos de Ejemplo',
  String version = '1.0.0',
  Map<String, int> counts = const {'subclasses': 1, 'items': 2, 'spells': 1, 'races': 0},
  String systemId = 'dnd5e',
  int formatVersion = 3,
  bool isBase = false,
  List<String> requires = const [],
}) => ContentPack(
  id: id,
  name: name,
  version: version,
  systemId: systemId,
  formatVersion: formatVersion,
  isBase: isBase,
  requires: requires,
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

/// The base pack of D&D 5e as `GET /campaigns/{id}/content-packs` lists it.
const srdCampaignPack = CampaignContentPack(
  id: 'srd',
  name: 'SRD 5.1',
  version: '5.1',
  isBase: true,
  enabled: true,
);

/// A 400 of `PUT /campaigns/{id}/content-packs` with its [code].
DioException campaignPacksInvalid(String code, String detail) {
  final options = RequestOptions(path: '/api/v1/campaigns/c1/content-packs');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response<dynamic>(
      requestOptions: options,
      statusCode: 400,
      data: {'title': 'La solicitud no es válida.', 'detail': detail, 'code': code},
    ),
  );
}

/// In-memory `/campaigns/{id}/content-packs`: [packs] are the packs of the
/// system (the base one included) and [enabled] the ones each campaign
/// enables. Pass the same map to `FakeCatalogRepository.enabledPacks` so the
/// catalog of a campaign follows what is saved here, like the server.
class FakeCampaignContentPacksRepository implements CampaignContentPacksRepository {
  FakeCampaignContentPacksRepository({
    this.packs = const [srdCampaignPack],
    Map<String, Set<String>>? enabled,
  }) : enabled = enabled ?? {};

  final List<CampaignContentPack> packs;
  final Map<String, Set<String>> enabled;

  /// Every `PUT`, in order.
  final List<({String campaignId, Set<String> packIds})> saved = [];
  int lists = 0;

  /// Thrown by [setEnabled] when set.
  Object? saveError;

  List<CampaignContentPack> _of(String campaignId) {
    final on = enabled[campaignId] ?? const <String>{};
    return [
      for (final p in packs)
        CampaignContentPack(
          id: p.id,
          name: p.name,
          version: p.version,
          isBase: p.isBase,
          enabled: p.isBase || on.contains(p.id),
          requires: p.requires,
        ),
    ];
  }

  @override
  Future<List<CampaignContentPack>> list(String campaignId) async {
    lists++;
    return _of(campaignId);
  }

  @override
  Future<List<CampaignContentPack>> setEnabled(String campaignId, Iterable<String> packIds) async {
    final ids = packIds.toSet();
    saved.add((campaignId: campaignId, packIds: ids));
    if (saveError != null) throw saveError!;
    final base = {
      for (final p in packs)
        if (p.isBase) p.id,
    };
    enabled[campaignId] = ids.difference(base);
    return _of(campaignId);
  }
}
