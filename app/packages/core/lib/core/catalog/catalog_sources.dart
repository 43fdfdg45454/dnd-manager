import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../systems/system_registry.dart';
import 'catalog_models.dart';

/// A counter the core bumps when the catalog of the instance changes (a
/// content pack is imported or deleted). The catalog providers of the game
/// systems watch it, so they reload without the core knowing them.
class CatalogRevision extends Notifier<int> {
  @override
  int build() => 0;

  /// The catalog changed: every provider that watches the revision reloads.
  void bump() => state++;
}

final catalogRevisionProvider = NotifierProvider<CatalogRevision, int>(CatalogRevision.new);

/// Sources of the catalog (SRD and content packs) of the default game system,
/// to name the pack a piece of content comes from. Kept alive: it is tiny and
/// every chip reads it. Errors (offline without cache) leave the chips showing
/// nothing rather than failing.
final catalogSourcesProvider = FutureProvider<List<CatalogSource>>((ref) {
  ref.watch(catalogRevisionProvider);
  return ref.watch(defaultGameSystemUiProvider).catalogSources(ref);
}, retry: (retryCount, error) => null);
