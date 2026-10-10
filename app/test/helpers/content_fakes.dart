import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:image_picker/image_picker.dart';
import 'package:opentrpg_core/core/content/content_visibility.dart';
import 'package:opentrpg_core/core/files/file_disk_cache.dart';
import 'package:opentrpg_core/core/files/files_repository.dart';
import 'package:opentrpg_core/core/files/stored_file.dart';
import 'package:opentrpg_core/features/library/data/library_repository.dart';
import 'package:opentrpg_core/features/library/data/library_storage.dart';
import 'package:opentrpg_core/features/library/data/models.dart';
import 'package:opentrpg_core/features/lore/data/lore_repository.dart';
import 'package:opentrpg_core/features/lore/data/models.dart';
import 'package:opentrpg_core/features/maps/data/maps_repository.dart';
import 'package:opentrpg_core/features/maps/data/models.dart';

import 'fakes.dart';

/// A valid 1x1 PNG.
final Uint8List tinyPng = Uint8List.fromList(const [
  0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, //
  0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
  0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xFF, 0xFF, 0x3F,
  0x00, 0x05, 0xFE, 0x02, 0xFE, 0xA7, 0x35, 0x81, 0x84, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
  0x44, 0xAE, 0x42, 0x60, 0x82,
]);

/// Serves [tinyPng] for every file URL, so no test touches the network.
Override fakeImageLoaderOverride() =>
    fileBytesLoaderProvider.overrideWithValue((url) async => tinyPng);

LoreSummary makeLore({
  String id = 'l1',
  String title = 'Valle del Norte',
  String? slug,
  LoreCategory category = LoreCategory.region,
  ContentVisibility visibility = ContentVisibility.players,
  String? parentId,
  String? coverFileId,
}) => LoreSummary(
  id: id,
  campaignId: 'c1',
  title: title,
  slug: slug ?? title.toLowerCase().replaceAll(' ', '-'),
  category: category,
  visibility: visibility,
  parentId: parentId,
  coverFileId: coverFileId,
);

/// In-memory lore. Like the server, players only get the `Players` entries and
/// a 404 for a hidden one.
class FakeLoreRepository implements LoreRepository {
  FakeLoreRepository({
    List<LoreSummary> entries = const [],
    Map<String, String> content = const {},
    this.isDm = false,
  }) : entries = [...entries],
       content = {...content};

  final List<LoreSummary> entries;
  final Map<String, String> content;
  final bool isDm;
  Object? error;
  final List<LoreDraft> created = [];
  final List<String> deleted = [];

  @override
  Future<List<LoreSummary>> list(String campaignId) async {
    if (error != null) throw error!;
    return [
      for (final e in entries)
        if (isDm || e.visibility == ContentVisibility.players) e,
    ];
  }

  LoreEntry _entry(String id) {
    final e = entries.firstWhere((e) => e.id == id, orElse: () => throw dioError(404));
    if (!isDm && e.visibility.isDmOnly) throw dioError(404);
    return LoreEntry(
      id: e.id,
      campaignId: e.campaignId,
      title: e.title,
      slug: e.slug,
      category: e.category,
      visibility: e.visibility,
      parentId: e.parentId,
      coverFileId: e.coverFileId,
      contentMarkdown: content[id] ?? '',
    );
  }

  @override
  Future<LoreEntry> get(String id) async {
    if (error != null) throw error!;
    return _entry(id);
  }

  @override
  Future<LoreEntry> create(String campaignId, LoreDraft draft) async {
    created.add(draft);
    final id = 'new${created.length}';
    entries.add(
      LoreSummary(
        id: id,
        campaignId: campaignId,
        title: draft.title,
        slug: draft.title.toLowerCase().replaceAll(' ', '-'),
        category: draft.category,
        visibility: draft.visibility,
        parentId: draft.parentId,
      ),
    );
    content[id] = draft.contentMarkdown;
    return _entry(id);
  }

  @override
  Future<LoreEntry> update(String id, LoreDraft draft) => throw UnimplementedError();

