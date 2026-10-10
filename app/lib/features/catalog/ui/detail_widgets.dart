import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/stale_data.dart';
import '../../../core/network/api_error.dart';
import '../../../core/ui/offline_widgets.dart';
import '../data/catalog_repository.dart';
import '../data/models.dart' show ItemModifier;
import '../domain/item_modifier_format.dart';

export '../../../core/ui/detail_widgets.dart';

const _notFoundMessage = 'No se encontró este elemento del compendio.';

/// Shows [value] with the standard loading and error (retry) states.
class CatalogAsyncBody<T> extends StatelessWidget {
  const CatalogAsyncBody({
    super.key,
    required this.value,
    required this.onRetry,
    required this.builder,
  });

  final AsyncValue<T> value;
  final VoidCallback onRetry;
  final Widget Function(T data) builder;

  @override
  Widget build(BuildContext context) {
    return value.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => CatalogErrorView(error: error, onRetry: onRetry),
      data: (data) => OfflineBannerLayout(
        scopes: [staleTree(CatalogRepository.rootPath)],
        child: builder(data),
      ),
    );
  }
}

class CatalogErrorView extends StatelessWidget {
  const CatalogErrorView({super.key, required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              describeApiError(error, byStatus: const {404: _notFoundMessage}),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Reintentar'),
            ),
          ],
        ),
      ),
    );
  }
}

/// One readable line per item modifier ("+3 Destreza", "Fuerza 19", "+1 CA").
class ModifierLines extends StatelessWidget {
  const ModifierLines(this.modifiers, {super.key});

  final List<ItemModifier> modifiers;

  @override
  Widget build(BuildContext context) {
    final style = Theme.of(context).textTheme.bodyMedium;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (var i = 0; i < modifiers.length; i++)
          Padding(
            padding: const EdgeInsets.only(bottom: 4),
            child: Text(
              '• ${describeItemModifier(modifiers[i])}',
              key: Key('item-modifier-$i'),
              style: style,
            ),
          ),
      ],
    );
  }
}
