import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:crypto/crypto.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:path_provider/path_provider.dart';

import 'files_repository.dart';

/// Disk cache of downloaded files. Stored files never change once uploaded, so
/// entries do not expire; the OS may clear the temporary directory at any time.
class FileDiskCache {
  FileDiskCache({Future<Directory> Function()? directory})
    : _directory = directory ?? _defaultDirectory;

  final Future<Directory> Function() _directory;

  static Future<Directory> _defaultDirectory() async =>
      Directory('${(await getTemporaryDirectory()).path}/file_cache');

  /// File name for [url]: the id for `/api/v1/files/{id}`, a hash otherwise.
  static String keyFor(String url) {
    final last = Uri.tryParse(url)?.pathSegments.lastOrNull ?? '';
    if (RegExp(r'^[A-Za-z0-9-]{8,64}$').hasMatch(last)) return last;
    return sha256.convert(utf8.encode(url)).toString();
  }

  Future<File> _file(String url) async => File('${(await _directory()).path}/${keyFor(url)}');

  Future<Uint8List?> read(String url) async {
    try {
      final file = await _file(url);
      return await file.exists() ? await file.readAsBytes() : null;
    } on FileSystemException {
      return null;
    }
  }

  Future<void> write(String url, Uint8List bytes) async {
    try {
      final file = await _file(url);
      await file.parent.create(recursive: true);
      await file.writeAsBytes(bytes, flush: true);
    } on FileSystemException {
      // Caching is best effort.
    }
  }
}

final fileDiskCacheProvider = Provider<FileDiskCache>((ref) => FileDiskCache());

/// Loads the bytes of a file URL: from the disk cache or, failing that, from
/// the server (and then cached).
typedef FileBytesLoader = Future<Uint8List> Function(String url);

final fileBytesLoaderProvider = Provider<FileBytesLoader>((ref) {
  final cache = ref.watch(fileDiskCacheProvider);
  final repository = ref.watch(filesRepositoryProvider);
  return (url) async {
    final cached = await cache.read(url);
    if (cached != null && cached.isNotEmpty) return cached;
    final bytes = await repository.fetchBytes(url);
    await cache.write(url, bytes);
    return bytes;
  };
});
