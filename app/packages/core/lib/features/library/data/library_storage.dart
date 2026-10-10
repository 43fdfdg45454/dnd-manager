import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:path_provider/path_provider.dart';

/// Where downloaded PDFs live on the device: `library/<id>.pdf` inside the
/// app's documents directory (private to the app, kept until deleted).
class LibraryStorage {
  LibraryStorage({Future<Directory> Function()? root}) : _root = root ?? _defaultRoot;

  final Future<Directory> Function() _root;

  static Future<Directory> _defaultRoot() async {
    final documents = await getApplicationDocumentsDirectory();
    return Directory('${documents.path}/library');
  }

  Future<File> fileFor(String documentId) async => File('${(await _root()).path}/$documentId.pdf');

  Future<bool> exists(String documentId) async => (await fileFor(documentId)).exists();

  /// Ids of the documents with a complete download.
  Future<Set<String>> downloadedIds() async {
    final root = await _root();
    if (!await root.exists()) return {};
    return {
      await for (final entity in root.list())
        if (entity is File && entity.path.endsWith('.pdf'))
          entity.uri.pathSegments.last.replaceFirst(RegExp(r'\.pdf$'), ''),
    };
  }

  Future<void> delete(String documentId) async {
    final file = await fileFor(documentId);
    if (await file.exists()) await file.delete();
  }
}

final libraryStorageProvider = Provider<LibraryStorage>((ref) => LibraryStorage());