  @override
  Future<void> delete(String id) async {
    deleted.add(id);
    entries.removeWhere((e) => e.id == id);
  }

  @override
  Future<LoreAttachment> addAttachment(String id, {required String fileId, String? caption}) =>
      throw UnimplementedError();

  @override
  Future<void> removeAttachment(String id, String attachmentId) => throw UnimplementedError();
}

MapPin makePin({
  String id = 'p1',
  double x = 0.25,
  double y = 0.5,
  String title = 'Fortaleza',
  String? note,
  PinIcon icon = PinIcon.dungeon,
  ContentVisibility visibility = ContentVisibility.players,
  String? loreEntryId,
}) => MapPin(
  id: id,
  mapId: 'm1',
  x: x,
  y: y,
  title: title,
  note: note,
  icon: icon,
  visibility: visibility,
  loreEntryId: loreEntryId,
);

MapDetail makeMap({
  String id = 'm1',
  String name = 'Costa de la Espada',
  ContentVisibility visibility = ContentVisibility.players,
  List<MapPin> pins = const [],
}) => MapDetail(
  id: id,
  campaignId: 'c1',
  name: name,
  fileId: 'f-$id',
  url: '/api/v1/files/f-$id',
  widthPx: 2000,
  heightPx: 1000,
  visibility: visibility,
  pins: pins,
);

/// In-memory maps; players never receive the `DmOnly` maps or pins.
class FakeMapsRepository implements MapsRepository {
  FakeMapsRepository({List<MapDetail> maps = const [], this.isDm = false}) : maps = [...maps];

  final List<MapDetail> maps;
  final bool isDm;
  final List<({double x, double y, PinDraft draft})> createdPins = [];
  final List<({String pinId, double x, double y})> movedPins = [];
  final List<String> deletedPins = [];
  final List<({String name, String fileId, String visibility})> created = [];

  MapDetail _visible(MapDetail m) => m.withPins([
    for (final p in m.pins)
      if (isDm || p.visibility == ContentVisibility.players) p,
  ]);

  @override
  Future<List<MapSummary>> list(String campaignId) async => [
    for (final m in maps)
      if (isDm || m.visibility == ContentVisibility.players) m,
  ];

  @override
  Future<MapDetail> get(String id) async {
    final map = maps.firstWhere((m) => m.id == id, orElse: () => throw dioError(404));
    if (!isDm && map.visibility.isDmOnly) throw dioError(404);
    return _visible(map);
  }

  @override
  Future<MapPin> createPin(
    String mapId, {
    required double x,
    required double y,
    required PinDraft draft,
  }) async {
    createdPins.add((x: x, y: y, draft: draft));
    final pin = makePin(
      id: 'new${createdPins.length}',
      x: x,
      y: y,
      title: draft.title,
      note: draft.note,
      icon: draft.icon,
      visibility: draft.visibility,
    );
    final index = maps.indexWhere((m) => m.id == mapId);
    maps[index] = maps[index].withPins([...maps[index].pins, pin]);
    return pin;
  }

  @override
  Future<MapPin> movePin(String mapId, String pinId, {required double x, required double y}) async {
    movedPins.add((pinId: pinId, x: x, y: y));
    return makePin(id: pinId, x: x, y: y);
  }

  @override
  Future<void> deletePin(String mapId, String pinId) async => deletedPins.add(pinId);

  @override
  Future<MapPin> updatePin(String mapId, String pinId, PinDraft draft) =>
      throw UnimplementedError();

  @override
  Future<MapDetail> create(
    String campaignId, {
    required String name,
    required String fileId,
    required ContentVisibility visibility,
  }) async {
    created.add((name: name, fileId: fileId, visibility: visibility.apiValue));
    final map = makeMap(id: 'new${created.length}', name: name, visibility: visibility);
    maps.add(map);
    return map;
  }

  @override
  Future<MapDetail> update(String id, {String? name, ContentVisibility? visibility}) =>
      throw UnimplementedError();

  @override
  Future<void> delete(String id) => throw UnimplementedError();
}

