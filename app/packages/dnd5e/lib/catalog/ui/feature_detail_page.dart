import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../characters/domain/class_theme.dart';
import '../data/catalog_controllers.dart';
import '../data/models.dart' hide Page;
import 'detail_widgets.dart';

/// One class or subclass feature of the catalog: name, level, class (and
/// subclass) and its rules text.
class FeatureDetailPage extends ConsumerWidget {
  const FeatureDetailPage({super.key, required this.index});

  final String index;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final feature = ref.watch(featureDetailProvider(index));
    return Scaffold(
      appBar: AppBar(title: Text(feature.value?.name ?? 'Rasgo')),
      body: CatalogAsyncBody<Feature>(
        value: feature,
        onRetry: () => ref.invalidate(featureDetailProvider(index)),
        builder: (f) => DetailList(
          children: [
            Text(
              f.name,
              key: const Key('feature-detail-name'),
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            FactRow('Nivel', f.level > 0 ? '${f.level}' : null),
            FactRow(
              'Clase',
              f.classIndex == null
                  ? null
                  : classThemes[f.classIndex]?.labelEs ?? titleFromIndex(f.classIndex!),
            ),
            FactRow('Subclase', f.subclassIndex == null ? null : titleFromIndex(f.subclassIndex!)),
            const SectionTitle('Descripción'),
            f.description.isEmpty ? const Text('Sin descripción.') : Paragraphs(f.description),
          ],
        ),
      ),
    );
  }
}
