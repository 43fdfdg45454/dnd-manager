import 'package:drift/drift.dart';
import 'package:drift_flutter/drift_flutter.dart';

import 'response_cache.dart';

part 'cache_database.g.dart';

/// `cached_responses (key TEXT PK, body TEXT, fetched_at INTEGER)`: one row
/// per GET, with the JSON body as received. No schema per entity.
class CachedResponses extends Table {
  TextColumn get key => text()();

  TextColumn get body => text()();

  /// Milliseconds since the epoch (UTC).
  IntColumn get fetchedAt => integer()();

  @override
  Set<Column<Object>> get primaryKey => {key};
}

@DriftDatabase(tables: [CachedResponses])
class CacheDatabase extends _$CacheDatabase {
  CacheDatabase(super.executor);

  /// The database file of the app (`response_cache.sqlite` in the documents
  /// directory).
  factory CacheDatabase.open() => CacheDatabase(driftDatabase(name: 'response_cache'));

  @override
  int get schemaVersion => 1;
}

/// [ResponseCache] stored in SQLite through drift.
class DriftResponseCache implements ResponseCache {
  DriftResponseCache(this._db, {this.maxEntries = 2000});

  final CacheDatabase _db;

  /// Entries kept by [prune]; the oldest ones are dropped first.
  final int maxEntries;

  @override
  Future<CachedEntry?> read(String key) async {
    final row = await (_db.select(
      _db.cachedResponses,
    )..where((t) => t.key.equals(key))).getSingleOrNull();
    if (row == null) return null;
    return CachedEntry(
      body: row.body,
      fetchedAt: DateTime.fromMillisecondsSinceEpoch(row.fetchedAt, isUtc: true),
    );
  }

  @override
  Future<void> write(String key, String body, DateTime fetchedAt) => _db
      .into(_db.cachedResponses)
      .insertOnConflictUpdate(
        CachedResponsesCompanion.insert(
          key: key,
          body: body,
          fetchedAt: fetchedAt.millisecondsSinceEpoch,
        ),
      );

  @override
  Future<void> clear() => _db.delete(_db.cachedResponses).go();

  @override
  Future<void> clearPrefix(String prefix) async {
    if (prefix.isEmpty) return clear();
    // `substr` instead of LIKE: LIKE is case-insensitive and treats % and _ as
    // wildcards.
    await (_db.delete(
      _db.cachedResponses,
    )..where((t) => t.key.substr(1, prefix.length).equals(prefix))).go();
  }

  /// Keeps the newest [maxEntries] rows (searches and pages add up over time).
  Future<void> prune() async {
    final count = await _db.cachedResponses.count().getSingle();
    if (count <= maxEntries) return;
    await _db.customStatement(
      'DELETE FROM cached_responses WHERE key NOT IN '
      '(SELECT key FROM cached_responses ORDER BY fetched_at DESC LIMIT ?)',
      [maxEntries],
    );
  }

  Future<void> close() => _db.close();
}
