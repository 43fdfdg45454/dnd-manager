import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../files/file_disk_cache.dart';
import 'response_cache.dart';
import 'stale_data.dart';

/// Empties the data kept for offline use: the cached API answers and the
/// downloaded images. Used by "Vaciar caché", on logout and on a server
/// switch. Downloaded library PDFs are kept: the user removes them explicitly.
class CacheMaintenance {
  CacheMaintenance(this._ref);

  final Ref _ref;

  Future<void> clearAll() async {
    try {
      await _ref.read(responseCacheProvider).clear();
    } catch (_) {
      // Best effort: a cache that cannot be cleared is overwritten later.
    }
    await _ref.read(fileDiskCacheProvider).clear();
    if (_ref.mounted) _ref.read(staleDataProvider.notifier).clear();
  }
}

final cacheMaintenanceProvider = Provider<CacheMaintenance>(CacheMaintenance.new);
