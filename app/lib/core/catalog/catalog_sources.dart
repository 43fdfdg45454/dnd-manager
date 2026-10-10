import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../systems/system_registry.dart';
import 'catalog_models.dart';

/// Sources of the catalog (SRD and content packs) of the default game system,
/// to name the pack a piece of content comes from. Kept alive: it is tiny and
/// every chip reads it. Errors (offline without cache) leave the chips showing
/// nothing rather than failing.
final catalogSourcesProvider = FutureProvider<List<CatalogSource>>(
  (ref) => ref.watch(defaultGameSystemUiProvider).catalogSources(ref),
  retry: (retryCount, error) => null,
);
