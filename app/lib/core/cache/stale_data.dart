import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Which cached resources a screen shows. [path] is an API path (the same as
/// the cache key without the query); with [exact] false every resource below it
/// matches too (`/api/v1/campaigns/c1` covers its characters, lore, maps...).
typedef StaleScope = ({String path, bool exact});

/// Scope covering [path] and everything below it.
StaleScope staleTree(String path) => (path: path, exact: false);

/// Scope covering only [path] (with any query).
StaleScope staleExact(String path) => (path: path, exact: true);

/// Resources whose last read came from the cache because the server was
/// unreachable: cache key -> when that data was received. A later successful
/// read of the same key removes it.
class StaleDataController extends Notifier<Map<String, DateTime>> {
  @override
  Map<String, DateTime> build() => const {};

  /// Records the outcome of a cached read: [staleSince] null means fresh.
  void report(String key, DateTime? staleSince) {
    if (staleSince == null) {
      if (!state.containsKey(key)) return;
      state = {...state}..remove(key);
    } else {
      if (state[key] == staleSince) return;
      state = {...state, key: staleSince};
    }
  }

  void clear() {
    if (state.isNotEmpty) state = const {};
  }
}

final staleDataProvider = NotifierProvider<StaleDataController, Map<String, DateTime>>(
  StaleDataController.new,
);

bool _matches(String key, StaleScope scope) {
  final path = scope.path;
  if (key == path || key.startsWith('$path?')) return true;
  return !scope.exact && key.startsWith('$path/');
}

/// Oldest receive time of the stale data in [scope], or null when everything
/// in it is fresh. This is the "datos obsoletos" indicator the screens show.
final staleSinceProvider = Provider.family<DateTime?, StaleScope>((ref, scope) {
  DateTime? oldest;
  for (final entry in ref.watch(staleDataProvider).entries) {
    if (!_matches(entry.key, scope)) continue;
    if (oldest == null || entry.value.isBefore(oldest)) oldest = entry.value;
  }
  return oldest;
});