LibraryDocument makeDocument({
  String id = 'd1',
  String title = 'Reglas básicas',
  LibraryCategory category = LibraryCategory.rules,
  String? description,
  bool isSystem = false,
  int sizeBytes = 2 * 1024 * 1024,
  int? pageCount = 120,
  String? note,
}) => LibraryDocument(
  id: id,
  title: title,
  category: category,
  fileId: 'f-$id',
  url: '/api/v1/files/f-$id',
  description: description,
  fileName: '$id.pdf',
  sizeBytes: sizeBytes,
  pageCount: pageCount,
  isSystem: isSystem,
  note: note,
);

class FakeLibraryRepository implements LibraryRepository {
  FakeLibraryRepository({
    List<LibraryDocument> documents = const [],
    List<String> recommended = const [],
  }) : documents = [...documents],
       recommended = {...recommended};

  final List<LibraryDocument> documents;
  final Set<String> recommended;
  Object? error;
  final List<String> deleted = [];

  @override
  Future<List<LibraryDocument>> list() async {
    if (error != null) throw error!;
    return [...documents];
  }

  @override
  Future<List<LibraryDocument>> campaignDocuments(String campaignId) async => [
    for (final d in documents)
      if (recommended.contains(d.id)) d,
  ];

  @override
  Future<void> recommend(String campaignId, String documentId, {String? note}) async =>
      recommended.add(documentId);

  @override
  Future<void> unrecommend(String campaignId, String documentId) async =>
      recommended.remove(documentId);

  @override
  Future<void> delete(String id) async {
    deleted.add(id);
    documents.removeWhere((d) => d.id == id);
  }

  @override
  Future<LibraryDocument> create({
    required String title,
    String? description,
    required LibraryCategory category,
    required String fileId,
  }) => throw UnimplementedError();
}

/// Library storage that only remembers which documents were "downloaded".
class FakeLibraryStorage implements LibraryStorage {
  FakeLibraryStorage([Set<String> downloaded = const {}]) : downloaded = {...downloaded};

  final Set<String> downloaded;

  @override
  Future<File> fileFor(String documentId) async => File('/fake/library/$documentId.pdf');

  @override
  Future<bool> exists(String documentId) async => downloaded.contains(documentId);

  @override
  Future<Set<String>> downloadedIds() async => {...downloaded};

  @override
  Future<void> delete(String documentId) async => downloaded.remove(documentId);
}

/// Files backend that never touches the network or the disk.
class FakeFilesRepository implements FilesRepository {
  FakeFilesRepository({this.storage});

  /// Marked as downloaded when a download completes.
  final FakeLibraryStorage? storage;
  final List<({String fileName, FileKind kind, String? campaignId, String? characterId})> uploads =
      [];
  final List<String> downloads = [];
  Object? uploadError;

  @override
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
    if (uploadError != null) throw uploadError!;
    uploads.add((fileName: fileName, kind: kind, campaignId: campaignId, characterId: characterId));
    onProgress?.call(1, 1);
    final id = 'up${uploads.length}';
    return StoredFile(
      id: id,
      fileName: fileName,
      contentType: contentType ?? 'image/png',
      sizeBytes: 10,
      url: filePath.isEmpty ? '' : '/api/v1/files/$id',
    );
  }

  @override
  Future<void> download(
    String url,
    File target, {
    void Function(double progress)? onProgress,
    CancelToken? cancelToken,
  }) async {
    downloads.add(url);
    onProgress?.call(0.5);
    storage?.downloaded.add(target.uri.pathSegments.last.replaceFirst('.pdf', ''));
  }

  @override
  Future<Uint8List> fetchBytes(String url) async => tinyPng;
}

/// Gallery picker that "picks" a fixed image.
class FakeImagePicker implements ImagePicker {
  @override
  Future<XFile?> pickImage({
    required ImageSource source,
    double? maxWidth,
    double? maxHeight,
    int? imageQuality,
    CameraDevice preferredCameraDevice = CameraDevice.rear,
    bool requestFullMetadata = true,
  }) async => XFile('/fake/retrato.png', name: 'retrato.png');

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
